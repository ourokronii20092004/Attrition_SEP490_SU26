using Gateway;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// The gateway proxies uploads (assets up to 50MB, music up to 100MB). Without raising
// Kestrel's default ~28.6MB body limit, large uploads 413 at the gateway before reaching
// the owning service. Configurable via Gateway:MaxRequestBodySizeMB (default 110MB headroom).
var maxBodyMb = long.TryParse(builder.Configuration["Gateway:MaxRequestBodySizeMB"], out var mb) && mb > 0 ? mb : 110;
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxBodyMb * 1024 * 1024);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// NOTE: we intentionally do NOT use UseForwardedHeaders / trust X-Forwarded-For. With
// KnownProxies/KnownNetworks cleared it would rewrite Connection.RemoteIpAddress from a
// client-settable XFF header, letting an attacker cycle fake IPs to evade the rate limiter.
// The rate-limit key comes from CF-Connecting-IP (set by Cloudflare, the only public ingress,
// and unforgeable through the tunnel); see ClientIp below.

// Centralized CORS — only the gateway sets CORS headers; downstream services do not.
// Origins come from the root .env via CORS_ORIGINS (comma-separated), surfaced as Cors:Origins.
// Falls back to the legacy Cors:AllowedOrigins array, then a dev default.
var allowedOrigins =
    (builder.Configuration["Cors:Origins"]?
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    ?? builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000", "https://attrition.io.vn" };

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    // Expose Retry-After so the SPA can show a rate-limit countdown on cross-origin (dev) calls.
    .WithExposedHeaders("Retry-After")));

static string ClientIp(HttpContext ctx)
{
    // The ONLY public ingress is the Cloudflare tunnel → nginx → gateway. Cloudflare sets
    // CF-Connecting-IP to the real client IP and overwrites any client-supplied value, so it
    // can't be forged through the tunnel — it's the trustworthy rate-limit key.
    //
    // We deliberately do NOT trust X-Forwarded-For or X-Real-IP for keying: both are
    // client-settable headers that survive a request made directly to nginx/gateway (bypassing
    // Cloudflare), so an attacker could cycle them to evade per-IP limits. When CF-Connecting-IP
    // is absent (non-Cloudflare/dev/LAN path) we fall back to the raw connection IP, which a
    // client cannot spoof. Worst case off-Cloudflare is over-aggressive limiting, never bypass.
    var cf = ctx.Request.Headers["CF-Connecting-IP"].ToString();
    if (!string.IsNullOrWhiteSpace(cf)) return cf;
    return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

// Per-IP rate limits (requests/minute). Generous defaults suit an actively-used/tested app while
// still capping runaway abuse; override via RateLimit:AuthPerMinute / RateLimit:GlobalPerMinute.
var authPerMinute = int.TryParse(builder.Configuration["RateLimit:AuthPerMinute"], out var apm) && apm > 0 ? apm : 30;
var globalPerMinute = int.TryParse(builder.Configuration["RateLimit:GlobalPerMinute"], out var gpm) && gpm > 0 ? gpm : 300;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Tell the client how long to wait (seconds until the window resets) so it can show a countdown.
    // The gateway error middleware also forwards this into ?retry= when it redirects a navigation.
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        return ValueTask.CompletedTask;
    };

    // Strict limiter for auth endpoints (login/register/forgot-password) to blunt brute-force.
    options.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ClientIp(ctx),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    // Global per-IP fallback so no route is entirely unprotected.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientIp(ctx),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = globalPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

app.UseMiddleware<GatewayErrorMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.MapReverseProxy();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.Run();