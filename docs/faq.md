---
title: FAQ
nav_order: 4
page_toc: false
---
# FAQ
<!-- #content -->
These are some frequently asked questions about the [reference implementation](github/index.md) 
of the [standard manifest format](spec.md) available in the 
[SponsorLink](https://github.com/devlooped/SponsorLink/) and 
[`gh-sponsors`](https://github.com/devlooped/gh-sponsors/) repositories.

1. Does SponsorLink "phone home" or track users?
   
   **NO.** 

2. Does SponsorLink require any changes to my project's code?

   **NO.**

3. Does SponsorLink get access to my private sponsorship data?

   **NO.** The [dotnet-sponsors](https://nuget.org/packages/dotnet-sponsors) 
   dotnet global tool is OSS and runs entirely locally. It can do sponsorships 
   discovery if you need to, and in that case, uses your authenticated 
   [GH CLI](https://cli.github.com/) to determine your sponsorships (either 
   direct or indirect via organization membership or active contributions), 
   and subsequently requests a sponsor manifest from the sponsored accounts.
   This is NOT needed when synchronizing a specific account.

4. Does the SponsorLink developer (Devlooped) get access to my profile
   or sponsorship data when synchronizing other account's manifests?

   **NO.** The backend that signs manifests must be self-hosted by each sponsored 
   account that wants to leverage SponsorLink. This code is also OSS at the 
   [SponsorLink repo](https://github.com/devlooped/SponsorLink/). 
   
5. Are my local [GH CLI](https://cli.github.com/) credentials exposed in any 
   way to a sponsorable account that uses SponsorLink?

   **NO.** Before invoking the sponsored account backend to provide a signed manifest, 
   the tool will prompt you to authenticate with GitHub.com using an OAuth app 
   provided by the sponsored account. You can reject this request or revoke access 
   at any time in your [GitHub settings](https://github.com/settings/applications).

6. Does a SponsorLink-enhanced library get access to my private sponsorship data?
   
   **NO.** The library and its tools can only check for the presence of a previously
   sync'ed manifest.

7. Can I remove all traces of SponsorLink locally and remotely?
   
   **YES.** You can run `sponsors remove [account|all]`. You can 
   then remove the `sponsors` CLI with `dotnet uninstall dotnet-sponsors`.

8. Can I use SponsorLink with my own sponsorsing platform?
   
   **YES.** SponsorLink is a standard manifest format and you can create equivalent 
   tools for manifest generation and signing. You can also suggest improvements 
   to the reference implementation to incorporate other platforms that expose 
   sponsorships via API.

9. Is there a privacy policy?
   
   **YES.** Read the [privacy policy](privacy.md) for more details.

10. Does the sponsorable account have access to my sponsorship information?
    
    **YES.** Accounts you are currently sponsoring already have this information as 
    part of their profile. SponsorLink itself does not use any information they 
    don't already have access to, and the SponsorLink developer (Devlooped) does 
    not have access to any of this information since it doesn't offer hosting for 
    the backend that signs manifests.

