---
title: Back an Issue
description: Fund the GitHub issues that matter most to you
parent: GitHub Sponsors
page_toc: false
---

<div id="spinner" class="spinner text-green-200" role="status"></div>

# Back an Issue

SponsorLink's got a new trick up its sleeve: you can now support specific GitHub issues! 
Just make a one-time donation to any GitHub Sponsors account that's turned on SponsorLink,
and come back here to back an issue. 

This way, you can push for the features you want and directly back the development that 
excites you. Pretty neat, huh?

<div id="sponsorable">
    <table class="borderless" style="border-collapse: collapse; padding: 4px; min-width: unset;" markdown="0">
        <tr>
            <td class="borderless">GitHub Sponsor account:</td>
            <td class="borderless">
                <input id="account" onblur="lookupSponsor();" class="border">
            </td>
            <td class="borderless">
                <div>
                    <button class="btn btn-green" onclick="lookupSponsor();">Go</button>
                </div>
            </td>
            <td class="borderless" style="display: none;" id="unsupported">
                ⚠️ SponsorLink is not supported
            </td>
            <td class="borderless" style="display: none;" id="supported">
                ✅ SponsorLink supported
            </td>
            <td class="borderless" style="display: none;" id="unauthorized">
                <a href="/github" id="login">
                    <button class="btn btn-green">Sign in via GitHub</button>
                </a>
            </td>
        </tr>
    </table>
</div>

<p id="error" class="no-before" />

<div id="user"></div>
<div id="issues"></div>

<details close markdown="block">
  <summary>
    <small>How this works (in-depth)</small>
  </summary>

Any GitHub account can be SponsorLink-enabled by following the instructions in the 
[sponsorable setup]({{ site.baseurl }}/github/#sponsorable-setup) guide. Once enabled,
the sponsorable will provide a `sponsorlink.jwt` file via their `[account]/.github` repository, 
which contains the [relevant information]({{ site.baseurl }}/spec/#sponsorable-manifest) 
for this page to retrieve sponsorships and previously backed issues. 

For example, you view the contents of [devlooped's manifest](https://raw.githubusercontent.com/devlooped/.github/main/sponsorlink.jwt) 
directly at [jwt.io](https://jwt.io/#debugger-io?token=eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJodHRwczovL3Nwb25zb3JsaW5rLmRldmxvb3BlZC5jb20vIiwiYXVkIjoiaHR0cHM6Ly9naXRodWIuY29tL3Nwb25zb3JzL2Rldmxvb3BlZCIsImlhdCI6IjE3MTgyMjQ4NzMiLCJjbGllbnRfaWQiOiJhODIzNTBmYjJiYWU0MDdiMzAyMSIsInN1Yl9qd2siOnsiZSI6IkFRQUIiLCJrdHkiOiJSU0EiLCJuIjoiNWluaHY4UXltYURCT2loTmkxZVktNi1oY0lCNXFTT05GWnhieHhYQXlPdHhBZGpGQ1BNLTk0Z0lacU05Q0RyWDNweWcxbFRKZm1sX2FfRlpTVTlkQjFpaTVtU1hfbU5IQkZYbjFfbF9naTFFcmRia0lGNVliVzZveFdGeGYzRzVtd1ZYd25QZnhIVHlRZG1XUTNZSlItQTNFQjRrYUZ3THFBNkhhNWxiMk9iR3BNVFFKTmFrRDRvVEFHRGhxSE1HaHU2UHVwR3E1aWU0cVpjUTdOOEFOdzh4SDduaWNUa2JxRWhRQUJIV09UbUxCV3E1ZjVGNlJZR0Y4UDdjbDBJV2xfdzRZY0laa0dtMnZYMmZpMjZGOUY2MGNVMXYxM0daRVZEVFhwSjlrenZZZU05c1lrNmZXYW95WTJqaEU1MXFidjBCMHU2aFNjWmlMUkV0bTNuN0NsSmJJR1hoa1VwcEZTMkpsTmFYM3JnUTZ0LTRMSzhnVVR5THQzekRzMkg4T1p5Q3dsQ3BmbUdtZHNVTWttMXhYNnQyci05NVUzenl3eW54b1daZmpCQ0pmNDFsZU05T01LWXdOV1o2TFFNeW84M0hXdzFQQklyWDRaTENsRndxQmNTWXNYRHlUOF9aTGQxY2RZbVBmbXRsbElYeFpoTENsd1Q1cWJDV3Y3M1YifX0.bTH1YjSuhaR0FGsGGcovzXpbT23pRdgZHiwjstlJ7FIyv_6UJ7SsR2x3K5Om8M1C7GaCQWdI_Hu92-0elLHG8yMeD-XbTtYD2z7td1DjmYV7mFNArKOKrg5pjNthrMP8U2yklC44bXcUOjWxHa5amT75Gr7L7mx5Evoe69yoE30ZhW1FgWJgQOHLvEGVyeEEXN96DzU3Ng5GqZZVNAs1zMd6RcraurmY_fr7FAMOlrCCnf6XmFxM8N0paJMWqBkgaO7h4bgzqVjGgUc1l6YKTR-zyLySOh4dcMbxLpAI6gwNImOVAtpC0cO8uL1bXQM86xrmYEDZ8HgwcRnrFtKZ3WQ7PoVs77fjiv8lChgDTEJWalWI7nGapkQDVA2-Hn1Ex_XiXW_KIuUrR7Y-Zt4f6GBiSIJmRL4s4YcgboEW61Lto0h5k_AZ5S2kL-OD4Qx2rudhsEEi2QP1oUW4aLTpmTHy5RAgLD-sGYnSZKDSaOBJ5aOUSilHEbBxy4_yeJgY) to inspect the provided information.

The included GitHub OAuth app identifier ([client_id](https://www.rfc-editor.org/rfc/rfc8693.html#name-client_id-client-identifier)), 
is used to authenticate the user and provide explicit consent on information to be shared. 
Once authenticated, this page invokes the self-hosted backend provided by the sponsorable 
to retrieve the list of one-time sponsorships available for backing new issues as well 
as the previously backed issues and their accumulated amounts. The backend URL is provided 
in the `iss` claim of the [manifest JWT]({{ site.baseurl }}/spec/#sponsorable-manifest), 
with the added path `/github/issues`.

Even though this static page is hosted by [Devlooped](https://devlooped.com) as part of 
SponsorLink documentation, it works with any SponsorLink-enabled account. The developer 
of the backend reference implementation of SponsorLink, does not have any visibility 
into the sponsorships or the sponsorable's GitHub account since it's all 
[self-hosted]({{ site.baseurl }}/github/#sponsorable-backend-self-hosting) by each 
project owner.

</details>
 
<script src="{{ '/assets/js/issues.js' | relative_url }}"></script> 