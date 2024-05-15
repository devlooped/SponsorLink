---
title: GitHub Sponsors
nav_order: 3
---
# GitHub Sponsors Reference Implementation
<!-- #content -->

## Overview

The reference implementation of the SponsorLink specification leverages [GitHub Sponsors](https://github.com/sponsors/)
to provide a simple and secure way for OSS projects on GitHub to properly attribute sponsorsing status to their users 
on their dev machine, to enable sponsorable libraries and tools to provide sponsor-specific features or benefits.


The reference implementation comprises two parts:

* Issuer backend service for self-hosting by sponsorable accounts.
* [GitHub CLI extension](https://docs.github.com/en/github-cli/github-cli/using-github-cli-extensions) for users to sync their sponsor manifest(s) with the backend service(s).

The primary goal of the reference implementation is to provide a simple way for sponsorables to 
issue sponsor manifests and for sponsors to sync them locally. The backend service is self-hosted by 
the sponsorable account, ensuring that no private data is shared with the SponsorLink developer.

## Sponsor Manifest Sync

The user-facing tool is implemented as a [GitHub CLI extension](https://docs.github.com/en/github-cli/github-cli/using-github-cli-extensions) 
which can be installed by running the following command:

```shell
gh extension install devlooped/gh-sponsors
```

{: .highlight }
On first run, the tool provides the usage terms, private policy and asks for
consent to proceed.

The user subsequently runs `gh sponsors` to sync their sponsorships for offline use while 
consuming sponsorable libraries. 

Whenever run, the tool performs the following steps:

1. Use the the GitHub CLI API to determine sponsorable candidates for the current user, which are:
    a. All directly sponsored accounts
    b. Publicly sponsored accounts by organizations the user is a member of
    c. Owners of all repositories the user has contributed to, considered indirect sponsporships
1. Each candidate is checked for a SponsorLink manifest at `https://github.com/[account]/.github/raw/main/sponsorlink.jwt`.
   This location is the same as the GitHub [default community health files](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/creating-a-default-community-health-file)
1. If found, the [sponsorable manifest](spec.html#sponsorable-manifest) `client_id` and `iss` claims are 
   used to authenticate with the sponsorable account's backend service and request the updated sponsor manifest. 

{: .important }
> Running `gh sponsor sync [account]*` will sync the manifest for specific account(s),
> and typically be much faster than the entire discovery + sync for all candidate 
> accounts.

This implementation honors the recommended convention for manifest location and places them 
at `~/.sponsorlink/github/[sponsorable].jwt`.

### Authentication

In order to avoid sharing any data (either by the sponsorable as well as the sponsor) with the 
SponsorLink developer, the tool relies on self-hosting of the issuer backend by each sponsorable, as 
well as the use of a sponsorable-provided GitHub OAuth apps to authenticate the user and request the necessary 
permissions to issue the sponsor manifest. In order to identify this OAuth app, this implementation requires 
an additional claim in the sponsorable manifest:

{: .important-title }
> client_id
> 
> A required [standard client_id OAuth 2.0 client identifier claim](https://www.rfc-editor.org/rfc/rfc8693.html#name-client_id-client-identifier) 
> provided in the sponsorable manifest for client authentication with the issuer through the GitHub OAuth app.

Example:

```json
{
  "iss": "https://sponsorlink.devlooped.com/",
  "aud": "https://github.com/sponsors/devlooped",
  "client_id": "asdf1234",
  "pub": "MII...=",
  "sub_jwk": {
    "e": "AQAB",
    "kty": "RSA",
    "n": "5inhv8Q..."
  }
}
```

Authentication is performed using the [device flow](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps#device-flow), 
where the sponsor gets a chance to review the requested permissions prior to granting access. The access token
is then used to request the sponsor manifest from the sponsorable backend service. 

{: .highlight }
> The default browser is launched to authenticate and enter the device code that is automatically copied to 
> the clipboard by the CLI extension.

To avoid having to authenticate every time for each sponsorable account, the tool caches the access token 
by `client_id` using the cross-platform credential store API provided by the [Git Credential Manager](https://github.com/git-ecosystem/git-credential-manager).

### Sponsoring Checks

SponsorLink-enabled libraries and tools can use the previously synchronized sponsor manifest to check the 
sponsoring status of the user. These checks are entirely offline and do not require any network access. 
Even though the publicly available sponsorable manifest contains the public key to verify the signature, 
the libraries and tools themselves would typically embed this public key for purely offline verification.

{: .important }
> This *SponsorLink* implementation does not dictate how a specific sponsorable library or tool integrates 
> these checks, it only provides the [manifest format](spec.md) and predictable location for the manifest.

### Auto Sync

When running the `gh sponsors sync` command, the tool will ask whether to enable auto-sync. If enabled, 
tools and libraries checking the manifest can attempt to refresh an expired manifest in an exclusively 
non-interactive way, by using the cached access token to request a new manifest from the sponsorable backend 
service. 

This unattended refresh is subject to the same consent and permissions as the initial sync, and it may 
not succeed (i.e. an interactive authentication to get new consent is needed). The user can disable 
auto-sync at any time by running `gh sponsors sync --autosync=false`.

Tools and libraries can check for this user preference setting by reading the [dotnetconfig](https://dotnetconfig.org/) 
formatted file at `~/.sponsorlink/.netconfig`.

```
[sponsorlink]
  autosync            # enabled
  autosync = false    # disabled
```

{: .note }
> An `autosync` key with no value assigned is shorthand for a `true` value.
> See [dotnetconfig](https://dotnetconfig.org/#format).

## Sponsorable Setup

Three steps are required to set up a GitHub account to issue sponsor manifests:
1. Create a GitHub OAuth app
1. Generate and upload a sponsorable manifest
1. Self-host the issuer backend service

### GitHub OAuth App

Follow the steps for [creating an OAuth app](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/creating-an-oauth-app) 
and make sure you enable Device Flow for it. Make note of the `Client ID` as well as a new `Client Secret` you will create. 
Fill in the application name, icon, description and other details as needed. Your sponsors will see this information when 
they authenticate for the first time.

The authorization callback URL will be `https://[app].azurewebsites.net/.auth/login/github/callback` (unless 
you provide a custom domain for your Azure Functions app), where `[app]` is the name of your Azure Functions app 
where you'll deploy the issuer backend service.

### Sponsorable Manifest

Next you will need to create a valid sponsorable manifest and upload it to your GitHub account's community 
health files repo (`.github` repo root).

The `gh sponsors` tool provides a command to generate it, `init`:

```shell
> gh sponsors init --help
DESCRIPTION:
Initializes a sponsorable manifest and token

USAGE:
    gh sponsors init [OPTIONS]

OPTIONS:
    -h, --help                 Prints help information
    -i, --issuer               The base URL of the manifest issuer web app
    -a, --audience <VALUES>    The intended audience or supported sponsorship platforms, e.g. https://github.com/sponsors/curl
    -c, --clientId             The Client ID of the GitHub OAuth application created by the sponsorable account
    -k, --key                  Existing private key to use. By default, creates a new one
```

If you were initializing the sponsorable manifest for the `curl` GitHub account, you would run:

```shell
gh sponsors init -i https://curl.azurewebsites.net -a https://github.com/sponsors/curl -c [client_id]
```

This will generate a few files in the current directory: 
* `curl.key`: a newly created RSA 3072bits private key in PKCS#1 binary format
* `curl.key.jwk`: same key in JWK format
* `curl.key.txt`: same key in Base64-encoded format
* `curl.pub`: corresponding public key in PKCS#1 binary format
* `curl.pub.jwk`: same public key in JWK format
* `curl.pub.txt`: same public key in Base64-encoded format
* `sponsorlink.jwt`: the sponsorable manifest in JWT format

Place the `sponsorlink.jwt` file in the `.github` repo root of the `main` branch for the `curl` account.
Move the other files to a secure location, as they are needed to sign the sponsor manifests.
The Base64-encoded public and private keys are used by the issuer backend service next.

You can copy this [real world SponsorLink manifest](https://github.com/devlooped/.github/blob/main/sponsorlink.jwt)
into [jwt.io](https://jwt.io) and inspect its contents.

## Sponsorable Backend Self-Hosting

The reference implementation of the GitHub-based issuer backend service is an Azure Functions app that 
provides a REST API to issue sponsor manifests. The sync tool makes no additional assumptions about the 
backend other than:

1. It provides a `/sync` endpoint that accepts a `Authorization: Bearer [token]` header with an access token 
   obtained by authenticating with the sponsorable's GitHub OAuth app.
1. It returns a signed sponsor manifest in the [standard format](spec.md) with the required claims that can 
   be verified against the public key provided in the `sponsorlink.jwt` sponsorable manifest.
1. The returned manifest contains `roles` for the user, determined based on the token provided.

To deploy and configure the backend: 

1. Fork the [SponsorLink](https://github.com/devlooped/SponsorLink) repository
1. [Create an Azure Functions app](https://portal.azure.com/#create/Microsoft.FunctionApp)
1. Setup deployment to the Azure Functions app from your forked repository
1. Configure the following application settings:
    * `GitHub__Token`: a GitHub token with permissions to read the sponsorable profile, emails, sponsorships and repositories
    * `SponsorLink__Account`: the sponsorable GitHub account name, unless it's the same as the GitHub token owner
    * `SponsorLink__PublicKey`: the Base64-encoded public key from the sponsorable manifest (the contents of `curl.pub.txt` in the example above)
    * `SponsorLink__PrivateKey`: the Base64-encoded private key (the contents of `curl.key.txt` in the example above)

Finally, enable the GitHub identity provider under Settings > Authentication, providing the OAuth app's 
Client ID and Client Secret. Make sure you set `Allow unauthenticated requests` and have `Token store` enabled.

At this point, go back to the GitHub OAuth app settings and and update the `Authorization callback URL` to 
`https://[app].azurewebsites.net/.auth/login/github/callback`.

{: .highlight }
The backend's authentication and configuration can be tested manually by navigating to `https://[app].azurewebsites.net/me`, 
which would redirect to the GitHub OAuth app for authentication and upon returning to your issuer site, return the 
user's profile and claims as JSON.

## Conclusion

By requiring a GitHub OAuth app for each the sponsorable, the reference implementation avoids having a central 
authority for issuing manifests, which would require sharing private data (e.g. sponsorships and repository 
contributions) with the SponsorLink developer. In addition, this approach provides an explicit consent step 
where sponsors can decide to grant only the necessary permissions to the sponsorable account for manifest 
issuance.

The user, in turn, can conveniently revoke access at any time in their [GitHub applications settings](https://github.com/settings/applications).