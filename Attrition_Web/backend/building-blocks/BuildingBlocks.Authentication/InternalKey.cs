using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Authentication;

/// <summary>
/// Shared validation for the service-to-service <c>X-Internal-Key</c> header.
/// Fails closed when the key is unset and compares in constant time to avoid a timing oracle.
///
/// Blast-radius seam: a service may set a dedicated key under <c>Internal:ApiKey:&lt;Service&gt;</c>
/// (e.g. <c>Internal__ApiKey__Character</c>). When present it is accepted IN ADDITION to the shared
/// <c>Internal:ApiKey</c>, so per-service keys can be rolled out one caller/callee pair at a time
/// without a flag-day. Once every caller of a service uses its dedicated key, the shared key can be
/// dropped from that service to contain the blast radius if one key leaks. See docs/design-choices.
/// </summary>
public static class InternalKey
{
    private const string HeaderName = "X-Internal-Key";

    public static bool Validate(HttpRequest request, IConfiguration config, string? service = null)
    {
        if (!request.Headers.TryGetValue(HeaderName, out var got)) return false;
        var gotValue = got.ToString();
        if (string.IsNullOrEmpty(gotValue)) return false;

        // Accept the per-service key (if configured) or the shared key. Both are checked in
        // constant time; we never short-circuit on a length/prefix difference.
        var serviceKey = service is null ? null : config[$"Internal:ApiKey:{service}"];
        var sharedKey = config["Internal:ApiKey"];

        var ok = false;
        if (!string.IsNullOrEmpty(serviceKey)) ok |= ConstantTimeEquals(serviceKey, gotValue);
        if (!string.IsNullOrEmpty(sharedKey)) ok |= ConstantTimeEquals(sharedKey, gotValue);
        return ok;
    }

    private static bool ConstantTimeEquals(string expected, string got)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var gotBytes = Encoding.UTF8.GetBytes(got);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, gotBytes);
    }
}
