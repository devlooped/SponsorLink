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

> NOTE: this reference implementation uses your configured git email (locally) 
> to link your GitHub sponsorships. Read the [privacy policy](https://github.com/devlooped/SponsorLink/blob/main/privacy.md) 
> for more details.

*SponsorLink* itself does not dictate how a specific sponsorable library
or tool integrates these checks, it only provides the [standard manifest format](spec.md) 
and this reference implementation provides tooling to generate and sign it.

## FAQ
<!-- include faq.md#content -->