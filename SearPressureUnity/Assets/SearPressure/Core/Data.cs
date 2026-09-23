using System;
using System.Collections.Generic;
using System.Linq;

namespace SearPressure
{
    // Palette swaps for a sprite: which palette letter or slot digit gets which colour.
    public sealed class Sw : Dictionary<char, string>
    {
        public Sw() { }
        public Sw(IDictionary<char, string> d) : base(d) { }
        public static Sw From(object json)
        {
            if (!(json is Dictionary<string, object> d)) return null;
            var s = new Sw();
            foreach (var kv in d) if (kv.Key.Length > 0 && kv.Value is string v) s[kv.Key[0]] = v;
            return s;
        }
        public Sw Copy() => new Sw(this);
        public string Key()
        {
            var keys = Keys.ToList(); keys.Sort();
            return string.Join(",", keys.Select(k => k + "=" + this[k]));
        }
    }

    public sealed class Icon { public string name; public Sw sw; public Icon(string n, Sw s = null) { name = n; sw = s; } }

    public sealed class Stop { public string name, blurb; public int needStars; public bool bonus, extra; }

    public sealed class Level
    {
        public int stop, tier, needTotal, maxOrders, plates;
        public string name, tip, note;
        public double time, delivery, timeScale;
        public double[] stars, interval;
        public string[] recipes, map;
        public bool fire, dishes, finale, judge, tutorial;
        // Daily-challenge copies.
        public bool daily, oneChef;
        public string baseName;
        public double tipMul;
        public Level Clone()
        {
            var l = (Level)MemberwiseClone();
            l.stars = (double[])stars.Clone(); l.interval = (double[])interval.Clone();
            return l;
        }
    }

    public sealed class Recipe
    {
        public string key, name;
        public string[] items, extras;
        public double reward, extraReward, time;
        public int[] pick;
        public bool mug;
    }

    public sealed class PotRecipe { public string[] needs; public string result, color; public bool ordered; }
    public sealed class HeatFood { public string from; public double secs; }
    public sealed class HeatDef { public double burn; public Dictionary<string, HeatFood> foods = new Dictionary<string, HeatFood>(); }
    public sealed class Blend { public string[] needs; public string kind, state; }
    public sealed class EventDef { public string id, name, sub; public double dur; }
    public sealed class Helper { public string id, name, blurb, station; public Icon icon; }
    public sealed class GearItem { public string id, slot, name, text, col; public double price; public Dictionary<string, double> fx = new Dictionary<string, double>(); }
    public sealed class Upgrade { public string id, fork, group, name, text; public double price; public Icon icon; }
    public sealed class Equip { public string label, chars; public string[] ids; }
    public sealed class Outfit { public string id, name, hat; public int need; public bool judge; public string[] hc, jacket; }
    public sealed class CastMember { public string hat; public string[] hc, scarf, jacket; }
    public sealed class StoryScene { public string[] sky; public List<string[]> lines = new List<string[]>(); }
    public sealed class JudgeDef { public string id, name, sprite; }
    public sealed class TutStepDef { public string text, ui; public bool next, finish; public List<string[]> icons; }
    public sealed class Twist { public string id, name, blurb; public double starMul; }
    public sealed class DriveRun
    {
        public string name, blurb, key;
        public int seed, blocks, traffic, peds, works, wrecks, ramps, hazards;
        public double time;
        public double[] stars;
    }

    // Everything the web version keeps in its data tables, loaded from game.json and sprites.json
    // (exported straight from the web game so both versions share the same data).
    public static class Data
    {
        public static List<Stop> STOPS;
        public static List<Level> LEVELS;
        public static Level TUTORIAL;
        public static Dictionary<string, Recipe> RECIPES;
        public static Dictionary<string, Dictionary<string, string[]>> TOPPINGS;
        public static Dictionary<string, string[]> BUILD_ICONS;
        public static List<PotRecipe> POT_RECIPES;
        public static Dictionary<char, string> CRATES;
        public static Dictionary<string, HeatDef> HEAT;
        public static List<string> CUPS, CHOPPABLE, HOP_TILES, FIREPROOF, PROP_KINDS;
        public static List<Blend> BLENDS;
        public static string[] CHEF_COLORS, CHEF_DARK, STOP_GROUND, HAIRS, SHIRTS, HELPER_COLOR, ROOFS;
        public static Dictionary<string, string> ITEM_NAMES, KIND_NAMES, BURN_UP;
        public static Dictionary<string, (string type, double k)> HEAT_UP;
        public static List<EventDef> EVENTS;
        public static Dictionary<string, JudgeDef> JUDGES;
        public static Dictionary<string, object> JUDGE_LINES;
        public static List<Helper> HELPERS;
        public static List<string[]> GEAR_SLOTS;
        public static List<GearItem> GEAR;
        public static List<Upgrade> UPGRADES;
        public static List<Equip> EQUIP;
        public static Dictionary<string, string[]> HATS;
        public static List<Outfit> OUTFITS;
        public static Dictionary<string, CastMember> CAST;
        public static Dictionary<int, StoryScene> STORY;
        public static List<TutStepDef> TUT_STEPS;
        public static List<string> DAILY_QUEST_IDS;
        public static List<Twist> DAILY_TWISTS;
        public static List<DriveRun> DRIVE_RUNS;
        public static List<TutStepDef> DRIVE_TUT;
        public static List<Dictionary<string, object>> DRIVE_TUT_RAW;

        public static Dictionary<char, string> PAL;
        public static Dictionary<string, string[]> SPR;

        public static bool Loaded;

        static string[] SA(object o) => J.Strs(o).ToArray();
        static double[] DA(object o) => J.Nums(o).ToArray();

        static Level ReadLevel(object o)
        {
            return new Level
            {
                stop = (int)J.Num(o, "stop"), tier = (int)J.Num(o, "tier"), needTotal = (int)J.Num(o, "needTotal"),
                maxOrders = (int)J.Num(o, "maxOrders"), plates = (int)J.Num(o, "plates"),
                name = J.Str(o, "name"), tip = J.Str(o, "tip", ""), note = J.Str(o, "note"),
                time = J.Num(o, "time"), delivery = J.Num(o, "delivery"), timeScale = J.Num(o, "timeScale"),
                stars = DA(J.Get(o, "stars")), interval = DA(J.Get(o, "interval")),
                recipes = SA(J.Get(o, "recipes")), map = SA(J.Get(o, "map")),
                fire = J.Bool(o, "fire"), dishes = J.Bool(o, "dishes"), finale = J.Bool(o, "finale"),
                judge = J.Bool(o, "judge"), tutorial = J.Bool(o, "tutorial"),
            };
        }

        static Icon ReadIcon(object o)
        {
            var a = J.Arr(o);
            if (a == null || a.Count == 0) return null;
            return new Icon(a[0] as string, a.Count > 1 ? Sw.From(a[1]) : null);
        }

        public static void Load(string gameJson, string spritesJson)
        {
            var g = Json.Parse(gameJson);
            STOPS = J.Arr(J.Get(g, "STOPS")).Select(o => new Stop
            {
                name = J.Str(o, "name"), blurb = J.Str(o, "blurb"), needStars = (int)J.Num(o, "needStars"),
                bonus = J.Bool(o, "bonus"), extra = J.Bool(o, "extra"),
            }).ToList();
            LEVELS = J.Arr(J.Get(g, "LEVELS")).Select(ReadLevel).ToList();
            TUTORIAL = ReadLevel(J.Get(g, "TUTORIAL"));
            RECIPES = new Dictionary<string, Recipe>();
            foreach (var kv in J.Obj(J.Get(g, "RECIPES")))
            {
                var o = kv.Value;
                var pick = J.Nums(J.Get(o, "pick"));
                RECIPES[kv.Key] = new Recipe
                {
                    key = kv.Key, name = J.Str(o, "name"), items = SA(J.Get(o, "items")),
                    extras = J.Has(o, "extras") ? SA(J.Get(o, "extras")) : null,
                    reward = J.Num(o, "reward"), extraReward = J.Num(o, "extraReward"), time = J.Num(o, "time"),
                    pick = pick.Count == 2 ? new[] { (int)pick[0], (int)pick[1] } : null, mug = J.Bool(o, "mug"),
                };
            }
            TOPPINGS = new Dictionary<string, Dictionary<string, string[]>>();
            foreach (var kv in J.Obj(J.Get(g, "TOPPINGS")))
            {
                var inner = new Dictionary<string, string[]>();
                foreach (var kv2 in J.Obj(kv.Value)) inner[kv2.Key] = SA(kv2.Value);
                TOPPINGS[kv.Key] = inner;
            }
            BUILD_ICONS = new Dictionary<string, string[]>();
            foreach (var kv in J.Obj(J.Get(g, "BUILD_ICONS"))) BUILD_ICONS[kv.Key] = SA(kv.Value);
            POT_RECIPES = J.Arr(J.Get(g, "POT_RECIPES")).Select(o => new PotRecipe
            {
                needs = SA(J.Get(o, "needs")), result = J.Str(o, "result"), color = J.Str(o, "color"), ordered = J.Bool(o, "ordered"),
            }).ToList();
            CRATES = new Dictionary<char, string>();
            foreach (var kv in J.Obj(J.Get(g, "CRATES"))) CRATES[kv.Key[0]] = kv.Value as string;
            HEAT = new Dictionary<string, HeatDef>();
            foreach (var kv in J.Obj(J.Get(g, "HEAT")))
            {
                var h = new HeatDef { burn = J.Num(kv.Value, "burn") };
                foreach (var f in J.Obj(J.Get(kv.Value, "foods")))
                {
                    var a = J.Arr(f.Value);
                    h.foods[f.Key] = new HeatFood { from = a[0] as string, secs = (double)a[1] };
                }
                HEAT[kv.Key] = h;
            }
            CUPS = J.Strs(J.Get(g, "CUPS")); CHOPPABLE = J.Strs(J.Get(g, "CHOPPABLE"));
            HOP_TILES = J.Strs(J.Get(g, "HOP_TILES")); FIREPROOF = J.Strs(J.Get(g, "FIREPROOF"));
            PROP_KINDS = J.Strs(J.Get(g, "PROP_KINDS"));
            BLENDS = J.Arr(J.Get(g, "BLENDS")).Select(o => { var r = SA(J.Get(o, "result")); return new Blend { needs = SA(J.Get(o, "needs")), kind = r[0], state = r[1] }; }).ToList();
            CHEF_COLORS = SA(J.Get(g, "CHEF_COLORS")); CHEF_DARK = SA(J.Get(g, "CHEF_DARK"));
            STOP_GROUND = SA(J.Get(g, "STOP_GROUND")); HAIRS = SA(J.Get(g, "HAIRS")); SHIRTS = SA(J.Get(g, "SHIRTS"));
            HELPER_COLOR = SA(J.Get(g, "HELPER_COLOR")); ROOFS = SA(J.Get(g, "ROOFS"));
            ITEM_NAMES = J.Obj(J.Get(g, "ITEM_NAMES")).ToDictionary(k => k.Key, k => k.Value as string);
            KIND_NAMES = J.Obj(J.Get(g, "KIND_NAMES")).ToDictionary(k => k.Key, k => k.Value as string);
            BURN_UP = J.Obj(J.Get(g, "BURN_UP")).ToDictionary(k => k.Key, k => k.Value as string);
            HEAT_UP = new Dictionary<string, (string, double)>();
            foreach (var kv in J.Obj(J.Get(g, "HEAT_UP"))) { var a = J.Arr(kv.Value); HEAT_UP[kv.Key] = (a[0] as string, (double)a[1]); }
            EVENTS = J.Obj(J.Get(g, "EVENTS")).Select(kv => new EventDef { id = kv.Key, name = J.Str(kv.Value, "name"), sub = J.Str(kv.Value, "sub", ""), dur = J.Num(kv.Value, "dur") }).ToList();
            JUDGES = J.Obj(J.Get(g, "JUDGES")).ToDictionary(kv => kv.Key, kv => new JudgeDef { id = kv.Key, name = J.Str(kv.Value, "name"), sprite = J.Str(kv.Value, "sprite") });
            JUDGE_LINES = J.Obj(J.Get(g, "JUDGE_LINES"));
            HELPERS = J.Obj(J.Get(g, "HELPERS")).Select(kv => new Helper
            {
                id = kv.Key, name = J.Str(kv.Value, "name"), blurb = J.Str(kv.Value, "blurb"), station = J.Str(kv.Value, "station"), icon = ReadIcon(J.Get(kv.Value, "icon")),
            }).ToList();
            GEAR_SLOTS = J.Arr(J.Get(g, "GEAR_SLOTS")).Select(o => SA(o)).ToList();
            GEAR = J.Arr(J.Get(g, "GEAR")).Select(o =>
            {
                var gi = new GearItem { id = J.Str(o, "id"), slot = J.Str(o, "slot"), name = J.Str(o, "name"), text = J.Str(o, "text"), col = J.Str(o, "col"), price = J.Num(o, "price") };
                foreach (var kv in J.Obj(J.Get(o, "fx"))) gi.fx[kv.Key] = (double)kv.Value;
                return gi;
            }).ToList();
            UPGRADES = J.Arr(J.Get(g, "UPGRADES")).Select(o => new Upgrade
            {
                id = J.Str(o, "id"), fork = J.Str(o, "fork"), group = J.Str(o, "group"), name = J.Str(o, "name"), text = J.Str(o, "text"),
                price = J.Num(o, "price"), icon = ReadIcon(J.Get(o, "icon")),
            }).ToList();
            EQUIP = J.Arr(J.Get(g, "EQUIP")).Select(o => { var a = J.Arr(o); return new Equip { label = a[0] as string, chars = a[1] as string, ids = SA(a[2]) }; }).ToList();
            HATS = J.Obj(J.Get(g, "HATS")).ToDictionary(kv => kv.Key, kv => SA(kv.Value));
            OUTFITS = J.Arr(J.Get(g, "OUTFITS")).Select(o => new Outfit
            {
                id = J.Str(o, "id"), name = J.Str(o, "name"), hat = J.Str(o, "hat"), need = (int)J.Num(o, "need"), judge = J.Bool(o, "judge"),
                hc = SA(J.Get(o, "hc")), jacket = J.Has(o, "jacket") ? SA(J.Get(o, "jacket")) : null,
            }).ToList();
            CAST = J.Obj(J.Get(g, "CAST")).ToDictionary(kv => kv.Key, kv => new CastMember
            {
                hat = J.Str(kv.Value, "hat"), hc = SA(J.Get(kv.Value, "hc")), scarf = SA(J.Get(kv.Value, "scarf")),
                jacket = J.Has(kv.Value, "jacket") ? SA(J.Get(kv.Value, "jacket")) : null,
            });
            STORY = new Dictionary<int, StoryScene>();
            foreach (var kv in J.Obj(J.Get(g, "STORY")))
            {
                var sc = new StoryScene { sky = SA(J.Get(kv.Value, "sky")) };
                foreach (var l in J.Arr(J.Get(kv.Value, "lines"))) sc.lines.Add(SA(l));
                STORY[int.Parse(kv.Key)] = sc;
            }
            TUT_STEPS = J.Arr(J.Get(g, "TUT_STEPS")).Select(ReadTut).ToList();
            DAILY_QUEST_IDS = J.Arr(J.Get(g, "DAILY_QUESTS")).Select(o => J.Str(o, "id")).ToList();
            DAILY_TWISTS = J.Arr(J.Get(g, "DAILY_TWISTS")).Select(o => new Twist { id = J.Str(o, "id"), name = J.Str(o, "name"), blurb = J.Str(o, "blurb"), starMul = J.Num(o, "starMul") }).ToList();
            DRIVE_RUNS = J.Arr(J.Get(g, "DRIVE_RUNS")).Select(o => new DriveRun
            {
                name = J.Str(o, "name"), blurb = J.Str(o, "blurb"), key = J.Str(o, "key"), seed = (int)J.Num(o, "seed"),
                blocks = (int)J.Num(o, "blocks"), traffic = (int)J.Num(o, "traffic"), peds = (int)J.Num(o, "peds"), works = (int)J.Num(o, "works"),
                wrecks = (int)J.Num(o, "wrecks"), ramps = (int)J.Num(o, "ramps"), hazards = (int)J.Num(o, "hazards"), time = J.Num(o, "time"),
                stars = DA(J.Get(o, "stars")),
            }).ToList();
            DRIVE_TUT_RAW = J.Arr(J.Get(g, "DRIVE_TUT")).Select(o => J.Obj(o)).ToList();
            DRIVE_TUT = DRIVE_TUT_RAW.Select(o => ReadTut(o)).ToList();

            var sp = Json.Parse(spritesJson);
            PAL = new Dictionary<char, string>();
            foreach (var kv in J.Obj(J.Get(sp, "PAL"))) PAL[kv.Key[0]] = kv.Value as string;
            SPR = new Dictionary<string, string[]>();
            foreach (var kv in J.Obj(J.Get(sp, "SPR"))) SPR[kv.Key] = SA(kv.Value);
            Loaded = true;
        }

        static TutStepDef ReadTut(object o)
        {
            var t = new TutStepDef { text = J.Str(o, "text"), ui = J.Str(o, "ui"), next = J.Bool(o, "next"), finish = J.Bool(o, "finish") };
            if (J.Get(o, "icons") is List<object> ic) { t.icons = new List<string[]>(); foreach (var i in ic) t.icons.Add(SA(i)); }
            return t;
        }

        public static Recipe R(string key) => RECIPES[key];
        public static Helper HelperById(string id) => HELPERS.FirstOrDefault(h => h.id == id);
        public static EventDef EventById(string id) => EVENTS.FirstOrDefault(e => e.id == id);
    }
}
