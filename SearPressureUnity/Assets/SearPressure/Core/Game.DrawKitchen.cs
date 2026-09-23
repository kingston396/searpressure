using System;
using System.Collections.Generic;
using System.Linq;

namespace SearPressure
{
    public sealed partial class Game
    {
        double kitchenDt;
        static string PAL(char c) => Data.PAL[c];

        // Tile size is chosen so every sprite pixel lands on whole device pixels.
        public (double ts, double ox, double oy) kitchenGeom()
        {
            double res = tutReserve();
            double shake = G.shake > 0 && save.shake ? (Random() - 0.5) * 10 * G.shake : 0;
            var k = res > 0 ? new Rect(L.kitchen.x, L.kitchen.y + res, L.kitchen.w, L.kitchen.h - res) : L.kitchen;
            // Room around the kitchen for the truck's body, cab, wheels and the street outside.
            var m = truckLayout().m;
            double fw = G.cols + m["l"] + m["r"], fh = G.rows + m["t"] + m["b"];
            double raw = Math.Min(Math.Min(k.w / fw, k.h / fh), 96);
            double ts = Math.Max(16, Math.Floor(raw * DPR / 16) * 16) / DPR;
            double Snap(double v) => U.Round(v * DPR) / DPR;
            return (ts, Snap(k.x + (k.w - fw * ts) / 2 + m["l"] * ts + shake), Snap(k.y + (k.h - fh * ts) / 2 + m["t"] * ts));
        }

        /* ---------- the food truck around the kitchen, and the street outside ---------- */
        const double TRUCK_WALL = 0.3;
        static readonly Dictionary<string, int[]> SIDE_N = new Dictionary<string, int[]> { ["t"] = new[] { 0, -1 }, ["b"] = new[] { 0, 1 }, ["l"] = new[] { -1, 0 }, ["r"] = new[] { 1, 0 } };
        string edgeSide(Tile t)
        {
            if (t == null) return null;
            if (t.y == 0) return "t"; if (t.y == G.rows - 1) return "b"; if (t.x == 0) return "l"; if (t.x == G.cols - 1) return "r";
            return null;
        }
        // Where the serving window, pickup window and cab are, and how much room each side needs.
        TruckLayout truckLayout()
        {
            if (G.truck != null) return G.truck;
            var serve = G.tiles.FirstOrDefault(t => t.type == "serve") ?? G.tiles.FirstOrDefault(t => t.type == "pickup");
            var pick = G.tiles.FirstOrDefault(t => t.type == "pickup");
            string ss = edgeSide(serve) ?? "b", ps = edgeSide(pick);
            var shorts = G.cols >= G.rows ? new[] { "l", "r" } : new[] { "t", "b" };
            string cab = shorts.FirstOrDefault(q => q != ss && q != ps) ?? shorts.FirstOrDefault(q => q != ss) ?? shorts[0];
            var m = new Dictionary<string, double> { ["l"] = 0.5, ["r"] = 0.5, ["t"] = 0.5, ["b"] = 0.5 };
            m[ss] += 1.35; m[cab] += 0.9;
            if (ps != null && ps != ss) m[ps] += 1;
            return G.truck = new TruckLayout { serve = serve, pick = pick, ss = ss, ps = ps, cab = cab, m = m };
        }
        // Customers queue along the outside of the truck, starting at the serving window.
        (double, double) queueSpot(double i)
        {
            var tl = truckLayout(); var s = tl.serve; var n = SIDE_N[tl.ss]; bool horiz = n[0] == 0;
            double pos = horiz ? s.x : s.y, len = horiz ? G.cols : G.rows, dir = pos + 0.5 < len / 2 ? 1 : -1;
            double outD = 0.5 + TRUCK_WALL + 0.62, step = 0.78;
            return (s.x + 0.5 + n[0] * outD + (horiz ? dir * i * step : 0), s.y + 0.5 + n[1] * outD + (horiz ? 0 : dir * i * step));
        }
        // One customer per open (dine-in) ticket: they walk up, wait, and leave happy or cross.
        void updateCrowd(double dt)
        {
            if (G.crowd == null) G.crowd = new Dictionary<int, Person>();
            if (G.leavers == null) G.leavers = new List<Person>();
            bool angry = G.failed > (G.lastFailed ?? G.failed); G.lastFailed = G.failed;
            var queue = G.orders.Where(o => !o.delivery).ToList();
            var ids = new HashSet<int>(queue.Select(o => o.id));
            foreach (var id in G.crowd.Keys.ToList())
                if (!ids.Contains(id)) { var c = G.crowd[id]; G.crowd.Remove(id); c.mood = angry ? "angry" : "happy"; c.life = 1.8; G.leavers.Add(c); }
            for (int i = 0; i < queue.Count; i++)
            {
                var o = queue[i];
                var (tx, ty) = queueSpot(i);
                if (!G.crowd.TryGetValue(o.id, out var c))
                {
                    var (sx, sy) = queueSpot(queue.Count + 3);
                    c = new Person { x = sx, y = sy, shirt = Data.SHIRTS[o.id % Data.SHIRTS.Length], hair = Data.HAIRS[(o.id * 7) % Data.HAIRS.Length] };
                    G.crowd[o.id] = c;
                }
                c.o = o;
                double dx = tx - c.x, dy = ty - c.y, d = U.Hypot(dx, dy);
                c.moving = d > 0.02;
                if (c.moving) { double st = Math.Min(d, 2.6 * dt); c.x += dx / d * st; c.y += dy / d * st; c.walk += dt; }
            }
            // Leavers walk off along the street, away from the window.
            var (fx, fy) = queueSpot(1); var (ax, ay) = queueSpot(0);
            double ux = fx - ax, uy = fy - ay, ul = U.Hypot(ux, uy); if (ul == 0) ul = 1;
            foreach (var l in G.leavers) { l.life -= dt; l.x += ux / ul * 2.2 * dt; l.y += uy / ul * 2.2 * dt; l.walk += dt; l.moving = true; }
            G.leavers.RemoveAll(l => l.life <= 0);
        }
        void drawPerson(Person c, double alpha = 1)
        {
            double bob = c.moving ? (Math.Floor(c.walk * 8) % 2) * P : 0;
            X.globalAlpha = alpha;
            X.fillStyle = "rgba(0,0,0,.22)"; X.fillRect(c.x - 3 * P, c.y + 0.12, 6 * P, 1.5 * P);
            spr("person", c.x, c.y - 0.22 - bob, P * 0.95, S("1", c.shirt, "B", c.hair));
            X.globalAlpha = 1;
        }
        // The street, the truck's body, cab and wheels (under the kitchen tiles).
        void drawTruck()
        {
            var tl = truckLayout(); var m = tl.m; int C = G.cols, R = G.rows; double WL = TRUCK_WALL;
            double gx = -m["l"], gy = -m["t"], gw = C + m["l"] + m["r"], gh = R + m["t"] + m["b"];
            int stop = G.lv.stop;
            string ground = stop >= 0 && stop < Data.STOP_GROUND.Length ? Data.STOP_GROUND[stop] : Data.STOP_GROUND[0];
            X.fillStyle = PAL('k'); X.fillRect(gx - P * 2, gy - P * 2, gw + P * 4, gh + P * 4);
            X.fillStyle = ground; X.fillRect(gx, gy, gw, gh);
            var r = Rng.Mulberry32(stop * 7 + 3);
            X.fillStyle = Css.Mix(ground, "#000000", 0.14);
            for (int i = 0; i < gw * gh * 2.5; i++) X.fillRect(gx + r() * gw, gy + r() * gh, P * 2, P);
            X.fillStyle = Css.Mix(ground, "#ffffff", 0.12);
            for (int i = 0; i < gw * gh; i++) X.fillRect(gx + r() * gw, gy + r() * gh, P, P);
            double bx = -WL, by = -WL, bw = C + WL * 2, bh = R + WL * 2;
            // Sidewalk with a curb along the serving side, where the line forms.
            if (tl.serve != null)
            {
                string side = tl.ss; double d = m[side] - 0.15; string walk = "#c9c3b4";
                double[] rect = side == "l" ? new[] { gx, gy, d, gh } : side == "r" ? new[] { C + m["r"] - d, gy, d, gh } : side == "t" ? new[] { gx, gy, gw, d } : new[] { gx, R + m["b"] - d, gw, d };
                X.fillStyle = walk; X.fillRect(rect[0], rect[1], rect[2], rect[3]);
                X.fillStyle = Css.Mix(walk, "#000000", 0.12);
                double rx = rect[0], ry = rect[1], rw = rect[2], rh = rect[3]; bool horiz = side == "t" || side == "b";
                for (double k = 0; k < (horiz ? rw : rh); k += 1) { if (horiz) X.fillRect(rx + k, ry, P, rh); else X.fillRect(rx, ry + k, rw, P); }
                X.fillStyle = "#8a8f9c";
                if (side == "l") X.fillRect(rx + rw - 2 * P, ry, 2 * P, rh);
                if (side == "r") X.fillRect(rx, ry, 2 * P, rh);
                if (side == "t") X.fillRect(rx, ry + rh - 2 * P, rw, 2 * P);
                if (side == "b") X.fillRect(rx, ry, rw, 2 * P);
            }
            // Cab at one short end.
            double cl = 0.85, inset = 0.35;
            double[] cab = tl.cab == "l" ? new[] { bx - cl, by + inset, cl + 0.05, bh - inset * 2 } : tl.cab == "r" ? new[] { bx + bw - 0.05, by + inset, cl + 0.05, bh - inset * 2 } : tl.cab == "t" ? new[] { bx + inset, by - cl, bw - inset * 2, cl + 0.05 } : new[] { bx + inset, by + bh - 0.05, bw - inset * 2, cl + 0.05 };
            // Wheels on the long sides (drawn first so the body sits on them).
            bool longH = tl.cab == "l" || tl.cab == "r";
            void Wheel(double x, double y, double w, double h) { X.fillStyle = PAL('k'); X.fillRect(x, y, w, h); X.fillStyle = "#3a3d48"; X.fillRect(x + P, y + P, w - 2 * P, h - 2 * P); X.fillStyle = "#8a8f9c"; X.fillRect(x + w / 2 - 2 * P, y + h / 2 - 2 * P, 4 * P, 4 * P); }
            foreach (double f in new[] { 0.14, 0.8 })
            {
                if (longH) { double x = bx + bw * f - 0.45; Wheel(x, by - 0.22, 0.9, 0.34); Wheel(x, by + bh - 0.12, 0.9, 0.34); }
                else { double y = by + bh * f - 0.45; Wheel(bx - 0.22, y, 0.34, 0.9); Wheel(bx + bw - 0.12, y, 0.34, 0.9); }
            }
            X.fillStyle = "rgba(0,0,0,.3)"; X.fillRect(bx + 0.12, by + 0.16, bw, bh);
            // Cab: body colour, windshield, headlights and bumper.
            X.fillStyle = PAL('k'); X.fillRect(cab[0] - P, cab[1] - P, cab[2] + 2 * P, cab[3] + 2 * P);
            X.fillStyle = "#e8472f"; X.fillRect(cab[0], cab[1], cab[2], cab[3]);
            double cx = cab[0], cy = cab[1], cw = cab[2], ch = cab[3];
            X.fillStyle = PAL('k');
            if (longH)
            {
                double wx = tl.cab == "l" ? cx + 0.12 : cx + cw - 0.52;
                X.fillRect(wx - P, cy + 0.15 - P, 0.4 + 2 * P, ch - 0.3 + 2 * P);
                X.fillStyle = "#6fb0d6"; X.fillRect(wx, cy + 0.15, 0.4, ch - 0.3);
                X.fillStyle = "#dff1fb"; X.fillRect(wx + 3 * P, cy + 0.25, 2 * P, ch * 0.3);
                double hx = tl.cab == "l" ? cx - 2 * P : cx + cw - 2 * P;
                X.fillStyle = "#f5d33e"; X.fillRect(hx, cy + 0.1, 4 * P, 5 * P); X.fillRect(hx, cy + ch - 0.1 - 5 * P, 4 * P, 5 * P);
            }
            else
            {
                double wy = tl.cab == "t" ? cy + 0.12 : cy + ch - 0.52;
                X.fillRect(cx + 0.15 - P, wy - P, cw - 0.3 + 2 * P, 0.4 + 2 * P);
                X.fillStyle = "#6fb0d6"; X.fillRect(cx + 0.15, wy, cw - 0.3, 0.4);
                X.fillStyle = "#dff1fb"; X.fillRect(cx + 0.25, wy + 3 * P, cw * 0.3, 2 * P);
                double hy = tl.cab == "t" ? cy - 2 * P : cy + ch - 2 * P;
                X.fillStyle = "#f5d33e"; X.fillRect(cx + 0.1, hy, 5 * P, 4 * P); X.fillRect(cx + cw - 0.1 - 5 * P, hy, 5 * P, 4 * P);
            }
            // Body: outline, cream paint and a red stripe (the kitchen tiles cover the middle).
            X.fillStyle = PAL('k'); X.fillRect(bx - P, by - P, bw + 2 * P, bh + 2 * P);
            X.fillStyle = "#fff5de"; X.fillRect(bx, by, bw, bh);
            X.fillStyle = "#e8472f"; X.fillRect(bx + 0.1, by + 0.1, bw - 0.2, bh - 0.2);
            X.fillStyle = "#a8301d"; X.fillRect(bx + 0.2, by + 0.2, bw - 0.4, bh - 0.4);
            X.fillStyle = "#fff5de";
            for (int i = 1; i < C; i++) { X.fillRect(i - P / 2, by + 0.03, P, P); X.fillRect(i - P / 2, by + bh - 0.03 - P, P, P); }
            for (int j = 1; j < R; j++) { X.fillRect(bx + 0.03, j - P / 2, P, P); X.fillRect(bx + bw - 0.03 - P, j - P / 2, P, P); }
        }
        // The Street Performer upgrade: a busker playing to the line.
        void drawBusker()
        {
            var tl = truckLayout(); if (tl.serve == null || tl.serve.type != "serve" || !upOn("booths")) return;
            var (x, y) = queueSpot(6.3); double bob = Math.Floor(T * 4) % 2 != 0 ? P : 0;
            drawPerson(new Person { x = x, y = y, shirt = "#9a6ab3", hair = "#5a3620" });
            X.fillStyle = "#8a5a36"; X.fillRect(x - 0.2, y - 0.2, 0.26, 0.14); X.fillRect(x + 0.04, y - 0.18, 0.18, 2 * P);   // guitar
            X.fillStyle = PAL('k'); double nx = x + 0.28, ny = y - 0.75 - bob;
            X.fillRect(nx, ny, P, 5 * P); X.fillRect(nx - 2 * P, ny + 4 * P, 3 * P, 2 * P); X.fillRect(nx + P, ny, 2 * P, P);   // music note
        }
        // A chalkboard menu sign on the sidewalk at the back of the line.
        void drawMenuBoard()
        {
            var tl = truckLayout(); if (tl.serve == null || tl.serve.type != "serve") return;
            var (x, y) = queueSpot(5.2);
            X.fillStyle = "rgba(0,0,0,.25)"; X.fillRect(x - 0.28, y + 0.2, 0.62, 0.12);
            X.fillStyle = PAL('k'); X.fillRect(x - 0.3, y - 0.42, 0.6, 0.66);
            X.fillStyle = "#8a5a36"; X.fillRect(x - 0.3 + P, y - 0.42 + P, 0.6 - 2 * P, 0.66 - 2 * P);
            X.fillStyle = "#2e3a34"; X.fillRect(x - 0.3 + 2 * P, y - 0.42 + 2 * P, 0.6 - 4 * P, 0.5 - 2 * P);
            X.fillStyle = "#fff5de";
            for (int i = 0; i < 3; i++) X.fillRect(x - 0.18, y - 0.3 + i * 0.13, 0.22 - (i % 2) * 0.08, P);
            X.fillStyle = "#f5d33e"; for (int i = 0; i < 3; i++) X.fillRect(x + 0.1, y - 0.3 + i * 0.13, 0.08, P);
        }
        // Awnings over the windows, the queue and the delivery drivers (over the kitchen tiles).
        void drawTruckOutside(double dt)
        {
            var tl = truckLayout();
            void Awning(Tile t, string side, string c1, string c2)
            {
                if (t == null || side == null) return;
                var n = SIDE_N[side]; bool horiz = n[0] == 0; double depth = 0.5;
                double ex = t.x + 0.5 + n[0] * (0.5 + TRUCK_WALL), ey = t.y + 0.5 + n[1] * (0.5 + TRUCK_WALL);
                double len = 2.2, x0 = horiz ? ex - len / 2 : (n[0] < 0 ? ex - depth : ex), y0 = horiz ? (n[1] < 0 ? ey - depth : ey) : ey - len / 2;
                double w = horiz ? len : depth, h = horiz ? depth : len;
                X.fillStyle = "rgba(0,0,0,.25)"; X.fillRect(x0 + n[0] * 0.08 + 0.05, y0 + n[1] * 0.08 + 0.05, w, h);
                X.fillStyle = PAL('k'); X.fillRect(x0 - P, y0 - P, w + 2 * P, h + 2 * P);
                int cnt = 6;
                for (int i = 0; i < cnt; i++) { X.fillStyle = i % 2 != 0 ? c2 : c1; if (horiz) X.fillRect(x0 + i * len / cnt, y0, len / cnt, h); else X.fillRect(x0, y0 + i * len / cnt, w, len / cnt); }
                // Scalloped outer edge.
                for (int i = 0; i < cnt; i++)
                {
                    X.fillStyle = i % 2 != 0 ? c2 : c1;
                    double a = i * len / cnt + len / cnt * 0.2, b = len / cnt * 0.6, lip = 3 * P;
                    if (n[0] < 0) X.fillRect(x0 - lip, y0 + a, lip, b); if (n[0] > 0) X.fillRect(x0 + w, y0 + a, lip, b);
                    if (n[1] < 0) X.fillRect(x0 + a, y0 - lip, b, lip); if (n[1] > 0) X.fillRect(x0 + a, y0 + h, b, lip);
                }
            }
            drawMenuBoard();
            drawBusker();
            if (tl.serve != null && tl.serve.type == "serve") Awning(tl.serve, tl.ss, "#e8472f", "#fff5de");
            Awning(tl.pick, tl.ps, "#3a86d4", "#fff5de");
            updateCrowd(dt);
            // Queue: front of the line last, so they're on top.
            foreach (var c in G.crowd.Values.OrderBy(a => a.y).ToList())
            {
                drawPerson(c);
                var o = c.o; double frac = o.timeLeft / o.total;
                if (o.vip) spr("coin", c.x, c.y - 0.78, P * 0.7);
                else if (frac < 0.3 && Math.Sin(T * 10) > -0.2) spr("bang", c.x + 0.22, c.y - 0.7, P);
            }
            foreach (var l in G.leavers)
            {
                drawPerson(l, Math.Min(1, l.life));
                spr(l.mood == "happy" ? "heart" : "bang", l.x, l.y - 0.72 - (1.8 - l.life) * 0.2, P * (l.mood == "happy" ? 1.2 : 1));
            }
            // A driver waits on a scooter outside the pickup window while a delivery is overdue.
            if (tl.pick != null && tl.ps != null && G.orders.Any(o => o.late))
            {
                var n = SIDE_N[tl.ps];
                spr("scooter", tl.pick.x + 0.5 + n[0] * (1.2 + TRUCK_WALL), tl.pick.y + 0.5 + n[1] * (1.2 + TRUCK_WALL) + (Math.Floor(T * 8) % 2 != 0 ? P : 0), P);
            }
        }

        // Drifting ingredients behind the menus.
        void drawBackdrop()
        {
            double s = 72;
            var kinds = new[] { ("tomato", "raw"), ("lettuce", "raw"), ("onion", "raw"), ("bun", "raw"), ("egg", "raw"), ("batter", "cooked"), ("bacon", "cooked"), ("coffee", "brewed"), ("potato", "cooked"), ("shake", "strawberry") };
            X.globalAlpha = 0.18;
            double off = (T * 12) % s;
            for (int y = -1; y < H / s + 1; y++) for (int x = -1; x < W / s + 1; x++)
                {
                    var k = kinds[Math.Abs((x * 7 + y * 3) % kinds.Length)];
                    drawIng(k.Item1, k.Item2, x * s + off + (y % 2) * s / 2, y * s + off, 3);
                }
            X.globalAlpha = 1;
        }

        static readonly HashSet<string> STEEL = new HashSet<string> { "pickup", "stove", "serve", "trash", "griddle", "sink", "hatch", "coffee", "fryer", "blender", "smoker", "oven" };

        void drawTile(Tile t)
        {
            int x = t.x, y = t.y;
            if (t.type == "floor") { sprTL((x + y) % 2 != 0 ? "floorA" : "floorB", x, y, P); return; }
            sprTL(STEEL.Contains(t.type) ? "steel" : "wood", x, y, P);
            bool flick = Math.Floor(T * 8) % 2 != 0;
            switch (t.type)
            {
                case "crate":
                    sprTL("crate", x, y, P);
                    if (t.item == null) drawIng(t.kind, "raw", x + 0.5, y + 0.4, P);
                    if (t.@out > 0)
                    {   // out of stock
                        X.fillStyle = "rgba(28,33,51,.6)"; X.fillRect(x, y, 1, 1);
                        X.fillStyle = "#e8472f";
                        for (int k = 2; k < 14; k++) { X.fillRect(x + k * P, y + k * P, 2 * P, 2 * P); X.fillRect(x + (15 - k) * P, y + k * P, 2 * P, 2 * P); }
                    }
                    break;
                case "board":
                    sprTL("board", x, y, P);
                    if (t.item == null) spr("knife", x + 0.5, y + 0.4, P);
                    break;
                case "stove":
                    {
                        bool hot = isCookware(t.item) && !t.item.burnt && t.fire <= 0 && cookwareFull(t.item);
                        sprTL("burner", x, y, P, hot ? S("1", flick ? PAL('o') : PAL('r'), "2", PAL('R')) : S("1", PAL('d'), "2", PAL('D')));
                        break;
                    }
                case "griddle": { bool hot = onHeat(t) && t.item.state != "burnt" && t.fire <= 0; sprTL("griddle", x, y, P, S("1", hot ? (flick ? PAL('R') : PAL('r')) : PAL('d'))); break; }
                case "fryer": { bool hot = onHeat(t) && t.item.state != "burnt" && t.fire <= 0; sprTL("fryer", x, y, P, S("1", hot ? (flick ? PAL('o') : PAL('y')) : "#b8862a")); break; }
                case "oven": { bool hot = onHeat(t) && t.item.state != "burnt" && t.fire <= 0; sprTL("oven", x, y, P, hot ? S("1", flick ? PAL('o') : PAL('r'), "2", PAL('y')) : S("1", "#2a2a30", "2", "#3a3a44")); break; }
                case "smoker": { bool hot = onHeat(t) && t.item.state != "burnt" && t.fire <= 0; sprTL("smoker", x, y, P, S("1", hot ? (flick ? PAL('o') : PAL('r')) : "#5a3a2a")); break; }
                case "blender":
                    {
                        var has = t.jar ?? new List<string>();
                        string col = has.Contains("banana") ? (has.Contains("strawberry") ? "#c05a9a" : "#f5e08a") : has.Contains("strawberry") ? "#f4a3b8" : has.Contains("icecream") ? "#fff1c8" : PAL('g');
                        sprTL("blender", x, y, P, S("1", col, "2", t.blending && flick ? PAL('w') : col));
                        break;
                    }
                case "serve": sprTL("bell", x, y, P); break;
                case "bagger": sprTL("bagStack", x, y, P); break;
                case "pickup": sprTL("pickup", x, y, P); break;
                case "trash": sprTL("bin", x, y, P); break;
                case "plates":
                    {
                        int n = Math.Min(t.count, 5);
                        if (n == 0) { X.globalAlpha = 0.3; spr("plate", x + 0.5, y + 0.42, P); X.globalAlpha = 1; }
                        for (int i = 0; i < n; i++) spr("plate", x + 0.5, y + 0.45 - i * P * 1.5, P);
                        break;
                    }
                case "sink": sprTL("sink", x, y, P); if (t.dirty > 0) drawDirty(t.dirty, x + 0.5, y + 0.44, P); break;
                case "hatch": sprTL("hatch", x, y, P); if (t.dirty > 0) drawDirty(t.dirty, x + 0.5, y + 0.42, P); break;
                case "coffee":
                    sprTL("coffeeMachine", x, y, P, S("1", t.brewing ? (flick ? PAL('l') : PAL('n')) : PAL('r')));
                    if (t.brewing) { X.fillStyle = PAL('B'); X.fillRect(x + 7 * P, y + 8 * P, P * 2, P * 2); }
                    break;
            }
        }

        void drawFire(Tile t)
        {
            double k = 0.5 + t.fire * 0.5;
            X.fillStyle = "rgba(255,120,40,.22)"; X.fillRect(t.x - P * 2, t.y - P * 4, 1 + P * 4, 1 + P * 4);
            X.globalAlpha = Math.Min(1, 0.4 + t.fire);
            var c = sprite("fire" + (int)(Math.Floor(T * 10 + t.x) % 3));
            double h = U.Round(16 * k) * P;
            X.drawImage(c, t.x, t.y + 0.9 - h, 1, h);
            X.globalAlpha = 1;
        }

        void drawBar(double x, double y, double frac, string color)
        {
            double w = 12 * P;
            X.fillStyle = PAL('k'); X.fillRect(x - w / 2 - P, y - 2 * P, w + 2 * P, 4 * P);
            X.fillStyle = "#1c2133"; X.fillRect(x - w / 2, y - P, w, 2 * P);
            X.fillStyle = color; X.fillRect(x - w / 2, y - P, Math.Max(P, U.Round(w * Math.Min(1, frac) / P) * P), 2 * P);
        }

        void drawTileOverlay(Tile t)
        {
            double cx = t.x + 0.5, top = t.y;
            if (t.type == "coffee" && t.brewing) drawBar(cx, top + 0.06, t.brew, "#c07a45");
            if (t.type == "sink" && t.wash > 0) drawBar(cx, top + 0.06, t.wash, PAL('u'));
            if (t.type == "blender" && t.blending) drawBar(cx, top + 0.06, t.blend, "#f4a3b8");
            var it = t.item;
            if (it == null) return;
            if (it.type == "ing" && it.chop > 0 && chopResult(it) != null) drawBar(cx, top + 0.06, it.chop, PAL('l'));
            // Griddle food shares the cookware progress/warning/done markers.
            double? hc = null, hb = null;
            if (onHeat(t) && it.state != "burnt") { hc = it.state == "cooked" ? 1 : it.cook; hb = it.burn; }
            else if (isCookware(it) && t.type == "stove" && !it.burnt) { hc = it.cook; hb = it.burn; }
            if (hc != null)
            {
                if (hc > 0 && hc < 1) drawBar(cx, top + 0.06, hc.Value, PAL('l'));
                else if (hc >= 1 && hb > 0.3) { if (Math.Sin(T * (hb > 0.75 ? 22 : 12)) > 0) spr("warn", cx, top - 0.02, P); }
                else if (hc >= 1) spr("check", cx + 0.34, top + 0.08, P);
            }
            // Pot contents bubble while it's still being filled.
            int need = it.type == "pot" ? (potTarget(it)?.needs.Length ?? 3) : 0;
            if (it.type == "pot" && !it.burnt && it.cook < 1 && it.items.Count > 0 && it.items.Count < need)
            {
                X.fillStyle = PAL('k'); X.fillRect(cx - 7 * P, top - 5 * P, 14 * P, 6 * P);
                X.fillStyle = "#fff5de"; X.fillRect(cx - 6 * P, top - 4 * P, 12 * P, 4 * P);
                for (int i = 0; i < need; i++)
                {
                    double sx = cx - 4 * P + i * 4 * P;
                    if (i < it.items.Count) drawIng(it.items[i].Split(':')[0], "raw", sx, top - 2 * P, P * 0.33);
                    else { X.fillStyle = PAL('q'); X.fillRect(sx - P / 2, top - 2.5 * P, P, P); }
                }
            }
        }

        static string chefDir(Chef c) => Math.Abs(c.fx) > Math.Abs(c.fy) ? (c.fx > 0 ? "R" : "L") : (c.fy > 0 ? "D" : "U");

        void drawChef(Chef c, int idx)
        {
            string dir = chefDir(c);
            int frame = c.moving ? (int)(Math.Floor(c.walk * 9) % 2) : 0;
            int busy = c.task != null ? (int)(Math.Floor(T * 10) % 2) : 0;
            string name = (dir == "D" ? "chefDown" : dir == "U" ? "chefUp" : "chefSide") + (frame != 0 ? "1" : "");
            double cy = c.y - 0.34 - busy * P;
            X.fillStyle = "rgba(0,0,0,.25)"; X.fillRect(c.x - 5 * P, c.y + 0.1, 10 * P, 2 * P);
            double[] heldAt = dir == "D" ? new[] { 0, 0.02 } : dir == "U" ? new[] { 0, -0.62 } : dir == "R" ? new[] { 0.4, -0.24 } : new[] { -0.4, -0.24 };
            if (c.held != null && dir == "U") drawItem(c.held, c.x + heldAt[0], c.y + heldAt[1], P);
            if (c.ai != null) spr(chefSprite(name, "toque"), c.x, cy, P, outfitSwaps(outfitOf("classic"), c.color, c.dark), dir == "L");
            else { var fit = outfitOf(c.fit); spr(chefSprite(name, fit.hat), c.x, cy, P, outfitSwaps(fit, c.color, c.dark), dir == "L"); }
            if (c.held != null && dir != "U") drawItem(c.held, c.x + heldAt[0], c.y + heldAt[1], P);
            if (c.task != null && c.task.kind == "chop")
            {
                var b = c.task.tile;
                spr("knife", b.x + 0.6, b.y + 0.3 - (Math.Floor(T * 12) % 2) * 3 * P, P);
            }
            if (c.ai != null)
            {
                var ic = Data.HelperById(c.ai.kind).icon;
                X.fillStyle = PAL('k'); X.fillRect(c.x - 5 * P, cy - 0.78, 10 * P, 10 * P);
                X.fillStyle = "#e9e0f8"; X.fillRect(c.x - 4 * P, cy - 0.78 + P, 8 * P, 8 * P);
                spr(ic.name, c.x, cy - 0.78 + 5 * P, P * 0.55, ic.sw);
            }
            if (c.idle > IDLE_ZZZ)
            {
                // Three Zs drift up and fade, one after another.
                for (int k = 0; k < 3; k++)
                {
                    double f = ((c.idle - IDLE_ZZZ) * 0.6 + k / 3.0) % 1, s = P;
                    double zx = c.x + 0.25 + f * 0.45 + Math.Sin(f * 6) * 0.06, zy = cy - 0.62 - f * 1.1;
                    X.globalAlpha = Math.Min(1, (1 - f) * 2) * Math.Min(1, (c.idle - IDLE_ZZZ) * 2);
                    spr("zz", zx, zy, s);
                }
                X.globalAlpha = 1;
            }
            if (idx == G.active)
            {
                double bob = (Math.Floor(T * 3) % 2) * P + G.swapFx * 0.2;
                spr("arrow", c.x, cy - 0.66 - bob, P, S("1", c.color));
            }
        }

        void drawKitchen()
        {
            var (ts, ox, oy) = kitchenGeom();
            X.save(); X.translate(ox, oy); X.scale(ts, ts);
            drawTruck();
            foreach (var t in G.tiles) drawTile(t);
            // Target brackets for the active chef
            var act = G.chefs[G.active];
            if (act.target != null && G.phase == "play")
            {
                var t = act.target; double a = Math.Floor(T * 4) % 2 != 0 ? 1 : 0.6;
                X.fillStyle = "rgba(255,255,255," + U.S(a) + ")";
                foreach (var q in new[] { new[] { 0, 0, 1, 1 }, new[] { 1, 0, -1, 1 }, new[] { 0, 1, 1, -1 }, new[] { 1, 1, -1, -1 } })
                {
                    double bx = t.x + q[0] - (q[2] < 0 ? P : 0), by = t.y + q[1] - (q[3] < 0 ? P : 0);
                    X.fillRect(bx, by, 4 * P * q[2], P); X.fillRect(bx, by, P, 4 * P * q[3]);
                }
            }
            if (G.tut != null) drawTutorialTile();
            foreach (var t in G.tiles) if (t.item != null) drawItem(t.item, t.x + 0.5, t.y + (t.type == "coffee" ? 0.62 : t.type == "blender" ? 0.5 : 0.4), P);
            var order = Enumerable.Range(0, G.chefs.Count).OrderBy(i => G.chefs[i].y).ToList();
            foreach (var i in order) drawChef(G.chefs[i], i);
            drawTruckOutside(kitchenDt);
            // Frost on anything that's gone cold (only the judge cares).
            if (G.lv.judge)
            {
                foreach (var t in G.tiles) if (isCold(t.item)) spr("frost", t.x + 0.82, t.y + 0.12, P);
                foreach (var c in G.chefs) if (isCold(c.held)) spr("frost", c.x + 0.3, c.y - 0.7, P);
            }
            foreach (var t in G.tiles) drawTileOverlay(t);
            foreach (var t in G.tiles) if (t.fire > 0) drawFire(t);
            foreach (var p in G.puffs)
            {
                double k = p.life / p.max;
                X.globalAlpha = Math.Max(0, k);
                double s = p.kind == "chip" ? P : U.Round(2 + (1 - k) * 3) * P;
                X.fillStyle = p.color; X.fillRect(U.Round(p.x / P) * P - s / 2, U.Round(p.y / P) * P - s / 2, s, s);
            }
            X.globalAlpha = 1;
            // Chef speech (toasts)
            X.textAlign = "center"; X.textBaseline = "middle";
            foreach (var c in G.chefs)
            {
                if (c.toastT <= 0) continue;
                X.globalAlpha = Math.Min(1, c.toastT * 3);
                X.font = "600 0.3px \"Pixelify Sans\", Nunito, sans-serif";
                double w = X.measureText(c.toast).width + 0.3;
                X.fillStyle = PAL('k'); X.fillRect(c.x - w / 2 - P, c.y - 1.2 - P, w + 2 * P, 0.42 + 2 * P);
                X.fillStyle = "#fff5de"; X.fillRect(c.x - w / 2, c.y - 1.2, w, 0.42);
                X.fillStyle = "#2b2230"; X.fillText(c.toast, c.x, c.y - 0.98);
                X.globalAlpha = 1;
            }
            X.restore();
        }

        // The icon row on a ticket (as ingredient keys). Pot dishes are spelled out step by step
        // on single-dish tickets; on bigger plates they show as the finished side to stay readable.
        public static List<string> recipeIngredients(IList<string> items)
        {
            var outL = new List<string>();
            string Raw(string k) => k.Split(':')[0] + ":raw";
            foreach (var k in items)
            {
                string kind = k.Split(':')[0];
                if (kind == "coffee") continue;
                var bl = Data.BLENDS.FirstOrDefault(b => b.kind + ":" + b.state == k);
                if (bl != null) { outL.AddRange(bl.needs.Select(n => n + ":raw")); continue; }
                if (Data.BUILD_ICONS.TryGetValue(k, out var bi)) { outL.AddRange(bi.Select(n => n + ":raw")); continue; }
                var pr = Data.POT_RECIPES.FirstOrDefault(r => r.result == k);
                if (pr != null && pr.needs.Any(n => n.Split(':')[0] != kind))
                {
                    if (items.Count == 1) outL.AddRange(pr.needs.Select(Raw)); else outL.Add(k);
                    continue;
                }
                outL.Add(Raw(k));
            }
            return outL;
        }

        public static string itemName(string k)
        {
            if (Data.ITEM_NAMES.TryGetValue(k, out var n)) return n;
            var p = k.Split(':'); string kind = p[0], state = p.Length > 1 ? p[1] : "raw";
            string name = Data.KIND_NAMES.TryGetValue(kind, out var kn) ? kn : kind;
            string s = state == "raw" ? name : state + " " + name;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
        // Tap a ticket to see it big, with every item named. Tap again (or wait) to close.
        Order ticketAt(double x, double y)
        {
            var R = L.orders;
            if (G == null || y < R.y - 4 || y > R.y + R.h + 4) return null;
            int n = maxOrders(); double gap = 6, cw = Math.Min(122, (R.w - gap * (n - 1)) / n);
            return G.orders.FirstOrDefault(o => o.x != null && x >= o.x - 3 && x <= o.x + cw + 3);
        }
        void drawZoom(double dt)
        {
            var z = G.zoom;
            if (z == null) return;
            z.t -= dt;
            var o = G.orders.FirstOrDefault(q => q.id == z.id);
            if (o == null || z.t <= 0) { G.zoom = null; return; }
            var items = orderItems(o); var R = RECIPES[o.recipe];
            double w = Math.Min(W - 32, 340), rowH = 40, h = 96 + items.Count * rowH + (o.delivery ? 26 : 0);
            double x = U.Round(W / 2 - w / 2), y = U.Round(L.orders.y + L.orders.h + 12);
            X.globalAlpha = Math.Min(1, z.t * 4);
            X.fillStyle = "rgba(0,0,0,.4)"; X.fillRect(x + 4, y + 4, w, h);
            X.fillStyle = PAL('k'); X.fillRect(x - 3, y - 3, w + 6, h + 6);
            X.fillStyle = "#fff5de"; X.fillRect(x, y, w, h);
            double frac = o.timeLeft / o.total;
            X.fillStyle = "#e8dcbc"; X.fillRect(x + 12, y + 12, w - 24, 8);
            X.fillStyle = frac > 0.5 ? "#3f9b45" : frac > 0.25 ? "#f5b82e" : "#e8472f"; X.fillRect(x + 12, y + 12, Math.Max(3, (w - 24) * frac), 8);
            if (R.mug) drawKey(items[0], x + 40, y + 56, 4); else drawPlate(items, x + 40, y + 56, 4);
            X.fillStyle = "#2b2230"; X.textAlign = "left"; X.textBaseline = "middle";
            X.font = "700 22px \"Pixelify Sans\", \"Courier New\", monospace"; X.fillText(R.name, x + 80, y + 46);
            X.font = "700 13px \"Nunito\", system-ui, sans-serif"; X.fillStyle = "#6b5a48";
            X.fillText(U.S(orderReward(o)) + " coins + tip", x + 80, y + 68);
            for (int i = 0; i < items.Count; i++)
            {
                double ry = y + 104 + i * rowH;
                X.fillStyle = i % 2 != 0 ? "#fff5de" : "#f6ead0"; X.fillRect(x + 8, ry - rowH / 2 + 2, w - 16, rowH - 4);
                drawKey(items[i], x + 32, ry, 2.5);
                X.fillStyle = "#2b2230"; X.font = "700 17px \"Nunito\", system-ui, sans-serif"; X.fillText(itemName(items[i]), x + 62, ry + 1);
            }
            if (o.delivery)
            {
                X.fillStyle = "#a8301d"; X.font = "800 14px \"Nunito\", system-ui, sans-serif";
                X.fillText(o.late ? "Driver waiting! Hand the bag off now." : "Delivery: bag it for the pickup window.", x + 14, y + h - 16);
            }
            X.globalAlpha = 1;
        }

        void drawHUD(double dt)
        {
            var b = L.bar;
            // Pause button
            X.fillStyle = "#2a3150"; X.fillRect(L.pause.x - 18, L.pause.y - 18, 36, 36);
            X.fillStyle = "#fff5de"; X.fillRect(L.pause.x - 7, L.pause.y - 8, 5, 16); X.fillRect(L.pause.x + 2, L.pause.y - 8, 5, 16);
            // Timer
            bool low = G.time <= 30 && !G.lv.tutorial;
            X.font = "400 26px \"Pixelify Sans\", system-ui, sans-serif"; X.textAlign = "center"; X.textBaseline = "middle";
            double tw = 96;
            X.fillStyle = low && Math.Sin(T * 8) > 0 ? "#e8472f" : "#2a3150"; X.fillRect(U.Round(W / 2 - tw / 2), b.y, tw, 38);
            X.fillStyle = "#fff5de"; X.fillText(G.lv.tutorial ? "Practice" : U.FmtTime(G.time), W / 2, b.y + 20);
            // Coins
            double cx = b.x + b.w - 14, cy = b.y + 20;
            double pulse = 1 + G.coinPulse * 0.3;
            X.font = "400 " + U.Round(24 * pulse) + "px \"Pixelify Sans\", system-ui, sans-serif"; X.textAlign = "right";
            X.fillStyle = "#fff5de"; X.fillText(U.S(G.score), cx - 26, cy + 1);
            spr("coin", cx - 8, cy, 3);

            // Active event, under the timer.
            if (G.evt != null)
            {
                string tag = G.evt.name + " " + Math.Ceiling(G.evt.t) + "s"; double ty = b.y + (G.lv.judge ? 72 : 44);
                X.font = "700 13px \"Pixelify Sans\", monospace"; X.textAlign = "center"; X.textBaseline = "middle";
                double tw2 = X.measureText(tag).width + 16;
                X.fillStyle = "#f5d33e"; X.fillRect(U.Round(W / 2 - tw2 / 2), ty, tw2, 18);
                X.fillStyle = "#2b2230"; X.fillText(tag, W / 2, ty + 10);
            }
            // Order tickets
            var R = L.orders; int n = maxOrders(); double gap = 6;
            double cw = Math.Min(122, (R.w - gap * (n - 1)) / n), ch = R.h;
            for (int i = 0; i < G.orders.Count; i++)
            {
                var o = G.orders[i];
                double tx = R.x + i * (cw + gap);
                if (o.x == null) o.x = R.x + R.w;
                o.x += (tx - o.x.Value) * Math.Min(1, dt * 10);
                double frac = o.timeLeft / o.total;
                double shake = frac < 0.25 ? Math.Sin(T * 40) * 1.5 : 0;
                double x = o.x.Value + shake, y = R.y;
                double px = U.Round(x);
                X.fillStyle = "rgba(0,0,0,.35)"; X.fillRect(px + 3, y + 3, cw, ch);
                X.fillStyle = PAL('k'); X.fillRect(px - 2, y - 2, cw + 4, ch + 4);
                X.fillStyle = "#fff5de"; X.fillRect(px, y, cw, ch);
                // time strip
                string col = frac > 0.5 ? "#3f9b45" : frac > 0.25 ? "#f5b82e" : "#e8472f";
                X.fillStyle = "#e8dcbc"; X.fillRect(px + 5, y + 5, cw - 10, 6);
                X.fillStyle = col; X.fillRect(px + 5, y + 5, Math.Max(3, U.Round((cw - 10) * frac / 3) * 3), 6);
                if (o.vip)
                {   // VIP: gold frame and a star
                    X.strokeStyle = "#f5d33e"; X.lineWidth = 3; X.strokeRect(px + 1.5, y + 1.5, cw - 3, ch - 3);
                    X.fillStyle = "#b9821a"; X.font = "700 15px \"Pixelify Sans\", monospace"; X.textAlign = "left"; X.textBaseline = "top";
                    X.fillText("★", px + 5, y + 13);
                }
                if (o.delivery)
                {
                    // Delivery badge, and an orange frame that flashes while the driver waits.
                    if (o.late && Math.Sin(T * 12) > 0) { X.strokeStyle = "#f59a2e"; X.lineWidth = 3; X.strokeRect(px + 1.5, y + 1.5, cw - 3, ch - 3); }
                    spr("bag", px + cw - 11, y + 22, 1.5);
                }
                // ingredients row at the bottom, dish in the space above it
                var oi = orderItems(o);
                var ings = recipeIngredients(oi);
                // Icons as big as the ticket allows (in half-pixel steps): build-your-own orders live or die by these.
                // Long lists on narrow tickets wrap onto two rows so the icons stay readable.
                int rows = ings.Count >= 4 && (cw - 8) / (ings.Count * 13.0) < 1.5 ? 2 : 1;
                int perRow = (int)Math.Ceiling(ings.Count / (double)rows);
                double ipx = Math.Max(1, Math.Min(ch > 80 ? 2 : 1.5, Math.Floor((cw - 8) / (Math.Max(1, perRow) * 13.0) * 2) / 2));
                double isz = 13 * ipx;
                double dpx = Math.Max(2, Math.Floor(Math.Min(ch - 16 - (ings.Count > 0 ? rows * isz + 6 : 0), cw * 0.6) / 12));
                if (RECIPES[o.recipe].mug) drawKey(oi[0], px + cw / 2, y + 14 + dpx * 6, dpx);
                else drawPlate(oi, px + cw / 2, y + 14 + dpx * 6, dpx);
                for (int r = 0; r < rows; r++)
                {
                    var row = ings.Skip(r * perRow).Take(perRow).ToList();
                    for (int j = 0; j < row.Count; j++) drawKey(row[j], px + cw / 2 + (j - (row.Count - 1) / 2.0) * isz, y + ch - isz / 2 - 4 - (rows - 1 - r) * isz, ipx);
                }
            }

            var kg = kitchenGeom();
            foreach (var f in G.floats)
            {
                X.globalAlpha = Math.Min(1, f.life * 2);
                double fx = f.at == "tile" ? kg.ox + f.x * kg.ts : L.orders.x + 50;
                double fy = (f.at == "tile" ? kg.oy + f.y * kg.ts : L.orders.y + L.orders.h + 14) + f.dy;
                X.font = "400 28px \"Pixelify Sans\", system-ui, sans-serif"; X.textAlign = "center";
                double half = X.measureText(f.text).width / 2 + 8;
                fx = Math.Max(half, Math.Min(W - half, fx));
                X.lineWidth = 5; X.strokeStyle = "#1c2133"; X.strokeText(f.text, fx, fy); X.fillStyle = f.color; X.fillText(f.text, fx, fy);
                if (f.sub != null) { X.font = "800 13px Nunito, sans-serif"; X.lineWidth = 4; X.strokeText(f.sub, fx, fy + 20); X.fillStyle = "#fff5de"; X.fillText(f.sub, fx, fy + 20); }
                X.globalAlpha = 1;
            }
        }

        // A circle built from 3px rows so the controls match the pixel art.
        public void pixCircle(double cx, double cy, double r, string color, double s = 3)
        {
            X.fillStyle = color;
            for (double dy = -r; dy < r; dy += s)
            {
                double yy = dy + s / 2, hw = U.Round(Math.Sqrt(Math.Max(0, r * r - yy * yy)) / s) * s;
                if (hw > 0) X.fillRect(U.Round(cx - hw), U.Round(cy + dy), hw * 2, s);
            }
        }

        void drawButton(Pt p, double r, string fill, string dark, string label, string sub, bool dim, double flash)
        {
            X.globalAlpha = dim ? 0.5 : 1;
            double press = U.Round(flash * 3);
            pixCircle(p.x, p.y + 1, r + 3, PAL('k'));
            pixCircle(p.x, p.y + 4, r, dark);
            pixCircle(p.x, p.y + press, r, fill);
            pixCircle(p.x - r * 0.3, p.y - r * 0.35 + press, r * 0.3, "rgba(255,255,255,.22)");
            X.fillStyle = fill == "#f5b82e" ? "#2b2230" : "#fff";
            X.textAlign = "center"; X.textBaseline = "middle";
            X.font = "400 " + U.Round(r * (label.Length > 4 ? 0.42 : 0.5)) + "px \"Pixelify Sans\", system-ui, sans-serif";
            X.fillText(label, p.x, p.y + press + (sub != null ? -r * 0.1 : 1));
            if (sub != null) { X.font = "800 " + U.Round(r * 0.26) + "px Nunito, sans-serif"; X.globalAlpha *= 0.75; X.fillText(sub, p.x, p.y + press + r * 0.36); }
            X.globalAlpha = 1;
        }

        void drawControls()
        {
            // Joystick
            var j = input.joy; bool active = j.id != null;
            double bx = active ? j.bx : L.joyRest.x, by = active ? j.by : L.joyRest.y;
            pixCircle(bx, by, JOY_R + 6, active ? "rgba(255,245,222,.14)" : "rgba(255,245,222,.08)");
            double kx = bx, ky = by;
            if (active) { double dx = j.x - j.bx, dy = j.y - j.by, d = U.Hypot(dx, dy); if (d == 0) d = 1; double m = Math.Min(d, JOY_R); kx = bx + dx / d * m; ky = by + dy / d * m; }
            pixCircle(kx, ky, 24, active ? "rgba(255,245,222,.75)" : "rgba(255,245,222,.3)");
            if (!active && G.phase != "over")
            {
                X.fillStyle = "rgba(255,245,222,.45)"; X.font = "800 11px Nunito, sans-serif"; X.textAlign = "center";
                X.fillText("DRAG TO MOVE", bx, by + JOY_R + 22);
            }

            var c = G.chefs[G.active];
            var gp = G.phase == "play" ? grabPlan(c) : null;
            var cp = G.phase == "play" ? actionPlan(c) : null;
            double r = L.ctrlR;
            drawButton(L.btnA, r, "#f5b82e", "#b9821a", gp != null ? gp.label : (c.held != null ? "Drop" : "Grab"), null, gp == null || gp.dim, input.flashA);
            drawButton(L.btnB, r * 0.85, "#e8472f", "#a8301d", cp != null ? cp.label : "Chop", null, cp == null || cp.dim, c.spraying ? 0.6 : input.flashB);
            if (NET.role != null && !G.versus) return;   // online co-op, each player keeps their own chef
            // Swap shows the colour of the chef you'd switch to.
            var humans = G.chefs.Where(q => q.ai == null).ToList();
            if (humans.Count < 2) return;   // Short-Staffed: nobody to swap to
            var other = humans[(humans.IndexOf(G.chefs[G.active]) + 1) % humans.Count];
            double sr = r * 0.62;
            if (G.swapPing > 0)
            {
                double k = (G.swapPing * 2.5) % 1;
                X.globalAlpha = 1 - k; pixCircle(L.btnS.x, L.btnS.y, sr + 4 + k * 16, "#f5d33e"); X.globalAlpha = 1;
            }
            drawButton(L.btnS, sr, "#3f9b45", "#2a6e2f", "Swap", null, false, input.flashS);
            pixCircle(L.btnS.x + sr * 0.72, L.btnS.y - sr * 0.72, sr * 0.3 + 3, "#fff");
            pixCircle(L.btnS.x + sr * 0.72, L.btnS.y - sr * 0.72, sr * 0.3, other.color);
        }

        void drawBanner()
        {
            string text = null; double size = 64;
            if (G.phase == "countdown")
            {
                int n = (int)Math.Ceiling(G.countdown);
                text = n > 3 ? null : n.ToString();
                if (G.countdown > 3) text = "Ready?";
            }
            else if (G.phase == "play" && !G.lv.tutorial && G.time > G.lv.time - 0.8) text = "Go!";
            else if (G.phase == "over") { text = G.kicked ? "Kicked out!" : "Time's up!"; size = 52; }
            if (text == null) return;
            var k = L.kitchen;
            X.font = "400 " + U.S(size) + "px \"Pixelify Sans\", system-ui, sans-serif"; X.textAlign = "center"; X.textBaseline = "middle";
            X.lineWidth = 10; X.strokeStyle = "#1c2133"; X.lineJoin = "round";
            X.strokeText(text, k.x + k.w / 2, k.y + k.h / 2);
            X.fillStyle = G.phase == "over" ? "#e8472f" : "#fff5de";
            X.fillText(text, k.x + k.w / 2, k.y + k.h / 2);
        }

        // The judge's portrait and speech bubble, over the top of the kitchen.
        void drawJudge()
        {
            var j = G.judge;
            var k = L.kitchen; double x = k.x + 6, y = k.y + 4;
            // Strikes, under the timer.
            double bw = 22, gap = 6, sx = W / 2 - 60 - (bw * 3 + gap * 2), sy = L.bar.y + 8;
            for (int i = 0; i < MAX_STRIKES; i++)
            {
                bool on = i < G.strikes;
                X.fillStyle = PAL('k'); X.fillRect(sx + i * (bw + gap) - 2, sy - 2, bw + 4, bw + 4);
                X.fillStyle = on ? "#e8472f" : "#2a3150"; X.fillRect(sx + i * (bw + gap), sy, bw, bw);
                X.fillStyle = on ? "#fff" : "#4a5270";
                X.font = "700 16px \"Pixelify Sans\", monospace"; X.textAlign = "center"; X.textBaseline = "middle";
                X.fillText("X", sx + i * (bw + gap) + bw / 2, sy + bw / 2 + 1);
            }
            if (j == null || j.t <= 0) return;
            X.globalAlpha = Math.Min(1, j.t * 3);
            string face = j.mood == 0 ? "#f2c9a0" : j.mood == 1 ? "#f0a080" : "#e8604a";
            X.fillStyle = PAL('k'); X.fillRect(x - 3, y - 3, 58, 58);
            X.fillStyle = "#2a3150"; X.fillRect(x, y, 52, 52);
            var judge = j.who != null && Data.JUDGES.TryGetValue(j.who, out var jd) ? jd : Data.JUDGES["ramsey"];
            spr(judge.sprite, x + 26, y + 26 + (j.mood != 0 ? U.Round(Math.Sin(T * 30)) : 0), 3, S("s", face, "S", Css.Mix(face, "#000000", 0.2)));
            // bubble
            double bx = x + 62, maxW = Math.Min(300, W - bx - 12);
            X.font = "700 15px \"Pixelify Sans\", \"Courier New\", monospace";
            var words = j.text.Split(' '); var lines = new List<string>();
            string line = "";
            foreach (var w in words) { string t = line.Length > 0 ? line + " " + w : w; if (X.measureText(t).width > maxW - 20 && line.Length > 0) { lines.Add(line); line = w; } else line = t; }
            lines.Add(line);
            double bh = lines.Count * 19 + 32;
            X.fillStyle = PAL('k'); X.fillRect(bx - 3, y - 3, maxW + 6, bh + 6);
            X.fillStyle = j.mood != 0 ? "#fff0ec" : "#fff5de"; X.fillRect(bx, y, maxW, bh);
            X.fillStyle = PAL('k'); X.beginPath(); X.moveTo(bx - 3, y + 16); X.lineTo(bx - 12, y + 24); X.lineTo(bx - 3, y + 30); X.fill();
            X.fillStyle = j.mood == 2 ? "#c0301d" : "#2b2230"; X.textAlign = "left"; X.textBaseline = "top";
            X.save(); X.font = "500 11px \"DM Mono\", ui-monospace, monospace"; X.fillStyle = "#7d6c57";
            X.fillText(judge.name.ToUpperInvariant(), bx + 10, y + 7); X.restore();
            for (int i = 0; i < lines.Count; i++) X.fillText(lines[i], bx + 10, y + 25 + i * 19);
            X.globalAlpha = 1;
        }

        // ---- tutorial highlights ----
        // Pulsing yellow arrow pointing down at (x, y), in whatever units are current.
        void tutArrow(double x, double y, double u)
        {
            double b = Math.Abs(Math.Sin(T * 5)) * 6 * u;
            X.fillStyle = PAL('k');
            X.beginPath(); X.moveTo(x - 9 * u, y - 16 * u - b); X.lineTo(x + 9 * u, y - 16 * u - b); X.lineTo(x, y - 2 * u - b); X.closePath(); X.fill();
            X.fillStyle = "#ffd23a";
            X.beginPath(); X.moveTo(x - 6 * u, y - 14.5 * u - b); X.lineTo(x + 6 * u, y - 14.5 * u - b); X.lineTo(x, y - 5 * u - b); X.closePath(); X.fill();
        }
        // Kitchen-space highlight (called inside the tile transform).
        void drawTutorialTile()
        {
            var t = G.tut != null ? tutStepTile(G.tut.i) : null;
            if (t == null) return;
            double a = 0.5 + Math.Abs(Math.Sin(T * 5)) * 0.5;
            X.strokeStyle = "rgba(255,210,58," + U.S(a) + ")"; X.lineWidth = 3 * P;
            X.strokeRect(t.x + P, t.y + P, 1 - 2 * P, 1 - 2 * P);
            // Top-row targets sit right under the card, so point at them from below instead.
            if (t.y == 0) { X.save(); X.translate(t.x + 0.5, t.y + 1); X.scale(1, -1); tutArrow(0, 0, P); X.restore(); }
            else tutArrow(t.x + 0.5, t.y, P);
        }
        // Screen-space highlight for buttons and HUD bits.
        void drawTutorialUI()
        {
            var step = G.tut != null ? Data.TUT_STEPS[G.tut.i] : null;
            if (step == null || step.ui == null) return;
            double r = L.ctrlR, a = 0.55 + Math.Abs(Math.Sin(T * 5)) * 0.45;
            double x = 0, y = 0, rad = 0; double[] rect = null;
            switch (step.ui)
            {
                case "joy": x = L.joyRest.x; y = L.joyRest.y; rad = JOY_R + 12; break;
                case "grab": x = L.btnA.x; y = L.btnA.y; rad = r + 8; break;
                case "chop": x = L.btnB.x; y = L.btnB.y; rad = r * 0.85 + 8; break;
                case "swap": x = L.btnS.x; y = L.btnS.y; rad = r * 0.62 + 8; break;
                case "coins": rect = new[] { L.bar.x + L.bar.w - 96, L.bar.y - 2, 96, 42 }; break;
                case "ticket":
                    {
                        int n = maxOrders(); double gap = 6, cw = Math.Min(122, (L.orders.w - gap * (n - 1)) / n);
                        var o = G.orders.Count > 0 ? G.orders[G.orders.Count - 1] : null;
                        rect = new[] { (o != null && o.x != null ? o.x.Value : L.orders.x) - 5, L.orders.y - 5, cw + 10, L.orders.h + 10 };
                        break;
                    }
            }
            X.strokeStyle = "rgba(255,210,58," + U.S(a) + ")"; X.lineWidth = 4;
            if (rect != null) { X.strokeRect(rect[0], rect[1], rect[2], rect[3]); x = rect[0] + rect[2] / 2; y = rect[1] + rect[3] + 30; }
            else { X.beginPath(); X.arc(x, y, rad, 0, Math.PI * 2); X.stroke(); y -= rad; }
            if (rect != null) { X.save(); X.translate(x, y); X.scale(1, -1); tutArrow(0, 0, 1.6); X.restore(); }
            else tutArrow(x, y, 1.6);
        }

        // A symbol from the kitchen, for the tutorial card (the web version's symbolCanvas).
        public void symbolIcon(string name, double x, double y, double size)
        {
            X.fillStyle = "#5b6272"; X.fillRect(x, y, size, size);
            double px = 2, mid = size / 2;
            if (name == "bar") { X.save(); X.translate(x + mid, y + mid); X.scale(px * 16, px * 16); drawBar(0, 0, 0.6, PAL('l')); X.restore(); }
            else if (name == "fire") X.drawImage(sprite("fire1"), x + mid - 8 * px, y + mid - 8 * px, 16 * px, 16 * px);
            else spr(name, x + mid, y + mid, px + 1);
        }
    }
}
