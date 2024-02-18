---
title: FAQ
nav_order: 2
page_toc: false
---
# Reference Implementation FAQ
<!-- #content -->
These are some frequently asked questions about the reference implementation 
of the [standard manifest format](spec.md) available in the 
[SponsorLink](https://github.com/devlooped/SponsorLink/) and 
[`gh-sponsors`](https://github.com/devlooped/gh-sponsors/) repositories.

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