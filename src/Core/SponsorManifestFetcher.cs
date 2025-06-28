using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Devlooped.Sponsors;

/// <summary>
/// Allows synchronizing client sponsor manifests.
/// </summary>
public class SponsorManifestFetcher
{
    /// <summary>
    /// Status of a manifest refresh operation.
    /// </summary>
    public enum Status
    {
        /// <summary>
        /// The user is not sponsoring (directly or indirectly via org or contributions)
        /// </summary>
        NotSponsoring,
        /// <summary>
        /// Unexpected error during synchronization with the manifest issuer backend.
        /// </summary>
        SyncFailure,
        /// <summary>
        /// Synchronization was successful and token is valid.
        /// </summary>
        Success,
    }

    /// <summary>
    /// Refreshes the sponsor manifest for the given sponsorable account.
    /// </summary>
    /// <param name="accessToken">The access token to use for fetching the manifest, must be an OAuth token issued by GitHub for the sponsorable app, representing the sponsoring user.</param>
    /// <param name="manifest">The SponsorLink manifest provided by the sponsorable account.</param>
    /// <param name="jwt">The sponsor manifest token, if sponsoring.</param>
    /// <returns>The status of the manifest synchronization and the optional JWT of the authenticated user if validation succeeded.</returns>
    public static async Task<(Status, string?)> FetchAsync(SponsorableManifest manifest, string accessToken, HttpClient? http = default)
    {
        var disposeHttp = http == null;
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(manifest.Issuer), "me"));
            request.Headers.Authorization = new("Bearer", accessToken);
            request.Headers.Accept.Add(new("application/jwt"));
            var response = await (http ??= new HttpClient()).SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return (Status.NotSponsoring, default);

            if (!response.IsSuccessStatusCode)
                return (Status.SyncFailure, default);

            var jwt = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(jwt))
                return (Status.SyncFailure, default);

            if (new JsonWebTokenHandler().CanReadToken(jwt) == false)
                return (Status.SyncFailure, default);

            try
            {
                // verify no tampering with the manifest
                var claims = manifest.Validate(jwt, out var sectoken);

                return (Status.Success, jwt);
            }
            catch (SecurityTokenException)
            {
                return (Status.SyncFailure, default);
            }
        }
        finally
        {
            if (disposeHttp)
                http?.Dispose();
        }
    }
}
