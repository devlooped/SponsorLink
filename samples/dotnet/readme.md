# .NET Sample

This sample contains an absolute minimal package that can be built and published to NuGet.org, 
which contains just an analyzer assembly that consumes the SponsorLink package for nuget authors.

It can be installed to a project by running the following dotnet command from the target 
project directory:

```
dotnet add package SponsorableLib --version 42.42.42-main.* -s https://pkg.kzu.io/index.json
```

This will run the SponsorLink check with `https://github.com/sponsors/devlooped` for your 
locally configured git email on IDE/Editor full builds. 

The sample contains two analyzers, one with simple SponsorLink settings and an advanced one, 
so you will get both running the sample check.

The Analyzer folder contains the analyzer project, and the Tests project is set up to consume 
it and allow for easy debugging by just running the Analyzer as the startup project from 
Visual Studio (for example).

> NOTE: after initial restore, it might be necessary to restart the IDE for the analyzer 
> assemblies to be properly resolved and loaded for debugging.

