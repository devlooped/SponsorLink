---
title: Back an Issue
parent: GitHub Sponsors
page_toc: false
---

<div id="spinner" class="spinner text-green-200" role="status"></div>

# Back an Issue

SponsorLink's got a new trick up its sleeve: you can now support specific GitHub issues! 
Just make a one-time donation to any GitHub Sponsors account that's turned on SponsorLink,
and come back here to back an issue. 

This way, you can push for the features you want and directly back the development that 
excites you. Pretty neat, huh?

<div id="sponsorable">
    <table class="borderless" style="border-collapse: collapse; padding: 4px; min-width: unset;" markdown="0">
        <tr>
            <td class="borderless">GitHub Sponsor account:</td>
            <td class="borderless">
                <input id="account" onblur="lookupSponsor();" class="border">
            </td>
            <td class="borderless">
                <div>
                    <button class="btn btn-green" onclick="lookupSponsor();">Go</button>
                </div>
            </td>
            <td class="borderless" style="display: none;" id="unsupported">
                ⚠️ SponsorLink is not supported
            </td>
            <td class="borderless" style="display: none;" id="supported">
                ✅ SponsorLink supported
            </td>
            <td class="borderless" style="display: none;" id="unauthorized">
                <a href="/github" id="login">
                    <button class="btn btn-green">Sign in via GitHub</button>
                </a>
            </td>
        </tr>
    </table>
</div>

<p id="error" class="no-before" />

<div id="user"></div>
<div id="issues"></div>

## How it works

Any GitHub account can be SponsorLink-enabled by following the instructions in the 
[sponsorable setup]({{ site.baseurl }}/github/#sponsorable-setup) guide. Once enabled,
the sponsorable will provide a `sponsorlink.jwt` file via their `[account]/.github` repository, 
which contains the [relevant information]({{ site.baseurl }}/spec/#sponsorable-manifest) 
for this page to retrieve sponsorships and previously backed issues. 

For example, you view the contents of [devlooped's manifest](https://raw.githubusercontent.com/devlooped/.github/main/sponsorlink.jwt) 
directly at [jwt.io](https://jwt.io/#debugger-io?token=eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJodHRwczovL3Nwb25zb3JsaW5rLmRldmxvb3BlZC5jb20vIiwiYXVkIjoiaHR0cHM6Ly9naXRodWIuY29tL3Nwb25zb3JzL2Rldmxvb3BlZCIsImlhdCI6IjE3MTgyMjQ4NzMiLCJjbGllbnRfaWQiOiJhODIzNTBmYjJiYWU0MDdiMzAyMSIsInN1Yl9qd2siOnsiZSI6IkFRQUIiLCJrdHkiOiJSU0EiLCJuIjoiNWluaHY4UXltYURCT2loTmkxZVktNi1oY0lCNXFTT05GWnhieHhYQXlPdHhBZGpGQ1BNLTk0Z0lacU05Q0RyWDNweWcxbFRKZm1sX2FfRlpTVTlkQjFpaTVtU1hfbU5IQkZYbjFfbF9naTFFcmRia0lGNVliVzZveFdGeGYzRzVtd1ZYd25QZnhIVHlRZG1XUTNZSlItQTNFQjRrYUZ3THFBNkhhNWxiMk9iR3BNVFFKTmFrRDRvVEFHRGhxSE1HaHU2UHVwR3E1aWU0cVpjUTdOOEFOdzh4SDduaWNUa2JxRWhRQUJIV09UbUxCV3E1ZjVGNlJZR0Y4UDdjbDBJV2xfdzRZY0laa0dtMnZYMmZpMjZGOUY2MGNVMXYxM0daRVZEVFhwSjlrenZZZU05c1lrNmZXYW95WTJqaEU1MXFidjBCMHU2aFNjWmlMUkV0bTNuN0NsSmJJR1hoa1VwcEZTMkpsTmFYM3JnUTZ0LTRMSzhnVVR5THQzekRzMkg4T1p5Q3dsQ3BmbUdtZHNVTWttMXhYNnQyci05NVUzenl3eW54b1daZmpCQ0pmNDFsZU05T01LWXdOV1o2TFFNeW84M0hXdzFQQklyWDRaTENsRndxQmNTWXNYRHlUOF9aTGQxY2RZbVBmbXRsbElYeFpoTENsd1Q1cWJDV3Y3M1YifX0.bTH1YjSuhaR0FGsGGcovzXpbT23pRdgZHiwjstlJ7FIyv_6UJ7SsR2x3K5Om8M1C7GaCQWdI_Hu92-0elLHG8yMeD-XbTtYD2z7td1DjmYV7mFNArKOKrg5pjNthrMP8U2yklC44bXcUOjWxHa5amT75Gr7L7mx5Evoe69yoE30ZhW1FgWJgQOHLvEGVyeEEXN96DzU3Ng5GqZZVNAs1zMd6RcraurmY_fr7FAMOlrCCnf6XmFxM8N0paJMWqBkgaO7h4bgzqVjGgUc1l6YKTR-zyLySOh4dcMbxLpAI6gwNImOVAtpC0cO8uL1bXQM86xrmYEDZ8HgwcRnrFtKZ3WQ7PoVs77fjiv8lChgDTEJWalWI7nGapkQDVA2-Hn1Ex_XiXW_KIuUrR7Y-Zt4f6GBiSIJmRL4s4YcgboEW61Lto0h5k_AZ5S2kL-OD4Qx2rudhsEEi2QP1oUW4aLTpmTHy5RAgLD-sGYnSZKDSaOBJ5aOUSilHEbBxy4_yeJgY) to inspect the provided information.

The included GitHub OAuth app identifier ([client_id](https://www.rfc-editor.org/rfc/rfc8693.html#name-client_id-client-identifier)), 
is used to authenticate the user and provide explicit consent on information to be shared. 
Once authenticated, this page invokes the self-hosted backend provided by the sponsorable 
to retrieve the list of one-time sponsorships available for backing new issues as well 
as the previously backed issues and their accumulated amounts. The backend URL is provided 
in the `iss` claim of the [manifest JWT]({{ site.baseurl }}/spec/#sponsorable-manifest), 
with the added path `/github/issues`.

Even though this static page is hosted by [Devlooped](https://devlooped.com) as part of 
SponsorLink documentation, it works with any SponsorLink-enabled account. The developer 
of the backend reference implementation of SponsorLink, does not have any visibility 
into the sponsorships or the sponsorable's GitHub account since it's all 
[self-hosted]({{ site.baseurl }}/github/#sponsorable-backend-self-hosting) by each 
project owner.
 
<script>
var template = Handlebars.compile(`
{{ site.issues_template }}
`);

var data = {
    "account": "devlooped",
    "issuer": "https://sponsorlink.devlooped.com",
    "unsupported": false
};

//if site.env.CI != "true" then branch = "dev" else branch = "main"
{% if site.env.CI != "true" %}
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
    {% if site.env.CI != "true" %}
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

</script>