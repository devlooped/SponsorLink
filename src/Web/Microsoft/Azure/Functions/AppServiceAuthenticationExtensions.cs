using System;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Implements app service authentication for Azure Functions, by automatically 
/// populating a <see cref="ClaimsPrincipal"/> from the request headers 
/// into the <see cref="FunctionContext.Features"/>.
/// </summary>
/// <remarks>
/// The populated <see cref="ClaimsPrincipal"/> can be retrieved from the function 
/// context using <see cref="FunctionContext.Features"/>. For example:
/// <code>
/// ClaimsPrincipal? principal = functionContext.Features.Get&lt;ClaimsPrincipal&gt;();
/// </code>
/// See https://learn.microsoft.com/en-us/azure/app-service/configure-authentication-user-identities.
/// </remarks>
public static partial class AppServiceAuthenticationExtensions
{
    /// <summary>
    /// Parses existing app service authentication headers and sets them as 
    /// <see cref="FunctionContext.Features"/>.Set(<see cref="ClaimsPrincipal"/>) and 
    /// <see cref="FunctionContext.Features"/>.Set(<see cref="AccessToken"/>) if present.
    /// </summary>
    public static IFunctionsWorkerApplicationBuilder UseAppServiceAuthentication(this IFunctionsWorkerApplicationBuilder builder)
        => builder.UseMiddleware<ClientPrincipalMiddleware>();

    class ClientPrincipalMiddleware : IFunctionsWorkerMiddleware
    {
        static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web);

        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            if (context.Features.Get<ClaimsPrincipal>() is not null)
            {
                // Already set by some other component.
                await next(context);
                return;
            }

            var req = await context.GetHttpRequestDataAsync();
            if (req is not null &&
                req.Headers.ToDictionary(x => x.Key, x => string.Join(",", x.Value), StringComparer.OrdinalIgnoreCase) is var headers &&
                headers.TryGetValue("x-ms-client-principal", out var msclient) &&
                Convert.FromBase64String(msclient) is var decoded &&
                Encoding.UTF8.GetString(decoded) is var json &&
                JsonSerializer.Deserialize<ClientPrincipal>(json, options) is { } cp)
            {
                var principal = new ClaimsPrincipal(new ClaimsIdentity(
                    cp.claims.Select(c => new Claim(c.typ, c.val)),
                    cp.auth_typ));

                context.Features.Set(principal);

                if (headers.TryGetValue($"x-ms-token-{cp.auth_typ}-access-token", out var token))
                    context.Features.Set(new AccessToken(token, DateTimeOffset.MaxValue));
            }

            await next(context);
        }

        record ClientClaim(string typ, string val);
        record ClientPrincipal(string auth_typ, ClientClaim[] claims);
    }
}