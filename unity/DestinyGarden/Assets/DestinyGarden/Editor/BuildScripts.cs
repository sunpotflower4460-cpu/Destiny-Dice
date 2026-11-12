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

            // Code-created materials use URP/Lit via Shader.Find; keep it from being stripped.
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var serialized = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var alwaysIncluded = serialized.FindProperty("m_AlwaysIncludedShaders");
            bool found = false;
            for (int i = 0; i < alwaysIncluded.arraySize; i++)
                if (alwaysIncluded.GetArrayElementAtIndex(i).objectReferenceValue == lit) found = true;
            if (lit != null && !found)
            {
                alwaysIncluded.arraySize++;
                alwaysIncluded.GetArrayElementAtIndex(alwaysIncluded.arraySize - 1).objectReferenceValue = lit;
                serialized.ApplyModifiedProperties();
            }

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
