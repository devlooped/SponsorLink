---
layout: null
---
var template = Handlebars.compile(window.site.issues_template);

var data = {
    "account": "devlooped",
    "issuer": "https://sponsorlink.devlooped.com",
    "unsupported": false
};

{% if jekyll.environment == "production" %}
// this is CI, use production issuer
{% else %}
data.issuer = "donkey-emerging-civet.ngrok-free.app";
{% endif %}

// persist the Referer URL in local storage if it is a github issue URL
if (document.referrer && document.referrer.startsWith('https://github.com/') && document.referrer.includes('/issues/')) {
    localStorage['issueUrl'] = document.referrer;
}

if (localStorage['issueUrl']) {
    data.issueUrl = localStorage['issueUrl'];
}

const urlParams = new URLSearchParams(window.location.search);
var sponsorable = urlParams.get('s');
if (sponsorable !== null && sponsorable !== "") {
    data.account = sponsorable;
    document.getElementById('account').value = sponsorable;
} else {
    document.getElementById('account').value = data.account;
}

var issuer = urlParams.get('i');
if (issuer !== null && issuer !== "") {
    data.issuer = issuer;
    displayIssues();
} else {
    lookupSponsor();
}

async function backIssue(sponsorshipId) {
    setError('');
    var issueUrl = document.getElementById(sponsorshipId).value;
    if (issueUrl === null || issueUrl === "") {
        return;
    }

    var parts = issueUrl.split('/');
    if (parts.length < 7) {
        setError('Invalid issue URL: ' + issueUrl);
        return;
    }

    var owner = parts[3];
    var repo = parts[4];
    var number = parts[6];

    try {
        var issuer = data.issuer;
        if (!issuer.startsWith('https://')) {
            issuer = 'https://' + issuer;
        }
        if (issuer.endsWith('/')) {
            issuer = issuer.slice(0, -1);
        }
        var url = issuer + '/github/issues';
        setBusy(true);
        
        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                sponsorship: sponsorshipId,
                owner: owner,
                repo: repo,
                issue: number
            }),
            credentials: 'include'
        });

        if (!response.ok) {
            throw new Error(`Failed to back issue: ${response.statusText}`);
        }
        
        var json = await response.json();
        setStatus(json.status);
        console.log(json);
        if (response.status === 'unauthorized') {
            document.getElementById('login').href = json.loginUrl;
        } else {
            // remove issue url from local storage after successful backing
            localStorage.removeItem('issueUrl');
            data.issueUrl = null;
            await displayIssues();
        }

    } catch (error) {
        console.error('Failed to back issue:', error);
        setError('Failed to back issue: ' + error);
    } finally {
        setBusy(false);
    }
}

async function displayIssues() {
    setError('');
    setStatus('loading');
    
    var issuer = data.issuer;
    if (!issuer.startsWith('https://')) {
        issuer = 'https://' + issuer;
    }
    if (issuer.endsWith('/')) {
        issuer = issuer.slice(0, -1);
    }

    try {
        const response = await fetch(issuer + '/github/issues', {
            method: 'GET',
            credentials: 'include'
        });

        if (!response.ok) {
            throw new Error('SponsorLink issuer failure.');
        }

        const json = await response.json();
        setStatus(json.status);
        console.log(json);
        if (json.status === 'unauthorized') {
            document.getElementById('login').href = json.loginUrl;
        } else if (json.status === 'ok') {
            document.getElementById('user').innerHTML = `Hi there ${json.user.split(' ')[0]}!`;
            // If there's an issue URL in storage, which we persist from referer when 
            // users navigate to this page, use it for the first available one-time sponsorship 
            // to make it easier for users to back the issue they came from.
            if (data.issueUrl && json.available.length > 0) {
                json.available[0].url = data.issueUrl;
            }
            document.getElementById('issues').innerHTML = template(json);
        } else {
            throw new Error('Unexpected response: ' + json);
        }
    } catch (error) {
        console.error('Failed to fetch backed issues:', error);
        setError('Failed to fetch backed issues: ' + error);
        setStatus("unsupported");
        document.getElementById('issues').innerHTML = '';
    }
}

async function lookupSponsor() {
    setError('');
    setStatus('loading');
    data.account = document.getElementById('account').value;
    console.log('Looking up sponsor: ' + data.account);
    var branch = "main";
    {% if jekyll.environment == "production" %}
    // this is CI, use main branch
    {% else %}
    branch = "dev";
    {% endif %}

    try {
        const response = await fetch(`https://raw.githubusercontent.com/${data.account}/.github/${branch}/sponsorlink.jwt`);
        if (!response.ok) {
            throw new Error('SponsorLink not supported.');
        }

        const token = await response.text();
        var issuer = getIssuerFromJWT(token);
        console.log('Issuer:', issuer);
        data.issuer = issuer;

        const url = new URL(window.location);
        url.searchParams.set('s', data.account);
        // remove https:// and trailing slash from issuer
        if (issuer.startsWith('https://')) {
            issuer = issuer.slice(8);
        }
        if (issuer.endsWith('/')) {
            issuer = issuer.slice(0, -1);
        }
        url.searchParams.set('i', issuer);
        window.history.pushState({}, '', url);
        
        await displayIssues();
    } catch (error) {
        data.issuer = null;
        setStatus("unsupported");
        setError(`Valid <a href="{{ site.baseurl }}/github/#sponsorable-manifest">SponsorLink manifest</a> not found for ${data.account}.`);
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

function setStatus(status) {
    if (status != 'ok') {
        document.getElementById('issues').innerHTML = '';
        document.getElementById('user').innerHTML = '';
    }

    setBusy(status === 'loading');

    document.getElementById('unsupported').style.display = status === 'unsupported' ? '' : 'none';
    document.getElementById('supported').style.display = status === 'ok' ? '' : 'none';
    document.getElementById('unauthorized').style.display = status === 'unauthorized' ? '' : 'none';
}

// Function to decode Base64Url encoded string using atob()
function base64UrlDecode(str) {
    // Replace non-url compatible chars with base64 standard chars
    str = str.replace(/-/g, '+').replace(/_/g, '/');
    // Pad with trailing '='
    switch (str.length % 4) {
        case 0: break;
        case 2: str += '=='; break;
        case 3: str += '='; break;
        default: throw 'Illegal base64url string!';
    }
    // Decode base64 string
    return atob(str);
}

// Function to decode JWT and retrieve 'iss' claim
function getIssuerFromJWT(token) {
    try {
        // Split the JWT into its parts
        const parts = token.split('.');
        if (parts.length !== 3) {
            throw new Error('Invalid JWT');
        }

        // Decode the payload
        const payload = base64UrlDecode(parts[1]);

        // Parse the JSON
        const decodedToken = JSON.parse(payload);

        // Retrieve the 'iss' claim
        const issuer = decodedToken.iss;

        // Return the issuer
        return issuer;
    } catch (error) {
        console.error('Failed to decode JWT:', error);
        return null;
    }
}