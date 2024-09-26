---
title: OSS Authors
description: See your impact as a GitHub OSS author and contributor
parent: GitHub Sponsors
page_toc: false
---

<div id="spinner" class="spinner text-green-200" role="status"></div>

# OSS Authors

[Devlooped](https://devlooped.com) is a proud supporter of open-source software and its 
authors. Therefore, we consider implicit sponsors any and all contributors to active 
nuget packages that are open-source and are hosted on GitHub. 

> An active nuget package has at least 200 downloads per day in aggregate across the last 5 versions.

This page allows you to check your eligibility for indirect sponsorship as an OSS author/contributor.

<div id="github">
    <table class="borderless" style="border-collapse: collapse; padding: 4px; min-width: unset;" markdown="0">
        <tr>
            <td class="borderless">GitHub account:</td>
            <td class="borderless">
                <input id="account" onblur="lookupAccount();" class="border">
            </td>
            <td class="borderless">
                <div>
                    <button class="btn btn-green" onclick="lookupAccount();">Go</button>
                </div>
            </td>
            <td class="borderless" style="display: none;" id="unsupported">
                ⚠️ Account is not eligible as OSS author
            </td>
            <td class="borderless" style="display: none;" id="supported">
                ✅ Account is eligible as OSS author
            </td>
        </tr>
    </table>
</div>

<p id="error" class="no-before" />

<div id="data"></div>

<script src="{{ '/assets/js/oss.js' | relative_url }}"></script>
