using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SearPressure
{
    public sealed class DailyQuest { public string id, text; public double n; public Func<Kitchen, bool> ok; }
    public sealed class DailyState { public string key; public int @base; public bool forced; public Level lv; public Twist twist; public double seed; public DailyQuest quest; }
    public sealed class Pay { public double paid; public string text = ""; }

    public sealed partial class Game
    {
        // ---- stars and unlocks ----
        // Co-op scaling: each extra human player makes service 25% busier and raises star targets to match.
        public static double crewFactor(int humans) => 1 + COOP_STEP * (Math.Max(1, humans) - 1);
        public double[] starTargets(int lvIdx, int humans = 1) => lvOf(lvIdx).stars.Select(s => U.Round(s * crewFactor(humans) / 10) * 10).ToArray();
        public int starsFor(int lvIdx, double score, int humans = 1) => starTargets(lvIdx, humans).Count(s => score >= s);
        // Stars are remembered as earned (older saves only had scores, scored solo).
        public int bestStars(int i) => i < 0 ? 0 : Math.Max((int)save.stars[i], save.best.Has(i) ? starsFor(i, save.best[i]) : 0);
        public int stopStars(int s) => Enumerable.Range(0, Data.LEVELS.Count).Where(i => Data.LEVELS[i].stop == s).Sum(bestStars);
        public bool firstOfStop(int i) => Data.LEVELS.FindIndex(l => l.stop == Data.LEVELS[i].stop) == i;
        public int totalStars() => Enumerable.Range(0, Data.LEVELS.Count).Sum(bestStars);
        // A campaign stop only shows up in the list once the last kitchen of the stop before it is open.
        // The Judge's Table and the bonus kitchens stay hidden until they're unlocked.
        public bool hiddenBonus(int i) { var st = Data.STOPS[Data.LEVELS[i].stop]; return (st.bonus || st.extra) && !unlocked(i); }
        public bool stopVisible(int si)
        {
            if (si == 0 || unlockAll()) return true;
            if (Data.STOPS[si].bonus || Data.STOPS[si].extra) return Enumerable.Range(0, Data.LEVELS.Count).Any(i => Data.LEVELS[i].stop == si && unlocked(i));
            int last = -1; for (int i = 0; i < Data.LEVELS.Count; i++) if (Data.LEVELS[i].stop == si - 1) last = i;
            return last < 0 || unlocked(last);
        }
        public bool unlocked(int i)
        {
            if (unlockAll()) return true;
            var lv = Data.LEVELS[i];
            if (lv.needTotal > 0) return totalStars() >= lv.needTotal;
            if (Data.STOPS[lv.stop].bonus) return bestStars(i - 1) >= 1;
            if (!firstOfStop(i)) return bestStars(i - 1) >= 1;
            return lv.stop == 0 || stopStars(lv.stop - 1) >= Data.STOPS[lv.stop].needStars;
        }

        // ---- shop: gear and upgrades ----
        public static GearItem gearById(string id) => Data.GEAR.FirstOrDefault(g => g.id == id);
        public bool owns(string id) => id != null && save.owned.TryGetValue(id, out var v) && v;
        // Upgrades count unless the service is meant to be even for everyone (versus, the daily challenge).
        public bool upOn(string id) => owns(id) && !(G != null && G.fair);
        public double heatBoost(string type) { foreach (var kv in Data.HEAT_UP) if (kv.Value.type == type && upOn(kv.Key)) return 1 + kv.Value.k; return 1; }
        public double burnGuard(string type) => Data.BURN_UP.TryGetValue(type, out var id) && upOn(id) ? 1.25 : 1;
        public static GearStats gearStats(IEnumerable<string> ids)
        {
            var st = new GearStats();
            if (ids != null) foreach (var id in ids) { var g = gearById(id); if (g != null) foreach (var kv in g.fx) st.Add(kv.Key, kv.Value); }
            return st;
        }
        public List<string> myLoadout(int i) => save.Gear(i).Where(id => id != null && owns(id)).ToList();
        // Which gear a chef wears: yours solo; online, your chef wears yours and your friends' wear theirs.
        public GearStats chefGear(int i, bool fair)
        {
            if (fair) return gearStats(null);
            if (NET.role == null) return gearStats(myLoadout(i));
            int me = NET.role == "host" ? 0 : NET.slot;
            return gearStats(i == me ? myLoadout(0) : (i < NET.gears.Count ? NET.gears[i] : null));
        }
        // Gear lists from other phones: only real item ids, one per slot at most.
        public static List<string> cleanGear(object a) => a is List<object> l ? l.Select(x => x as string).Where(x => x != null && gearById(x) != null).Take(Data.GEAR_SLOTS.Count).ToList() : new List<string>();
        public double addToWallet(double n)
        {
            n = Math.Max(0, U.Round(n));
            save.wallet += n; persist();
            return n;
        }
        public static string fmtCoins(double n) => U.FmtCoins(n);

        // ---- coins per level ----
        // Coins pay out once per level: a bonus for three stars on the first attempt,
        // and nothing more once a level has three stars (you can still replay it for a best score).
        public string levelKey(int idx) => idx == DAILY_IDX ? "daily:" + (DAILY != null ? DAILY.key : todayKey()) : idx == TUTORIAL_IDX ? "tutorial" : "k" + idx;
        public int priorStars(string key)
        {
            if (key.StartsWith("daily:")) return save.daily != null && "daily:" + save.daily.key == key && save.daily.best > 0 ? starsFor(DAILY_IDX, save.daily.best) : 0;
            if (key.StartsWith("drive")) { int k = int.Parse(key.Substring(5)); double b = save.drive[k]; return b > 0 ? driveStars(k, b) : 0; }
            return bestStars(int.Parse(key.Substring(1)));
        }
        public bool hasPlayed(string key)
        {
            if ((save.plays.TryGetValue(key, out var p) && p > 0) || priorStars(key) > 0) return true;
            if (key[0] == 'k') return save.best.Has(int.Parse(key.Substring(1)));
            if (key.StartsWith("drive")) return save.drive.Has(int.Parse(key.Substring(5)));
            return save.daily != null && "daily:" + save.daily.key == key && save.daily.plays > 0;
        }
        // Each level can only ever pay its three-star target in total. Once that's used up (or three
        // stars are earned) the level is completed, and replays pay 20% of what you earn.
        public const double REPLAY_RATE = 0.2;
        public double budgetOf(string key) => key.StartsWith("drive") ? Data.DRIVE_RUNS[int.Parse(key.Substring(5))].stars[2] : Data.LEVELS[int.Parse(key.Substring(1))].stars[2];
        public double earnedAt(string key) => save.earned.TryGetValue(key, out var v) ? v : 0;
        public bool isCompleted(string key) => priorStars(key) >= 3 || earnedAt(key) >= budgetOf(key);
        // Every start counts as an attempt (restarts and quits too), so the first try can't be retried.
        public Attempt beginAttempt(string key)
        {
            bool daily = key.StartsWith("daily:");
            var att = new Attempt { key = key, daily = daily, first = !hasPlayed(key), completed = !daily && isCompleted(key) };
            save.plays[key] = (save.plays.TryGetValue(key, out var p) ? p : 0) + 1; persist();
            return att;
        }
        public static double firstTryBonus(double target3) => U.Round(target3 * 0.5 / 10) * 10;
        public Pay settle(Attempt att, double earned, int stars, double target3)
        {
            if (att == null || att.daily) return new Pay();
            earned = Math.Max(0, U.Round(earned));
            if (att.completed)
            {
                double paid0 = addToWallet(U.Round(earned * REPLAY_RATE));
                return new Pay { paid = paid0, text = $"Completed level: replays pay 20%. +{fmtCoins(paid0)} coins ({fmtCoins(save.wallet)} to spend)." };
            }
            double budget = budgetOf(att.key), left = Math.Max(0, budget - earnedAt(att.key));
            double bas = Math.Min(earned, left), bonus = att.first && stars >= 3 ? firstTryBonus(target3) : 0;
            save.earned[att.key] = stars >= 3 ? budget : earnedAt(att.key) + bas;
            double paid = addToWallet(bas + bonus);
            bool done = save.earned[att.key] >= budget;
            return new Pay
            {
                paid = paid,
                text = (bonus > 0 ? $"First-try three stars! +{fmtCoins(bas)} coins and a +{fmtCoins(bonus)} bonus" : $"+{fmtCoins(bas)} coins") + $" ({fmtCoins(save.wallet)} to spend)."
                    + (done ? " Level completed: replays now pay 20% and have random events." : $" {fmtCoins(budget - save.earned[att.key])} more to earn here.") + (earned > bas ? $" ({fmtCoins(earned - bas)} over this level's limit.)" : ""),
            };
        }
        public Pay settleQuest()
        {
            var q = DAILY.quest; var lv = G.lv;
            bool scoreOk = G.score >= lv.stars[2] && !G.kicked, goalOk = q.ok(G);
            bool already = questDone();
            if (save.daily == null || save.daily.key != DAILY.key) save.daily = new DailySave { key = DAILY.key };
            if (scoreOk && goalOk && !already)
            {
                save.daily.quest = true; persist();
                double paid = addToWallet(QUEST_REWARD);
                return new Pay { paid = paid, text = $"Daily quest complete! +{QUEST_REWARD} coins ({fmtCoins(save.wallet)} to spend)." };
            }
            if (already) return new Pay { text = "Today’s quest is already done. A new one arrives tomorrow." };
            return new Pay { text = $"Quest: {(scoreOk ? "✓" : "✗")} score {fmtCoins(lv.stars[2])} (you got {fmtCoins(G.score)}) · {(goalOk ? "✓" : "✗")} {q.text}. Try again!" };
        }
        public string coinNote(string key, double target3)
        {
            if (isCompleted(key)) return "Completed: replays pay 20% of the coins you earn" + (priorStars(key) >= 3 && key[0] == 'k' ? ", and random events can hit mid-service." : ".");
            double left = budgetOf(key) - earnedAt(key);
            return (!hasPlayed(key) ? $"First try: three stars on your first attempt pays a +{fmtCoins(firstTryBonus(target3))} bonus. " : "") + $"Coins left to earn here: {fmtCoins(left)} of {fmtCoins(budgetOf(key))}.";
        }

        // Kitchen upgrades only show once a kitchen you've unlocked has their equipment.
        public bool equipOpen(string chars)
        {
            return string.IsNullOrEmpty(chars) || unlockAll() || Enumerable.Range(0, Data.LEVELS.Count).Any(i => unlocked(i) && Data.LEVELS[i].map.Any(row => chars.Any(ch => row.IndexOf(ch) >= 0)));
        }
        // The other half of a fork this player already owns, if any.
        public Upgrade forkTaken(Upgrade it) => it.fork != null ? Data.UPGRADES.FirstOrDefault(u => u.fork == it.fork && u.id != it.id && owns(u.id)) : null;
        public const double SELL_RATE = 0.25;
        public static double sellPrice(Upgrade it) => U.Round(it.price * SELL_RATE);
        public static string statsText(GearStats st)
        {
            var names = new (string k, string n)[] { ("chop", "chopping"), ("wash", "washing"), ("walk", "walking"), ("carry", "walking with a plate"), ("spray", "firefighting"), ("tip", "tips"), ("burn", "burn time") };
            var parts = names.Where(p => st.Get(p.k) != 0).Select(p => $"{p.n} {(st.Get(p.k) > 0 ? "+" : "−")}{U.Round(Math.Abs(st.Get(p.k)) * 100)}%").ToList();
            return parts.Count > 0 ? string.Join(", ", parts) : "no bonuses yet";
        }

        // ---- outfits ----
        public static Outfit outfitOf(string id) => Data.OUTFITS.FirstOrDefault(o => o.id == id) ?? Data.OUTFITS[0];
        public bool outfitUnlocked(Outfit o)
        {
            if (unlockAll()) return true;
            return o.judge ? bestStars(Data.LEVELS.FindIndex(l => l.judge)) >= 1 : totalStars() >= o.need;
        }
        // A chef sprite wearing a given hat (side views sit one pixel further left).
        public static string chefSprite(string name, string hat)
        {
            string key = name + "_" + hat;
            if (!Data.SPR.ContainsKey(key))
            {
                bool side = name.StartsWith("chefSide");
                Data.SPR[key] = Data.HATS[hat].Select(r => side ? r.Substring(1) + "." : r).Concat(Data.SPR[name].Skip(6)).ToArray();
            }
            return key;
        }
        public static Sw outfitSwaps(string hat0, string hat1, string[] jacket, string color, string dark)
        {
            var sw = new Sw { ['1'] = color, ['2'] = dark, ['3'] = hat0, ['4'] = hat1 };
            if (jacket != null) { sw['w'] = jacket[0]; sw['W'] = jacket[1]; }
            return sw;
        }
        public static Sw outfitSwaps(Outfit o, string color, string dark) => outfitSwaps(o.hc[0], o.hc[1], o.jacket, color, dark);
        // Which outfit a chef wears: yours solo; online, your first chef's outfit and your friends'.
        public string chefFit(int i, bool solo = false)
        {
            var mine = save.outfits;
            if (NET.role == null || solo) return i < mine.Length ? mine[i] ?? "classic" : "classic";
            int me = NET.role == "host" ? 0 : NET.slot;
            return i == me ? mine[0] ?? "classic" : i < NET.fits.Count ? NET.fits[i] : "classic";
        }

        // ---- daily challenge: one kitchen, twist and ticket order for everyone each day ----
        public const int QUEST_REWARD = 400;
        public DailyState DAILY;

        DailyQuest makeQuest(string id, Level lv)
        {
            switch (id)
            {
                case "nomiss": return new DailyQuest { id = id, text = "Don’t miss a single order", ok = g => g.failed == 0 };
                case "nofire": return new DailyQuest { id = id, text = "No fires at all", ok = g => g.fires == 0 };
                case "speedy": return new DailyQuest { id = id, text = "Serve 5 orders with a speedy tip", ok = g => g.speedy >= 5 };
                case "nobin": return new DailyQuest { id = id, text = "Never use the bin", ok = g => g.binned == 0 };
                default:
                    {
                        double n = U.Round(lv.time / 16);
                        return new DailyQuest { id = "served", n = n, text = $"Serve at least {n} orders", ok = g => g.served >= n };
                    }
            }
        }
        static void applyTwist(string id, Level lv)
        {
            switch (id)
            {
                case "rush": lv.interval = lv.interval.Select(v => v * 0.8).ToArray(); break;
                case "tips": lv.tipMul = 2; break;
                case "overtime": lv.time += 90; break;
                case "solo": lv.oneChef = true; break;
            }
        }

        // Days change at midnight UTC, so everyone in the world gets the same challenge at the same moment.
        public string todayKey() => platform.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        // The kitchen comes from the campaign kitchens this player has unlocked. It's remembered for the
        // day so unlocking a new kitchen doesn't swap it; online guests get the host's (forceBase).
        public Level dailyLevel(string key = null, int? forceBase = null)
        {
            key = key ?? todayKey();
            if (DAILY != null && DAILY.key == key && (forceBase == null ? !DAILY.forced : DAILY.@base == forceBase)) return DAILY.lv;
            var r = Rng.Mulberry32(Rng.SeedOf(key));
            var pool = Enumerable.Range(0, Data.LEVELS.Count).Where(i => Data.LEVELS[i].stop <= 9 && unlocked(i)).ToList();
            if (pool.Count == 0) pool.Add(0);
            int pick = pool[(int)Math.Floor(r() * pool.Count)];
            var kept = save.dailyPick;
            int bi = forceBase ?? (kept != null && kept.key == key && pool.Contains(kept.@base) ? kept.@base : pick);
            if (forceBase == null && !(kept != null && kept.key == key && kept.@base == bi)) { save.dailyPick = new DailyPick { key = key, @base = bi }; persist(); }
            var bas = Data.LEVELS[bi];
            var twist = Data.DAILY_TWISTS[(int)Math.Floor(r() * Data.DAILY_TWISTS.Count)];
            var lv = bas.Clone();
            lv.daily = true; lv.baseName = bas.name;
            applyTwist(twist.id, lv);
            double mul = twist.starMul > 0 ? twist.starMul : lv.time / bas.time;
            lv.stars = bas.stars.Select(v => U.Round(v * mul / 10) * 10).ToArray();
            var qs = Data.DAILY_QUEST_IDS.Where(id => id != "nofire" || lv.fire).ToList();
            var q = makeQuest(qs[(int)Math.Floor(r() * qs.Count)], lv);
            DAILY = new DailyState { key = key, @base = bi, forced = forceBase != null, lv = lv, twist = twist, seed = Rng.SeedOf(key + ":orders"), quest = q };
            return lv;
        }
        public bool questDone() => save.daily != null && save.daily.key == todayKey() && save.daily.quest;
        public double dailyBest() => save.daily != null && save.daily.key == todayKey() ? save.daily.best : 0;
        public string dailyDate() => platform.UtcNow.ToString("MMM d", CultureInfo.InvariantCulture);
    }
}
