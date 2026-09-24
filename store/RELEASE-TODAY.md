# Release Sear Pressure today: Unity → Android Studio → Google Play

Sear Pressure 1.0.0 (build 1) · package `com.kingston.searpressure` · **Free with ads** · one-time in-app purchase
**"Remove ads" $4.99** (product ID `remove_ads`), which also makes revives free.

## Your account: an organisation (LLC)

The developer account belongs to Ragnarok Talent Partners LLC (an **organisation account**), so Google's
"12 testers for 14 days" closed-test rule for new personal accounts **does not apply**. You can publish straight to
**production** today. What's left is Google's verification of the account (if not already done) and its review of a
new app, which usually takes from a few hours to a few days.

---

## Part A: things to install (about 30–60 min, one time)

1. **Unity Hub**, then **Unity 6000.3.9f1** with the **Android Build Support** module. Tick its **OpenJDK** and **Android SDK & NDK Tools** too.
2. **Android Studio** (the current version, from developer.android.com/studio). It brings its own Java and SDK.
3. The project: download `main` from GitHub (Code → Download ZIP) or `git clone` it, and unzip it to a folder **without spaces** (e.g. `C:\dev\searpressure`).

## Part B: AdMob (10 min)

Already in the project: App ID `ca-app-pub-2822796427144413~6568766943` and the three ad units:
- Banner `…/2544520785`
- Interstitial `…/3140426310`
- Rewarded `…/4261847002`

**Development builds automatically use Google's test ads instead**, so testing on your own phone never serves (or lets you tap) your real ads. Release builds use the real ones.
1. **Privacy & messaging → GDPR → Create message.** Link it to the app, add your privacy-policy URL (Part F step 3) and publish it.
   Without it, the consent form can't show, and ads won't serve in Europe.
2. After the app is live on Google Play: **Apps → Sear Pressure → App settings → Link to app store**. Also put the `app-ads.txt` line
   AdMob gives you on your website (see `store/ADMOB.md`).

⚠️ Never tap your own ads in the store version. AdMob bans accounts for invalid clicks.

## Part C: open it and test on your phone (about 30 min)

1. Unity Hub → **Add → Add project from disk** → pick `searpressure/SearPressureUnity` → open it with 6000.3.9f1.
   - The first import downloads Google Mobile Ads and the External Dependency Manager, which takes a few minutes.
   - If the Console shows **"Assembly with name 'Unity.AI…' already exists"**: Window → Package Manager → *In Project* → remove **AI Generators** (and **AI Assistant**).
   - If the Dependency Manager asks to **enable Android auto-resolution** or **Gradle templates**, choose **Enable**.
2. Menu **Sear Pressure → Apply Store Settings**. This sets the icon, splash, version, package name and release options, and copies the AdMob App ID into place.
3. **File → Build Profiles → Android → Switch Platform** (takes a few minutes the first time).
   Then **Assets → External Dependency Manager → Android Resolver → Force Resolve**. This pulls in Google Mobile Ads and Play Billing.
4. On your phone:
   - Settings → About phone → tap **Build number** 7 times.
   - Settings → Developer options → **USB debugging** on.
   - Plug the phone in and allow the computer.
5. In Build Profiles, **untick "Build App Bundle"** and **tick "Development Build"** (so you get test ads), then click **Build And Run** and save it as `test.apk`. It installs and starts on the phone.
6. **Phone checklist:**
   - [ ] **Sound:** music plays on the title, and effects play when you chop, serve and when a fire starts.
   - [ ] **Banner:** a "Test Ad" banner sits along the bottom of the title screen. It **disappears when a kitchen starts** and comes back on the results card.
   - [ ] **Interstitial:** finish 2 kitchens. Leaving the 2nd results card shows a full-screen test ad; closing it carries on.
   - [ ] **Revive:** let Salad Days run out with 0 coins → **Keep going?** → **Watch an ad** → watch it to the end → +30 seconds.
   - [ ] **Remove ads button:** it shows on the title. In this sideloaded build it says Google Play isn't available. That's expected: purchases only work once the app is on Play (Part G).
   - [ ] **Touch:** the joystick moves the chef; Grab, Chop and Swap respond; tickets zoom when tapped.
   - [ ] **Back button:**
     - in a kitchen, it pauses;
     - on the results card, it goes to the kitchen list;
     - on the title, it closes the game.
     - On Android 16 phones, check this especially carefully.
   - [ ] **Progress survives restarts:** play a kitchen, force-close the app, reopen it, and the stars are still there.
   - [ ] **Vibration:** short taps when serving, a longer buzz for a strike.
   If anything looks wrong, take a screenshot and send it to Claude before going on.

## Part D: make the upload key (10 min, once, keep it forever)

Either in Unity (**Project Settings → Player → Android → Publishing Settings → Keystore Manager → Create New → Anywhere**)
or later in Android Studio's signing wizard (**Create new…**):
- Save it **outside** the project, e.g. `Documents/SearPressure-upload.jks`.
- Alias `upload`, validity 50 years, your name as the owner.
- **Back up the file and both passwords** (a password manager plus a USB stick or cloud drive). Every future update must be signed with it.

## Part E: build the release in Android Studio (15–30 min)

1. In Unity: **Sear Pressure → Release → Export Android Studio Project**. Choose a folder outside the project, e.g. `C:\dev\SearPressure-AndroidStudio`. Wait for "Build completed".
2. **Android Studio → Open** → that folder. Let **Gradle sync** finish (the first time downloads a lot, 5–10 min).
   - If it asks to upgrade the Android Gradle Plugin, choose **"Don't remind me"** and keep Unity's version.
   - If it says an SDK platform is missing, click the **Install** link it shows.
3. **Build → Generate Signed App Bundle or APK → Android App Bundle → Next.**
4. Pick your keystore (or **Create new…**), enter the passwords, alias `upload` → **Next**.
5. Choose the **release** variant → **Create**.
6. When it finishes, click **locate**. The file is `launcher/release/launcher-release.aab`. Rename it `SearPressure-1.0.0-1.aab`.

(The GitHub **Android build** workflow can also produce the signed `.aab` or this Android Studio project, once its secrets are set. See `.github/workflows/README.md`.)

## Part F: Google Play Console (about 1–2 hours of forms)

1. **play.google.com/console**, in the LLC's name.
   - Organisation verification uses the LLC's **D-U-N-S number**. If you don't have one, it's free from Dun & Bradstreet, but it can take a few days.
   - **Setup → Payments profile:** selling "Remove ads" needs a **business** payments profile in the LLC's name, with its bank account and tax details (EIN).
2. **Create app:**
   - Sear Pressure, English (US), **Game**, **Free**.
   - Accept the declarations.
3. **Put the privacy policy online** (Play and AdMob both need a public link). Pick one:
   - **Google Sites:** new site → paste the text of `store/privacy-policy.html` → Publish. This takes 10 minutes.
   - **GitHub Pages:** repo Settings → Pages → Branch `main`, folder `/root`. The link is `https://kingston396.github.io/searpressure/store/privacy-policy.html`. Private repos need a paid GitHub plan for Pages.
4. **App content** (left menu → Policy → App content). Answers:

   | Form | Answer |
   |---|---|
   | Privacy policy | your link from step 3 |
   | Ads | **Yes, my app contains ads** |
   | App access | All functionality available without special access |
   | Content rating | IARC questionnaire, category *Game*: no violence (cartoon kitchen fires), no user interaction, no sharing of location; "digital purchases": **Yes** → **Everyone / PEGI 3** |
   | Target audience | **13 and over** |
   | Data safety | AdMob's data (approximate location, device IDs, app interactions, diagnostics): see `listing.md` |
   | Advertising ID | **Yes**, for Advertising and Analytics |
   | Government / Financial / Health / News | No / None / None / No |

5. **Store listing** (Grow → Store presence → Main store listing). Everything is in `store/`:
   - **Name, short description, full description:** from `listing.md`.
   - **App icon:** `google-play/icon-512.png`.
   - **Feature graphic:** `google-play/feature-graphic-1024x500.png`.
   - **Phone screenshots:** the 7 in `screenshots/google-play-phone/`.
   - **Tablet screenshots** (optional): `screenshots/ipad-13/` works for the 10-inch slot.
   - **Category and contact:** Game → Casual; email b.kingston396@gmail.com.
   - **Website** (Store settings → Store listing contact details): the domain that will serve `app-ads.txt`. See `store/ADMOB.md` step 7; a free option is `https://kingston396.github.io`.
6. **Countries:** Production → Countries/regions → add the countries you want (or all).

## Part G: upload, create "Remove ads", test it, go live

1. **Internal testing first** (Google only lets you create the in-app product once a build using Play Billing is uploaded):
   - **Test and release → Testing → Internal testing → Create new release.**
   - Upload `SearPressure-1.0.0-1.aab`, accept **Play App Signing**, name it `1.0.0 (1)`.
   - **Next → Save → Roll out.**
   - Add your own Google account as a tester.
2. **Create the product:** **Monetise with Play → Products → One-time products → Create one-time product**
   - Product ID **`remove_ads`**. It must be exactly this, and it can't be changed later.
   - Name "Remove ads", description "Removes all ads from Sear Pressure. Revives become free too."
   - Purchase option: **Buy**, price **$4.99 USD** (Play fills in other countries).
   - Save, then **Activate**.
3. **Test the purchase without paying:** **Settings (the Play Console home page's gear) → License testing** → add your Gmail (and anyone else testing) → Save.
   - ⚠️ This Play-installed build shows your **real** ads. First add your phone under **AdMob → Settings → Test devices** (AdMob shows how to find its ID), so you only get test ads on it. Never tap real ads on your own phone.
   - On your phone, open the internal-testing opt-in link and install **from the Play Store**.
   - On the title screen, **Remove ads · <price>** should show the price in your Play country's currency. If it shows just "Remove ads", the product isn't active yet, or it was created only minutes ago.
   - Tap it and choose the **test card ("always approves")**.
   - The ads disappear and the title says "Ads removed".
   - Uninstall and reinstall, then **Settings → Restore purchase**: ads stay off.
   - Try **"slow test card, approves after a few minutes"** too: the game shows "Payment pending", and the ads go once it clears.
4. **Production:** **Test and release → Production → Create new release** → **Add from library** (the same bundle)
   → release notes → **Next → Save → Send changes for review**.
   - You can start the rollout at 20% and raise it once it's been seen on real phones.
   - Google reviews the new app (usually hours to a few days), then it goes live in the countries you picked.

## Part H: after launch

- **AdMob:** link the app to its Play listing and publish `app-ads.txt` (Part B step 2 and `store/ADMOB.md` step 7). Check the numbers the next day.
- **Every update:**
  1. In Unity, run **Sear Pressure → Release → Next Build Number** (and bump the version for players, e.g. 1.0.1).
  2. **Export Android Studio Project**, then **Generate Signed App Bundle** with the **same keystore**.
  3. Upload to a track.
- **Google raises the required target API level and Play Billing Library version** over time. When Play Console warns you:
  - change `TargetApi` in `SearPressureRelease.cs`;
  - change the billing version in `Editor/SearPressureDependencies.xml`.
- **Pre-launch report:** Play tests your build on real devices automatically. Look at it under Release → Pre-launch report, and send Claude any crashes.
