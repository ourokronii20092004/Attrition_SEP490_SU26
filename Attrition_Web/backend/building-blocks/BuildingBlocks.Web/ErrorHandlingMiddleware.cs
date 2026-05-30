using System.Net;
using System.Text.Json;
using BuildingBlocks.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Web;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try { await _next(context); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            if (context.Response.HasStarted)
                throw;
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            var response = new ApiResponse(false, "An unexpected error occurred");
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
