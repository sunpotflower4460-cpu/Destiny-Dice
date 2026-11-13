using System.IO;
using DestinyGarden.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DestinyGarden.EditorTools
{
    /// <summary>Batchmode entry points for G1 project configuration and builds.</summary>
    public static class BuildScripts
    {
        private const string ScenePath = "Assets/Scenes/GardenScene.unity";

        // NOTE: bundle identifier is a development placeholder (要確定 before any store submission).
        private const string DevBundleId = "com.example.destinygarden.dev";

        public static void ConfigureProject()
        {
            PlayerSettings.companyName = "Destiny Garden";
            PlayerSettings.productName = "Destiny Garden";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, DevBundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, DevBundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, DevBundleId);
            PlayerSettings.iOS.targetOSVersionString = "15.0";

            // Code-created materials use these shaders via Shader.Find; keep them from being stripped.
            var serialized = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var alwaysIncluded = serialized.FindProperty("m_AlwaysIncludedShaders");
            foreach (string shaderName in new[]
            {
                "Universal Render Pipeline/Lit", "DestinyGarden/Foliage", "DestinyGarden/Glow",
                "DestinyGarden/Glass", "DestinyGarden/Lagoon"
            })
            {
                var shader = Shader.Find(shaderName);
                if (shader == null) { Debug.LogWarning("DestinyGarden: shader not found " + shaderName); continue; }
                bool found = false;
                for (int i = 0; i < alwaysIncluded.arraySize; i++)
                    if (alwaysIncluded.GetArrayElementAtIndex(i).objectReferenceValue == shader) found = true;
                if (!found)
                {
                    alwaysIncluded.arraySize++;
                    alwaysIncluded.GetArrayElementAtIndex(alwaysIncluded.arraySize - 1).objectReferenceValue = shader;
                }
            }
            serialized.ApplyModifiedProperties();

            // The template PC renderer enables SSAO, whose resources can be stripped
            // in players and break the post-processing pass. This game does not use SSAO.
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets/Settings" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var rendererData = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(path);
                if (rendererData == null) continue;
                foreach (var feature in rendererData.rendererFeatures)
                {
                    if (feature != null && feature.GetType().Name.Contains("AmbientOcclusion"))
                    {
                        feature.SetActive(false);
                        EditorUtility.SetDirty(feature);
                    }
                }
                EditorUtility.SetDirty(rendererData);
            }

            foreach (string assetPath in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                if (assets.Length == 0) continue;
                var so = new SerializedObject(assets[0]);
                void SetInt(string prop, int value)
                {
                    var pr = so.FindProperty(prop);
                    if (pr != null) pr.intValue = value;
                }
                void SetFloat(string prop, float value)
                {
                    var pr = so.FindProperty(prop);
                    if (pr != null) pr.floatValue = value;
                }
                bool pc = assetPath.Contains("PC_");
                SetInt("m_ShadowAtlasResolution", pc ? 2048 : 1024);
                SetInt("m_ShadowCascadeCount", pc ? 2 : 1);
                SetFloat("m_ShadowDistance", 32f);
                so.ApplyModifiedProperties();
            }

            EnsureGardenScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("DestinyGarden: project configured (landscape, dev bundle id).");
        }

        private static void EnsureGardenScene()
        {
            if (File.Exists(ScenePath)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("GardenBootstrap");
            root.AddComponent<GardenBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        public static void BuildMac()
        {
            ConfigureProject();
            string outDir = "Builds/macOS";
            Directory.CreateDirectory(outDir);
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath },
                Path.Combine(outDir, "DestinyGarden.app"), BuildTarget.StandaloneOSX, BuildOptions.None);
            Debug.Log("DestinyGarden macOS build: " + report.summary.result);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        public static void BuildAndroid()
        {
            ConfigureProject();
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            string outDir = "Builds/Android";
            Directory.CreateDirectory(outDir);
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath },
                Path.Combine(outDir, "DestinyGarden.apk"), BuildTarget.Android, BuildOptions.None);
            Debug.Log("DestinyGarden Android build: " + report.summary.result);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        public static void BuildWindows()
        {
            ConfigureProject();
            string outDir = "Builds/Windows";
            Directory.CreateDirectory(outDir);
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath },
                Path.Combine(outDir, "DestinyGarden.exe"), BuildTarget.StandaloneWindows64, BuildOptions.None);
            Debug.Log("DestinyGarden Windows build: " + report.summary.result);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        /// <summary>
        /// Browser build for itch.io. itch.io does not send Content-Encoding headers,
        /// so gzip is paired with the JS decompression fallback. 1280x720 keeps the
        /// 16:9 layout the UI was designed for (CanvasScaler reference 1600x900).
        /// </summary>
        public static void BuildWebGL()
        {
            ConfigureProject();
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            // Custom template: 16:9 letterbox in any frame, IndexedDB auto-persist of the journal.
            PlayerSettings.WebGL.template = "PROJECT:DestinyGarden";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.showDiagnostics = false;
            string outDir = "Builds/WebGL";
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath },
                outDir, BuildTarget.WebGL, BuildOptions.None);
            Debug.Log("DestinyGarden WebGL build: " + report.summary.result
                + " (" + report.summary.totalSize + " bytes)");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        public static void BuildIosXcodeProject()
        {
            ConfigureProject();
            string outDir = "Builds/iOS";
            Directory.CreateDirectory(outDir);
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath },
                outDir, BuildTarget.iOS, BuildOptions.None);
            Debug.Log("DestinyGarden iOS Xcode project build: " + report.summary.result);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
