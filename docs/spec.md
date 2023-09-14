---
title: Manifest Spec 
nav_order: 4
has_children: true
has_toc: false
---

{%- assign versions = site[page.collection]
  | default: site.html_pages
  | where: "parent", page.title -%}

[Current](spec/1.0.0-beta.html){: .btn .btn-blue }
{% for spec in versions -%}
[{{ spec.title }}]({{ spec.url | relative_url }}){: .btn }
{% endfor -%}

<!-- include spec/1.0.0-beta.md#content -->
<!-- #content -->
# SponsorLink Manifest Version 1.0

## Overview

The SponsorLink manifest is a JWT (JSON Web Token) that encapsulates a set of hashes, each representing a sponsorship from a user or organization. This manifest can be utilized to enable features exclusive to sponsors or to suppress requests for sponsorships.

## Purpose

Establishing a standard method to represent and verify sponsorships in a secure, offline, and privacy-conscious manner would be advantageous for both open-source software (OSS) authors and users.

## Manifest Structure

A SponsorLink manifest is a JWT, which can be optionally signed, containing the following claims:

| Claim | Description |
| ----- | ----------- |
| `aud` | The intended audience for the token, specifically `SponsorLink` |
| `hash` | A collection of hashes, each representing a sponsorship |
| `exp` | The token's expiration date |
| `iss` | The issuer of the token |
| `sub` | The identifier for the user (e.g., GitHub user id) |

Example:

```json
{
  "aud": "SponsorLink",
  "hash": [
    "BDijrDmFcAe21P8otS3q0qtY2n/XPd3UL3loZlaGlF0=",
    "dk3xig+I5FgtQwfPaS3BGhtgru9DkKd5Bm8lKwVBboY="
  ],
  "exp": 1696118400,
  "iss": "[SPECIFIC_ISSUER]",
  "sub": "[USER_ID]"
}
```

### Hash Generation

Upon initial use of any SponsorLink tool developed to generate or synchronize a manifest, a random string (typically a GUID) is produced and stored in an environment variable named `SPONSORLINK_INSTALLATION`. This string is utilized as a salt in all hashes generated for that particular installation.

The process to generate a manifest includes: 

1. Identifying the user (e.g., GitHub user id, Open Collective account, etc.) and the account(s) they sponsor.
2. Creating a unique hash for each sponsored account using the salt, user identifier, and sponsored account. The hashing function is defined as: `base64(sha256([SALT]+[USER]+[SPONSORED]))`.

### Token Signing

Implementations may choose to sign the JWT using a private key through a backend service. Although this is optional, it is recommended to prevent unauthorized alterations to the manifest.

The manifest can be entirely generated via a backend service with sponsorship information access, or it can be locally generated and then sent to a backend service for signing.

Typically, the backend service would use a private key to sign the JWT using RS256. The corresponding public key is then distributed to the manifest consuming libraries and tools to verify the signature.

### Subject Claim

The `sub` claim can be used by the backend service to identify the user to whom the manifest is applicable. The issuer has the freedom to choose the format of the `sub` claim and its validation or authentication mechanism. 

For unsigned manifests, the `sub` claim is typically not utilized.

## Manifest Usage

The manifest is stored in a user environment variable named `SPONSORLINK_MANIFEST`. This variable, in conjunction with `SPONSORLINK_INSTALLATION`, can be used by a tool or library author to verify sponsorships in a secure, offline, and local manner.

If both variables are detected, the manifest can be interpreted using any standard JWT library. The signature can be optionally verified against a public key provided by the *issuer*.

The author may also choose to verify the token's expiration date, decide on a grace period, and issue a notification if the manifest is expired.

Lastly, the author can identify the user (using the same method used for manifest generation), create the hash for their sponsorable account (e.g., GitHub user/org, Open Collective account, etc.), and verify if it is included in the manifest.

## Privacy Considerations

* The method of user identification for manifest generation is entirely determined by the issuer.
* The method of user identification for manifest consumption is left to the discretion of the tool or library author.

Once a manifest is generated (and optionally signed) and stored in the `SPONSORLINK_MANIFEST` environment variable, it can be utilized by any tool or library to locally verify sponsorships without any further need to access the user's information, beyond their identifier. One commonly used identifier is the user's email address, which is usually accessible in the `git` configuration. The domain of the email can also be used to verify sponsorships at the organization level.
<!-- spec/1.0.0-beta.md#content -->
