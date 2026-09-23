using System;
using System.IO;
using System.Linq;
using SearPressure;

// Store screenshots at the sizes Google Play and the App Store ask for, from real game states.
//   dotnet run --no-build -- storeshots <outdir>   → <outdir>/<set>/NN-name.png
static partial class Tests
{
    static readonly (string set, double w, double h, double dpr)[] ShotSets =
    {
        ("google-play-phone", 360, 720, 3),   // 1080 x 2160
        ("iphone-6.9", 440, 956, 3),          // 1320 x 2868
        ("iphone-6.5", 428, 926, 3),          // 1284 x 2778
        ("ipad-13", 688, 2752 / 3.0, 3),       // 2064 x 2752 (tablets scale up: short side ~560-690 px)
    };

    // Nobody naps in a screenshot.
    static void Wake(Game g) { foreach (var c in g.G.chefs) c.idle = 0; Program.Run(g, 1 / 60.0); }

    static void StoreShots()
    {
        string root = Program.Out;
        foreach (var (set, w, h, dpr) in ShotSets)
        {
            Program.Out = Path.Combine(root, set);
            Directory.CreateDirectory(Program.Out);

            // 1. Title, a few stops into the trip.
            {
                var g = Program.NewGame(new HarnessPlatform(), w, h, dpr); g.onlineEnabled = false; Program.Run(g, 0.1);
                g.save.tutorialDone = true; g.save.wallet = 1240;
                int[] st = { 3, 3, 2, 3, 2, 1 };
                for (int i = 0; i < st.Length; i++) g.save.stars[i] = st[i];
                Program.Run(g, 0.3);
                Program.Shot(g, "01-title.png");
            }
            // 2. Burgers across The Divide: patties on, a plate going, tickets up.
            {
                var k = new K(2, true, w, h, dpr);
                Program.Run(k.g, 9);
                k.ChopInto("meat", t => t.type == "stove" && t.item != null && t.item.type == "pan" && t.item.items.Count == 0);
                Program.Run(k.g, 1.2);
                k.Grab(Plate); k.Grab(k.Crate("bun"));
                Program.Run(k.g, 0.8);
                Wake(k.g); Program.Shot(k.g, "02-kitchen.png");
            }
            // 3. A pot boils over and the extinguisher comes out.
            {
                var k = new K(1, true, w, h, dpr);
                Program.Run(k.g, 6);
                for (int i = 0; i < 3; i++) k.ChopInto("onion", t => t.type == "stove" && t.item != null && t.item.type == "pot");
                var stv = k.G.tiles.First(t => t.type == "stove" && t.item != null && t.item.items.Count == 3);
                stv.item.cook = 1; stv.item.burn = 0.99;
                Program.Run(k.g, 1.6);
                k.Grab(t => t.item != null && t.item.type == "ext");
                Program.Run(k.g, 2.5);   // the fire spreads along the counter; the hint fades
                k.Go(t => t.fire > 0);
                k.g.KeyDown("KeyE", false); Program.Run(k.g, 0.12);
                Wake(k.g); Program.Shot(k.g, "03-fire.png");
                k.g.KeyUp("KeyE");
            }
            // 4. The Judge's Table: one strike already.
            {
                var k = new K(Lvl("The Judge's Table"), true, w, h, dpr);
                Program.Run(k.g, 5);
                k.ChopInto("lettuce", null);
                k.Grab(FreeCounter); k.Grab(Plate); k.Grab(t => t.item != null && t.item.kind == "lettuce");
                k.Grab(Serve, 0.6);
                Wake(k.g); Program.Shot(k.g, "04-judge.png");
            }
            // 5. A Delivery Run.
            {
                var g = Program.NewGame(DrivePlatform(true), w, h, dpr); Program.Run(g, 0.2);
                g.startDrive(0); Program.Run(g, 3.4);
                g.input.keys["KeyW"] = true; Program.Run(g, 2.5);
                g.input.keys["KeyD"] = true; g.KeyDown("Space", false); Program.Run(g, 0.6);
                g.input.keys["KeyD"] = false; g.KeyUp("Space"); Program.Run(g, 1.2);
                Program.Shot(g, "05-delivery-run.png");
            }
            // 6. Wardrobe.
            {
                var g = Program.NewGame(new HarnessPlatform { unlock = true }, w, h, dpr); g.onlineEnabled = false; Program.Run(g, 0.1);
                g.save.wallet = 860; g.save.owned["headband"] = true;
                int[] st = { 3, 3, 2, 3, 2, 1 };
                for (int i = 0; i < st.Length; i++) g.save.stars[i] = st[i];
                g.show("scr-wardrobe"); Program.Run(g, 0.3);
                Program.Shot(g, "06-wardrobe.png");
            }
            // 7. Three stars.
            {
                var g = Program.NewGame(new HarnessPlatform(), w, h, dpr); Program.Run(g, 0.2);
                g.play(0); Program.Run(g, 3.7);
                g.G.score = 236; g.G.served = 11; g.G.tips = 44; g.G.time = 0.01;
                Program.Run(g, 2.2);
                Program.Shot(g, "07-three-stars.png");
            }
            Console.WriteLine("wrote " + Program.Out);
        }
        Program.Out = root;
    }
}
