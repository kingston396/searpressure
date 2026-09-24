# Sear Pressure on Google Play: step by step

> The shortest path, with Android Studio: **`store/RELEASE-TODAY.md`**. This page has more detail on each step.

What's already done in the project:
- Package name `com.kingston.searpressure`
- Version 1.0.0 (build 1)
- IL2CPP, ARM64, target API 36, App Bundle builds
- Icon (including Android adaptive), splash screen
- Back button: goes back, and closes the game from the title screen
- Notch/cutout support
- Online play hidden

What follows is the part only you can do.

> ⚠️ **The package name is permanent.** Once the first build is uploaded, `com.kingston.searpressure`
> can never change. If you want a different one, change `AppId` in
> `SearPressureUnity/Assets/SearPressure/Editor/SearPressureSetup.cs` *before* step 3.

## 1. Test on your phone (before anything else)

1. Install **Unity 6000.3.9f1** from Unity Hub with **Android Build Support**, including the OpenJDK and
   Android SDK & NDK tools it offers.
2. Open `SearPressureUnity`. On first open it sets everything up. Check the menu **Sear Pressure → Apply Store Settings**.
3. **File → Build Profiles → Android → Switch Platform.**
4. On your phone: Settings → About phone → tap *Build number* 7 times; then Developer options → USB debugging on.
   Plug it in.
5. In Build Profiles, untick "Build App Bundle" for now, then click **Build And Run**. This installs a test `.apk` on the phone.
6. Play the tutorial, a few kitchens, the shop and the daily challenge. Try portrait and landscape, and the back button.
   Tell Claude about anything odd, with a screenshot.

## 2. Create your upload key (once, and keep it forever)

Google signs the app for the store. You sign each upload with your own **upload key**.
1. **Edit → Project Settings → Player → Android tab → Publishing Settings → Keystore Manager.**
2. **Keystore → Create New → Anywhere…** Save it **outside** the project folder, e.g.
   `Documents/SearPressure-upload.keystore`. Never commit it to GitHub.
3. Choose a keystore password, then add a key: alias `upload`, a password, validity 50 years, and your name as the owner.
4. **Back up the keystore file and both passwords** (a password manager plus a copy on a USB stick or cloud drive).
   If you lose them, Google can reset the upload key, but it takes days.
5. Unity forgets the passwords when it closes. You'll type them in again before each release build.

## 3. Build the release

1. **Sear Pressure → Release → Next Build Number** before every upload except the very first (build 1).
2. Build Profiles → Android → tick **Build App Bundle (Google Play)** → **Build**. Save as `SearPressure-1.0.0.aab`.

**Or build and sign it in Android Studio:** in Unity, **Sear Pressure → Release → Export Android Studio Project**.
Pick a folder, then open that folder in **Android Studio** (File → Open) and let Gradle sync.
**Build → Generate Signed App Bundle or APK → Android App Bundle**, then choose your upload keystore from step 2 and the **release** variant.
The `.aab` lands in `launcher/release/`. Android Studio's **Run ▶** also installs it on a plugged-in phone.
(GitHub can produce this project too: Actions → Android build → Run workflow → tick "Export an Android Studio project".)

**Or let GitHub build it:** every push to `main` builds a signed `.aab` automatically once the secrets are set up
(see `.github/workflows/README.md`). Download it from the run's **Artifacts**. CI builds are numbered 101, 102, …,
so if you use them, don't mix in hand-made builds with higher numbers.

## 4. Google Play Console

1. Sign up at **play.google.com/console** ($25 once, identity verification can take a few days).
   Use **Ragnarok Talent Partners LLC** as the developer name (an organisation account in the LLC's name).
2. **Create app:** name *Sear Pressure*, default language English (US), **Game**, **Paid**. Accept the declarations.
   Then **Monetise → App pricing → Set price → $4.99 (USD)**; Play converts it for other countries. This needs a **payments profile** (Setup → Payments profile), which Google asks you to create first.
3. Work through **Dashboard → Set up your app**. Every answer is in `store/listing.md`:

| Section | Answer |
|---|---|
| Privacy policy | the public address of `store/privacy-policy.html` (see "Hosting the privacy policy" in `listing.md`) |
| App access | All functionality available without special access |
| Ads | No, my app does not contain ads |
| Content rating | Fill the IARC questionnaire: category *Game*, answers in `listing.md` → Everyone / PEGI 3 |
| Target audience | 13 and over (see `listing.md` for why) |
| News app | No |
| Data safety | See `listing.md` (no data collected) |
| Government app | No |
| Financial features | None |
| Health | None |
| Advertising ID | No (the game doesn't use it) |

4. **Store listing** (Grow → Store presence → Main store listing):
   - App name, short description, full description: copy them from `listing.md`.
   - App icon: `store/google-play/icon-512.png`
   - Feature graphic: `store/google-play/feature-graphic-1024x500.png`
   - Phone screenshots: all 7 from `store/screenshots/google-play-phone/`
   - Category: Game → Casual. Tags: see `listing.md`. Contact email: b.kingston396@gmail.com

## 5. The closed test (new personal accounts only; not needed for your LLC account)

Google requires **at least 12 testers who stay opted in for 14 days in a row** before you can publish to everyone.
1. **Test and release → Testing → Closed testing → Create track** (or use "Alpha").
2. **Testers:** add a Google Group or an email list with 12+ friends' Gmail addresses. More is safer: some people drop out.
3. **Create release → upload `SearPressure-1.0.0.aab`.** Play App Signing is on by default; accept it.
   Release name `1.0.0 (1)`, notes: "First test build".
4. **Review and roll out.** Google reviews it (hours to a few days), then send testers the opt-in link.
   They must accept it and install from the Play Store.
5. Ask testers to play and to leave feedback. Google looks at whether the test was real.
   Fix anything they find and upload new builds (Next Build Number each time).

## 6. Go live

After 14 days: **Dashboard → Apply for production access**. Answer the questions about your test.
Once approved: **Production → Create release** → add the latest `.aab` → roll out (you can start at 20% and increase).

## Every later update

1. Make the changes, then **Sear Pressure → Release → Next Build Number**.
2. Bump the version (Player Settings → Version, e.g. 1.0.1) if players should see a new number.
3. Enter the keystore passwords → Build → upload the `.aab` to a track → roll out.
4. Every August, Google raises the target API level. When Play Console warns you, raise `TargetApi` in
   `SearPressureRelease.cs`, run **Apply Store Settings**, and rebuild.
