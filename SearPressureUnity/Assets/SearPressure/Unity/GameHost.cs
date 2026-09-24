using System;
using System.Collections.Generic;
using UnityEngine;

namespace SearPressure.UnityHost
{
    // Runs Sear Pressure inside Unity. The whole game lives in the engine-free SearPressure.Core
    // assembly; this component feeds it the screen size, touches, mouse and keys, draws what it
    // paints each frame (textured triangles) with GL, and plays its synthesized sounds.
    // It starts itself in any scene, so pressing Play is enough.
    [DefaultExecutionOrder(-100)]
    public sealed class GameHost : MonoBehaviour, IPlatform
    {
        public static GameHost Instance;

        Game game;
        Canvas canvas;
        UnityFonts fonts;
        CanvasRenderer2D renderer2D;
        AudioOut audioOut;
        Camera cam;
        double dpr = 1;
        TouchScreenKeyboard keyboard;
        AdMobAds adMob;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
#if UNITY_2021_3_OR_NEWER
            if (FindAnyObjectByType<GameHost>() != null) return;
#else
            if (FindObjectOfType<GameHost>() != null) return;
#endif
            new GameObject("Sear Pressure").AddComponent<GameHost>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Input.multiTouchEnabled = true;

            cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Camera").AddComponent<Camera>();
                cam.transform.SetParent(transform);
                cam.orthographic = true;
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x1c, 0x21, 0x33, 255);
            cam.cullingMask = 0;

            var gameJson = Resources.Load<TextAsset>("SearPressure/Data/game");
            var spritesJson = Resources.Load<TextAsset>("SearPressure/Data/sprites");
            Data.Load(gameJson.text, spritesJson.text);

            fonts = new UnityFonts();
            canvas = new Canvas { Fonts = fonts };
            renderer2D = new CanvasRenderer2D(fonts);
            audioOut = gameObject.AddComponent<AudioOut>();
            game = new Game(this, canvas);
            game.onlineEnabled = NetConfig.OnlineEnabled;
#if UNITY_ANDROID && !UNITY_EDITOR
            game.onQuit = () => Application.Quit();
#endif
            if (AdsConfig.Enabled) { adMob = gameObject.AddComponent<AdMobAds>(); adMob.Begin(); }
            ApplyScreen(true);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }
        void OnApplicationQuit() => game?.AppQuit();

        // ---- IPlatform ----
        public string LoadSave() => PlayerPrefs.GetString(SaveData.Key, "");
        public void WriteSave(string json) { PlayerPrefs.SetString(SaveData.Key, json); PlayerPrefs.Save(); }
        float lastBuzz;
        public void Vibrate(int ms)
        {
#if UNITY_ANDROID || UNITY_IOS
            if (ms >= 20 && Time.unscaledTime - lastBuzz > 0.15f) { lastBuzz = Time.unscaledTime; Handheld.Vibrate(); }
#endif
        }
        public void PlaySound(string name) => audioOut.Play(name);
        public void SetMusic(string key, bool rush) => audioOut.SetMusic(key, rush);
        public DateTime UtcNow => DateTime.UtcNow;
        // Testers: every kitchen open. Toggle it from the Sear Pressure menu in the editor, or
        // launch a build with -unlockall.
        public bool UnlockAll => PlayerPrefs.GetInt("SearPressure.UnlockAll", 0) == 1 || Environment.CommandLine.Contains("-unlockall");
        public bool Calm => false;
        public INetTransport CreateTransport() => NetConfig.Create();
        public IAds Ads => adMob;
        public void OpenKeyboard(string text, int maxLength)
        {
            if (TouchScreenKeyboard.isSupported) keyboard = TouchScreenKeyboard.Open(text, TouchScreenKeyboardType.ASCIICapable, false, false, false, false, "ABCD", maxLength);
        }

        // ---- screen ----
        int lastW, lastH; UnityEngine.Rect lastSafe; float lastBanner;
        void ApplyScreen(bool force)
        {
            float bannerPx = adMob != null ? adMob.BannerHeightPx : 0;
            if (!force && Screen.width == lastW && Screen.height == lastH && Screen.safeArea == lastSafe && bannerPx == lastBanner) return;
            lastW = Screen.width; lastH = Screen.height; lastSafe = Screen.safeArea; lastBanner = bannerPx;
            // Logical pixels like CSS pixels: phones come out around 360-430 wide.
            double d = Screen.dpi > 0 ? Screen.dpi / 160.0 : 1;
            // Tablets: scale up so the short side is at most ~560 logical px. Otherwise the game is a
            // phone-sized layout lost in the middle of the screen (phones are unaffected).
            d = Math.Max(d, Math.Min(Screen.width, Screen.height) / 560.0);
            d = Math.Max(1, Math.Min(6, d));
            if (Screen.width / d < 320) d = Math.Max(0.5, Screen.width / 320.0);
            dpr = d;
            var sa = Screen.safeArea;
            double st = (Screen.height - sa.yMax) / d, sb = sa.yMin / d, sl = sa.xMin / d, sr = (Screen.width - sa.xMax) / d;
            // The AdMob banner sits along the bottom: keep the game (and its controls) above it.
            sb += bannerPx / d;
            game.Resize(Screen.width / d, Screen.height / d, d, st, sr, sb, sl);
        }

        // ---- the frame ----
        void Update()
        {
            ApplyScreen(false);
            ReadInput();
            game.Frame(Time.unscaledDeltaTime);
            audioOut.SetEngine(game.engineLevel);
            if (keyboard != null)
            {
                if (keyboard.status == TouchScreenKeyboard.Status.Visible || keyboard.status == TouchScreenKeyboard.Status.Done)
                {
                    string t = (keyboard.text ?? "").ToUpperInvariant();
                    var clean = new System.Text.StringBuilder();
                    foreach (char ch in t) if (ch >= 'A' && ch <= 'Z' && clean.Length < 4) clean.Append(ch);
                    game.typed = clean.ToString();
                }
                if (keyboard.status != TouchScreenKeyboard.Status.Visible) keyboard = null;
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            renderer2D.Draw(canvas, Screen.width, Screen.height);
        }

        void OnApplicationPause(bool pause) { if (pause) { game.Hidden(); game.Blur(); } }
        void OnApplicationFocus(bool focus) { if (!focus) { game.Hidden(); game.Blur(); } }

        // ---- input: touches and the mouse become pointers, keys use the browser's key names ----
        readonly HashSet<int> downTouches = new HashSet<int>();
        bool mouseDown;
        const int MouseId = 1000;

        void ReadInput()
        {
            double X(float px) => px / dpr;
            double Y(float py) => (Screen.height - py) / dpr;
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    switch (t.phase)
                    {
                        case TouchPhase.Began: downTouches.Add(t.fingerId); game.PointerDown(t.fingerId, X(t.position.x), Y(t.position.y)); break;
                        case TouchPhase.Moved:
                        case TouchPhase.Stationary: if (downTouches.Contains(t.fingerId)) game.PointerMove(t.fingerId, X(t.position.x), Y(t.position.y)); break;
                        case TouchPhase.Ended:
                        case TouchPhase.Canceled: if (downTouches.Remove(t.fingerId)) game.PointerUp(t.fingerId, X(t.position.x), Y(t.position.y)); break;
                    }
                }
            }
            else if (!Input.touchSupported || Application.isEditor)
            {
                var mp = Input.mousePosition;
                if (Input.GetMouseButtonDown(0)) { mouseDown = true; game.PointerDown(MouseId, X(mp.x), Y(mp.y)); }
                else if (mouseDown && Input.GetMouseButton(0)) game.PointerMove(MouseId, X(mp.x), Y(mp.y));
                if (mouseDown && Input.GetMouseButtonUp(0)) { mouseDown = false; game.PointerUp(MouseId, X(mp.x), Y(mp.y)); }
                float wheel = Input.mouseScrollDelta.y;
                if (wheel != 0) game.Scroll(-wheel * 40);
            }
            foreach (var (key, code) in Keys)
            {
                if (Input.GetKeyDown(key)) game.KeyDown(code, false);
                if (Input.GetKeyUp(key)) game.KeyUp(code);
            }
            if (!string.IsNullOrEmpty(Input.inputString)) game.TextInput(Input.inputString);
        }

        static readonly (KeyCode, string)[] Keys =
        {
            (KeyCode.W, "KeyW"), (KeyCode.A, "KeyA"), (KeyCode.S, "KeyS"), (KeyCode.D, "KeyD"),
            (KeyCode.UpArrow, "ArrowUp"), (KeyCode.DownArrow, "ArrowDown"), (KeyCode.LeftArrow, "ArrowLeft"), (KeyCode.RightArrow, "ArrowRight"),
            (KeyCode.Space, "Space"), (KeyCode.Return, "Enter"), (KeyCode.KeypadEnter, "Enter"), (KeyCode.J, "KeyJ"), (KeyCode.K, "KeyK"),
            (KeyCode.E, "KeyE"), (KeyCode.Q, "KeyQ"), (KeyCode.Tab, "Tab"), (KeyCode.LeftShift, "ShiftLeft"),
            (KeyCode.Escape, "Escape"), (KeyCode.P, "KeyP"), (KeyCode.Backspace, "Backspace"),
        };
    }
}
