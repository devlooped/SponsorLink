using System.Text.Json;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.Release;

namespace Devlooped.Sponsors;

public class DraftReleaseTests
{
    [Theory]
    [InlineData("v1.2.3", "v1.2.3", "v1.2.3")]
    [InlineData("untagged-e54e403a01285ba20e70", "v1.2.3", "v1.2.3")]
    [InlineData("unnamedtag-abc", "v1.2.3", "v1.2.3")]
    [InlineData("UNTAGGED-abc", "1.0.0", "1.0.0")]
    [InlineData("", "v1.0.0", "v1.0.0")]
    [InlineData("   ", "v1.0.0", "v1.0.0")]
    [InlineData(null, "v1.0.0", "v1.0.0")]
    [InlineData("v1.0.0", null, "v1.0.0")]
    [InlineData("untagged-abc", "untagged-def", null)]
    [InlineData(null, null, null)]
    [InlineData("", "", null)]
    [InlineData("unnamedtag", "  ", null)]
    public void ResolvePublishTag_SkipsPlaceholders(string? tagName, string? releaseName, string? expected) =>
        Assert.Equal(expected, DraftRelease.ResolvePublishTag(tagName, releaseName));

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void ShouldReplaceDraft_PublishesEveryDraft(bool isDraft, bool bodyChanged, bool expected) =>
        Assert.Equal(expected, DraftRelease.ShouldReplaceDraft(isDraft, bodyChanged));

    [Fact]
    public void CreatedDraftPayload_IsHandledAsCreated()
    {
        var json = """
            {
              "action": "created",
              "release": {
                "url": "https://api.github.com/repos/devlooped/NativeValidation/releases/1",
                "assets_url": "https://api.github.com/repos/devlooped/NativeValidation/releases/1/assets",
                "upload_url": "https://uploads.github.com/repos/devlooped/NativeValidation/releases/1/assets{?name,label}",
                "html_url": "https://github.com/devlooped/NativeValidation/releases/tag/untagged-abc",
                "id": 1,
                "node_id": "R1",
                "tag_name": "untagged-abc",
                "target_commitish": "main",
                "name": "v1.0.0",
                "draft": true,
                "author": {
                  "login": "kzu",
                  "id": 1,
                  "node_id": "U1",
                  "avatar_url": "https://github.com/kzu.png",
                  "gravatar_id": "",
                  "url": "https://api.github.com/users/kzu",
                  "html_url": "https://github.com/kzu",
                  "followers_url": "https://api.github.com/users/kzu/followers",
                  "following_url": "https://api.github.com/users/kzu/following{/other_user}",
                  "gists_url": "https://api.github.com/users/kzu/gists{/gist_id}",
                  "starred_url": "https://api.github.com/users/kzu/starred{/owner}{/repo}",
                  "subscriptions_url": "https://api.github.com/users/kzu/subscriptions",
                  "organizations_url": "https://api.github.com/users/kzu/orgs",
                  "repos_url": "https://api.github.com/users/kzu/repos",
                  "events_url": "https://api.github.com/users/kzu/events{/privacy}",
                  "received_events_url": "https://api.github.com/users/kzu/received_events",
                  "type": "User",
                  "site_admin": false
                },
                "prerelease": false,
                "created_at": "2026-10-09T06:00:00Z",
                "published_at": null,
                "assets": [],
                "tarball_url": null,
                "zipball_url": null,
                "body": "notes"
              }
            }
            """;

        var payload = JsonSerializer.Deserialize<ReleaseEvent>(json);

        Assert.NotNull(payload);
        Assert.Equal(ReleaseActionValue.Created, payload.Action);
        Assert.True(payload.Release.Draft);
        Assert.Equal("v1.0.0", DraftRelease.ResolvePublishTag(payload.Release.TagName, payload.Release.Name));
        Assert.NotEqual(ReleaseAction.Deleted, ReleaseAction.Created);
    }
}
