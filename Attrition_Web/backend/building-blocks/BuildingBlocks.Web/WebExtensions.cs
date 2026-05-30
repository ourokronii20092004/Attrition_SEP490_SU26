using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using BuildingBlocks.Web.Filters;

namespace BuildingBlocks.Web;

public static class WebExtensions
{
    /// <summary>
    /// Registers controllers with the shared error-handling conventions: the null-[FromBody] guard
    /// (400 envelope instead of a downstream NRE → 500) and the validation-error envelope
    /// (ApiResponse instead of ProblemDetails). Use in place of <c>AddControllers()</c>.
    /// </summary>
    public static IMvcBuilder AddAttritionControllers(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = ValidationResponseFactory.Create;
        });

        return services.AddControllers(options =>
        {
            options.Filters.Add<RejectNullBodyFilter>();
        });
    }

    /// <summary>Swagger with JWT bearer auth button, titled per service.</summary>
    public static IServiceCollection AddAttritionSwagger(this IServiceCollection services, string title)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = title, Version = "v1" });

            var scheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT bearer token issued by Identity.Service",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            };
            c.AddSecurityDefinition("Bearer", scheme);
            c.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
        });
        return services;
    }

    /// <summary>Error envelope + Swagger (dev) + auth pipeline + controllers + /health, in the right order.</summary>
    public static WebApplication UseAttritionPipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
        return app;
    }
}
