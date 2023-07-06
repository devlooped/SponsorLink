# v0.11.0

# v0.10.5
## What's Changed
### ✨ Implemented enhancements
* Consider no session id as no editor too and skip checks
# v0.10.4
<!-- Release notes generated using configuration in .github/release.yml at main -->

## What's Changed
### ✨ Implemented enhancements
* If no session ID can be determined, don't optimize checks: the session is available in Roslyn/VS and Rider ATM


**Full Changelog**: https://github.com/devlooped/SponsorLinkCore/compare/v0.10.3...v0.10.4
# v0.10.3
## What's Changed
### ✨ Implemented enhancements
* Make sure buildTransitive assets from SL are depended upon 

# v0.10.2
## What's Changed
### ✨ Implemented enhancements
* Make it more clear when an issue SponsorLink's and not the package using it 
* Make SponsorLink check incremental 
* Improve broken configuration reporting and telemetry 
* Improve packing and diagnostics 
* Add setting and behavior to skip checks if dependency isn't direct 
* Make sure transitive targets are always properly packed 
* Suggest installing the Devlooped.SponsorLink package as a fix for build issues
* Only run SL check once per editor session (per consuming project) 
* Enable diagnostics logging to %TEMP%\SponsorLink\log.txt by setting SPONSORLINK_TRACE environment variable to a non-empty value.

# v0.10.1
## What's Changed
### ✨ Implemented enhancements
* Enable diagnostics logging to `%TEMP%\SponsorLink\log.txt` by setting `SPONSORLINK_TRACE` environment variable to a non-empty value.
# v0.10.0
## What's Changed
### ✨ Implemented enhancements
* Make it more clear when an issue SponsorLink's and not the package using it 
* Make SponsorLink check incremental 
* Improve broken configuration reporting and telemetry 
* Improve packing and diagnostics 
* Add setting and behavior to skip checks if dependency isn't direct 
* Make sure transitive targets are always properly packed 
* Suggest installing the Devlooped.SponsorLink package as a fix for build issues
* Only run SL check once per editor session (per consuming project) 

# v0.9.9
## What's Changed
### ✨ Implemented enhancements
* Set default pack include/exclude when using NuGetizer for packing by @kzu
# v0.9.8
## What's Changed
### ✨ Implemented enhancements
* Allow checking sponsor status directly via API for non-analyzer scenarios by @kzu 
# v0.9.7
## What's Changed
### ✨ Implemented enhancements
* Report SponsorLink version running the check too by @kzu: this allows detecting older versions of SponsorLink 
   in use in the wild, which might help identify packages that might need updating by contacting the owners.
# v0.9.6
## What's Changed
### ✨ Implemented enhancements
* Protect user personal data by hashing their email

# v0.9.5
## What's Changed
### ✨ Implemented enhancements
* Further improve proxy support for non-Win/VS scenarios
* Automate publishing of release notes

### 🐛 Fixed bugs
* Halve network error timeout by exiting early on first failure when checking for installed GH app
# v0.9.4
## What's Changed
### ✨ Implemented enhancements
* After quiet days passed, increase random pause by 1sec/day until max configured pause
* Add 250ms network timeout to speed up HTTP request failures
* Add proxy support for Windows/.NET Framework

### 🐛 Fixed bugs
* Fix detection of install time for package to start quiet days countdown

# v0.9.0
## What's Changed
### ✨ Implemented enhancements
* Make quiet days configurable by the sponsorable
* Introduce 12hrs CDN-based caching to improve performance 

# v0.8.0
## What's Changed
### ✨ Implemented enhancements
* Track localized resources from upstream for easier translation
* Further optimize diagnostic reporting for multiple reports for same sponsorable/project
* Don't issue warnings during first two weeks after install (quiet days)
* Improve devlooped thanks page for SponsorApp/Admin installations
* Support reporting package id/version for a sponsorable project

# v0.7.0
## What's Changed
### ✨ Implemented enhancements
* Move strings to resources to enable localization
* Add spanish localization in SponsorLink repo 
* Set up localization sync in upstream package

# v0.6.0
## What's Changed
### ✨ Implemented enhancements
* Allow replacing reported diagnostic descriptions

# v0.5.0
## What's Changed
### ✨ Implemented enhancements
* Honor compiler-based supressions: allows skipping Info diagnostic with `[assembly: SuppressMessage("SponsorLink", "SL04")]` for example.
* Deduplicate diagnostics that are for the same sponsorable and project (i.e. using multiple ThisAssembly.* packages)

# v0.3.0
## What's Changed
### ✨ Implemented enhancements
* Improve detection of Rider
* Improve detection of disabled SponsorLink warnings and surface via MSBuild
* Downgrade Roslyn dependency to enable running on VS2022 RTM and newer
* Fix rendering of diagnostics links by moving the actual diagnostic reporting to an analyzer
### 🐛 Fixed bugs
* Info (thanks) diagnostics aren't properly reported from source generators. 

# v0.2.0
## What's Changed
### ✨ Implemented enhancements
* Enable configurable diagnostic reporting action
