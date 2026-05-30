using System.Text.Json;

namespace Gateway;

/// <summary>
/// Gives the gateway the same <c>{ "Success": false, "Error": "..." }</c> error contract every
/// downstream service uses. YARP emits a raw, empty-bodied 502/504 when a destination is
/// unreachable, and the rate limiter emits an empty 429 — clients that expect the envelope choke
/// on exactly the errors they are most likely to hit at the edge.
///
/// Relies on the fact that YARP proxy failures and rate-limit rejections set the status code but do
/// NOT start the response body, so we can write a body after _next without buffering. A successful
/// proxied response has already started (HasStarted == true) and is streamed through untouched —
/// this never interferes with range/SSE/streamed responses.
/// </summary>
public sealed class GatewayErrorMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GatewayErrorMiddleware> _logger;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = null };

    public GatewayErrorMiddleware(RequestDelegate next, ILogger<GatewayErrorMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled gateway exception");
            if (context.Response.HasStarted) throw;
            await WriteEnvelopeAsync(context, StatusCodes.Status502BadGateway);
            return;
        }

        // Proxy/ratelimit error with no body of its own → wrap it in the envelope.
        if (!context.Response.HasStarted
            && context.Response.StatusCode >= 400
            && (context.Response.ContentLength is null or 0)
            && string.IsNullOrEmpty(context.Response.ContentType))
        {
            await WriteEnvelopeAsync(context, context.Response.StatusCode);
        }
    }

    private static Task WriteEnvelopeAsync(HttpContext context, int status)
    {
        var message = status switch
        {
            429 => "Too many requests. Please slow down and try again shortly.",
            502 => "An upstream service is currently unavailable.",
            503 => "The service is temporarily unavailable.",
            504 => "An upstream service did not respond in time.",
            401 => "Authentication is required.",
            403 => "You do not have permission to perform this action.",
            _ => "The request could not be completed."
        };
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new { Success = false, Error = message }, Json));
    }
}
