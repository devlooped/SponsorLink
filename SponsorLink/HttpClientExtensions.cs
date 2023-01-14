using System.Net.Http.Headers;

namespace Devlooped.SponsorLink;

public static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> PostAsync(this HttpClient http, string? requestUri, HttpContent? content, string? bearerToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Content = content;
        if (!string.IsNullOrEmpty(bearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        return http.SendAsync(request);
    }
}
