using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SearPressure;
using SearPressure.Harness;

// Headless runs of the game for checking the port: screenshots and scripted scenarios.
sealed class HarnessPlatform : IPlatform
{
    public string saveJson;
    public List<string> sounds = new List<string>();
    public DateTime now = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
    public bool unlock, calm = true;
    public string LoadSave() => saveJson;
    public void WriteSave(string json) { saveJson = json; }
    public void Vibrate(int ms) { }
    public void PlaySound(string name) { sounds.Add(name); }
    public void SetMusic(string key, bool rush) { }
    public DateTime UtcNow => now;
    public bool UnlockAll => unlock;
    public bool Calm => calm;
    public INetTransport CreateTransport() => null;
    public void OpenKeyboard(string text, int maxLength) { }
}

static class Program
{
    public static string Root, Out;
    public static SkiaFonts Fonts;

    public static Game NewGame(HarnessPlatform p, double w = 390, double h = 844, double dpr = 2)
    {
        Sprites.Reset();
        var cv = new Canvas { Fonts = Fonts };
        var g = new Game(p, cv);
        g.Resize(w, h, dpr, 0, 0, 0, 0);
        return g;
    }
    public static void Shot(Game g, string name)
    {
        int w = (int)Math.Round(g.W * g.DPR), h = (int)Math.Round(g.H * g.DPR);
        var bmp = SkiaRaster.Render(g.X, Fonts, w, h);
        SkiaRaster.Save(bmp, Path.Combine(Out, name));
    }
    public static void Run(Game g, double seconds, double dt = 1 / 60.0) { for (double t = 0; t < seconds; t += dt) g.Frame(dt); }

    static void Main(string[] args)
    {
        Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Assets/SearPressure/Resources/SearPressure"));
        Out = args.Length > 1 ? args[1] : "/tmp/sp-shots";
        Directory.CreateDirectory(Out);
        Data.Load(File.ReadAllText(Path.Combine(Root, "Data/game.json")), File.ReadAllText(Path.Combine(Root, "Data/sprites.json")));
        Fonts = new SkiaFonts(Path.Combine(Root, "Fonts"));
        string which = args.Length > 0 ? args[0] : "smoke";
        Tests.Run(which);
    }
}
