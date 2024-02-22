using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;

namespace Devlooped.Sponsors;

public class ClaimsHttpMessageHandler(IFunctionContextAccessor context) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var claims = context.FunctionContext?.Features.Get<ClaimsFeature>();
        if (claims is { AccessToken.Length: > 0 })
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", claims.AccessToken);

        return base.SendAsync(request, cancellationToken);
    }
}
