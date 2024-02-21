using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace System.Security.Claims;

/// <summary>
/// A functions middleware that extracts the client principal from the request headers 
/// and sets it as the current principal via the <see cref="ClaimsFeature"/>.
/// </summary>
public class ClientPrincipalMiddleware : IFunctionsWorkerMiddleware
{
    static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Invokes the middleware to extract the client principal from the request headers
    /// </summary>
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if (context.Features.Get<ClaimsFeature>() is not null)
        {
            // We don't set the feature at all if it's already set by some other middleware.
            await next(context);
            return;
        }

        // Based on https://learn.microsoft.com/en-us/azure/static-web-apps/user-information?tabs=javascript#api-functions 
        // Same header applies to Azure Functions as well.

        var req = await context.GetHttpRequestDataAsync();
        if (req is not null &&
            req.Headers.ToDictionary(x => x.Key, x => string.Join(',', x.Value), StringComparer.OrdinalIgnoreCase) is var headers &&
            headers.TryGetValue("x-ms-client-principal", out var msclient) &&
            Convert.FromBase64String(msclient) is var decoded &&
            Encoding.UTF8.GetString(decoded) is var json &&
            JsonSerializer.Deserialize<ClientPrincipal>(json, options) is { } cp)
        {
            var claims = new List<Claim>(cp.claims.Select(c => new Claim(c.typ, c.val)));
            var access_token = headers.TryGetValue($"x-ms-token-{cp.auth_typ}-access-token", out var token) ? token : default;
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                claims,
                cp.auth_typ));

            context.Features.Set(new ClaimsFeature(principal, access_token));
            await next(context);
            return;
        }

        await next(context);
    }

    record ClientClaim(string typ, string val);
    record ClientPrincipal(string auth_typ, ClientClaim[] claims);
}