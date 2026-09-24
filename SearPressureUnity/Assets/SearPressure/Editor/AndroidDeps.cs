using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SearPressure.EditorTools
{
    // Pulls the Android libraries into the build: the Google Mobile Ads SDK (plus consent) and the Play
    // Billing Library for "Remove ads". The External Dependency Manager (EDM4U) adds them; it doesn't
    // auto-resolve in batch mode, and a build without them still succeeds and then fails on the phone.
    // So builds call Prepare() first: it turns on the custom Gradle templates Google's AdMob guide asks
    // for (as the AdMob plugin's own build step does), has the resolver write the dependencies into
    // them, and checks that both libraries are really there.
    public static class AndroidDeps
    {
        const string PluginDir = "Assets/Plugins/Android";
        static readonly string[] Templates = { "mainTemplate.gradle", "gradleTemplate.properties" };
        static readonly (string what, string spec, string aar)[] Required =
        {
            ("Google Mobile Ads SDK", "com.google.android.gms:play-services-ads:", "play-services-ads-"),
            ("Play Billing Library", "com.android.billingclient:billing:", "billing-"),
        };

        [MenuItem("Sear Pressure/Release/Resolve Android Libraries", priority = 42)]
        public static void PrepareMenu()
        {
            if (Prepare()) EditorUtility.DisplayDialog("Sear Pressure", "Google Mobile Ads and Play Billing are set up for the Android build.", "OK");
            else EditorUtility.DisplayDialog("Sear Pressure", "The Android libraries couldn't be resolved. See the Console for details.", "OK");
        }

        public static bool Prepare()
        {
            EnableGradleTemplates();
            if (!Resolve()) return false;
            return Check();
        }

        // Unity uses a custom Gradle template when its file is in Assets/Plugins/Android (the "Custom Main
        // Gradle Template" boxes in Player Settings just create these). Copy them from this Unity version.
        static void EnableGradleTemplates()
        {
            string src = FindTemplateDir();
            if (src == null) { Debug.LogWarning("Sear Pressure: Unity's Gradle templates weren't found; the resolver will download the libraries instead."); return; }
            Directory.CreateDirectory(PluginDir);
            bool added = false;
            foreach (var t in Templates)
            {
                string from = Path.Combine(src, t), to = Path.Combine(PluginDir, t);
                if (File.Exists(to) || !File.Exists(from)) continue;
                File.Copy(from, to);
                added = true;
                Debug.Log("Sear Pressure: enabled the custom Gradle template " + t + ".");
            }
            if (added) AssetDatabase.Refresh();
        }

        static string FindTemplateDir()
        {
            try
            {
                string engine = BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None);
                if (string.IsNullOrEmpty(engine) || !Directory.Exists(engine)) return null;
                string dir = Path.Combine(engine, "Tools", "GradleTemplates");
                if (File.Exists(Path.Combine(dir, "mainTemplate.gradle"))) return dir;
                string found = Directory.GetFiles(engine, "mainTemplate.gradle", SearchOption.AllDirectories).FirstOrDefault();
                return found == null ? null : Path.GetDirectoryName(found);
            }
            catch (Exception e) { Debug.LogWarning("Sear Pressure: " + e.Message); return null; }
        }

        // GooglePlayServices.PlayServicesResolver.ResolveSync(true), found by name so this compiles without
        // the resolver installed.
        static bool Resolve()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => { try { return a.GetType("GooglePlayServices.PlayServicesResolver"); } catch { return null; } })
                .FirstOrDefault(t => t != null);
            var method = type?.GetMethod("ResolveSync", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool) }, null);
            if (method == null)
            {
                Debug.LogError("Sear Pressure: the External Dependency Manager isn't loaded, so the Android libraries (AdMob, Play Billing) can't be added. Open the project once in the Unity editor so Package Manager installs it.");
                return false;
            }
            Debug.Log("Sear Pressure: resolving Android libraries…");
            bool ok;
            try { ok = (bool)method.Invoke(null, new object[] { true }); }
            catch (Exception e) { Debug.LogError("Sear Pressure: Android library resolution failed: " + (e.InnerException ?? e)); return false; }
            AssetDatabase.Refresh();
            if (!ok) Debug.LogError("Sear Pressure: Android library resolution failed (see the resolver's messages above).");
            return ok;
        }

        // With the templates on, the resolver writes the libraries into mainTemplate.gradle; without them it
        // downloads .aar files into Assets/Plugins/Android. Either way both must be there.
        static bool Check()
        {
            string main = Path.Combine(PluginDir, "mainTemplate.gradle");
            string gradle = File.Exists(main) ? File.ReadAllText(main) : "";
            string[] aars = Directory.Exists(PluginDir) ? Directory.GetFiles(PluginDir, "*.aar", SearchOption.AllDirectories).Select(Path.GetFileName).ToArray() : new string[0];
            bool ok = true;
            foreach (var (what, spec, aar) in Required)
            {
                int at = gradle.IndexOf(spec, StringComparison.Ordinal);
                if (at >= 0)
                {
                    int end = gradle.IndexOfAny(new[] { '\'', '"', '\n' }, at);
                    Debug.Log("Sear Pressure: " + what + " → " + (end > at ? gradle.Substring(at, end - at) : spec));
                }
                else if (aars.Any(f => f.StartsWith(aar, StringComparison.Ordinal)))
                    Debug.Log("Sear Pressure: " + what + " → " + aars.First(f => f.StartsWith(aar, StringComparison.Ordinal)));
                else { Debug.LogError("Sear Pressure: the " + what + " is missing from the Android build."); ok = false; }
            }
            return ok;
        }
    }
}
