using System;

namespace SearPressure
{
    // Ads the platform can show (AdMob in the Unity build). The banner is the host's business: it sits
    // under the game, which is told about it as extra bottom safe area. Everything here is optional:
    // with no IAds (or no ad loaded) the game simply carries on.
    public interface IAds
    {
        bool InterstitialReady { get; }
        void ShowInterstitial(Action done);          // done runs once the ad is closed (or failed to show)
        bool RewardedReady { get; }
        void ShowRewarded(Action<bool> done);        // done(true) once closed if the reward was earned
        bool PrivacyOptionsRequired { get; }         // GDPR: Settings must offer a way to change consent
        void ShowPrivacyOptions();
    }

    public sealed partial class Game
    {
        public const int INTERSTITIAL_EVERY = 2;     // a full-screen ad after every 2nd finished service
        public const double REVIVE_SECONDS = 30, DRIVE_REVIVE_SECONDS = 20;

        // No ads for players who bought "Remove ads".
        IAds ads => adsRemoved ? null : platform.Ads;
        public int adLevels;                         // services finished since the last interstitial
        bool adBusy;                                 // a full-screen ad is up: menus ignore taps
        double adBusyAt; int adToken; Action adNext;
        const double AD_TIMEOUT = 75;               // never wait on an ad SDK callback longer than this
        public const double AD_TIMEOUT_FOR_TESTS = AD_TIMEOUT;
        string reviveKind;                           // "time" or "strikes" (kitchen), "drive"
        bool reviveWaiting;
        double reviveAt; int reviveToken;

        // The bottom banner only shows on menus and results, never while a service or run is being played
        // (AdMob: no ads next to controls players tap all the time). The host reads this every frame.
        public bool BannerAllowed =>
            !adsRemoved && NET.role == null &&
            (G == null || (G.phase == "over" && G.shown)) && (D == null || (D.phase == "over" && D.shown));   // not at "Time's up!", only once the results card is up

        // ---- interstitials: at the natural break after the results card ----
        void countLevelForAds() { if (NET.role == null) adLevels++; }

        // Run `next` (Retry, Next kitchen, All kitchens), with a full-screen ad first when one is due.
        void afterBreakAd(Action next)
        {
            if (adBusy) return;
            if (ads != null && NET.role == null && adLevels >= INTERSTITIAL_EVERY && ads.InterstitialReady)
            {
                adLevels = 0; adBusy = true; adBusyAt = T; adNext = next;
                int token = ++adToken;
                ads.ShowInterstitial(() => { if (token == adToken && adBusy) finishBreakAd(); });
                return;
            }
            next();
        }

        void finishBreakAd() { adBusy = false; var n = adNext; adNext = null; n?.Invoke(); }

        // Called every frame: if the ad SDK never reports back (its activity was killed, say), carry on anyway.
        void adsTick()
        {
            storeTick();
            if (adBusy && T - adBusyAt > AD_TIMEOUT) { adToken++; finishBreakAd(); }
            if (reviveWaiting && T - reviveAt > AD_TIMEOUT) { reviveToken++; reviveWaiting = false; declineRevive(); }
        }

        // ---- revive: a failed service can watch one rewarded ad to keep going ----
        // Paid players get the revive without an ad.
        bool canOfferRevive() => NET.role == null && (adsRemoved || (ads != null && ads.RewardedReady));

        // Time ran out with no stars, or the Judge's third strike. True if the offer is now showing.
        bool offerRevive(string kind)
        {
            // Not in the tutorial, and not on the daily challenge: it's the same, fair contest for everyone.
            if (G == null || G.lv.tutorial || G.lv.daily) return false;
            if (G.revived || !canOfferRevive()) return false;
            if (kind == "time" && starsFor(G.lvIdx, G.score, G.humans) > 0) return false;
            G.phase = "revive"; reviveKind = kind; reviveWaiting = false;
            foreach (var c in G.chefs) { c.task = null; c.spraying = false; }
            input.joy.id = null; Blur();
            sfx("end");
            show("scr-revive");
            return true;
        }

        bool offerDriveRevive()
        {
            if (D == null || D.tut != null || D.revived || !canOfferRevive()) return false;
            if (driveStars(D.k, D.coins) > 0) return false;
            D.phase = "revive"; reviveKind = "drive"; reviveWaiting = false;
            input.joy.id = null; D.hand = false; Blur();
            engineSet(0); sfx("end");
            show("scr-revive");
            return true;
        }

        void acceptRevive()
        {
            if (reviveWaiting) return;
            if (!canOfferRevive()) { declineRevive(); return; }
            if (adsRemoved) { applyRevive(); return; }
            reviveWaiting = true; reviveAt = T;
            adLevels = 0;   // a rewarded ad counts as this break's ad: no interstitial straight after it
            int token = ++reviveToken;
            ads.ShowRewarded(earned =>
            {
                if (token != reviveToken || !reviveWaiting) return;   // too late: the offer was already settled
                reviveWaiting = false;
                if (earned) applyRevive(); else declineRevive();
            });
        }

        bool reviveOpen() => reviveKind == "drive" ? D != null && D.phase == "revive" : G != null && G.phase == "revive";

        void applyRevive()
        {
            if (!reviveOpen()) return;
            if (reviveKind == "drive" && D != null)
            {
                D.revived = true; D.time += DRIVE_REVIVE_SECONDS; D.lastSec = 99; D.phase = "play";
            }
            else if (G != null)
            {
                G.revived = true;
                if (reviveKind == "strikes") { G.strikes = MAX_STRIKES - 1; G.kicked = false; G.judge = null; }
                else { G.time += REVIVE_SECONDS; G.lastSec = 99; }
                G.phase = "play";
            }
            show(null);
            sfx("go"); buzz(40);
        }

        void declineRevive()
        {
            if (reviveWaiting || !reviveOpen()) return;   // wait for the ad that's showing
            show(null);
            if (reviveKind == "drive" && D != null) { D.phase = "over"; D.endT = 0; return; }
            if (G == null) return;
            if (reviveKind == "strikes") { G.kicked = true; judgeSay("out", 2); }
            G.phase = "play";   // endLevel() only ends a service that isn't over yet
            endLevel();
        }

        void ReviveScreen()
        {
            bool drive = reviveKind == "drive", strikes = reviveKind == "strikes";
            Mono(strikes ? "Three strikes" : "Time's up", drive ? "Delivery Run" : G != null ? G.lv.name : "");
            H2(strikes ? "Thrown out?" : "Keep going?");
            string how = adsRemoved ? "Take" : "Watch a short ad for";
            Para(strikes
                ? (adsRemoved ? "Wipe your last strike and get back to the pass." : "Watch a short ad to wipe your last strike and get back to the pass.")
                : drive
                    ? $"No stars yet. {how} {U.S(DRIVE_REVIVE_SECONDS)} more seconds on the clock."
                    : $"No stars yet. {how} {U.S(REVIVE_SECONDS)} more seconds of service.");
            string gain = strikes ? "wipe a strike" : $"+{U.S(drive ? DRIVE_REVIVE_SECONDS : REVIVE_SECONDS)} seconds";
            if (Button("btn-revive", adsRemoved ? "Keep going: " + gain : "Watch an ad: " + gain, "btn", null, reviveWaiting)) acceptRevive();
            if (Button("btn-revive-no", "No thanks", "ghost", null, reviveWaiting)) declineRevive();
            Para("One revive per service.", "small");
            cy -= GAP;
        }
    }
}
