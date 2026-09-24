using System;
using System.Collections.Generic;
using System.Linq;

namespace SearPressure
{
    public sealed class StartOpts
    {
        public bool? transpose, events;
        public string dailyKey;
        public int? dailyBase;
        public int players;
        public bool versus;
        public double? seed, vary;
    }

    public sealed class Plan
    {
        public string label;
        public bool dim;
        public Action run;
        public Plan(string label, Action run, bool dim = false) { this.label = label; this.run = run; this.dim = dim; }
    }
    public sealed class ActPlan { public string kind, label; public Tile tile; public bool dim; }

    public sealed partial class Game
    {
        static Dictionary<string, Recipe> RECIPES => Data.RECIPES;

        public static HeatFood heatFood(Tile t, Item it)
        {
            if (it == null || it.type != "ing" || !Data.HEAT.TryGetValue(t.type, out var h)) return null;
            return h.foods.TryGetValue(it.kind, out var f) ? f : null;
        }
        public static bool heatAccepts(Tile t, Item it) { var f = heatFood(t, it); return f != null && (it.state == f.from || it.state == "cooked" || it.state == "burnt"); }
        public static bool onHeat(Tile t) => heatAccepts(t, t.item);
        public static bool isCookware(Item i) => i != null && (i.type == "pot" || i.type == "pan");
        public static string ingKey(Item i) => i.kind + ":" + i.state;

        public Level lvOf(int i) => i == DAILY_IDX ? (G != null && G.lv.daily ? G.lv : dailyLevel()) : i == TUTORIAL_IDX ? Data.TUTORIAL : Data.LEVELS[i];

        public void startLevel(int idx, StartOpts opts = null)
        {
            opts = opts ?? new StartOpts();
            var lv = idx == TUTORIAL_IDX ? Data.TUTORIAL : idx == DAILY_IDX ? dailyLevel(opts.dailyKey, opts.dailyBase) : Data.LEVELS[idx];
            tutHidden = true;
            var map = lv.map.ToArray();
            // No two services alike: a seed per service may mirror the kitchen, shuffles the crates and
            // schedules random events. The daily quest, versus and online friends all share the host's seed.
            bool calm = lv.tutorial || CALM;
            double vary = opts.vary ?? (lv.daily ? DAILY.seed + 7 : Math.Floor(Random() * 2147483647));
            var vr = Rng.Mulberry32(vary);
            if (!calm && vr() < 0.5) map = map.Select(row => new string(row.Reverse().ToArray())).ToArray();
            // Tall screens get tall kitchens: transpose wide maps in portrait. Online, the host decides for both.
            bool transposed = opts.transpose ?? (H > W && map[0].Length > map.Length);
            if (transposed) { var m0 = map; map = Enumerable.Range(0, m0[0].Length).Select(x => new string(m0.Select(row => row[x]).ToArray())).ToArray(); }
            int rows = map.Length, cols = map[0].Length;
            var tiles = new List<Tile>();
            var spawns = new Dictionary<char, (double x, double y)>();
            for (int y = 0; y < rows; y++) for (int x = 0; x < cols; x++)
                {
                    char ch = map[y][x];
                    var t = new Tile { x = x, y = y };
                    switch (ch)
                    {
                        case '#': t.type = "counter"; break;
                        case 'X': t.type = "serve"; break;
                        case 'P': t.type = "plates"; t.count = lv.plates + (owns("plates") && !lv.daily && !opts.versus && !lv.tutorial ? 1 : 0); break;
                        case 'R': t.type = "trash"; break;
                        case 'C': t.type = "board"; break;
                        case 'S': t.type = "stove"; t.item = Item.Pot(); break;
                        case 'F': t.type = "stove"; t.item = Item.Pan(); break;
                        case 'G': t.type = "griddle"; break;
                        case 'Y': t.type = "fryer"; break;
                        case 's': t.type = "smoker"; break;
                        case 'o': t.type = "oven"; break;
                        case 'V': t.type = "blender"; t.jar = new List<string>(); break;
                        case 'Q': t.type = "coffee"; break;
                        case 'K': t.type = "sink"; break;
                        case 'H': t.type = "hatch"; break;
                        case 'A': t.type = "bagger"; break;
                        case '@': t.type = "pickup"; break;
                        case 'Z': t.type = "counter"; t.item = new Item { type = "ext" }; break;
                        case '1': case '2': spawns[ch] = (x + 0.5, y + 0.5); break;
                        default: if (Data.CRATES.TryGetValue(ch, out var kind)) { t.type = "crate"; t.kind = kind; } break;
                    }
                    tiles.Add(t);
                }
            // Versus: every phone runs its own copy of the kitchen, solo with two chefs.
            bool shared = NET.role != null && !opts.versus;
            if (!calm)
            {
                var crates = tiles.Where(t => t.type == "crate").ToList();
                var kinds = crates.Select(t => t.kind).ToList();
                for (int k = kinds.Count - 1; k > 0; k--) { int j = (int)Math.Floor(vr() * (k + 1)); var tmp = kinds[k]; kinds[k] = kinds[j]; kinds[j] = tmp; }
                for (int k = 0; k < crates.Count; k++) crates[k].kind = kinds[k];
            }
            int nChefs = shared ? Math.Max(2, Math.Min(MAX_PLAYERS, opts.players > 0 ? opts.players : 2)) : 2;
            var starts = new List<(double x, double y)> { spawns['1'], spawns['2'] };
            for (int i = 2; i < nChefs; i++)
            {
                var from = starts[i % 2];
                Tile best = null; double bestD = 1e9;
                foreach (var t in tiles)
                {
                    if (t.type != "floor" || starts.Any(p => Math.Floor(p.x) == t.x && Math.Floor(p.y) == t.y)) continue;
                    double d = U.Hypot(t.x + 0.5 - from.x, t.y + 0.5 - from.y);
                    if (d < bestD) { bestD = d; best = t; }
                }
                starts.Add((best.x + 0.5, best.y + 0.5));
            }
            var chefs = starts.Select((sp, i) => new Chef
            {
                x = sp.x, y = sp.y, fx = 0, fy = 1, color = Data.CHEF_COLORS[i], dark = Data.CHEF_DARK[i],
                fit = chefFit(i, opts.versus), gear = chefGear(i, lv.daily || opts.versus || lv.tutorial),
            }).ToList();
            G = new Kitchen
            {
                lvIdx = idx, lv = lv, rows = rows, cols = cols, tiles = tiles, chefs = chefs, active = 0,
                time = lv.time,
                plateTile = tiles.FirstOrDefault(t => t.type == "plates"), hatch = tiles.FirstOrDefault(t => t.type == "hatch"),
                transposed = transposed, netTiles = tiles.Where(t => t.type != "floor").ToList(),
                humans = shared ? nChefs : 1, versus = opts.versus, fair = lv.daily || opts.versus,
                crateKinds = new HashSet<string>(tiles.Where(t => t.type == "crate").Select(t => t.kind)),
            };
            // The daily challenge deals the same tickets to everyone; one chef only when it's Short-Staffed (solo).
            G.rng = lv.daily ? Rng.Mulberry32(DAILY.seed) : opts.seed != null ? Rng.Mulberry32(opts.seed.Value) : (RandFn)Random;
            if (lv.oneChef && (NET.role == null || opts.versus)) G.chefs.RemoveRange(1, G.chefs.Count - 1);
            input.grab = input.chop = input.swap = false;
            G.vary = vary;
            // Random events only on replays of a level you've already three-starred (and on the daily quest).
            bool eventsOn = opts.events ?? (lv.daily || (idx >= 0 && bestStars(idx) >= 3));
            G.eventsOn = !calm && eventsOn;
            G.events = G.eventsOn ? scheduleEvents(lv, vr) : new List<SchedEvent>();
            G.evRng = Rng.Mulberry32(vary + 3);
            if (!lv.tutorial) G.attempt = beginAttempt(levelKey(idx));
            if (lv.judge) { judgeSay("start", 0); G.judge.t = 6; }
        }

        // More cooks, more tickets on the rail (one extra per extra human, up to the ticket rail's width).
        public int maxOrders() => Math.Min(6, G.lv.maxOrders + (G.humans > 0 ? G.humans : 1) - 1);

        // ---- random kitchen events ----
        List<SchedEvent> scheduleEvents(Level lv, RandFn vr)
        {
            var kinds = Data.EVENTS.Select(e => e.id).ToList();
            var outL = new List<SchedEvent>();
            int n = lv.time >= 200 ? 2 : 1;
            for (int k = 0; k < n; k++)
            {
                string kind;
                do kind = kinds[(int)Math.Floor(vr() * kinds.Count)]; while (outL.Any(e => e.kind == kind));
                outL.Add(new SchedEvent { kind = kind, at = lv.time * (k > 0 ? 0.58 + vr() * 0.17 : 0.22 + vr() * 0.2) });
            }
            return outL;
        }
        public void startEvent(string kind)
        {
            var E = Data.EventById(kind);
            string sub = E.sub;
            if (E.dur > 0) G.evt = new Evt { kind = kind, name = E.name, t = E.dur };
            if (kind == "rush") G.nextOrder = Math.Min(G.nextOrder, 1);
            else if (kind == "vip") G.vipNext = true;
            else if (kind == "patient") foreach (var o in G.orders) { o.timeLeft += 10; o.total += 10; }
            else if (kind == "outage")
            {
                var want = new HashSet<string>(menuItems().SelectMany(x => x).Select(k => k.Split(':')[0]));
                var crates = G.tiles.Where(t => t.type == "crate").ToList();
                var pick = crates.Where(t => want.Contains(t.kind)).ToList();
                var from = pick.Count > 0 ? pick : crates;
                if (from.Count == 0) return;
                var t = from[(int)Math.Floor(G.evRng() * from.Count)];
                t.@out = 15; sub = "No " + t.kind + " for 15 seconds";
            }
            addFloat("hud", 0, 0, E.name, "#f5d33e", sub);
            sfx("order");
        }
        public double tipBoost() => (G.lv.tipMul > 0 ? G.lv.tipMul : 1) * (G.evt != null && G.evt.kind == "tipper" ? 2 : 1);
        void updateEvents(double dt)
        {
            if (G.phase == "play") foreach (var ev in G.events) if (!ev.done && G.lv.time - G.time >= ev.at) { ev.done = true; startEvent(ev.kind); }
            if (G.evt != null) { G.evt.t -= dt; if (G.evt.t <= 0) G.evt = null; }
            foreach (var t in G.tiles) if (t.@out > 0) t.@out = Math.Max(0, t.@out - dt);
        }

        public Tile tileAt(int x, int y) => G?.TileAt(x, y);
        bool solidAt(int x, int y) { var t = tileAt(x, y); return t == null || t.type != "floor"; }

        // ---- movement ----
        bool collides(double x, double y)
        {
            int x0 = (int)Math.Floor(x - CHEF_R), x1 = (int)Math.Floor(x + CHEF_R - 1e-6);
            int y0 = (int)Math.Floor(y - CHEF_R), y1 = (int)Math.Floor(y + CHEF_R - 1e-6);
            for (int ty = y0; ty <= y1; ty++) for (int tx = x0; tx <= x1; tx++) if (solidAt(tx, ty)) return true;
            return false;
        }
        public void moveChef(Chef c, double mx, double my, double dt)
        {
            double mag = U.Hypot(mx, my);
            c.moving = mag > 0.15;
            if (mag > 0.15)
            {
                c.fx = mx / mag; c.fy = my / mag;
                c.walk += dt * Math.Min(1, mag);
                c.task = null;
            }
            if (mag < 0.15) return;
            var gr = c.gear;
            bool carrying = c.held != null && (c.held.type == "plate" || (c.held.type == "ing" && Data.CUPS.Contains(c.held.kind)));
            double sp = SPEED * (1 + (gr != null ? gr.walk + (carrying ? gr.carry : 0) : 0)) * Math.Min(1, mag) * dt;
            // Axis-separated moves with a small step so we slide along counters.
            double nx = c.x + mx / Math.Max(1, mag) * sp;
            if (!collides(nx, c.y)) c.x = nx;
            else c.x = mx > 0 ? Math.Floor(nx + CHEF_R) - CHEF_R - 0.001 : Math.Floor(nx - CHEF_R) + 1 + CHEF_R + 0.001;
            double ny = c.y + my / Math.Max(1, mag) * sp;
            if (!collides(c.x, ny)) c.y = ny;
            else c.y = my > 0 ? Math.Floor(ny + CHEF_R) - CHEF_R - 0.001 : Math.Floor(ny - CHEF_R) + 1 + CHEF_R + 0.001;
            // Don't walk through the other chef. Solo, you slip past your own other chef instead of
            // getting stuck behind them; if they're idle (not chopping or washing) they step aside.
            foreach (var o in G.chefs)
            {
                if (o == c) continue;
                double dx = c.x - o.x, dy = c.y - o.y, d = U.Hypot(dx, dy), min = CHEF_R * 2;
                if (d < min && NET.role == null && o.ai == null && c == G.chefs[G.active])
                {
                    if (o.task == null)
                    {
                        double ux = mx / mag, uy = my / mag, side = (o.x - c.x) * -uy + (o.y - c.y) * ux >= 0 ? 1 : -1;
                        double st = sp * 1.2 * side, nx2 = o.x - uy * st, ny2 = o.y + ux * st;
                        if (!collides(nx2, ny2)) { o.x = nx2; o.y = ny2; }
                    }
                    continue;
                }
                if (d < min && d > 0.0001)
                {
                    double px = c.x + dx / d * (min - d), py = c.y + dy / d * (min - d);
                    if (!collides(px, c.y)) c.x = px;
                    if (!collides(c.x, py)) c.y = py;
                }
            }
        }

        // The counter the chef is facing: nearest interactive tile in front of them.
        public Tile findTarget(Chef c)
        {
            Tile best = null; double bestScore = -1e9;
            int cx = (int)Math.Floor(c.x), cy = (int)Math.Floor(c.y);
            for (int y = cy - 1; y <= cy + 1; y++) for (int x = cx - 1; x <= cx + 1; x++)
                {
                    var t = tileAt(x, y);
                    if (t == null || t.type == "floor") continue;
                    double dx = x + 0.5 - c.x, dy = y + 0.5 - c.y, d = U.Hypot(dx, dy);
                    if (d > 1.35) continue;
                    double dot = (dx * c.fx + dy * c.fy) / d;
                    if (dot < 0.3) continue;
                    double s = dot * 1.2 - d * 0.7;
                    if (s > bestScore) { bestScore = s; best = t; }
                }
            return best;
        }

        // ---- combining rules ----
        // Mug drinks never go on plates.
        public List<List<string>> menuItems() => G.lv.recipes.Where(k => !RECIPES[k].mug).Select(k => RECIPES[k].items.Concat(RECIPES[k].extras ?? new string[0]).ToList()).ToList();
        public static List<string> orderItems(Order o) => o.items ?? RECIPES[o.recipe].items.ToList();
        public static double orderReward(Order o) => o.reward;
        // A plate may hold a set of items only if it's on the way to something on the menu.
        public bool canPlate(Item plate, string key)
        {
            var next = plate.items.Concat(new[] { key }).ToList();
            return menuItems().Any(items => subMultiset(next, items));
        }
        public static bool subMultiset(IList<string> small, IList<string> big)
        {
            var pool = big.ToList();
            foreach (var k in small) { int i = pool.IndexOf(k); if (i < 0) return false; pool.RemoveAt(i); }
            return true;
        }
        // Pot recipes whose result this kitchen's menu uses.
        public List<PotRecipe> potRecipes() { var want = new HashSet<string>(menuItems().SelectMany(x => x)); return Data.POT_RECIPES.Where(r => want.Contains(r.result)).ToList(); }
        public static bool potFits(IList<string> items, PotRecipe r) => r.ordered ? items.Select((k, i) => r.needs.Length > i && r.needs[i] == k).All(b => b) && items.Count <= r.needs.Length : subMultiset(items, r.needs);
        public PotRecipe potTarget(Item p) => potRecipes().FirstOrDefault(r => potFits(p.items, r));
        public PotRecipe potFull(Item p) => potRecipes().FirstOrDefault(r => r.needs.Length == p.items.Count && potFits(p.items, r));
        // Topping in the wrong order (cheese before sauce): name the step that's missing.
        string toppingHint(Item bas, Item top)
        {
            if (bas == null || top == null || bas.type != "ing" || top.type != "ing") return null;
            if (!Data.TOPPINGS.TryGetValue(ingKey(bas), out var next)) return null;
            var needsBase = Data.TOPPINGS.Keys.FirstOrDefault(k => Data.TOPPINGS[k].ContainsKey(top.kind));
            if (needsBase == null) return null;
            var step = next.Keys.FirstOrDefault(x => string.Join(":", next[x]) == needsBase);
            return step != null ? "Add the " + step + " first" : null;
        }
        // For ordered pots: tell the chef what goes in next instead of a generic refusal.
        string potHint(Item p, Item ing)
        {
            if (p == null || p.type != "pot" || ing == null || ing.type != "ing" || p.cook > 0) return null;
            var r = potRecipes().FirstOrDefault(q => q.ordered && potFits(p.items, q) && q.needs.Contains(ingKey(ing)));
            return r != null && p.items.Count < r.needs.Length ? "Add the " + r.needs[p.items.Count].Split(':')[0] + " next" : null;
        }
        public bool cookwareFull(Item cw) => cw.type == "pot" ? potFull(cw) != null : cw.items.Count == 1;
        public string cookResult(Item cw)
        {
            if (!isCookware(cw) || cw.burnt || cw.cook < 1) return null;
            if (cw.type == "pot") { var r = potFull(cw); return r?.result; }
            return "meat:cooked";
        }
        public bool canCook(Item cw, Item ing)
        {
            if (!isCookware(cw) || ing == null || ing.type != "ing" || cw.burnt || cw.cook > 0) return false;
            if (cw.type == "pot") { var next = cw.items.Concat(new[] { ingKey(ing) }).ToList(); return potRecipes().Any(r => potFits(next, r)); }
            return ing.kind == "meat" && ing.state == "chopped" && cw.items.Count == 0;
        }
        static readonly string[] SLICE_COOKED = { "brisket", "pizza", "pepperonipizza", "turkey", "pie" };
        // What chopping turns a food into, or null if it can't be chopped.
        public static string chopResult(Item it)
        {
            if (it == null || it.type != "ing") return null;
            if (it.state == "raw" && Data.CHOPPABLE.Contains(it.kind)) return "chopped";
            if (it.kind == "dough" && it.state == "raw") return "stretched";
            if (it.kind == "pastrami" && it.state == "raw") return "sliced";
            if (it.state == "cooked" && SLICE_COOKED.Contains(it.kind)) return "sliced";
            return null;
        }
        static void emptyCookware(Item cw) { cw.items = new List<string>(); cw.cook = 0; cw.burn = 0; cw.burnt = false; }

        Plan combinePlan(Chef c, Tile t)
        {
            var h = c.held; var it = t.item;
            // Toppings onto a base (either way round).
            if (h.type == "ing" && it.type == "ing")
            {
                string[] on = Data.TOPPINGS.TryGetValue(ingKey(it), out var tp) && tp.TryGetValue(h.kind, out var o1) ? o1 : null;
                if (on != null && h.state == "raw") return new Plan("Add", () => { it.kind = on[0]; it.state = on[1]; it.cook = 0; c.held = null; sfx("drop"); });
                string[] under = Data.TOPPINGS.TryGetValue(ingKey(h), out var tp2) && tp2.TryGetValue(it.kind, out var o2) ? o2 : null;
                if (under != null && it.state == "raw") return new Plan("Add", () => { h.kind = under[0]; h.state = under[1]; h.cook = 0; t.item = null; sfx("pick"); });
            }
            if (h.type == "ing" && it.type == "plate" && canPlate(it, ingKey(h)))
                return new Plan("Plate", () => { platePush(it, ingKey(h), h); c.held = null; sfx("drop"); });
            if (h.type == "ing" && canCook(it, h))
                return new Plan("Add", () => { it.items.Add(ingKey(h)); c.held = null; sfx("drop"); });
            if (h.type == "plate" && it.type == "ing" && canPlate(h, ingKey(it)))
                return new Plan("Plate", () => { platePush(h, ingKey(it), it); t.item = null; sfx("pick"); });
            if (h.type == "plate" && cookResult(it) != null && canPlate(h, cookResult(it)))
                return new Plan("Plate", () => { platePush(h, cookResult(it), it); emptyCookware(it); sfx("pick"); });
            if (isCookware(h) && it.type == "plate" && cookResult(h) != null && canPlate(it, cookResult(h)))
                return new Plan("Plate", () => { platePush(it, cookResult(h), h); emptyCookware(h); sfx("drop"); });
            if (isCookware(h) && canCook(h, it))
                return new Plan("Add", () => { h.items.Add(ingKey(it)); t.item = null; sfx("pick"); });
            return null;
        }

        public Plan grabPlan(Chef c)
        {
            var t = c.target;
            if (t == null) return null;
            var h = c.held; var it = t.item;
            if (t.fire > 0) return new Plan("Fire!", () => { say(c, "Put the fire out first"); sfx("nope"); }, true);
            if (t.type == "crate" && t.@out > 0 && (h == null || h.type == "plate")) return new Plan("Out", () => { say(c, "Out of stock"); sfx("nope"); }, true);
            if (h == null)
            {
                if (t.type == "plates") return t.count > 0 ? new Plan("Plate", () => { t.count--; c.held = Item.Plate(); sfx("pick"); }) : null;
                if (t.type == "hatch") return t.dirty > 0 ? new Plan("Grab", () => { c.held = new Item { type = "dirty", n = t.dirty }; t.dirty = 0; sfx("pick"); }) : null;
                if (t.type == "sink" || t.type == "bagger" || t.type == "pickup") return null;
                if (t.type == "coffee" && it == null) return t.brewing ? null : new Plan("Brew", () => { t.brewing = true; t.brew = 0; sfx("brew"); });
                if (t.type == "blender" && it == null) return null;
                if (it != null) return new Plan("Grab", () => { c.held = it; t.item = null; sfx("pick"); if (it.type == "ext") say(c, "Hold Spray to use"); });
                if (t.type == "crate") return new Plan("Grab", () => { c.held = Item.Ing(t.kind); sfx("pick"); });
                return null;
            }
            if (t.type == "trash")
            {
                if (h.type == "ing") return new Plan("Bin", () => { c.held = null; G.binned++; sfx("trash"); });
                if (h.type == "dirty") return new Plan("Bin", () => { say(c, "Wash them in the sink"); sfx("nope"); }, true);
                if (h.type == "ext") return null;
                if (h.type == "bag") return new Plan("Bin", () => { c.held = null; G.binned++; sfx("trash"); });
                if (h.items != null && h.items.Count > 0) return new Plan("Bin", () => { G.binned++; if (isCookware(h)) emptyCookware(h); else { h.items = new List<string>(); h.hotAt = null; h.over = false; } sfx("trash"); });
                return null;
            }
            if (t.type == "bagger")
            {
                if ((h.type == "plate" && h.items.Count > 0) || (h.type == "ing" && Data.CUPS.Contains(h.kind)))
                    return new Plan("Bag", () => bagUp(c));
                return new Plan("Bag", () => { say(c, h.type == "bag" ? "Take it to the pickup window" : "Bag a finished plate"); sfx("nope"); }, true);
            }
            if (t.type == "pickup")
            {
                if (h.type == "bag") return new Plan("Hand off", () => deliver(c));
                return new Plan("Hand off", () => { say(c, "Bag it first"); sfx("nope"); }, true);
            }
            if (h.type == "bag" && t.type == "serve") return new Plan("Serve", () => { say(c, "Bags go to the pickup window"); sfx("nope"); }, true);
            if (t.type == "serve")
            {
                if ((h.type == "plate" && h.items.Count > 0) || (h.type == "ing" && Data.CUPS.Contains(h.kind))) return new Plan("Serve", () => serve(c));
                return new Plan("Serve", () => { say(c, h.type == "plate" ? "Plate is empty" : "Put it on a plate"); sfx("nope"); }, true);
            }
            if (t.type == "plates")
                return h.type == "plate" && h.items.Count == 0 ? new Plan("Drop", () => { t.count++; c.held = null; sfx("drop"); }) : null;
            if (t.type == "sink" || t.type == "hatch")
                return h.type == "dirty" ? new Plan("Drop", () => { t.dirty += h.n; c.held = null; sfx("splash"); }) : null;
            if (t.type == "coffee")
                return it == null && !t.brewing && h.type == "ing" && h.kind == "coffee" ? new Plan("Drop", () => { t.item = h; c.held = null; sfx("drop"); }) : null;
            if (t.type == "blender")
            {
                if (it != null || t.blending || h.type != "ing" || h.state != "raw" || !Data.BLENDS.Any(b => subMultiset(t.jar.Concat(new[] { h.kind }).ToList(), b.needs))) return null;
                return new Plan("Add", () => { t.jar.Add(h.kind); c.held = null; sfx("drop"); });
            }
            // Holding a plate at a crate of something ready to eat (chips, cheese, buns): straight onto the plate.
            if (t.type == "crate" && it == null && h.type == "plate" && canPlate(h, t.kind + ":raw"))
                return new Plan("Plate", () => { platePush(h, t.kind + ":raw", null); sfx("pick"); });
            if (it == null)
            {
                if (t.type == "stove" && !isCookware(h)) return null;
                if (Data.HEAT.ContainsKey(t.type) && !heatAccepts(t, h)) return null;
                return new Plan("Drop", () => { t.item = h; c.held = null; sfx("drop"); });
            }
            return combinePlan(c, t);
        }

        // What the second button does right now: chop, wash or spray.
        public ActPlan actionPlan(Chef c)
        {
            if (c.held != null && c.held.type == "ext") return new ActPlan { kind = "spray", label = "Spray" };
            var t = c.target;
            if (t == null || t.fire > 0) return null;
            if (t.type == "sink" && t.dirty > 0) return new ActPlan { kind = "wash", label = "Wash", tile = t, dim = c.held != null };
            if (t.type == "blender" && !t.blending && t.item == null && Data.BLENDS.Any(b => sameItems(b.needs, t.jar))) return new ActPlan { kind = "blend", label = "Blend", tile = t };
            var it = t.item;
            if (t.type != "board" || chopResult(it) == null) return null;
            string cr = chopResult(it);
            string label = it.kind == "turkey" ? "Carve" : it.kind == "clam" ? "Shuck" : it.kind == "fish" ? "Fillet" : cr == "sliced" ? "Slice" : cr == "stretched" ? "Stretch" : "Chop";
            return new ActPlan { kind = "chop", label = label, tile = t, dim = c.held != null };
        }

        // ---- actions ----
        public void say(Chef c, string text) { c.toast = text; c.toastT = 1.6; }

        public void doGrab(Chef c)
        {
            var p = grabPlan(c);
            if (p != null)
            {
                p.run(); c.task = null;
                var t = c.target;
                if (c.gear != null && c.gear.burn > 0 && t != null && t.item != null && (Data.HEAT.ContainsKey(t.type) || t.type == "stove")) t.item.mitts = c.gear.burn;
            }
            else
            {
                sfx("nope");
                if (c.held != null && c.target != null) say(c, potHint(c.target.item, c.held) ?? potHint(c.held, c.target.item) ?? toppingHint(c.target.item, c.held) ?? "Can't put that there");
            }
        }
        public void doChop(Chef c)
        {
            var p = actionPlan(c);
            if (p != null && p.kind == "spray") return;       // spraying is handled while the button is held
            if (p == null) { sfx("nope"); return; }
            if (p.dim) { say(c, "Hands full"); sfx("nope"); return; }
            if (p.kind == "blend") { p.tile.blending = true; p.tile.blend = 0; sfx("blend"); return; }
            c.task = new ChefTask { kind = p.kind, tile = p.tile };
        }
        // Your other chef just finished chopping or washing: the Swap button pulses to say they're free.
        public const double IDLE_ZZZ = 6;   // seconds your other chef stands idle before dozing off
        void freedUp(Chef ch) { if (NET.role == null && ch != G.chefs[G.active] && ch.ai == null) G.swapPing = 2.4; }
        public void doSwap()
        {
            var humans = Enumerable.Range(0, G.chefs.Count).Where(i => G.chefs[i].ai == null).ToList();
            if (humans.Count < 2 || (NET.role != null && !G.versus)) return;
            G.active = humans[(humans.IndexOf(G.active) + 1) % humans.Count];
            // Swapping to a chef who's chopping or washing: a thumb still on the stick doesn't walk them
            // off the job. Movement comes back once the stick is let go.
            G.swapGuard = G.chefs[G.active].task != null;
            G.swapPing = 0;
            G.swapFx = 1;
            sfx("swap");
        }

        public static bool sameItems(IList<string> a, IList<string> b)
        {
            if (a.Count != b.Count) return false;
            var x = a.OrderBy(s => s, StringComparer.Ordinal).ToList(); var y = b.OrderBy(s => s, StringComparer.Ordinal).ToList();
            for (int i = 0; i < x.Count; i++) if (x[i] != y[i]) return false;
            return true;
        }

        void serve(Chef c)
        {
            var h = c.held;
            var items = h.type == "plate" ? h.items : new List<string> { ingKey(h) };
            int idx = G.orders.FindIndex(o => !o.delivery && sameItems(orderItems(o), items));
            if (idx < 0)
            {
                if (G.orders.Any(o => o.delivery && sameItems(orderItems(o), items))) { say(c, "That one's delivery: bag it"); sfx("nope"); return; }
                if (G.lv.judge) { judgeReject(c, h, "wrong"); return; }
                say(c, "Nobody ordered that"); sfx("nope"); return;
            }
            var flaw = judgeFlaw(h);
            if (flaw != null) { judgeReject(c, h, flaw); return; }
            var ord = G.orders[idx];
            double tip = U.Round(TIP_MAX * tipBoost() * (1 + (c.gear != null ? c.gear.tip : 0)) * ord.timeLeft / ord.total);
            double earned = orderReward(ord) + tip;
            G.score += earned; G.tips += tip; G.served++;
            G.orders.RemoveAt(idx);
            c.held = null;
            if (h.type == "plate") G.returns.Add(PLATE_RETURN);
            var st = c.target;
            if (tip >= 7 * tipBoost()) G.speedy++;
            addFloat("tile", st.x + 0.5, st.y + 0.2, "+" + U.S(earned), "#f5b82e", tip >= 7 * tipBoost() ? "Speedy!" : null);
            G.coinPulse = 1;
            sfx("serve"); buzz(25);
            // A burst of sparkles at the window.
            for (int i = 0; i < 7; i++) addPuff(st.x + 0.5, st.y + 0.35, i % 2 == 0 ? "#fff1a8" : "#ffffff", 0.55 + Random() * 0.25, "spark");
            if (G.lv.judge && (G.judge == null || G.judge.t <= 0 || G.judge.mood == 0) && Random() < 0.7) judgeSay("good", 0);
            if (G.orders.Count == 0) G.nextOrder = Math.Min(G.nextOrder, 1.2);
        }

        // Bagging: the food goes in a paper bag and the plate goes back to the dish pile.
        void bagUp(Chef c)
        {
            var h = c.held;
            var bag = new Item { type = "bag", items = h.type == "plate" ? h.items.ToList() : new List<string> { ingKey(h) } };
            if (h.type == "plate") G.returns.Add(PLATE_RETURN);
            c.held = bag;
            sfx("pick");
        }

        // A bag at the pickup window goes out with the driver for its delivery ticket.
        void deliver(Chef c)
        {
            var h = c.held;
            int idx = G.orders.FindIndex(o => o.delivery && sameItems(orderItems(o), h.items));
            if (idx < 0) { say(c, G.orders.Any(o => o.delivery) ? "Wrong bag for this driver" : "No delivery for that"); sfx("nope"); return; }
            var o2 = G.orders[idx];
            double tip = o2.late ? 0 : U.Round(TIP_MAX * tipBoost() * (1 + (c.gear != null ? c.gear.tip : 0)) * o2.timeLeft / o2.total);
            double earned = orderReward(o2) + tip;
            G.score += earned; G.tips += tip; G.served++;
            G.orders.RemoveAt(idx);
            c.held = null;
            var st = c.target;
            addFloat("tile", st.x + 0.5, st.y + 0.2, "+" + U.S(earned), "#f5b82e", o2.late ? "Late delivery" : tip >= 7 ? "Speedy delivery!" : "Delivered");
            G.coinPulse = 1;
            sfx("serve"); buzz(25);
            if (G.orders.Count == 0) G.nextOrder = Math.Min(G.nextOrder, 1.2);
        }

        // Floating text anchored to a kitchen tile ('tile', in tiles) or under the tickets ('hud').
        public void addFloat(string at, double x, double y, string text, string color, string sub = null)
        {
            G.floats.Add(new Float { at = at, x = x, y = y, text = text, color = color, sub = sub, life = 1.3 });
            if (NET.role == "host" && !G.versus) NET.events.Add(new object[] { "f", at, x, y, text, color, sub });
        }

        public void spawnOrder()
        {
            var opts = G.lv.recipes;
            string r = opts[(int)Math.Floor(G.rng() * opts.Length)];
            if (r == G.lastRecipe && opts.Length > 1 && G.rng() < 0.5) r = opts[(int)Math.Floor(G.rng() * opts.Length)];
            G.lastRecipe = r;
            var R = RECIPES[r];
            double total = U.Round(R.time * (G.lv.timeScale > 0 ? G.lv.timeScale : 1) * (upOn("booths") ? 1.1 : 1));
            var items = R.items.ToList();
            double reward = R.reward;
            if (R.extras != null)
            {
                // Only ask for extras this kitchen has a crate for.
                var pool = R.extras.Where(k => G.crateKinds.Contains(k.Split(':')[0])).ToList();
                int n = Math.Min(pool.Count, R.pick[0] + (int)Math.Floor(G.rng() * (R.pick[1] - R.pick[0] + 1)));
                for (int i = 0; i < n; i++) { int j = (int)Math.Floor(G.rng() * pool.Count); items.Add(pool[j]); pool.RemoveAt(j); reward += R.extraReward; }
            }
            var o = new Order { id = ++G.orderSeq, recipe = r, items = items, reward = reward, total = total, timeLeft = total };
            // Delivery tickets: bag it and leave it at the pickup window before the driver gives up.
            if (G.lv.delivery > 0 && G.tiles.Any(t => t.type == "pickup") && G.rng() < G.lv.delivery)
            {
                o.delivery = true; o.total = o.timeLeft = U.Round(total * 1.25); o.reward += DELIVERY_BONUS;
            }
            if (G.vipNext) { o.vip = true; o.reward *= 2; G.vipNext = false; }
            G.orders.Add(o);
            sfx("order");
        }

        void endLevel()
        {
            if (G.phase == "over") return;
            int won = starsFor(G.lvIdx, G.score, G.humans);
            if (G.lv.judge && !G.kicked) judgeSay(won > 0 ? "pass" : "out", won > 0 ? 0 : 2);
            G.phase = "over"; G.endT = 0;
            foreach (var c in G.chefs) { c.task = null; c.spraying = false; }
            sfx("end");
        }

        // ---- update ----
        sealed class Ctrl { public Chef chef; public double[] move; public bool grab, chop, swap, held; }

        // Who drives which chef this frame. Offline, the player drives the active chef.
        // Hosting online, the player drives chef 0 and the guests' inputs drive theirs.
        List<Ctrl> controllers(double dt)
        {
            var move = readMove();
            if (G.swapGuard) { if (move[0] == 0 && move[1] == 0) G.swapGuard = false; else move = new double[] { 0, 0 }; }
            var local = new Ctrl { chef = G.chefs[G.active], move = move, grab = input.grab, chop = input.chop, swap = input.swap, held = input.chopHeld };
            input.grab = input.chop = input.swap = false;
            if (NET.role != "host" || G.versus) return new List<Ctrl> { local }.Concat(G.chefs.Where(c => c.ai != null).Select(c => helperControl(c, dt))).ToList();
            var outL = new List<Ctrl> { local };
            foreach (var g in NET.guests)
            {
                var r = g.remote;
                if (g.slot < 0 || g.slot >= G.chefs.Count) continue;
                outL.Add(new Ctrl { chef = G.chefs[g.slot], move = new[] { r.mx, r.my }, grab = r.grab, chop = r.chop, held = r.held });
                r.grab = r.chop = false;
            }
            return outL;
        }

        public void update(double dt)
        {
            input.flashA = Math.Max(0, input.flashA - dt * 5); input.flashB = Math.Max(0, input.flashB - dt * 5); input.flashS = Math.Max(0, input.flashS - dt * 5);
            if (D != null) { if (!paused) updateDrive(dt); else engineSet(0); return; }
            if (NET.role != null) netTick(dt);
            if (G == null || paused) return;
            if (G.phase == "revive") return;   // waiting on the revive offer
            if (NET.role == "guest" && !G.versus) { updateGuest(dt); return; }
            G.swapFx = Math.Max(0, G.swapFx - dt * 2.5);
            G.swapPing = Math.Max(0, G.swapPing - dt);
            // Your other chef dozes off (zzz) after a while with nothing to do: a nudge to use both.
            if (G.phase == "play")
            {
                int humansN = G.chefs.Count(o => o.ai == null);
                for (int i = 0; i < G.chefs.Count; i++)
                {
                    var c = G.chefs[i];
                    bool napping = NET.role == null && c.ai == null && c.task == null && i != G.active && humansN > 1;
                    c.idle = napping ? c.idle + dt : 0;
                }
            }
            if (G.judge != null) G.judge.t -= dt;
            G.shake = Math.Max(0, G.shake - dt);
            G.coinPulse = Math.Max(0, G.coinPulse - dt * 3);
            updateFx(dt);

            if (G.phase == "countdown")
            {
                G.countdown -= dt;
                int n = (int)Math.Ceiling(G.countdown);
                if (n < G.lastCount && n > 0) { sfx("tick"); G.lastCount = n; }
                if (G.countdown <= 0) { G.phase = "play"; sfx("go"); }
                input.grab = input.chop = input.swap = false;
                foreach (var g in NET.guests) g.remote.grab = g.remote.chop = false;
                return;
            }
            if (G.phase == "over")
            {
                G.endT += dt;
                if (G.endT > 1.6 && !G.shown) { G.shown = true; showResults(); }
                return;
            }

            if (!G.lv.tutorial) G.time -= dt;
            G.clock += dt;
            int sec = G.lv.tutorial ? 99 : (int)Math.Ceiling(G.time);
            if (sec <= 10 && sec < G.lastSec && sec > 0) sfx("tick");
            G.lastSec = sec;
            if (G.time <= 0 && !G.lv.tutorial) { G.time = 0; if (offerRevive("time")) return; endLevel(); return; }

            var ctrls = controllers(dt);
            foreach (var ch in G.chefs) ch.moving = false;
            foreach (var k in ctrls) moveChef(k.chef, k.move[0], k.move[1], dt);
            foreach (var ch in G.chefs) ch.target = findTarget(ch);
            foreach (var k in ctrls)
            {
                if (k.swap) doSwap();
                if (k.grab) doGrab(k.chef);
                if (k.chop) doChop(k.chef);
            }

            // Chopping and washing continue on their own until done or the chef walks off.
            foreach (var ch in G.chefs)
            {
                ch.toastT = Math.Max(0, ch.toastT - dt);
                if (ch.task == null) continue;
                var b = ch.task.tile;
                if (ch.target != b || ch.held != null || b.fire > 0) { ch.task = null; continue; }
                ch.taskSfx -= dt;
                if (ch.task.kind == "chop")
                {
                    var it = b.item;
                    var into = chopResult(it);
                    if (into == null) { ch.task = null; continue; }
                    it.chop += dt / CHOP_TIME * (1 + (ch.gear != null ? ch.gear.chop : 0));
                    if (ch.taskSfx <= 0) { sfx("chop"); ch.taskSfx = 0.22; addPuff(b.x + 0.5, b.y + 0.45, "#ffffff", 0.4, "chip"); }
                    if (it.chop >= 1) { it.state = into; it.chop = 0; ch.task = null; sfx("chopped"); freedUp(ch); }
                }
                else
                {
                    if (b.dirty <= 0) { ch.task = null; continue; }
                    b.wash += dt / WASH_TIME * (1 + (ch.gear != null ? ch.gear.wash : 0));
                    if (ch.taskSfx <= 0) { sfx("splash"); ch.taskSfx = 0.3; addPuff(b.x + 0.5, b.y + 0.45, "rgba(220,240,255,.9)", 0.7, "steam"); }
                    if (b.wash >= 1)
                    {
                        b.wash = 0; b.dirty--;
                        if (G.plateTile != null) G.plateTile.count++;
                        sfx("clean");
                        if (b.dirty <= 0) { ch.task = null; freedUp(ch); }
                    }
                }
            }

            updateFire(ctrls, dt);

            // Cooking happens on stoves and griddles, and stops while they're on fire.
            foreach (var t in G.tiles)
            {
                if (t.fire > 0) continue;
                if (onHeat(t)) { cookHeat(t, dt); continue; }
                if (t.type == "blender" && t.blending)
                {
                    t.blend += dt / BLEND_TIME * (upOn("blender") ? 1.3 : 1);
                    if (t.blend >= 1)
                    {
                        var bl = Data.BLENDS.First(b => sameItems(b.needs, t.jar));
                        t.item = Item.Ing(bl.kind, bl.state);
                        t.jar = new List<string>(); t.blending = false; t.blend = 0; sfx("ding");
                    }
                    continue;
                }
                if (t.type == "coffee" && t.brewing)
                {
                    t.brew += dt / BREW_TIME * (upOn("espresso") ? 1.3 : 1);
                    if (Random() < dt * 3) addPuff(t.x + 0.5, t.y + 0.3, "rgba(255,255,255,.5)", 0.8, "steam");
                    if (t.brew >= 1) { t.brewing = false; t.brew = 0; t.item = Item.Ing("coffee"); sfx("ding"); }
                    continue;
                }
                if (t.type != "stove" || !isCookware(t.item)) continue;
                var cw = t.item; bool full = cookwareFull(cw);
                if (!full || cw.burnt) continue;
                double cook = cw.type == "pot" ? POT_COOK : PAN_COOK, burn = cw.type == "pot" ? POT_BURN : PAN_BURN;
                if (cw.cook < 1)
                {
                    cw.cook += dt / cook * (upOn("pots") ? 1.15 : 1);
                    if (Random() < dt * 3) addPuff(t.x + 0.5, t.y + 0.4, "rgba(255,255,255,.55)", 0.9, "steam");
                    if (cw.cook >= 1) { cw.cook = 1; cw.hotAt = G.clock; sfx("ding"); }
                }
                else
                {
                    cw.burn += dt / burn / (1 + cw.mitts) / (upOn("heavypots") ? 1.25 : 1);
                    cw.hotAt = G.clock;
                    warnBeep(cw, dt);
                    if (Random() < dt * 4 * cw.burn) addPuff(t.x + 0.5, t.y + 0.4, "rgba(90,90,90,.6)", 1.2, "steam");
                    if (cw.burn >= 1) { cw.burnt = true; burnUp(t); }
                }
            }
            // Live lobsters left on a counter wander off, and escape from edge counters.
            foreach (var t in G.tiles.ToList())
            {
                var it = t.item;
                if (it == null || it.type != "ing" || it.kind != "lobster" || it.state != "raw" || t.fire > 0) continue;
                it.hopT = (it.hopT ?? LOBSTER_HOP) - dt;
                if (it.hopT > 0 || it.moved == G.frame) continue;
                it.hopT = LOBSTER_HOP * (0.8 + Random() * 0.4);
                bool edge = t.x == 0 || t.y == 0 || t.x == G.cols - 1 || t.y == G.rows - 1;
                if (edge && t.type == "counter" && Random() < 0.35)
                {
                    t.item = null; sfx("fail");
                    addFloat("tile", t.x + 0.5, t.y + 0.3, "Escaped!", "#ff6b57", null);
                    continue;
                }
                var free = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Select(dd => tileAt(t.x + dd.Item1, t.y + dd.Item2)).Where(q => q != null && Data.HOP_TILES.Contains(q.type) && q.item == null && q.fire <= 0).ToList();
                if (free.Count == 0) continue;
                var to = free[(int)Math.Floor(Random() * free.Count)];
                to.item = it; t.item = null; it.moved = G.frame; sfx("hop");
                foreach (var c in G.chefs) if (c.task != null && c.task.tile == t) c.task = null;
            }
            G.frame++;

            // Burnt food keeps smoking wherever it is.
            if (Random() < dt * 3) foreach (var t in G.tiles)
                {
                    var it = t.item;
                    if ((isCookware(it) && it.burnt) || (it != null && it.type == "ing" && it.state == "burnt")) addPuff(t.x + 0.5, t.y + 0.4, "rgba(50,50,50,.55)", 1.4, "steam");
                }

            // Orders (the tutorial hands them out itself, and nobody waits too long)
            updateEvents(dt);
            if (!G.lv.tutorial) G.nextOrder -= dt;
            if (G.nextOrder <= 0)
            {
                if (G.orders.Count < maxOrders()) spawnOrder();
                double a = G.lv.interval[0] / crewFactor(G.humans), b = G.lv.interval[1] / crewFactor(G.humans);
                G.nextOrder = G.orders.Count <= 1 ? Math.Min(3, a) : a + G.rng() * (b - a);
                if (G.evt != null && G.evt.kind == "rush") G.nextOrder *= 0.45;
            }
            for (int i = G.orders.Count - 1; i >= 0; i--)
            {
                var o = G.orders[i];
                if (!G.lv.tutorial) o.timeLeft -= dt;
                if (o.timeLeft <= 0 && o.delivery && !o.late)
                {
                    // The driver pulls up and waits a few seconds. Handing the bag over now earns no tip.
                    o.late = true; o.total = o.timeLeft = DRIVER_WAIT;
                    addFloat("hud", 0, 0, "Driver waiting!", "#f59a2e");
                    sfx("order");
                    continue;
                }
                if (o.timeLeft <= 0)
                {
                    G.orders.RemoveAt(i);
                    G.failed++;
                    double lost = Math.Min(G.score, MISS_PENALTY);
                    G.score -= lost; G.missLoss += lost;
                    addFloat("hud", 0, 0, lost > 0 ? "-" + U.S(lost) : "Missed", "#ff6b57", lost > 0 ? "Missed" : null);
                    strike("late");
                    sfx("fail"); buzz(140);
                }
            }

            // Plates come back from the dining room: dirty through the hatch in kitchens with a sink.
            for (int i = G.returns.Count - 1; i >= 0; i--)
            {
                G.returns[i] -= dt;
                if (G.returns[i] > 0) continue;
                G.returns.RemoveAt(i);
                if (G.lv.dishes && G.hatch != null) G.hatch.dirty++;
                else if (G.plateTile != null) G.plateTile.count++;
            }
            if (G != null && G.tut != null) tutorialTick();
        }

        void warnBeep(Item o, double dt)
        {
            if (o.burn <= 0.45) return;
            o.warnT -= dt;
            if (o.warnT <= 0) { sfx("warn"); o.warnT = o.burn > 0.75 ? 0.25 : 0.5; }
        }

        void cookHeat(Tile t, double dt)
        {
            var it = t.item; var hf = heatFood(t, it);
            if (it.state == hf.from)
            {
                it.cook += dt / hf.secs * heatBoost(t.type);
                if (t.type == "smoker") { if (Random() < dt * 3) addPuff(t.x + 0.72, t.y - 0.05, "rgba(200,200,200,.5)", 1.4, "steam"); }
                else if (Random() < dt * 4) addPuff(t.x + 0.5, t.y + 0.4, "rgba(255,255,255,.5)", 0.7, "steam");
                if (it.cook >= 1) { it.cook = 1; it.state = "cooked"; it.hotAt = G.clock; sfx("ding"); }
            }
            else if (it.state == "cooked")
            {
                it.hotAt = G.clock;
                it.burn += dt / Data.HEAT[t.type].burn / (1 + it.mitts) / burnGuard(t.type);
                warnBeep(it, dt);
                if (Random() < dt * 4 * it.burn) addPuff(t.x + 0.5, t.y + 0.4, "rgba(90,90,90,.6)", 1.2, "steam");
                if (it.burn >= 1) { it.state = "burnt"; burnUp(t); }
            }
        }

        // Food burning: smoke, and in kitchens with fire, the counter goes up in flames.
        void burnUp(Tile t)
        {
            G.fires++;
            sfx("burn");
            strike("burn");
            for (int i = 0; i < 8; i++) addPuff(t.x + 0.5, t.y + 0.4, "rgba(40,40,40,.7)", 1.6, "steam");
            ignite(t);
        }

        static bool flammable(Tile t) => t != null && !Data.FIREPROOF.Contains(t.type);
        void ignite(Tile t)
        {
            if (!G.lv.fire || t.fire > 0 || !flammable(t)) return;
            t.fire = 1;
            t.spreadT = FIRE_SPREAD * (0.8 + Random() * 0.5);
            foreach (var c in G.chefs) if (c.task != null && c.task.tile == t) c.task = null;
            sfx("fire"); buzz(60);
        }

        void sprayPuffs(Chef a)
        {
            double nx = a.x + a.fx * 0.5, ny = a.y + a.fy * 0.5;
            for (int i = 0; i < 3; i++)
            {
                double spread = (Random() - 0.5) * 1.6;
                G.puffs.Add(new Puff { x = nx, y = ny, vx = a.fx * 4.5 - a.fy * spread, vy = a.fy * 4.5 + a.fx * spread, color = "rgba(235,245,255,.85)", life = 0.45, max = 0.45, kind = "spray" });
            }
        }

        void updateFire(List<Ctrl> ctrls, double dt)
        {
            // Spraying: a driven chef holding an extinguisher, with their button held down.
            foreach (var ch in G.chefs) ch.spraying = false;
            foreach (var k in ctrls)
            {
                var active = k.chef;
                active.spraying = active.held != null && active.held.type == "ext" && k.held;
                if (!active.spraying) continue;
                sprayPuffs(active);
                G.sprayT -= dt;
                if (G.sprayT <= 0) { sfx("spray"); G.sprayT = 0.12; }
                foreach (var t in G.tiles)
                {
                    if (t.fire <= 0) continue;
                    double dx = t.x + 0.5 - active.x, dy = t.y + 0.5 - active.y, d = U.Hypot(dx, dy);
                    if (d > SPRAY_RANGE + (active.gear != null ? active.gear.reach : 0) || (dx * active.fx + dy * active.fy) / d < 0.55) continue;
                    t.sprayed = true;
                    t.fire -= dt * 1.6 * (1 + (active.gear != null ? active.gear.spray : 0));
                    if (t.fire <= 0) { t.fire = 0; sfx("out"); for (int i = 0; i < 5; i++) addPuff(t.x + 0.5, t.y + 0.4, "rgba(200,200,200,.7)", 1, "steam"); }
                }
            }
            bool burning = false;
            foreach (var t in G.tiles)
            {
                if (t.fire <= 0) continue;
                burning = true;
                if (!t.sprayed) t.fire = Math.Min(1, t.fire + dt * 0.25);
                t.sprayed = false;
                t.spreadT -= dt;
                if (t.spreadT <= 0)
                {
                    var next = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Select(dd => tileAt(t.x + dd.Item1, t.y + dd.Item2)).Where(q => flammable(q) && q.fire <= 0).ToList();
                    if (next.Count > 0) ignite(next[(int)Math.Floor(Random() * next.Count)]);
                    t.spreadT = FIRE_SPREAD * (0.8 + Random() * 0.5);
                }
                if (Random() < dt * 5) addPuff(t.x + 0.5, t.y + 0.3, "rgba(60,60,60,.5)", 1.3, "steam");
            }
            if (burning) { G.crackleT -= dt; if (G.crackleT <= 0) { sfx("crackle"); G.crackleT = 0.15 + Random() * 0.35; } }
        }

        public void addPuff(double x, double y, string color, double life, string kind)
        {
            bool fly = kind == "chip" || kind == "spark";
            var p = new Puff { x = x + (Random() - 0.5) * 0.3, y = y, vx = (Random() - 0.5) * (kind == "spark" ? 2.4 : kind == "chip" ? 1.6 : 0.2), vy = fly ? -1 - Random() : -0.5 - Random() * 0.3, color = color, life = life, max = life, kind = kind };
            G.puffs.Add(p);
            if (NET.role == "host" && !G.versus) NET.events.Add(new object[] { "p", Math.Round(p.x, 2), Math.Round(p.y, 2), Math.Round(p.vx, 2), Math.Round(p.vy, 2), color, life, kind });
        }
        void updateFx(double dt)
        {
            for (int i = G.puffs.Count - 1; i >= 0; i--)
            {
                var p = G.puffs[i];
                p.life -= dt; p.x += p.vx * dt; p.y += p.vy * dt;
                if (p.kind == "chip") p.vy += 5 * dt;
                else if (p.kind == "spark") p.vy += 3 * dt;
                if (p.life <= 0) G.puffs.RemoveAt(i);
            }
            for (int i = G.floats.Count - 1; i >= 0; i--)
            {
                var f = G.floats[i];
                f.life -= dt; f.dy -= 28 * dt;
                if (f.life <= 0) G.floats.RemoveAt(i);
            }
        }
    }
}
