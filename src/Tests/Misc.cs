using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spectre.Console;

namespace Devlooped.Sponsors;

public class Misc
{
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
