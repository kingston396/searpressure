using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SearPressure;

// Scenarios for the headless harness. `dotnet run -- <name> <outdir>`.
static partial class Tests
{
    // Drive and online scenarios live in DriveTests.cs and NetTests.cs.
    static partial void RunDrive(string which, ref bool handled);
    static partial void RunNet(string which, ref bool handled);

    public static void Run(string which)
    {
        switch (which)
        {
            case "smoke": Smoke(); break;
            case "kitchen": Kitchen(); break;
            case "kitchen2": Kitchen2(); break;
            case "screens": Screens(); break;
            case "economy": Economy(); break;
            case "ads": Ads(); break;
            case "store": Store(); break;
            case "fuzz": Fuzz(); break;
            case "fuzz2": Fuzz2(); break;
            case "geo": GeoProbe(); break;
            case "look": Look(); break;
            case "geoshots": GeoShots(); break;
            case "storeart": StoreArt(); break;
            case "storeshots": StoreShots(); break;
            default:
                {
                    bool handled = false;
                    RunDrive(which, ref handled);
                    RunNet(which, ref handled);
                    if (!handled) Console.WriteLine("unknown test " + which);
                    break;
                }
        }
    }

    static void Smoke()
    {
        var p = new HarnessPlatform { unlock = true };
        var g = Program.NewGame(p);
        Program.Run(g, 0.5);
        Program.Shot(g, "title.png");
        g.Scroll(900); Program.Run(g, 0.2);
        Program.Shot(g, "title_scrolled.png");
        // Play the first kitchen for a few seconds.
        g.play(0);
        Program.Run(g, 4);
        g.spawnOrder(); g.spawnOrder();
        Program.Run(g, 2);
        Program.Shot(g, "kitchen0.png");
        Console.WriteLine($"phase={g.G.phase} time={g.G.time:0.0} orders={g.G.orders.Count} chefs={g.G.chefs.Count}");
    }
}
