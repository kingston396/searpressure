using System;
using System.Collections.Generic;
using System.Linq;
using SearPressure;

static partial class Tests
{
    static int Lvl(string name) => Data.LEVELS.FindIndex(l => l.name == name);

    static void Kitchen2()
    {
        fails = 0;
        Console.WriteLine("Curb Service: fries, and a delivery ticket bagged and handed off");
        {
            var k = new K(Lvl("Curb Service")); k.Orders("fries");
            k.G.orders[0].delivery = true;
            k.ChopInto("potato", t => t.type == "fryer" && t.item == null);
            Program.Run(k.g, 0.5); k.Fast();
            var fr = k.G.tiles.First(t => t.type == "fryer" && t.item != null);
            Check(fr.item.state == "cooked", "fries cooked in the fryer");
            k.Grab(Plate); k.Grab(t => t == fr);
            k.Grab(Serve, 0.3);
            Check(k.G.served == 0 && k.C.toast == "That one's delivery: bag it", "the bell refuses a delivery (" + k.C.toast + ")");
            k.Grab(t => t.type == "bagger");
            Check(k.Held == "bag[potato:cooked]", "bagged (" + k.Held + ")");
            k.Grab(t => t.type == "pickup", 0.3);
            Check(k.G.served == 1, "handed off at the pickup window");
        }
        Console.WriteLine("Shake Shack: a strawberry shake in the blender");
        {
            var k = new K(Lvl("Shake Shack")); k.Orders("sshake");
            k.Grab(k.Crate("icecream")); k.Grab(t => t.type == "blender");
            k.Grab(k.Crate("strawberry")); k.Grab(t => t.type == "blender");
            k.Chop(t => t.type == "blender", 4);
            var bl = k.G.tiles.First(t => t.type == "blender" && t.item != null);
            Check(Game.ingKey(bl.item) == "shake:strawberry", "blended a strawberry shake");
            k.Grab(t => t == bl); k.Grab(Serve, 0.3);
            Check(k.G.served == 1, "served the shake in its cup");
        }
        Console.WriteLine("Brisket Board: smoke, slice, plate with cornbread");
        {
            var k = new K(Lvl("Brisket Board")); k.Orders("brisket");
            k.Grab(k.Crate("brisket")); k.Grab(t => t.type == "smoker" && t.item == null);
            k.Grab(k.Crate("cornmeal")); k.Grab(t => t.type == "griddle" && t.item == null);
            Program.Run(k.g, 0.5); k.Fast();
            k.Grab(t => t.type == "smoker" && t.item != null);
            Check(k.Held == "brisket:cooked", "smoked brisket (" + k.Held + ")");
            k.Grab(t => t.type == "board" && t.item == null);
            k.Chop(t => t.type == "board" && t.item != null);
            var b = k.G.tiles.First(t => t.type == "board" && t.item != null);
            Check(b.item.state == "sliced", "sliced it on a board");
            k.Grab(Plate); k.Grab(t => t == b); k.Grab(t => t.type == "griddle" && t.item != null);
            Check(k.Held == "plate[brisket:sliced,cornmeal:cooked]", "brisket plate (" + k.Held + ")");
            k.Grab(Serve, 0.3);
            Check(k.G.served == 1, "served the brisket plate");
        }
        Console.WriteLine("Corner Slice: stretch, sauce, cheese, bake, slice");
        {
            var k = new K(Lvl("Corner Slice")); k.Orders("slice");
            k.Grab(k.Crate("dough")); k.Grab(t => t.type == "board" && t.item == null);
            k.Chop(t => t.type == "board" && t.item != null);
            var b = k.G.tiles.First(t => t.type == "board" && t.item != null);
            Check(b.item.state == "stretched", "stretched the dough");
            k.Grab(k.Crate("cheese")); k.Grab(t => t == b);
            Check(k.C.toast == "Add the sauce first", "cheese first is refused with a hint (" + k.C.toast + ")");
            k.Grab(FreeCounter);
            k.Grab(k.Crate("sauce")); k.Grab(t => t == b);
            k.Grab(t => t.type == "counter" && t.item != null && t.item.kind == "cheese"); k.Grab(t => t == b);
            Check(Game.ingKey(b.item) == "pizza:raw", "sauced and cheesed (" + Game.ingKey(b.item) + ")");
            k.Grab(t => t == b); k.Grab(t => t.type == "oven" && t.item == null);
            Program.Run(k.g, 0.5); k.Fast();
            k.Grab(t => t.type == "oven" && t.item != null); k.Grab(t => t.type == "board" && t.item == null);
            k.Chop(t => t.type == "board" && t.item != null);
            k.Grab(Plate); k.Grab(t => t.type == "board" && t.item != null && t.item.state == "sliced");
            k.Grab(Serve, 0.3);
            Check(k.G.served == 1, "served a cheese slice");
        }
        Console.WriteLine("Taco Truck: exactly the toppings on the ticket");
        {
            var k = new K(Lvl("Taco Truck")); k.Orders("taco");
            k.G.orders[0].items = new List<string> { "tortilla:cooked", "meat:cooked", "cheese:raw" };
            k.Grab(k.Crate("tortilla")); k.Grab(t => t.type == "griddle" && t.item == null);
            k.ChopInto("meat", t => t.type == "griddle" && t.item == null);
            Program.Run(k.g, 0.5); k.Fast();
            k.Grab(Plate);
            k.Grab(t => t.type == "griddle" && t.item != null && t.item.kind == "tortilla");
            k.Grab(t => t.type == "griddle" && t.item != null && t.item.kind == "meat");
            k.Grab(k.Crate("cheese"));
            Check(k.Held == "plate[tortilla:cooked,meat:cooked,cheese:raw]", "taco built (" + k.Held + ")");
            k.Grab(Serve, 0.3);
            Check(k.G.served == 1, "served the taco");
        }
        Console.WriteLine("Lobster Pound: a live lobster wanders off a counter");
        {
            var k = new K(Lvl("Lobster Pound")); k.Orders("lobsterroll");
            k.Grab(k.Crate("lobster"));
            var inner = k.G.tiles.First(t => t.type == "counter" && t.item == null && t.x > 0 && t.y > 0 && t.x < k.G.cols - 1 && t.y < k.G.rows - 1 && k.G.TileAt(t.x + 1, t.y)?.type == "counter" && k.G.TileAt(t.x + 1, t.y).item == null);
            k.Grab(t => t == inner);
            Program.Run(k.g, 4.5);
            Check(inner.item == null, "the lobster hopped away");
        }
        Console.WriteLine("The Judge's Table: a wrong dish is a strike");
        {
            var k = new K(Lvl("The Judge's Table")); k.Orders("tsoup");
            k.ChopInto("lettuce", null);
            k.Grab(FreeCounter); k.Grab(Plate); k.Grab(t => t.item != null && t.item.kind == "lettuce");
            k.Grab(Serve, 0.3);
            Check(k.G.strikes == 1 && k.G.judge != null, "strike one: " + k.G.judge?.text);
            Program.Shot(k.g, "k_judge.png");
        }
        Console.WriteLine("Judge's Table helpers: a Prep Cook chops what's left on a board");
        {
            var p = new HarnessPlatform { unlock = true };
            var g = Program.NewGame(p); Program.Run(g, 0.2);
            int j = Lvl("The Judge's Table");
            g.openIntro(j); g.chosenHelpers = new List<string> { "chop" }; g.play(j); Program.Run(g, 3.6);
            Check(g.G.chefs.Count == 3 && g.G.chefs[2].ai != null, "hired a helper");
            var board = g.G.tiles.First(t => t.type == "board");
            board.item = Item.Ing("lettuce");
            Program.Run(g, 8);
            Check(board.item != null && board.item.state == "chopped", "the helper chopped the lettuce");
        }
        Console.WriteLine("Tutorial: every step moves on");
        {
            var p = new HarnessPlatform();
            var g = Program.NewGame(p); Program.Run(g, 0.2);
            g.startTutorial(); Program.Run(g, 0.1);
            Program.Shot(g, "k_tut0.png");
            var c = g.G.chefs[0];
            c.x += 1.5; Program.Run(g, 0.1);
            Check(g.G.tut.i == 1, "walking finishes step 1");
            g.tutNext(); Program.Run(g, 0.1);
            var kk = new K2(g);
            kk.Grab(t => t.type == "crate" && t.kind == "lettuce");
            kk.Grab(t => t.type == "board" && t.item == null);
            kk.Chop(t => t.type == "board" && t.item != null);
            kk.Grab(t => t.type == "plates");
            kk.Grab(t => t.item != null && t.item.kind == "lettuce");
            kk.Grab(t => t.type == "serve", 0.3);
            Check(g.G.tut.i == 8, "served the first order (step " + (g.G.tut.i + 1) + ")");
            g.tutNext(); Program.Run(g, 0.1);
            g.KeyDown("KeyQ", false); Program.Run(g, 0.1);
            Check(g.G.tut.i == 10, "swapping finishes the swap step");
            Program.Shot(g, "k_tut10.png");
        }
        Console.WriteLine(fails == 0 ? "ALL PASSED" : fails + " FAILED");
    }

    // The same stand-and-press helpers for a game that's already running.
    sealed class K2
    {
        readonly Game g;
        public K2(Game g) { this.g = g; }
        Chef C => g.G.chefs[g.G.active];
        void Go(Func<Tile, bool> pred)
        {
            foreach (var t in g.G.tiles.Where(pred))
                foreach (var (dx, dy) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
                {
                    var f = g.G.TileAt(t.x + dx, t.y + dy);
                    if (f == null || f.type != "floor") continue;
                    C.x = f.x + 0.5; C.y = f.y + 0.5; C.fx = -dx; C.fy = -dy;
                    Program.Run(g, 1 / 60.0); return;
                }
        }
        public void Grab(Func<Tile, bool> pred, double wait = 0.15) { Go(pred); g.KeyDown("Space", false); g.KeyUp("Space"); Program.Run(g, wait); }
        public void Chop(Func<Tile, bool> pred, double wait = 2.3) { Go(pred); g.KeyDown("KeyE", false); g.KeyUp("KeyE"); Program.Run(g, wait); }
    }
}
