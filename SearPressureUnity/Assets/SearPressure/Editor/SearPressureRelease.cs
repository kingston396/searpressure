using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.Build;
#endif

namespace SearPressure.EditorTools
{
    // Store-ready settings: the app icon (and Android's adaptive icon), the splash screen, the version
    // number, and Android release options (IL2CPP, ARM64, App Bundle, target API level).
    // Runs on every editor load but only fills in what's still at Unity's defaults, so anything you
    // change by hand in Player Settings stays changed.
    [InitializeOnLoad]
    public static class SearPressureRelease
    {
        const string Dir = "Assets/SearPressure/Branding/";
        public const string Version = "1.0.0";
        // Google Play's required target API level for new apps and updates (Android 16). Google raises
        // it every August: check Play Console's "Target API level" page before each release.
        const int TargetApi = 36;

        static SearPressureRelease()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Apply(false);
            };
        }

        [MenuItem("Sear Pressure/Apply Store Settings", priority = 2)]
        public static void ApplyMenu() => Apply(true);

        internal static void Apply(bool force)
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "Icon.png");
            if (icon == null) return;   // still importing; runs again next load
            Icons(icon, force);
            Splash(force);
            VersionAndAndroid(force);
            AssetDatabase.SaveAssets();
            if (force) Debug.Log("Sear Pressure: store settings applied (icon, splash, version " + PlayerSettings.bundleVersion + ", Android release options).");
        }

        // ---- icons ----
        static void Icons(Texture2D icon, bool force)
        {
#if UNITY_2021_2_OR_NEWER
            var cur = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (force || cur.Length == 0 || cur[0] == null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
#else
            var cur = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Unknown, IconKind.Any);
            if (force || cur.Length == 0 || cur[0] == null) PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon }, IconKind.Any);
#endif
            // Android: adaptive (background + foreground layers), round and legacy icons. These kinds
            // live in the Android module, so look them up by name: no module, nothing to set.
            var back = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "IconAdaptiveBack.png");
            var fore = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "IconAdaptiveFore.png");
            SetAndroidIcons("Adaptive", force, back != null && fore != null ? new[] { back, fore } : null);
            SetAndroidIcons("Round", force, new[] { icon });
            SetAndroidIcons("Legacy", force, new[] { icon });
        }

        static void SetAndroidIcons(string kindName, bool force, Texture2D[] textures)
        {
            if (textures == null) return;
            try
            {
                var kindType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => { try { return a.GetType("UnityEditor.Android.AndroidPlatformIconKind"); } catch { return null; } })
                    .FirstOrDefault(t => t != null);
                if (kindType == null) return;
                object k = kindType.GetProperty(kindName)?.GetValue(null) ?? kindType.GetField(kindName)?.GetValue(null);
                if (!(k is PlatformIconKind kind)) return;
#if UNITY_2021_2_OR_NEWER
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
#else
                var icons = PlayerSettings.GetPlatformIcons(BuildTargetGroup.Android, kind);
#endif
                bool changed = false;
                foreach (var pi in icons)
                {
                    var have = pi.GetTextures();
                    if (!force && have != null && have.Any(t => t != null)) continue;
                    pi.SetTextures(textures.Take(Math.Max(1, pi.maxLayerCount)).ToArray());
                    changed = true;
                }
#if UNITY_2021_2_OR_NEWER
                if (changed) PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
#else
                if (changed) PlayerSettings.SetPlatformIcons(BuildTargetGroup.Android, kind, icons);
#endif
            }
            catch (Exception e) { Debug.LogWarning("Sear Pressure: couldn't set the Android " + kindName + " icon (" + e.Message + "). Set it in Player Settings > Android > Icon."); }
        }

        // ---- splash: the Sear Pressure logo on the game's navy ----
        static void Splash(bool force)
        {
            var logo = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "SplashLogo.png");
            if (logo == null) return;
            var logos = PlayerSettings.SplashScreen.logos;
            if (!force && logos != null && logos.Any(l => l.logo != null)) return;
            PlayerSettings.SplashScreen.show = true;
            // Unity 6 lets every plan turn the "Made with Unity" logo off; older plans keep it regardless.
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(1.6f, logo) };
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly;
            PlayerSettings.SplashScreen.backgroundColor = new Color32(0x1c, 0x21, 0x33, 255);
            PlayerSettings.SplashScreen.overlayOpacity = 0;
        }

        // ---- version 1.0.0 (build 1) and Android release options ----
        static void VersionAndAndroid(bool force)
        {
            string v = PlayerSettings.bundleVersion;
            if (force || string.IsNullOrEmpty(v) || v == "0.1" || v == "1.0")
            {
                PlayerSettings.bundleVersion = Version;
                if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;
                if (string.IsNullOrEmpty(PlayerSettings.iOS.buildNumber) || PlayerSettings.iOS.buildNumber == "0") PlayerSettings.iOS.buildNumber = "1";
            }
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
#else
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
#endif
            // Google Play requires 64-bit; ARM64 covers every current phone and keeps the download small.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            if (force || (int)PlayerSettings.Android.targetSdkVersion == 0 || (int)PlayerSettings.Android.targetSdkVersion < TargetApi)
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetApi;
            // Draw under the notch/camera cutout; the game keeps its buttons inside the safe area itself.
            PlayerSettings.Android.renderOutsideSafeArea = true;
            // Play Store uploads are App Bundles (.aab).
            EditorUserBuildSettings.buildAppBundle = true;
        }

        // Bump for each store upload: Play and App Store both refuse a build number they've seen.
        [MenuItem("Sear Pressure/Release/Next Build Number", priority = 40)]
        public static void NextBuild()
        {
            PlayerSettings.Android.bundleVersionCode += 1;
            int ios = int.TryParse(PlayerSettings.iOS.buildNumber, out var n) ? n : 0;
            PlayerSettings.iOS.buildNumber = Math.Max(ios + 1, PlayerSettings.Android.bundleVersionCode).ToString();
            AssetDatabase.SaveAssets();
            Debug.Log($"Sear Pressure: version {PlayerSettings.bundleVersion}, Android build {PlayerSettings.Android.bundleVersionCode}, iOS build {PlayerSettings.iOS.buildNumber}.");
        }
    }

    // Branding images: crisp pixels, no compression, the splash logo as a sprite.
    public sealed class BrandingImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/SearPressure/Branding/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = assetPath.EndsWith("SplashLogo.png") ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (ti.textureType == TextureImporterType.Sprite) ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 2048;
        }
    }
}
