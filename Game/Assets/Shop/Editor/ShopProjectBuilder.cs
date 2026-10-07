using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DdukDduk.Shop.Editor
{
    public static class ShopProjectBuilder
    {
        public const string ScenePath = "Assets/Shop/Scenes/RiceCakeShop.unity";

        [MenuItem("DDUKDDUK/Build or open shop scene")]
        public static void BuildScene()
        {
            Directory.CreateDirectory("Assets/Shop/Scenes");
            AssetDatabase.Refresh();
            ConfigureTexture("Assets/Shop/Resources/Shop/ShopBackground.png", false);
            ConfigureTexture("Assets/Shop/Resources/Shop/RiceCakeAtlas.png", true);
            ConfigureTexture("Assets/Shop/Resources/Shop/CustomerPortrait.png", true);
            ConfigureTexture("Assets/Shop/Resources/Shop/RecipeBookBackground.png", true);
            ConfigureTexture("Assets/Shop/Resources/Shop/RecipeBookReference.png", false);
            ConfigureTexture("Assets/Shop/Resources/Shop/RecipeResearchButtonSource.png", false);
            ConfigureTexture("Assets/Shop/Resources/Shop/RecipeSpreadMugwort.png", false);
            ConfigureTexture("Assets/Shop/Resources/Shop/RecipeSpreadHoney.png", false);
            ConfigureTexture("Assets/Shop/Resources/Shop/RecipeSpreadBean.png", false);
            ConfigureTexture("Assets/Shop/Resources/Shop/RecipeSpreadRoyal.png", false);
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.companyName = "DDUKDDUK";
            PlayerSettings.productName = "떡방앗간";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.runInBackground = true;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.15f, 0.09f, 0.055f);
            cameraObject.transform.position = new Vector3(0, 0, -10);
            var appObject = new GameObject("Rice Cake Shop");
            var view = appObject.AddComponent<ShopView>();
            appObject.AddComponent<ShopApp>();
            appObject.AddComponent<ShopDayFlow>();
            view.BuildUI();
            ShopApp.EnsureEventSystem();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("SHOP_SCENE_CREATED: " + Path.GetFullPath(ScenePath));
        }

        private static void ConfigureTexture(string path, bool transparent)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = transparent;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        public static void BuildAndVerify()
        {
            BuildScene();
            ShopSceneVerification.RunAll();
            ShopModelVerification.RunAll();
            ShopCommonApiVerification.RunAll();
            Debug.Log("SHOP_BUILD_AND_VERIFY_OK");
        }

        public static void BuildPreview()
        {
            BuildAndVerify();
            string destination = Path.GetFullPath("Builds/ShopPreview/ShopPreview.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = destination,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Shop preview build failed: " + report.summary.result);
            Debug.Log("SHOP_PREVIEW_BUILD_OK: " + destination);
        }

        [MenuItem("DDUKDDUK/Play testing/Set reputation 0 - 동네 떡집")]
        public static void Reputation0() => SetReputation(0);
        [MenuItem("DDUKDDUK/Play testing/Set reputation 15 - 소문난 떡집")]
        public static void Reputation15() => SetReputation(15);
        [MenuItem("DDUKDDUK/Play testing/Set reputation 35 - 유명한 떡집")]
        public static void Reputation35() => SetReputation(35);
        [MenuItem("DDUKDDUK/Play testing/Unlock all researched products")]
        public static void UnlockResearch()
        {
            var app = UnityEngine.Object.FindFirstObjectByType<ShopApp>();
            if (app == null || app.Model == null) { Debug.Log("Enter Play mode first."); return; }
            for (int i = 5; i < app.Model.Products.Count; i++) app.Model.UnlockProduct(i);
        }

        private static void SetReputation(int score)
        {
            var app = UnityEngine.Object.FindFirstObjectByType<ShopApp>();
            if (app == null || app.Model == null) { Debug.Log("Enter Play mode first."); return; }
            app.Model.AddReputation((score) - app.Model.GetReputation());
        }
    }
}
