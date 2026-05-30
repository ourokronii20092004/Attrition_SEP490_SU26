using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Web;

/// <summary>
/// Dependency-free transient-fault retry for typed HttpClients used by the aggregators
/// (Search/Admin). Retries idempotent GET failures (network error, 5xx, 408) a few times
/// with linear backoff so a briefly-unhealthy downstream degrades gracefully instead of
/// failing the whole fan-out on the first blip. Non-GET requests are never retried.
/// </summary>
public sealed class TransientRetryHandler : DelegatingHandler
{
    private readonly int _maxRetries;
    private readonly TimeSpan _baseDelay;

    public TransientRetryHandler(int maxRetries = 2, int baseDelayMs = 150)
    {
        _maxRetries = maxRetries;
        _baseDelay = TimeSpan.FromMilliseconds(baseDelayMs);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get)
            return await base.SendAsync(request, cancellationToken);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                if (attempt >= _maxRetries || !IsTransient(response))
                    return response;
                response.Dispose();
            }
            catch (HttpRequestException) when (attempt < _maxRetries)
            {
                // fall through to backoff + retry
            }
            catch (TaskCanceledException) when (attempt < _maxRetries && !cancellationToken.IsCancellationRequested)
            {
                // timeout (not caller cancellation) — retry
            }

            await Task.Delay(_baseDelay * (attempt + 1), cancellationToken);
        }
    }

    private static bool IsTransient(HttpResponseMessage response)
    {
        var code = (int)response.StatusCode;
        return code >= 500 || code == 408;
    }
}

public static class ResilienceExtensions
{
    /// <summary>Adds the transient-retry handler to a typed HttpClient registration.</summary>
    public static IHttpClientBuilder AddTransientRetry(this IHttpClientBuilder builder) =>
        builder.AddHttpMessageHandler(() => new TransientRetryHandler());
}
