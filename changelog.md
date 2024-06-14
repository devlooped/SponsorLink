# Changelog

## [v1.2.0-beta](https://github.com/devlooped/SponsorLink/tree/v1.2.0-beta) (2024-06-14)

[Full Changelog](https://github.com/devlooped/SponsorLink/compare/v1.1.0...v1.2.0-beta)

:sparkles: Implemented enhancements:

- Improve self-hosting story for signed manifests [\#146](https://github.com/devlooped/SponsorLink/issues/146)
- Consider contributed sponsorable repos as sponsored [\#66](https://github.com/devlooped/SponsorLink/issues/66)
- Create remove endpoint to improve compliance with GDPR [\#61](https://github.com/devlooped/SponsorLink/issues/61)
- Suggestion: when including SponsorLink in a project, call the integration project something other than `<ProjectName>.CodeAnalyzers` [\#60](https://github.com/devlooped/SponsorLink/issues/60)
- Idea for achieving the goel of sponsorlink without email and/or connection [\#54](https://github.com/devlooped/SponsorLink/issues/54)
- Telemetry enhancement: Add "kill switch" for strict corporate environments [\#50](https://github.com/devlooped/SponsorLink/issues/50)
- RFC: A Proposal to Fix Major Privacy Risk [\#48](https://github.com/devlooped/SponsorLink/issues/48)
- Support org-wide sponsorships [\#47](https://github.com/devlooped/SponsorLink/issues/47)
- Add license acceptance and some explanation on telemetry? [\#34](https://github.com/devlooped/SponsorLink/issues/34)
- Build pauses should be gone [\#33](https://github.com/devlooped/SponsorLink/issues/33)
- Warnings for sponsoring messages break build in some cases [\#32](https://github.com/devlooped/SponsorLink/issues/32)
- Replace hashed email with manifest-based offline check [\#31](https://github.com/devlooped/SponsorLink/issues/31)
- Remove remaining usage of "pub", render roles on sync [\#231](https://github.com/devlooped/SponsorLink/pull/231) (@kzu)
- Allow accepting the ToS programmatically [\#230](https://github.com/devlooped/SponsorLink/pull/230) (@kzu)
- Don't require GH CLI for specific account\(s\) sync [\#227](https://github.com/devlooped/SponsorLink/pull/227) (@kzu)
- Delete credentials when removing manifests, add global clear [\#223](https://github.com/devlooped/SponsorLink/pull/223) (@kzu)
- Add remove command to CLI [\#222](https://github.com/devlooped/SponsorLink/pull/222) (@kzu)
- Remove base64-encoded public key from all APIs [\#221](https://github.com/devlooped/SponsorLink/pull/221) (@kzu)
- Switch to known error codes constants in sync command [\#220](https://github.com/devlooped/SponsorLink/pull/220) (@kzu)
- Unify output of gh cli view vs /me endpoint [\#219](https://github.com/devlooped/SponsorLink/pull/219) (@kzu)
- Minor improvements, introduction of /jwk endpoint [\#218](https://github.com/devlooped/SponsorLink/pull/218) (@kzu)
- Unify /me as the endpoint for sponsor manifests [\#217](https://github.com/devlooped/SponsorLink/pull/217) (@kzu)
- Show cached manifests details with view command [\#216](https://github.com/devlooped/SponsorLink/pull/216) (@kzu)
- Initial work for standalone manifest per sponsorable account [\#176](https://github.com/devlooped/SponsorLink/pull/176) (@kzu)
- Add sponsorable init backend function [\#147](https://github.com/devlooped/SponsorLink/pull/147) (@kzu)
- Add a development-only and source-only helper package for NuGet integration [\#72](https://github.com/devlooped/SponsorLink/pull/72) (@kzu)
- Delete entire analyzer/package project which is no longer needed [\#65](https://github.com/devlooped/SponsorLink/pull/65) (@kzu)
- Create remove endpoint to improve compliance with GDPR [\#62](https://github.com/devlooped/SponsorLink/pull/62) (@kzu)
- Add signing function and update deployment dir [\#59](https://github.com/devlooped/SponsorLink/pull/59) (@kzu)
- Remove all code related to build pauses [\#56](https://github.com/devlooped/SponsorLink/pull/56) (@kzu)

:bug: Fixed bugs:

- Enhancement/Fix : make it Opt-In [\#52](https://github.com/devlooped/SponsorLink/issues/52)
- SponsorLink must NEVER be obfuscated [\#36](https://github.com/devlooped/SponsorLink/issues/36)
- SponsorLink is Source Available, not Open Source [\#35](https://github.com/devlooped/SponsorLink/issues/35)

:hammer: Other:

- Document self-hosting of manifest signing backend and CLI [\#141](https://github.com/devlooped/SponsorLink/issues/141)
- Make the SPONSORLINK\_MANIFEST envvar more of a suggestion/example [\#140](https://github.com/devlooped/SponsorLink/issues/140)
- If using Warnings for SL messages, recommend providing WarningsNotAsErrors  [\#76](https://github.com/devlooped/SponsorLink/issues/76)

:twisted_rightwards_arrows: Merged:

- Sample analyzer and packaging cleanup [\#214](https://github.com/devlooped/SponsorLink/pull/214) (@kzu)
- Add much needed privacy policy/statement [\#73](https://github.com/devlooped/SponsorLink/pull/73) (@kzu)
- Remove ALL existing code related to stored sponsors [\#57](https://github.com/devlooped/SponsorLink/pull/57) (@kzu)
- Replace log analytics workspace which is GONE [\#49](https://github.com/devlooped/SponsorLink/pull/49) (@kzu)
- Update dependencies with those without the old SL [\#42](https://github.com/devlooped/SponsorLink/pull/42) (@kzu)

## [v1.1.0](https://github.com/devlooped/SponsorLink/tree/v1.1.0) (2023-08-10)

[Full Changelog](https://github.com/devlooped/SponsorLink/compare/78ce0ad6fd4eee935c4dc5dfc7ab792e1e30a2d2...v1.1.0)

:bug: Fixed bugs:

- Why closed source [\#19](https://github.com/devlooped/SponsorLink/issues/19)
- Package harvests email addresses and private data to send to remote infrastructure [\#18](https://github.com/devlooped/SponsorLink/issues/18)
- Harvesting user email addresses without any form of consent is against GDPR regulation [\#17](https://github.com/devlooped/SponsorLink/issues/17)
- Will cause packages to be no-go in corporate  [\#16](https://github.com/devlooped/SponsorLink/issues/16)

:hammer: Other:

- Where's the source code? [\#13](https://github.com/devlooped/SponsorLink/issues/13)



\* *This Changelog was automatically generated by [github_changelog_generator](https://github.com/github-changelog-generator/github-changelog-generator)*
