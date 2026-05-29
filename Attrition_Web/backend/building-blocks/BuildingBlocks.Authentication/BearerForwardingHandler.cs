using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Authentication;

/// <summary>
/// Forwards the caller's bearer token to downstream services so aggregators
/// (Search/Admin) call owning services as the original principal.
/// </summary>
public sealed class BearerForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _accessor;

    public BearerForwardingHandler(IHttpContextAccessor accessor) => _accessor = accessor;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var auth = _accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(auth) && request.Headers.Authorization is null)
            request.Headers.TryAddWithoutValidation("Authorization", auth);

        return base.SendAsync(request, cancellationToken);
    }
}
