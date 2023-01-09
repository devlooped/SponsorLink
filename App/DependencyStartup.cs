using System;
using App;
using Devlooped;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(DependencyStartup))]

public class DependencyStartup : FunctionsStartup
{
    public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
    {
        builder.ConfigurationBuilder.AddUserSecrets(ThisAssembly.Project.UserSecretsId);
    }

    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder.Services.AddHttpClient();

        var storage = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") == "Development"
            ? CloudStorageAccount.DevelopmentStorageAccount
            : CloudStorageAccount.Parse(
                Environment.GetEnvironmentVariable("AppStorage") ??
                builder.GetContext().Configuration["AppStorage"] ??
                throw new InvalidOperationException("Missing AppStorage configuration."));

        builder.Services.AddSingleton(storage);

        builder.Services.AddSingleton(sp => TablePartition.Create<User>(
            sp.GetRequiredService<CloudStorageAccount>(), "Sponsor", "User", x => x.Login));

        builder.Services.AddSingleton(sp => TablePartition.Create<UserEmail>(
            sp.GetRequiredService<CloudStorageAccount>(), "Sponsor", "Email", x => x.Email));
    }
}