# ![](https://github.com/devlooped/SponsorLink/raw/main/assets/img/sponsorlink-32.png) SponsorLink 

Integrate [GitHub Sponsors](https://github.com/sponsors) into your libraries so that 
users can be properly linked to their sponsorship to unlock features or simply get 
the recognition they deserve for supporting your project.

SponsorLink supports two scenarios:

1. Open source project developers or maintainers who are looking to incentivize 
   sponsors to contribute to the project, to ensure ongoing and recurring income 
   that can help ensure proper maintenance and further feature work.

2. Open source project consumers, who want to ensure their dependencies keep have 
   an active team that can provide support, bug fixes and new features.

## ![](https://avatars.githubusercontent.com/in/281005?s=24&u=20155dd9bc48951a962b40289bf40fd4d0e758e9&v=4) Open source developers

[GitHub Sponsors](https://github.com/sponsors) provides the core functionality to 
accept sponsorships from all over the world. It's a great feature available for 
everyone. 

The "missing" link is a way for your oss project to check sponsorships seamlessly 
on consumers' machines, and reminding them to support the project, without being 
too annoying or obnoxious. 

All the pieces to build this are in place already, if you invest the time and 
infrastructure to implement it: 

- [Sponsor Webhooks](https://docs.github.com/en/sponsors/integrating-with-github-sponsors/configuring-webhooks-for-events-in-your-sponsored-account)
- [GitHub Apps](https://docs.github.com/en/developers/apps/getting-started-with-apps)

This project provides the plumbing for you, so you can just focus on your oss library :).

Setting up SponsorLink for your sponsor account involves the following steps:

1. [Sponsor Devlooped](https://github.com/sponsors/devlooped): you will need to have 
   an active monthly sponsorship to use SponsorLink on an ongoing basis. You can try 
   it with a one-month subs too. There is no minimum tier (for now?), we want this to 
   be accessible to as many oss developers as possible.
2. Install the [SponsorLink Admin](https://github.com/apps/sponsorlink-admin) GitHub
   app: this will "link" your sponsorable account with your sponsorship.
3. Email sponsorlink@devlooped.com to request your shared secret to secure your webhooks 
   with SponsorLink.
4. Add a Sponsors webhook from your dashboard at `https://github.com/sponsors/[SPONSORABLE]/dashboard/webhooks` with the following values:
   * Payload URL: `https://sponsorlink.devlooped.com/sponsor/[SPONSORABLE]`
   * Content type: `application/json`
   * Secret: the secret received from us via email.
5. *Only* if you have existing sponsors: right now, [GitHub apps cannot access the sponsors API](https://github.com/orgs/community/discussions/44226), so we'll need to get them from you via email until 
   that's fixed. Email sponsorlink@devlooped.com with the response of running the following 
   GraphQL query at https://docs.github.com/en/graphql/overview/explorer:
   ```
   query { 
     organization(login: "[SPONSORABLE]") {
       id
       login
       sponsorshipsAsMaintainer(first: 100, orderBy: {field: CREATED_AT, direction: ASC}, includePrivate: true) {
         nodes {
           createdAt
           isOneTimePayment
           sponsorEntity {
             ... on Organization {
               id
               login
             }
             ... on User {
               id
               login
             }
           }
           tier {
             monthlyPriceInDollars
           }
         }
       }
     }
   }
   ```
   We will run a one-time process to link the reported sponsorships with your sponsorable account.
   Whenever GitHub adds support for querying this information, this step will no longer be necessary.

Integrating into your OSS project depends on the kind of library you provide. 
We offer initial support for .NET NuGet packages.

### Integrating via NuGet for .NET

Integration is very straightforward, especially if you use [NuGetizer](https://github.com/devlooped/nugetizer/).
Your NuGet package needs to add an analyzer/generator assembly/project, which performs the 
SponsorLink check during compilation. 

The following is an example analyzer that performs the sponsor link check:

Project:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <PackageId>SponsorableLib</PackageId>
    <PackFolder>analyzers/dotnet</PackFolder>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NuGetizer" Version="0.9.1" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.3.1" Pack="false" />
    <PackageReference Include="Devlooped.SponsorLink" Version="1.0.0" />
  </ItemGroup>
  
</Project>
```

Generator:
```csharp
using Devlooped;
using Microsoft.CodeAnalysis;

namespace SponsorableLib;

[Generator]
public class Generator : IIncrementalGenerator
{
    readonly SponsorLink link;

    public Generator() => link = new("[SPONSORABLE]", "MyLib");

    public void Initialize(IncrementalGeneratorInitializationContext context) => link.Initialize(context);
}
```

Packing and installing the resulting nuget package will result in the following user 
experience:

1. User does not have the [GitHub SponsorLink](https://github.com/apps/sponsorlink) (user) 
   app installed in his personal account.

   ![Screenshot of build warning SL03 stating app is not installed](/assets/img/vs-sl03.png)

2. User installed the app, but is not sponsoring the sponsorable account:

   ![Screenshot of build warning SL04 stating user is not a sponsor](/assets/img/vs-sl04.png)

3. User installed the app and is sponsoring:

   ![Screenshot of build info SL01 thanking the user user for sponsoring](/assets/img/vs-sl01.png)


If you are using [NuGetizer](https://github.com/devlooped/nugetizer/) for packing your main 
library, you just need to add a project reference to that project and the right packaging 
will happen (remove the PackageId property in that case, since the referencing project will 
be the one doing the packing).


## ![](https://avatars.githubusercontent.com/in/279204?s=24&u=d13eed8cef2b965c8bb34f6298b4edac31688c5a&v=4) Open source consumers

The experience for consumers intentionally targets IDE/Editor usage only. If users build 
on CI or via command-line, no sponsorship checks should be performed at all. The experience 
will vary depending on the asset being consumed.

### .NET NuGet package experience

After installation/restore, your users will consume your library just as usual. During 
command line and CI builds, as well as intellisense builds (a.k.a. design-time builds), 
SponsorLink will not perform any checks, so as to minimize its impact on those scenarios.

For regular builds performed in an editor/IDE, the analyzer will issue the warnings 
shown above for the three scenarios: GitHub app not installed, app installed but not 
sponsoring, and app installed and sponsoring.

Upon installing the [SponsorLink](https://github.com/apps/sponsorlink) GitHub 
app (*not* the Admin one), users authorize access to their email addresses, which are 
used from that point on to match their sponsorship. 

The ordering of app install and sponsorship is irrelevant, and SponsorLink will properly 
link both regardless of which happens first.

Initially, no tier-checking is performed, only that a sponsorship is active at check 
time.


## Safety and Security

SponsorLink does not receive any information about users' code, repositories, or credentials. 


<!-- include https://github.com/devlooped/sponsors/raw/main/footer.md -->
# Sponsors 

<!-- sponsors.md -->
[![Clarius Org](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/clarius.png "Clarius Org")](https://github.com/clarius)
[![Christian Findlay](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/MelbourneDeveloper.png "Christian Findlay")](https://github.com/MelbourneDeveloper)
[![C. Augusto Proiete](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/augustoproiete.png "C. Augusto Proiete")](https://github.com/augustoproiete)
[![Kirill Osenkov](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/KirillOsenkov.png "Kirill Osenkov")](https://github.com/KirillOsenkov)
[![MFB Technologies, Inc.](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/MFB-Technologies-Inc.png "MFB Technologies, Inc.")](https://github.com/MFB-Technologies-Inc)
[![SandRock](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/sandrock.png "SandRock")](https://github.com/sandrock)
[![Eric C](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/eeseewy.png "Eric C")](https://github.com/eeseewy)
[![Andy Gocke](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/agocke.png "Andy Gocke")](https://github.com/agocke)


<!-- sponsors.md -->

[![Sponsor this project](https://raw.githubusercontent.com/devlooped/sponsors/main/sponsor.png "Sponsor this project")](https://github.com/sponsors/devlooped)
&nbsp;

[Learn more about GitHub Sponsors](https://github.com/sponsors)

<!-- https://github.com/devlooped/sponsors/raw/main/footer.md -->
