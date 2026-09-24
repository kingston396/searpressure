using System;
using System.Collections.Generic;
using System.Linq;

namespace SearPressure
{
    /* =========================================================================
       Delivery Run: drive the orders out yourself, Crazy Taxi style.
       A countdown, an arrow to the customer, time bonuses for every drop-off,
       and coins for drifts, jumps, near misses and anything you knock over.
       ========================================================================= */

    public sealed class DCar { public double x, y, a, vx, vy, air, airMax, slip; }
    public sealed class DCam { public double x, y, a; }
    public sealed class DPoint { public double x, y; public DPoint(double x, double y) { this.x = x; this.y = y; } }
    public sealed class DRamp { public int x, y; public bool horiz; }
    public sealed class DWorks { public double x, y; public bool horiz; }
    public sealed class DHazard { public double x, y; public string kind; public bool on; }
    public sealed class DSolid { public double x, y, r; public string kind; }
    public sealed class DProp { public double x, y, vx, vy, spin, rot, life = 1; public string kind; public bool hit; }
    public sealed class DTarget { public double x, y; public bool truck; }
    public sealed class DPop { public string text, sub, color; public double life; }
    public sealed class DSkid { public double x, y, life; }
    public sealed class DPuff { public double x, y, vx, vy, life; public bool smoke; }
    public sealed class DTraffic
    {
        public double x, y, v, vmax, stopT, nearT, wreck, waitT;
        public string dir, color;
        public bool reckless;
        public DTraffic Clone() => (DTraffic)MemberwiseClone();
    }
    public sealed class DPed
    {
        public double x, y, v, down, dodge, dvx, dvy, safe;
        public int tx, ty, px, py;
        public string shirt;
        public bool tried;
    }
    public sealed class DTutS { public double a; public bool fast; }
    public sealed class DTut { public int i; public DTutS s = new DTutS(); public Rect skip, next; }

    public sealed class DriveCity
    {
        public int n;
        public byte[] t;
        public short[] bid;
        public List<string> bcol = new List<string>();
        public List<DRamp> ramps = new List<DRamp>();
        public List<DProp> props = new List<DProp>();
        public List<DSolid> solids = new List<DSolid>();
        public List<DHazard> hazards = new List<DHazard>();
        public List<DWorks> works = new List<DWorks>();
        public List<DPoint> doors = new List<DPoint>();
        public DPoint truck;
        public int at(int x, int y) => x < 0 || y < 0 || x >= n || y >= n ? Game.BLDG : t[y * n + x];
        public int at(double x, double y) => at((int)Math.Floor(x), (int)Math.Floor(y));
    }

    public sealed class DriveState
    {
        public string phase = "count";
        public bool revived;
        public bool hand;
        public int k;
        public DriveRun run;
        public DriveCity city;
        public double count = 3.2, lastCount = 4, endT;
        public DCar car;
        public DCam cam;
        public double time, coins, tips, delivered, bags, cap, orderT, par = 10;
        public DTarget target;
        public int? handId;
        public double driftT, shake, smashed, stunts, ouch, lastSec = 99, hits, hitLoss;
        public List<DPop> pops = new List<DPop>();
        public List<DSkid> skids = new List<DSkid>();
        public List<DPuff> puffs = new List<DPuff>();
        public List<DPed> peds = new List<DPed>();
        public List<DTraffic> traffic = new List<DTraffic>();
        public Attempt attempt;
        public DTut tut;
        public double? popY;
        public bool shown, eng;
    }

    public sealed partial class Game
    {
        public DriveState D;
        // The engine hum for the host to play: 0 = silent, otherwise the level 0..1 (the web version
        // plays a sawtooth at 55 + level * 90 Hz, gain 0.025 + level * 0.025, through a 500 Hz lowpass).
        public double engineLevel;

        // Secret bonus: each run is discovered by three-starring a delivery kitchen.
        public bool driveOpen(int k) => unlockAll() || bestStars(Data.LEVELS.FindIndex(l => l.name == Data.DRIVE_RUNS[k].key)) >= 3;
        // Runs opened since the player last saw a results screen (announced once).
        public List<int> newDriveRuns()
        {
            var fresh = Enumerable.Range(0, Data.DRIVE_RUNS.Count).Where(k => driveOpen(k) && !(k < save.driveSeen.Count && save.driveSeen[k]) && !unlockAll()).ToList();
            foreach (var k in fresh) { while (save.driveSeen.Count <= k) save.driveSeen.Add(false); save.driveSeen[k] = true; }
            if (fresh.Count > 0) persist();
            return fresh;
        }
        public int driveStars(int k, double coins) => Data.DRIVE_RUNS[k].stars.Count(s => coins >= s);
        double driveTimeLeft() => D != null ? D.time : 99;

        public const int RD = 0, WALK = 1, BLDG = 2, GRASS = 3, TREE = 4, DIRT = 5, BARR = 6;   // city tiles
        const double CAR_MAX = 9, CAR_REV = 3, CAR_ACC = 7, CAR_BRAKE = 14, CAR_TURN = 2.7, CAR_R = 0.32;
        const double PED_PENALTY = 10;
        // Delivery-van upgrades from the shop.
        bool vanUp(string id) => owns(id);
        static readonly Dictionary<string, int[]> DIRV = new Dictionary<string, int[]> { ["E"] = new[] { 1, 0 }, ["W"] = new[] { -1, 0 }, ["N"] = new[] { 0, -1 }, ["S"] = new[] { 0, 1 } };
        static readonly int[][] DIRS4 = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };

        static DProp newProp(double x, double y, string kind) => new DProp { x = x, y = y, kind = kind };

        // Build a city from the run's seed: a grid of two-lane roads, sidewalks, buildings and parks,
        // with road works, potholes, oil and street clutter.
        DriveCity driveCity(DriveRun run, double seed)
        {
            var r = Rng.Mulberry32(seed);
            int n = run.blocks * 8 + 2;
            var c = new DriveCity { n = n, t = new byte[n * n], bid = new short[n * n] };
            var t = c.t;
            for (int i = 0; i < c.bid.Length; i++) c.bid[i] = -1;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                int bx = x % 8, by = y % 8;
                t[y * n + x] = (byte)(bx < 2 || by < 2 ? RD : bx == 2 || bx == 7 || by == 2 || by == 7 ? WALK : BLDG);
            }
            for (int j = 0; j < run.blocks; j++) for (int i = 0; i < run.blocks; i++)
            {
                int ox = i * 8 + 3, oy = j * 8 + 3;
                if (r() < 0.16)
                {   // a park: grass you can drive over, and trees you can't
                    for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) t[(oy + y) * n + ox + x] = (byte)(r() < 0.3 ? TREE : GRASS);
                    continue;
                }
                // One, two or four buildings on the block.
                double split = r();
                int[][] rects = split < 0.3 ? new[] { new[] { 0, 0, 4, 4 } } : split < 0.65 ? (r() < 0.5 ? new[] { new[] { 0, 0, 2, 4 }, new[] { 2, 0, 2, 4 } } : new[] { new[] { 0, 0, 4, 2 }, new[] { 0, 2, 4, 2 } }) : new[] { new[] { 0, 0, 2, 2 }, new[] { 2, 0, 2, 2 }, new[] { 0, 2, 2, 2 }, new[] { 2, 2, 2, 2 } };
                foreach (var q in rects)
                {
                    int id = c.bcol.Count;
                    c.bcol.Add(Data.ROOFS[(int)Math.Floor(r() * Data.ROOFS.Length)]);
                    for (int y = 0; y < q[3]; y++) for (int x = 0; x < q[2]; x++) c.bid[(oy + q[1] + y) * n + ox + q[0] + x] = (short)id;
                }
            }
            void set(int x, int y, int k) { t[y * n + x] = (byte)k; }
            // Doors (sidewalk next to a building) are where customers wait. The truck parks nearest the middle.
            var doorsAll = new List<DPoint>();
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                if (t[y * n + x] == WALK && DIRS4.Any(d => c.at(x + d[0], y + d[1]) == BLDG)) doorsAll.Add(new DPoint(x + 0.5, y + 0.5));
            double mid = n / 2.0;
            var truck = doorsAll.OrderBy(a => U.Hypot(a.x - mid, a.y - mid)).First();
            c.truck = truck;
            // Road works close a whole block of road: barricades at each end, dirt in between.
            while (c.works.Count < run.works)
            {
                bool horiz = r() < 0.5;
                int band = (1 + (int)Math.Floor(r() * (run.blocks - 1))) * 8, seg = (int)Math.Floor(r() * run.blocks) * 8;
                double cx = horiz ? seg + 4.5 : band + 1, cy = horiz ? band + 1 : seg + 4.5;
                if (U.Hypot(cx - truck.x, cy - truck.y) < 10 || c.works.Any(w => U.Hypot(w.x - cx, w.y - cy) < 12)) continue;
                c.works.Add(new DWorks { x = cx, y = cy, horiz = horiz });
                for (int a = 2; a <= 7; a++) for (int l = 0; l < 2; l++)
                {
                    int x = horiz ? seg + a : band + l, y = horiz ? band + l : seg + a;
                    set(x, y, a == 2 || a == 7 ? BARR : DIRT);
                }
            }
            // Ramps sit mid-block on a lane and launch you along the road.
            for (int tries = 0; c.ramps.Count < run.ramps && tries < 500; tries++)
            {
                bool horiz = r() < 0.5;
                int band = (int)Math.Floor(r() * (run.blocks + 1)) * 8; band += (int)Math.Floor(r() * 2);
                int along = (int)Math.Floor(r() * run.blocks) * 8 + 3; along += (int)Math.Floor(r() * 4);
                int x = horiz ? along : band, y = horiz ? band : along;
                if (c.at(x, y) == RD && !c.ramps.Any(q => Math.Abs(q.x - x) + Math.Abs(q.y - y) < 6)) c.ramps.Add(new DRamp { x = x, y = y, horiz = horiz });
            }
            // Potholes jolt you; oil makes you spin.
            for (int tries = 0; c.hazards.Count < run.hazards && tries < 2000; tries++)
            {
                int x = (int)Math.Floor(r() * n), y = (int)Math.Floor(r() * n);
                int bx = x % 8, by = y % 8;
                if (c.at(x, y) != RD || (bx < 2 && by < 2) || c.ramps.Any(q => q.x == x && q.y == y) || c.hazards.Any(h => Math.Abs(h.x - x - 0.5) + Math.Abs(h.y - y - 0.5) < 3)) continue;
                c.hazards.Add(new DHazard { x = x + 0.5, y = y + 0.5, kind = r() < 0.6 ? "pothole" : "oil" });
            }
            // Street clutter to knock over, dumpsters you can't, and cones round the road works.
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                if (t[y * n + x] != WALK) continue;
                double q = r();
                if (q < 0.09)
                {
                    double px = x + 0.3 + r() * 0.4, py = y + 0.3 + r() * 0.4;
                    c.props.Add(newProp(px, py, Data.PROP_KINDS[(int)Math.Floor(r() * Data.PROP_KINDS.Count)]));
                }
                else if (q < 0.11 && U.Hypot(x - truck.x, y - truck.y) > 3) c.solids.Add(new DSolid { x = x + 0.5, y = y + 0.5, r = 0.36, kind = "dumpster" });
            }
            foreach (var w in c.works) for (int i = -1; i <= 1; i += 2) for (int l = 0; l < 2; l++)
            {
                double ax = w.horiz ? w.x + i * 3.3 : w.x - 0.5 + l, ay = w.horiz ? w.y - 0.5 + l : w.y + i * 3.3;
                c.props.Add(newProp(ax + (w.horiz ? i * 0.4 : 0), ay + (w.horiz ? 0 : i * 0.4), "cone"));
            }
            c.doors = doorsAll.Where(d => c.at(d.x, d.y) == WALK).ToList();
            return c;
        }

        /* ---------- the city picture ---------- */

        // A small software canvas for painting the city once (the web version paints an offscreen canvas).
        sealed class PixBuf
        {
            public readonly int w, h;
            public readonly byte[] px;
            public string fillStyle = "#000000";
            public PixBuf(int w, int h) { this.w = w; this.h = h; px = new byte[w * h * 4]; }
            public void fillRect(double fx, double fy, double fw, double fh)
            {
                int x0 = Math.Max(0, (int)fx), y0 = Math.Max(0, (int)fy), x1 = Math.Min(w, (int)(fx + fw)), y1 = Math.Min(h, (int)(fy + fh));
                var c = Css.Parse(fillStyle);
                if (c.a == 0) return;
                double a = c.a / 255.0;
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                    {
                        int i = (y * w + x) * 4;
                        if (c.a == 255) { px[i] = c.r; px[i + 1] = c.g; px[i + 2] = c.b; px[i + 3] = 255; continue; }
                        double da = px[i + 3] / 255.0, oa = a + da * (1 - a);
                        if (oa <= 0) continue;
                        px[i] = (byte)Math.Round((c.r * a + px[i] * da * (1 - a)) / oa);
                        px[i + 1] = (byte)Math.Round((c.g * a + px[i + 1] * da * (1 - a)) / oa);
                        px[i + 2] = (byte)Math.Round((c.b * a + px[i + 2] * da * (1 - a)) / oa);
                        px[i + 3] = (byte)Math.Round(oa * 255);
                    }
            }
        }

        // Paint the whole city once (16 px per tile), then draw it rotated every frame.
        static PixBuf renderCity(DriveCity c)
        {
            int n = c.n;
            var g = new PixBuf(n * 16, n * 16);
            var r = Rng.Mulberry32(7);
            int F(double v) => (int)Math.Floor(v);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                int k = c.t[y * n + x], px = x * 16, py = y * 16;
                if (k == RD)
                {
                    g.fillStyle = "#3a3d48"; g.fillRect(px, py, 16, 16);
                    int bx = x % 8, by = y % 8;
                    g.fillStyle = "#f5d33e";
                    if (by == 0 && bx >= 2) g.fillRect(px + 2, py + 15, 8, 2);   // centre line between the two lanes
                    if (bx == 0 && by >= 2) g.fillRect(px + 15, py + 2, 2, 8);
                    g.fillStyle = "#e8e0d0";   // crosswalks at the edge of each junction
                    if (bx < 2 && (by == 2 || by == 7)) for (int i = 1; i < 16; i += 4) g.fillRect(px + i, by == 2 ? py + 1 : py + 11, 2, 4);
                    if (by < 2 && (bx == 2 || bx == 7)) for (int i = 1; i < 16; i += 4) g.fillRect(bx == 2 ? px + 1 : px + 11, py + i, 4, 2);
                }
                else if (k == DIRT)
                {
                    g.fillStyle = "#9a7a52"; g.fillRect(px, py, 16, 16);
                    g.fillStyle = "#7a5a3a"; for (int i = 0; i < 6; i++) { int ax = px + F(r() * 15); g.fillRect(ax, py + F(r() * 15), 2, 1); }
                    g.fillStyle = "#b89868"; for (int i = 0; i < 3; i++) { int ax = px + F(r() * 14); g.fillRect(ax, py + F(r() * 14), 2, 2); }
                }
                else if (k == BARR)
                {
                    g.fillStyle = "#3a3d48"; g.fillRect(px, py, 16, 16);
                    g.fillStyle = "#2b1d2a"; g.fillRect(px, py + 4, 16, 8);
                    for (int i = 0; i < 4; i++) { g.fillStyle = i % 2 != 0 ? "#fff5de" : "#f59a2e"; g.fillRect(px + i * 4, py + 5, 4, 6); }
                    g.fillStyle = "#e8472f"; g.fillRect(px + 1, py + 2, 2, 2); g.fillRect(px + 13, py + 2, 2, 2);
                }
                else if (k == WALK)
                {
                    g.fillStyle = "#c2c6cf"; g.fillRect(px, py, 16, 16);
                    g.fillStyle = "#a8adb8"; g.fillRect(px, py + 15, 16, 1); g.fillRect(px + 15, py, 1, 16);
                    g.fillStyle = "#8a8f9c";   // curb
                    if (c.at(x - 1, y) == RD) g.fillRect(px, py, 2, 16);
                    if (c.at(x + 1, y) == RD) g.fillRect(px + 14, py, 2, 16);
                    if (c.at(x, y - 1) == RD) g.fillRect(px, py, 16, 2);
                    if (c.at(x, y + 1) == RD) g.fillRect(px, py + 14, 16, 2);
                }
                else if (k == GRASS || k == TREE)
                {
                    g.fillStyle = "#5fae4a"; g.fillRect(px, py, 16, 16);
                    g.fillStyle = "#4f9a3c"; for (int i = 0; i < 4; i++) { int ax = px + F(r() * 15); g.fillRect(ax, py + F(r() * 15), 1, 2); }
                    if (k == TREE)
                    {
                        g.fillStyle = "rgba(0,0,0,.25)"; g.fillRect(px + 3, py + 5, 12, 11);
                        g.fillStyle = "#2b1d2a"; g.fillRect(px + 2, py + 1, 12, 12);
                        g.fillStyle = "#2e6e34"; g.fillRect(px + 3, py + 2, 10, 10);
                        g.fillStyle = "#4fa843"; g.fillRect(px + 4, py + 3, 6, 5);
                        g.fillStyle = "#8ad65e"; g.fillRect(px + 5, py + 4, 2, 2);
                    }
                }
                else
                {
                    int id = c.bid[y * n + x];
                    string col = id >= 0 && id < c.bcol.Count ? c.bcol[id] : "#5a5f6e";
                    g.fillStyle = col; g.fillRect(px, py, 16, 16);
                    g.fillStyle = Css.Mix(col, "#000000", 0.3);
                    bool same(int dx, int dy) { int xx = x + dx, yy = y + dy; return xx >= 0 && yy >= 0 && xx < n && yy < n && c.bid[yy * n + xx] == id; }
                    if (!same(-1, 0)) g.fillRect(px, py, 2, 16);
                    if (!same(1, 0)) g.fillRect(px + 14, py, 2, 16);
                    if (!same(0, -1)) g.fillRect(px, py, 16, 2);
                    if (!same(0, 1)) g.fillRect(px, py + 13, 16, 3);
                    g.fillStyle = Css.Mix(col, "#ffffff", 0.2);
                    if (r() < 0.35) { g.fillRect(px + 5, py + 5, 5, 4); g.fillStyle = "#2b1d2a"; g.fillRect(px + 6, py + 6, 3, 2); }   // rooftop vents
                }
            }
            // Road hazards are painted on too.
            foreach (var h in c.hazards)
            {
                double px = U.Round(h.x * 16), py = U.Round(h.y * 16);
                if (h.kind == "pothole")
                {
                    g.fillStyle = "#2b1d2a"; g.fillRect(px - 5, py - 4, 10, 8); g.fillRect(px - 4, py - 5, 8, 10);
                    g.fillStyle = "#1c1624"; g.fillRect(px - 3, py - 3, 6, 6);
                    g.fillStyle = "#5a5f6e"; g.fillRect(px - 5, py + 3, 3, 1);
                }
                else
                {
                    g.fillStyle = "rgba(20,16,30,.85)"; g.fillRect(px - 7, py - 4, 14, 8); g.fillRect(px - 5, py - 6, 10, 12);
                    g.fillStyle = "rgba(111,176,214,.5)"; g.fillRect(px - 3, py - 3, 3, 1); g.fillStyle = "rgba(220,184,234,.5)"; g.fillRect(px + 1, py + 1, 3, 1);
                }
            }
            return g;
        }
        // A one-pixel-per-tile overview for the corner map.
        static PixBuf renderMinimap(DriveCity c)
        {
            var g = new PixBuf(c.n, c.n);
            string[] cols = { "#5a5f6e", "#8a8f9c", "#2a3150", "#3f7a34", "#2e5a2a", "#9a7a52", "#f59a2e" };   // by tile kind
            for (int y = 0; y < c.n; y++) for (int x = 0; x < c.n; x++) { g.fillStyle = cols[c.t[y * c.n + x]]; g.fillRect(x, y, 1, 1); }
            return g;
        }

        // The city picture is too big for one atlas page, so it goes into square chunks. The chunks
        // (and the minimap) are reserved once per atlas and repainted for each run, so runs don't leak.
        const int CHUNK = 31;   // tiles per chunk side: 496 px, four to an atlas page
        static Atlas driveAtlas;
        static Img[] citySlots;
        static Img miniSlot;
        int cityChunks;

        void uploadCity(PixBuf city, PixBuf mini)
        {
            var atlas = Sprites.Atlas;
            int maxN = Data.DRIVE_RUNS.Max(r => r.blocks) * 8 + 2;
            int per = (maxN + CHUNK - 1) / CHUNK, S = CHUNK * 16;
            if (driveAtlas != atlas || citySlots == null || citySlots.Length < per * per)
            {
                driveAtlas = atlas;
                citySlots = new Img[per * per];
                for (int i = 0; i < citySlots.Length; i++) citySlots[i] = atlas.Add(new byte[S * S * 4], S, S);
                miniSlot = atlas.Add(new byte[maxN * maxN * 4], maxN, maxN);
            }
            cityChunks = (city.w / 16 + CHUNK - 1) / CHUNK;
            for (int j = 0; j < cityChunks; j++) for (int i = 0; i < cityChunks; i++)
                blit(atlas, citySlots[j * per + i], city, i * S, j * S);
            blit(atlas, miniSlot, mini, 0, 0);
        }
        // Copy part of a picture into a reserved slot, gutter included (edge pixels repeat into it,
        // so the chunks meet without seams).
        static void blit(Atlas atlas, Img slot, PixBuf src, int sx0, int sy0)
        {
            var page = atlas.Pages[slot.page];
            for (int y = -1; y <= slot.height; y++)
            {
                int sy = Math.Max(0, Math.Min(src.h - 1, sy0 + y));
                for (int x = -1; x <= slot.width; x++)
                {
                    int sx = Math.Max(0, Math.Min(src.w - 1, sx0 + x));
                    int si = (sy * src.w + sx) * 4, di = ((slot.y + y) * Atlas.Size + slot.x + x) * 4;
                    page[di] = src.px[si]; page[di + 1] = src.px[si + 1]; page[di + 2] = src.px[si + 2]; page[di + 3] = src.px[si + 3];
                }
            }
            atlas.Dirty[slot.page] = true;
        }
        // X.drawImage(D.img, 0, 0, n, n), chunk by chunk.
        void drawCity()
        {
            int n = D.city.n, per = (int)Math.Sqrt(citySlots.Length);
            for (int j = 0; j < cityChunks; j++) for (int i = 0; i < cityChunks; i++)
            {
                double tw = Math.Min(CHUNK, n - i * CHUNK), th = Math.Min(CHUNK, n - j * CHUNK);
                X.drawImage(citySlots[j * per + i], 0, 0, tw * 16, th * 16, i * CHUNK, j * CHUNK, tw, th);
            }
        }

        /* ---------- driving tutorial: coaches the first Delivery Run, clock stopped ---------- */

        double carSpeed() => U.Hypot(D.car.vx, D.car.vy);
        static double angDiff(double a, double b) { double d = a - b; while (d > Math.PI) d -= Math.PI * 2; while (d < -Math.PI) d += Math.PI * 2; return Math.Abs(d); }
        // DRIVE_TUT's text comes from the data; what finishes each step lives here.
        bool driveTutDone(int i, DTutS s)
        {
            switch (i)
            {
                case 0: return carSpeed() > 3;
                case 1: return angDiff(D.car.a, s.a) > 1.2;
                case 2: if (carSpeed() > 3.5) s.fast = true; return s.fast && carSpeed() < 0.8;
                case 3: return D.driftT > 0.5;
                case 4: return D.delivered >= 1;
            }
            return false;
        }
        void tutGoDrive(int i)
        {
            D.tut = new DTut { i = i };
            if (i == 1) D.tut.s.a = D.car.a;
            if (i > 0) sfx("clean");
        }
        void tutFinishDrive()
        {
            D.tut = null;
            save.driveTut = true; persist();
            // The real run starts now, from where you are.
            D.time = D.run.time; D.coins = 0; D.tips = 0; D.delivered = 0; D.stunts = 0; D.smashed = 0; D.hits = 0; D.bags = D.cap > 0 ? D.cap : 3; D.lastSec = 99;
            nextDrop();
            drivePop("Go!", "The clock is running", "#8ad65e"); sfx("go");
        }
        // The coaching card, drawn on the canvas so it works in every build.
        double drawDriveTut(DriveView v)
        {
            var st = Data.DRIVE_TUT[D.tut.i]; var b = L.bar;
            double w = Math.Min(W - 24, 380), x = U.Round(W / 2 - w / 2), y = U.Round(b.y + 46 + Math.Min(96, W * 0.24) + 12);   // under the corner map
            X.font = "800 15px Nunito, system-ui, sans-serif";
            var words = st.text.Split(' '); var lines = new List<string>();
            string line = "";
            foreach (var wd in words) { string t = line.Length > 0 ? line + " " + wd : wd; if (X.measureText(t).width > w - 28 && line.Length > 0) { lines.Add(line); line = wd; } else line = t; }
            lines.Add(line);
            double h = 38 + lines.Count * 20 + (st.next ? 46 : 8);
            X.fillStyle = "rgba(0,0,0,.35)"; X.fillRect(x + 4, y + 4, w, h);
            X.fillStyle = PAL('k'); X.fillRect(x - 3, y - 3, w + 6, h + 6);
            X.fillStyle = "#fff5de"; X.fillRect(x, y, w, h);
            X.textAlign = "left"; X.textBaseline = "middle";
            X.font = "500 12px \"DM Mono\", ui-monospace, monospace"; X.fillStyle = "#7a6450";
            X.fillText($"DRIVING LESSON {D.tut.i + 1} OF {Data.DRIVE_TUT.Count}", x + 14, y + 17);
            X.textAlign = "right"; X.fillText("SKIP", x + w - 14, y + 17);
            D.tut.skip = new Rect(x + w - 70, y, 70, 34);
            X.textAlign = "left"; X.font = "800 15px Nunito, system-ui, sans-serif"; X.fillStyle = "#2b2230";
            for (int i = 0; i < lines.Count; i++) X.fillText(lines[i], x + 14, y + 42 + i * 20);
            D.tut.next = null;
            if (st.next)
            {
                double bw = 110, bh = 34, bx = x + w - bw - 12, by = y + h - bh - 10;
                X.fillStyle = "#a8301d"; X.fillRect(bx, by + 3, bw, bh);
                X.fillStyle = "#e8472f"; X.fillRect(bx, by, bw, bh);
                X.fillStyle = "#fff"; X.textAlign = "center"; X.font = "400 19px \"Pixelify Sans\", system-ui, sans-serif";
                X.fillText(st.finish ? "Go!" : "Next", bx + bw / 2, by + bh / 2 + 1);
                D.tut.next = new Rect(bx, by, bw, bh + 3);
            }
            // Point at what the step is about.
            double a = 0.5 + Math.Abs(Math.Sin(T * 5)) * 0.5;
            X.strokeStyle = "rgba(255,210,58," + U.S(a) + ")"; X.lineWidth = 4;
            X.beginPath();
            if (st.ui == "stick") X.arc(input.joy.id != null ? input.joy.bx : L.joyRest.x, input.joy.id != null ? input.joy.by : L.joyRest.y, JOY_R + 14, 0, Math.PI * 2);
            else if (st.ui == "drift") X.arc(L.btnA.x, L.btnA.y, L.ctrlR + 12, 0, Math.PI * 2);
            else if (st.ui == "arrow") X.arc(v.cx, v.cy - v.s * 1.9, 34, 0, Math.PI * 2);
            if (!string.IsNullOrEmpty(st.ui)) X.stroke();
            return y + h;
        }
        static bool inRect(Rect r, double x, double y) => r != null && x >= r.x && x <= r.x + r.w && y >= r.y && y <= r.y + r.h;
        bool tutTapDrive(double x, double y)
        {
            if (D.tut == null) return false;
            if (inRect(D.tut.skip, x, y)) { tutFinishDrive(); return true; }
            if (inRect(D.tut.next, x, y)) { var st = Data.DRIVE_TUT[D.tut.i]; if (st.finish) tutFinishDrive(); else tutGoDrive(D.tut.i + 1); return true; }
            return false;
        }

        public void startDrive(int k, bool tutorial = false) => startDrive(k, tutorial, null);
        public void startDrive(int k, bool tutorial, double? seed)
        {
            // A new city every run (same size, traffic and targets).
            var run = Data.DRIVE_RUNS[k];
            double s0 = seed ?? (CALM ? run.seed : Math.Floor(Random() * 1e9));
            var city = driveCity(run, s0);
            G = null; paused = false; show(null);
            var start = city.truck;
            // Start on the road next to the truck, facing along it.
            double sx = start.x, sy = start.y, a = 0;
            foreach (var d in DIRS4) if (city.at(start.x + d[0], start.y + d[1]) == RD) { sx = start.x + d[0]; sy = start.y + d[1]; a = d[0] != 0 ? Math.PI / 2 : 0; break; }
            double cap = vanUp("cargo") ? 4 : 3;
            D = new DriveState
            {
                k = k, run = run, city = city, phase = "count", count = 3.2, lastCount = 4, endT = 0,
                car = new DCar { x = sx, y = sy, a = a },
                cam = new DCam { x = sx, y = sy, a = a },
                time = run.time, bags = cap, cap = cap, par = 10, lastSec = 99,
            };
            uploadCity(renderCity(city), renderMinimap(city));
            var r = Rng.Mulberry32(s0 + 1);
            for (int i = 0; i < run.traffic; i++) D.traffic.Add(spawnTraffic(r));
            // A few crashes are already blocking lanes when you set off.
            for (int i = 0; i < run.wrecks; i++)
            {
                var a1 = spawnTraffic(r);
                a1.wreck = double.PositiveInfinity; a1.v = 0;
                var a2 = a1.Clone();
                a2.x = a1.x + 0.45; a2.y = a1.y + 0.3; a2.dir = a1.dir == "E" ? "N" : a1.dir == "W" ? "S" : a1.dir == "N" ? "W" : "E";
                a2.color = Data.ROOFS[(int)Math.Floor(r() * Data.ROOFS.Length)];
                D.traffic.Add(a1); D.traffic.Add(a2);
                foreach (var o in new[] { new[] { -1.2, 0 }, new[] { 1.4, 0.2 }, new[] { 0, -1.1 }, new[] { 0.3, 1.2 } }) city.props.Add(newProp(a1.x + o[0], a1.y + o[1], "cone"));
            }
            for (int i = 0; i < run.peds; i++) D.peds.Add(spawnPed(r));
            D.attempt = beginAttempt("drive" + k);
            nextDrop();
            input.joy.id = null;
            // First time on the first run (or asked for): a driving lesson before the clock starts.
            if (tutorial || (k == 0 && !save.driveTut)) tutGoDrive(0);
            engineStart();
        }

        // Traffic keeps to the right-hand lane: westbound on each road's top lane, eastbound below it,
        // southbound on the left column and northbound on the right.
        DTraffic spawnTraffic(RandFn r)
        {
            int blocks = D.run.blocks;
            for (int tries = 0; tries < 80; tries++)
            {
                bool horiz = r() < 0.5;
                int lane = (int)Math.Floor(r() * 2);
                int band = (int)Math.Floor(r() * (blocks + 1)) * 8 + lane;
                double along = Math.Floor(r() * blocks) * 8 + 2; along += r() * 6;
                var c = horiz ? new DTraffic { x = along, y = band + 0.5, dir = lane != 0 ? "E" : "W" } : new DTraffic { x = band + 0.5, y = along, dir = lane != 0 ? "N" : "S" };
                if (U.Hypot(c.x - D.car.x, c.y - D.car.y) < 8 || D.city.at(c.x, c.y) != RD) continue;
                if (D.traffic.Any(o => U.Hypot(o.x - c.x, o.y - c.y) < 1.5)) continue;
                // About one driver in six doesn't look out for cross traffic: that's where accidents come from.
                c.v = 0; c.vmax = 2.6 + r() * 1.8; c.color = Data.ROOFS[(int)Math.Floor(r() * Data.ROOFS.Length)]; c.reckless = r() < 0.17;
                return c;
            }
            return new DTraffic { x = 1.5, y = 1.5, dir = "E", v = 0, vmax = 3, color = "#3f6f8f" };
        }

        // Pedestrians stroll the sidewalks and use the crosswalks.
        bool pedTile(int x, int y)
        {
            int k = D.city.at(x, y);
            if (k == WALK) return true;
            if (k != RD) return false;
            int bx = x % 8, by = y % 8;
            return (bx < 2 && (by == 2 || by == 7)) || (by < 2 && (bx == 2 || bx == 7));
        }
        DPed spawnPed(RandFn r)
        {
            int n = D.city.n;
            for (int tries = 0; tries < 80; tries++)
            {
                int x = (int)Math.Floor(r() * n), y = (int)Math.Floor(r() * n);
                if (D.city.at(x, y) != WALK || U.Hypot(x - D.car.x, y - D.car.y) < 4) continue;
                var p = new DPed { x = x + 0.5, y = y + 0.5, tx = x, ty = y, px = x, py = y };
                p.v = 0.7 + r() * 0.5; p.shirt = Data.SHIRTS[(int)Math.Floor(r() * Data.SHIRTS.Length)];
                return p;
            }
            return new DPed { x = 2.5, y = 2.5, tx = 2, ty = 2, px = 2, py = 2, v = 1, shirt = "#3a86d4" };
        }

        void nextDrop()
        {
            var c = D.car; var city = D.city;
            if (D.bags <= 0) { D.target = new DTarget { x = city.truck.x, y = city.truck.y, truck = true }; return; }
            // Far enough to be a drive, near enough to make it in time, even on the big maps.
            var far = city.doors.Where(dd => { double q = U.Hypot(dd.x - c.x, dd.y - c.y); return q > 10 && q < 28; }).ToList();
            var pool = far.Count > 0 ? far : city.doors;
            var d = pool[(int)Math.Floor(Random() * pool.Count)];
            D.target = new DTarget { x = d.x, y = d.y };
            D.orderT = 0;
            D.par = U.Hypot(d.x - c.x, d.y - c.y) / 4.5 + 5;
        }

        void drivePop(string text, string sub, string color = "#f5b82e") { D.pops.Add(new DPop { text = text, sub = sub, color = color, life = 1.6 }); if (D.pops.Count > 3) D.pops.RemoveAt(0); }

        // The web version's noise(dur, vol, freq) bursts, played as the closest named synth sound.
        void noise(double dur, double vol, double freq) => sfx(freq >= 1000 ? "splash" : "trash");

        bool driveSolid(double x, double y)
        {
            foreach (var d in new[] { new[] { -CAR_R, -CAR_R }, new[] { CAR_R, -CAR_R }, new[] { -CAR_R, CAR_R }, new[] { CAR_R, CAR_R } })
            {
                int k = D.city.at(x + d[0], y + d[1]);
                if (k == BLDG || k == TREE || k == BARR) return true;
            }
            return false;
        }

        void driveBump(double impact)
        {
            if (impact < 1.5) return;
            D.shake = Math.Min(0.45, impact * 0.05);
            noise(0.15, Math.Min(0.5, impact * 0.06), 300);
            buzz(20);
            if (impact > 6) drivePop("Crunch!", null, "#ff6b57");
        }

        void updateDrive(double dt)
        {
            if (D.phase == "revive") { engineSet(0); return; }
            var c = D.car; var city = D.city;
            D.shake = Math.Max(0, D.shake - dt);
            foreach (var p in D.pops) p.life -= dt;
            D.pops.RemoveAll(p => p.life <= 0);
            foreach (var s in D.skids) s.life -= dt * 0.25;
            D.skids.RemoveAll(s => s.life <= 0);
            foreach (var p in D.puffs) { p.life -= dt; p.x += p.vx * dt; p.y += p.vy * dt; }
            if (D.phase != "play") foreach (var t in D.traffic) if (t.wreck > 0 && Random() < dt * 2) D.puffs.Add(new DPuff { x = t.x, y = t.y, vx = 0, vy = -0.5, life = 0.9, smoke = true });
            D.puffs.RemoveAll(p => p.life <= 0);
            foreach (var p in city.props) if (p.hit && p.life > 0) { p.x += p.vx * dt; p.y += p.vy * dt; p.vx *= Math.Exp(-2 * dt); p.vy *= Math.Exp(-2 * dt); p.rot += p.spin * dt; p.life -= dt * 0.4; }

            if (D.phase == "count")
            {
                D.count -= dt;
                double cn = Math.Ceiling(D.count);
                if (cn < D.lastCount && cn > 0) { sfx("tick"); D.lastCount = cn; }
                if (D.count <= 0) { D.phase = "play"; sfx("go"); }
                engineSet(0);
                return;
            }
            if (D.phase == "over")
            {
                D.endT += dt;
                c.vx *= Math.Exp(-3 * dt); c.vy *= Math.Exp(-3 * dt);
                c.x += c.vx * dt; c.y += c.vy * dt;
                engineSet(0);
                if (D.endT > 1.8 && !D.shown) { D.shown = true; engineStop(); driveResults(); }
                return;
            }

            if (D.tut != null) { if (driveTutDone(D.tut.i, D.tut.s)) tutGoDrive(D.tut.i + 1); }
            else D.time -= dt;
            double sec = Math.Ceiling(D.time);
            if (D.tut == null && sec <= 10 && sec < D.lastSec && sec > 0) sfx("tick");
            D.lastSec = sec;
            if (D.time <= 0) { D.time = 0; if (offerDriveRevive()) return; D.phase = "over"; sfx("end"); return; }

            // Stick: up is gas, down is brake then reverse, sideways steers. Drift (or Space) loosens the grip.
            var mv = readMove();
            double throttle = -mv[1], steer = mv[0]; bool hand = D.hand;
            double fx = Math.Cos(c.a), fy = Math.Sin(c.a);
            double vf = c.vx * fx + c.vy * fy, vl = -c.vx * fy + c.vy * fx;
            int ground = city.at(c.x, c.y);
            if (c.air <= 0)
            {
                if (throttle > 0.05) vf += CAR_ACC * throttle * dt * (vf < 0 ? 2.5 : 1);
                else if (throttle < -0.05) vf += (vf > 0.3 ? -CAR_BRAKE : -CAR_ACC * 0.6) * -throttle * dt;
                double grip = Math.Max(-1, Math.Min(1, vf / 3));   // you need speed to turn; steering flips in reverse
                c.a += steer * CAR_TURN * grip * dt * (hand ? 1.4 : 1);
                // Oil: almost no grip and a bit of a spin for a moment.
                c.slip = Math.Max(0, c.slip - dt);
                if (c.slip > 0) c.a += dt * 3.2 * (vf != 0 ? Math.Sign(vf) : 1) * Math.Min(1, Math.Abs(vf) / 4);
                bool tyres = vanUp("tyres");
                vl *= Math.Exp(-(c.slip > 0 ? (tyres ? 0.8 : 0.4) : hand ? (tyres ? 2 : 1.4) : (tyres ? 11 : 9)) * dt);
                bool rough = ground == GRASS || ground == DIRT;
                vf *= Math.Exp(-(rough ? 1.5 : hand ? 0.7 : 0.22) * dt);
                vf = Math.Max(-CAR_REV, Math.Min(CAR_MAX * (vanUp("engine") ? 1.1 : 1) * (rough ? 0.6 : 1), vf));
            }
            else
            {
                c.air -= dt;
                if (c.air <= 0) landJump();
            }
            double nfx = Math.Cos(c.a), nfy = Math.Sin(c.a);
            c.vx = vf * nfx - vl * nfy; c.vy = vf * nfy + vl * nfx;

            // Move, sliding along walls.
            double nx = c.x + c.vx * dt, ny = c.y + c.vy * dt;
            if (c.air > 0) { c.x = Math.Max(0.5, Math.Min(city.n - 0.5, nx)); c.y = Math.Max(0.5, Math.Min(city.n - 0.5, ny)); }
            else
            {
                if (!driveSolid(nx, c.y)) c.x = nx; else { driveBump(Math.Abs(c.vx)); c.vx *= -0.3; }
                if (!driveSolid(c.x, ny)) c.y = ny; else { driveBump(Math.Abs(c.vy)); c.vy *= -0.3; }
            }
            double speed = U.Hypot(c.vx, c.vy);

            // Drifts: a long slide pays out when you straighten up.
            if (c.air <= 0 && Math.Abs(vl) > 1.8 && Math.Abs(vf) > 3.5)
            {
                D.driftT += dt;
                if (Random() < 0.6) foreach (int s in new[] { -1, 1 }) D.skids.Add(new DSkid { x = c.x - nfx * 0.35 + s * -nfy * 0.22, y = c.y - nfy * 0.35 + s * nfx * 0.22, life = 1 });
            }
            else if (D.driftT > 0)
            {
                if (D.driftT > 0.6) { double b = U.Round(D.driftT * 3); D.coins += b; D.stunts += b; drivePop("Drift! +" + U.S(b), null, "#8ad65e"); sfx("chopped"); }
                D.driftT = 0;
            }

            // Ramps launch you if you hit them fast and roughly straight.
            if (c.air <= 0) foreach (var rp in city.ramps)
            {
                if (Math.Floor(c.x) != rp.x || Math.Floor(c.y) != rp.y) continue;
                double along = rp.horiz ? Math.Abs(c.vx) : Math.Abs(c.vy);
                if (along > 4) { c.air = c.airMax = 0.45 + along * 0.07; sfx("swap"); }
            }

            // Street clutter goes flying.
            if (c.air <= 0) foreach (var p in city.props)
            {
                if (p.hit || U.Hypot(p.x - c.x, p.y - c.y) > 0.5 || speed < 1.5) continue;
                p.hit = true; p.vx = c.vx * 1.3 + (Random() - 0.5) * 3; p.vy = c.vy * 1.3 + (Random() - 0.5) * 3; p.spin = (Random() - 0.5) * 20;
                c.vx *= 0.93; c.vy *= 0.93;
                D.coins += 1; D.smashed++; D.stunts += 1;
                drivePop("Smash! +1", null, "#fff5de"); noise(0.12, 0.3, 1800);
            }

            updateTraffic(dt);
            updatePeds(dt);
            updateHazards();

            // Deliveries: stop in the green ring to hand the bag over.
            D.orderT += dt;
            var tg = D.target; double dist = U.Hypot(tg.x - c.x, tg.y - c.y);
            if (dist < 1.5 && speed < 3 && c.air <= 0)
            {
                if (tg.truck)
                {
                    D.bags = D.cap > 0 ? D.cap : 3; D.time += 3;
                    drivePop("Bags loaded!", "+3s", "#6fb0d6"); sfx("pick");
                }
                else
                {
                    double tip = Math.Max(0, U.Round(10 * (1 - D.orderT / D.par)));
                    double earned = 12 + tip, bonus = tip >= 5 ? 10 : 6;
                    D.coins += earned; D.tips += tip; D.delivered++; D.bags--; D.time += bonus;
                    drivePop("+" + U.S(earned), $"{(tip >= 5 ? "Speedy delivery!" : "Delivered")}  +{U.S(bonus)}s"); sfx("serve"); buzz(25);
                }
                nextDrop();
            }
            engineSet(Math.Min(1, speed / CAR_MAX));

            // Camera: follows a little ahead of the car and turns with it.
            double da = c.a - D.cam.a;
            while (da > Math.PI) da -= Math.PI * 2;
            while (da < -Math.PI) da += Math.PI * 2;
            D.cam.a += da * Math.Min(1, dt * 5);
            double look = Math.Min(2.2, speed * 0.25);
            D.cam.x += (c.x + nfx * look - D.cam.x) * Math.Min(1, dt * 8);
            D.cam.y += (c.y + nfy * look - D.cam.y) * Math.Min(1, dt * 8);
        }

        void landJump()
        {
            var c = D.car; var city = D.city;
            int k0 = city.at(c.x, c.y);
            if (k0 == BLDG || k0 == TREE || k0 == BARR)
            {
                // Came down on a roof: drop onto the nearest street.
                double[] best = null; double bd = 1e9;
                int fx = (int)Math.Floor(c.x), fy = (int)Math.Floor(c.y);
                for (int dy = -4; dy <= 4; dy++) for (int dx = -4; dx <= 4; dx++)
                {
                    int k = city.at(fx + dx, fy + dy); double d = U.Hypot(dx, dy);
                    if ((k == RD || k == WALK || k == GRASS || k == DIRT) && d < bd) { bd = d; best = new double[] { fx + dx + 0.5, fy + dy + 0.5 }; }
                }
                if (best != null) { c.x = best[0]; c.y = best[1]; }
            }
            double b = U.Round(c.airMax * 10);
            D.coins += b; D.stunts += b;
            drivePop("Big air! +" + U.S(b), null, "#dcb8ea");
            D.shake = 0.25; noise(0.2, 0.4, 250);
            for (int i = 0; i < 8; i++) D.puffs.Add(new DPuff { x = c.x, y = c.y, vx = (Random() - 0.5) * 3, vy = (Random() - 0.5) * 3, life = 0.5 });
        }

        // Road ahead is closed for works (or the city ends): look a few tiles along.
        bool laneClosed(double x, double y, string dir, int reach = 3)
        {
            var d = DIRV[dir];
            for (int s = 1; s <= reach; s++) { int k = D.city.at(x + d[0] * s, y + d[1] * s); if (k == BARR || k == BLDG || k == DIRT) return true; }
            return false;
        }
        void wreckCar(DTraffic t)
        {
            t.wreck = 20; t.v = 0;
            for (int i = 0; i < 6; i++) D.puffs.Add(new DPuff { x = t.x, y = t.y, vx = (Random() - 0.5) * 2, vy = (Random() - 0.5) * 2, life = 0.6 });
        }

        static readonly Dictionary<string, Dictionary<int, string>> TURNS = new Dictionary<string, Dictionary<int, string>>
        {
            ["E"] = new Dictionary<int, string> { [0] = "S", [1] = "N" }, ["W"] = new Dictionary<int, string> { [1] = "N", [0] = "S" },
            ["N"] = new Dictionary<int, string> { [1] = "E", [0] = "W" }, ["S"] = new Dictionary<int, string> { [0] = "W", [1] = "E" },
        };
        static readonly Dictionary<string, string> RIGHT = new Dictionary<string, string> { ["E"] = "S", ["W"] = "N", ["N"] = "E", ["S"] = "W" };
        static readonly Dictionary<string, string> BACK = new Dictionary<string, string> { ["E"] = "W", ["W"] = "E", ["N"] = "S", ["S"] = "N" };

        void updateTraffic(double dt)
        {
            var c = D.car; int n = D.city.n; const double near = 26;
            foreach (var t in D.traffic)
            {
                // Far away traffic takes it easy (cheaper, and you can't see it anyway).
                bool far = Math.Abs(t.x - c.x) > near || Math.Abs(t.y - c.y) > near;
                if (t.wreck > 0)
                {
                    t.wreck -= dt;
                    if (!far && Random() < dt * 3) D.puffs.Add(new DPuff { x = t.x + (Random() - 0.5) * 0.3, y = t.y, vx = (Random() - 0.5) * 0.4, vy = -0.6, life = 0.9, smoke = true });
                    // The tow truck takes it away; a fresh car joins somewhere else.
                    if (t.wreck <= 0)
                    {
                        var f = spawnTraffic(Random);
                        t.x = f.x; t.y = f.y; t.dir = f.dir; t.v = f.v; t.vmax = f.vmax; t.color = f.color; t.stopT = f.stopT; t.nearT = f.nearT; t.wreck = f.wreck; t.reckless = f.reckless;
                    }
                    else { crashCheck(t, c, far); continue; }
                }
                var dv = DIRV[t.dir]; int dx = dv[0], dy = dv[1];
                t.nearT -= dt; t.stopT -= dt;
                // Slow for anything just ahead in the lane: cars, wrecks, you, and people on the crosswalk.
                bool blocked = t.stopT > 0 || laneClosed(t.x, t.y, t.dir, 1);
                if (!blocked) foreach (var o in D.traffic)
                {
                    if (o == t || Math.Abs(o.x - t.x) > 2 || Math.Abs(o.y - t.y) > 2) continue;
                    if (t.reckless && !(o.wreck > 0) && o.dir != t.dir) continue;
                    double rx = o.x - t.x, ry = o.y - t.y, along = rx * dx + ry * dy, side = Math.Abs(rx * dy - ry * dx);
                    if (along > 0.2 && along < 1.5 && side < 0.6) { blocked = true; break; }
                }
                if (!blocked && !far)
                {
                    if (aheadOf(t, c.x, c.y, dx, dy)) blocked = true;
                    else foreach (var o in D.peds)
                    {
                        if (o.down > 0) continue;
                        if (aheadOf(t, o.x, o.y, dx, dy)) { blocked = true; break; }
                    }
                }
                t.v += ((blocked ? 0 : t.vmax) - t.v) * Math.Min(1, dt * 3);
                // Stuck at a closed road for a while: turn round into the other lane.
                t.waitT = blocked && laneClosed(t.x, t.y, t.dir, 1) ? t.waitT + dt : 0;
                if (t.waitT > 1)
                {
                    string back = BACK[t.dir];
                    if (dx != 0) t.y += t.dir == "E" ? -1 : 1; else t.x += t.dir == "N" ? -1 : 1;
                    t.dir = back; t.waitT = 0; continue;
                }
                double prev = dx != 0 ? t.x : t.y;
                t.x += dx * t.v * dt; t.y += dy * t.v * dt;
                double next = dx != 0 ? t.x : t.y;
                // Crossing the middle of a lane of the cross street: maybe turn onto it.
                double pc = prev - 0.5, nc = next - 0.5;
                if (Math.Floor(pc) != Math.Floor(nc))
                {
                    int line = (int)(dx + dy > 0 ? Math.Floor(nc) : Math.Floor(pc)), m = line % 8;
                    string right = RIGHT[t.dir];
                    if (TURNS[t.dir].TryGetValue(m, out var to))
                    {
                        double cx = dx != 0 ? line + 0.5 : t.x, cy = dy != 0 ? line + 0.5 : t.y;
                        bool must = (t.dir == "E" && line >= n - 1) || (t.dir == "W" && line <= 0) || (t.dir == "N" && line <= 0) || (t.dir == "S" && line >= n - 1) || laneClosed(cx, cy, t.dir, 4);
                        var tv = DIRV[to];
                        bool ok = D.city.at(cx + tv[0], cy + tv[1]) == RD && !laneClosed(cx, cy, to, 4);
                        if (ok && (must || Random() < (to == right ? 0.3 : 0.2))) { t.x = cx; t.y = cy; t.dir = to; }
                    }
                }
                t.x = Math.Max(0.5, Math.Min(n - 0.5, t.x)); t.y = Math.Max(0.5, Math.Min(n - 0.5, t.y));
                // Two cars meeting side-on at a junction: an accident that blocks the road for a while.
                if (!far) foreach (var o in D.traffic)
                {
                    if (o == t || o.wreck > 0 || Math.Abs(o.x - t.x) > 0.6 || Math.Abs(o.y - t.y) > 0.6) continue;
                    bool cross = (DIRV[o.dir][0] != 0) != (dx != 0);
                    if (cross && U.Hypot(o.x - t.x, o.y - t.y) < 0.5 && t.v + o.v > 2)
                    {
                        wreckCar(t); wreckCar(o);
                        if (U.Hypot(t.x - c.x, t.y - c.y) < 12) { noise(0.3, 0.3, 400); drivePop("Accident!", "Find another way round", "#f59a2e"); }
                    }
                }
                crashCheck(t, c, far);
            }
        }
        static bool aheadOf(DTraffic t, double ox, double oy, int dx, int dy)
        {
            if (Math.Abs(ox - t.x) > 2 || Math.Abs(oy - t.y) > 2) return false;
            double rx = ox - t.x, ry = oy - t.y, along = rx * dx + ry * dy, side = Math.Abs(rx * dy - ry * dx);
            return along > 0.2 && along < 1.5 && side < 0.6;
        }

        // You and one car: bumps, big hits that wreck it, and near misses.
        void crashCheck(DTraffic t, DCar c, bool far)
        {
            if (far || c.air > 0) return;
            double d = U.Hypot(t.x - c.x, t.y - c.y), speed = U.Hypot(c.vx, c.vy);
            if (d < 0.72)
            {
                double dd = d != 0 ? d : 1, ux = (c.x - t.x) / dd, uy = (c.y - t.y) / dd;
                c.x = t.x + ux * 0.74; c.y = t.y + uy * 0.74;
                double into = -(c.vx * ux + c.vy * uy);
                if (into > 0) { c.vx += ux * into * 1.3; c.vy += uy * into * 1.3; driveBump(into); }
                if (into > 5 && !(t.wreck > 0)) { wreckCar(t); drivePop("Wreck!", null, "#ff6b57"); }
                t.stopT = 1.2; t.nearT = 3;
            }
            else if (d < 1.25 && speed > 5 && t.nearT <= 0 && !(t.wreck > 0))
            {
                t.nearT = 4;
                D.coins += 2; D.stunts += 2;
                drivePop("Near miss! +2", null, "#6fb0d6"); sfx("hop");
            }
        }

        // People walk tile to tile along the sidewalks, dive out of your way, and get knocked over if they don't.
        void updatePeds(double dt)
        {
            var c = D.car; double speed = U.Hypot(c.vx, c.vy), fx = Math.Cos(c.a), fy = Math.Sin(c.a);
            D.ouch = Math.Max(0, D.ouch - dt);
            foreach (var p in D.peds)
            {
                if (Math.Abs(p.x - c.x) > 22 || Math.Abs(p.y - c.y) > 22) continue;
                p.safe -= dt;
                if (p.down > 0)
                {
                    p.down -= dt; p.x += p.dvx * dt; p.y += p.dvy * dt; p.dvx *= Math.Exp(-4 * dt); p.dvy *= Math.Exp(-4 * dt);
                    if (p.down <= 0) { p.tx = (int)Math.Floor(p.x); p.ty = (int)Math.Floor(p.y); if (!pedTile(p.tx, p.ty)) { p.tx = p.px; p.ty = p.py; } p.safe = 2; }
                    continue;
                }
                if (p.dodge > 0) { p.dodge -= dt; p.x += p.dvx * dt; p.y += p.dvy * dt; if (p.dodge <= 0) { p.tx = (int)Math.Floor(p.x); p.ty = (int)Math.Floor(p.y); if (!pedTile(p.tx, p.ty)) { p.tx = p.px; p.ty = p.py; } } continue; }
                // Saw you coming: a quick dive to the side.
                double rx = p.x - c.x, ry = p.y - c.y, along = rx * fx + ry * fy, side = rx * -fy + ry * fx;
                if (speed > 3 && along > 0 && along < 2.4 && Math.Abs(side) < 0.8 && c.air <= 0 && !p.tried)
                {
                    p.tried = true;
                    if (Random() < 0.55) { double s = side >= 0 ? 1 : -1; p.dodge = 0.3; p.dvx = -fy * s * 4; p.dvy = fx * s * 4; continue; }
                }
                if (along < -1 || U.Hypot(rx, ry) > 3) p.tried = false;
                // Walk to the middle of the next tile, then pick another (not straight back if there's a choice).
                double gx = p.tx + 0.5, gy = p.ty + 0.5, dd = U.Hypot(gx - p.x, gy - p.y);
                if (dd < 0.05)
                {
                    var opts = DIRS4.Select(o => new[] { p.tx + o[0], p.ty + o[1] }).Where(q => pedTile(q[0], q[1])).ToList();
                    var fwd = opts.Where(q => q[0] != p.px || q[1] != p.py).ToList();
                    var from = fwd.Count > 0 ? fwd : opts;
                    int idx = (int)Math.Floor(Random() * (fwd.Count > 0 ? fwd.Count : opts.Count));
                    if (idx < from.Count) { var pick = from[idx]; p.px = p.tx; p.py = p.ty; p.tx = pick[0]; p.ty = pick[1]; }
                }
                else { p.x += (gx - p.x) / dd * Math.Min(dd, p.v * dt); p.y += (gy - p.y) / dd * Math.Min(dd, p.v * dt); }
            }
            // Knocked over: it costs you, and they get up a bit dizzy.
            if (c.air > 0 || speed < 1.2) return;
            foreach (var p in D.peds)
            {
                if (p.down > 0 || p.safe > 0 || Math.Abs(p.x - c.x) > 0.6 || Math.Abs(p.y - c.y) > 0.6) continue;
                if (U.Hypot(p.x - c.x, p.y - c.y) > 0.45) continue;
                p.down = 3; p.dvx = c.vx * 0.8; p.dvy = c.vy * 0.8; p.tried = false;
                c.vx *= 0.8; c.vy *= 0.8;
                double lost = Math.Min(D.coins, PED_PENALTY);
                D.coins -= lost; D.hits++; D.hitLoss += lost;
                drivePop(lost != 0 ? "-" + U.S(lost) : "Watch out!", "You hit someone!", "#ff6b57");
                sfx("fail"); buzz(40); D.shake = 0.3;
            }
        }

        // Potholes and oil slicks.
        void updateHazards()
        {
            var c = D.car;
            if (c.air > 0) return;
            double speed = U.Hypot(c.vx, c.vy);
            foreach (var h in D.city.hazards)
            {
                if (Math.Abs(h.x - c.x) > 0.5 || Math.Abs(h.y - c.y) > 0.5) { if (h.on) h.on = false; continue; }
                if (h.on) continue;
                h.on = true;
                if (h.kind == "pothole" && speed > 2) { c.vx *= 0.8; c.vy *= 0.8; driveBump(4); drivePop("Pothole!", null, "#c2c6cf"); }
                else if (h.kind == "oil" && speed > 2) { c.slip = 1.2; drivePop("Oil!", "Hold on", "#9a6ab3"); sfx("swap"); }
            }
            // Dumpsters are solid.
            foreach (var o in D.city.solids)
            {
                double dx = c.x - o.x, dy = c.y - o.y, d = U.Hypot(dx, dy), min = o.r + CAR_R;
                if (d >= min || d == 0) continue;
                c.x = o.x + dx / d * min; c.y = o.y + dy / d * min;
                double into = -(c.vx * dx + c.vy * dy) / d;
                if (into > 0) { c.vx += dx / d * into * 1.4; c.vy += dy / d * into * 1.4; driveBump(into); }
            }
        }

        /* ---------- engine hum (played by the host from engineLevel) ---------- */

        void engineStart()
        {
            engineStop();
            if (save.muted) return;
            D.eng = true;
            engineLevel = 0;
        }
        void engineSet(double v)
        {
            if (D == null || !D.eng) return;
            bool on = !paused && !save.muted;
            engineLevel = on ? Math.Max(0.001, Math.Min(1, v)) : 0;   // idle still hums
        }
        void engineStop()
        {
            engineLevel = 0;
            if (D == null || !D.eng) return;
            D.eng = false;
        }

        /* ---------- drawing ---------- */

        struct DriveView { public double cx, cy, s, rot; }
        double driveScale() => Math.Max(26, Math.Min(44, Math.Min(W, H) / 11));
        DriveView driveView() => new DriveView { cx = W / 2, cy = H * (L.land ? 0.7 : 0.6), s = driveScale(), rot = -D.cam.a - Math.PI / 2 };

        static readonly double[][] ARROW = { new double[] { 22, 0 }, new double[] { 2, -16 }, new double[] { 2, -7 }, new double[] { -18, -7 }, new double[] { -18, 7 }, new double[] { 2, 7 }, new double[] { 2, 16 } };

        void renderDrive(double dt)
        {
            var v = driveView(); var c = D.car; var cam = D.cam;
            X.fillStyle = "#2a3150"; X.fillRect(0, 0, W, H);
            X.save();
            double sh = D.shake > 0 && save.shake ? D.shake * 10 : 0;
            X.translate(v.cx + (Random() - 0.5) * sh, v.cy + (Random() - 0.5) * sh);
            X.rotate(v.rot); X.scale(v.s, v.s); X.translate(-cam.x, -cam.y);
            X.imageSmoothingEnabled = false;
            drawCity();
            // Ramps
            foreach (var rp in D.city.ramps)
            {
                X.fillStyle = "#2b1d2a"; X.fillRect(rp.x + 0.08, rp.y + 0.08, 0.84, 0.84);
                for (int i = 0; i < 4; i++)
                {
                    X.fillStyle = i % 2 != 0 ? "#2b1d2a" : "#f5d33e";
                    if (rp.horiz) X.fillRect(rp.x + 0.12 + i * 0.19, rp.y + 0.12, 0.17, 0.76); else X.fillRect(rp.x + 0.12, rp.y + 0.12 + i * 0.19, 0.76, 0.17);
                }
            }
            foreach (var s in D.skids) { X.fillStyle = "rgba(20,20,30," + U.S(0.45 * s.life) + ")"; X.fillRect(s.x - 0.05, s.y - 0.05, 0.1, 0.1); }
            // Target ring, customer or truck
            var tg = D.target; double pulse = 1 + Math.Sin(T * 6) * 0.08;
            X.strokeStyle = tg.truck ? "rgba(111,176,214,.9)" : "rgba(138,214,94,.9)"; X.lineWidth = 0.14;
            X.beginPath(); X.arc(tg.x, tg.y, 1.3 * pulse, 0, Math.PI * 2); X.stroke();
            X.fillStyle = tg.truck ? "rgba(111,176,214,.18)" : "rgba(138,214,94,.18)"; X.fill();
            var tr = D.city.truck;
            X.save(); X.translate(tr.x, tr.y); X.rotate(-v.rot); spr("truckTop", 0, 0, P * 1.2); X.restore();
            if (!tg.truck) { X.save(); X.translate(tg.x, tg.y); X.rotate(-v.rot); spr("person", 0, -0.1 - Math.Abs(Math.Sin(T * 8)) * 0.1, P * 1.3, S("1", "#e8472f")); X.restore(); }
            // Street clutter (upright to the camera unless it's flying)
            bool vis(double x, double y) => Math.Abs(x - cam.x) < 16 && Math.Abs(y - cam.y) < 16;
            foreach (var o in D.city.solids) { if (!vis(o.x, o.y)) continue; X.save(); X.translate(o.x, o.y); X.rotate(-v.rot); spr("dumpster", 0, 0, P * 1.3, S("N", "#2e6e34", "n", "#4fa843")); X.restore(); }
            foreach (var p in D.city.props)
            {
                if (p.life <= 0 || !vis(p.x, p.y)) continue;
                X.save(); X.translate(p.x, p.y); X.rotate(p.hit ? p.rot : -v.rot); X.globalAlpha = Math.Min(1, p.life * 2);
                spr(p.kind, 0, 0, P * 1.2); X.restore();
            }
            X.globalAlpha = 1;
            // Traffic
            foreach (var t in D.traffic)
            {
                if (!vis(t.x, t.y)) continue;
                bool wr = t.wreck > 0;
                X.save(); X.translate(t.x, t.y); X.rotate(Math.Atan2(DIRV[t.dir][1], DIRV[t.dir][0]) + Math.PI / 2 + (wr ? 0.35 : 0));
                X.fillStyle = "rgba(0,0,0,.25)"; X.fillRect(-0.3, -0.38, 0.66, 0.9);
                string col = wr ? Css.Mix(t.color, "#2b1d2a", 0.45) : t.color;
                spr("car", 0, 0, P, S("1", col, "2", Css.Mix(col, "#ffffff", 0.3)));
                if (wr && Math.Sin(T * 10) > 0) { X.fillStyle = "#f59a2e"; X.fillRect(-0.34, -0.44, 0.12, 0.12); X.fillRect(0.22, -0.44, 0.12, 0.12); X.fillRect(-0.34, 0.36, 0.12, 0.12); X.fillRect(0.22, 0.36, 0.12, 0.12); }
                X.restore();
            }
            // People: upright to the camera, flat on their back when knocked over.
            foreach (var p in D.peds)
            {
                if (!vis(p.x, p.y)) continue;
                X.save(); X.translate(p.x, p.y); X.rotate(-v.rot + (p.down > 0 ? Math.PI / 2 : 0));
                double hop = p.down > 0 || p.dodge > 0 ? 0 : Math.Abs(Math.Sin(T * 9 + p.x * 3)) * 0.04;
                spr("person", 0, -hop, P * 0.95, S("1", p.shirt));
                X.restore();
                if (p.down > 0 || p.safe > 0) { X.save(); X.translate(p.x, p.y); X.rotate(-v.rot + T * 4); spr("dizzy", 0, 0, P * 0.9); X.restore(); }
            }
            // You: bigger in the air, with the shadow left on the ground.
            double z = c.air > 0 ? Math.Sin(Math.PI * (1 - c.air / c.airMax)) : 0;
            X.save(); X.translate(c.x, c.y); X.rotate(c.a + Math.PI / 2);
            X.fillStyle = "rgba(0,0,0,.3)"; X.fillRect(-0.3 + z * 0.3, -0.4 + z * 0.3, 0.64, 0.9);
            X.scale(1 + z * 0.45, 1 + z * 0.45);
            spr("car", 0, 0, P, S("1", "#e8472f", "2", "#fff5de"));
            X.restore();
            foreach (var p in D.puffs) { X.fillStyle = p.smoke ? "rgba(70,70,80," + U.S(Math.Max(0, p.life * 0.6)) + ")" : "rgba(230,220,200," + U.S(Math.Max(0, p.life * 1.4)) + ")"; double s = p.smoke ? 0.22 + (0.9 - p.life) * 0.3 : 0.16; X.fillRect(p.x - s / 2, p.y - s / 2, s, s); }
            X.restore();

            // Guidance arrow over the car, pointing at the drop-off.
            double ang = Math.Atan2(tg.y - c.y, tg.x - c.x) + v.rot, dist = U.Hypot(tg.x - c.x, tg.y - c.y);
            double ax = v.cx, ay = v.cy - v.s * 1.9;
            X.save(); X.translate(ax, ay); X.rotate(ang);
            string acol = tg.truck ? "#6fb0d6" : "#8ad65e";
            X.fillStyle = PAL('k'); X.beginPath(); for (int i = 0; i < ARROW.Length; i++) { if (i != 0) X.lineTo(ARROW[i][0] * 1.15, ARROW[i][1] * 1.15); else X.moveTo(ARROW[i][0] * 1.15, ARROW[i][1] * 1.15); } X.closePath(); X.fill();
            X.fillStyle = acol; X.beginPath(); for (int i = 0; i < ARROW.Length; i++) { if (i != 0) X.lineTo(ARROW[i][0], ARROW[i][1]); else X.moveTo(ARROW[i][0], ARROW[i][1]); } X.closePath(); X.fill();
            X.restore();
            X.font = "700 14px \"Pixelify Sans\", \"Courier New\", monospace"; X.textAlign = "center"; X.textBaseline = "middle";
            X.lineWidth = 4; X.strokeStyle = "#1c2133"; string lbl = (tg.truck ? "Truck " : "") + U.S(U.Round(dist * 10)) + " m";
            X.strokeText(lbl, ax, ay + 30); X.fillStyle = "#fff5de"; X.fillText(lbl, ax, ay + 30);

            D.popY = D.tut != null ? drawDriveTut(v) + 30 : (double?)null;
            drawDriveHUD();
            drawDriveControls();
            if (D.tut != null) drawDriveTut(v);
            if (D.phase == "count") driveBanner(D.count > 0.2 ? U.S(Math.Ceiling(D.count)) : "Go!");
            else if (D.phase == "over") driveBanner("Time's up!");
        }

        void driveBanner(string text)
        {
            X.font = "400 64px \"Pixelify Sans\", system-ui, sans-serif"; X.textAlign = "center"; X.textBaseline = "middle";
            X.lineWidth = 8; X.strokeStyle = "#1c2133"; X.strokeText(text, W / 2, H * 0.4); X.fillStyle = "#fff5de"; X.fillText(text, W / 2, H * 0.4);
        }

        void drawDriveHUD()
        {
            var b = L.bar;
            X.fillStyle = "#2a3150"; X.fillRect(L.pause.x - 18, L.pause.y - 18, 36, 36);
            X.fillStyle = "#fff5de"; X.fillRect(L.pause.x - 7, L.pause.y - 8, 5, 16); X.fillRect(L.pause.x + 2, L.pause.y - 8, 5, 16);
            bool low = D.time <= 10;
            X.font = "400 26px \"Pixelify Sans\", system-ui, sans-serif"; X.textAlign = "center"; X.textBaseline = "middle";
            X.fillStyle = low && Math.Sin(T * 8) > 0 ? "#e8472f" : "#2a3150"; X.fillRect(U.Round(W / 2 - 48), b.y, 96, 38);
            X.fillStyle = "#fff5de"; X.fillText(U.FmtTime(Math.Ceiling(D.time)), W / 2, b.y + 20);
            double cx = b.x + b.w - 14, cy = b.y + 20;
            X.font = "400 24px \"Pixelify Sans\", system-ui, sans-serif"; X.textAlign = "right";
            X.fillStyle = "#fff5de"; X.fillText(U.S(D.coins), cx - 26, cy + 1);
            spr("coin", cx - 8, cy, 3);
            // Bags left, under the pause button
            double cap = D.cap > 0 ? D.cap : 3;
            for (int i = 0; i < cap; i++) { X.globalAlpha = i < D.bags ? 1 : 0.25; spr("bag", L.pause.x + i * 30, b.y + 64, 2.2); }
            X.globalAlpha = 1;
            X.font = "700 13px \"Nunito\", system-ui, sans-serif"; X.textAlign = "left"; X.fillStyle = "#fff5de";
            X.fillText(D.bags != 0 ? $"{U.S(D.delivered)} delivered" : "Back to the truck!", L.pause.x - 16, b.y + 92);
            // Corner map (north up): you, the drop-off and the truck.
            double ms = Math.Min(96, W * 0.24), mx = b.x + b.w - ms, my = b.y + 46;
            X.fillStyle = PAL('k'); X.fillRect(mx - 3, my - 3, ms + 6, ms + 6);
            X.imageSmoothingEnabled = false; X.drawImage(miniSlot, 0, 0, D.city.n, D.city.n, mx, my, ms, ms);
            double k = ms / D.city.n;
            void dot(double x, double y, string col, double r) { X.fillStyle = PAL('k'); X.fillRect(mx + x * k - r - 1, my + y * k - r - 1, r * 2 + 2, r * 2 + 2); X.fillStyle = col; X.fillRect(mx + x * k - r, my + y * k - r, r * 2, r * 2); }
            dot(D.city.truck.x, D.city.truck.y, "#6fb0d6", 2);
            if (!D.target.truck && Math.Sin(T * 8) > -0.3) dot(D.target.x, D.target.y, "#8ad65e", 3);
            dot(D.car.x, D.car.y, "#e8472f", 2.5);
            // Pop-ups
            for (int i = 0; i < D.pops.Count; i++)
            {
                var p = D.pops[i];
                double y = (D.popY ?? b.y + 130) + i * 44 - (1.6 - p.life) * 12;
                X.globalAlpha = Math.Min(1, p.life * 2);
                X.font = "400 28px \"Pixelify Sans\", system-ui, sans-serif"; X.textAlign = "center";
                X.lineWidth = 5; X.strokeStyle = "#1c2133"; X.strokeText(p.text, W / 2, y); X.fillStyle = p.color; X.fillText(p.text, W / 2, y);
                if (p.sub != null) { X.font = "800 13px Nunito, sans-serif"; X.lineWidth = 4; X.strokeText(p.sub, W / 2, y + 20); X.fillStyle = "#fff5de"; X.fillText(p.sub, W / 2, y + 20); }
                X.globalAlpha = 1;
            }
        }

        void drawDriveControls()
        {
            var j = input.joy;
            double jx = j.id != null ? j.bx : L.joyRest.x, jy = j.id != null ? j.by : L.joyRest.y;
            X.globalAlpha = j.id != null ? 0.9 : 0.45;
            pixCircle(jx, jy, JOY_R, "rgba(255,245,222,.18)");
            var mv = readMove();
            pixCircle(jx + mv[0] * JOY_R * 0.8, jy + mv[1] * JOY_R * 0.8, 24, "rgba(255,245,222,.55)");
            X.globalAlpha = 1;
            if (j.id == null)
            {
                X.font = "700 12px \"Nunito\", system-ui, sans-serif"; X.textAlign = "center"; X.fillStyle = "rgba(255,245,222,.7)";
                X.fillText("Drag: up gas, down brake", jx, jy + JOY_R + 18);
            }
            drawButton(L.btnA, L.ctrlR, "#6fb0d6", "#3f6f8f", "Drift", null, false, D.hand ? 0.6 : 0);
        }

        // drivePointer: the pause button, the lesson card, the Drift button, then the stick.
        void drivePointerDown(int id, double x, double y)
        {
            if (paused || D.phase == "over") return;
            if (dist(L.pause, x, y) < 30) { pauseGame(); return; }
            if (tutTapDrive(x, y)) return;
            if (dist(L.btnA, x, y) < L.ctrlR * 1.3) { D.hand = true; D.handId = id; return; }
            if (input.joy.id == null && stickSide(x, 0.62)) { input.joy.id = id; input.joy.bx = x; input.joy.by = y; input.joy.x = x; input.joy.y = y; }
        }
        void drivePointerMove(int id, double x, double y) { }   // the stick follows in PointerMove
        void drivePointerUp(int id)
        {
            if (D != null && D.handId == id) { D.hand = false; D.handId = null; }
        }
        // Enter presses the lesson card's Next / Go! button.
        void driveEnterKey()
        {
            if (D.tut != null && Data.DRIVE_TUT[D.tut.i].next && D.tut.next != null) tutTapDrive(D.tut.next.x + 1, D.tut.next.y + 1);
        }

        void driveResults()
        {
            int k = D.k; var run = D.run; int stars = driveStars(k, D.coins);
            bool best = !save.drive.Has(k) || D.coins > save.drive[k];
            if (best) { save.drive[k] = D.coins; persist(); }
            var pay = settle(D.attempt, D.coins, stars, run.stars[2]);
            countLevelForAds();
            var m = new ResultsModel
            {
                wallet = pay.text,
                no = "Delivery Run " + (k + 1),
                over = "Time's up",
                name = run.name,
                stars = stars,
                l1 = "Deliveries", served = U.S(D.delivered),
                l2 = "People knocked over", failed = U.S(D.hits) + (D.hitLoss != 0 ? $" (−{U.S(D.hitLoss)})" : ""),
                tips = U.S(D.tips),
                score = U.S(D.coins),
                next = (best && D.coins != 0 ? "New best! " : $"Best: {U.S(save.drive[k])}. ") + (stars < 3 ? $"{U.S(run.stars[stars])} coins for the next star." : "Three stars!"),
                nextHidden = true, retryHidden = false,
                menuLabel = "All kitchens",
                onRetry = () => startDrive(k),
            };
            results = m;
            show("scr-results");
        }

        public void openDriveIntro(int k)
        {
            var run = Data.DRIVE_RUNS[k];
            var m = new IntroModel
            {
                no = "Delivery Run " + (k + 1),
                time = U.FmtTime(run.time) + " to start",
                name = run.name,
                showMenu = false,
                tip = run.blurb + " Drag on the left to drive: up is gas, down is brake, left and right steer. Hold Drift to slide round corners. Follow the arrow and stop in the green ring to hand over a bag. Every delivery adds time, and after three bags head back to the truck. Drifts, ramps, near misses and knocking over cones all pay coins.",
                note = null,
                stars = "Stars at " + string.Join(" / ", run.stars.Select(U.S)) + " coins",
                coins = coinNote("drive" + k, run.stars[2]),
                driveTutButton = k == 0 && save.driveTut,
                goLabel = k == 0 && !save.driveTut ? "Start the lesson" : "Start driving",
                onGo = () => startDrive(k),
            };
            intro = m;
            show("scr-intro");
        }
    }
}
