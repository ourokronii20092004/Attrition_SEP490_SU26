using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Contracts;

namespace Forum.Service.Clients;

/// <summary>Minimal shape of Identity's internal user summary (id + current avatar).</summary>
public record UserSummary(Guid Id, string Username, string? DisplayName, string? AvatarUrl, string Role);

/// <summary>
/// Resolves current author avatars from Identity's internal batch endpoint
/// (<c>POST /api/internal/users/batch</c>), guarded by the shared X-Internal-Key. Forum posts only
/// store the author's name at write time, so avatars are looked up fresh on read. Failure is
/// non-fatal: callers fall back to whatever is stored (typically just initials).
/// </summary>
public class IdentityClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly ILogger<IdentityClient> _logger;

    public IdentityClient(HttpClient http, IConfiguration config, ILogger<IdentityClient> logger)
    {
        _http = http;
        _logger = logger;
        var key = config["Internal:ApiKey"];
        if (!string.IsNullOrEmpty(key))
            _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Internal-Key", key);
    }

    /// <summary>Maps author id → summary. Ids that don't resolve are simply absent from the map.</summary>
    public async Task<Dictionary<Guid, UserSummary>> ResolveUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        var ids = userIds.Where(id => id != Guid.Empty).Distinct().Take(200).ToList();
        if (ids.Count == 0) return new();
        try
        {
            var resp = await _http.PostAsJsonAsync("api/internal/users/batch", ids, JsonOpts, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Identity batch lookup returned {Status}; author avatars will be unresolved", resp.StatusCode);
                return new();
            }
            var envelope = await resp.Content.ReadFromJsonAsync<InternalEnvelope<List<UserSummary>>>(JsonOpts, ct);
            return envelope?.Data?.ToDictionary(u => u.Id) ?? new();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Identity batch lookup failed; author avatars will be unresolved");
            return new();
        }
    }
}
