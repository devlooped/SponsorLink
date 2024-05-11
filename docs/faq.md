---
title: FAQ
nav_order: 3
page_toc: false
---
# FAQ
<!-- #content -->
These are some frequently asked questions about the [reference implementation](github.md) 
of the [standard manifest format](spec.md) available in the 
[SponsorLink](https://github.com/devlooped/SponsorLink/) and 
[`gh-sponsors`](https://github.com/devlooped/gh-sponsors/) repositories.

1. Does SponsorLink "phone home" or track users?
   
   **NO.** 

2. Does SponsorLink require any changes to my project's code?

   **NO.**

3. Does SponsorLink get access to my private sponsorship data?

   **NO.** The [`gh sponsors`](https://github.com/devlooped/gh-sponsors) tool is 
   OSS and runs entirely locally. It uses your authenticated 
   [GH CLI](https://cli.github.com/) to determine your sponsorships (either 
   direct or indirect via organization membership or active contributions), 
   and subsequently requests a sponsor manifest from the sponsored accounts.

4. Does the SponsorLink developer (Devlooped) get access to my profile
   or sponsorship data?

   **NO.** The backend that signs manifests must be self-hosted by each sponsored 
   account that wants to leverge SponsorLink. This code is also OSS at the 
   [SponsorLink repo](https://github.com/devlooped/SponsorLink/).
   
5. Are my local [GH CLI](https://cli.github.com/) credentials exposed in any 
   way to a sponsorable account that uses SponsorLink?

   **NO.** Before invoking the sponsored account backend to provide a signed manifest, 
   the tool will prompt you to authenticate with GitHub.com using an OAuth app 
   provided by the sponsored account. You can reject this request or revoke access 
   at any time in your GitHub settings.

4. Does a SponsorLink-enhanced library get access to my private sponsorship data?
   
   **NO.** The library and its tools can only check for the presence of a previously
   sync'ed manifest.

5. Can I remove all traces of SponsorLink locally and remotely?
   
   **YES.** You can run `gh sponsors remove [account|all]`. You can 
   then remove the `gh-sponsors` extension with `gh extension remove sponsors`.

6. Can I use SponsorLink with my own sponsorsing platform?
   
   **YES.** SponsorLink is a standard manifest format and you can create equivalent 
   tools for manifest generation and signing. You can also suggest improvements 
   to the reference implementation to incorporate other platforms that expose 
   sponsorships via API.

7. Is there a privacy policy?
   
   **YES.** Read the [privacy policy](privacy.md) for more details.

8. Does the sponsorable account have access to my sponsorship information?

   **YES.** Accounts you are currently sponsoring already have this information as 
   part of their profile. SponsorLink itself does not use any information they 
   don't already have access to, and the SponsorLink developer (Devlooped) does 
   not have access to any of this information since it doesn't offer hosting for 
   the backend that signs manifests.