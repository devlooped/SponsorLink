# Changelog

## [v2.0.8](https://github.com/devlooped/SponsorLink/tree/v2.0.8) (2024-10-01)

[Full Changelog](https://github.com/devlooped/SponsorLink/compare/v2.0.7...v2.0.8)

:sparkles: Implemented enhancements:

- Add tier as sponsor claim [\#234](https://github.com/devlooped/SponsorLink/issues/234)
- When auth fails during nuget stats, render failure [\#363](https://github.com/devlooped/SponsorLink/pull/363) (@kzu)
- Use stable hash comparer for nuget/oss model [\#361](https://github.com/devlooped/SponsorLink/pull/361) (@kzu)
- Add arbitrary yaml metadata from tier as sponsor claims [\#359](https://github.com/devlooped/SponsorLink/pull/359) (@kzu)
- Allow viewing the manifest of a specific account [\#358](https://github.com/devlooped/SponsorLink/pull/358) (@kzu)

:bug: Fixed bugs:

- Ensure org sponsorships are collected for OSS authors too [\#360](https://github.com/devlooped/SponsorLink/pull/360) (@kzu)

:twisted_rightwards_arrows: Merged:

- Make sure we never inadvertently push launchSettings.json [\#362](https://github.com/devlooped/SponsorLink/pull/362) (@kzu)

## [v2.0.7](https://github.com/devlooped/SponsorLink/tree/v2.0.7) (2024-10-01)

[Full Changelog](https://github.com/devlooped/SponsorLink/compare/v2.0.6...v2.0.7)

:sparkles: Implemented enhancements:

- Allow easily copying markdown of OSS author badges [\#356](https://github.com/devlooped/SponsorLink/pull/356) (@kzu)
- Add specific diagnostic/status for oss authors [\#355](https://github.com/devlooped/SponsorLink/pull/355) (@kzu)

## [v2.0.6](https://github.com/devlooped/SponsorLink/tree/v2.0.6) (2024-09-28)

[Full Changelog](https://github.com/devlooped/SponsorLink/compare/v2.0.5...v2.0.6)

:sparkles: Implemented enhancements:

- Use new human readable metric summary values [\#351](https://github.com/devlooped/SponsorLink/pull/351) (@kzu)

## [v2.0.5](https://github.com/devlooped/SponsorLink/tree/v2.0.5) (2024-09-28)

[Full Changelog](https://github.com/devlooped/SponsorLink/compare/v2.0.4...v2.0.5)

:sparkles: Implemented enhancements:

- Add user-friendly render of nuget stats totals [\#350](https://github.com/devlooped/SponsorLink/pull/350) (@kzu)

## [v2.0.4](https://github.com/devlooped/SponsorLink/tree/v2.0.4) (2024-09-28)

[Full Changelog](https://github.com/devlooped/SponsorLink/compare/v2.0.3...v2.0.4)

:sparkles: Implemented enhancements:

- Render globals totals after running nuget command [\#349](https://github.com/devlooped/SponsorLink/pull/349) (@kzu)
- Add exception propagation behavior with --exceptions [\#348](https://github.com/devlooped/SponsorLink/pull/348) (@kzu)

## [v2.0.3](https://github.com/devlooped/SponsorLink/tree/v2.0.3) (2024-09-28)

[Full Changelog](https://github.com/devlooped/SponsorLink/compare/v2.0.2...v2.0.3)

:sparkles: Implemented enhancements:

- Add totals summary for nuget stats [\#347](https://github.com/devlooped/SponsorLink/pull/347) (@kzu)
- Allow fetching specific nuget owner stats [\#346](https://github.com/devlooped/SponsorLink/pull/346) (@kzu)
- Allow retrieving a shields.io badge with nuget stats [\#345](https://github.com/devlooped/SponsorLink/pull/345) (@kzu)

## [v2.0.2](https://github.com/devlooped/SponsorLink/tree/v2.0.2) (2024-09-27)

[Full Changelog](https://github.com/devlooped/SponsorLink/compare/v2.0.1...v2.0.2)

:sparkles: Implemented enhancements:

- If starting browser fails, render the URI to navigate to [\#344](https://github.com/devlooped/SponsorLink/pull/344) (@kzu)
- Ensure output is UTF-8 [\#343](https://github.com/devlooped/SponsorLink/pull/343) (@kzu)
- Add support for considering oss authors/contribs as indirect sponsors [\#339](https://github.com/devlooped/SponsorLink/pull/339) (@kzu)

## [v2.0.1](https://github.com/devlooped/SponsorLink/tree/v2.0.1) (2024-09-25)

[Full Changelog](https://github.com/devlooped/SponsorLink/compare/v2.0.0...v2.0.1)

:sparkles: Implemented enhancements:

- Add command to fetch and discover nuget oss stats [\#338](https://github.com/devlooped/SponsorLink/pull/338) (@kzu)
- Add query to retrieve contributors for a given repository [\#334](https://github.com/devlooped/SponsorLink/pull/334) (@kzu)
- Enable UTF-8 output from GitHub CLI queries [\#333](https://github.com/devlooped/SponsorLink/pull/333) (@kzu)
- Add overload to simplify getting or setting status [\#330](https://github.com/devlooped/SponsorLink/pull/330) (@kzu)
- Add Grace period info reporting [\#327](https://github.com/devlooped/SponsorLink/pull/327) (@kzu)
- Make the funding help url configurable via MSBuild [\#326](https://github.com/devlooped/SponsorLink/pull/326) (@kzu)
- Add an easy way to check for design-time builds [\#319](https://github.com/devlooped/SponsorLink/pull/319) (@kzu)
- Add Back an Issue feature [\#310](https://github.com/devlooped/SponsorLink/pull/310) (@kzu)
- Add GitHub webhook for auto-labeling of sponsor issues [\#299](https://github.com/devlooped/SponsorLink/pull/299) (@kzu)
- Add schema version to issued manifests [\#296](https://github.com/devlooped/SponsorLink/pull/296) (@kzu)
- Add distinct diagnostic for contributors [\#294](https://github.com/devlooped/SponsorLink/pull/294) (@kzu)

:twisted_rightwards_arrows: Merged:

- Minor analyzer sample fixes [\#331](https://github.com/devlooped/SponsorLink/pull/331) (@kzu)
- Add optional message to use as summary in sponsored APIs [\#328](https://github.com/devlooped/SponsorLink/pull/328) (@kzu)
- Fix intellisense for SL resources [\#325](https://github.com/devlooped/SponsorLink/pull/325) (@kzu)
- Fix dead links to the GitHub spec. [\#321](https://github.com/devlooped/SponsorLink/pull/321) (@teo-tsirpanis)
- When multiple sponsorables, ensure @ each [\#318](https://github.com/devlooped/SponsorLink/pull/318) (@kzu)

## [v2.0.0](https://github.com/devlooped/SponsorLink/tree/v2.0.0) (2024-07-24)

[Full Changelog](https://github.com/devlooped/SponsorLink/compare/v2.0.0-rc.6...v2.0.0)

:sparkles: Implemented enhancements:

- Allow disabling Info diagnostics globally [\#285](https://github.com/devlooped/SponsorLink/pull/285) (@kzu)
- Add support for detecting indirect metapackage reference [\#283](https://github.com/devlooped/SponsorLink/pull/283) (@kzu)
- Simplify JWK download by using curl directly from backend [\#281](https://github.com/devlooped/SponsorLink/pull/281) (@kzu)

## [v2.0.0-rc.6](https://github.com/devlooped/SponsorLink/tree/v2.0.0-rc.6) (2024-07-21)

[Full Changelog](https://github.com/devlooped/SponsorLink/compare/v2.0.0-rc.5...v2.0.0-rc.6)

:twisted_rightwards_arrows: Merged:

- Ensure we retry failing tests for samples too [\#275](https://github.com/devlooped/SponsorLink/pull/275) (@kzu)
- Bring analyzer sample from devlooped/oss into this repo [\#268](https://github.com/devlooped/SponsorLink/pull/268) (@kzu)
- Install by directly adding source w/o nuget.config [\#266](https://github.com/devlooped/SponsorLink/pull/266) (@kzu)

## [v2.0.0-rc.5](https://github.com/devlooped/SponsorLink/tree/v2.0.0-rc.5) (2024-06-27)

[Full Changelog](https://github.com/devlooped/SponsorLink/compare/v2.0.0-rc.4...v2.0.0-rc.5)

## [v2.0.0-rc.4](https://github.com/devlooped/SponsorLink/tree/v2.0.0-rc.4) (2024-06-27)

[Full Changelog](https://github.com/devlooped/SponsorLink/compare/v2.0.0-rc.3...v2.0.0-rc.4)

:sparkles: Implemented enhancements:

- Add optional endpoint that can emit shields.io endpoint badge data [\#258](https://github.com/devlooped/SponsorLink/pull/258) (@kzu)
- Add --with-token option to list command [\#257](https://github.com/devlooped/SponsorLink/pull/257) (@kzu)
- Switch to using ClaimsIdentity/Subject in token [\#253](https://github.com/devlooped/SponsorLink/pull/253) (@kzu)
- Upgrade to newer IdentityModel [\#251](https://github.com/devlooped/SponsorLink/pull/251) (@kzu)

:bug: Fixed bugs:

- Fix issued-at being a string rather than a date [\#252](https://github.com/devlooped/SponsorLink/pull/252) (@kzu)

:twisted_rightwards_arrows: Merged:

- Use a fixed session id from CI to avoid contamination [\#256](https://github.com/devlooped/SponsorLink/pull/256) (@kzu)
- Switch to the devlooped \(bot\) token [\#249](https://github.com/devlooped/SponsorLink/pull/249) (@kzu)

## [v2.0.0-rc.3](https://github.com/devlooped/SponsorLink/tree/v2.0.0-rc.3) (2024-06-25)

[Full Changelog](https://github.com/devlooped/SponsorLink/compare/v2.0.0-rc.2...v2.0.0-rc.3)

:sparkles: Implemented enhancements:

- Add basic anonymous usage telemetry [\#246](https://github.com/devlooped/SponsorLink/pull/246) (@kzu)
- Notify users when there is a new version of the CLI [\#242](https://github.com/devlooped/SponsorLink/pull/242) (@kzu)

## [v2.0.0-rc.2](https://github.com/devlooped/SponsorLink/tree/v2.0.0-rc.2) (2024-06-17)

[Full Changelog](https://github.com/devlooped/SponsorLink/compare/v2.0.0-rc.1...v2.0.0-rc.2)

:sparkles: Implemented enhancements:

- Add --force option to sync all accounts [\#239](https://github.com/devlooped/SponsorLink/pull/239) (@kzu)

:bug: Fixed bugs:

- Never fail on MacOS/Linux [\#38](https://github.com/devlooped/SponsorLink/issues/38)

:twisted_rightwards_arrows: Merged:

- Run acceptance tests cross-platform on manifest sync [\#238](https://github.com/devlooped/SponsorLink/pull/238) (@kzu)

## [v2.0.0-rc.1](https://github.com/devlooped/SponsorLink/tree/v2.0.0-rc.1) (2024-06-15)

[Full Changelog](https://github.com/devlooped/SponsorLink/compare/v1.1.0...v2.0.0-rc.1)

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
