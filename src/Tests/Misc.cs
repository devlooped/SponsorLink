﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace Devlooped.Sponsors;

public class Misc
{
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

        Assert.Equal("baz", new ConfigurationBuilder().AddDotNetConfig(".netconfig").Build()["foo:bar"]);
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
