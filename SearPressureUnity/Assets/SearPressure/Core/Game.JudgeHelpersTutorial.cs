using System;
using System.Collections.Generic;
using System.Linq;

namespace SearPressure
{
    public sealed partial class Game
    {
        /* ---------- The Judge's Table: three strikes (wrong, cold, overcooked, burnt, or a customer left waiting) ---------- */

        public const double COLD_TIME = 15;   // seconds a cooked dish stays hot once it's off the heat
        public const double OVERDONE = 0.35;  // burn progress past which food counts as overcooked
        public const int MAX_STRIKES = 3;

        public void judgeSay(string kind, int mood)
        {
            var entry = J.Arr(Data.JUDGE_LINES[kind]);
            string who = entry[0] as string;
            List<string> lines;
            if (who == null)
            {
                var byWho = J.Obj(entry[1]);
                var ids = byWho.Keys.ToList();
                who = ids[(int)Math.Floor(Random() * ids.Count)];
                lines = J.Strs(byWho[who]);
            }
            else lines = J.Strs(entry[1]);
            G.judge = new JudgeState { text = lines[(int)Math.Floor(Random() * lines.Count)], t = 3.2, mood = mood, who = who };
        }

        public void strike(string kind)
        {
            if (!G.lv.judge || G.phase != "play") return;
            G.strikes++;
            G.shake = 0.4;
            sfx("strike"); buzz(160);
            if (G.strikes >= MAX_STRIKES)
            {
                if (offerRevive("strikes")) { judgeSay("out", 2); return; }
                G.kicked = true;
                judgeSay("out", 2);
                endLevel();
            }
            else judgeSay(kind, 1);
        }

        // Quality travels with a plate: when its hot parts came off the heat, and whether any were overdone.
        void platePush(Item plate, string key, Item src)
        {
            plate.items.Add(key);
            if (src == null) return;
            if (src.hotAt != null) plate.hotAt = Math.Min(plate.hotAt ?? double.PositiveInfinity, src.hotAt.Value);
            if (src.burn > OVERDONE) plate.over = true;
        }
        public bool isCold(Item x) => G.lv.judge && x != null && x.hotAt != null && G.clock - x.hotAt.Value > COLD_TIME;

        // The judge's verdict on a served plate or cup that matched an order: null means perfect.
        string judgeFlaw(Item h)
        {
            if (!G.lv.judge) return null;
            if (h.over || (h.type == "ing" && h.burn > OVERDONE)) return "overcooked";
            if (isCold(h)) return "cold";
            return null;
        }

        void judgeReject(Chef c, Item h, string kind)
        {
            c.held = null;
            if (h.type == "plate") G.returns.Add(PLATE_RETURN);
            var st = c.target;
            addFloat("tile", st.x + 0.5, st.y + 0.2, "Rejected!", "#ff6b57", kind == "wrong" ? "Wrong dish" : kind == "cold" ? "Cold" : "Overcooked");
            strike(kind);
        }

        /* ---------- AI helper chefs: solo play at the Judge's Table only ---------- */

        static readonly int[][] DIRS = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };

        // How many helpers the Judge's Table lets you hire: one per star on the kitchen before it
        // (The Big Feast), up to three. Testers with every kitchen unlocked always get at least one.
        public int helperHires(int i) => Math.Max(1, Math.Min(3, bestStars(i - 1)));

        static readonly Dictionary<char, string> STATION_OF = new Dictionary<char, string> { ['C'] = "board", ['Y'] = "fryer", ['G'] = "griddle", ['S'] = "stove", ['F'] = "stove", ['s'] = "smoker", ['o'] = "oven", ['K'] = "sink", ['Q'] = "coffee" };
        // Helpers worth offering in a kitchen: only stations it actually has.
        public List<string> helperChoices(Level lv)
        {
            var types = new HashSet<string>(string.Join("", lv.map).Where(ch => STATION_OF.ContainsKey(ch)).Select(ch => STATION_OF[ch]));
            var keys = Data.HELPERS.Where(h => types.Contains(h.station)).Select(h => h.id).ToList();
            for (int i = keys.Count - 1; i > 0; i--) { int j = (int)Math.Floor(Random() * (i + 1)); var tmp = keys[i]; keys[i] = keys[j]; keys[j] = tmp; }
            return keys.Take(3).ToList();
        }

        bool floorAt(int x, int y) { var t = tileAt(x, y); return t != null && t.type == "floor"; }
        static (int x, int y) chefCell(Chef c) => ((int)Math.Floor(c.x), (int)Math.Floor(c.y));
        // Breadth-first search from a floor cell: cell -> previous cell.
        Dictionary<int, (int x, int y)?> floodFrom((int x, int y) start)
        {
            int Key(int x, int y) => y * G.cols + x;
            var prev = new Dictionary<int, (int x, int y)?> { [Key(start.x, start.y)] = null };
            var q = new Queue<(int x, int y)>(); q.Enqueue(start);
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                foreach (var d in DIRS)
                {
                    int nx = cur.x + d[0], ny = cur.y + d[1], k = Key(nx, ny);
                    if (!prev.ContainsKey(k) && floorAt(nx, ny)) { prev[k] = cur; q.Enqueue((nx, ny)); }
                }
            }
            return prev;
        }
        // Shortest route to stand next to any of `targets`.
        public Route routeTo(Chef c, IEnumerable<Tile> targets)
        {
            var start = chefCell(c);
            var prev = floodFrom(start);
            int Key(int x, int y) => y * G.cols + x;
            Route best = null;
            foreach (var t in targets)
            {
                foreach (var d in DIRS)
                {
                    int sx = t.x + d[0], sy = t.y + d[1];
                    if (sx < 0 || sy < 0 || sx >= G.cols || sy >= G.rows || !prev.ContainsKey(Key(sx, sy))) continue;
                    var path = new List<(int x, int y)>();
                    (int x, int y)? cur = (sx, sy);
                    while (cur != null) { path.Insert(0, cur.Value); cur = prev[Key(cur.Value.x, cur.Value.y)]; }
                    if (best == null || path.Count < best.path.Count) best = new Route { tile = t, path = path.Skip(1).ToList(), face = new[] { -d[0], -d[1] } };
                }
            }
            return best;
        }

        static readonly string[] TENDERS = { "fryer", "griddle", "stove", "smoker", "oven" };
        bool finishedOn(Tile t)
        {
            if (t.fire > 0 || t.item == null) return false;
            if (t.type == "stove") return isCookware(t.item) && t.item.cook >= 1 && !t.item.burnt && cookwareFull(t.item);
            return onHeat(t) && t.item.state == "cooked";
        }

        // What this helper should do next: targets and the button to press, or null to wait.
        (List<Tile> targets, string action)? helperJob(Chef c)
        {
            string kind = c.ai.kind; var h = c.held;
            List<Tile> Tiles(string type) => G.tiles.Where(t => t.type == type && t.fire <= 0).ToList();
            if (kind == "chop")
            {
                if (h != null) return null;
                var boards = Tiles("board").Where(t => chopResult(t.item) != null).ToList();
                return boards.Count > 0 ? (boards, "chop") : ((List<Tile>, string)?)null;
            }
            if (TENDERS.Contains(kind))
            {
                if (h != null) return (G.tiles.Where(t => t.type == "counter" && t.item == null && t.fire <= 0).ToList(), "grab");
                var done = Tiles(kind).Where(finishedOn).ToList();
                return done.Count > 0 ? (done, "grab") : ((List<Tile>, string)?)null;
            }
            if (kind == "sink")
            {
                if (h != null && h.type == "dirty") return (Tiles("sink"), "grab");
                var sinks = Tiles("sink").Where(t => t.dirty > 0).ToList();
                if (h == null && sinks.Count > 0) return (sinks, "chop");
                var hatch = Tiles("hatch").Where(t => t.dirty > 0).ToList();
                return h == null && hatch.Count > 0 ? (hatch, "grab") : ((List<Tile>, string)?)null;
            }
            if (kind == "coffee")
            {
                if (h != null) return null;
                var idle = Tiles("coffee").Where(t => t.item == null && !t.brewing).ToList();
                return idle.Count > 0 ? (idle, "grab") : ((List<Tile>, string)?)null;
            }
            return null;
        }

        // The helper's "controller" for this frame: a move vector and button presses.
        Ctrl helperControl(Chef c, double dt)
        {
            var ai = c.ai; var outC = new Ctrl { chef = c, move = new double[] { 0, 0 } };
            if (G.phase != "play" || c.task != null) return outC;           // busy chopping or washing
            ai.cool -= dt; ai.age += dt;
            if (ai.plan == null || ai.age > 6)
            {
                if (ai.cool > 0) return outC;
                ai.cool = 0.3; ai.age = 0;
                var job = helperJob(c);
                ai.plan = job != null && job.Value.targets.Count > 0 ? routeTo(c, job.Value.targets) : null;
                if (ai.plan != null) ai.action = job.Value.action;
                if (ai.plan == null) return outC;
            }
            var plan = ai.plan;
            if (plan.path.Count > 0)
            {
                var wp = plan.path[0];
                double dx = wp.x + 0.5 - c.x, dy = wp.y + 0.5 - c.y, d = U.Hypot(dx, dy);
                if (d < 0.12) { plan.path.RemoveAt(0); return outC; }
                outC.move = new[] { dx / d * 0.85, dy / d * 0.85 };
                return outC;
            }
            // Arrived: face the station, then press once it's the thing we're facing.
            c.fx = plan.face[0]; c.fy = plan.face[1];
            if (c.target == plan.tile)
            {
                if (ai.action == "chop") outC.chop = true; else outC.grab = true;
                ai.plan = null; ai.cool = 0.35;
            }
            else if (ai.age > 1.5) ai.plan = null;
            return outC;
        }

        // Where a helper starts: the free floor cell closest to its stations.
        public void spawnHelper(string kind)
        {
            var hd = Data.HelperById(kind);
            var targets = G.tiles.Where(t => t.type == hd.station).ToList();
            var taken = new HashSet<int>(G.chefs.Select(c => { var p = chefCell(c); return p.y * G.cols + p.x; }));
            Tile best = null; double bestD = 1e9;
            foreach (var t in G.tiles)
            {
                if (t.type != "floor" || taken.Contains(t.y * G.cols + t.x)) continue;
                double d = targets.Count > 0 ? targets.Min(s => Math.Abs(s.x - t.x) + Math.Abs(s.y - t.y)) : 1e9;
                if (d < bestD) { bestD = d; best = t; }
            }
            if (best == null) return;
            G.chefs.Add(new Chef
            {
                x = best.x + 0.5, y = best.y + 0.5, fx = 0, fy = 1, color = Data.HELPER_COLOR[0], dark = Data.HELPER_COLOR[1],
                ai = new Ai { kind = kind, cool = 0.5 },
            });
        }

        /* ---------- Tutorial: a calm little kitchen with one instruction at a time ---------- */

        Chef tutChef() => G.chefs[G.active];
        bool tutHeld(string kind) { var h = tutChef().held; return h != null && h.type == "ing" && h.kind == kind; }
        Tile tutTile(Func<Tile, bool> pred) => G.tiles.FirstOrDefault(pred);
        List<Item> tutPlates() => G.tiles.Select(t => t.item).Concat(G.chefs.Select(c => c.held)).Where(i => i != null && i.type == "plate").ToList();
        void tutOrder(string recipe)
        {
            var R = RECIPES[recipe];
            G.orders.Add(new Order { id = ++G.orderSeq, recipe = recipe, items = R.items.ToList(), reward = R.reward, total = 60, timeLeft = 60 });
            sfx("order");
        }

        // The behaviour of each tutorial step (its text and buttons live in the data).
        void tutEnter(int i, TutState s)
        {
            switch (i)
            {
                case 0: { var c = tutChef(); s.sx = c.x; s.sy = c.y; break; }
                case 1: tutOrder("salad"); break;
                case 9: s.active = G.active; tutOrder("garden"); break;
            }
        }
        bool tutDone(int i, TutState s)
        {
            switch (i)
            {
                case 0: return U.Hypot(tutChef().x - s.sx, tutChef().y - s.sy) > 1.2;
                case 2: return tutHeld("lettuce");
                case 3: return tutTile(t => t.type == "board" && t.item != null && t.item.kind == "lettuce") != null;
                case 4: return tutTile(t => t.item != null && t.item.kind == "lettuce" && t.item.state == "chopped") != null || (tutChef().held != null && tutChef().held.state == "chopped");
                case 5: return tutPlates().Count > 0;
                case 6: return tutPlates().Any(p => p.items.Contains("lettuce:chopped"));
                case 7: return G.served >= 1;
                case 9: return G.active != s.active;
                case 10: return G.served >= 2;
            }
            return false;
        }
        Tile tutStepTile(int i)
        {
            switch (i)
            {
                case 2: return tutTile(t => t.type == "crate" && t.kind == "lettuce");
                case 3: return tutTile(t => t.type == "board" && t.item == null);
                case 4: return tutTile(t => t.type == "board" && t.item != null && t.item.kind == "lettuce");
                case 5: return tutTile(t => t.type == "plates");
                case 6: return tutTile(t => t.item != null && t.item.kind == "lettuce");
                case 7: return tutTile(t => t.type == "serve");
                case 11: return tutTile(t => t.type == "trash");
            }
            return null;
        }
        static readonly HashSet<int> TUT_HAS_DONE = new HashSet<int> { 0, 2, 3, 4, 5, 6, 7, 9, 10 };

        public void startTutorial()
        {
            paused = false; show(null);
            startLevel(TUTORIAL_IDX);
            G.phase = "play";
            tutGo(0);
        }

        void tutGo(int i)
        {
            G.tut = new TutState { i = i };
            tutEnter(i, G.tut);
            tutHidden = false;
            if (i > 0) sfx("clean");
        }

        void tutorialTick()
        {
            int i = G.tut.i;
            if (TUT_HAS_DONE.Contains(i) && tutDone(i, G.tut)) tutGo(i + 1);
        }

        public void tutNext()
        {
            if (G == null || G.tut == null) return;
            var step = Data.TUT_STEPS[G.tut.i];
            if (step.finish) { finishTutorial(); return; }
            tutGo(G.tut.i + 1);
        }

        void finishTutorial()
        {
            save.tutorialDone = true; persist();
            tutHidden = true;
            G = null; paused = false;
            syncTutorialButton();
            buildLevelList();
            show("scr-title");
        }
    }
}
