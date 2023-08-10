using System.Net;
using System.Runtime.InteropServices;

namespace Devlooped;

class HttpClientFactory
{
    static readonly TimeSpan defaultNetworkTimeout = TimeSpan.FromSeconds(1);

    public static HttpClient Default { get; } = Create(defaultNetworkTimeout);

    public static HttpClient Create(TimeSpan networkTimeout)
    {
        var proxy = WebRequest.GetSystemWebProxy();
        var useProxy = !proxy.IsBypassed(new Uri("https://cdn.devlooped.com"));

        HttpMessageHandler handler;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework"))
        {
            // When running on Windows + .NET Framework, this guarantees proper proxy settings behavior automatically
            handler = new WinHttpHandler
            {
                ReceiveDataTimeout = networkTimeout,
                ReceiveHeadersTimeout = networkTimeout,
                SendTimeout = networkTimeout
            };
        }
        else if (useProxy)
        {
            handler = new HttpClientHandler
            {
                UseProxy = true,
                Proxy = proxy,
                DefaultProxyCredentials = CredentialCache.DefaultCredentials
            };
        }
        else
        {
            handler = new HttpClientHandler();
        }

        return new HttpClient(handler)
        {
            // Customize network timeout so we don't become unusable when target is 
            // unreachable (i.e. a proxy prevents access or misconfigured)
            Timeout = networkTimeout
        };
    }
}
