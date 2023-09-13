---
title: Home
nav_order: 1
---
# ![](https://github.com/devlooped/SponsorLink/raw/main/assets/img/sponsorlink-32.png) SponsorLink 

SponsorLink's goal is to raise awareness of your project's funding needs, 
help users sponsor your project and thus improve the sustainability of OSS 
projects.

At its core it is a [standard manifest format](spec.md), but reference implementations 
and tools are provided to make it easy to integrate into your project.

SponsorLink can be used to remind users of sponsoring options and to unlock 
sponsors-only features (such as additional APIs or tooling).

This reference implementation supports [GitHub Sponsors](https://github.com/sponsors) 
at the moment, but other sponsorsing platforms might be added in the future.

The following applies to this reference implementation.

## How it works

1. Sponsorable library users will typically get a warning message when they 
   use the library, informing them that the library is seeking funding and 
   how to sponsor it.
2. The user decides to sponsor the project, does so on github.com. They now 
   need to link their sponsorship on their local machine to remove the warning 
   message and potentially unlock sponsors-only features.
3. They download the [GitHub CLI](https://cli.github.com/) and installs the 
   [gh-sponsors](https://github.com/devlooped/gh-sponsors) extension by running 
   `gh extension install devlooped/gh-sponsors`.
4. The user runs `gh sponsors` to accept the usage terms and sync/link a 
   sponsorship manifest.
   > At this point they authenticate with their GitHub account and accept 
   > the usage terms.
5. The sponsorable library can now check for the manifest and change its 
   behavior accordingly.

{: .info }
> NOTE: the reference implementation uses your configured git email (locally) 
> to link your GitHub sponsorships. Read the [privacy policy](privacy.md) 
> for more details.

*SponsorLink* itself does not dictate how a specific sponsorable library
or tool integrates these checks, it only provides the [standard manifest format](spec.md) 
and this reference implementation provides tooling to generate and sign it.

## FAQ
<!-- include faq.md#content -->
<!-- #content -->

1. Does SponsorLink "phone home" or track users?
   
   NO. 

2. Does SponsorLink require any changes to my project's code?

   NO.

3. Does SponsorLink get access to my private sponsorship data?

   NO. The [`gh sponsors`](https://github.com/devlooped/gh-sponsors) tool is 
   OSS and runs entirely locally. It uses your authenticated 
   [GH CLI](https://cli.github.com/) to read your account's email(s) and generate 
   a set of [hashes](spec.md#hashing) to represent your sponsorships. 
   These hashes are sent to the [backend](https://github.com/devlooped/SponsorLink/blob/main/src/App/Functions.cs#L57)
   exclusively for signing. The backend can never reverse the hashes to get 
   your email(s) or sponsorship data since they are salted with a locally-generated 
   GUID on your machine which is never sent to the backend.

4. Does a SponsorLink-enhanced library get access to my private sponsorship data?
   
   NO. The library can only create a new [hash](spec.md#hashing) and check for its 
   presence in the manifest. To do so, it may use your configured git email (locally) 
   to generate the hash to check locally and offline, but it never sends it anywhere.

5. Can I remove all traces of SponsorLink locally and remotely?
   
   YES. You can run `gh sponsors remove` (which will remove even your Auth0 
   [user account](https://github.com/devlooped/SponsorLink/blob/main/src/App/Functions.cs#L26] 
   in the backend) and also clear all local environment variables. You can 
   then remove the `gh-sponsors` extension with `gh extension remove sponsors`.

6. Can I use SponsorLink with my own sponsorsing platform?
   
   YES. SponsorLink is a standard manifest format and you can create equivalent 
   tools for manifest generation and signing. You can also suggest improvements 
   to the reference implementation to incorporate other platforms that expose 
   sponsorships via API.

7. Is there a privacy policy?
   
   YES. Read the [privacy policy](privacy.md) for more details.
<!-- faq.md#content -->
