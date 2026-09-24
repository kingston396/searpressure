# Ads in Sear Pressure (Google AdMob) and "Remove ads"

Sear Pressure is **free with ads**. A one-time in-app purchase, **"Remove ads" ($4.99, product ID `remove_ads`)**, turns
every ad off for good and makes the revive free (no ad to watch).
- AdMob App ID: `ca-app-pub-2822796427144413~6568766943`, already set in `AdsConfig.cs` and the Google Mobile Ads settings asset.
- Ad unit IDs, all set in `AdsConfig.cs`:
  - banner `ca-app-pub-2822796427144413/2544520785`
  - interstitial `…/3140426310`
  - rewarded `…/4261847002`
- **Development builds (and the editor) automatically use Google's test units.** Release builds use the real ones.
- The purchase uses Google Play Billing directly (`Unity/PlayBilling.cs`, library version in `Editor/SearPressureDependencies.xml`).
  The editor uses a pretend store, and **Sear Pressure → Testing → Reset "Remove Ads" Purchase** undoes it.
- `SEARPRESSURE_ADS` is defined automatically while the Google Mobile Ads package is installed. Removing the package
  (and setting `AdsConfig.Enabled = false`) makes the build ad-free.

## What the game does

| Ad | When | Code |
|---|---|---|
| **Banner** | Along the bottom **on menus and results screens only**. It is hidden while a kitchen or Delivery Run is being played, including pause and the revive offer, so it's never next to the controls. The screens sit above it. | `AdMobAds.cs`, `Game.BannerAllowed` |
| **Interstitial** | After every **2nd** finished kitchen or Delivery Run, when the player leaves the results card (Retry / Next kitchen / All kitchens). Never in the tutorial. | `Game.Ads.cs`, `INTERSTITIAL_EVERY = 2` |
| **Rewarded revive** | When a kitchen ends with **0 stars** (or on the Judge's **third strike**), the player can watch one ad: **+30 seconds** (Delivery Runs: +20 s), or the last strike is wiped. Once per attempt; not on the daily challenge (it's the same contest for everyone). | `Game.Ads.cs`, `REVIVE_SECONDS` |
| **Consent** | Google's consent form (UMP) runs at start-up. Players in the EEA, UK and Switzerland see it; elsewhere nothing shows. Settings gets a **Privacy choices** button where required by law. | `AdMobAds.Begin()` |

These safeguards are built in:
- A revive ad also counts as the break ad, so the player never gets an interstitial right after one.
- If an ad fails to load, the game simply carries on without it.
- If an ad never reports back, the game continues after 75 s.

## Setting up AdMob

Steps 1–4 are **already done for Android**: the app and its three ad units exist, and their IDs are in `AdsConfig.cs`.
1. ~~Sign up at admob.google.com~~ (done)
2. ~~Add the app~~ (done: App ID `ca-app-pub-2822796427144413~6568766943`)
3. ~~Create the Banner, Interstitial and Rewarded ad units~~ (done)
4. ~~Put the IDs in `AdsConfig.cs`~~ (done)
   - **Development builds and the editor switch to Google's test units automatically.**
   - Only the iOS IDs are still Google's test IDs; fill them in if you ever publish on iPhone.
5. In Unity, run **Sear Pressure → Apply Store Settings**. It copies the App ID into Google's settings file (`Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset`).
6. **Privacy & messaging → GDPR → Create message.** Link it to the app, choose your privacy policy URL and publish it. Without this, the consent form can't show in Europe, and ads there won't serve.
7. **app-ads.txt** (after launch). This tells ad buyers that your AdMob account really owns this app.
   - It must be a plain text file at the root of the website you list as your **developer website** in Play Console (**Grow → Store presence → Store settings → Store listing contact details → Website**).
     Crawlers look **exactly** at `https://<that domain>/app-ads.txt`, not in a sub-folder.
   - The file holds the one line AdMob shows under **Apps → View all apps → app-ads.txt**, e.g.
     `google.com, pub-2822796427144413, DIRECT, f08c47fec0942fa0`
   - Two options that work:
     - **Your own domain** (e.g. the LLC's website): upload `app-ads.txt` to its root.
     - **Free GitHub user site:**
       1. Create a new public repository named exactly **`kingston396.github.io`**.
       2. Add a file `app-ads.txt` with that line, then Settings → Pages → deploy from `main`.
       3. Use `https://kingston396.github.io` as the Play developer website.
     - **Google Sites can't serve a raw text file**, so it won't work for this. A project page like `kingston396.github.io/searpressure/` won't either, because the file must be at the domain root.
   - AdMob checks it within about a day (Apps → app-ads.txt shows "Found").

⚠️ **Never tap your own live ads**, and don't ask friends to. AdMob bans accounts for invalid clicks. While testing, use a
**Development Build** (it shows Google's test ads), or add your phone under AdMob → Settings → Test devices.

## Testing ads on your phone (before every release)

Build with **Development Build ticked** (File → Build Profiles), which uses Google's test units:
1. **Start-up:** the game starts with no consent form (outside Europe). A banner labelled "Test Ad" appears along the bottom of the **title screen** within a few seconds.
2. **Banner:** it shows on menus and results. It **disappears when a kitchen starts**, stays away through "Time's up!", and comes back once the results card is up, in portrait and landscape.
3. **Revive:**
   - Play Salad Days and let the timer run out with 0 coins. **"Keep going?"** appears.
   - Tap **Watch an ad** and watch the test video to the end. You should get **+30 seconds**.
   - Let it run out again. This time it goes straight to the results.
4. **Revive, closed early:** the same, but close the ad early. It goes to the results with no revive.
5. **Interstitial:** finish two services. Leaving the second results card shows a full-screen test ad; closing it continues to where you tapped.
6. **Sound:** music is silent during full-screen ads and comes back afterwards.
7. **Offline:** flight mode, then play. There are no banner or revive offers, and nothing gets stuck.
8. **Remove ads:** see `RELEASE-TODAY.md` Part G. It only works with the app installed from Google Play (internal testing) and your account set as a license tester. That Play build shows **real** ads, so add the phone (and other testers') under **AdMob → Settings → Test devices** before installing it. It can take up to a day to kick in: wait for the "Test mode" label before touching any ad.
9. **Europe check** (optional): in `AdMobAds.Begin()`, temporarily add
   `ConsentDebugSettings = new ConsentDebugSettings { DebugGeography = DebugGeography.EEA, TestDeviceHashedIds = { "<id from logcat>" } }`
   to the `ConsentRequestParameters`. The consent form then shows, and **Settings → Privacy choices** appears.

Then untick Development Build, build the release `.aab` (RELEASE-TODAY Part E) and upload it to **internal testing** (Part G).

## Changing the numbers

In `Game.Ads.cs`:
- `INTERSTITIAL_EVERY = 2`
- `REVIVE_SECONDS = 30`
- `DRIVE_REVIVE_SECONDS = 20`

To turn every ad off (for example, a paid ad-free version), set `AdsConfig.Enabled = false`.
