using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Authentication;

/// <summary>
/// Shared validation for the service-to-service <c>X-Internal-Key</c> header.
/// Fails closed when the key is unset and compares in constant time to avoid a timing oracle.
/// </summary>
public static class InternalKey
{
    private const string HeaderName = "X-Internal-Key";

    public static bool Validate(HttpRequest request, IConfiguration config)
    {
        var expected = config["Internal:ApiKey"];
        if (string.IsNullOrEmpty(expected)) return false;
        if (!request.Headers.TryGetValue(HeaderName, out var got)) return false;

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var gotBytes = Encoding.UTF8.GetBytes(got.ToString());
        return CryptographicOperations.FixedTimeEquals(expectedBytes, gotBytes);
    }
}
