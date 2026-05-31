using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Authentication;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddAttritionJwtAuth(
        this IServiceCollection services, IConfiguration config)
    {
        var secret = config["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT Secret not configured");

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var isDevelopment = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);
        if (!isDevelopment)
        {
            if (secret.StartsWith("dev-secret", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Jwt:Secret is still the development default. Set a real secret via the Jwt__Secret environment variable.");
            if (Encoding.UTF8.GetByteCount(secret) < 32)
                throw new InvalidOperationException("Jwt:Secret must be at least 32 bytes.");

            var internalKey = config["Internal:ApiKey"];
            if (!string.IsNullOrEmpty(internalKey) &&
                internalKey.StartsWith("dev-internal", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Internal:ApiKey is still the development default. Set a real key via the Internal__ApiKey environment variable.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidAudience = config["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ClockSkew = TimeSpan.Zero,
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = "username"
                };
                options.Events = new JwtBearerEvents
                {
                    // Web clients authenticate via an HttpOnly cookie; non-browser clients (the Unity
                    // game client) keep using the Authorization bearer header. The header always wins;
                    // we only fall back to the cookie when no bearer token was supplied.
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrEmpty(context.Token))
                        {
                            var cookieToken = context.Request.Cookies[AuthCookies.AccessToken];
                            if (!string.IsNullOrEmpty(cookieToken))
                                context.Token = cookieToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        return services;
    }
}
