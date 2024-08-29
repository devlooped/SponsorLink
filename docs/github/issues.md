---
title: Back an Issue
parent: GitHub Sponsors
---

# Back an Issue

This page allows you to back an issue on GitHub. If you arrived at this 
page from an email or link after a one-time sponsorship, you can specify 
the issue URL you would like to back below.

{% if site.env.CI == "true" %}
{% else %}
  <p class="highlight">Built for localhost</p>
{% endif %}

<style>
  .no-before::before {
    content: none !important;
  }
</style>

<div id="sponsorable" markdown="0">
    <table class="borderless" style="border-collapse: collapse; padding: 4px; min-width: unset;" markdown="0">
        <tr>
            <td class="borderless">Enter the sponsor account:</td>
            <td class="borderless">
                <input id="account" class="border">
            </td>
            <td class="borderless">
                <div>
                    <button class="btn btn-green" onclick="lookupSponsor();">Go</button>
                </div>
            </td>
            <td class="borderless" id="loading">
                <div class="spinner-border text-green-200" role="status"></div>                
            </td>
            <td class="borderless" style="display: none;" id="unsupported">
                ⚠️ SponsorLink is not supported
            </td>
            <td class="borderless" style="display: none;" id="supported">
                ✅ SponsorLink supported
            </td>
            <td class="borderless" style="display: none;" id="unauthorized">
                <a href="/github" id="login">
                    <button class="btn btn-green">Sign in via SponsorLink</button>
                </a>
            </td>
        </tr>
    </table>
</div>

<div id="user"></div>
<div id="issues"></div>

<script>
var template = Handlebars.compile(`
{{ site.issues_template }}
`);

var data = {
    "account": "devlooped",
    "issuer": "https://sponsorlink.devlooped.com",
    "unsupported": false
};

const urlParams = new URLSearchParams(window.location.search);
var sponsorable = urlParams.get('s');
if (sponsorable !== null && sponsorable !== "") {
    data.account = sponsorable;
    document.getElementById('account').value = sponsorable;
}

var issuer = urlParams.get('i');
if (issuer !== null && issuer !== "") {
    data.issuer = issuer;
    displayIssues();
} else {
    lookupSponsor();
}

function displayIssues() {
    var issuer = data.issuer;
    if (!issuer.startsWith('https://')) {
        issuer = 'https://' + issuer;
    }
    if (issuer.endsWith('/')) {
        issuer = issuer.slice(0, -1);
    }

    fetch(issuer + '/github/issues', {
            method: 'GET',
            credentials: 'include'
        })
        .then(function(response) {
            if (!response.ok) {
                throw new Error('SponsorLink issuer failure.');
            }
            return response.text()
        })
        .then(function(json) {
            var response = JSON.parse(json);
            setStatus(response.status);
            console.log(json);
            if (response.status === 'unauthorized') {
                document.getElementById('login').href = response.loginUrl;
            } else if (response.status === 'ok') {
                document.getElementById('user').innerHTML = `Welcome back ${response.user}!`;
                document.getElementById('issues').innerHTML = template(response);
            } else {
                document.getElementById('issues').innerHTML = json;
            }
        })
        .catch(function(err) {  
            console.log('Failed to fetch page: ', err);  
            setStatus("unsupported");
        });
}

function lookupSponsor() {
    data.account = document.getElementById('account').value;
    console.log('Looking up sponsor: ' + data.account);
    var branch = "main";
    {% if site.env.CI != "true" %}
    branch = "dev";
    {% endif %}

    fetch('https://raw.githubusercontent.com/' + data.account + '/.github/' + branch + '/sponsorlink.jwt')
        .then(function(response) {
            if (!response.ok) {
                throw new Error('SponsorLink not supported.');
            }
            return response.text()
        })
        .then(function(token) {
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
            
            displayIssues();
        })
        .catch(function(err) {  
            data.issuer = null;
            setStatus("unsupported");
        });
}

function setStatus(status) {
    if (status != 'ok') {
        document.getElementById('issues').innerHTML = '';
        document.getElementById('user').innerHTML = '';
    }

    document.getElementById('loading').style.display = 'none';
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

</script>