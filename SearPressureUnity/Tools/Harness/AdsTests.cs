using System;
using System.Linq;
using SearPressure;

// Ads: the revive offer, interstitials, double-your-coins, privacy choices, and no ads at all.
static partial class Tests
{
    static (Game g, FakeAds ads) AdGame(bool withAds = true, bool unlock = true)
    {
        var fa = withAds ? new FakeAds() : null;
        var g = Program.NewGame(new HarnessPlatform { unlock = unlock, Ads = fa }); Program.Run(g, 0.2);
        g.onlineEnabled = false;
        return (g, fa);
    }

    static void RunOut(Game g) { g.G.time = 0.01; Program.Run(g, 0.1); }

    static void Ads()
    {
        fails = 0;
        Console.WriteLine("Revive: time runs out with no stars → offer, watch, +30 seconds, only once");
        {
            var (g, fa) = AdGame();
            g.play(0); Program.Run(g, 3.7);
            g.G.score = 10; RunOut(g);
            Check(g.screen == "scr-revive" && g.G.phase == "revive", $"revive offered ({g.screen}, {g.G.phase})");
            Program.Run(g, 1);
            Check(g.G.phase == "revive" && g.G.time == 0, "the service waits on the offer");
            Program.Shot(g, "ad_revive.png");
            Tap(g, "btn-revive");
            Check(fa.rewardeds == 1 && g.G.phase == "play" && g.screen == null, "watched the ad: back in service");
            Check(Math.Abs(g.G.time - Game.REVIVE_SECONDS) < 0.2, $"30 more seconds ({g.G.time:0.0})");
            RunOut(g);
            Check(g.G.phase == "over" && g.screen != "scr-revive", "second time out: no second offer");
            Program.Run(g, 2.2);
            Check(g.screen == "scr-results", "then the results card");
        }
        Console.WriteLine("Revive: 'No thanks' goes straight to the results");
        {
            var (g, fa) = AdGame();
            g.play(0); Program.Run(g, 3.7); RunOut(g);
            Tap(g, "btn-revive-no");
            Program.Run(g, 2.2);
            Check(fa.rewardeds == 0 && g.screen == "scr-results", "declined → results");
        }
        Console.WriteLine("Revive: closing the ad early earns nothing");
        {
            var (g, fa) = AdGame(); fa.earn = false;
            g.play(0); Program.Run(g, 3.7); RunOut(g);
            Tap(g, "btn-revive"); Program.Run(g, 2.2);
            Check(fa.rewardeds == 1 && g.screen == "scr-results", "no reward → results");
        }
        Console.WriteLine("Revive: not offered with a star, with no ad loaded, or with no ads at all");
        {
            var (g, fa) = AdGame();
            g.play(0); Program.Run(g, 3.7);
            g.G.score = 500; RunOut(g);
            Check(g.G.phase == "over" && g.screen != "scr-revive", "earned a star: no offer");
            var (g2, fa2) = AdGame(); fa2.rewardedReady = false;
            g2.play(0); Program.Run(g2, 3.7); RunOut(g2);
            Check(g2.G.phase == "over", "no rewarded ad loaded: no offer");
            var (g3, _) = AdGame(false);
            g3.play(0); Program.Run(g3, 3.7); RunOut(g3);
            Check(g3.G.phase == "over", "no ads: no offer");
        }
        Console.WriteLine("Revive: the Judge's third strike can be wiped");
        {
            var (g, fa) = AdGame();
            int j = Lvl("The Judge's Table");
            g.play(j); Program.Run(g, 3.7);
            g.strike("wrong"); g.strike("wrong"); g.strike("wrong");
            Check(g.screen == "scr-revive" && !g.G.kicked, "third strike → offer");
            Tap(g, "btn-revive");
            Check(g.G.phase == "play" && g.G.strikes == Game.MAX_STRIKES - 1 && !g.G.kicked, $"strike wiped (strikes {g.G.strikes})");
            g.strike("wrong");
            Check(g.G.phase == "over" && g.G.kicked, "next third strike ends it");
        }
        Console.WriteLine("Revive: Delivery Runs get +20 seconds");
        {
            var fa = new FakeAds();
            var p = DrivePlatform(true); p.Ads = fa;
            var g = Program.NewGame(p); Program.Run(g, 0.2);
            g.startDrive(0); Program.Run(g, 3.4);
            g.D.coins = 0; g.D.time = 0.01; Program.Run(g, 0.1);
            Check(g.screen == "scr-revive" && g.D.phase == "revive", "drive offer");
            Tap(g, "btn-revive");
            Check(g.D.phase == "play" && Math.Abs(g.D.time - Game.DRIVE_REVIVE_SECONDS) < 0.2, $"+20 s ({g.D.time:0.0})");
        }
        Console.WriteLine("Interstitial: every 2nd finished service, when leaving the results card");
        {
            var (g, fa) = AdGame();
            g.play(0); Program.Run(g, 3.7); g.G.score = 500; RunOut(g); Program.Run(g, 2.2);
            Tap(g, "btn-retry"); Program.Run(g, 3.7);
            Check(fa.interstitials == 0 && g.G != null && g.G.phase == "play", "after service 1: no ad, straight back in");
            g.G.score = 500; RunOut(g); Program.Run(g, 2.2);
            Tap(g, "btn-res-menu");
            Check(fa.interstitials == 1 && g.screen == "scr-title", "after service 2: one ad, then the kitchen list");
            g.play(0); Program.Run(g, 3.7); g.G.score = 500; RunOut(g); Program.Run(g, 2.2);
            Tap(g, "btn-retry");
            Check(fa.interstitials == 1, "the count starts again");
            fa.interstitialReady = false;
            Program.Run(g, 3.7); g.G.score = 500; RunOut(g); Program.Run(g, 2.2);
            Tap(g, "btn-retry");
            Check(fa.interstitials == 1 && g.G.phase != "over", "no ad loaded: carries on without one");
        }
        Console.WriteLine("Tutorial: never an ad");
        {
            var (g, fa) = AdGame(true, false);
            g.startTutorial(); Program.Run(g, 1);
            Check(g.adLevels == 0 && fa.interstitials == 0 && fa.rewardeds == 0, "no counting or offers in the tutorial");
        }
        Console.WriteLine("Settings: Privacy choices when consent rules apply");
        {
            var (g, fa) = AdGame(); fa.privacy = true;
            g.show("scr-settings"); Program.Run(g, 0.2);
            Tap(g, "btn-privacy");
            Check(fa.privacyShown == 1, "privacy options opened");
            var (g2, fa2) = AdGame();
            g2.show("scr-settings"); Program.Run(g2, 0.2);
            Check(!HasHit(g2, "btn-privacy"), "hidden when not required");
        }
        Console.WriteLine("Regression: finished service → Next → Back → How to play → Resume is never a frozen kitchen");
        {
            var (g, _) = AdGame();
            g.play(0); Program.Run(g, 3.7); g.G.score = 500; RunOut(g); Program.Run(g, 2.2);
            Tap(g, "btn-next"); Program.Run(g, 0.2);
            if (g.screen == "scr-story") { g.show("scr-intro"); }
            g.KeyDown("Escape", false); g.KeyUp("Escape"); Program.Run(g, 0.2);
            Check(g.screen == "scr-title", "back at the kitchen list (" + g.screen + ")");
            g.show("scr-howto"); Program.Run(g, 0.1);
            Tap(g, "btn-howto-close");
            Check(g.screen == "scr-title", "How to play closes to the title, not a pause card (" + g.screen + ")");
            g.show("scr-pause"); Program.Run(g, 0.1); g.resumeGame(); Program.Run(g, 0.1);
            Check(g.screen == "scr-title" && g.G == null, "Resume with nothing to resume goes to the title");
        }
        Console.WriteLine("Regression: back during a rewarded ad waits for the ad; a late reward can't restart a finished service");
        {
            var (g, fa) = AdGame(); fa.async = true;
            g.play(0); Program.Run(g, 3.7); RunOut(g);
            Tap(g, "btn-revive");
            g.KeyDown("Escape", false); g.KeyUp("Escape"); Program.Run(g, 0.2);
            Check(g.G.phase == "revive" && g.screen == "scr-revive", "back is ignored while the ad is up");
            fa.Finish(); Program.Run(g, 0.1);
            Check(g.G.phase == "play" && g.G.time > 29, "reward arrives → revived");
            var (g2, fa2) = AdGame(); fa2.async = true;
            g2.play(0); Program.Run(g2, 3.7); RunOut(g2);
            Tap(g2, "btn-revive");
            Program.Run(g2, Game.AD_TIMEOUT_FOR_TESTS + 1, 0.05);
            Check(g2.G.phase == "over", "an ad that never reports back is given up on");
            Program.Run(g2, 2.2);
            fa2.Finish(); Program.Run(g2, 0.2);
            Check(g2.screen == "scr-results" && g2.G.phase == "over", "…and its late reward changes nothing");
        }
        Console.WriteLine("Regression: no interstitial straight after a revive ad; interstitial callback timeout");
        {
            var (g, fa) = AdGame();
            g.play(0); Program.Run(g, 3.7); g.G.score = 500; RunOut(g); Program.Run(g, 2.2);
            Tap(g, "btn-retry"); Program.Run(g, 3.7);
            RunOut(g); Tap(g, "btn-revive"); RunOut(g); Program.Run(g, 2.2);
            Tap(g, "btn-retry");
            Check(fa.rewardeds == 1 && fa.interstitials == 0, $"revive ad counted as the break (interstitials {fa.interstitials})");
            var (g2, fa2) = AdGame(); fa2.async = true;
            for (int i = 0; i < 2; i++) { if (i == 0) g2.play(0); Program.Run(g2, 3.7); g2.G.score = 500; RunOut(g2); Program.Run(g2, 2.2); Tap(g2, "btn-retry"); }
            Check(fa2.interstitials == 1 && g2.screen == "scr-results", "interstitial showing, menus waiting");
            Program.Run(g2, Game.AD_TIMEOUT_FOR_TESTS + 1, 0.05);
            Check(g2.G != null && g2.G.phase != "over", "no callback → carries on to the retry anyway");
        }
        Console.WriteLine("Interstitial: after every service once 3 minutes have passed since the last ad");
        {
            var (g, fa) = AdGame();
            g.play(0); Program.Run(g, 3.7); g.lastAdAt = g.T - Game.AD_GAP - 1; g.G.score = 500; RunOut(g); Program.Run(g, 2.2);
            Tap(g, "btn-retry");
            Check(fa.interstitials == 1, "long service: an ad after the first one");
            Program.Run(g, 3.7); g.G.score = 500; RunOut(g); Program.Run(g, 2.2);
            Tap(g, "btn-retry");
            Check(fa.interstitials == 1, "but not again within 3 minutes");
        }
        Console.WriteLine("Double your coins: rewarded ad on the results card, a few times a day");
        {
            var (g, fa) = AdGame();
            g.play(0); Program.Run(g, 3.7); g.G.score = 500; RunOut(g); Program.Run(g, 2.2);
            double paid = g.results.bonus, before = g.save.wallet;
            Check(paid > 0 && HasHit(g, "btn-double"), $"offered after a paying service (+{paid})");
            Tap(g, "btn-double");
            Check(fa.rewardeds == 1 && Math.Abs(g.save.wallet - before - paid) < 0.01, $"watched: +{paid} more ({g.save.wallet - before})");
            Program.Run(g, 0.2);
            Check(!HasHit(g, "btn-double"), "only once per result");
            Tap(g, "btn-retry");
            Check(fa.interstitials == 0, "the rewarded ad counts as this break's ad");
            // No reward → no coins.
            Program.Run(g, 3.7); g.G.score = 500; RunOut(g); Program.Run(g, 2.2);
            fa.earn = false; before = g.save.wallet; Tap(g, "btn-double");
            Check(g.save.wallet == before && g.doublesLeft() == Game.DOUBLES_PER_DAY - 1, "skipped ad: no coins, no use counted");
            fa.earn = true;
            // Daily limit.
            g.save.bonusCount = Game.DOUBLES_PER_DAY; Program.Run(g, 0.2);
            g.results.bonusTaken = false;
            Check(g.doublesLeft() == 0 && !HasHit(g, "btn-double"), "no offer once today's limit is used");
            g.save.bonusDay = "2000-01-01";
            Check(g.doublesLeft() == Game.DOUBLES_PER_DAY, "a new day resets the limit");
            // Nothing to double: replay of a level with no coins, the daily, the tutorial.
            g.results.bonus = 0; Program.Run(g, 0.2);
            Check(!HasHit(g, "btn-double"), "no offer when nothing was paid");
            // A late reward after giving up changes nothing.
            var (g2, fa2) = AdGame(); fa2.async = true;
            g2.play(0); Program.Run(g2, 3.7); g2.G.score = 500; RunOut(g2); Program.Run(g2, 2.2);
            before = g2.save.wallet; Tap(g2, "btn-double");
            Program.Run(g2, Game.AD_TIMEOUT_FOR_TESTS + 1, 0.05); fa2.Finish();
            Check(g2.save.wallet == before, "late reward after the timeout: no coins");
            // Saved across restarts.
            var s = SaveData.FromJson(g.save.ToJson());
            Check(s.bonusDay == g.save.bonusDay && s.bonusCount == g.save.bonusCount, "limit is saved");
        }
        Console.WriteLine("Double your coins: free for Remove-ads owners, same daily limit, no ad");
        {
            var fs = new FakeStore { owned = true };
            var fa = new FakeAds();
            var g = Program.NewGame(new HarnessPlatform { unlock = true, Ads = fa, Store = fs }); Program.Run(g, 0.2); g.onlineEnabled = false;
            g.play(0); Program.Run(g, 3.7); g.G.score = 500; RunOut(g); Program.Run(g, 2.2);
            double before = g.save.wallet, paid = g.results.bonus;
            Tap(g, "btn-double");
            Check(fa.rewardeds == 0 && Math.Abs(g.save.wallet - before - paid) < 0.01, "doubled without an ad");
            Check(g.doublesLeft() == Game.DOUBLES_PER_DAY - 1, "counts toward the daily limit");
        }
        Console.WriteLine("Regression: no revive on the daily challenge");
        {
            var (g, fa) = AdGame();
            g.dailyLevel(); g.play(Game.DAILY_IDX); Program.Run(g, 3.7); RunOut(g);
            Check(g.G.phase == "over" && fa.rewardeds == 0 && g.screen != "scr-revive", "daily: straight to results");
        }
        Console.WriteLine(fails == 0 ? "ALL PASSED" : fails + " FAILED");
    }
}
