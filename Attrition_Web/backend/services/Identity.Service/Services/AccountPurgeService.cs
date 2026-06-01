using Identity.Service.Repositories;

namespace Identity.Service.Services;

/// <summary>
/// PROB-4: purges soft-deleted accounts after their 90-day recovery window. Runs daily. For each
/// user with IsDeleted and DeletedAt older than 90 days, anonymizes PII (tombstone) so the account
/// can no longer be recovered or identified, while leaving the row in place to keep authored
/// content (posts/contributions) attributable to a stable "Deleted User" id.
/// </summary>
public class AccountPurgeService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RecoveryWindow = TimeSpan.FromDays(90);

    private readonly IServiceProvider _services;
    private readonly ILogger<AccountPurgeService> _logger;

    public AccountPurgeService(IServiceProvider services, ILogger<AccountPurgeService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small startup delay so the purge sweep doesn't contend with migrations/seeding on boot.
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PurgeExpiredAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Account purge sweep failed; will retry next interval."); }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task PurgeExpiredAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var cutoff = DateTime.UtcNow - RecoveryWindow;
        // Not yet tombstoned: a tombstoned row has PasswordHash null + a deleted_user_ username.
        var expired = await userRepo.ListAsync(u =>
            u.IsDeleted && u.DeletedAt != null && u.DeletedAt < cutoff && u.PasswordHash != null);

        if (expired.Count == 0) return;

        foreach (var user in expired)
        {
            user.Username = $"deleted_user_{user.Id.ToString()[..8]}";
            user.DisplayName = "Deleted User";
            user.Email = null;
            user.PendingEmail = null;
            user.PasswordHash = null;
            user.Bio = null;
            user.AvatarPath = null;
            user.BackgroundUrl = null;
            user.GoogleId = null;
            user.GoogleAvatarUrl = null;
            user.RefreshToken = null;
            user.RefreshTokenExpiresAt = null;
            user.DeletionConfirmToken = null;
            user.DeletionConfirmTokenExpiry = null;
            await userRepo.UpdateAsync(user);
        }

        _logger.LogInformation("Account purge: tombstoned {Count} account(s) past the 90-day recovery window.", expired.Count);
    }
}
