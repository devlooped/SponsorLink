using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Devlooped.Web;
using DotNetConfig;
using Humanizer;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Providers;
using NuGet.Versioning;
using Polly;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Devlooped.Sponsors;

[Description("Emits the nuget.json manifest with all contributors to active nuget packages")]
public partial class NuGetStatsCommand(Config config, IGraphQueryClient graph, IHttpClientFactory httpFactory) : GitHubAsyncCommand<NuGetStatsCommand.NuGetStatsSettings>(config)
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

#if DEBUG
        [Description(@"Specific GitHub authentication token for debug")]
        [CommandOption("--token")]
        public string? Token { get; set; }
#endif

        [Description("Pages to skip")]
        [CommandOption("--skip", IsHidden = true)]
        public int Skip { get; set; }

        [Description("Specific package owner to fetch full stats for")]
        [CommandOption("--owner")]
        public string? Owner { get; set; }

        [CommandOption("--package", IsHidden = true)]
        public string? PackageId { get; set; }

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

    public override async Task<int> ExecuteAsync(CommandContext context, NuGetStatsSettings settings, CancellationToken cancellation)
    {
        string? token = default;
#if DEBUG
        token = settings.Token;
#endif
        if (settings.WithToken)
            token = new StreamReader(Console.OpenStandardInput()).ReadToEnd().Trim();

        using var withToken = GitHub.WithToken(token);
        // Whether via custom token or exiting one, we need to ensure user can be authenticated with the GH CLI
        if ((token != null && withToken == null) || await base.ExecuteAsync(context, settings, cancellation) is not 0)
        {
            AnsiConsole.MarkupLine(":cross_mark: [yellow]GitHub CLI auth status could not be determined[/]");
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

        using var http = httpFactory.CreateClient("NuGet");

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

        await progress.StartAsync(async progressContext =>
        {
            async Task<List<IPackageSearchMetadata>> GetMetadataAsync(string packageId)
            {
                var metadata = (await retry.ExecuteAsync(() => resource.GetMetadataAsync(packageId, false, false, cache,
                    NuGet.Common.NullLogger.Instance, CancellationToken.None))).Reverse().Take(MaxVersions).ToList();

                if (metadata.Count < MaxVersions)
                {
                    var added = metadata.Select(x => x.Identity.Version).ToHashSet();

                    // Only consider pre-release versions if there are no stable versions (yet).
                    // This is more of an odd case of a popular long-running pre-release package.
                    metadata.AddRange((await retry.ExecuteAsync(() => resource.GetMetadataAsync(packageId, true, false, cache,
                        NuGet.Common.NullLogger.Instance, CancellationToken.None)))
                        .Reverse()
                        .Where(x => !added.Contains(x.Identity.Version))
                        .Take(MaxVersions - added.Count)
                        .ToList());
                }

                return metadata;
            }

            async Task<Dictionary<string, long>> GetSearchDownloadsAsync(string packageId, CancellationToken cancellationToken)
            {
                var queryUrl = $"https://api-v2v3search-0.nuget.org/query?q=id:{Uri.EscapeDataString(packageId)}&semVerLevel=2.0.0";
                using var response = await http.GetAsync(queryUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return [];

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var result = await JsonSerializer.DeserializeAsync<NuGetSearchResponse>(stream, JsonOptions.Default, cancellationToken);
                var package = result?.Data.FirstOrDefault(x => string.Equals(x.Id, packageId, StringComparison.OrdinalIgnoreCase));
                if (package?.Versions == null)
                    return [];

                var downloads = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                foreach (var version in package.Versions)
                {
                    if (string.IsNullOrEmpty(version.Version))
                        continue;

                    downloads[NuGetVersion.Parse(version.Version).ToNormalizedString()] = version.Downloads;
                }

                return downloads;
            }

            async Task ProcessPackagesAsync(ProgressTask listTask, IReadOnlyList<PackageIdentity> ids, CancellationToken cancellationToken)
            {
                listTask.MaxValue = ids.Count;

                // packages that are inactive based on download count.
                var inactive = 0;
                // packages that do have sources in github
                var sourced = 0;
                var tasks = ids.Select(id => (progressContext.AddTask($":hourglass_not_done: [deepskyblue1][link=https://nuget.org/packages/{id.Id}]{id.Id}[/][/]: processing", true), id));

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

                        var metadata = await GetMetadataAsync(id.Id);
                        if (metadata.Count == 0)
                        {
                            task.Description = $":cross_mark: [yellow]{link}[/]: package metadata not found";
                            return;
                        }

                        var version = metadata.First().Identity.Version;

                        string? ownerRepo = null;
                        // see https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource#download-package-manifest-nuspec
                        var nuspec = await http.GetAsync($"https://api.nuget.org/v3-flatcontainer/{id.Id.ToLowerInvariant()}/{version.ToNormalizedString().ToLowerInvariant()}/{id.Id.ToLowerInvariant()}.nuspec", cancellation);
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
                                            task.Description = $":red_question_mark: [yellow]{link}[/]: GitHub repo from nuspec not found at {uri}";
                                            return;
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        task.Description = $":red_question_mark: [yellow]{link}[/]: GitHub repo from nuspec not found at {uri}";
                                        return;
                                    }
                                }

                                ownerRepo = uri.PathAndQuery.TrimStart('/');
                                if (ownerRepo.EndsWith(".git"))
                                    ownerRepo = ownerRepo[..^4];

                                var parts = ownerRepo.Split("/", StringSplitOptions.RemoveEmptyEntries);
                                if (uri.Host == "github.com")
                                {
                                    if (parts.Length < 2)
                                    {
                                        task.Description = $":warning: [yellow]{link}[/]: source URL '{uri}' missing specific repo";
                                        return;
                                    }
                                    else if (parts.Length > 2)
                                    {
                                        ownerRepo = string.Join('/', ownerRepo.Split("/", StringSplitOptions.RemoveEmptyEntries)[..2]);
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

                        var metadataByVersion = metadata.ToDictionary(
                            x => x.Identity.Version.ToNormalizedString(),
                            x => x,
                            StringComparer.OrdinalIgnoreCase);
                        var versionsWithDownloads = metadata
                            .Where(x => x.DownloadCount is > 0)
                            .Select(x => x.Identity.Version.ToNormalizedString())
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                        var downloads = metadata.Sum(x => x.DownloadCount);
                        sourced++;

                        // NOTE: metadata.DownloadCount is always null, hence we need to go to the details page.
                        // It may start working some day?
                        if (downloads == null || downloads == 0)
                        {
                            task.Description = $":hourglass_not_done: [deepskyblue1]{link}[/]: getting download count from search API";

                            foreach (var pair in await GetSearchDownloadsAsync(id.Id, cancellation))
                            {
                                if (!metadataByVersion.ContainsKey(pair.Key))
                                    continue;

                                downloads = (downloads ?? 0) + pair.Value;
                                versionsWithDownloads.Add(pair.Key);
                            }
                        }

                        if (downloads == null || downloads == 0)
                        {
                            // Retry since at least https://www.nuget.org/packages/UnicornEngine.Unicorn failed once.
                            var retries = 0;
                            var attemptPrefix = $":hourglass_not_done: [deepskyblue1]{link}[/]: getting download count for [cornsilk1]v{version.ToNormalizedString()}[/]";
                            task.Description = attemptPrefix;

                            while (retries < 5)
                            {
                                var html = await retry.ExecuteAsync(() => http.GetStringAsync($"https://www.nuget.org/packages/{id.Id}", cancellation));
                                html = CleanSponsorshipUrlBug().Replace(html, "");
                                var details = HtmlDocument.Load(new StringReader(html));

                                // First determine if the package is active, so we can stop scraping when we reach the low-numbers
                                // Get the row matching the latest version in the API (there can be prereleases, we don't want to consider those
                                // which might have low download numbers)
                                foreach (var item in metadata
                                    .Select(x => new
                                    {
                                        Version = x.Identity.Version.ToNormalizedString(),
                                        Label = x.Identity.Version.ToNormalizedString().Truncate(30),
                                    })
                                    .DistinctBy(x => x.Version)
                                    .Where(x => !versionsWithDownloads.Contains(x.Version)))
                                {
                                    var row = details.CssSelectElements(".version-history table tbody tr")
                                        // See https://github.com/NuGet/NuGetGallery/blob/main/src/NuGetGallery/Views/Packages/DisplayPackage.cshtml#L922
                                        .FirstOrDefault(x => x.CssSelectElement($"td a[title='{item.Label}']") != null);

                                    if (row == null)
                                    {
                                        // nuget.org only shows a handful of recent releases in the version table, so consider 
                                        // the loop done once we don't get any more download stats. Note that we may stop short of the 
                                        // actual package feed metadata, but that's ok because there's no more info to gather from the web.
                                        break;
                                    }

                                    if (long.TryParse(row.CssSelectElements("td").Skip(1).First().Value.Trim(), NumberStyles.AllowThousands, ParseCulture, out var downloadsValue))
                                    {
                                        downloads += downloadsValue;
                                        versionsWithDownloads.Add(item.Version);
                                    }
                                }

                                if (downloads != null && downloads > 0)
                                    break;

                                retries++;
                                task.Description = $":hourglass_not_done: [deepskyblue1]{link}[/]: getting download count for [cornsilk1]v{version.ToNormalizedString()}[/] (retry #{retries})";
                            }

                            if (downloads == null || downloads == 0)
                            {
                                Debugger.Launch();
                                task.Description = $":red_question_mark: [yellow]{link}[/]: no download count found";
                                return;
                            }
                        }

                        var updatedVersions = metadata
                            .Where(x => x.Published.HasValue && versionsWithDownloads.Contains(x.Identity.Version.ToNormalizedString()))
                            .Select(x => x.Published!.Value)
                            .ToList();
                        if (updatedVersions.Count == 0)
                        {
                            task.Description = $":red_question_mark: [yellow]{link}[/]: no download history found";
                            return;
                        }

                        var updatedAt = updatedVersions.Min();
                        var daysSince = Convert.ToInt32(Math.Max(1, Math.Round((DateTimeOffset.UtcNow - updatedAt).TotalDays)));
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
                            // Account for repo renames/ownership transfers
                            if (await graph.QueryAsync(GraphQueries.RepositoryFullName(ownerRepo)) is { } fullName)
                                ownerRepo = fullName;

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
                        else
                            task.Description = $":check_mark_button: [deepskyblue1]{link}[/]: [grey]no source repo information[/]";
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        task.Description = $":cross_mark: [yellow]{link}[/]: error processing '{ex.Message}'";
                        AnsiConsole.MarkupLine($":warning: [red]Failed to process https://www.nuget.org/packages/{id.Id}[/]: {ex.GetType().Name}: {ex.Message}");
                    }
                    finally
                    {
                        task.Value = task.MaxValue;
                        task.StopTask();
                        listTask.Increment(1);
                    }
                });
            }

            void PersistModel() => File.WriteAllText(fileName, JsonSerializer.Serialize(model, JsonOptions.Default));

            if (settings.PackageId is { Length: > 0 } packageId)
            {
                var packageUrl = $"https://www.nuget.org/packages/{packageId}";
                AnsiConsole.MarkupLine($":globe_with_meridians: [aqua][link={packageUrl}]{packageId}[/][/] ");
                var listTask = progressContext.AddTask($":backhand_index_pointing_right: [grey]Processing package[/] [aqua][link={packageUrl}]{packageId}[/][/][grey]. Total[/] [lime]{model.Authors.Count}[/] [grey]oss authors so far across[/] {model.Repositories.Count} [grey]repos[/]", false);

                await ProcessPackagesAsync(listTask, [new(packageId, NuGetVersion.Parse("0.0.0"))], cancellation);

                listTask.Description = $":hourglass_not_done: [grey]Finished package[/] [aqua]{packageId}[/][grey]. Persisting model...[/]";
                lock (model)
                {
                    PersistModel();
                }

                listTask.Description = $":call_me_hand: [grey]Finished package[/] [aqua]{packageId}[/][grey]. Total[/] [lime]{model.Summary.Authors}[/] [grey]oss authors so far across[/] {model.Summary.Repositories} [grey]repos.[/]";
                listTask.StopTask();
                return;
            }

            while (true)
            {
                var listUrl = $"{baseUrl}page={index}";
                AnsiConsole.MarkupLine($":globe_with_meridians: [aqua][link={listUrl}]packages#{index}[/][/]");
                var listTask = progressContext.AddTask($":backhand_index_pointing_right: [grey]Processing page[/] [aqua][link={listUrl}]#{index}[/][/][grey]. Total[/] [lime]{model.Authors.Count}[/] [grey]oss authors so far across[/] {model.Repositories.Count} [grey]repos[/]", false);
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

                await ProcessPackagesAsync(listTask, ids, cancellation);

                listTask.Description = $":hourglass_not_done: [grey]Finished page[/] [aqua]#{index}[/][grey]. Persisting model...[/]";
                lock (model)
                {
                    PersistModel();
                }

                listTask.Description = $":call_me_hand: [grey]Finished page[/] [aqua]#{index}[/][grey]. Total[/] [lime]{model.Summary.Authors}[/] [grey]oss authors so far across[/] {model.Summary.Repositories} [grey]repos.[/]";
                listTask.StopTask();
                index++;
            }
        });

        var path = new FileInfo(fileName).FullName;
        if (App.IsInteractive)
            AnsiConsole.MarkupLine($"Total [lime]{model.Summary.Authors}[/] oss authors contributing to {model.Summary.Repositories} repos producing {model.Summary.Packages} packages with {model.Summary.Downloads} dl/day => [link={path}]{fileName}[/]");
        else
            AnsiConsole.MarkupLine($"Total [lime]{model.Summary.Authors}[/] oss authors contributing to {model.Summary.Repositories} repos producing {model.Summary.Packages} packages with {model.Summary.Downloads} dl/day => [deepskyblue1]{fileName}[/]");

        return 0;
    }

    // Remove when https://github.com/NuGet/NuGetGallery/issues/10739 is fixed
    [GeneratedRegex(
        """
        data-sponsorship-url=""[^"]*""
        """)]
    private static partial Regex CleanSponsorshipUrlBug();

    sealed record NuGetSearchResponse([property: JsonPropertyName("data")] NuGetSearchPackage[] Data);
    sealed record NuGetSearchPackage(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("versions")] NuGetSearchVersion[] Versions);
    sealed record NuGetSearchVersion(
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("downloads")] long Downloads);
}
