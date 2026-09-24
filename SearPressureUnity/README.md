# Sear Pressure — Unity

A Unity copy of the web game in `../OrderUp/index.html`. The web version stays as it is and
is still the reference: this project was ported from it feature for feature, including the kitchens,
the Judge, helpers, the tutorial, stars and unlocks, the shop and upgrades, the wardrobe, the story,
the daily challenge, Delivery Runs and online co-op and versus.

## Open it

1. Unity Hub → **Add project from disk** → pick this `SearPressureUnity` folder. The project was set
   up for **Unity 6000.3.9f1**, the same version as the repo root project.
2. On first open, the editor script makes `Assets/SearPressure/Scenes/Main.unity`, adds it to Build
   Settings and sets the player settings: Gamma colour, auto-rotation, and bundle id
   `com.kingston.searpressure`. If Active Input Handling is "Input System Package (New)", it is set to
   **Both**, and Unity asks to restart.
3. Press **Play**. The game starts in any scene; the `GameHost` component creates itself if it isn't there.

The **Sear Pressure** menu has:
- **Set Up Project**: runs step 2 again.
- **Open Main Scene**.
- **Testing → Unlock Every Kitchen**: a toggle. A player build takes `-unlockall` instead.
- **Testing → Add 5,000 Coins**.
- **Testing → Reset Save**.

- **Apply Store Settings**: icon, splash, version and Android release options (see below).
- **Release → Next Build Number**: run before every store upload.

Saves use PlayerPrefs key `orderup.v1` and the same JSON as the web version's localStorage.

## How it's built

```
Assets/SearPressure/
  Core/        the whole game: plain C#, no UnityEngine (asmdef noEngineReferences)
    Game*.cs     one partial class Game, split like the JS sections, with the JS function names
    Gfx/         Canvas: the browser 2D canvas API (paths, arcs, text, drawImage) turned into triangles
    Audio/       Synth: the web SFX recipes and music loops rendered to PCM
    WsRelayTransport.cs  online play over a WebSocket relay
  Unity/       the host: screen and safe area, touch/mouse/keys, GL drawing, audio, PlayerPrefs
  Editor/      first-open setup and the tester menu
  Resources/SearPressure/
    Data/      game.json (levels, recipes, shop and so on) and sprites.json, exported from the web page
    Fonts/     Pixelify Sans, DM Mono and Nunito (SIL OFL, licences next to them)
    Shaders/   two unlit shaders for the canvas triangles
Server/        relay.js, the online relay (see Server/README.md)
Tools/
  Harness/     a .NET 8 console app that runs the Core headless and draws screenshots with SkiaSharp
  UnityCheck/, EditorCheck/   compile Core + Unity and Editor code against Unity reference assemblies
```

The game draws everything itself, as the web version draws on a `<canvas>`. Menus that were HTML
on the web (title, intro card, shop, wardrobe, results, settings, story, online) are drawn with a
small immediate-mode UI in `Game.Ui.cs` / `Game.Screens.cs`, in the same ticket-card style.
Sprites are built from the same pixel palettes into an atlas at start-up.

The random number generator (`mulberry32`) and seeding match the web version bit for bit. The
Delivery Run cities and the traffic in calm mode come out identical, and daily challenges are the same
kitchen and twist on both versions for a given UTC day.

## Tests (no Unity needed)

```sh
cd Tools/Harness
dotnet build -v q
dotnet run --no-build -- kitchen  /tmp/shots   # salads, soup, burgers, fire, griddle, dishes, coffee
dotnet run --no-build -- kitchen2 /tmp/shots   # fries + delivery, blender, smoker, pizza, tacos, lobsters, Judge, helpers, tutorial
dotnet run --no-build -- screens  /tmp/shots   # every menu screen, story, results, pause, landscape
dotnet run --no-build -- economy  /tmp/shots   # coins, unlocks, buy/sell upgrades, daily, save round trip
dotnet run --no-build -- driveall /tmp/shots   # Delivery Runs: driving, drops, lesson, results
dotnet run --no-build -- ads      /tmp/shots   # revive, interstitial timing, consent button, stuck-state regressions
dotnet run --no-build -- fuzz     /tmp/shots   # every kitchen/run/menu with random input (~2 min)
(cd ../../Server && npm install)
dotnet run --no-build -- net      /tmp/shots   # host + 2 guests over a local relay: co-op, versus, leaving
dotnet run --no-build -- relay    /tmp/shots   # 4-player rooms, full room, bad code, dropped guest
```

Each scenario prints `ok`/`FAIL` lines and writes PNG screenshots to the folder.

## Online play

The web version links phones directly with PeerJS. Unity has no WebRTC built in, so the Unity build
connects through `Server/relay.js`, a small Node WebSocket server. The messages and the host-runs-the-kitchen
design are the same as the web version's. To play online:

1. Deploy `Server/` (Render, Fly or Railway; see `Server/README.md`).
2. Set `NetConfig.RelayUrl` in `Assets/SearPressure/Unity/NetConfig.cs` to its `wss://` address.

Unity WebGL builds can't use `ClientWebSocket`, so online play is off in WebGL.

## Releasing

On load, `Editor/SearPressureRelease.cs` fills in anything still at Unity's defaults:
- **Icon:** `Branding/Icon.png`, plus Android adaptive, round and legacy icons.
- **Splash:** the Sear Pressure logo on navy.
- **Version:** 1.0.0, build 1.
- **Android:** IL2CPP, ARM64 only, target API 36, and builds as an App Bundle (`.aab`).

Settings you change by hand in Player Settings are kept. Google raises the required target API every
August, so update `TargetApi` in that file when Play Console asks.

- **Android:** File → Build Profiles → Android → Switch Platform. In Player Settings → Publishing
  Settings, create a keystore (keep it and its passwords safe: every update must be signed with it).
  Then Build, and upload the `.aab` to Play Console.
- **iOS** (needs a Mac with Xcode): Build Profiles → iOS → Build, open the Xcode project, set your team,
  then Product → Archive → Distribute.

Online play is hidden (`NetConfig.OnlineEnabled = false`) until a relay is deployed.
On tablets the game scales up so the short side is at most about 560 logical pixels.

Store listing text, screenshots, the feature graphic and the privacy policy are in `../store/`.

## Ads and "Remove ads"

The game is free with Google AdMob ads, and a one-time **"Remove ads"** purchase ($4.99, `remove_ads`) switches them off:
- **Game rules:**
  - `Core/Game.Ads.cs`: interstitials after every 2nd service, the rewarded revive (free once ads are removed).
  - `Core/Game.Store.cs`: the offer on the title screen, Restore purchase in Settings.
- **Unity:**
  - `Unity/AdMobAds.cs`: banner, full-screen ads and consent. Compiled when the Google Mobile Ads package is present (the asmdef defines `SEARPRESSURE_ADS`).
  - `Unity/PlayBilling.cs`: Google Play Billing 9 through JNI, plus a pretend store in the editor.
  - `Editor/SearPressureDependencies.xml` adds the billing library to the Gradle build.
- **IDs:** in `Unity/AdsConfig.cs`.

Setup, testing and store forms: `../store/ADMOB.md` and `../store/RELEASE-TODAY.md`.

## Differences from the web version

- **Online:** it goes through the relay (above), and the relay turns away a 5th player itself.
- **Delivery Run bursts:** bumps, smashes and landings reuse the splash/trash sounds instead of
  a separate noise burst. Vibration patterns are single buzzes.
- **Delivery Run city:** it is pre-painted into atlas chunks rather than an offscreen canvas. It looks the same.
- **Text:** it is drawn with Unity's font rasteriser, so letter shapes can differ very slightly from the browser's.
