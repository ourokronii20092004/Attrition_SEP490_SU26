using System.Threading.RateLimiting;
using Gateway;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// The gateway proxies uploads (assets up to 50MB, music up to 100MB). Without raising
// Kestrel's default ~28.6MB body limit, large uploads 413 at the gateway before reaching
// the owning service. Configurable via Gateway:MaxRequestBodySizeMB (default 110MB headroom).
var maxBodyMb = long.TryParse(builder.Configuration["Gateway:MaxRequestBodySizeMB"], out var mb) && mb > 0 ? mb : 110;
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxBodyMb * 1024 * 1024);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Honor X-Forwarded-For from the nginx/cloudflared edge so the rate limiter partitions by the real
// client IP (ClientIp reads Connection.RemoteIpAddress, which UseForwardedHeaders rewrites) instead
// of lumping every request behind the proxy into a single bucket.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

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
    .AllowCredentials()));

static string ClientIp(HttpContext ctx) =>
    ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Strict limiter for auth endpoints (login/register/forgot-password) to blunt brute-force.
    options.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ClientIp(ctx),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    // Global per-IP fallback so no route is entirely unprotected.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientIp(ctx),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<GatewayErrorMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.MapReverseProxy();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.Run();
