# Ads in Sear Pressure (Google AdMob) and "Remove ads"

Sear Pressure is **free with ads**. A one-time in-app purchase, **"Remove ads" ($4.99, product ID `remove_ads`)**, turns
every ad off for good and makes the revive free (no ad to watch).
- AdMob App ID: `ca-app-pub-2822796427144413~6568766943`, already set in `AdsConfig.cs` and the Google Mobile Ads settings asset.
- Ad unit IDs: still Google's **test** units in `AdsConfig.cs` until you create the real ones (steps below).
- The purchase uses Google Play Billing directly (`Unity/PlayBilling.cs`, library version in `Editor/SearPressureDependencies.xml`).
  The editor uses a pretend store, and **Sear Pressure → Testing → Reset "Remove Ads" Purchase** undoes it.
- `SEARPRESSURE_ADS` is defined automatically while the Google Mobile Ads package is installed. Removing the package
  (and setting `AdsConfig.Enabled = false`) makes the build ad-free.

## What the game does

| Ad | When | Code |
|---|---|---|
| **Banner** | Always, along the bottom. The game shrinks to sit above it, so it never covers the controls. | `AdMobAds.cs` (adaptive anchored banner) |
| **Interstitial** | After every **2nd** finished kitchen or Delivery Run, when the player leaves the results card (Retry / Next kitchen / All kitchens). Never in the tutorial. | `Game.Ads.cs`, `INTERSTITIAL_EVERY = 2` |
| **Rewarded revive** | When a kitchen ends with **0 stars** (or on the Judge's **third strike**), the player can watch one ad: **+30 seconds** (Delivery Runs: +20 s), or the last strike is wiped. Once per attempt; not on the daily challenge (it's the same contest for everyone). | `Game.Ads.cs`, `REVIVE_SECONDS` |
| **Consent** | Google's consent form (UMP) runs at start-up. Players in the EEA, UK and Switzerland see it; elsewhere nothing shows. Settings gets a **Privacy choices** button where required by law. | `AdMobAds.Begin()` |

These safeguards are built in:
- A revive ad also counts as the break ad, so the player never gets an interstitial right after one.
- If an ad fails to load, the game simply carries on without it.
- If an ad never reports back, the game continues after 75 s.

## Setting up AdMob (about 20 minutes)

1. Sign up at **admob.google.com** with your Google account. Add payment details and tax info later, before your first payout.
2. **Apps → Add app → Android → "Is the app listed on a supported app store?"**: choose **No** for now. You link it to Google Play after it's published.
   Name: *Sear Pressure*.
3. Copy the **App ID** (`ca-app-pub-…~…`).
4. **Ad units → Add ad unit**, three times:
   - **Banner**, named "Bottom banner"
   - **Interstitial**, named "Between levels"
   - **Rewarded**, named "Revive". Reward: amount 1, item "revive"
5. Open `SearPressureUnity/Assets/SearPressure/Unity/AdsConfig.cs` and replace the test IDs:
   - `AndroidAppId`
   - `AndroidBanner`, `AndroidInterstitial`, `AndroidRewarded`
   - (and the iOS ones later, if you publish on iPhone)
6. In Unity, run **Sear Pressure → Apply Store Settings**. It copies the App ID into Google's settings file (`Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset`).
7. **Privacy & messaging → GDPR → Create message.** Link it to the app, choose your privacy policy URL and publish it. Without this, the consent form can't show in Europe, and ads there won't serve.
8. **app-ads.txt.** AdMob will ask for a file at `https://<your website>/app-ads.txt`. The website must be the one listed as your developer website on Google Play. It contains one line from AdMob (Apps → View all apps → app-ads.txt), e.g.
   `google.com, pub-XXXXXXXXXXXXXXXX, DIRECT, f08c47fec0942fa0`
   Hosting options for `store/` (GitHub Pages and others) are in `listing.md`; put `app-ads.txt` at the site's root.

⚠️ **Never tap your own live ads**, and don't ask friends to. AdMob bans accounts for invalid clicks. While testing, keep the test IDs, or add your phone as a test device (AdMob → Settings → Test devices).

## Testing ads on your phone (before every release)

With the test IDs still in `AdsConfig.cs`:
1. **Start-up:** the game starts with no consent form (outside Europe). A banner labelled "Test Ad" appears along the bottom within a few seconds.
2. **Banner:** the kitchen, joystick and buttons all sit above the banner, in portrait and landscape.
3. **Revive:**
   - Play Salad Days and let the timer run out with 0 coins. **"Keep going?"** appears.
   - Tap **Watch an ad** and watch the test video to the end. You should get **+30 seconds**.
   - Let it run out again. This time it goes straight to the results.
4. **Revive, closed early:** the same, but close the ad early. It goes to the results with no revive.
5. **Interstitial:** finish two services. Leaving the second results card shows a full-screen test ad; closing it continues to where you tapped.
6. **Sound:** music is silent during full-screen ads and comes back afterwards.
7. **Offline:** flight mode, then play. There are no banner or revive offers, and nothing gets stuck.
8. **Europe check** (optional): in `AdMobAds.Begin()`, temporarily add
   `ConsentDebugSettings = new ConsentDebugSettings { DebugGeography = DebugGeography.EEA, TestDeviceHashedIds = { "<id from logcat>" } }`
   to the `ConsentRequestParameters`. The consent form then shows, and **Settings → Privacy choices** appears.

Then put in your real IDs, build, and upload to the closed test track.

## Changing the numbers

In `Game.Ads.cs`:
- `INTERSTITIAL_EVERY = 2`
- `REVIVE_SECONDS = 30`
- `DRIVE_REVIVE_SECONDS = 20`

To turn every ad off (for example, a paid ad-free version), set `AdsConfig.Enabled = false`.
