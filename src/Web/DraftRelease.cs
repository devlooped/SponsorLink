namespace Devlooped.Sponsors;

/// <summary>
/// Tag selection for turning a GitHub draft release into a published release.
/// </summary>
public static class DraftRelease
{
    /// <summary>
    /// Picks the tag to publish a draft under.
    /// </summary>
    /// <remarks>
    /// GitHub draft webhooks often carry a placeholder <c>tag_name</c> (<c>untagged-…</c>,
    /// or null/empty when the draft was saved before a tag was chosen). The release
    /// title is the tag in that case. A null result means neither value can be published
    /// and the draft must be left in place.
    /// </remarks>
    public static string? ResolvePublishTag(string? tagName, string? releaseName)
    {
        if (IsUsableTag(tagName))
            return tagName;

        if (IsUsableTag(releaseName))
            return releaseName;

        return null;
    }

    /// <summary>
    /// A draft is always published. An unchanged sponsor section only skips edits of
    /// releases that are already live.
    /// </summary>
    public static bool ShouldReplaceDraft(bool isDraft, bool bodyChanged) =>
        isDraft || bodyChanged;

    static bool IsUsableTag(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        // Historical check was the "unnamedtag" prefix. Webhook payloads and the
        // release URL use GitHub's "untagged-…" placeholder, which does not match
        // that prefix and is not a tag that should be created.
        if (value.StartsWith("untagged", StringComparison.OrdinalIgnoreCase))
            return false;

        if (value.StartsWith("unnamedtag", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
