using System.Collections.Generic;
using UnityEngine;

namespace SearPressure.UnityHost
{
    // Plays the synthesized sounds: a small pool of sources for effects, one looping source
    // for the music and one for the delivery van's engine hum.
    public sealed class AudioOut : MonoBehaviour
    {
        readonly Dictionary<string, AudioClip> sfx = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, AudioClip> tunes = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        AudioSource music, engine;
        int next;
        string musicKey; bool musicRush;

        void Awake()
        {
            for (int i = 0; i < 10; i++) pool.Add(NewSource(false));
            music = NewSource(true);
            engine = NewSource(true);
            engine.clip = MakeClip("engine", EngineLoop());
            engine.volume = 0;
            engine.Play();
        }

        AudioSource NewSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = loop; s.spatialBlend = 0;
            return s;
        }

        static AudioClip MakeClip(string name, float[] data)
        {
            var c = AudioClip.Create(name, Mathf.Max(1, data.Length), 1, Synth.Rate, false);
            c.SetData(data, 0);
            return c;
        }

        public void Play(string name)
        {
            if (!sfx.TryGetValue(name, out var clip)) sfx[name] = clip = MakeClip(name, Synth.Sfx(name));
            var s = pool[next]; next = (next + 1) % pool.Count;
            s.clip = clip; s.volume = 1; s.Play();
        }

        public void SetMusic(string key, bool rush)
        {
            if (key == musicKey && rush == musicRush) return;
            float at = music.isPlaying && music.clip != null ? (float)music.timeSamples / music.clip.samples : 0;
            bool sameTune = key == musicKey && key != null;
            musicKey = key; musicRush = rush;
            if (key == null) { music.Stop(); return; }
            string ck = key + (rush ? ":rush" : "");
            if (!tunes.TryGetValue(ck, out var clip)) tunes[ck] = clip = MakeClip("tune " + ck, Synth.Tune(key, rush));
            music.clip = clip; music.volume = 1;
            // Speeding up for the last 30 seconds carries on from the same spot in the loop.
            music.timeSamples = sameTune ? Mathf.Clamp((int)(at * clip.samples), 0, clip.samples - 1) : 0;
            music.Play();
        }

        // The van's hum, like the web version: a 55 Hz sawtooth through a low-pass filter,
        // pitched up to 145 Hz and a little louder as the van speeds up.
        public void SetEngine(double level)
        {
            float k = Mathf.Clamp01((float)level);
            engine.volume = level > 0 ? (0.025f + k * 0.025f) * 8f : 0;
            engine.pitch = (55f + k * 90f) / 55f;
        }

        static float[] EngineLoop()
        {
            int n = Synth.Rate;          // one second: exactly 55 cycles, so it loops cleanly
            var b = new float[n];
            double lp = 0, a = 1 - System.Math.Exp(-2 * System.Math.PI * 500 / Synth.Rate);
            for (int pass = 0; pass < 2; pass++)          // run twice so the filter has settled at the loop point
                for (int i = 0; i < n; i++)
                {
                    double ph = (i * 55.0 / Synth.Rate) % 1;
                    lp += (2 * ph - 1 - lp) * a;
                    if (pass == 1) b[i] = (float)(lp * 0.5);
                }
            return b;
        }
    }
}
