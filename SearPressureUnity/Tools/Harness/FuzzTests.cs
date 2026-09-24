using System;
using System.Collections.Generic;
using System.Linq;
using SearPressure;

// Stress test: every kitchen, the daily challenge and each Delivery Run played with random input, then
// random taps through the menus. Looks for exceptions and impossible states (NaN positions, chefs
// outside the kitchen, negative counts, a blank screen with nothing to tap).
static partial class Tests
{
    static int fuzzProblems;
    static void Problem(string what) { fuzzProblems++; if (fuzzProblems <= 60) Console.WriteLine("  PROBLEM " + what); }

    static readonly string[] FuzzKeys = { "KeyW", "KeyA", "KeyS", "KeyD" };

    static void FuzzInput(Game g, Random r)
    {
        foreach (var k in FuzzKeys) g.input.keys[k] = false;
        if (r.NextDouble() < 0.85) { g.input.keys[FuzzKeys[r.Next(4)]] = true; if (r.NextDouble() < 0.3) g.input.keys[FuzzKeys[r.Next(4)]] = true; }
        double a = r.NextDouble();
        if (a < 0.25) { g.KeyDown("Space", false); g.KeyUp("Space"); }
        else if (a < 0.40) { g.KeyDown("KeyE", false); if (r.NextDouble() < 0.5) g.KeyUp("KeyE"); }
        else if (a < 0.47) { g.KeyUp("KeyE"); }
        else if (a < 0.53) { g.KeyDown("KeyQ", false); g.KeyUp("KeyQ"); }
        else if (a < 0.535) { g.KeyDown("Escape", false); g.KeyUp("Escape"); Program.Run(g, 0.05); g.KeyDown("Escape", false); g.KeyUp("Escape"); }
        else if (a < 0.54) { g.Hidden(); g.Blur(); Program.Run(g, 0.05); if (g.paused) g.resumeGame(); }
        else if (a < 0.56)
        {
            // A random touch somewhere on screen (joystick, buttons, tickets).
            double x = r.NextDouble() * g.W, y = r.NextDouble() * g.H; int id = 50 + r.Next(3);
            g.PointerDown(id, x, y); Program.Run(g, 0.03); g.PointerMove(id, x + (r.NextDouble() - 0.5) * 60, y + (r.NextDouble() - 0.5) * 60); Program.Run(g, 0.03); g.PointerUp(id, x, y);
        }
    }

    static bool fuzzSmart;

    // Step next to a random station (as if the player walked there), face it, and use it.
    static void FuzzInteract(Game g, Random r)
    {
        var G = g.G; if (G == null || G.phase != "play") return;
        var c = G.chefs[G.active];
        var targets = G.tiles.Where(t => t.type != "floor").ToList();
        for (int tries = 0; tries < 8; tries++)
        {
            var t = targets[r.Next(targets.Count)];
            var dirs = new[] { (0, 1), (0, -1), (1, 0), (-1, 0) };
            var (dx, dy) = dirs[r.Next(4)];
            var f = G.TileAt(t.x + dx, t.y + dy);
            if (f == null || f.type != "floor") continue;
            // Only if that floor tile is free of the other chef.
            if (G.chefs.Any(o => o != c && Math.Abs(o.x - (f.x + 0.5)) < 0.8 && Math.Abs(o.y - (f.y + 0.5)) < 0.8)) continue;
            c.x = f.x + 0.5; c.y = f.y + 0.5; c.fx = -dx; c.fy = -dy; c.task = null;
            Program.Run(g, 1 / 60.0);
            double a = r.NextDouble();
            if (a < 0.6) { g.KeyDown("Space", false); g.KeyUp("Space"); }
            else if (a < 0.9) { g.KeyDown("KeyE", false); Program.Run(g, 0.4 + r.NextDouble() * 2); g.KeyUp("KeyE"); }
            else { g.KeyDown("KeyQ", false); g.KeyUp("KeyQ"); }
            // Now and then, let cooking run long enough to burn.
            if (r.NextDouble() < 0.05) Program.Run(g, 6, 1 / 30.0);
            return;
        }
    }

    static void CheckKitchen(Game g, string where)
    {
        var G = g.G; if (G == null) return;
        foreach (var c in G.chefs)
        {
            if (double.IsNaN(c.x) || double.IsNaN(c.y) || double.IsInfinity(c.x)) { Problem($"{where}: chef position NaN"); continue; }
            if (c.x < 0 || c.y < 0 || c.x > G.cols || c.y > G.rows) Problem($"{where}: chef outside the kitchen ({c.x:0.00},{c.y:0.00})");
            var t = G.TileAt((int)Math.Floor(c.x), (int)Math.Floor(c.y));
            if (t != null && t.type != "floor" && G.phase == "play") Problem($"{where}: chef standing on a {t.type} at ({c.x:0.00},{c.y:0.00})");
        }
        if (double.IsNaN(G.score) || double.IsNaN(G.time)) Problem($"{where}: score/time NaN");
        if (G.orders.Count > 12) Problem($"{where}: {G.orders.Count} orders piling up");
        if (G.plateTile != null && G.plateTile.count < 0) Problem($"{where}: negative plates {G.plateTile.count}");
        if (G.puffs.Count > 3000) Problem($"{where}: puffs leaking ({G.puffs.Count})");
    }

    static void PlayFuzz(Game g, Random r, string where, double seconds)
    {
        for (double t = 0; t < seconds; t += 0.1)
        {
            try { if (fuzzSmart && r.NextDouble() < 0.45) FuzzInteract(g, r); else FuzzInput(g, r); Program.Run(g, 0.1, 1 / 30.0); }
            catch (Exception e) { Problem($"{where} at {t:0.0}s: {e.GetType().Name}: {e.Message} @ {e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}"); return; }
            if (g.screen == "scr-revive") { try { Tap(g, r.NextDouble() < 0.5 ? "btn-revive" : "btn-revive-no"); } catch (Exception e) { Problem($"{where} revive: {e.Message}"); } }
            if (g.screen == "scr-pause") g.resumeGame();
            CheckKitchen(g, where);
            if (g.G != null && g.G.phase == "over" && g.screen == "scr-results") return;
        }
    }

    static void Fuzz()
    {
        fuzzProblems = 0;
        int seed = 1;
        var levels = Enumerable.Range(0, Data.LEVELS.Count).ToList();
        Console.WriteLine($"Kitchens: {levels.Count}, random input, ~90 s each, portrait and landscape");
        foreach (int lv in levels)
        {
            foreach (bool land in new[] { false, true })
            {
                var r = new Random(seed++);
                var fa = new FakeAds { rewardedReady = r.NextDouble() < 0.7, interstitialReady = r.NextDouble() < 0.7 };
                var g = land ? Program.NewGame(new HarnessPlatform { unlock = true, Ads = fa }, 844, 390, 2) : Program.NewGame(new HarnessPlatform { unlock = true, Ads = fa });
                string where = $"{Data.LEVELS[lv].name}{(land ? " (landscape)" : "")}";
                try
                {
                    Program.Run(g, 0.2);
                    if (land) g.save.lefty = r.NextDouble() < 0.5;
                    g.play(lv); Program.Run(g, 3.7);
                    PlayFuzz(g, r, where, 60);
                    if (g.G.phase != "over") { g.G.time = 0.01; Program.Run(g, 0.2); if (g.screen == "scr-revive") Tap(g, "btn-revive-no"); Program.Run(g, 2.4); }
                    if (g.screen != "scr-results") Problem($"{where}: no results card after time up (screen {g.screen ?? "none"}, phase {g.G?.phase})");
                    // Leave by a random results button.
                    var ids = new[] { "btn-retry", "btn-next", "btn-res-menu" };
                    Tap(g, ids[r.Next(ids.Length)]); Program.Run(g, 0.5);
                    if (g.screen == null && g.G == null && g.D == null) Problem($"{where}: blank screen after results");
                }
                catch (Exception e) { Problem($"{where}: {e.GetType().Name}: {e.Message} @ {e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}"); }
            }
        }
        Console.WriteLine("Daily challenge and the tutorial");
        {
            var r = new Random(seed++);
            var g = Program.NewGame(new HarnessPlatform { unlock = true, Ads = new FakeAds() }); Program.Run(g, 0.2);
            try
            {
                g.dailyLevel(); g.openIntro(Game.DAILY_IDX); Program.Run(g, 0.2); Tap(g, "btn-intro-go"); Program.Run(g, 3.7);
                if (g.G == null) { g.play(Game.DAILY_IDX); Program.Run(g, 3.7); }
                PlayFuzz(g, r, "Daily", 60);
            }
            catch (Exception e) { Problem($"daily: {e.GetType().Name}: {e.Message} @ {e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}"); }
            var g2 = Program.NewGame(new HarnessPlatform()); Program.Run(g2, 0.2);
            try { g2.startTutorial(); Program.Run(g2, 0.5); PlayFuzz(g2, r, "Tutorial", 90); }
            catch (Exception e) { Problem($"tutorial: {e.GetType().Name}: {e.Message} @ {e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}"); }
        }
        Console.WriteLine($"Delivery Runs: {Data.DRIVE_RUNS.Count}");
        for (int k = 0; k < Data.DRIVE_RUNS.Count; k++)
        {
            var r = new Random(seed++);
            var p = DrivePlatform(true); p.Ads = new FakeAds();
            var g = Program.NewGame(p); Program.Run(g, 0.2);
            try
            {
                g.startDrive(k); Program.Run(g, 3.4);
                for (double t = 0; t < 120 && g.screen != "scr-results"; t += 0.1)
                {
                    FuzzInput(g, r); Program.Run(g, 0.1, 1 / 30.0);
                    if (g.screen == "scr-revive") Tap(g, r.NextDouble() < 0.5 ? "btn-revive" : "btn-revive-no");
                    if (g.screen == "scr-pause") g.resumeGame();
                    var c = g.D?.car;
                    if (c != null && (double.IsNaN(c.x) || double.IsNaN(c.y))) { Problem($"drive {k}: car NaN"); break; }
                }
                if (g.screen != "scr-results") Problem($"drive {k}: no results after 120 s (screen {g.screen}, phase {g.D?.phase})");
            }
            catch (Exception e) { Problem($"drive {k}: {e.GetType().Name}: {e.Message} @ {e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}"); }
        }
        Console.WriteLine("Menus: 4000 random taps from a fresh save and from a finished one");
        foreach (bool unlock in new[] { false, true })
        {
            var r = new Random(seed++);
            var fa = new FakeAds { privacy = true };
            var g = Program.NewGame(new HarnessPlatform { unlock = unlock, Ads = fa }); g.onlineEnabled = false; Program.Run(g, 0.2);
            if (unlock) g.save.wallet = 20000;
            var f = typeof(Game).GetField("uiHits", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int blank = 0;
            for (int i = 0; i < 2000; i++)
            {
                try
                {
                    var hits = (List<(string id, Rect r)>)f.GetValue(g);
                    if (g.screen != null && hits.Count > 0 && r.NextDouble() < 0.9)
                    {
                        var h = hits[r.Next(hits.Count)];
                        double x = h.r.x + h.r.w / 2, y = h.r.y + h.r.h / 2;
                        g.PointerDown(9, x, y); Program.Run(g, 1 / 30.0); g.PointerUp(9, x, y); Program.Run(g, 1 / 30.0);
                    }
                    else if (r.NextDouble() < 0.3) { g.KeyDown("Escape", false); g.KeyUp("Escape"); }
                    else { g.Scroll((r.NextDouble() - 0.3) * 400); }
                    Program.Run(g, 0.1);
                    if (g.G != null && g.screen == null) { PlayFuzz(g, r, "menu-started kitchen", 4); if (g.G != null && g.G.phase != "over") { g.pauseGame(); Program.Run(g, 0.1); } }
                    if (g.D != null && g.screen == null) { for (int s = 0; s < 20; s++) { FuzzInput(g, r); Program.Run(g, 0.1); } g.pauseGame(); Program.Run(g, 0.1); }
                    if (g.screen == null && g.G == null && g.D == null) { if (++blank == 1) Problem($"menus: blank screen (nothing drawn, nothing to tap) after tap {i}"); g.show("scr-title"); }
                    if (g.screen != null && ((List<(string id, Rect r)>)f.GetValue(g)).Count == 0) Problem($"menus: screen {g.screen} has nothing to tap");
                }
                catch (Exception e) { Problem($"menus (unlock={unlock}) tap {i} on {g.screen}: {e.GetType().Name}: {e.Message} @ {e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}"); g = Program.NewGame(new HarnessPlatform { unlock = unlock, Ads = fa }); g.onlineEnabled = false; Program.Run(g, 0.2); }
            }
            Console.WriteLine($"  save after fuzzing: wallet {g.save.wallet}, stars {g.totalStars()}, JSON {g.save.ToJson().Length} chars");
            if (g.save.wallet < 0) Problem("menus: negative wallet " + g.save.wallet);
            var back = SaveData.FromJson(g.save.ToJson());
            if (back.wallet != g.save.wallet) Problem("menus: save round trip lost the wallet");
        }
        Console.WriteLine(fuzzProblems == 0 ? "ALL PASSED" : fuzzProblems + " FAILED");
    }
}

static partial class Tests
{
    // Wider sweep: random kitchen × random screen × random save, many seeds.
    static void Fuzz2()
    {
        fuzzSmart = true;
        fuzzProblems = 0; int served = 0, fires = 0, grabs = 0, revives = 0;
        var sizes = new (double w, double h, double d)[] { (320, 480, 1), (320, 568, 2), (360, 800, 3), (393, 852, 3), (412, 915, 2.625), (768, 1024, 2), (1024, 1366, 2), (568, 320, 2), (844, 390, 3), (1280, 800, 1.5) };
        var rng = new Random(4242);
        for (int run = 0; run < 150; run++)
        {
            var (w, h, d) = sizes[rng.Next(sizes.Length)];
            int lv = rng.Next(Data.LEVELS.Count);
            var r = new Random(run * 31 + 7);
            var g = Program.NewGame(new HarnessPlatform { unlock = rng.NextDouble() < 0.8 }, w, h, d);
            string where = $"run {run}: {Data.LEVELS[lv].name} @ {w}x{h}";
            try
            {
                Program.Run(g, 0.2);
                g.save.lefty = rng.NextDouble() < 0.3; g.save.shake = rng.NextDouble() < 0.5; g.save.muted = rng.NextDouble() < 0.3;
                g.save.wallet = rng.Next(0, 30000);
                foreach (var u in Data.UPGRADES) if (rng.NextDouble() < 0.3) g.save.owned[u.id] = true;
                if (rng.NextDouble() < 0.3) g.Resize(h, w, d, 0, 0, 0, 0);   // rotate before playing
                g.play(lv); Program.Run(g, 3.7);
                if (rng.NextDouble() < 0.3) { g.Resize(g.H, g.W, d, 20, 0, 30, 0); Program.Run(g, 0.1); }   // rotate mid-service with notch insets
                PlayFuzz(g, r, where, 40);
                served += g.G.served; fires += g.G.fires; grabs += g.G.chefs.Count(c => c.held != null) + g.G.tiles.Count(t => t.item != null && t.type == "counter");
                if (g.G.phase != "over") { g.G.time = 0.01; Program.Run(g, 0.2); }
                if (g.screen == "scr-revive") Tap(g, "btn-revive-no");
                Program.Run(g, 2.4);
                if (g.screen != "scr-results") Problem($"{where}: no results (screen {g.screen}, phase {g.G?.phase}, time {g.G?.time:0.00}, endT {g.G?.endT:0.00}, shown {g.G?.shown}, paused {g.paused}, fires {g.G?.fires})");
                // Every menu screen must lay out at this size without throwing.
                foreach (var sc in new[] { "scr-title", "scr-shop", "scr-wardrobe", "scr-settings", "scr-howto" })
                { g.show(sc); Program.Run(g, 0.1); for (int k = 0; k < 6; k++) { g.Scroll(300); Program.Run(g, 0.05); } }
                var js = g.save.ToJson(); var back = SaveData.FromJson(js);
                if (back.ToJson() != js) Problem($"{where}: save JSON not stable across a round trip");
            }
            catch (Exception e) { Problem($"{where}: {e.GetType().Name}: {e.Message} @ {e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}"); }
        }
        Console.WriteLine($"  activity: {served} orders served, {fires} fires, {grabs} things held or set down across 150 runs");
        Console.WriteLine($"  activity: {served} orders served, {fires} fires, {grabs} things held or set down across 150 runs");
        Console.WriteLine(fuzzProblems == 0 ? "ALL PASSED" : fuzzProblems + " FAILED");
    }
}
