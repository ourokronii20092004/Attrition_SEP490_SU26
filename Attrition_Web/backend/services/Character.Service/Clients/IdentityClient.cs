using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Contracts;
using Character.Service.DTOs;

namespace Character.Service.Clients;

/// <summary>
/// Resolves owner usernames from Identity's internal batch endpoint
/// (<c>POST /api/internal/users/batch</c>), guarded by the shared X-Internal-Key.
/// Failure is non-fatal: callers fall back to displaying the raw OwnerId.
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

    /// <summary>Maps owner id → username. Ids that don't resolve are simply absent from the map.</summary>
    public async Task<Dictionary<Guid, string>> ResolveUsernamesAsync(IReadOnlyCollection<Guid> ownerIds, CancellationToken ct)
    {
        var ids = ownerIds.Where(id => id != Guid.Empty).Distinct().Take(200).ToList();
        if (ids.Count == 0) return new();
        try
        {
            var resp = await _http.PostAsJsonAsync("api/internal/users/batch", ids, JsonOpts, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Identity batch lookup returned {Status}; owner usernames will be unresolved", resp.StatusCode);
                return new();
            }
            var envelope = await resp.Content.ReadFromJsonAsync<InternalEnvelope<List<UserSummaryDto>>>(JsonOpts, ct);
            return envelope?.Data?.ToDictionary(u => u.Id, u => u.Username) ?? new();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Identity batch lookup failed; owner usernames will be unresolved");
            return new();
        }
    }
}
