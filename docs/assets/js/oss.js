---
layout: null
---
Handlebars.registerHelper('format', function(number) {
    return new Intl.NumberFormat().format(number);
  });
var template = Handlebars.compile(window.site.oss_template);
    
var data = {
    authors: { },
    repositories: { },
    packages: { }
};

var authors = { };

fetch('https://raw.githubusercontent.com/devlooped/nuget/refs/heads/main/nuget.json')
    .then(response => {
        // Check if the response is successful
        if (!response.ok) {
            setError(`Failed to retrieve OSS data: ${response.status}`);
            throw new Error('Failed to retrieve OSS data');
        }
        return response.json();
    })
    .then(json => {
        data = json;
        // make authors lowercase for case-insensitive lookup
        authors = Object.keys(data.authors).reduce((acc, key) => {
            acc[key.toLowerCase()] = data.authors[key];
            return acc;
          }, {});
               
        document.getElementById('summary').innerHTML = `<a href="https://github.com/devlooped/nuget/blob/main/nuget.json">Tracking</a> ${data.summary.authors} authors contributing to ${data.summary.repositories} repositories producing ${data.summary.packages} packages with ${data.summary.downloads} combined daily downloads. Learn <a href="https://github.com/devlooped/SponsorLink/blob/main/src/Commands/NuGetStatsCommand.cs">how</a> and <a href="https://github.com/devlooped/nuget/blob/main/.github/workflows/nuget.yml">when</a> your contributions are refreshed.`;
        document.getElementById('summary').className = '';
        setBusy(false);

        // if there's an account in the query string, look it up
        const url = new URL(window.location);
        const account = url.searchParams.get('a');
        if (account)
        {
            document.getElementById('account').value = account;
            lookupAccount();
        }
    });

async function lookupAccount() {
    setBusy(true);
    setError('');
    document.getElementById('data').innerHTML = '';
    var account = document.getElementById('account').value.toLowerCase();
    console.log('Looking up account: ' + account);

    if (account === '') {
        setError('Please enter your github account.');
        setBusy(false);
        return;
    }

    // data format is { "authors": { "account": [ "repo" ] }, "repositories": { "repo": [ "package " ] }, "packages": { "repo": { "package": 1234 } } } 
    // the number being the download count

    var repositories = {};
    if (authors[account] != undefined) {
        // project a list of repositories (with packages) for the account
        repositories = authors[account]
            // The resulting object should have the form { "repo1": [ { "package": 1234 }  ] }
            .map(repo => [repo, Object.entries(data.packages[repo])]);
    }
    else
    {
        // if repositories is empty, use all repositories starting with "account/"
        // filter case insensitively
        repositories = Object.entries(data.repositories)
            .filter(([repo, _]) => repo.toLowerCase().startsWith(account + '/'))
            // The resulting object should have the form { "repo1": [ { "package": 1234 }  ] }
            .map(([repo, _]) => [repo, Object.entries(data.packages[repo])]);
    }

    // sort list of repositories by repository name, then by package id
    // The resulting object should have the form { "repo": "repo1", "packages": [ { "id": "package", "downloads": 1234 }  ] }
    repositories = repositories
        .map(([repo, packages]) => ({ repo: repo, packages: packages.map(([id, downloads]) => ({ id: id, downloads: downloads })) }))
        .sort((a, b) => a.repo.localeCompare(b.repo))
        .map(({ repo, packages }) => ({ repo: repo, packages: packages.sort((a, b) => a.id.localeCompare(b.id)) }));

    // Sum up the downloads for all packages
    const totalDownloads = repositories
        .map(repo => repo.packages)
        .reduce((acc, val) => acc.concat(val), [])
        .map(pkg => pkg.downloads)
        .reduce((acc, val) => acc + val, 0);

    setBusy(false);
        
    if (totalDownloads < 200) {
        document.getElementById('unsupported').style.display = '';
        document.getElementById('supported').style.display = 'none';
        return;
    }

    const model = { account: account, icon: "{{ '/assets/img/copy.svg' | relative_url }}", repositories: repositories, downloads: totalDownloads };
    document.getElementById('data').innerHTML = template(model);
    document.getElementById('unsupported').style.display = 'none';
    document.getElementById('supported').style.display = '';

    // push to history if the search url is different than ?a=account
    const url = new URL(window.location);
    if (url.searchParams.get('a') !== account) {
        url.searchParams.set('a', account);
        window.history.pushState({}, '', url);
    }
}

function setError(message) {
    document.getElementById('error').innerHTML = message;
    if (message !== '') {
        document.getElementById('error').classList.add('warning');
    } else {
        document.getElementById('error').classList.remove('warning');
    }
}

function setBusy(busy) {
    document.getElementById('spinner').style.display = busy ? '' : 'none';
}

function copyMarkdown() {
    const url = new URL(window.location);
    const account = url.searchParams.get('a');
    if (account)
    {
        const markdown = `
![Popular packages](https://img.shields.io/endpoint?label=Popular%20packages&style=social&logo=nuget&url=https%3A%2F%2Fsponsorlink.devlooped.com%2Fnuget%2Fid%3Fa%3D${account})
![Daily downloads](https://img.shields.io/endpoint?label=Daily%20downloads&style=social&logo=nuget&url=https%3A%2F%2Fsponsorlink.devlooped.com%2Fnuget%2Fdl%3Fa%3D${account})
`;
        navigator.clipboard.writeText(markdown);
    }
}