using System;

namespace SearPressure
{
    // The one in-app purchase: "Remove ads", a one-time (non-consumable) product. Google Play Billing in the
    // Unity build; a fake in the tests. Owning it switches off the banner and interstitials and makes the
    // revive free.
    public interface IStore
    {
        bool Owned { get; }                  // bought (cached on the device, confirmed with the store at start-up)
        string Price { get; }                // the store's localised price, e.g. "$4.99"; null until known
        bool Ready { get; }                  // connected to the store and the product was found
        void Buy(Action<StoreResult> done);  // done runs on the game thread
        void Restore(Action<StoreResult> done);
    }

    public enum StoreResult { Purchased, Pending, Cancelled, AlreadyOwned, NotOwned, Unavailable, Error }

    public sealed partial class Game
    {
        IStore store => platform.Store;
        // Paid players: no ads at all (the host also drops the banner).
        public bool adsRemoved => store != null && store.Owned;
        bool storeBusy; double storeBusyAt; int storeToken;
        const double STORE_TIMEOUT = 300;   // Google Play's sheet can stay open a while (typing card details)
        public string storeNote;             // one line under the button: thanks, pending, couldn't reach the store…
        double storeNoteT;

        // Only ever show Google Play's own (local-currency) price; before it's known, no price at all.
        string removeAdsLabel => store?.Price is string p && p.Length > 0 ? "Remove ads · " + p : "Remove ads";

        public void buyRemoveAds()
        {
            if (store == null || storeBusy || adsRemoved) return;
            storeBusy = true; storeBusyAt = T; storeNote = "Opening Google Play…"; storeNoteT = T;
            int token = ++storeToken;
            store.Buy(r => { if (token != storeToken) { if (r == StoreResult.Purchased) storeFeedback(r); return; } storeBusy = false; storeFeedback(r); });
        }

        public void restorePurchase()
        {
            if (store == null || storeBusy) return;
            storeBusy = true; storeBusyAt = T; storeNote = "Checking your purchases…"; storeNoteT = T;
            int token = ++storeToken;
            store.Restore(r => { if (token != storeToken) return; storeBusy = false; storeFeedback(r); });
        }

        // Never leave the button greyed out if the store never answers.
        void storeTick() { if (storeBusy && T - storeBusyAt > STORE_TIMEOUT) { storeBusy = false; storeToken++; storeNote = null; } }

        void storeFeedback(StoreResult r)
        {
            storeNoteT = T;
            switch (r)
            {
                case StoreResult.Purchased:
                case StoreResult.AlreadyOwned:
                    storeNote = "Ads removed. Thank you for supporting Sear Pressure!";
                    adLevels = 0; sfx("serve"); buzz(40);
                    break;
                case StoreResult.Pending: storeNote = "Payment pending. Ads go away as soon as Google Play confirms it."; break;
                case StoreResult.Cancelled: storeNote = null; break;
                case StoreResult.NotOwned: storeNote = "No purchase found on this Google account."; break;
                case StoreResult.Unavailable: storeNote = "Google Play isn't available right now. Try again in a moment."; break;
                default: storeNote = "Something went wrong with Google Play. You haven't been charged. Try again."; break;
            }
        }

        // Title screen: the offer, or a quiet thank-you once bought.
        void RemoveAdsOffer()
        {
            if (store == null) return;
            if (!adsRemoved)
            {
                if (Button("btn-remove-ads", removeAdsLabel, "ghost", "One-time purchase. Revives become free too.", storeBusy)) buyRemoveAds();
            }
            if (!string.IsNullOrEmpty(storeNote) && T - storeNoteT < 12) Para(storeNote, "small");
        }

        // Settings: restore (new phone, reinstall) and what you own.
        void StoreSettings()
        {
            if (store == null) return;
            if (adsRemoved) Para("Ads removed. Thanks for buying Sear Pressure!", "small");
            else if (Button("btn-settings-buy", removeAdsLabel, "ghost", "One-time purchase", storeBusy)) buyRemoveAds();
            if (Button("btn-restore", "Restore purchase", "ghost", "Bought it before? Get it back on this device", storeBusy)) restorePurchase();
            if (!string.IsNullOrEmpty(storeNote) && T - storeNoteT < 12) Para(storeNote, "small");
        }
    }
}
