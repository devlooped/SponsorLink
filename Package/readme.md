Integrate [GitHub Sponsors](https://github.com/sponsors) into your libraries so that 
users can be properly linked to their sponsorship to unlock features or simply get 
the recognition they deserve for supporting your project.

SponsorLink for .NET allows you to integrate sponsorship checks in the build process 
itself, without affecting IDE/Editor performance or command line/CI builds. 
[Learn more about SponsorLink for open source developers](https://github.com/devlooped/SponsorLink#-open-source-developers) 
and the onboarding process.

> NOTE: this package is intended for *package authors*. Downstream package *consumers* 
> should get it transitively automatically to support the SponsorLink checks.

## Usage

Add the following analyzer to an analyzer project you include in your package:

```csharp
using Devlooped;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SponsorableLib;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
class SponsorLinker : SponsorLink
{
    public SponsorLinker() : base("[SPONSORABLE]", "[PROJECT]") 
    { }
}
```

Replace `SPONSORABLE` with your sponsor account login and `PROJECT` with a recognizable 
name of your project or library (i.e. `ThisAssembly` or `Moq`). Optionally, use the 
base constructor overload receiving a `SponsorLinkSettings` which offers many options 
to customize the behavior of the analyzer.

Make sure you have installed the [GitHub SponsorLink Admin](https://github.com/apps/sponsorlink-admin) app 
and have followed the [onboarding steps](https://github.com/devlooped/SponsorLink#-open-source-developers).

We recommend using [NuGetizer](https://nuget.org/packages/nugetizer) for packing your 
libraries, which provides the easiest integration with SponsorLink too. Simply add
a project reference from your main library to the analyzer project and all packaging should 
Just Work. An example analyzer project referenced by your main library is typically just:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <PackFolder>analyzers/dotnet</PackFolder>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NuGetizer" Version="0.9.1" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.0.1" Pack="false" />
    <PackageReference Include="Devlooped.SponsorLink" Version="[VERSION]" />
  </ItemGroup>

</Project>
```

To only run the SponsorLink check when a consumer directly references you package, include a 
`buildTransitive` targets file with your package that surfaces the relevant package id to the 
analyzer:

```xml
<Project>
  <ItemGroup>
    <SponsorablePackageId Include="[YOUR_PACKAGE_ID]" />
  </ItemGroup>
</Project>
```


Learn more about [SponsorLink NuGet integration](https://github.com/devlooped/SponsorLink#integrating-via-nuget-for-net).

## How it works

When you ship a new version of your library including the above analyzer, the first 
build in an IDE/editor session will result in one of these three messages:

1. User does not have the [GitHub SponsorLink](https://github.com/apps/sponsorlink) (user) 
   app installed in his personal account. [Warning SL02](https://github.com/devlooped/SponsorLink/blob/main/docs/SL02.md):

   ![Screenshot of build warning SL02 stating app is not installed](https://raw.githubusercontent.com/devlooped/SponsorLink/main/assets/img/VS-SL02.png)

2. User installed the app, but is not sponsoring the sponsorable account. 
   [Warning SL03](https://github.com/devlooped/SponsorLink/blob/main/docs/SL03.md):

   ![Screenshot of build warning SL04 stating user is not a sponsor](https://raw.githubusercontent.com/devlooped/SponsorLink/main/assets/img/VS-SL03.png)

3. User installed the app and is sponsoring:

   ![Screenshot of build info SL04 thanking the user user for sponsoring](https://raw.githubusercontent.com/devlooped/SponsorLink/main/assets/img/VS-SL04.png)


> NOTE: you can provide your own actions for the above scenarios, or simply configure 
> the default behavior by tweaking the min/max amount of random waits introduced during 
> builds. By default, the waits will range from 0 to 4000ms. Use to relevant constructor 
> overload in combination with `SponsorLinkSettings.Create()` factory method to access 
> all configurable options.

The goal of SponsorLink is to help make your project more sustainable, support your 
ongoing development and ensure your customers can depend on it in the long run!


Learn more about [SponsorLink NuGet integration](https://github.com/devlooped/SponsorLink#integrating-via-nuget-for-net), 
in particular the section on properly packing to workaround the issue of forced 
transitiveness of analyzers in the .NET SDK.


<!-- include https://github.com/devlooped/sponsors/raw/main/footer.md -->
# Sponsors 

<!-- sponsors.md -->
[![Clarius Org](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/clarius.png "Clarius Org")](https://github.com/clarius)
[![C. Augusto Proiete](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/augustoproiete.png "C. Augusto Proiete")](https://github.com/augustoproiete)
[![Kirill Osenkov](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/KirillOsenkov.png "Kirill Osenkov")](https://github.com/KirillOsenkov)
[![MFB Technologies, Inc.](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/MFB-Technologies-Inc.png "MFB Technologies, Inc.")](https://github.com/MFB-Technologies-Inc)
[![SandRock](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/sandrock.png "SandRock")](https://github.com/sandrock)
[![Andy Gocke](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/agocke.png "Andy Gocke")](https://github.com/agocke)
[![Stephen Shaw](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/decriptor.png "Stephen Shaw")](https://github.com/decriptor)
[![Torutek](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/torutek-gh.png "Torutek")](https://github.com/torutek-gh)


<!-- sponsors.md -->

[![Sponsor this project](https://raw.githubusercontent.com/devlooped/sponsors/main/sponsor.png "Sponsor this project")](https://github.com/sponsors/devlooped)
&nbsp;

[Learn more about GitHub Sponsors](https://github.com/sponsors)

<!-- https://github.com/devlooped/sponsors/raw/main/footer.md -->
