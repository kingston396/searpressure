# Automatic Android builds

`android.yml` runs on every push to `main` that changes `SearPressureUnity/`, and whenever you start it by hand
(**Actions** tab → **Android build** → **Run workflow**). It:

1. **Runs the game tests**: all the kitchen, menu, economy, Delivery Run and online scenarios, headless in .NET (about 3 min).
2. **Builds with Unity 6000.3.9f1**, on GameCI's Docker image: a Google Play **`.aab`** (release build, real ads),
   plus an **`.apk`** you can install straight on a phone when you run it by hand with "Also build an .apk" ticked.
   The `.apk` is a **Development build, so it shows Google's test ads**; never upload it to Play. It takes about 20–40 min
   the first time and less once Unity's Library folder is cached.
3. Attaches the files to the run: open the run → **Artifacts** → `SearPressure-android-<number>` (a zip).
4. Optionally, on a manual run, **uploads the `.aab` to Google Play** as a draft on the internal or closed (alpha) track.

Build numbers (Android `versionCode`) are **100 + the run number**, so every CI build can be uploaded to Play and
never clashes with builds 1–99 made by hand in Unity.

## Secrets to add

GitHub → the repo → **Settings → Secrets and variables → Actions → New repository secret**. Secrets are encrypted.
GitHub hides them in logs, and nobody (including Claude) can read them back.

### Required: Unity licence (free Personal licence is fine)

| Secret | Value |
|---|---|
| `UNITY_EMAIL` | the email you sign in to Unity with |
| `UNITY_PASSWORD` | your Unity password |
| `UNITY_LICENSE` | the whole contents of your Unity licence file (below) |

To get the licence file: install **Unity Hub**, sign in, and make sure it shows an active Personal licence
(Hub → Preferences/Settings → Licenses → Add → Get a free personal license). Then open this file in a text editor
and copy **all** of it, starting from `<?xml`:
- **Windows:** `C:\ProgramData\Unity\Unity_lic.ulf` (ProgramData is hidden: paste the path into Explorer's address bar)
- **Mac:** `/Library/Application Support/Unity/Unity_lic.ulf` (Finder → Go → Go to Folder…)
- **Linux:** `~/.local/share/unity3d/Unity/Unity_lic.ulf`

If your Unity account uses two-factor login, CI activation can fail. GameCI's docs cover the options
("game.ci/docs/github/activation").

### For Play Store builds: your upload key

Without these, builds are signed with a debug key: they install on a phone, but Google Play rejects them.
Create the key first (see `store/google-play/GUIDE.md`, step 2).

| Secret | Value |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | the keystore file, base64-encoded (below) |
| `ANDROID_KEYSTORE_PASS` | the keystore password |
| `ANDROID_KEYALIAS_NAME` | the key alias (e.g. `upload`) |
| `ANDROID_KEYALIAS_PASS` | the key password |

Base64 the keystore and put it on the clipboard:
- **Windows (PowerShell):** `[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\path\to\SearPressure-upload.keystore")) | Set-Clipboard`
- **Mac:** `base64 -i ~/Documents/SearPressure-upload.keystore | pbcopy`

### Optional: upload to Google Play from GitHub

| Secret | Value |
|---|---|
| `PLAY_SERVICE_ACCOUNT_JSON` | the JSON key of a Google Cloud service account with release access to the app |

1. The **first** `.aab` must be uploaded by hand in Play Console (Google's rule). Do that with a CI build.
2. Play Console → **Setup → API access** → link or create a Google Cloud project → **Create new service account**
   (it opens Google Cloud: create the account, then **Keys → Add key → JSON**, and download it).
3. Back in Play Console → **Users and permissions → Invite new users** → the service account's email →
   **App permissions** → Sear Pressure → *Release to testing tracks* (and *Release to production* if you want it).
4. Paste the whole JSON file into the secret.

Then run the workflow by hand with **Upload to Google Play** set to `internal` or `alpha`. The release arrives as a
**draft**, and you roll it out in Play Console.

## Cost

Public repos: free. Private repos: GitHub Free includes 2,000 Actions minutes a month. One push costs about
25–45 minutes (tests + build), so pushes that don't touch `SearPressureUnity/` don't trigger a build.
