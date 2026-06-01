using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;

namespace BuildingBlocks.Web;

/// <summary>
/// Hardening headers for user-uploaded media served statically or via PhysicalFile.
/// Defense-in-depth against polyglot files (a valid image/audio that is also valid HTML/JS):
///  - <c>X-Content-Type-Options: nosniff</c> stops the browser re-interpreting the declared
///    content-type as HTML.
///  - A locked-down <c>Content-Security-Policy</c> (<c>default-src 'none'; sandbox</c>) means that
///    even if a response were somehow treated as a document, no script could execute and it can't
///    frame/navigate. Uploads are inert data, so 'none' is safe and maximally strict.
/// </summary>
public static class MediaSecurityHeaders
{
    public const string Csp = "default-src 'none'; sandbox; frame-ancestors 'none'";

    /// <summary>Apply to a controller response (PhysicalFile/FileResult media endpoints).</summary>
    public static void Apply(HttpResponse response)
    {
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["Content-Security-Policy"] = Csp;
    }

    /// <summary>OnPrepareResponse callback for UseStaticFiles serving uploaded media.</summary>
    public static void OnPrepareStaticResponse(StaticFileResponseContext ctx)
    {
        ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Context.Response.Headers["Content-Security-Policy"] = Csp;
    }
}
