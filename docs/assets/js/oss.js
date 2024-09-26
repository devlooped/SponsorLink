Handlebars.registerHelper('format', function(number) {
    return new Intl.NumberFormat().format(number);
  });
var template = Handlebars.compile(window.site.oss_template);
    
var data = {
    authors: { },
    repositories: { },
    packages: { }
};

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
        setBusy(false);
    });

async function lookupAccount() {
    setBusy(true);
    setError('');
    document.getElementById('data').innerHTML = '';
    var account = document.getElementById('account').value;
    console.log('Looking up account: ' + account);

    if (account === '') {
        setError('Please enter your github account.');
        setBusy(false);
        return;
    }

    if (data.authors[account] === undefined) {
        document.getElementById('unsupported').style.display = '';
        document.getElementById('supported').style.display = 'none';
    } else {
        const repositories = data.authors[account].sort().map(repo => ({
            repo: repo,
            packages: Object.entries(data.packages[repo])
                .map(([id, downloads]) => ({
                    id: id,
                    downloads: downloads
                }))
                .sort((a, b) => a.id.localeCompare(b.id))
        }));

        const totalDownloads = repositories.reduce((total, repo) => {
            return total + repo.packages.reduce((repoTotal, pkg) => {
              return repoTotal + pkg.downloads;
            }, 0);
          }, 0);

        document.getElementById('data').innerHTML = template({ repositories: repositories, downloads: totalDownloads });
        document.getElementById('unsupported').style.display = 'none';
        document.getElementById('supported').style.display = '';
    }

    setBusy(false);
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