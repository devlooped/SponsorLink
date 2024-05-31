using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace Devlooped.Sponsors;

public class Misc
{
    public record TypedConfig
    {
        public required string Bar { get; init; }
        public required bool Auto { get; init; }
    }

    [Fact]
    public void WriteIni()
    {
        if (File.Exists(".netconfig"))
            File.Delete(".netconfig");

        var config = new ConfigurationBuilder()
            .AddDotNetConfig(".netconfig")
            .Build();

        // Can read and write just using config extensions
        config["foo:bar"] = "baz";
        config["foo:auto"] = "true";

        var saved = new ConfigurationBuilder().AddDotNetConfig(".netconfig").Build();

        Assert.Equal("baz", saved["foo:bar"]);

        var services = new ServiceCollection()
            .Configure<TypedConfig>(saved.GetSection("foo"))
            .BuildServiceProvider();

        var typed = services.GetRequiredService<IOptions<TypedConfig>>().Value;

        Assert.NotNull(typed);
        Assert.Equal("baz", typed.Bar);
        Assert.True(typed.Auto);
    }

    public static void SponsorsAscii()
    {
        var heart =
            """        
              xxxxxxxxxxx      xxxxxxxxxx
             xxxxxxxxxxxxxx   xxxxxxxxxxxxx
            xxxxxxxxxxxxxxxx xxxxxxxxxxxxxx
            xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
             xxxxxxxxxxxxxxxxxxxxxxxxxxxxx
              xxxxxxxxxxxxxxxxxxxxxxxxxxx
                xxxxxxxxxxxxxxxxxxxxxx
                   xxxxxxxxxxxxxxxxx
                    xxxxxxxxxxxx
                      xxxxxxxxx
                        xxxxx
                         xxx
                         xx
                         x
            """.Split(Environment.NewLine);

        var canvas = new Canvas(32, 32);
        for (var y = 0; y < heart.Length; y++)
        {
            for (var x = 0; x < heart[y].Length; x++)
            {
                if (heart[y][x] == 'x')
                {
                    canvas.SetPixel(x, y, Color.Purple);
                }
            }
        }

        AnsiConsole.Write(canvas);
    }
}
