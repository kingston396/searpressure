using System;
using System.Collections.Generic;

namespace SearPressure
{
    // The web version's little WebAudio synth, rendered to PCM: every sound effect is a few
    // oscillator tones and filtered noise bursts, and each stop has a looping tune made from a seed.
    public static class Synth
    {
        public const int Rate = 44100;
        // One generator per thread: tunes are built on a background thread while effects play.
        [ThreadStatic] static Random rndT;
        static Random rnd => rndT ??= new Random(7);

        // tone(freq, dur, type, vol, when, slide): an oscillator with a quick attack and exponential fade.
        static void Tone(float[] buf, double freq, double dur, string type = "sine", double vol = 0.12, double when = 0, double slide = 0)
        {
            int start = (int)(when * Rate), n = (int)((dur + 0.05) * Rate);
            double phase = 0, f1 = Math.Max(40, freq + slide);
            for (int i = 0; i < n && start + i < buf.Length; i++)
            {
                double t = (double)i / Rate;
                double f = slide != 0 ? freq * Math.Pow(f1 / freq, Math.Min(1, t / dur)) : freq;
                phase += f / Rate; phase -= Math.Floor(phase);
                double s = type switch
                {
                    "square" => phase < 0.5 ? 1 : -1,
                    "sawtooth" => 2 * phase - 1,
                    "triangle" => phase < 0.5 ? 4 * phase - 1 : 3 - 4 * phase,
                    _ => Math.Sin(2 * Math.PI * phase),
                };
                // Exponential ramps: 0.0001 -> vol in 10 ms, then -> 0.0001 at dur.
                double g = t < 0.01 ? 0.0001 * Math.Pow(vol / 0.0001, t / 0.01)
                    : t < dur ? vol * Math.Pow(0.0001 / vol, (t - 0.01) / Math.Max(1e-4, dur - 0.01)) : 0;
                buf[start + i] += (float)(s * g);
            }
        }

        // noise(dur, vol, freq): white noise fading out through a band-pass filter.
        static void Noise(float[] buf, double dur, double vol, double freq = 1500, double when = 0)
        {
            int start = (int)(when * Rate), n = (int)(dur * Rate);
            // RBJ band-pass (constant 0 dB peak gain), Q = 1 like WebAudio's default.
            double w0 = 2 * Math.PI * freq / Rate, alpha = Math.Sin(w0) / 2;
            double b0 = alpha, b2 = -alpha, a0 = 1 + alpha, a1 = -2 * Math.Cos(w0), a2 = 1 - alpha;
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < n && start + i < buf.Length; i++)
            {
                double x = (rnd.NextDouble() * 2 - 1) * (1 - (double)i / n);
                double y = (b0 * x + b2 * x2 - a1 * y1 - a2 * y2) / a0;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                buf[start + i] += (float)(y * vol);
            }
        }

        // Longest sound first, so buffers are sized right.
        static readonly Dictionary<string, double> LEN = new Dictionary<string, double>
        {
            ["pick"] = 0.15, ["drop"] = 0.15, ["chop"] = 0.06, ["chopped"] = 0.25, ["ding"] = 0.72, ["serve"] = 0.5, ["fail"] = 0.65, ["nope"] = 0.18,
            ["trash"] = 0.2, ["warn"] = 0.12, ["burn"] = 0.62, ["swap"] = 0.18, ["order"] = 0.26, ["tick"] = 0.1, ["fire"] = 0.72, ["crackle"] = 0.07,
            ["spray"] = 0.13, ["out"] = 0.42, ["splash"] = 0.11, ["clean"] = 0.18, ["brew"] = 0.31, ["strike"] = 0.62, ["hop"] = 0.17, ["blend"] = 0.95,
            ["go"] = 0.5, ["end"] = 0.72,
        };
        public static IEnumerable<string> Names => LEN.Keys;

        public static float[] Sfx(string name)
        {
            var b = new float[(int)((LEN.TryGetValue(name, out var l) ? l : 0.3) * Rate)];
            switch (name)
            {
                case "pick": Tone(b, 620, 0.09, "triangle", 0.12, 0, 260); break;
                case "drop": Tone(b, 420, 0.09, "triangle", 0.12, 0, -140); break;
                case "chop": Noise(b, 0.05, 0.5, 2600); break;
                case "chopped": Tone(b, 880, 0.08, "triangle", 0.1); Tone(b, 1175, 0.12, "triangle", 0.1, 0.07); break;
                case "ding": Tone(b, 1568, 0.5, "sine", 0.1); Tone(b, 2093, 0.6, "sine", 0.06, 0.06); break;
                case "serve": { var fs = new[] { 523, 659, 784, 1047 }; for (int i = 0; i < 4; i++) Tone(b, fs[i], 0.2, "triangle", 0.11, i * 0.07); break; }
                case "fail": Tone(b, 220, 0.3, "sawtooth", 0.07, 0, -90); Tone(b, 165, 0.4, "sawtooth", 0.06, 0.18, -60); break;
                case "nope": Tone(b, 190, 0.12, "square", 0.05); break;
                case "trash": Noise(b, 0.18, 0.4, 500); break;
                case "warn": Tone(b, 1320, 0.06, "square", 0.04); break;
                case "burn": Noise(b, 0.6, 0.5, 300); break;
                case "swap": Tone(b, 520, 0.12, "sine", 0.1, 0, 380); break;
                case "order": Tone(b, 988, 0.1, "sine", 0.08); Tone(b, 1319, 0.14, "sine", 0.07, 0.09); break;
                case "tick": Tone(b, 1000, 0.05, "square", 0.04); break;
                case "fire": Noise(b, 0.7, 0.6, 250); Tone(b, 140, 0.5, "sawtooth", 0.05, 0, -60); break;
                case "crackle": Noise(b, 0.06, 0.25, 900); break;
                case "spray": Noise(b, 0.12, 0.18, 4000); break;
                case "out": Noise(b, 0.4, 0.3, 3000); Tone(b, 700, 0.2, "sine", 0.06, 0.1, 300); break;
                case "splash": Noise(b, 0.1, 0.25, 1200); break;
                case "clean": Tone(b, 1480, 0.12, "sine", 0.07, 0, 400); break;
                case "brew": Tone(b, 300, 0.25, "triangle", 0.06, 0, 120); break;
                case "strike": Tone(b, 110, 0.55, "sawtooth", 0.12, 0, -30); Tone(b, 98, 0.55, "square", 0.06); Noise(b, 0.3, 0.3, 200); break;
                case "hop": Tone(b, 900, 0.05, "square", 0.04); Tone(b, 1200, 0.05, "square", 0.03, 0.06); break;
                case "blend": Tone(b, 180, 0.9, "sawtooth", 0.04, 0, 220); Noise(b, 0.8, 0.12, 700); break;
                case "go": Tone(b, 784, 0.14, "triangle", 0.12); Tone(b, 1175, 0.3, "triangle", 0.12, 0.14); break;
                case "end": { var fs = new[] { 784, 659, 523, 392 }; for (int i = 0; i < 4; i++) Tone(b, fs[i], 0.25, "triangle", 0.1, i * 0.12); break; }
            }
            return b;
        }

        static double Midi(double m) => 440 * Math.Pow(2, (m - 69) / 12);

        // A little looping tune per stop ("0".."11"), the menus ("menu") or driving ("drive").
        // Rendered as one seamless loop; `rush` is the faster version for the last 30 seconds.
        public static float[] Tune(string key, bool rush)
        {
            var r = Rng.Mulberry32(Rng.SeedOf("tune:" + key));
            bool minor = key == "10" || key == "drive";
            var scale = minor ? new[] { 0, 3, 5, 7, 10 } : new[] { 0, 2, 4, 7, 9 };
            int root = 50 + (int)Math.Floor(r() * 6);
            int.TryParse(key, out int stop);
            double bpm = key == "menu" ? 92 : key == "drive" ? 140 : 104 + stop * 2;
            var chords = minor ? new[] { 0, -4, -2, -5 } : new[] { 0, 7, 9, 5 };
            var lead = new int?[32];
            int deg = 2;
            for (int i = 0; i < 32; i++)
            {
                if (i % 8 == 7 || r() < 0.3) { lead[i] = null; continue; }
                deg = Math.Max(0, Math.Min(9, deg + (int)Math.Floor(r() * 5) - 2));
                lead[i] = root + 12 + scale[deg % 5] + 12 * (deg / 5);
            }
            double v = key == "menu" ? 0.6 : 1;
            double step = 60 / (bpm * (rush ? 1.18 : 1)) / 2;
            int len = (int)(step * 32 * Rate);
            var loop = new float[len];
            var tmp = new float[len * 2];
            for (int i = 0; i < 32; i++)
            {
                double when = i * step;
                int chord = root - 12 + chords[(i / 8) % 4];
                if (i % 2 == 0) Tone(tmp, Midi(chord + (i % 4 == 2 ? 7 : 0)), step * 1.6, "triangle", 0.05 * v, when);
                if (lead[i] != null) Tone(tmp, Midi(lead[i].Value), step * 0.9, "square", 0.018 * v, when);
            }
            // Fold the tail past the loop end back onto the start so it loops without a click.
            for (int i = 0; i < tmp.Length; i++) loop[i % len] += tmp[i];
            return loop;
        }
    }
}
