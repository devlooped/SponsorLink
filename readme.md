# ![](https://github.com/devlooped/SponsorLink/raw/main/assets/img/sponsorlink-32.png) SponsorLink 

Core specification and reference implementation for integrating GitHub Sponsors into 
libraries and tools.

[![Spec](https://img.shields.io/github/v/release/devlooped/SponsorLink?include_prereleases&sort=semver&display_name=tag&label=spec&labelColor=EA4AAA&color=black)](https://www.devlooped.com/SponsorLink/spec.html)
[![Version](https://img.shields.io/nuget/v/dotnet-sponsor.svg?color=royalblue)](https://www.nuget.org/packages/dotnet-sponsor) 
[![Downloads](https://img.shields.io/nuget/dt/dotnet-sponsor.svg?color=green)](https://www.nuget.org/packages/dotnet-sponsor) 

Integrate [GitHub Sponsors](https://github.com/sponsors) into your libraries so that 
users can be properly linked to their sponsorship to unlock features or simply get 
the recognition they deserve for supporting your project. 

SponsorLink supports two scenarios:

1. Open source project developers or maintainers who are looking to incentivize 
   sponsors to contribute to the project, to ensure ongoing and recurring income 
   that can help ensure proper maintenance and further feature work.

2. Open source project consumers, who want to ensure their dependencies have 
   an active team that can provide support, bug fixes and add new features.

[Explore the documentation site](https://www.devlooped.com/SponsorLink).

## Why GitHub sponsors?

![Octocat lifted by a sponsors heart-shaped globe](/assets/img/sponsors-mona.png)

[GitHub Sponsors](https://github.com/sponsors/) is a great way to support open 
source projects, and it's available throughout most of the world. 

That is not to say that there aren't other mechanisms that can provide similar 
functionality and support. At this point, however, the tooling, API access and 
very low barrier to entry make it a great initial choice for SponsorLink.

That said, the reference implementation is not deeply tied to GitHub Sponsors, 
and the specification is entirely agnostic to the sponsorship platform. 

The value SponsorLink brings is in providing the "missing" link between a user's 
sponsorship and the libraries they use, in an easy to check, secure and offline 
way.

<!-- #package -->
## How it works

Roughly, the reference implementation works as follows:

1. A library/tool author adds a check (i.e. on usage, build, etc.) for a 
   [sponsor manifest](https://www.devlooped.com/SponsorLink/spec.html#sponsor-manifest) 
   at a well-known location in the local machine (i.e. `~/.sponsorlink/github/devlooped.jwt.`). If not found, the library/tool issues a notice to the user, typically stating 
   that they are seeking funding, how to fund the project and how to sync their status, 
   which is unknown at this point.
2. User decides to sponsor the project, does so on github.com
3. User installs the suggested [dotnet sponsor global tool](https://www.nuget.org/packages/dotnet-sponsor) and runs `sponsor sync [account]` to sync their sponsorships.
   * On first run, user accepts usage terms and conditions.
4. The tool fetches the author's [sponsorable manifest](https://www.devlooped.com/SponsorLink/spec.html#sponsorable-manifest) from their community files repo 
   at `https://github.com/[account]/.github/blob/[default_branch]/sponsorlink.jwt` and 
   uses its information to authenticate the user on github.com with an OAuth app belonging 
   to the author, using device flow.
5. The resulting authentication token is used to invoke the author's backend ("issuer") 
   API to retrieve the user's sponsor manifest (if any) and persist it at the well-known location 
   mentioned in step 1. This manifest is signed, has an expiration date and can be 
   verified by the library/tool without any network access.

Notes:
* Sponsor manifest expires monthly (like GitHub sponsorships themselves) and is signed 
   with a private key only the author has access to but is public and accessible on the 
   sponsorable manifest.
* Users can optionally turn on/off auto-sync, so that after the first sync, the author can 
   automatically refresh the manifest on the user's behalf by re-running the sync command 
   unattended.
* Users can have the following role claims:
   * `user`: the user is direct sponsor of the account.
   * `org`: the user is a member of an organization that sponsors the account.
   * `contrib`: the user is a contributor to the account's project(s).
   * `team`: the user is team a member of the author's organization.
* Typically, an autor would consider any of the above roles to qualify as an active 
   sponsor, but the actual behavior is up to the library/tool author.

[Explore the documentation site](https://www.devlooped.com/SponsorLink) to learn more, 
and make sure to check the [privacy statement](https://www.devlooped.com/SponsorLink/privacy.html).

## Integrating via NuGet for .NET

The reference implementation .NET global tool, `dotnet-sponsor`, provides generic 
manifest discovery and sync capabilities, but the actual check from within a library 
or tool is left to the author.

Since the sponsor manifest is a standard JWT token, it can be verified by any JWT
library in any language and at any point in the library/tool usage (at installation 
time, run-time, build-time, etc.).

If you are looking for inspiration on how to do this for .NET with NuGet and C#, 
check the code we use ourselves in [the devlooped OSS template repo](https://github.com/devlooped/oss/tree/main/src/SponsorLink).

