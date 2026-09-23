using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SearPressure.EditorTools
{
    // Command-line Android build, used by .github/workflows/android.yml (GameCI unity-builder v4):
    //   Unity -batchmode -projectPath SearPressureUnity -executeMethod SearPressure.EditorTools.CiBuild.Android
    //         -customBuildPath <out.aab|out.apk> [-androidVersionCode N]
    //         [-androidKeystoreName file -androidKeystorePass p -androidKeyaliasName a -androidKeyaliasPass p]
    //         [-spAlsoApk true]
    // A fresh checkout has no ProjectSettings.asset, so this first applies the same setup the editor does on
    // first open (scene, package name, icon, splash, version, IL2CPP/ARM64/target API), then builds.
    public static class CiBuild
    {
        public static void Android()
        {
            int code = 1;
            try { code = Run() ? 0 : 1; }
            catch (Exception e) { Debug.LogError("Sear Pressure build failed: " + e); }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static bool Run()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            SearPressureSetup.Setup(false);
            AssetDatabase.Refresh();
            SearPressureRelease.Apply(true);

            if (int.TryParse(Arg("androidVersionCode"), out int vc) && vc > 0) PlayerSettings.Android.bundleVersionCode = vc;
            Debug.Log($"Sear Pressure: {PlayerSettings.applicationIdentifier} version {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode}), target API {(int)PlayerSettings.Android.targetSdkVersion}");

            // Signing: the workflow decodes the upload keystore into the project folder. Without one the
            // build is signed with Unity's debug key: fine for testing on a phone, refused by Google Play.
            string ks = Arg("androidKeystoreName");
            if (!string.IsNullOrEmpty(ks) && File.Exists(ks))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = Path.GetFullPath(ks);
                PlayerSettings.Android.keystorePass = Arg("androidKeystorePass");
                PlayerSettings.Android.keyaliasName = Arg("androidKeyaliasName");
                PlayerSettings.Android.keyaliasPass = Arg("androidKeyaliasPass");
                Debug.Log("Sear Pressure: signing with the upload key (alias " + PlayerSettings.Android.keyaliasName + ").");
            }
            else
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.LogWarning("Sear Pressure: no upload keystore; signing with the debug key (not accepted by Google Play).");
            }

            string path = Arg("customBuildPath");
            if (string.IsNullOrEmpty(path)) path = Path.GetFullPath("../build/Android/SearPressure.aab");
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            bool ok = Build(path, path.EndsWith(".aab", StringComparison.OrdinalIgnoreCase));
            // A phone-installable .apk next to the bundle, for testing without the Play Store.
            if (ok && Arg("spAlsoApk") == "true" && path.EndsWith(".aab", StringComparison.OrdinalIgnoreCase))
                ok = Build(Path.ChangeExtension(path, ".apk"), false);
            return ok;
        }

        static bool Build(string path, bool bundle)
        {
            EditorUserBuildSettings.buildAppBundle = bundle;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/SearPressure/Scenes/Main.unity" },
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"Sear Pressure: {(bundle ? "AAB" : "APK")} {s.result}, {s.totalSize / (1024 * 1024.0):0.0} MB, {s.totalErrors} errors, {s.totalTime:mm\\:ss} → {path}");
            return s.result == BuildResult.Succeeded;
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-" + name) return args[i + 1].StartsWith("-") ? "" : args[i + 1];
            return "";
        }
    }
}
