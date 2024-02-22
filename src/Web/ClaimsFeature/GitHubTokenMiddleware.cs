using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Azure.Functions.Worker;
using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace System.Security.Claims;

/// <summary>
/// Sets the <see cref="ClaimsFeature"/> if the incoming request has a valid GitHub token.
/// </summary>
public class GitHubTokenMiddleware(IHttpClientFactory httpFactory) : IFunctionsWorkerMiddleware
{
    const string Scheme = "Bearer ";

    /// <summary>
    /// Invokes the middleware to extract the client principal from the auth bearer token.
    /// </summary>
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if (context.Features.Get<ClaimsFeature>() is not null)
        {
            // We don't set the feature at all if it's already set by some other middleware.
            await next(context);
            return;
        }

        var req = await context.GetHttpRequestDataAsync();
        if (req is not null &&
            req.Headers.TryGetValues("Authorization", out var values) && 
            values is { } auths && 
            auths.FirstOrDefault() is { Length: > 0 } auth &&
            auth.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            // An example of this invocation style is a CLI app authenticating
            // using device flow
            using var http = httpFactory.CreateClient();

            http.BaseAddress = new Uri("https://api.github.com");
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Devlooped.SponsorLink", ThisAssembly.Info.InformationalVersion));
            http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth);
            
            var resp = await http.GetAsync("/user");

            // NOTE: by getting the user's profile, we're also verifying the token
            if (resp is { StatusCode: HttpStatusCode.OK, Content: { } content })
            {
                var gh = await content.ReadAsStringAsync();
                var claims = new List<Claim>();
                var doc = JsonDocument.Parse(gh);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind != JsonValueKind.Object &&
                        prop.Value.ValueKind != JsonValueKind.Array &&
                        prop.Value.ToString() is { Length: > 0 } value)
                    {
                        // For compatiblity with the client principal populated claims.
                        claims.Add(new Claim("urn:github:" + prop.Name, value));
                    }
                }

                context.Features.Set(new ClaimsFeature(new ClaimsPrincipal(
                    new ClaimsIdentity(claims, "github")),
                    auth[Scheme.Length..]));

                await next(context);
                return;
            } 
            else 
            {
                var error = await resp.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(error))
                    error = resp.ReasonPhrase;

                context.InstanceServices.GetRequiredService<ILogger<GitHubTokenMiddleware>>()
                    .LogWarning("Failed to authenticate with GitHub using the provided bearer token: " + error);
            }
        }

        await next(context);
    }
}