---
title: Home
nav_order: 1
page_toc: false
---
# ![](https://github.com/devlooped/SponsorLink/raw/main/assets/img/sponsorlink-32.png) SponsorLink 

SponsorLink's goal is to raise awareness of your OSS projects' funding needs, 
help users sponsor them and thus improve their longer-term sustainability.

At its core it is a [standard manifest format](spec.md), but reference implementations 
and tools are provided to make it easy to integrate into your project.

SponsorLink can be used to remind users of sponsoring options and to unlock 
sponsors-only features (such as additional APIs or tooling).

The reference implementation supports [GitHub Sponsors](https://github.com/sponsors) 
at the moment, but other sponsorsing platforms might be added in the future.

The following applies to the reference implementation.

## How it works

Whenever a SponsorLink-enabled library is used, the following flow is typical:

1. The library runs a check for a local sponsor manifest file at a 
   well-known location using the [standard manifest format](spec.md). If present 
   and valid (and not expired), the library can use this information to determine 
   the user's sponsorship status and potentially unlock sponsors-only features.
2. If manifest is not found, users may get a warning, informing them that the 
   library is seeking funding and instructions on how to sponsor.
3. If the user decides to sponsor the project, and does so on the suggested platform 
   (i.e. `https://github.com/sponsors/[sponsorable]`), they now need to link their sponsorship 
   on their local machine to remove the warning message and potentially unlock sponsors-only 
   features.

{: .note }
> SponsorLink does not determine how libraries and tools act upon the presence
> or absence of the manifest.

The synchronization is performed by a separate tool, which runs interactively so 
as to properly inform the user of the actions being taken and to ensure proper consent 
is given before any data is exchanged.

### Sponsor Manifest Sync

The reference implementation of SponsorLink leverages the [GitHub CLI](https://cli.github.com/) 
to lookup a user's sponsorships and sync them locally for offline use.

The tool is implemented as a [GitHub CLI extension](https://docs.github.com/en/github-cli/github-cli/using-github-cli-extensions) 
which can be installed by running the following command:

```shell
gh extension install devlooped/gh-sponsors
```

On first run, the tool provides the usage terms, private policy and asks for
consent to proceed.

Subsequently (periodically or on-demand), the user runs `gh sponsors` to 
sync their sponsorships for offline use while consuming sponsorable libraries. 

{: .highlight }
> Running `gh sponsor sync [sponsorable]` will sync the manifest for the specified account.
> and typically be much quicker than the entire discovery + sync for all candidate 
> accounts.

Whenever run, the tool performs the following steps (entirely locally without 
any involved intermediary):

1. Determine sponsorable account candidates for the current user, using the
   GitHub API to list all directly sponsored accounts, organizations the user is a 
   member of and their (public) sponsorships, and all repositories the user has contributed
   to, which can be considered as indirect sponsorships.
1. Each candidate is checked for a SponsorLink manifest at `https://github.com/[account]/.github/raw/main/sponsorlink.jwt`.
   This location is the same as the.github/ [default community health files](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/creating-a-default-community-health-file)
1. If found, the manifest consist of a signed [JWT](https://jwt.io) containing some 
   [standard claims](spec.html#sponsorable-manifest) plus a [client_id](https://www.rfc-editor.org/rfc/rfc8693.html#name-client_id-client-identifier)
   claim. This is the Client ID of a [GitHub OAuth](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/creating-an-oauth-app) 
   app belonging to the sponsorable with Device Flow authorization enabled.
1. At this point the user is directed to authenticate on github.com with an OAuth app provided 
   by the sponsorable account which requests the necessary permissions to read the user's profile 
   and email(s) for sponsorship linking. 
1. A request is made to the issuer's backend using the obtained OAuth access token, to get a 
   signed [sponsor manifest](spec.html#sponsor-manifest) which is then stored locally at `~/.sponsorlink/github/[sponsorable].jwt`.

{: .highlight }
The `gh-sponsors` extension uses a separate OAuth access token for each sponsorable 
to guarantee isolation and ensure explicit permissions are granted individually.

After a successful sync of the [sponsor manifest](spec.html#sponsor-manifest), the 
libraries and tools can now check for its presence, authenticity and expiration 
(entirely offline) and change their behavior accordingly.

*SponsorLink* itself does not dictate how a specific library or tool integrates 
these checks, it only provides the [standard manifest format](spec.md) 
that a sponsorable account backend needs to provide for local persistence 
and subsequent (purely offline) checks.
