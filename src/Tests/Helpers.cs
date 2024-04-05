using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Devlooped;

class Helpers
{
    public static IConfiguration Configuration { get; } = new ConfigurationBuilder()
        .AddUserSecrets<Helpers>()
        .AddEnvironmentVariables()
        .Build();

    public static IServiceProvider Services { get; } = new ServiceCollection()
        .AddHttpClient()
        .AddSingleton<IConfiguration>(Configuration)
        .AddSingleton(_ => AsyncLazy.Create(async () =>
        {
            var playwright = await Playwright.CreateAsync();
            var options = new BrowserTypeLaunchOptions
            {
                Headless = !Debugger.IsAttached,
            };

            if (OperatingSystem.IsWindows())
                options.Channel = "msedge";
            else
                options.ExecutablePath = Chromium.Path;

            return await playwright.Chromium.LaunchAsync(options);
        }))
        .AddAsyncLazy()
        .BuildServiceProvider();

}
