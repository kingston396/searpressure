using System;
using System.IO;
using SearPressure;

// Store art drawn with the game's own sprites and fonts: the app icon (and Android's adaptive
// layers), the splash logo and Google Play's feature graphic.
namespace SearPressure
{
    public sealed partial class Game
    {
        const string SKY_TOP = "#ffcf6b", SKY_BOT = "#f07a3a", ROAD = "#3a3d48", DASH = "#f5d33e";

        // Warm sky in bands, then the road with its centre line. `roadY` is where the tarmac starts.
        void artSky(double w, double h, double roadY, double band)
        {
            int n = (int)Math.Ceiling(roadY / band);
            for (int i = 0; i < n; i++) { X.fillStyle = Css.Mix(SKY_TOP, SKY_BOT, n > 1 ? i / (double)(n - 1) : 0); X.fillRect(0, i * band, w, band + 1); }
            X.fillStyle = ROAD; X.fillRect(0, roadY, w, h - roadY);
            X.fillStyle = "#2b2e38"; X.fillRect(0, roadY, w, band * 0.5);
            double dy = roadY + (h - roadY) * 0.55, dw = band * 2.2;
            X.fillStyle = DASH; for (double x = band * 0.6; x < w; x += dw * 2) X.fillRect(x, dy, dw, band * 0.45);
        }
        void artSun(double x, double y, double s) { X.fillStyle = "#fff1a8"; X.fillRect(x - s * 0.6, y - s * 0.6, s * 1.2, s * 1.2); X.fillStyle = "#fff8d0"; X.fillRect(x - s * 0.4, y - s * 0.4, s * 0.8, s * 0.8); }

        // The truck (32x16 sprite; its wheels touch row 15) with flames licking off the grill hatch.
        void artTruck(double cx, double wheelY, double px, bool flames)
        {
            double left = cx - 16 * px, top = wheelY - 15 * px;
            if (flames)
            {
                spr("fire1", left + 8 * px, top - 5 * px, px * 0.9);
                spr("fire2", left + 15 * px, top - 6 * px, px);
            }
            sprTL("truck", left, top, px);
        }

        // The icon: a chef holding a flaming pan on a red-check tablecloth. `layer` is "full", or
        // Android's adaptive "bg"/"fg" (launchers crop those to the middle 66%, so the chef is smaller).
        public void ArtIcon(int size, string layer)
        {
            X.Reset();
            double s = size;
            if (layer != "fg")
            {
                X.fillStyle = TOMATO; X.fillRect(0, 0, s, s);
                X.fillStyle = "#d63d27";
                for (int i = 0; i < 16; i++) for (int j = 0; j < 16; j++) if ((i + j) % 2 == 0) X.fillRect(i * s / 16, j * s / 16, s / 16, s / 16);
            }
            if (layer == "bg") return;
            double k = layer == "full" ? 1 : 0.6, px = s / 19 * k, c = s / 2;
            spr("chefDown", c - s * 0.14 * k, c + s * 0.04 * k, px);
            spr("pan", c + s * 0.23 * k, c + s * 0.12 * k, px * 0.85);
            spr("fire2", c + s * 0.23 * k, c - s * 0.1 * k, px * 0.85);
        }

        // Splash logo: the wordmark over the truck, on a transparent background.
        public void ArtLogo(int w, int h)
        {
            X.Reset();
            double big = h * 0.24;
            X.font = "700 " + U.S(big) + "px \"Pixelify Sans\""; X.textAlign = "center"; X.textBaseline = "middle";
            X.fillStyle = TOMATO; X.fillText("Sear", w / 2, h * 0.14);
            X.fillStyle = PAPER; X.fillText("Pressure", w / 2, h * 0.36);
            artTruck(w / 2, h * 0.95, h / 30, false);
        }

        // Google Play feature graphic, 1024x500.
        public void ArtFeature(int w, int h)
        {
            X.Reset();
            double band = h / 20, road = h * 0.72;
            artSky(w, h, road, band);
            artSun(w * 0.88, h * 0.2, h * 0.08);
            double tp = h / 34;
            artTruck(w * 0.775, road + h * 0.05, tp, false);
            // A short line waiting at the hatch.
            double pp = h / 64;
            spr("person", w * 0.585, road + h * 0.05 - 8 * pp, pp, S("1", "#3f9b45", "B", "#5a3a22"));
            spr("person", w * 0.535, road + h * 0.06 - 8 * pp, pp, S("1", "#3b6fd1", "B", "#f5d33e"));
            spr("person", w * 0.485, road + h * 0.05 - 8 * pp, pp, S("1", "#8e4fc9", "B", "#2b2230"));
            double big = h * 0.2;
            X.font = "700 " + U.S(big) + "px \"Pixelify Sans\""; X.textAlign = "left"; X.textBaseline = "middle";
            X.fillStyle = "rgba(28,33,51,0.35)"; X.fillText("Sear", w * 0.055 + 6, h * 0.2 + 6); X.fillText("Pressure", w * 0.055 + 6, h * 0.42 + 6);
            X.fillStyle = TOMATO_DK; X.fillText("Sear", w * 0.055, h * 0.2);
            X.fillStyle = INK; X.fillText("Pressure", w * 0.055, h * 0.42);
        }
    }
}

static partial class Tests
{
    static void Save(Game g, int w, int h, string name, string bg)
    {
        var bmp = SearPressure.Harness.SkiaRaster.Render(g.X, Program.Fonts, w, h, bg);
        SearPressure.Harness.SkiaRaster.Save(bmp, Path.Combine(Program.Out, name));
    }

    static void StoreArt()
    {
        var g = Program.NewGame(new HarnessPlatform()); Program.Run(g, 0.1);
        g.ArtIcon(1024, "full"); Save(g, 1024, 1024, "icon_1024.png", "#000000");
        g.ArtIcon(512, "full"); Save(g, 512, 512, "icon_512.png", "#000000");
        g.ArtIcon(432, "bg"); Save(g, 432, 432, "icon_adaptive_bg.png", "#000000");
        g.ArtIcon(432, "fg"); Save(g, 432, 432, "icon_adaptive_fg.png", "rgba(0,0,0,0)");
        g.ArtLogo(1200, 600); Save(g, 1200, 600, "splash_logo.png", "rgba(0,0,0,0)");
        g.ArtFeature(1024, 500); Save(g, 1024, 500, "feature_graphic.png", "#000000");
        Console.WriteLine("store art written to " + Program.Out);
    }
}
