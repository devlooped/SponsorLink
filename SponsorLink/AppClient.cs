using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Octokit;

namespace Devlooped.SponsorLink;

public record AppClient(SponsorsRepository Repository, SecurityManager Security, IHttpClientFactory HttpFactory)
{
    static readonly ProductInfoHeaderValue userAgent = new ProductInfoHeaderValue("SponsorLink", new Version(ThisAssembly.Info.Version).ToString(2));
    static readonly Octokit.ProductHeaderValue octoAgent = new Octokit.ProductHeaderValue("SponsorLink", new Version(ThisAssembly.Info.Version).ToString(2));

    public async Task<Uri> AuthorizeAsync(AppKind kind, string code)
    {
        using var http = HttpFactory.CreateClient();
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.UserAgent.Add(userAgent);

        var jwt = Security.IssueToken(kind);
        var body = Security.CreateAuthorization(kind, code);
        
        var resp = await http.PostAsync(
            "https://github.com/login/oauth/access_token",
            new StringContent(body, Encoding.UTF8, "application/json"),
            jwt);

        dynamic data = JsonConvert.DeserializeObject(await resp.Content.ReadAsStringAsync())!;
        string accessToken = data.access_token;

        var octo = new GitHubClient(octoAgent)
        {
            Credentials = new Credentials(accessToken)
        };

        var emails = await octo.User.Email.GetAll();
        var verified = emails.Where(x => x.Verified && !x.Email.EndsWith("@users.noreply.github.com")).Select(x => x.Email).ToArray();
        var user = await octo.User.Current();

        //await users.PutAsync(new User(user.Id, user.Login, user.Email, accessToken!));

        //foreach (var email in verified)
        //    await this.emails.PutAsync(new EmailUser(email, user.Id));

        // If user is already a sponsor, go to thanks
        // otherwise, go to sponsorship page

        return new Uri("https://devlooped.com/sponsors");

    }
}
