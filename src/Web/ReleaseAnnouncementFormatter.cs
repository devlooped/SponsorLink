namespace Devlooped.Sponsors;

/// <summary>
/// Formats release summaries into threaded X/Twitter posts (280-char limit per post).
/// </summary>
public class ReleaseAnnouncementFormatter
{
    const int MaxPostLength = 280;
    const int UrlLength = 23; // t.co shortens all URLs to 23 chars
    const int TopHighlights = 3;
    const int ThreadIndicatorReserve = 12; // " 🧵 (X/Y)" reserve

    public IReadOnlyList<string> FormatThread(ThreadPlan plan, string repoFullName, string tagName, string releaseUrl)
    {
        if (plan.Items.Count == 0)
            return [$"🚀 {repoFullName}@{tagName} released!\n\n{releaseUrl}"];

        var header = $"🚀 {repoFullName}@{tagName} released!";

        var highlights = plan.Items.Take(TopHighlights).ToList();
        var remaining = plan.Items.Skip(TopHighlights).ToList();
        var followUpGroups = PackItemsIntoPosts(remaining);

        return AssembleThread(header, highlights, followUpGroups, plan.TotalCount, releaseUrl);
    }

    static IReadOnlyList<string> AssembleThread(
        string header,
        IReadOnlyList<string> highlights,
        IReadOnlyList<string> followUpGroups,
        int totalCount,
        string releaseUrl)
    {
        var posts = new List<string>();

        // First post: header + top highlights
        var firstPost = header;
        if (highlights.Count > 0)
        {
            firstPost += "\n\n" + string.Join("\n", highlights);

            // If there are more items beyond highlights, show count
            var shownCount = highlights.Count;
            if (followUpGroups.Count > 0)
                firstPost += " 🧵";
            else if (totalCount > shownCount)
                firstPost += $"\n...and {totalCount - shownCount} more";
        }

        // If thread is just one post, include the URL
        if (followUpGroups.Count == 0)
        {
            firstPost += "\n\n" + releaseUrl;
            posts.Add(TruncatePost(firstPost));
            return posts;
        }

        posts.Add(TruncatePost(firstPost));

        // Middle posts: packed feature groups
        foreach (var group in followUpGroups)
        {
            posts.Add(TruncatePost(group));
        }

        // Last post: remaining count + release URL
        var shownTotal = highlights.Count + followUpGroups.Sum(g => g.Split('\n').Length);
        var lastPost = releaseUrl;
        if (totalCount > shownTotal)
            lastPost = $"...and {totalCount - shownTotal} more\n\n{releaseUrl}";

        posts.Add(TruncatePost(lastPost));

        return posts;
    }

    static IReadOnlyList<string> PackItemsIntoPosts(IList<string> items)
    {
        if (items.Count == 0)
            return [];

        var usable = MaxPostLength - ThreadIndicatorReserve;
        var groups = new List<string>();
        var current = new List<string>();
        var currentLength = 0;

        foreach (var item in items)
        {
            var addLength = current.Count > 0 ? item.Length + 1 : item.Length; // +1 for newline
            if (currentLength + addLength > usable && current.Count > 0)
            {
                groups.Add(string.Join("\n", current));
                current = [];
                currentLength = 0;
                addLength = item.Length;
            }

            current.Add(item);
            currentLength += addLength;
        }

        if (current.Count > 0)
            groups.Add(string.Join("\n", current));

        return groups;
    }

    static string TruncatePost(string post)
    {
        if (post.Length <= MaxPostLength)
            return post;

        return post[..(MaxPostLength - 3)] + "...";
    }
}
