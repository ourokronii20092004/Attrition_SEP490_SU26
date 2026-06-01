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

    private async Task SendAsync(object payload, CancellationToken ct)
    {
        try
        {
            await _http.PostAsJsonAsync("api/internal/notifications", payload, JsonOpts, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification dispatch failed (non-fatal).");
        }
    }
}
