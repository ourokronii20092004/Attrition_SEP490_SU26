using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Authentication;

/// <summary>
/// Cookie names and helpers for HttpOnly cookie auth. The web app authenticates via these
/// cookies; the Unity game client keeps using the Authorization bearer header (see the
/// OnMessageReceived fallback in <see cref="AuthenticationExtensions"/>).
/// </summary>
public static class AuthCookies
{
    public const string AccessToken = "attrition_access";
    public const string RefreshToken = "attrition_refresh";
    /// <summary>Non-HttpOnly, readable by JS for the double-submit CSRF check.</summary>
    public const string Csrf = "attrition_csrf";
    public const string CsrfHeader = "X-CSRF";

    private static bool IsProd =>
        !string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Development", StringComparison.OrdinalIgnoreCase);

    public static void SetAuth(HttpResponse res, string accessToken, string refreshToken,
        TimeSpan accessTtl, TimeSpan refreshTtl, string csrf)
    {
        res.Cookies.Append(AccessToken, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = IsProd,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = accessTtl,
        });
        // Refresh cookie is only ever sent to the refresh endpoint, shrinking its exposure.
        res.Cookies.Append(RefreshToken, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = IsProd,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth/refresh",
            MaxAge = refreshTtl,
        });
        // Readable by JS so the SPA can echo it back in the X-CSRF header (double-submit).
        res.Cookies.Append(Csrf, csrf, new CookieOptions
        {
            HttpOnly = false,
            Secure = IsProd,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = refreshTtl,
        });
    }

    public static void Clear(HttpResponse res)
    {
        res.Cookies.Delete(AccessToken, new CookieOptions { Path = "/" });
        res.Cookies.Delete(RefreshToken, new CookieOptions { Path = "/api/auth/refresh" });
        res.Cookies.Delete(Csrf, new CookieOptions { Path = "/" });
    }
}
