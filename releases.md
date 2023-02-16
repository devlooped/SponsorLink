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
