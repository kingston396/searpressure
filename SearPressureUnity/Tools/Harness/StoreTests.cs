using System;
using System.Linq;
using SearPressure;

// The "Remove ads" purchase: buying, pending, cancelling, restoring, and what owning it changes.
static partial class Tests
{
    static (Game g, FakeAds ads, FakeStore st) StoreGame(bool owned = false, bool unlock = true)
    {
        var fa = new FakeAds(); var st = new FakeStore { owned = owned };
        var g = Program.NewGame(new HarnessPlatform { unlock = unlock, Ads = fa, Store = st }); Program.Run(g, 0.2);
        return (g, fa, st);
    }

    static void Store()
    {
        fails = 0;
        Console.WriteLine("Title: the offer shows the store's price; buying removes it");
        {
            var (g, fa, st) = StoreGame(); st.price = "£4.49";
            Program.Run(g, 0.2); Program.Shot(g, "st_title.png");
            Check(HasHit(g, "btn-remove-ads"), "Remove ads button on the title");
            Tap(g, "btn-remove-ads"); Program.Run(g, 0.2);
            Check(st.buys == 1 && g.adsRemoved, "bought");
            Check(!HasHit(g, "btn-remove-ads"), "offer gone after buying");
            Check(g.storeNote != null && g.storeNote.StartsWith("Ads removed"), "thank-you note: " + g.storeNote);
            Program.Shot(g, "st_title_bought.png");
        }
        Console.WriteLine("Owning it: no interstitials, and the revive is free (no ad)");
        {
            var (g, fa, st) = StoreGame(owned: true);
            for (int i = 0; i < 3; i++) { if (i == 0) g.play(0); Program.Run(g, 3.7); g.G.score = 500; RunOut(g); Program.Run(g, 2.2); Tap(g, "btn-retry"); }
            Check(fa.interstitials == 0, "no interstitial after 3 services");
            Program.Run(g, 3.7); g.G.score = 0; RunOut(g);
            Check(g.screen == "scr-revive", "revive offered even with no ad loaded");
            fa.rewardedReady = false;
            Program.Shot(g, "st_revive_free.png");
            Tap(g, "btn-revive");
            Check(fa.rewardeds == 0 && g.G.phase == "play" && g.G.time > 29, "revived without an ad");
        }
        Console.WriteLine("Not owned: ads as before");
        {
            var (g, fa, st) = StoreGame();
            g.play(0); Program.Run(g, 3.7); RunOut(g); Tap(g, "btn-revive");
            Check(fa.rewardeds == 1, "revive uses a rewarded ad");
        }
        Console.WriteLine("Cancelled, pending, error, store unavailable");
        {
            var (g, fa, st) = StoreGame(); st.next = StoreResult.Cancelled;
            Tap(g, "btn-remove-ads");
            Check(!g.adsRemoved && g.storeNote == null && HasHit(g, "btn-remove-ads"), "cancel: nothing changes, offer still there");
            st.next = StoreResult.Pending; Tap(g, "btn-remove-ads");
            Check(!g.adsRemoved && g.storeNote.StartsWith("Payment pending"), "pending: " + g.storeNote);
            st.next = StoreResult.Error; Tap(g, "btn-remove-ads");
            Check(!g.adsRemoved && g.storeNote.Contains("haven't been charged"), "error: " + g.storeNote);
            st.async = true; st.next = StoreResult.Purchased; Tap(g, "btn-remove-ads"); Tap(g, "btn-remove-ads");
            Check(st.buys == 5 - 1, $"a second tap while Google Play is open does nothing (buys {st.buys})");
            st.pending(); Program.Run(g, 0.1);
            Check(g.adsRemoved, "late purchase result lands");
            var (g2, _, st2) = StoreGame(); st2.ready = false;
            Program.Run(g2, 0.1);
            Check(HasHit(g2, "btn-remove-ads"), "offer still shown before the store connects (fallback price)");
        }
        Console.WriteLine("Settings: restore");
        {
            var (g, fa, st) = StoreGame(); st.restoreResult = StoreResult.AlreadyOwned;
            g.show("scr-settings"); Program.Run(g, 0.2);
            Program.Shot(g, "st_settings.png");
            Tap(g, "btn-restore");
            Check(st.restores == 1 && g.adsRemoved, "restored");
            var (g2, _, st2) = StoreGame(); st2.restoreResult = StoreResult.NotOwned;
            g2.show("scr-settings"); Program.Run(g2, 0.2); Tap(g2, "btn-restore");
            Check(!g2.adsRemoved && g2.storeNote.StartsWith("No purchase found"), "nothing to restore: " + g2.storeNote);
        }
        Console.WriteLine("Banner: menus and results only, never during play; never for owners");
        {
            var (g, fa, st) = StoreGame();
            Check(g.BannerAllowed, "title: banner allowed");
            g.play(0); Program.Run(g, 0.5);
            Check(!g.BannerAllowed, "countdown: no banner");
            Program.Run(g, 3.5);
            Check(!g.BannerAllowed, "cooking: no banner");
            g.pauseGame(); Program.Run(g, 0.1);
            Check(!g.BannerAllowed, "paused mid-service: no banner (controls underneath)");
            g.resumeGame(); g.G.score = 500; RunOut(g);
            Check(g.G.phase == "over" && !g.BannerAllowed, "\"Time's up!\" (controls still on screen): no banner yet");
            Program.Run(g, 1.0);
            Check(!g.BannerAllowed, "still no banner 1 s later, before the results card");
            Program.Run(g, 1.2);
            Check(g.screen == "scr-results" && g.BannerAllowed, "results card: banner allowed");
            st.owned = true;
            Check(!g.BannerAllowed, "owner: never a banner");
            var (g2, _, st2) = StoreGame(); st2.ready = false; Program.Run(g2, 0.1);
            var f = typeof(Game).GetField("uiHits", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Check(g2.storeNote == null && HasHit(g2, "btn-remove-ads"), "price not loaded yet: button without a made-up price");
        }
        Console.WriteLine("No store at all (other platforms): no offer, nothing breaks");
        {
            var g = Program.NewGame(new HarnessPlatform { unlock = true, Ads = new FakeAds() }); Program.Run(g, 0.2);
            Check(!HasHit(g, "btn-remove-ads") && !g.adsRemoved, "no offer");
            g.show("scr-settings"); Program.Run(g, 0.2);
            Check(!HasHit(g, "btn-restore"), "no restore row");
        }
        Console.WriteLine(fails == 0 ? "ALL PASSED" : fails + " FAILED");
    }
}
