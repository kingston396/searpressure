using System;
using System.Collections.Generic;
using System.Linq;

namespace SearPressure
{
    // What the game needs from the engine it runs in (Unity, or the test harness).
    public interface IPlatform
    {
        string LoadSave();
        void WriteSave(string json);
        void Vibrate(int ms);
        void PlaySound(string name);                 // one of the Synth sound names
        void SetMusic(string key, bool rush);        // null stops the music
        DateTime UtcNow { get; }
        bool UnlockAll { get; }                      // testers: every kitchen open (the web version's #unlockall)
        bool Calm { get; }                           // tests: no mirroring, crate shuffles or events (the web version's ?calm)
        INetTransport CreateTransport();             // online play; null when not available
        void OpenKeyboard(string text, int maxLength);
        IAds Ads { get; }                            // banner/interstitial/rewarded ads; null for none
        IStore Store { get; }                        // the "Remove ads" purchase; null when there's no store
    }

    public sealed class Pt { public double x, y, r; public Pt(double x, double y, double r = 0) { this.x = x; this.y = y; this.r = r; } }
    public sealed class Rect { public double x, y, w, h; public Rect(double x, double y, double w, double h) { this.x = x; this.y = y; this.w = w; this.h = h; } }

    public sealed class Layout
    {
        public bool land;
        public Rect bar, orders, kitchen;
        public Pt pause, joyRest, btnA, btnB, btnS;
        public double ctrlR;
    }

    public sealed class Joy { public int? id; public double bx, by, x, y; }
    public sealed class InputState
    {
        public bool grab, chop, swap, chopHeld;
        public int? chopId;
        public Dictionary<string, bool> keys = new Dictionary<string, bool>();
        public Joy joy = new Joy();
        public double flashA, flashB, flashS;
    }

    // The whole game. The web version keeps its state in globals and plain functions; here they
    // are members of one partial class, split over several files, with the same names so each
    // piece can be checked against the original.
    public sealed partial class Game
    {
        public static Game I;
        public readonly IPlatform platform;
        public Canvas X;
        public SaveData save;

        public double W = 390, H = 844, DPR = 1;
        public double safeT, safeR, safeB, safeL;
        public readonly Layout L = new Layout();
        public double T;
        public Kitchen G;
        public bool paused;
        public readonly InputState input = new InputState();
        public const double JOY_R = 52;
        public const double P = 1.0 / 16;

        // Constants from the web version.
        public const double BLEND_TIME = 3.5, BREW_TIME = 4, WASH_TIME = 1.5, FIRE_SPREAD = 4.5, SPRAY_RANGE = 2.7, LOBSTER_HOP = 3.5, CHOP_TIME = 2.0;
        public const double POT_COOK = 9, POT_BURN = 10, PAN_COOK = 7, PAN_BURN = 9, SPEED = 4.2, CHEF_R = 0.3, TIP_MAX = 10, MISS_PENALTY = 10, PLATE_RETURN = 5;
        public const double DRIVER_WAIT = 8, DELIVERY_BONUS = 5;
        public const int MAX_PLAYERS = 4;
        public const double COOP_STEP = 0.25;
        public const int TUTORIAL_IDX = -1, DAILY_IDX = -2;

        public Game(IPlatform platform, Canvas canvas)
        {
            I = this;
            this.platform = platform;
            X = canvas;
            save = SaveData.FromJson(platform.LoadSave());
        }

        public void persist() { try { platform.WriteSave(save.ToJson()); } catch (Exception) { } }
        public void sfx(string n)
        {
            if (NET.role == "host" && G != null && !G.versus) NET.events.Add(new object[] { "s", n });
            if (!save.muted) platform.PlaySound(n);
        }
        public void buzz(int ms) { if (!save.buzz) return; try { platform.Vibrate(ms); } catch (Exception) { } }
        public bool unlockAll() => platform.UnlockAll;
        public bool CALM => platform.Calm;
        public static double Random() => Rng.Random();

        // ---- screen size, safe areas and layout ----
        public void Resize(double w, double h, double dpr, double st, double sr, double sb, double sl)
        {
            W = w; H = h; DPR = dpr; safeT = st; safeR = sr; safeB = sb; safeL = sl;
            layout();
        }

        public void layout()
        {
            bool land = W > H * 1.1;
            L.land = land;
            double top = safeT + 8, left = safeL + 10, right = W - safeR - 10;
            L.bar = new Rect(left, top, right - left, 40);
            L.pause = new Pt(left + 20, top + 20, 18);
            L.orders = new Rect(left, top + 48, right - left, land ? 70 : 84);
            double hudBottom = L.orders.y + L.orders.h + 6;
            double r = Math.Max(32, Math.Min(44, Math.Min(W, H) * 0.095));
            L.ctrlR = r;
            if (land)
            {
                double side = Math.Min(180, W * 0.19);
                L.kitchen = new Rect(safeL + side, hudBottom, W - safeL - safeR - side * 2, H - hudBottom - safeB - 6);
                L.joyRest = new Pt(safeL + side * 0.5, H - safeB - Math.Min(side * 0.55, 110));
                L.btnA = new Pt(W - safeR - r - 22, H - safeB - r - 26);
            }
            else
            {
                double ctrlH = Math.Max(136, Math.Min(186, H * 0.21));
                L.kitchen = new Rect(safeL + 8, hudBottom, W - safeL - safeR - 16, H - hudBottom - ctrlH - safeB);
                L.joyRest = new Pt(safeL + Math.Min(100, W * 0.24), H - safeB - ctrlH / 2);
                L.btnA = new Pt(W - safeR - r - 20, H - safeB - ctrlH / 2 + r * 0.3);
            }
            L.btnB = new Pt(L.btnA.x - r * 2.25, L.btnA.y + r * 0.35);
            L.btnS = new Pt(L.btnA.x - r * 0.35, L.btnA.y - r * 1.85);
            if (save.lefty) foreach (var p in new[] { L.joyRest, L.btnA, L.btnB, L.btnS }) p.x = W - p.x;
        }
        public bool stickSide(double x, double frac) => save.lefty ? x > W * (1 - frac) : x < W * frac;

        // ---- the frame ----
        bool booted;
        public void Frame(double dt)
        {
            if (!booted) { booted = true; Boot(); }
            dt = Math.Min(0.05, dt);
            T += dt;
            update(dt);
            adsTick();
            RenderFrame(dt);
            musicTick();
        }

        void Boot()
        {
            layout();
            syncMute();
            syncTutorialButton();
            buildLevelList();
            show("scr-title");
        }

        // Draw the whole frame; if a font texture was rebuilt part-way (stale glyphs), draw it again.
        // Draw the current frame again without advancing anything (the host calls this after a layout change).
        public void Redraw() => RenderFrame(0);

        void RenderFrame(double dt)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                X.Reset();
                X.Fonts?.TakeRebuilt();
                render(attempt == 0 ? dt : 0);
                UiFrame(attempt == 0 ? dt : 0);
                if (X.Fonts == null || !X.Fonts.TakeRebuilt()) break;
            }
        }

        public void render(double dt)
        {
            X.setTransform(DPR, 0, 0, DPR, 0, 0);
            X.imageSmoothingEnabled = false;
            X.fillStyle = "#1c2133"; X.fillRect(0, 0, W, H);
            if (D != null) { renderDrive(dt); return; }
            if (G == null) { drawBackdrop(); return; }
            kitchenDt = paused ? 0 : dt;
            drawPlayBackdrop();
            drawKitchen();
            drawHUD(dt);
            if (G.lv.judge) drawJudge();
            drawControls();
            if (G.versus) drawVsBoard();
            drawZoom(dt);
            if (G.tut != null) drawTutorialUI();
            drawBanner();
        }

        // ---- input ----
        public static double dist(Pt a, double x, double y) => U.Hypot(a.x - x, a.y - y);

        public void PointerDown(int id, double x, double y)
        {
            if (UiPointerDown(id, x, y)) return;
            if (D != null) { drivePointerDown(id, x, y); return; }
            if (G == null || G.phase == "over" || paused) return;
            if (dist(L.pause, x, y) < 30) { pauseGame(); return; }
            if (G.zoom != null) { G.zoom = null; return; }
            var tk = ticketAt(x, y);
            if (tk != null) { G.zoom = new Zoom { id = tk.id, t = 4 }; sfx("pick"); return; }
            double r = L.ctrlR;
            if (dist(L.btnA, x, y) < r * 1.2) { input.grab = true; input.flashA = 1; return; }
            if (dist(L.btnB, x, y) < r * 1.1) { input.chop = true; input.chopHeld = true; input.chopId = id; input.flashB = 1; return; }
            if ((NET.role == null || G.versus) && dist(L.btnS, x, y) < r * 0.9) { input.swap = true; input.flashS = 1; return; }
            if (input.joy.id == null && stickSide(x, 0.55) && y > L.orders.y + L.orders.h)
            {
                input.joy.id = id; input.joy.bx = x; input.joy.by = y; input.joy.x = x; input.joy.y = y;
            }
        }

        public void PointerMove(int id, double x, double y)
        {
            UiPointerMove(id, x, y);
            if (id == input.joy.id) { input.joy.x = x; input.joy.y = y; }
            if (D != null) drivePointerMove(id, x, y);
        }

        public void PointerUp(int id, double x, double y)
        {
            UiPointerUp(id, x, y);
            if (id == input.joy.id) input.joy.id = null;
            if (id == input.chopId) { input.chopHeld = false; input.chopId = null; }
            if (D != null) drivePointerUp(id);
        }

        static readonly Dictionary<string, double[]> MOVE_KEYS = new Dictionary<string, double[]>
        {
            ["ArrowUp"] = new double[] { 0, -1 }, ["KeyW"] = new double[] { 0, -1 }, ["ArrowDown"] = new double[] { 0, 1 }, ["KeyS"] = new double[] { 0, 1 },
            ["ArrowLeft"] = new double[] { -1, 0 }, ["KeyA"] = new double[] { -1, 0 }, ["ArrowRight"] = new double[] { 1, 0 }, ["KeyD"] = new double[] { 1, 0 },
        };

        // Key codes use the browser's names (KeyW, ArrowUp, Space, Escape...).
        public void KeyDown(string code, bool repeat)
        {
            if (UiKeyDown(code)) return;
            if (MOVE_KEYS.ContainsKey(code)) { input.keys[code] = true; return; }
            if (D != null && !repeat)
            {
                if (code == "Escape" || code == "KeyP") { if (paused) resumeGame(); else pauseGame(); }
                else if (code == "Space" || code == "ShiftLeft" || code == "KeyJ") D.hand = true;
                else if (code == "Enter") driveEnterKey();
                return;
            }
            if (G == null || repeat) return;
            if (code == "Escape" || code == "KeyP") { if (paused) resumeGame(); else if (G.phase != "over") pauseGame(); return; }
            if (paused || G.phase == "over") return;
            if (code == "Space" || code == "KeyJ" || code == "Enter") { input.grab = true; input.flashA = 1; }
            else if (code == "KeyE" || code == "KeyK") { input.chop = true; input.chopHeld = true; input.flashB = 1; }
            else if (code == "KeyQ" || code == "Tab" || code == "ShiftLeft") { input.swap = true; input.flashS = 1; }
        }

        public void KeyUp(string code)
        {
            if (MOVE_KEYS.ContainsKey(code)) input.keys[code] = false;
            if (code == "KeyE" || code == "KeyK") input.chopHeld = false;
            if (D != null && (code == "Space" || code == "ShiftLeft" || code == "KeyJ")) D.hand = false;
        }

        public void Blur()
        {
            input.keys.Clear(); input.joy.id = null; input.chopHeld = false; input.chopId = null;
        }

        // The app went to the background: pause a running service.
        public void Hidden()
        {
            if (!paused && ((G != null && G.phase == "play") || (D != null && D.phase == "play"))) pauseGame();
        }

        public double[] readMove()
        {
            var j = input.joy;
            if (j.id != null)
            {
                double dx = j.x - j.bx, dy = j.y - j.by;
                double d = U.Hypot(dx, dy);
                if (d > JOY_R) { j.bx = j.x - dx / d * JOY_R; j.by = j.y - dy / d * JOY_R; dx = j.x - j.bx; dy = j.y - j.by; }
                double m = Math.Min(1, d / JOY_R);
                if (m < 0.15) return new double[] { 0, 0 };
                return new[] { Math.Max(-1, Math.Min(1, dx / JOY_R * 1.25)), Math.Max(-1, Math.Min(1, dy / JOY_R * 1.25)) };
            }
            double mx = 0, my = 0;
            foreach (var kv in input.keys) if (kv.Value) { mx += MOVE_KEYS[kv.Key][0]; my += MOVE_KEYS[kv.Key][1]; }
            double mm = U.Hypot(mx, my);
            return mm > 0 ? new[] { mx / mm, my / mm } : new double[] { 0, 0 };
        }
    }
}
