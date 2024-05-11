---
title: Privacy Statement
nav_order: 4
---
<!-- #content -->
# SponsorLink Privacy Statement

This privacy statement explains the personal data SponsorLink processes, how SponsorLink processes it, 
and for what purposes.

SponsorLink offers a mechanism for integrators ("sponsorables") to check whether a user is sponsoring 
them. References to SponsorLink products in this statement include SponsorLink backend services, websites, 
apps, extensions and other related software.

This statement applies to the interactions SponsorLink has with you and the SponsorLink products listed below, 
as well as other SponsorLink products that display this statement.

## Terminology

The term "sponsor" refers to the user or organization that is providing financial support 
to another user or organization. 

The term "sponsorable" refers to the user or organization that is receiving financial 
support from another user or organization.

The term "user" refers to an individual that may or may not be a sponsor, but is using software that 
integrates with SponsorLink.

## Personal data we collect

SponsorLink does not provide any central service that collects personal data. Each *sponsorable* hosts 
its own copy of the SponsorLink backend service. The provided reference implementation of this service 
in SponsorLink is open source and does not persist any user data whatesoever. Each *sponsorable* is 
responsible for hosting and maintaining their own instance of the backend service, and for ensuring
that it complies with all applicable laws and regulations.

SponsorLink uses only local storage in the user's machine to cache the following data to improve the 
user experience:

* An access token with limited scope to read the sponsor's profile information, after explicit consent
  is given on the GitHub website to the corresponding OAuth app.
* A manifest file containing the claims documented in [the specification](spec.md).

The user can authorize each *sponsorable* individually. Declining to authorize a *sponsorable* may result 
in the user not being able to use certain features of the software that integrates with SponsorLink.

For example, if a *sponsorable* requires a sponsorship to enable a certain feature, you will not be able 
to use that feature unless you authorize the sharing of the requested data.

Where providing the data is optional (e.g. it is only used to avoid displaying a notice while using the 
product), and you choose not to share personal data with the *sponsorable*, features like personalization 
that use such data will not work for you.

### Email

If you authorized a *sponsorable* to access your email(s) for the purpose of manifest generation and signing, 
the same *sponsorable* can consider this consent as sufficient permission to check that your locally configured 
git email matches the email in your (previously cached) sponsor manifest (typically by running `git config --get user.email`).

This implicit consent does not extend to any other use of your email(s) by the *sponsorable* except for 
purely offline verification against a local manifest. 

Since generating the manifest requires explicit consent, the *sponsorable* can consider the presence of the 
manifest as sufficient indication you have given consent.

## How we use personal data

After explicitly authorizing a *sponsorable* to access your profile information, SponsorLink uses the resulting 
access token to access a *sponsorable*-provided backend service to generate a signed manifest for the *sponsor*. 
The *sponsor* access token is only used to emit the final manifest, and is not stored or used for any other purpose.

## How to access and control your personal data

You can control your personal data that SponsorLink may have obtained, and exercise your data protection rights, 
by revoking access in your [GitHub applications settings](https://github.com/settings/applications). You can 
also use the [GitHub CLI extension](https://github.com/devlooped/gh-sponsors) tool we provide to remove all 
data associated with SponsorLink, both locally and remotely.
