# ![](https://github.com/devlooped/SponsorLink/raw/main/assets/img/sponsorlink-32.png) SponsorLink 

Integrate [GitHub Sponsors](https://github.com/sponsors) into your libraries so that 
users can be properly linked to their sponsorship to unlock features or simply get 
the recognition they deserve for supporting your project. 

SponsorLink supports two scenarios:

1. Open source project developers or maintainers who are looking to incentivize 
   sponsors to contribute to the project, to ensure ongoing and recurring income 
   that can help ensure proper maintenance and further feature work.

2. Open source project consumers, who want to ensure their dependencies have 
   an active team that can provide support, bug fixes and add new features.


## Why only GitHub sponsors?

![Octocat lifted by a sponsors heart-shaped globe](/assets/img/sponsors-mona.png)

[GitHub Sponsors](https://github.com/sponsors/) is a great way to support open 
source projects, and it's available throughout most of the world. 

That is not to say that there aren't other mechanisms that can provide similar 
functionality and support. At this point, however, the tooling, API access and 
very low barrier to entry make it a great initial choice for SponsorLink.

That said, the technical implementation is not deeply tied to GitHub Sponsors, 
and might evolve to include other platforms in the future, provided they offer 
similar levels of integration and support.

The value SponsorLink brings is in providing the "missing" link between a user's 
sponsorship and the libraries they use, in an easy to check, secure and offline 
way.

<!-- #package -->
## How does it work?

1. A library author adds a check for an environment variable named `SPONSORLINK_MANIFEST` 
   which contains a signed JWT with a bunch of hashes that represents a user's sponsorships.
2. If it's not found (or expired or its signature is invalid), the library issues a 
   notice (i.e. a diagnostic message) to the user, explaining they are seeking funding, 
   how to fund the project and how to sync their sponsorlink manifest.
3. The user decides to sponsor the project, does so on github.com, and then downloads 
   the [GitHub CLI](https://cli.github.com/) and installs the 
   [gh-sponsors](https://github.com/devlooped/gh-sponsors) extension by running 
   `gh extension install devlooped/gh-sponsors`.
4. The user `gh sponsorlink` to accept the usage terms and syncs their sponsorships 
   manifest.
5. Now the library can check for the `SPONSORLINK_MANIFEST` environment variable, 
   which will contain a signed JWT. The library can verify the signature and expiration, 
   and create (locally) a hash with `base62(sha256([salt]+[user/org]+[sponsored]))` 
   where:
   a. `salt` is the value of the `SPONSORLINK_INSTALLATION` environment variable, 
      initialized to a new GUID when the `gh sponsors` tool ran.
   b. `user/org` should be the user's email or his organization's domain name (for 
      org-wide sponsorships).
   c. `sponsored` is the GH sponsors account to check for sponsorships from the 
      user/org.
   d. If the JWT token contains a `hash` claim with the given hash, then the user 
      is a sponsor.
5. Now the user can either get additional features from the library, or simply have 
   the initial notice to go away. The actual behavior is up to the library author.

> NOTE: The manifest also contains sponsorships from organizations a user belongs to. 
> This relies on organizations' [verified domain](https://docs.github.com/en/organizations/managing-organization-settings/verifying-or-approving-a-domain-for-your-organization).

> NOTE: contributors to a project are also considered sponsors of the account(s) in 
> the project's `FUNDING.yml` file.

> NOTE: the email/domain never leaves the user's machine when checking for sponsorships 
> and it happens entirely offline against the JWT token persisted in the environment 
> variable.

## Privacy Considerations

SponsorLink is build by developers for developers. As such, we don't have an attorney 
or a big corporation backing this. If you're evaluating SponsorLink, it's likely because 
you are currently sponsoring or considering sponsoring the developer of a package you 
enjoy which has integrated SponsorLink, or you are considering integrating it in your 
library.

The short story is: we never persist or access *any* personally identifying information, 
with the sole exception being your GitHub user identifier (an integer like `169707`) 
which you authorize as part of authenticating with your GitHub account to our backend 
API (we use [Auth0](https://auth0.com) for this). This identifier is already public for
everyone on GitHub (i.e. open [Linus](https://api.github.com/users/torvalds)
or [microsoft](https://api.github.com/orgs/microsoft)).

Everything else is hashed and salted locally with a random GUID so the resulting hashes 
cannot be used to uncover any of the original information. All checks are performed 
fully offline with no connnection back to any external servers. Nothing about the user 
can be inferred from the hashes (other than their count).

Finally, a user can remove *all* traces (both locally and on the backend) of their 
interaction with SponsorLink by simply running `gh sponsorlink remove` and then delete 
the extension entirely with `gh extension remove sponsors`.

Please read the [full privacy policy](/privacy.md) to learn more.

<!-- #package -->

## Integrating via NuGet for .NET

[![Version](https://img.shields.io/nuget/vpre/Devlooped.SponsorLink.svg?color=royalblue)](https://www.nuget.org/packages/Devlooped.SponsorLink)
[![Downloads](https://img.shields.io/nuget/dt/Devlooped.SponsorLink.svg?color=green)](https://www.nuget.org/packages/Devlooped.SponsorLink)

Library authors can manually integrate SponsorLink whichever way they want (e.g. as 
a build task, custom analyzer, etc.), and just using any JWT library to check for 
the presence of a `hash` claim in the `SPONSORLINK_MANIFEST` environment variable.

To simplify this (already fairly simple) process, we provide a 
[NuGet package](https://www.nuget.org/packages/Devlooped.SponsorLink) that includes 
a few helper classes to make this easier, as development-dependency only and source-only.

For the most part, you just need to add a reference to the package, and then run this 
line of code in your library's code:

```csharp
SponsorLink.Initialize(
    [git repo root or project directory],
    [sponsorable account(s) to check for]);
```

After that, the `SponsorLink.Status` property provides the current status of the 
manifest: 

```csharp
public enum ManifestStatus { Expired, Invalid, NotFound, Verified }
```

There's an `SponsorLink.IsSponsor` property that returns `true` if the user is a 
sponsor of any of the accounts specified in the `Initialize` call, as well as 
`SponsorLink.IsEditor` property to avoid running outside the context of an actual 
editor (e.g. Visual Studio, Rider).

The [analyzer sample](/samples/dotnet/Analyzer) provides a complete example.

> NOTE: the SponsorLink package is *NEVER* a dependency of your library packages.
> All code is provided as source-only as helpers to make the standard JWT check easier.

