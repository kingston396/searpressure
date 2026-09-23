using System;
using System.Collections.Generic;
using System.Linq;

namespace SearPressure
{
    // Numbers by index that grow on demand, like the web version's sparse arrays (holes are null).
    public sealed class Sparse
    {
        public readonly List<double?> v = new List<double?>();
        public double this[int i]
        {
            get => i >= 0 && i < v.Count && v[i].HasValue ? v[i].Value : 0;
            set { while (v.Count <= i) v.Add(null); v[i] = value; }
        }
        public bool Has(int i) => i >= 0 && i < v.Count && v[i].HasValue && v[i].Value != 0;
        public object ToJson() => v.Select(x => x.HasValue ? (object)x.Value : null).ToList();
        public static Sparse From(object o)
        {
            var s = new Sparse();
            if (o is List<object> a) foreach (var x in a) s.v.Add(x is double d ? d : (double?)null);
            return s;
        }
    }

    public sealed class DailySave { public string key; public double best, plays; public bool quest; }
    public sealed class DailyPick { public string key; public int @base; }

    // The save file, stored as the same JSON the web version keeps in localStorage ("orderup.v1").
    public sealed class SaveData
    {
        public Sparse best = new Sparse(), stars = new Sparse(), drive = new Sparse();
        public bool muted, tutorialDone, lefty, driveTut;
        public bool music = true, buzz = true, shake = true;
        public string[] outfits = { "classic", "classic" };
        public Dictionary<string, bool> story = new Dictionary<string, bool>();
        public DailySave daily;
        public DailyPick dailyPick;
        public double wallet;
        public Dictionary<string, bool> owned = new Dictionary<string, bool>();
        public List<List<string>> gear = new List<List<string>> { new List<string>(), new List<string>() };
        public Dictionary<string, double> earned = new Dictionary<string, double>();
        public Dictionary<string, double> plays = new Dictionary<string, double>();
        public List<bool> driveSeen = new List<bool>();

        public const string Key = "orderup.v1";

        public static SaveData FromJson(string json)
        {
            var s = new SaveData();
            if (string.IsNullOrEmpty(json)) return s;
            object o;
            try { o = Json.Parse(json); } catch (Exception) { return s; }
            if (!(o is Dictionary<string, object> d)) return s;
            s.best = Sparse.From(J.Get(d, "best")); s.stars = Sparse.From(J.Get(d, "stars")); s.drive = Sparse.From(J.Get(d, "drive"));
            s.muted = J.Bool(d, "muted"); s.tutorialDone = J.Bool(d, "tutorialDone"); s.lefty = J.Bool(d, "lefty"); s.driveTut = J.Bool(d, "driveTut");
            s.music = !(J.Get(d, "music") is bool m) || m;
            s.buzz = !(J.Get(d, "buzz") is bool b) || b;
            s.shake = !(J.Get(d, "shake") is bool sh) || sh;
            var of = J.Strs(J.Get(d, "outfits")); if (of.Count > 0) s.outfits = new[] { of[0] ?? "classic", of.Count > 1 ? of[1] ?? "classic" : "classic" };
            if (J.Get(d, "story") is Dictionary<string, object> st) foreach (var kv in st) s.story[kv.Key] = kv.Value is bool v && v;
            if (J.Get(d, "daily") is Dictionary<string, object> dy) s.daily = new DailySave { key = J.Str(dy, "key"), best = J.Num(dy, "best"), plays = J.Num(dy, "plays"), quest = J.Bool(dy, "quest") };
            if (J.Get(d, "dailyPick") is Dictionary<string, object> dp) s.dailyPick = new DailyPick { key = J.Str(dp, "key"), @base = (int)J.Num(dp, "base") };
            s.wallet = J.Num(d, "wallet");
            if (J.Get(d, "owned") is Dictionary<string, object> ow) foreach (var kv in ow) if (kv.Value is bool v && v) s.owned[kv.Key] = true;
            if (J.Get(d, "gear") is List<object> g)
            {
                s.gear = new List<List<string>>();
                foreach (var lo in g) s.gear.Add(lo is List<object> l ? l.Select(x => x as string).ToList() : new List<string>());
                while (s.gear.Count < 2) s.gear.Add(new List<string>());
            }
            if (J.Get(d, "earned") is Dictionary<string, object> ea) foreach (var kv in ea) if (kv.Value is double v) s.earned[kv.Key] = v;
            if (J.Get(d, "plays") is Dictionary<string, object> pl) foreach (var kv in pl) if (kv.Value is double v) s.plays[kv.Key] = v;
            if (J.Get(d, "driveSeen") is List<object> ds) foreach (var x in ds) s.driveSeen.Add(x is bool v && v);
            return s;
        }

        public string ToJson()
        {
            var d = new Dictionary<string, object>
            {
                ["best"] = best.ToJson(), ["stars"] = stars.ToJson(), ["muted"] = muted, ["tutorialDone"] = tutorialDone,
                ["outfits"] = outfits.ToList(), ["story"] = story.ToDictionary(k => k.Key, k => (object)k.Value),
                ["daily"] = daily == null ? null : new Dictionary<string, object> { ["key"] = daily.key, ["best"] = daily.best, ["plays"] = daily.plays, ["quest"] = daily.quest },
                ["music"] = music, ["buzz"] = buzz, ["shake"] = shake, ["lefty"] = lefty, ["wallet"] = wallet,
                ["owned"] = owned.ToDictionary(k => k.Key, k => (object)k.Value),
                ["gear"] = gear.Select(l => (object)l.Select(x => (object)x).ToList()).ToList(),
                ["earned"] = earned.ToDictionary(k => k.Key, k => (object)k.Value),
                ["plays"] = plays.ToDictionary(k => k.Key, k => (object)k.Value),
                ["drive"] = drive.ToJson(), ["driveTut"] = driveTut, ["driveSeen"] = driveSeen.Select(x => (object)x).ToList(),
            };
            if (dailyPick != null) d["dailyPick"] = new Dictionary<string, object> { ["key"] = dailyPick.key, ["base"] = dailyPick.@base };
            return Json.Write(d);
        }

        // One chef's loadout: [hat, apron, gloves, shoes] item ids (null for empty).
        public List<string> Gear(int i)
        {
            while (gear.Count <= i) gear.Add(new List<string>());
            return gear[i];
        }
    }
}
