using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BepJam.EditorTools
{
    /// <summary>One-click (or batch-mode) project setup: player settings, main scene, build settings.
    /// Batch: Unity -batchmode -quit -projectPath . -executeMethod BepJam.EditorTools.ProjectSetup.Run</summary>
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/_Game/Scenes/Main.unity";

        [MenuItem("BepJam/1. Setup Project (scene + settings)")]
        public static void Run()
        {
            AssetDatabase.Refresh();
            PlayerSettings.companyName = "BepJam Studio";
            PlayerSettings.productName = "Bep Jam Viet Han";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 1168;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            QualitySettings.vSyncCount = 1;

            Directory.CreateDirectory("Assets/_Game/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 5; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.98f, 0.92f, 0.82f); cam.transform.position = new Vector3(0, 0, -10);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            new GameObject("Game", typeof(GameBoot));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[BepJam] Project setup complete → " + ScenePath);
        }

        [MenuItem("BepJam/2. Build Windows")]
        public static void BuildWindows()
        {
            Run();
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/BepJam.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("[BepJam] Build result: " + report.summary.result + " size " + report.summary.totalSize);
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        [MenuItem("BepJam/4. Build Android APK")]
        public static void BuildAndroid()
        {
            Run();
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.bepjam.viethan");
            PlayerSettings.bundleVersion = "1.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.Android.renderOutsideSafeArea = false;
            EditorUserBuildSettings.buildAppBundle = false;
            // app icon
            const string iconPath = "Assets/_Game/Icon/app_icon.png";
            AssetDatabase.ImportAsset(iconPath);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (icon != null) PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            Directory.CreateDirectory("Builds/Android");
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Android/BepJam_VietHan.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("[BepJam] Android build: " + report.summary.result + " size " + report.summary.totalSize + " errors " + report.summary.totalErrors);
        }

        [MenuItem("BepJam/3. Build WebGL")]
        public static void BuildWebGL()
        {
            Run();
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/WebGL",
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("[BepJam] WebGL build: " + report.summary.result);
        }

        /// <summary>Exports the designer's homepage.psd (flattened by Unity's importer) to a PNG for reference.</summary>
        public static void ExportRefs()
        {
            const string p = "Assets/_Game/ArtRef/homepage.psd";
            var imp = AssetImporter.GetAtPath(p) as TextureImporter;
            if (imp == null) { Debug.LogWarning("no psd"); return; }
            imp.isReadable = true; imp.textureType = TextureImporterType.Default; imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.maxTextureSize = 4096; imp.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            copy.SetPixels(tex.GetPixels()); copy.Apply();
            File.WriteAllBytes("Tools/homepage_flat.png", copy.EncodeToPNG());
            Debug.Log("[BepJam] exported homepage " + tex.width + "x" + tex.height);
        }
    }
}
