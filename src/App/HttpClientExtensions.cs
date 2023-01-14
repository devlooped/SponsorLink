using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace App;

static class HttpClientExtensions
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
