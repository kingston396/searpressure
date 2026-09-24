using System;
using System.Collections.Generic;
using System.Linq;

namespace SearPressure
{
    // Anything a chef can hold or a counter can carry. `type` is ing, plate, pot, pan, ext, dirty or bag.
    public sealed class Item
    {
        public string type, kind, state;
        public double chop, cook, burn, warnT, mitts;
        public List<string> items;        // plate, pot, pan and bag contents
        public bool burnt, over;
        public double? hotAt;
        public int n;                     // dirty plates in a stack
        public double? hopT;              // live lobsters
        public int moved = -1;

        public static Item Ing(string kind, string state = null) => new Item { type = "ing", kind = kind, state = state ?? (kind == "coffee" ? "brewed" : "raw") };
        public static Item Plate() => new Item { type = "plate", items = new List<string>() };
        public static Item Pot() => new Item { type = "pot", items = new List<string>() };
        public static Item Pan() => new Item { type = "pan", items = new List<string>() };
        public string Key => kind + ":" + state;

        // Snapshots for online play.
        public object ToJson()
        {
            var d = new Dictionary<string, object> { ["type"] = type };
            if (kind != null) d["kind"] = kind;
            if (state != null) d["state"] = state;
            if (chop != 0) d["chop"] = Math.Round(chop, 3);
            if (cook != 0) d["cook"] = Math.Round(cook, 3);
            if (burn != 0) d["burn"] = Math.Round(burn, 3);
            if (items != null) d["items"] = items.Select(x => (object)x).ToList();
            if (burnt) d["burnt"] = true;
            if (over) d["over"] = true;
            if (hotAt.HasValue) d["hotAt"] = Math.Round(hotAt.Value, 2);
            if (n != 0) d["n"] = n;
            return d;
        }
        public static Item FromJson(object o)
        {
            if (!(o is Dictionary<string, object> d)) return null;
            var it = new Item { type = J.Str(d, "type"), kind = J.Str(d, "kind"), state = J.Str(d, "state"), chop = J.Num(d, "chop"), cook = J.Num(d, "cook"), burn = J.Num(d, "burn"), burnt = J.Bool(d, "burnt"), over = J.Bool(d, "over"), n = (int)J.Num(d, "n") };
            if (J.Get(d, "items") is List<object> l) it.items = l.Select(x => x as string).ToList();
            if (J.Get(d, "hotAt") is double h) it.hotAt = h;
            return it;
        }
    }

    public sealed class Tile
    {
        public int x, y;
        public string type = "floor", kind;
        public Item item;
        public double fire, spreadT, @out;
        public bool sprayed;
        public int count, dirty;
        public double wash, brew, blend;
        public bool brewing, blending;
        public List<string> jar;
    }

    public sealed class ChefTask { public string kind; public Tile tile; }

    public sealed class Ai
    {
        public string kind;
        public Route plan;
        public string action;
        public double cool, age;
    }

    public sealed class Route { public Tile tile; public List<(int x, int y)> path; public int[] face; }

    public sealed class GearStats
    {
        public double chop, wash, walk, carry, spray, tip, burn, reach;
        public void Add(string k, double v)
        {
            switch (k)
            {
                case "chop": chop += v; break; case "wash": wash += v; break; case "walk": walk += v; break; case "carry": carry += v; break;
                case "spray": spray += v; break; case "tip": tip += v; break; case "burn": burn += v; break; case "reach": reach += v; break;
            }
        }
        public double Get(string k) => k switch { "chop" => chop, "wash" => wash, "walk" => walk, "carry" => carry, "spray" => spray, "tip" => tip, "burn" => burn, "reach" => reach, _ => 0 };
    }

    public sealed class Chef
    {
        public double x, y, fx, fy = 1, walk, taskSfx, toastT, idle;
        public Item held;
        public string color, dark, toast, fit;
        public bool moving, spraying;
        public ChefTask task;
        public Tile target;
        public GearStats gear;
        public Ai ai;
        public double? tx, ty;   // online: where the host says another chef is
    }

    public sealed class Order
    {
        public int id;
        public string recipe;
        public List<string> items;
        public double reward, total, timeLeft;
        public double? x;
        public bool delivery, late, vip;
    }

    public sealed class Float { public string at, text, color, sub; public double x, y, dy, life; }
    public sealed class Puff { public double x, y, vx, vy, life, max; public string color, kind; }
    public sealed class JudgeState { public string text, who; public double t; public int mood; }
    public sealed class Evt { public string kind, name; public double t; }
    public sealed class SchedEvent { public string kind; public double at; public bool done; }
    public sealed class Attempt { public string key; public bool daily, first, completed; }
    public sealed class Zoom { public int id; public double t; }
    public sealed class TutState { public int i; public double sx, sy; public int active; }
    public sealed class Person
    {
        public double x, y, walk, life;
        public string shirt, hair, mood;
        public bool moving;
        public Order o;
    }
    public sealed class TruckLayout
    {
        public Tile serve, pick;
        public string ss, ps, cab;
        public Dictionary<string, double> m;
    }

    // The kitchen during a service (the web version's `G`).
    public sealed class Kitchen
    {
        public int lvIdx;
        public Level lv;
        public int rows, cols;
        public List<Tile> tiles;
        public List<Chef> chefs;
        public int active;
        public List<Order> orders = new List<Order>();
        public int orderSeq;
        public string lastRecipe;
        public double nextOrder = 0.6;
        public double score, tips;
        public int served, failed;
        public double time;
        public string phase = "countdown";
        public double countdown = 3.2, endT;
        public int lastCount = 4, lastSec = 99;
        public List<double> returns = new List<double>();
        public List<Float> floats = new List<Float>();
        public List<Puff> puffs = new List<Puff>();
        public double swapFx, sprayT, crackleT, swapPing, shake, coinPulse, clock;
        public bool swapGuard;
        public Tile plateTile, hatch;
        public bool transposed;
        public List<Tile> netTiles;
        public int strikes;
        public JudgeState judge;
        public bool kicked;
        public bool revived;          // used this service's one rewarded-ad revive
        public int humans = 1;
        public bool versus, fair;
        public HashSet<string> crateKinds;
        public RandFn rng, evRng;
        public double vary;
        public bool eventsOn;
        public List<SchedEvent> events = new List<SchedEvent>();
        public Attempt attempt;
        public Evt evt;
        public bool vipNext;
        public int binned, speedy, fires;
        public double missLoss;
        public Zoom zoom;
        public TutState tut;
        public TruckLayout truck;
        public Dictionary<int, Person> crowd;
        public List<Person> leavers;
        public int? lastFailed;
        public int frame;
        public bool shown;

        public Tile TileAt(int x, int y) => x < 0 || y < 0 || x >= cols || y >= rows ? null : tiles[y * cols + x];
    }
}
