using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SearPressure.EditorTools
{
    // First-open setup: makes the main scene, puts it in Build Settings, and sets the player
    // settings the game expects. Also adds a "Sear Pressure" menu with tester tools.
    [InitializeOnLoad]
    public static class SearPressureSetup
    {
        const string ScenePath = "Assets/SearPressure/Scenes/Main.unity";
        const string DoneKey = "SearPressure.SetupDone.v1";
        public const string AppId = "com.kingstongames.searpressure";

        static SearPressureSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!File.Exists(ScenePath) || !SessionState.GetBool(DoneKey, false)) Setup(false);
            };
        }

        [MenuItem("Sear Pressure/Set Up Project", priority = 0)]
        public static void SetupMenu() => Setup(true);

        internal static void Setup(bool verbose)
        {
            SessionState.SetBool(DoneKey, true);
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color32(0x1c, 0x21, 0x33, 255);
                cam.cullingMask = 0;
                camGo.AddComponent<AudioListener>();
                new GameObject("Sear Pressure").AddComponent<SearPressure.UnityHost.GameHost>();
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("Sear Pressure: created " + ScenePath);
            }
            var scenes = EditorBuildSettings.scenes;
            bool listed = false;
            foreach (var s in scenes) if (s.path == ScenePath) listed = true;
            if (!listed)
            {
                var list = new System.Collections.Generic.List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
                list.AddRange(scenes);
                EditorBuildSettings.scenes = list.ToArray();
            }

            PlayerSettings.productName = "Sear Pressure";
            if (PlayerSettings.companyName == "DefaultCompany") PlayerSettings.companyName = "Kingston Games";
            // Pixel art in the web version's exact colours: gamma space, no smoothing.
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SplashScreen.backgroundColor = new Color32(0x1c, 0x21, 0x33, 255);
            // The store package name. It can never change once the app is on Google Play.
            string id = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            if (string.IsNullOrEmpty(id) || id == "com.Company.ProductName" || id.StartsWith("com.DefaultCompany.") || id == "com.searpressure.game")
            {
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, AppId);
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, AppId);
            }
            if (PlayerSettings.companyName == "Sear Pressure") PlayerSettings.companyName = "Kingston Games";
            EnsureLegacyInput();
            AssetDatabase.SaveAssets();
            if (verbose) EditorUtility.DisplayDialog("Sear Pressure", "Project set up. Open Assets/SearPressure/Scenes/Main.unity and press Play.", "OK");
        }

        // The game reads touches and keys through the classic Input Manager. If only the new Input
        // System is active, switch to "Both" (Unity asks to restart).
        static void EnsureLegacyInput()
        {
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (ps == null || ps.Length == 0) return;
            var so = new SerializedObject(ps[0]);
            var p = so.FindProperty("activeInputHandler");
            if (p != null && p.intValue == 1)
            {
                p.intValue = 2;
                so.ApplyModifiedProperties();
                if (!Application.isBatchMode) EditorUtility.DisplayDialog("Sear Pressure", "Active Input Handling was set to \"Both\" so the game can read touches and keys. Unity needs a restart for this to take effect.", "OK");
            }
        }

        [MenuItem("Sear Pressure/Open Main Scene", priority = 1)]
        public static void OpenScene() { if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath); }

        [MenuItem("Sear Pressure/Testing/Unlock Every Kitchen", priority = 20)]
        public static void ToggleUnlock()
        {
            bool on = PlayerPrefs.GetInt("SearPressure.UnlockAll", 0) == 1;
            PlayerPrefs.SetInt("SearPressure.UnlockAll", on ? 0 : 1); PlayerPrefs.Save();
        }
        [MenuItem("Sear Pressure/Testing/Unlock Every Kitchen", true)]
        public static bool ToggleUnlockCheck() { Menu.SetChecked("Sear Pressure/Testing/Unlock Every Kitchen", PlayerPrefs.GetInt("SearPressure.UnlockAll", 0) == 1); return true; }

        [MenuItem("Sear Pressure/Testing/Add 5,000 Coins", priority = 21)]
        public static void AddCoins()
        {
            var save = SaveData.FromJson(PlayerPrefs.GetString(SaveData.Key, ""));
            save.wallet += 5000;
            PlayerPrefs.SetString(SaveData.Key, save.ToJson()); PlayerPrefs.Save();
            Debug.Log("Sear Pressure: wallet is now " + save.wallet + " (restart Play mode to see it).");
        }

        [MenuItem("Sear Pressure/Testing/Reset Save", priority = 22)]
        public static void ResetSave()
        {
            if (!EditorUtility.DisplayDialog("Sear Pressure", "Delete the saved game (stars, coins, shop items)?", "Delete", "Cancel")) return;
            PlayerPrefs.DeleteKey(SaveData.Key); PlayerPrefs.Save();
        }
    }
}
