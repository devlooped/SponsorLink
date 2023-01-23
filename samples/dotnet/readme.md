# .NET Sample Package

This sample contains an absolute minimal package that can be built and published to NuGet.org, 
which contains just an analyzer assembly that consumes the SponsorLink package for nuget authors.

It can be installed to a project by running the following dotnet command from the target 
project directory:

```
> dotnet add package SponsorableLib --version 42.42.42-main.* -s https://pkg.kzu.io/index.json
```

This will run the SponsorLink check with `https://github.com/sponsors/devlooped` for your 
locally configured git email on IDE/Editor full builds.