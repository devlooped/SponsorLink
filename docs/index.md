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

<!-- include github.md#sync -->
<!-- #sync -->
## Sponsor Manifest Sync

The user-facing tool is implemented as a [dotnet global tool](https://nuget.org/packages/dotnet-sponsor) 
which can be installed by running the following command:

```shell
dotnet tool install -g dotnet-sponsor
```

(or `dotnet tool update -g dotnet-sponsor` to update to the latest version).

{: .highlight }
On first run, the tool provides the usage terms, private policy and asks for consent to proceed.

The user subsequently runs `sponsors sync [account]*` to sync the manifest for the given account(s) 
for offline use while consuming sponsorable libraries. 

Whenever run, the tool performs the following steps:

1. If no accounts were provided, automatic discovery is offered, which involves using the the GitHub CLI API 
   to determine sponsorable candidate accounts for the current user, which are:

   - [x] All directly sponsored accounts
   - [x] Publicly sponsored accounts by organizations the user is a member of
   - [x] Sponsorables of repositories the user has contributed to, considered indirect sponsporships

1. Each account is checked for a SponsorLink manifest at `https://github.com/[account]/.github/raw/main/sponsorlink.jwt`.
   This location is the same as the GitHub [default community health files](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/creating-a-default-community-health-file)
1. If found, the [sponsorable manifest](spec.html#sponsorable-manifest) `client_id` and `iss` claims are 
   used to authenticate with the sponsorable account's backend service and request the updated sponsor manifest. 

{: .important }
> Running `sponsor sync [account]*` will sync the manifest for specific account(s),
> and typically be much faster than the entire discovery + sync for all candidate 
> accounts.

This implementation honors the recommended convention for manifest location and places them 
at `~/.sponsorlink/github/[sponsorable].jwt`.
<!-- #sync -->
<!-- github.md#sync -->

After a successful sync of the [sponsor manifest](spec.html#sponsor-manifest), the 
libraries and tools can now check for its presence, authenticity and expiration 
(entirely offline) and change their behavior accordingly.

*SponsorLink* itself does not dictate how a specific library or tool integrates 
these checks, it only provides the [standard manifest format](spec.md) 
that a sponsorable account backend needs to provide for local persistence 
and subsequent (purely offline) checks. See [sponsoring checks](github.md#sponsoring-checks) 
for some examples of how this might be done.
