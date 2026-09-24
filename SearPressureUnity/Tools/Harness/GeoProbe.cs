using System;
using System.Linq;
using SearPressure;
static partial class Tests
{
    static void GeoProbe()
    {
        var sizes = new (string n, double w, double h, double d)[] { ("iPhone 15", 393, 852, 3), ("Pixel 8", 412, 915, 2.625), ("Galaxy A15", 360, 780, 3), ("small 360x640", 360, 640, 2), ("landscape Pixel", 915, 412, 2.625) };
        foreach (var (n, w, h, d) in sizes)
        {
            double sumTs = 0, sumFrac = 0; int cnt = 0;
            foreach (int lv in new[] { 0, 1, 2, 4, 7, 10, 13, 19, 22, 25, 30 })
            {
                var g = Program.NewGame(new HarnessPlatform { unlock = true }, w, h, d); Program.Run(g, 0.1);
                g.play(lv); Program.Run(g, 0.2);
                var (ts, ox, oy) = g.kitchenGeom();
                sumTs += ts; sumFrac += g.G.cols * ts * g.G.rows * ts / (w * h); cnt++;
            }
            Console.WriteLine($"{n,-16} avg tile {sumTs / cnt:0.0}px  kitchen floor {100 * sumFrac / cnt:0.0}% of screen");
        }
    }
}
static partial class Tests
{
    static void GeoShots()
    {
        foreach (var (n, w, h, d) in new[] { ("pixel8", 412.0, 915.0, 2.625), ("small", 360.0, 640.0, 2.0), ("land", 915.0, 412.0, 2.625) })
        {
            var k = new K(2, true, w, h, d);
            Program.Run(k.g, 6); k.ChopInto("meat", t => t.type == "stove" && t.item != null && t.item.type == "pan" && t.item.items.Count == 0);
            foreach (var c in k.G.chefs) c.idle = 0;
            Program.Shot(k.g, "geo_" + n + ".png");
        }
    }
}
static partial class Tests
{
    static void Look()
    {
        foreach (var (lv, n) in new[] { (Lvl("Taco Truck"), "taco"), (Lvl("Friday Night Rush"), "friday") })
        {
            var k = new K(lv, true, 412, 915, 2.625);
            Program.Run(k.g, 14);
            try { k.ChopInto("meat", t => t.type == "griddle" && t.item == null); } catch { }
            Program.Run(k.g, 1.5);
            foreach (var c in k.G.chefs) c.idle = 0;
            Program.Run(k.g, 1 / 60.0);
            Program.Shot(k.g, "look_" + n + ".png");
        }
        var g = Program.NewGame(new HarnessPlatform(), 412, 915, 2.625); Program.Run(g, 0.5); Program.Shot(g, "look_title.png");
    }
}
