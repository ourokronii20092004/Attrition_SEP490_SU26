using System.Net.Http.Json;
using System.Text.Json;

namespace Forum.Service.Clients;

/// <summary>
/// Fire-and-forget notifications to Identity's internal endpoint
/// (<c>POST /api/internal/notifications</c>), guarded by the shared X-Internal-Key.
/// Failure is non-fatal — a missed notification must never break posting.
/// </summary>
public class NotificationClient
{
    /// <summary>Recipients per bulk request. Must stay at or below Identity's
    /// InternalNotificationsController.MaxBulkRecipients, which rejects anything larger.</summary>
    private const int BulkBatchSize = 500;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly ILogger<NotificationClient> _logger;

    public NotificationClient(HttpClient http, IConfiguration config, ILogger<NotificationClient> logger)
    {
        _http = http;
        _logger = logger;
        var key = config["Internal:ApiKey"];
        if (!string.IsNullOrEmpty(key))
            _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Internal-Key", key);
    }

    /// <summary>Notify by recipient user id (replies).</summary>
    public Task NotifyUserAsync(Guid userId, string type, string message, string? link, string? actorName, CancellationToken ct)
        => SendAsync(new { type, message, link, actorName, userId }, ct);

    /// <summary>Notify by @username (mentions); Identity resolves the user.</summary>
    public Task NotifyUsernameAsync(string username, string type, string message, string? link, string? actorName, CancellationToken ct)
        => SendAsync(new { type, message, link, actorName, username }, ct);

    /// <summary>
    /// Notify many recipients with one shared message. Used for thread-subscriber fan-out, where
    /// per-recipient calls would add one round-trip each to the reply request. Sent in batches so a
    /// heavily-followed thread stays under Identity's per-request recipient cap instead of being
    /// rejected wholesale. No-op for an empty list.
    /// </summary>
    public async Task NotifyUsersAsync(IReadOnlyCollection<Guid> userIds, string type, string message, string? link, string? actorName, CancellationToken ct)
    {
        foreach (var batch in userIds.Chunk(BulkBatchSize))
            await SendAsync(new { type, message, link, actorName, userIds = batch }, ct, "api/internal/notifications/bulk");
    }

    private async Task SendAsync(object payload, CancellationToken ct, string path = "api/internal/notifications")
    {
        try
        {
            await _http.PostAsJsonAsync(path, payload, JsonOpts, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification dispatch failed (non-fatal).");
        }
    }
}
