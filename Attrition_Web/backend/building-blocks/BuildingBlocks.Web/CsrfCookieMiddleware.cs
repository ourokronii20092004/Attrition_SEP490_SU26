using BuildingBlocks.Authentication;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Web;

/// <summary>
/// Double-submit CSRF guard for cookie-authenticated requests. When a request carries the
/// access cookie but NO Authorization header (a browser using cookie auth) and uses an unsafe
/// verb, the X-CSRF header must equal the readable CSRF cookie. A cross-site attacker can ride
/// the cookie but cannot read it to forge the header, so the request is rejected.
///
/// Bearer-header requests (the Unity game client, API consumers) are exempt — an attacker can't
/// set an Authorization header cross-site, so they can't be CSRF'd.
/// </summary>
public sealed class CsrfCookieMiddleware
{
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };

    private readonly RequestDelegate _next;
    public CsrfCookieMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx)
    {
        var req = ctx.Request;
        var usesCookieAuth =
            string.IsNullOrEmpty(req.Headers.Authorization) &&
            !string.IsNullOrEmpty(req.Cookies[AuthCookies.AccessToken]);

        if (usesCookieAuth && !SafeMethods.Contains(req.Method))
        {
            var cookie = req.Cookies[AuthCookies.Csrf];
            var header = req.Headers[AuthCookies.CsrfHeader].ToString();
            if (string.IsNullOrEmpty(cookie) || !CryptoEquals(cookie, header))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(
                    "{\"success\":false,\"error\":\"CSRF validation failed.\"}");
                return;
            }
        }

        await _next(ctx);
    }

    private static bool CryptoEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
