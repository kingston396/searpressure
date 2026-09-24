using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SearPressure.UnityHost
{
    // Plays the synthesized sounds: a small pool of sources for effects, one looping source for the
    // music and one for the delivery van's engine hum. Sounds are synthesized on background threads
    // (a tune is ~400k samples) so the game never hitches; only the finished clips are made here.
    public sealed class AudioOut : MonoBehaviour
    {
        readonly Dictionary<string, AudioClip> sfx = new Dictionary<string, AudioClip>();
        readonly ConcurrentDictionary<string, float[]> sfxReady = new ConcurrentDictionary<string, float[]>();
        readonly Dictionary<string, AudioClip> tunes = new Dictionary<string, AudioClip>();       // only the current tune's two versions
        readonly Dictionary<string, Task<float[]>> tuneJobs = new Dictionary<string, Task<float[]>>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        AudioSource music, engine;
        int next;
        string musicKey; bool musicRush;
        string pendingClip; bool pendingSame;         // waiting for a tune to finish building

        void Awake()
        {
            // Unity plays nothing without a listener; the scene camera may not have one.
#if UNITY_2021_3_OR_NEWER
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
#else
            if (FindObjectOfType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
#endif
            for (int i = 0; i < 10; i++) pool.Add(NewSource(false));
            music = NewSource(true);
            engine = NewSource(true);
            engine.clip = MakeClip("engine", EngineLoop());
            engine.volume = 0;
            engine.Play();
            // Build every effect in the background now, so the first chop or fire doesn't stutter.
            Task.Run(() => { foreach (var n in Synth.Names) sfxReady[n] = Synth.Sfx(n); });
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
            if (!sfx.TryGetValue(name, out var clip))
            {
                // Built in the background already, or (rarely, right at start-up) build it now: effects are short.
                var data = sfxReady.TryRemove(name, out var d) ? d : Synth.Sfx(name);
                sfx[name] = clip = MakeClip(name, data);
            }
            var s = pool[next]; next = (next + 1) % pool.Count;
            s.clip = clip; s.volume = 1; s.Play();
        }

        static string ClipKey(string key, bool rush) => key + (rush ? ":rush" : "");

        void Build(string key, bool rush)
        {
            string ck = ClipKey(key, rush);
            if (tunes.ContainsKey(ck) || tuneJobs.ContainsKey(ck)) return;
            tuneJobs[ck] = Task.Run(() => Synth.Tune(key, rush));
        }

        public void SetMusic(string key, bool rush)
        {
            if (key == musicKey && rush == musicRush) return;
            bool sameTune = key == musicKey && key != null;
            musicKey = key; musicRush = rush;
            if (key == null) { music.Stop(); pendingClip = null; return; }
            // Keep memory small: drop other tunes' clips.
            var stale = new List<string>();
            foreach (var k in tunes.Keys) if (!k.StartsWith(key + ":") && k != key) stale.Add(k);
            foreach (var k in stale) { Destroy(tunes[k]); tunes.Remove(k); }
            Build(key, rush);
            Build(key, !rush);                 // the other version (normal/rush) is ready when it's needed
            pendingClip = ClipKey(key, rush); pendingSame = sameTune;
            if (!sameTune) music.Stop();       // a new tune: silence until it's built (a moment); the rush switch keeps playing
            TryStartPending();
        }

        void TryStartPending()
        {
            if (pendingClip == null || !tunes.TryGetValue(pendingClip, out var clip)) return;
            float at = pendingSame && music.isPlaying && music.clip != null ? (float)music.timeSamples / music.clip.samples : 0;
            music.clip = clip; music.volume = 1;
            // Speeding up for the last 30 seconds carries on from the same spot in the loop.
            music.timeSamples = Mathf.Clamp((int)(at * clip.samples), 0, clip.samples - 1);
            music.Play();
            pendingClip = null;
        }

        void Update()
        {
            if (tuneJobs.Count == 0) return;
            List<string> done = null;
            foreach (var kv in tuneJobs) if (kv.Value.IsCompleted) (done ??= new List<string>()).Add(kv.Key);
            if (done == null) return;
            foreach (var ck in done)
            {
                var job = tuneJobs[ck]; tuneJobs.Remove(ck);
                if (job.Status != TaskStatus.RanToCompletion) { Debug.LogWarning("Sear Pressure: couldn't build tune " + ck); continue; }
                // Only keep it if it still belongs to the current tune.
                if (musicKey == null || !(ck == musicKey || ck.StartsWith(musicKey + ":"))) continue;
                tunes[ck] = MakeClip("tune " + ck, job.Result);
            }
            TryStartPending();
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
