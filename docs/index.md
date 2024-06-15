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

After a successful sync of the [sponsor manifest](spec.html#sponsor-manifest), the 
libraries and tools can now check for its presence, authenticity and expiration 
(entirely offline) and change their behavior accordingly.

*SponsorLink* itself does not dictate how a specific library or tool integrates 
these checks, it only provides the [standard manifest format](spec.md) 
that a sponsorable account backend needs to provide for local persistence 
and subsequent (purely offline) checks.
