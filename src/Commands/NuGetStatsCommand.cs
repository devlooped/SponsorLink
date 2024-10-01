using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Devlooped.Web;
using DotNetConfig;
using Humanizer;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Polly;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Devlooped.Sponsors;

[Description("Emits the nuget.json manifest with all contributors to active nuget packages")]
public class NuGetStatsCommand(ICommandApp app, Config config, IGraphQueryClient graph, IHttpClientFactory httpFactory) : GitHubAsyncCommand<NuGetStatsCommand.NuGetStatsSettings>(app, config)
{
    // Maximum versions to consider from a package history for determining whether the package 
    // is a popular with a minimum amount of downloads.
    const int MaxVersions = 5;
    // Minimum amount of downloads to consider a package popular/active.
    const int DailyDownloadsThreshold = 200;

    // Culture to use to parse nuget.org data
    static readonly CultureInfo ParseCulture = new("en-US");

    // We skip these package owners since they belong to plaforms, not individual/small teams.
    static readonly HashSet<string> SkippedOwners = new(
    [
        "aspnet",
        "dotnetframework",
        "Microsoft",
        "azure-sdk",
        "awsdotnet",
        "google-apis-packages",
        "jetbrains",
        "dotnet",

    ],
    StringComparer.OrdinalIgnoreCase);

    public class NuGetStatsSettings : ToSSettings
    {
        [Description("Force complete data refresh. Otherwise, resume from 'nuget.json' if found.")]
        [DefaultValue(false)]
        [CommandOption("--force")]
        public bool Force { get; set; }

        [Description(@"Read GitHub authentication token from standard input for sync")]
        [DefaultValue(false)]
        [CommandOption("--with-token")]
        public bool WithToken { get; set; }

        [Description("Pages to skip")]
        [CommandOption("--skip", IsHidden = true)]
        public int Skip { get; set; }

        [Description("Specific package owner to fetch full stats for")]
        [CommandOption("--owner")]
        public string? Owner { get; set; }

        [Description("Only include OSS packages hosted on GitHub")]
        [DefaultValue(true)]
        [CommandOption("--gh-only")]
        public bool GitHubOnly { get; set; } = true;

        [Description("Only include OSS packages")]
        [DefaultValue(true)]
        [CommandOption("--oss-only")]
        public bool OssOnly { get; set; } = true;

        public override ValidationResult Validate()
        {
            if (OssOnly == false && Owner == null)
                return ValidationResult.Error("Non-OSS packages can only be fetched for a specific owner.");

            // If not requesting OSS, change default for GH only.
            if (OssOnly == false)
                GitHubOnly = false;

            return base.Validate();
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, NuGetStatsSettings settings)
    {
        string? token = default;
        if (settings.WithToken)
            token = new StreamReader(Console.OpenStandardInput()).ReadToEnd().Trim();

        using var withToken = GitHub.WithToken(token);
        if (!string.IsNullOrEmpty(token) && withToken is null)
        {
            AnsiConsole.MarkupLine(":cross_mark: [yellow]Invalid GitHub token provided[/]");
            return -1;
        }

        var repository = new SourceRepository(new PackageSource("https://api.nuget.org/v3/index.json"), Repository.Provider.GetCoreV3());
        var resource = await repository.GetResourceAsync<PackageMetadataResource>();
        var cache = new SourceCacheContext { RefreshMemoryCache = true };
        var retry = Policy
            .Handle<WebException>()
            .Or<HttpRequestException>()
            // See https://github.com/NuGet/Home/issues/13804
            .Or<NullReferenceException>()
            .WaitAndRetryForeverAsync(retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        var fileName = (settings.Owner ?? "nuget") + ".json";

        // The resulting model we'll populate.
        OpenSource model;
        if (File.Exists(fileName) && settings.Force != true)
            model = JsonSerializer.Deserialize<OpenSource>(File.ReadAllText(fileName), JsonOptions.Default) ?? new OpenSource();
        else
            model = new OpenSource();

        using var http = httpFactory.CreateClient();

        var progress = AnsiConsole.Progress()
            .Columns(
            [
                new ProgressBarColumn() { Width = 10 },
                new TaskDescriptionColumn() { Alignment = Justify.Left },
            ]).AutoClear(true);

        var index = 1;
        if (settings.Skip > 0)
            index = settings.Skip + 1;

        var baseUrl = "https://www.nuget.org/packages?sortBy=totalDownloads-desc&";
        if (settings.Owner != null)
            baseUrl += $"q=owner%3A{settings.Owner}&";

        await progress.StartAsync(async context =>
        {
            while (true)
            {
                var listUrl = $"{baseUrl}page={index}";
                AnsiConsole.MarkupLine($":globe_with_meridians: [aqua][link={listUrl}]packages#{index}[/][/]");
                var listTask = context.AddTask($":backhand_index_pointing_right: [grey]Processing page[/] [aqua][link={listUrl}]#{index}[/][/][grey]. Total[/] [lime]{model.Authors.Count}[/] [grey]oss authors so far across[/] {model.Repositories.Count} [grey]repos[/]", false);
                // Parse search page
                var search = await retry.ExecuteAsync(() => Task.FromResult(HtmlDocument.Load(listUrl)));
                var allIds = search.CssSelectElements(".package .package-by [data-owner]")
                    .Select(x => new
                    {
                        Id = x.Attribute("data-package-id")?.Value ?? "",
                        Owner = x.Attribute("data-owner")?.Value ?? "",
                        Version = x.Attribute("data-package-version")?.Value ?? "",
                    })
                    .Where(x => x.Id != "" && x.Owner != "" && x.Version != "")
                    .GroupBy(x => x.Id);

                if (!allIds.Any())
                {
                    listTask.Description = $":stop_sign: [yellow]No more packages found on page {index}[/]";
                    listTask.StopTask();
                    break;
                }

                // Skip corp owners
                var ids = (settings.Owner != null
                        // Don't filter anything if we're fetching a specific owner
                        ? allIds
                        : allIds.Where(x => settings.Owner == null && !x.Any(i => SkippedOwners.Contains(i.Owner))))
                    .Select(x => new PackageIdentity(x.Key, NuGetVersion.Parse(x.First().Version)))
                    .ToList();

                listTask.MaxValue = ids.Count;

                // packages that are inactive based on download count.
                var inactive = 0;
                // packages that do have sources in github
                var sourced = 0;
                var tasks = ids.Select(id => (context.AddTask($":hourglass_not_done: [deepskyblue1][link=https://nuget.org/packages/{id.Id}]{id.Id}[/][/]: processing", true), id));

                // Be gentle with nuget.org
                var paralell = new ParallelOptions { MaxDegreeOfParallelism = 5 };
                if (Debugger.IsAttached)
                    // Makes things far easier to debug
                    paralell.MaxDegreeOfParallelism = 1;

                await Parallel.ForEachAsync(tasks, paralell, async (source, cancellation) =>
                {
                    var (task, id) = source;
                    var link = $"[link=https://nuget.org/packages/{id.Id}]{id.Id}[/]";
                    try
                    {
                        // This allows us to resume execution if we already have a serialized model containing 
                        // the package id
                        if (model.Packages.Any(x => x.Value.ContainsKey(id.Id)))
                        {
                            task.Description = $":fast_forward_button: [grey]{link}: already processed[/]";
                            return;
                        }

                        string? repoUrl = null;
                        string? ownerRepo = null;
                        // see https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource#download-package-manifest-nuspec
                        var nuspec = await http.GetAsync($"https://api.nuget.org/v3-flatcontainer/{id.Id.ToLowerInvariant()}/{id.Version.ToNormalizedString().ToLowerInvariant()}/{id.Id.ToLowerInvariant()}.nuspec", cancellation);
                        // Try getting repo information from nuspec first. This can help 
                        // avoid loading the package page altogether if we have already checked that repo 
                        // for contributors.
                        if (nuspec.IsSuccessStatusCode)
                        {
                            RepositoryMetadata repoMeta;
                            try
                            {
                                // read source repo from spec
                                using var stream = await nuspec.Content.ReadAsStreamAsync(cancellation);
                                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                                var spec = new NuspecReader(await XDocument.LoadAsync(reader, LoadOptions.None, cancellation));
                                repoMeta = spec.GetRepositoryMetadata();
                            }
                            catch (Exception e)
                            {
                                task.Description = $":cross_mark: [yellow]{link}[/]: invalid nuspec '{e.Message}'";
                                return;
                            }

                            if (!string.IsNullOrEmpty(repoMeta?.Url) &&
                                Uri.TryCreate(repoMeta.Url, UriKind.Absolute, out var uri))
                            {
                                repoUrl = repoMeta.Url;

                                if (settings.GitHubOnly && uri.Host != "github.com")
                                {
                                    task.Description = $":cross_mark: [yellow]{link}[/]: non GitHub source, skipping";
                                    return;
                                }

                                // Some packages have git:// there
                                if (uri.Scheme != "https")
                                    // change scheme to https
                                    uri = new UriBuilder(uri) { Scheme = "https", Port = 443 }.Uri;

                                if (settings.GitHubOnly)
                                {
                                    // Ensure we get an existing GH source repo as requested
                                    try
                                    {
                                        if (!(await http.SendAsync(new(HttpMethod.Head, uri), cancellation)).IsSuccessStatusCode)
                                        {
                                            task.Description = $":cross_mark: [yellow]{link}[/]: GitHub repo from nuspec not found at {uri}";
                                            return;
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        task.Description = $":cross_mark: [yellow]{link}[/]: GitHub repo from nuspec not found at {uri}";
                                        return;
                                    }
                                }

                                ownerRepo = uri.PathAndQuery.TrimStart('/');
                                if (ownerRepo.EndsWith(".git"))
                                    ownerRepo = ownerRepo[..^4];

                                var parts = ownerRepo.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
                                if (uri.Host == "github.com")
                                {
                                    if (parts.Length < 2)
                                    {
                                        task.Description = $":cross_mark: [yellow]{link}[/]: source URL '{uri}' missing specific repo";
                                        return;
                                    }
                                    else if (parts.Length > 2)
                                    {
                                        ownerRepo = string.Join('/', ownerRepo.Split(['/'], StringSplitOptions.RemoveEmptyEntries)[..2]);
                                    }
                                }
                                // otherwise just keep the original.
                            }
                            else if (settings.OssOnly)
                            {
                                // stop as there's no repo info even if we got nuspec ok
                                task.Description = $":locked: [yellow]{link}[/]: no source repo information";
                                return;
                            }
                        }
                        else
                        {
                            task.Description = $":cross_mark: [yellow]{link}[/]: failed to retrieve nuspec";
                            return;
                        }

                        // Wrapped in a retry since this also failed sporadically
                        var metadata = (await retry.ExecuteAsync(() => resource.GetMetadataAsync(id.Id, false, false, cache,
                            NuGet.Common.NullLogger.Instance, CancellationToken.None))).Reverse().Take(MaxVersions).ToList();

                        // We get max versions only, first stable (non-prerelease). If we don't get enough, we then append prerelease ones.
                        if (metadata.Count < MaxVersions)
                        {
                            var added = metadata.Select(x => x.Identity.Version).ToHashSet();

                            // Only consider pre-release versions if there are no stable versions (yet). 
                            // This is more of an odd case of a popular long-running pre-release package.
                            metadata.AddRange((await retry.ExecuteAsync(() => resource.GetMetadataAsync(id.Id, true, false, cache,
                                NuGet.Common.NullLogger.Instance, CancellationToken.None)))
                                .Reverse()
                                .Where(x => !added.Contains(x.Identity.Version))
                                .Take(MaxVersions - added.Count)
                                .ToList());

                            Debug.Assert(metadata.Count > 0, "Could not find any package versions?");
                        }

                        Debug.Assert(metadata.Last().Published != null);
                        var updated = metadata.Where(x => x.Published.HasValue).Select(x => x.Published!.Value).Min();
                        // At the moment, this will actually not result in an actual sum, since the download count is always null.
                        var downloads = metadata.Sum(x => x.DownloadCount);
                        sourced++;

                        // NOTE: metadata.DownloadCount is always null, hence we need to go to the details page.
                        // It may start working some day?
                        if (downloads == null || downloads == 0)
                        {
                            // Retry since at least https://www.nuget.org/packages/UnicornEngine.Unicorn failed once.
                            var retries = 0;
                            var attemptPrefix = $":hourglass_not_done: [deepskyblue1]{link}[/]: getting download count for [cornsilk1]v{id.Version.ToNormalizedString()}[/]";
                            task.Description = attemptPrefix;

                            while (retries < 5)
                            {
                                var details = await retry.ExecuteAsync(() => Task.FromResult(HtmlDocument.Load($"https://www.nuget.org/packages/{id.Id}")));

                                // First determine if the package is active, so we can stop scraping when we reach the low-numbers
                                // Get the row matching the latest version in the API (there can be prereleases, we don't want to consider those 
                                // which might have low download numbers)
                                foreach (var label in metadata.Select(x => x.Identity.Version.ToNormalizedString().Truncate(30)).Distinct())
                                {
                                    var row = details.CssSelectElements(".version-history table tbody tr")
                                        // See https://github.com/NuGet/NuGetGallery/blob/main/src/NuGetGallery/Views/Packages/DisplayPackage.cshtml#L922
                                        .FirstOrDefault(x => x.CssSelectElement("td")?.Value.Trim() == label);
                                    if (row == null)
                                    {
                                        task.Description = $":cross_mark: [yellow]{link}[/]: no version history found";
                                        return;
                                    }

                                    if (long.TryParse(row.CssSelectElements("td").Skip(1).First().Value.Trim(), NumberStyles.AllowThousands, ParseCulture, out var downloadsValue))
                                        downloads += downloadsValue;
                                }

                                if (downloads != null && downloads > 0)
                                    break;

                                retries++;
                                task.Description = $":hourglass_not_done: [deepskyblue1]{link}[/]: getting download count for [cornsilk1]v{id.Version.ToNormalizedString()}[/] (retry #{retries})";
                            }

                            if (downloads == null || downloads == 0)
                            {
                                Debugger.Launch();
                                task.Description = $":red_question_mark: [yellow]{link}[/]: no download count found";
                                return;
                            }
                        }

                        var daysSince = Convert.ToInt32(Math.Max(1, Math.Round((DateTimeOffset.UtcNow - updated).TotalDays)));
                        var dailyDownloads = Convert.ToInt32(downloads / daysSince);
                        // We only consider the package "active" if it's got a minimum amount of downloads per day in the last x versions we consider.
                        // We don't filter inactive packages if we're fetching a specific owner.
                        if (settings.Owner == null && dailyDownloads < DailyDownloadsThreshold)
                        {
                            inactive++;
                            task.Description = $":thumbs_down: [yellow]{link}[/]: skipping with {dailyDownloads} downloads/day";
                            return;
                        }

                        if (ownerRepo != null)
                        {
                            // Check contributors only once per repo, since multiple packages can come out of the same repository
                            if (!model.Repositories.ContainsKey(ownerRepo))
                            {
                                var contribs = await graph.QueryAsync(GraphQueries.RepositoryContributors(ownerRepo));
                                if (contribs?.Length == 0)
                                {
                                    // Make sure we haven't exhausted the GH API rate limit
                                    var rate = await graph.QueryAsync(GraphQueries.RateLimits);
                                    if (rate is not { })
                                        throw new InvalidOperationException("Failed to get rate limits for current token.");

                                    if (rate.General.Remaining < 10 || rate.GraphQL.Remaining < 10)
                                    {
                                        var reset = DateTimeOffset.FromUnixTimeSeconds(Math.Min(rate.General.Reset, rate.GraphQL.Reset));
                                        var wait = reset - DateTimeOffset.UtcNow;
                                        task.Description = $":hourglass_not_done: [yellow]Rate limit exhausted, waiting {wait.Humanize()} until reset[/]";
                                        await Task.Delay(wait, cancellation);
                                        contribs = await graph.QueryAsync(GraphQueries.RepositoryContributors(ownerRepo));
                                    }
                                }

                                if (contribs != null)
                                {
                                    model.Repositories.TryAdd(ownerRepo, new(contribs, FnvHashComparer.Default));
                                }
                                else
                                {
                                    // Might not be a GH repo at all, or perhaps it's just empty?
                                    model.Repositories.TryAdd(ownerRepo, new(FnvHashComparer.Default));
                                    AnsiConsole.MarkupLine($":warning: [yellow]{link}[/]: no contributors found for [white]{ownerRepo}[/]");
                                }
                            }

                            foreach (var author in model.Repositories[ownerRepo])
                                model.Authors.GetOrAdd(author, _ => new(FnvHashComparer.Default)).Add(ownerRepo);
                        }

                        // If we allow non-oss packages, we won't have an ownerRepo, so consider that an empty string.
                        model.Packages.GetOrAdd(ownerRepo ?? "", _ => new(FnvHashComparer.Default)).TryAdd(id.Id, dailyDownloads);
                        if (ownerRepo != null)
                            task.Description = $":check_mark_button: [deepskyblue1]{link}[/]: [white]{ownerRepo}[/] [grey]has[/] [lime]{model.Repositories[ownerRepo].Count}[/] [grey]contributors.[/]";
                    }
                    finally
                    {
                        task.Value = task.MaxValue;
                        task.StopTask();
                        listTask.Increment(1);
                    }
                });

                listTask.Description = $":hourglass_not_done: [grey]Finished page[/] [aqua]#{index}[/][grey]. Persisting model...[/]";
                lock (model)
                {
                    File.WriteAllText(fileName, JsonSerializer.Serialize(model, JsonOptions.Default));
                }

                listTask.Description = $":call_me_hand: [grey]Finished page[/] [aqua]#{index}[/][grey]. Total[/] [lime]{model.Summary.Authors}[/] [grey]oss authors so far across[/] {model.Summary.Repositories} [grey]repos.[/]";
                listTask.StopTask();
                index++;
            }
        });

        var path = new FileInfo(fileName).FullName;
        AnsiConsole.MarkupLine($"Total [lime]{model.Summary.Authors}[/] oss authors contributing to {model.Summary.Repositories} repos producing {model.Summary.Packages} packages with {model.Summary.Downloads} dl/day => [link={path}]{fileName}[/]");

        return 0;
    }
}
