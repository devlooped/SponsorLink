using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GitCredentialManager;

namespace Devlooped.Sponsors;

public interface IGitHubAppAuthenticator
{
    Task<string?> AuthenticateAsync(string clientId, IProgress<string> progress, bool interactive, string @namespace = GitHubAppAuthenticator.DefaultNamespace, ICredentialStore? credentials = default);
}

public class GitHubAppAuthenticator(IHttpClientFactory httpFactory) : IGitHubAppAuthenticator
{
    /// <summary>
    /// Default namespace used to scope credentials stored by this authenticator.
    /// </summary>
    public const string DefaultNamespace = "com.devlooped";

    static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter<AuthError>() },
        WriteIndented = true
    };

    // Don't allow concurrent authentications, since that would be quite a 
    // confusing experience for the user, with multiple browser tabs opening.
    readonly SemaphoreSlim semaphore = new(1, 1);

    public async Task<string?> AuthenticateAsync(string clientId, IProgress<string> progress, bool interactive, string @namespace = DefaultNamespace, ICredentialStore? credentials = default)
    {
        using var http = httpFactory.CreateClient("GitHub");

        // Check existing creds, if any
        var store = credentials ?? CredentialManager.Create(@namespace);
        // We use the client ID to persist the token, so it can be used across different apps.
        var creds = store.Get("https://github.com", clientId);

        if (creds != null)
        {
            // Try using the creds to see if they are still valid.
            var request = new HttpRequestMessage(interactive ? HttpMethod.Get : HttpMethod.Head, "https://api.github.com/user");
            request.Headers.Authorization = new("Bearer", creds.Password);
            if (await http.SendAsync(request) is HttpResponseMessage { IsSuccessStatusCode: true } response)
            {
                if (interactive)
                {
                    var user = await response.Content.ReadFromJsonAsync<JsonElement>();
                    progress.Report($":check_mark_button: logged in as [lime]{user.GetProperty("login").GetString()}[/]");
                }

                return creds.Password;
            }
            else
            {
                // If the token is invalid, remove it from the store.
                store.Remove("https://github.com", clientId);
            }
        }

        if (!interactive)
            return null;

        // Perform device flow auth. See https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps#device-flow

        var codeUrl = $"https://github.com/login/device/code?client_id={clientId}&scope=read:user,user:email,read:org";
        var auth = await (await http.PostAsync(codeUrl, null)).Content.ReadFromJsonAsync<Auth>(options);
        if (auth is null)
        {
            progress.Report($":cross_mark: could not retrieve device code to authenticate with GitHub");
            return null;
        }

        TryCopy(auth.verification_uri, auth.user_code, progress);

        await semaphore.WaitAsync();
        try
        {
            // Start the browser to the verification URL
            try
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo(auth!.verification_uri) { UseShellExecute = true });
            }
            catch (Exception)
            {
                progress.Report($":globe_with_meridians: Please navigate to the following page and use the code above: {auth!.verification_uri}");
            }

            AuthCode? code;
            do
            {
                // Be gentle with the backend, wait for the interval before polling again.
                await Task.Delay(TimeSpan.FromSeconds(auth!.interval));

                var url = $"https://github.com/login/oauth/access_token?client_id={clientId}&device_code={auth.device_code}&grant_type=urn:ietf:params:oauth:grant-type:device_code";

                code = await (await http.PostAsync(url, null)).Content.ReadFromJsonAsync<AuthCode>(options);

                // Render status and code again, just in case.
                //AnsiConsole.Write(new JsonText(JsonSerializer.Serialize(code, options)));
                //AnsiConsole.WriteLine();

                if (code!.error == AuthError.slow_down && code.interval is int interval)
                {
                    // This is per the docs, we should slow down the polling.
                    await Task.Delay(TimeSpan.FromSeconds(interval));
                }
                else if (code.error == AuthError.expired_token)
                {
                    // We need an entirely new code, start over.
                    auth = await (await http.PostAsync(codeUrl, null)).Content.ReadFromJsonAsync<Auth>();
                    if (auth is null)
                    {
                        progress.Report($":cross_mark: could not retrieve device code to authenticate with GitHub");
                        return null;
                    }

                    TryCopy(auth.verification_uri, auth.user_code, progress);
                }
                // Continue while we have an error, meaning the code has not been authorized yet.
            } while (code.error != null);

            // At this point, we should have a valid access token with the right scopes.
            http.DefaultRequestHeaders.Authorization = new("Bearer", code.access_token);
            if (await http.GetAsync("https://api.github.com/user") is HttpResponseMessage { IsSuccessStatusCode: true } response)
            {
                var user = await response.Content.ReadFromJsonAsync<JsonElement>();
                progress.Report($":check_mark_button: logged in as [lime]{user.GetProperty("login").GetString()}[/]");

                store.AddOrUpdate("https://github.com", clientId, code.access_token);
                return code.access_token;
            }

            return null;
        }
        finally
        {
            semaphore.Release();
        }
    }

    static void TryCopy(string url, string code, IProgress<string> progress)
    {
        try
        {
            // best-effort to copy the device code to the clipboard
            var clip = System.Diagnostics.Process.Start(new ProcessStartInfo("pwsh",
                $"""
                -c "set-clipboard -value '{code}'"
                """)
            {
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            clip?.WaitForExit();
            if (clip?.ExitCode == 0)
                progress.Report($":clipboard: copied device code to clipboard for pasting in at [link]{url}[/]: [lime]{code}[/]");
            else
                progress.Report($":clipboard: copy device code to clipboard and paste it at [link]{url}[/]: [lime]{code}[/]");
        }
        catch (Exception)
        {
            progress.Report($":clipboard: copy device code to clipboard and paste it at [link]{url}[/]: [lime]{code}[/]");
        }
    }

    record Auth(string device_code, string user_code, string verification_uri, int interval, int expires_in);
    record AuthCode(string? access_token, string? token_type, string? scope, AuthError? error, string? error_description, int? interval);
    enum AuthError
    {
        authorization_pending,
        slow_down,
        expired_token,
        unsupported_grant_type,
        incorrect_client_credentials,
        incorrect_device_code,
        access_denied,
        device_flow_disabled
    }
}
