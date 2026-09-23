using System;
using System.Collections.Generic;
using System.Linq;
using SearPressure;

// Kitchen scenarios, the same way the web version's tests drive it: stand a chef in front of a
// station, press Grab or Chop through the real input path, and check what happened.
static partial class Tests
{
    static int fails;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what); if (!ok) fails++; }

    sealed class K
    {
        public Game g;
        public K(int level, bool unlock = true)
        {
            g = Program.NewGame(new HarnessPlatform { unlock = unlock });
            Program.Run(g, 0.2);
            g.play(level);
            Program.Run(g, 3.6);    // countdown
        }
        public Kitchen G => g.G;
        public Chef C => G.chefs[G.active];
        // Put the active chef on a free floor tile next to the first tile matching `pred`, facing it.
        public Tile Go(Func<Tile, bool> pred)
        {
            var dirs = new[] { (0, 1), (0, -1), (1, 0), (-1, 0) };
            foreach (var t in G.tiles.Where(pred))
                foreach (var (dx, dy) in dirs)
                {
                    var f = G.TileAt(t.x + dx, t.y + dy);
                    if (f == null || f.type != "floor") continue;
                    C.x = f.x + 0.5; C.y = f.y + 0.5; C.fx = -dx; C.fy = -dy; C.task = null;
                    Program.Run(g, 1 / 60.0);
                    return t;
                }
            throw new Exception("no reachable tile");
        }
        public void Grab(Func<Tile, bool> pred, double wait = 0.15) { Go(pred); g.KeyDown("Space", false); g.KeyUp("Space"); Program.Run(g, wait); }
        public void Chop(Func<Tile, bool> pred, double wait = 2.3) { Go(pred); g.KeyDown("KeyE", false); g.KeyUp("KeyE"); Program.Run(g, wait); }
        public void Orders(params string[] recipes)
        {
            G.orders.Clear();
            foreach (var r in recipes) { var R = Data.RECIPES[r]; G.orders.Add(new Order { id = 300 + G.orders.Count, recipe = r, items = R.items.ToList(), reward = R.reward, total = 150, timeLeft = 150 }); }
            G.nextOrder = 9999;
        }
        public Func<Tile, bool> Crate(string kind) => t => t.type == "crate" && t.kind == kind;
        public void ChopInto(string kind, Func<Tile, bool> dest)
        {
            Grab(Crate(kind));
            Grab(t => t.type == "board" && t.item == null);
            Chop(t => t.type == "board" && t.item != null && t.item.kind == kind);
            Grab(t => t.type == "board" && t.item != null && t.item.kind == kind);
            if (dest != null) Grab(dest);
        }
        // Speed up anything cooking.
        public void Fast(double wait = 0.7)
        {
            foreach (var t in G.tiles) { var it = t.item; if (it == null) continue; if (it.cook > 0 && it.cook < 1) it.cook = 0.99; }
            Program.Run(g, wait);
        }
        public string Held => C.held == null ? "-" : C.held.type == "ing" ? Game.ingKey(C.held) : C.held.type + (C.held.items != null ? "[" + string.Join(",", C.held.items) + "]" : "");
    }

    static bool Plate(Tile t) => t.type == "plates";
    static bool Serve(Tile t) => t.type == "serve";
    static bool FreeCounter(Tile t) => t.type == "counter" && t.item == null;

    static void Kitchen()
    {
        fails = 0;
        Console.WriteLine("Salad Days: green and garden salads");
        {
            var k = new K(0); k.Orders("salad", "garden");
            k.ChopInto("lettuce", null);
            Check(k.Held == "lettuce:chopped", "chopped lettuce in hand (" + k.Held + ")");
            k.Grab(FreeCounter);
            k.Grab(Plate);
            Check(k.Held == "plate[]", "took a plate");
            k.Grab(t => t.item != null && t.item.kind == "lettuce");
            Check(k.Held == "plate[lettuce:chopped]", "plated lettuce (" + k.Held + ")");
            double before = k.G.score;
            k.Grab(Serve, 0.3);
            Check(k.G.served == 1 && k.G.score > before, $"served a green salad (+{k.G.score - before})");
            // Garden: lettuce + tomato on one plate.
            k.ChopInto("lettuce", FreeCounter);
            k.ChopInto("tomato", FreeCounter);
            k.Grab(Plate);
            k.Grab(t => t.item != null && t.item.kind == "lettuce");
            k.Grab(t => t.item != null && t.item.kind == "tomato");
            Check(k.Held == "plate[lettuce:chopped,tomato:chopped]", "garden salad plated (" + k.Held + ")");
            k.Grab(Serve, 0.3);
            Check(k.G.served == 2, "served a garden salad");
            Program.Shot(k.g, "k_salad.png");
        }
        Console.WriteLine("Soup Kitchen: three tomatoes in a pot");
        {
            var k = new K(1); k.Orders("tsoup");
            for (int i = 0; i < 3; i++) k.ChopInto("tomato", t => t.type == "stove" && t.item != null && t.item.type == "pot");
            var pot = k.G.tiles.First(t => t.type == "stove" && t.item.items.Count == 3).item;
            Check(pot.items.Count == 3, "pot has three tomatoes");
            Program.Run(k.g, 1);
            Check(pot.cook > 0, $"pot is cooking ({pot.cook:0.00})");
            k.Fast();
            Check(pot.cook >= 1, "soup is ready");
            k.Grab(Plate);
            k.Grab(t => t.type == "stove" && t.item.items.Count == 3);
            Check(k.Held == "plate[soup:tomato]", "poured soup on a plate (" + k.Held + ")");
            k.Grab(Serve, 0.3);
            Check(k.G.served == 1, "served tomato soup");
        }
        Console.WriteLine("The Divide: burgers (meat chopped, pan-fried, on a bun)");
        {
            var k = new K(2); k.Orders("burger");
            // The divide splits the chefs; reach whatever tile is needed with whichever chef can.
            k.ChopInto("meat", null);
            Check(k.Held == "meat:chopped", "chopped meat (" + k.Held + ")");
            k.Grab(t => t.type == "stove" && t.item != null && t.item.type == "pan");
            var pan = k.G.tiles.First(t => t.type == "stove" && t.item != null && t.item.type == "pan" && t.item.items.Count > 0).item;
            Program.Run(k.g, 1); k.Fast();
            Check(pan.cook >= 1, "patty cooked");
            k.Grab(Plate);
            k.Grab(k.Crate("bun"));
            k.Grab(t => t.type == "stove" && t.item != null && t.item.type == "pan" && t.item.items.Count > 0);
            Check(k.Held == "plate[bun:raw,meat:cooked]", "burger plated (" + k.Held + ")");
            k.Grab(Serve, 0.3);
            Check(k.G.served == 1, "served a burger");
            Program.Shot(k.g, "k_divide.png");
        }
        Console.WriteLine("Fire: a burning pot catches the counter; the extinguisher puts it out");
        {
            var k = new K(1); k.Orders("osoup");
            for (int i = 0; i < 3; i++) k.ChopInto("onion", t => t.type == "stove" && t.item != null && t.item.type == "pot");
            var st = k.G.tiles.First(t => t.type == "stove" && t.item.items.Count == 3);
            st.item.cook = 1; st.item.burn = 0.99;
            Program.Run(k.g, 0.5);
            Check(st.item.burnt && st.fire > 0, "pot burnt and the stove is on fire");
            Check(k.G.fires == 1, "fires counted for the daily quest");
            Program.Shot(k.g, "k_fire.png");
            k.Grab(t => t.item != null && t.item.type == "ext");
            Check(k.Held == "ext", "holding the extinguisher");
            k.Go(t => t.fire > 0 || t == st);
            k.g.KeyDown("KeyE", false); Program.Run(k.g, 2.5); k.g.KeyUp("KeyE");
            Check(k.G.tiles.All(t => t.fire <= 0), "fire is out");
        }
        Console.WriteLine("Sunrise Griddle: eggs and bacon on the griddle, dishes through the hatch");
        {
            var k = new K(4); k.Orders("eggsbacon");
            k.Grab(k.Crate("egg")); k.Grab(t => t.type == "griddle" && t.item == null);
            k.Grab(k.Crate("bacon")); k.Grab(t => t.type == "griddle" && t.item == null);
            Program.Run(k.g, 0.5); k.Fast();
            Check(k.G.tiles.Count(t => t.type == "griddle" && t.item != null && t.item.state == "cooked") == 2, "egg and bacon cooked");
            k.Grab(Plate);
            k.Grab(t => t.type == "griddle" && t.item != null && t.item.kind == "egg");
            k.Grab(t => t.type == "griddle" && t.item != null && t.item.kind == "bacon");
            k.Grab(Serve, 0.3);
            Check(k.G.served == 1, "served eggs & bacon");
            Program.Run(k.g, 5.5);
            Check(k.G.hatch.dirty == 1, "a dirty plate came back through the hatch");
            k.Grab(t => t.type == "hatch");
            k.Grab(t => t.type == "sink");
            int plates = k.G.plateTile.count;
            k.Chop(t => t.type == "sink", 2);
            Check(k.G.plateTile.count == plates + 1, "washed it back onto the stack");
        }
        Console.WriteLine("Bottomless Coffee: brew a mug and serve it in the cup");
        {
            var k = new K(5); k.Orders("coffee");
            k.Grab(t => t.type == "coffee");
            Program.Run(k.g, 4.5);
            var cm = k.G.tiles.First(t => t.type == "coffee" && t.item != null);
            k.Grab(t => t == cm);
            Check(k.Held == "coffee:brewed", "holding a coffee");
            k.Grab(Serve, 0.3);
            Check(k.G.served == 1, "served coffee without a plate");
        }
        Console.WriteLine(fails == 0 ? "ALL PASSED" : fails + " FAILED");
    }
}
