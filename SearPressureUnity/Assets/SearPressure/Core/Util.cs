using System;
using System.Collections.Generic;
using System.Globalization;

namespace SearPressure
{
    public delegate double RandFn();

    // Seeded random numbers, bit-for-bit the same as the web version's mulberry32 and seedOf,
    // so the daily challenge and seeded services match between the two.
    public static class Rng
    {
        static readonly Random sys = new Random();
        public static double Random() => sys.NextDouble();

        public static RandFn Mulberry32(double seed)
        {
            int a = unchecked((int)(uint)(long)seed);
            return () =>
            {
                unchecked
                {
                    a = a + 0x6D2B79F5;
                    int t = (a ^ (int)((uint)a >> 15)) * (1 | a);
                    t = (t + (t ^ (int)((uint)t >> 7)) * (61 | t)) ^ t;
                    return (uint)(t ^ (int)((uint)t >> 14)) / 4294967296.0;
                }
            };
        }

        public static uint SeedOf(string str)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char ch in str) h = (h ^ ch) * 16777619;
                return h;
            }
        }
    }

    // CSS-style colours ("#rrggbb", "#rgb", "rgba(r,g,b,a)", "rgb(r,g,b)") parsed once and cached.
    public struct Rgba
    {
        public byte r, g, b, a;
        public Rgba(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static readonly Rgba White = new Rgba(255, 255, 255, 255);
        public static readonly Rgba Clear = new Rgba(0, 0, 0, 0);
    }

    public static class Css
    {
        static readonly Dictionary<string, Rgba> cache = new Dictionary<string, Rgba>();

        public static Rgba Parse(string s)
        {
            if (s == null) return Rgba.Clear;
            if (cache.TryGetValue(s, out var c)) return c;
            c = ParseRaw(s.Trim());
            if (cache.Count > 4000) cache.Clear();
            cache[s] = c;
            return c;
        }

        static Rgba ParseRaw(string s)
        {
            try
            {
                if (s.StartsWith("#"))
                {
                    string h = s.Substring(1);
                    if (h.Length == 3) h = "" + h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
                    int n = Convert.ToInt32(h.Substring(0, 6), 16);
                    byte a = h.Length >= 8 ? (byte)Convert.ToInt32(h.Substring(6, 2), 16) : (byte)255;
                    return new Rgba((byte)(n >> 16), (byte)((n >> 8) & 255), (byte)(n & 255), a);
                }
                if (s.StartsWith("rgb"))
                {
                    int o = s.IndexOf('('), e = s.LastIndexOf(')');
                    var parts = s.Substring(o + 1, e - o - 1).Split(',');
                    double P(int i) => double.Parse(parts[i].Trim(), CultureInfo.InvariantCulture);
                    double al = parts.Length > 3 ? P(3) : 1;
                    return new Rgba((byte)Math.Round(P(0)), (byte)Math.Round(P(1)), (byte)Math.Round(P(2)), (byte)Math.Round(Math.Max(0, Math.Min(1, al)) * 255));
                }
                if (s == "transparent") return Rgba.Clear;
                if (s == "white") return Rgba.White;
                if (s == "black") return new Rgba(0, 0, 0, 255);
            }
            catch (Exception) { }
            return new Rgba(255, 0, 255, 255);
        }

        public static int[] Hex(string c) { var p = Parse(c); return new[] { (int)p.r, p.g, p.b }; }

        // Blend two "#rrggbb" colours, like the web version's mix().
        public static string Mix(string a, string b, double t)
        {
            var A = Parse(a); var B = Parse(b);
            int M(byte x, byte y) => (int)Math.Round(x + (y - x) * t);
            return "#" + M(A.r, B.r).ToString("x2") + M(A.g, B.g).ToString("x2") + M(A.b, B.b).ToString("x2");
        }
    }

    public static class U
    {
        public static double Hypot(double x, double y) => Math.Sqrt(x * x + y * y);
        public static double Clamp(double v, double a, double b) => v < a ? a : v > b ? b : v;
        public static double Floor(double v) => Math.Floor(v);
        // JS Math.round: halves round up.
        public static double Round(double v) => Math.Floor(v + 0.5);
        public static string FmtTime(double s) { s = Math.Max(0, Math.Ceiling(s)); return Math.Floor(s / 60) + ":" + ((int)(s % 60)).ToString("00"); }
        public static string FmtCoins(double n) => ((long)Math.Round(n)).ToString("#,0", CultureInfo.InvariantCulture);
        public static string Pad2(int n) => n.ToString("00");
        // JS-style % (sign follows the dividend), for doubles.
        public static double Mod(double a, double b) => a % b;
        public static string S(double d) => Json.Num(d);
    }
}
