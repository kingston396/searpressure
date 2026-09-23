using System;
using System.Collections.Generic;
using System.Linq;
using SearPressure;

static partial class Tests
{
    static void Screens()
    {
        fails = 0;
        var p = new HarnessPlatform { unlock = true };
        var g = Program.NewGame(p); Program.Run(g, 0.2);
        g.save.wallet = 5000; g.save.owned["headband"] = true; g.save.owned["pots"] = true;
        g.openIntro(0); Program.Run(g, 0.2); Program.Shot(g, "s_intro.png");
        g.openIntro(Lvl("The Judge's Table")); Program.Run(g, 0.2); Program.Shot(g, "s_intro_judge.png");
        g.openIntro(Game.DAILY_IDX); Program.Run(g, 0.2); Program.Shot(g, "s_intro_daily.png");
        g.openShop("kitchen"); Program.Run(g, 0.2); Program.Shot(g, "s_shop_kitchen.png");
        g.openShop("gear"); Program.Run(g, 0.2); Program.Shot(g, "s_shop_gear.png");
        g.show("scr-wardrobe"); Program.Run(g, 0.2); Program.Shot(g, "s_wardrobe.png");
        g.show("scr-settings"); Program.Run(g, 0.2); Program.Shot(g, "s_settings.png");
        g.show("scr-howto"); Program.Run(g, 0.2); Program.Shot(g, "s_howto.png");
        g.show("scr-online"); Program.Run(g, 0.2); Program.Shot(g, "s_online.png");
        // Story before the first stop (a fresh save).
        var g2 = Program.NewGame(new HarnessPlatform()); Program.Run(g2, 0.2);
        Tap(g2, "lvl-0"); Program.Run(g2, 0.2); Program.Shot(g2, "s_story.png");
        Check(g2.screen == "scr-story", "tapping the first kitchen opens the story (" + g2.screen + ")");
        // A service to the results card.
        g.play(0); Program.Run(g, 3.7);
        g.G.score = 150; g.G.served = 7; g.G.failed = 1; g.G.missLoss = 10; g.G.tips = 30; g.G.time = 0.01;
        Program.Run(g, 2.2);
        Check(g.screen == "scr-results", "results after time's up");
        Program.Shot(g, "s_results.png");
        g.pauseGame(); g.play(0); Program.Run(g, 3.7); g.pauseGame(); Program.Run(g, 0.2);
        Check(g.screen == "scr-pause" && g.paused, "pause card");
        Program.Shot(g, "s_pause.png");
        Program.Shot(Landscape(), "s_land.png");
        // Online hidden (the store build until a relay is deployed).
        var g3 = Program.NewGame(new HarnessPlatform()); g3.onlineEnabled = false; Program.Run(g3, 0.2);
        Program.Shot(g3, "s_title_offline.png");
        Check(!HasHit(g3, "btn-online"), "no Play with a friend button when online is off");
        g3.show("scr-online"); Program.Run(g3, 0.1);
        Check(g3.screen == "scr-title", "the online screen can't be opened when online is off");
        Console.WriteLine(fails == 0 ? "ALL PASSED" : fails + " FAILED");
    }

    static Game Landscape()
    {
        var g = Program.NewGame(new HarnessPlatform { unlock = true }, 844, 390, 2);
        Program.Run(g, 0.2); g.play(Lvl("Curb Service")); Program.Run(g, 4); g.spawnOrder(); Program.Run(g, 1);
        return g;
    }

    static bool HasHit(Game g, string id)
    {
        var f = typeof(Game).GetField("uiHits", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return ((List<(string id, Rect r)>)f.GetValue(g)).Any(x => x.id == id);
    }

    // Tap a UI element by its id (as the player would), using last frame's hit boxes.
    static void Tap(Game g, string id)
    {
        var f = typeof(Game).GetField("uiHits", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var hits = (List<(string id, Rect r)>)f.GetValue(g);
        var h = hits.FirstOrDefault(x => x.id == id);
        if (h.id == null)
        {
            // Scroll until it shows up.
            for (int i = 0; i < 40 && h.id == null; i++) { g.Scroll(200); Program.Run(g, 1 / 30.0); hits = (List<(string id, Rect r)>)f.GetValue(g); h = hits.FirstOrDefault(x => x.id == id && x.r.y > 0 && x.r.y < g.H); }
            if (h.id == null) { Console.WriteLine("  (no hit box for " + id + ")"); return; }
        }
        double x = h.r.x + h.r.w / 2, y = h.r.y + h.r.h / 2;
        g.PointerDown(7, x, y); Program.Run(g, 1 / 30.0); g.PointerUp(7, x, y); Program.Run(g, 1 / 30.0); Program.Run(g, 1 / 30.0);
    }

    static void Economy()
    {
        fails = 0;
        var p = new HarnessPlatform();
        var g = Program.NewGame(p); Program.Run(g, 0.2);
        Console.WriteLine("First try, three stars: the level's coins plus a bonus");
        g.play(0); Program.Run(g, 3.7);
        g.G.score = 200; g.G.time = 0.01; Program.Run(g, 2.2);
        Check(g.save.wallet == 190 + 100, $"wallet {g.save.wallet} (190 budget + 100 bonus)");
        Check(g.results.wallet.StartsWith("First-try three stars!"), g.results.wallet);
        Console.WriteLine("Replaying a completed level pays 20%");
        g.play(0); Program.Run(g, 3.7);
        g.G.score = 100; g.G.time = 0.01; Program.Run(g, 2.2);
        Check(g.save.wallet == 290 + 20, $"wallet {g.save.wallet} (+20)");
        Console.WriteLine("Unlocks: Soup Kitchen opens with a star on Salad Days; stop 1 needs 3 stars");
        Check(g.unlocked(1) && !g.unlocked(2), "kitchen 2 open, kitchen 3 locked");
        Check(!g.stopVisible(1), "Route 66 hidden until Dinner Rush is open");
        Console.WriteLine("Shop: buy a fork (two taps), sell it back for 25%");
        g.save.wallet = 3000;
        g.openShop("kitchen"); Program.Run(g, 0.1);
        g.save.stars[0] = 3; g.save.stars[1] = 1; // soup kitchen unlocked: pots show
        Program.Run(g, 0.1);
        Tap(g, "buy-pots"); Check(!g.owns("pots"), "first tap only asks to confirm");
        Tap(g, "buy-pots"); Check(g.owns("pots") && g.save.wallet == 900, $"bought Pressure Pots (wallet {g.save.wallet})");
        Tap(g, "buy-pots"); Tap(g, "buy-pots");
        Check(!g.owns("pots") && g.save.wallet == 900 + 525, $"sold back for 525 (wallet {g.save.wallet})");
        Console.WriteLine("Daily challenge: same kitchen all day, quest pays 400 once");
        var lv = g.dailyLevel();
        Check(g.DAILY.@base == 0 || g.unlocked(g.DAILY.@base), "daily kitchen is one you've unlocked: " + lv.baseName);
        Console.WriteLine("Save round trip");
        var json = g.save.ToJson(); var back = SaveData.FromJson(json);
        Check(back.wallet == g.save.wallet && back.stars[0] == 3 && back.plays.Count == g.save.plays.Count, "save survives JSON");
        Console.WriteLine(fails == 0 ? "ALL PASSED" : fails + " FAILED");
    }
}
