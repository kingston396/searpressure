# Release Sear Pressure today: Unity → Android Studio → Google Play

Sear Pressure 1.0.0 (build 1) · package `com.kingston.searpressure` · **Paid $4.99, no ads, no in-app purchases**

## Your account: an organisation (LLC)

Because the developer account belongs to your LLC (an **organisation account**), Google's "12 testers for 14 days"
closed-test rule for new personal accounts **does not apply**. You can publish straight to **production** today.
What's left is Google's own verification of the account (if not already done) and its review of a new app, which
usually takes from a few hours to a few days.

---|---|
| **New personal account** (made after Nov 2023) | Upload the build and get it into **internal and closed testing** today. Google then requires **12 testers opted in for 14 days** before you can apply for production. Identity verification can also take a few days first. |
| **Organisation account**, or a personal account from before Nov 2023 | You can go to **production** today. Google's first review of a new app usually takes a few hours to a few days. |

Either way, everything below can be done today.

---

## Part A: things to install (about 30–60 min, one time)

1. **Unity Hub**, then **Unity 6000.3.9f1** with the **Android Build Support** module. Tick its **OpenJDK** and **Android SDK & NDK Tools** too.
2. **Android Studio** (the current version, from developer.android.com/studio). It brings its own Java and SDK.
3. The project: download `main` from GitHub (Code → Download ZIP) or `git clone` it, and unzip it to a folder **without spaces** (e.g. `C:\dev\searpressure`).

## Part B: open it and test on your phone (about 30 min)

1. Unity Hub → **Add → Add project from disk** → pick `searpressure/SearPressureUnity` → open it with 6000.3.9f1.
   The first import takes a few minutes. If the Console shows **"Assembly with name 'Unity.AI…' already exists"**:
   Window → Package Manager → *In Project* → remove **AI Generators** (and **AI Assistant**).
2. Menu **Sear Pressure → Apply Store Settings**. This sets the icon, splash, version, package name and release options.
3. **File → Build Profiles → Android → Switch Platform** (takes a few minutes the first time).
4. On your phone:
   - Settings → About phone → tap **Build number** 7 times.
   - Settings → Developer options → **USB debugging** on.
   - Plug the phone in and allow the computer.
5. In Build Profiles, **untick "Build App Bundle"**, then click **Build And Run** and save it as `test.apk`. It installs and starts on the phone.
6. **Phone checklist:**
   - [ ] **Sound:** music plays on the title, and effects play when you chop, serve and when a fire starts. Settings → Sound off/on works.
   - [ ] **Touch:** the joystick moves the chef; Grab, Chop and Swap respond; tickets zoom when tapped.
   - [ ] **Back button:**
     - in a kitchen, it pauses;
     - on the results card, it goes to the kitchen list;
     - on the title, it closes the game.
     - On Android 16 phones, check this especially carefully.
   - [ ] **Rotation:** portrait and landscape both lay out properly, with nothing under the notch or the gesture bar.
   - [ ] **Progress survives restarts:** play a kitchen, force-close the app, reopen it, and the stars are still there.
   - [ ] **Tutorial:** it plays to the end.
   - [ ] **Daily challenge:** it starts.
   - [ ] **Shop:** buying something works.
   - [ ] **Vibration:** short taps when serving, a longer buzz for a strike. Settings → Vibration off stops it.
   If anything looks wrong, take a screenshot and send it to Claude before going on.

## Part C: make the upload key (10 min, once, keep it forever)

Either in Unity (**Project Settings → Player → Android → Publishing Settings → Keystore Manager → Create New → Anywhere**)
or later in Android Studio's signing wizard (**Create new…**):
- Save it **outside** the project, e.g. `Documents/SearPressure-upload.jks`.
- Alias `upload`, validity 50 years, your name as the owner.
- **Back up the file and both passwords** (a password manager plus a USB stick or cloud drive). Every future update must be signed with it.

## Part D: build the release in Android Studio (15–30 min)

1. In Unity: **Sear Pressure → Release → Export Android Studio Project**. Choose a folder outside the project, e.g. `C:\dev\SearPressure-AndroidStudio`. Wait for "Build completed".
2. **Android Studio → Open** → that folder. Let **Gradle sync** finish (the first time downloads a lot, 5–10 min).
   - If it asks to upgrade the Android Gradle Plugin, choose **"Don't remind me"** and keep Unity's version.
   - If it says an SDK platform is missing, click the **Install** link it shows.
3. **Build → Generate Signed App Bundle or APK → Android App Bundle → Next.**
4. Pick your keystore (or **Create new…**), enter the passwords, alias `upload` → **Next**.
5. Choose the **release** variant → **Create**.
6. When it finishes, click **locate**. The file is `launcher/release/launcher-release.aab`. Rename it `SearPressure-1.0.0-1.aab`.
7. Optional check: with the phone plugged in, **Run ▶ (launcher)** installs the release build. Play for a minute.

(The GitHub **Android build** workflow can also produce the signed `.aab` or this Android Studio project, once its secrets are set. See `.github/workflows/README.md`.)

## Part E: Google Play Console (about 1–2 hours of forms)

1. **play.google.com/console** → pay the $25 fee and verify your identity.
   The account is in the **LLC's name**: organisation verification uses the LLC's **D-U-N-S number**. If you
   don't have one, it's free from Dun & Bradstreet, but it can take a few days. Your developer name on the store
   is the LLC's (or the name you choose in Settings → Developer account → Account details).
   Also: **Setup → Payments profile**. A paid app needs a **business** payments profile in the LLC's name, with the
   LLC's bank account and tax details (EIN).
2. **Create app:**
   - Sear Pressure, English (US), **Game**, **Paid**.
   - Accept the declarations.
3. **Put the privacy policy online** (Play needs a public link). Pick one:
   - **Google Sites:** new site → paste the text of `store/privacy-policy.html` → Publish. This takes 10 minutes.
   - **GitHub Pages:** repo Settings → Pages → Branch `main`, folder `/root`. The link is `https://kingston396.github.io/searpressure/store/privacy-policy.html`. Private repos need a paid GitHub plan for Pages.
4. **App content** (left menu → Policy → App content). Answers:

   | Form | Answer |
   |---|---|
   | Privacy policy | your link from step 3 |
   | Ads | **No, my app does not contain ads** |
   | App access | All functionality available without special access |
   | Content rating | IARC questionnaire, category *Game*: no violence (cartoon kitchen fires), no user interaction, no sharing of location → **Everyone / PEGI 3** |
   | Target audience | **13 and over** |
   | Data safety | **No data collected or shared** (see `listing.md` for the note on Unity diagnostics) |
   | Advertising ID | **No** |
   | Government / Financial / Health / News | No / None / None / No |

5. **Monetise → App pricing:**
   - Set the price to **$4.99 USD**. Play fills in local prices for other countries; review them and save.
6. **Store listing** (Grow → Store presence → Main store listing). Everything is in `store/`:
   - **Name, short description, full description:** from `listing.md`.
   - **App icon:** `google-play/icon-512.png`.
   - **Feature graphic:** `google-play/feature-graphic-1024x500.png`.
   - **Phone screenshots:** the 7 in `screenshots/google-play-phone/`.
   - **Tablet screenshots** (optional): `screenshots/ipad-13/` works for the 10-inch slot.
   - **Category and contact:** Game → Casual; email b.kingston396@gmail.com.
7. **Countries:** Production → Countries/regions → add the countries you want (or all).

## Part F: upload and roll out (15 min)

1. **Optional quick check (recommended):** **Test and release → Testing → Internal testing → Create new release.**
   - Upload `SearPressure-1.0.0-1.aab` and accept **Play App Signing**.
   - Release name `1.0.0 (1)`, notes "First release". **Next → Save → Roll out.**
   - Add your own Google account as a tester, open the opt-in link on your phone, and install it from the Play Store.
     Internal test builds are usually available within minutes. This is the exact file customers will get.
2. **Production:** **Test and release → Production → Create new release** → **Add from library** (the same bundle)
   → release notes → **Next → Save → Send for review** (Publishing overview → **Send changes for review**).
   You can start the rollout at 20% and raise it once you've seen it on real phones.
3. Google reviews the new app (usually hours to a few days). When it's approved, it goes live in the countries you picked.

## Part G: after launch

- **Every update:**
  1. In Unity, run **Sear Pressure → Release → Next Build Number** (and bump the version for players, e.g. 1.0.1).
  2. **Export Android Studio Project**, then **Generate Signed App Bundle** with the **same keystore**.
  3. Upload to a track.
- **Google's target API level** rises every August. When Play Console warns you, change `TargetApi` in `SearPressureRelease.cs`.
- **Pre-launch report:** Play tests your build on real devices automatically. Look at it under Release → Pre-launch report, and send Claude any crashes.
