using System;
using System.IO;
using System.Linq;
using ByteDance.PICO.XR;
using PicoTowerDefense;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

namespace PicoTowerDefenseEditor
{
    public static class ProjectSetup
    {
        private const string SceneFolder = "Assets/SpatialTowerDefense/Scenes";
        private const string ScenePath = SceneFolder + "/SpatialDefense.unity";
        private const string XrSettingsPath = "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset";
        private const string AndroidManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string InputSettingsPath = "Assets/SpatialTowerDefense/Settings/InputSystemSettings.asset";
        private const string InputSettingsConfigKey = "com.unity.input.settings";
        private const string PicoXrLoaderType = "ByteDance.PICO.XR.PXR_Loader";

        [MenuItem("Tools/Spatial Tower Defense/Apply PICO Project Setup")]
        public static void Apply()
        {
            EnsureAssetFolder(SceneFolder);
            CreateMainScene();
            ConfigurePlayer();
            ConfigureShaders();
            ConfigureAudioImporters();
            ConfigurePicoXr();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Spatial Tower Defense] Scene, Android player settings, official PICO XR loader, and controller input configured.");
        }

        [MenuItem("Tools/Spatial Tower Defense/Verify Project")]
        public static void Verify()
        {
            if (!GameDefinitions.Validate(out string error))
            {
                throw new BuildFailedException(error);
            }

            if (!ProceduralFactory.ValidateImportedGameplayModels(out string importedModelError))
            {
                throw new BuildFailedException(importedModelError);
            }

            foreach (string audioResource in new[] { "Audio/LotusCityMusic", "Audio/TowerPlace", "Audio/Victory" })
            {
                if (Resources.Load<AudioClip>(audioResource) == null)
                {
                    throw new BuildFailedException($"Required licensed audio is missing or failed to import: Resources/{audioResource}");
                }
            }

            foreach (string textureResource in new[]
                     {
                         "UI/Generated/CoinPanelTop/01_generated_image_url",
                         "UI/Generated/EnemyBriefingPanelV2/01_generated_image_url",
                         "UI/Generated/MorningBellCard/01_generated_image_url",
                         "UI/Generated/GuardianCard/01_generated_image_url",
                         "UI/Generated/LotusLampCard/01_generated_image_url"
                     })
            {
                if (Resources.Load<Texture2D>(textureResource) == null)
                {
                    throw new BuildFailedException($"Generated world-space UI texture is missing: Resources/{textureResource}");
                }
            }
            if (Resources.Load<Shader>("UI/GeneratedUiSurface") == null ||
                Resources.Load<Shader>("UI/WorldTextOverlay") == null ||
                Resources.Load<Shader>("UI/WorldTextTransparent") == null ||
                Resources.Load<Shader>("DesktopCameraBackground") == null)
            {
                throw new BuildFailedException("Generated UI, world text, or desktop MR shader is missing from Resources.");
            }

            if (GameDefinitions.BuildPathCellSet().Count != 21)
            {
                throw new BuildFailedException("Expanded route must occupy exactly 21 board cells.");
            }

            if (!File.Exists(ScenePath))
            {
                throw new BuildFailedException($"Main scene is missing: {ScenePath}");
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpatialTowerDefenseGame>(true)).Count() != 1)
            {
                throw new BuildFailedException("Main scene must contain exactly one SpatialTowerDefenseGame root.");
            }

            EditableSceneModelLayout[] layouts = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EditableSceneModelLayout>(true))
                .ToArray();
            if (layouts.Length != 1)
            {
                throw new BuildFailedException("Main scene must contain exactly one editable decorative model layout.");
            }
            if (!layouts[0].ValidateModels(out string layoutError))
            {
                throw new BuildFailedException(layoutError);
            }

            XRGeneralSettings general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (general == null || general.Manager == null || !XRPackageMetadataStore.IsLoaderAssigned(PicoXrLoaderType, BuildTargetGroup.Android))
            {
                throw new BuildFailedException("Official PICO XR loader is not assigned for Android.");
            }

            UnityEditor.PackageManager.PackageInfo picoPackage = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.bytedance.pico.xr");
            if (picoPackage == null || picoPackage.version != "6.0.0")
            {
                throw new BuildFailedException("Official PICO XR package 6.0.0 is not installed.");
            }
            string[] requiredPicoControllerAssets =
            {
                "Runtime/InputSystem/DeviceLayouts.cs",
                "Runtime/Scripts/OpenXRFeatures/Interactions/PICO4ControllerProfile.cs",
                "Runtime/Scripts/OpenXRFeatures/Interactions/PICO4UltraControllerProfile.cs",
                "Assets/Resources/Prefabs/LeftControllerModel.prefab",
                "Assets/Resources/Prefabs/RightControllerModel.prefab"
            };
            for (int i = 0; i < requiredPicoControllerAssets.Length; i++)
            {
                string requiredPath = Path.Combine(picoPackage.resolvedPath, requiredPicoControllerAssets[i]);
                if (!File.Exists(requiredPath))
                {
                    throw new BuildFailedException($"Official PICO controller asset is missing: {requiredPicoControllerAssets[i]}");
                }
            }

            PXR_Settings picoSettings = PXR_Settings.GetSettings();
            if (picoSettings == null || picoSettings.appMode != PXR_Settings.AppMode.XR ||
                picoSettings.stereoRenderingModeAndroid != PXR_Settings.StereoRenderingModeAndroid.Multiview)
            {
                throw new BuildFailedException("PICO XR settings must use XR app mode and Multiview rendering.");
            }
            PXR_ProjectSetting projectConfig = PXR_ProjectSetting.GetProjectConfig();
            if (projectConfig == null || !projectConfig.videoSeeThrough || !projectConfig.mrSafeguard)
            {
                throw new BuildFailedException("PICO MR must enable video see-through and the MR safeguard.");
            }
            if (PlayerSettings.Android.applicationEntry != AndroidApplicationEntry.Activity)
            {
                throw new BuildFailedException("PICO Android builds must use Activity instead of GameActivity.");
            }

            if (!File.Exists(AndroidManifestPath))
            {
                throw new BuildFailedException("PICO immersive Android manifest is missing.");
            }
            string manifest = File.ReadAllText(AndroidManifestPath);
            if (!manifest.Contains("pvr.app.type") || !manifest.Contains("android:value=\"vr\"") ||
                !manifest.Contains("controller") || !manifest.Contains("enable_vst") ||
                !manifest.Contains("enable_mr_safeguard"))
            {
                throw new BuildFailedException("PICO Android manifest must declare immersive VR, controller support, video see-through, and MR safeguard.");
            }
            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64 ||
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
            {
                throw new BuildFailedException("PICO Android builds must use ARM64 and IL2CPP.");
            }

            SerializedObject playerSettings = new(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            SerializedProperty inputHandling = playerSettings.FindProperty("activeInputHandler");
            if (inputHandling == null || inputHandling.intValue != 1)
            {
                throw new BuildFailedException("Unity Input System must be the only active input backend.");
            }
            if (!EditorBuildSettings.TryGetConfigObject(InputSettingsConfigKey, out InputSettings inputSettings) ||
                inputSettings == null ||
                inputSettings.backgroundBehavior != InputSettings.BackgroundBehavior.IgnoreFocus ||
                inputSettings.editorInputBehaviorInPlayMode != InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView ||
                !PlayerSettings.runInBackground)
            {
                throw new BuildFailedException("Background play must remain enabled and Input System focus changes must not pause PICO or desktop combat.");
            }

            var smokeRoot = new GameObject("Tower Merge Rule Smoke Test");
            try
            {
                SpatialTowerDefenseGame smokeGame = smokeRoot.AddComponent<SpatialTowerDefenseGame>();
                if (!smokeGame.RunMergeRuleSmokeTest(out string mergeError))
                {
                    throw new BuildFailedException(mergeError);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(smokeRoot);
            }

            var dragSmokeRoot = new GameObject("Tower Manual Drag Interaction Smoke Test");
            try
            {
                SpatialTowerDefenseGame dragSmokeGame = dragSmokeRoot.AddComponent<SpatialTowerDefenseGame>();
                if (!dragSmokeGame.RunManualDragInteractionSmokeTest(out string dragError))
                {
                    throw new BuildFailedException(dragError);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dragSmokeRoot);
            }

            var cardSmokeRoot = new GameObject("Tower Build Card Drag Smoke Test");
            try
            {
                SpatialTowerDefenseGame cardSmokeGame = cardSmokeRoot.AddComponent<SpatialTowerDefenseGame>();
                if (!cardSmokeGame.RunBuildCardDragSmokeTest(out string cardError))
                {
                    throw new BuildFailedException(cardError);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cardSmokeRoot);
            }

            var playerWeaponSmokeRoot = new GameObject("Player Right Hand Weapon Smoke Test");
            try
            {
                SpatialTowerDefenseGame playerWeaponSmokeGame = playerWeaponSmokeRoot.AddComponent<SpatialTowerDefenseGame>();
                if (!playerWeaponSmokeGame.RunPlayerWeaponSmokeTest(out string playerWeaponError))
                {
                    throw new BuildFailedException(playerWeaponError);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerWeaponSmokeRoot);
            }

            var combatContinuityRoot = new GameObject("Tower Combat Continuity Smoke Test");
            try
            {
                SpatialTowerDefenseGame combatContinuityGame = combatContinuityRoot.AddComponent<SpatialTowerDefenseGame>();
                if (!combatContinuityGame.RunCombatContinuitySmokeTest(out string combatContinuityError))
                {
                    throw new BuildFailedException(combatContinuityError);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(combatContinuityRoot);
            }

            Debug.Log("[Yi Nian Lotus City] VERIFY PASSED: three-level spatial temple, purification theme, left-hand card/tower placement, right-hand keeper purification, movable restored towers, manual merges through level three, 25 waves with one boss every five waves, PICO immersive VR manifest, PICO XR 6.0.0 loader, Multiview, ARM64/IL2CPP, and controller input are valid.");
        }

        [MenuItem("Tools/Spatial Tower Defense/Fix Background Input Only")]
        public static void ConfigureBackgroundInput()
        {
            EnsureAssetFolder("Assets/SpatialTowerDefense/Settings");
            InputSettings inputSettings = AssetDatabase.LoadAssetAtPath<InputSettings>(InputSettingsPath);
            if (inputSettings == null)
            {
                inputSettings = ScriptableObject.CreateInstance<InputSettings>();
                AssetDatabase.CreateAsset(inputSettings, InputSettingsPath);
            }

            inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            PlayerSettings.runInBackground = true;
            EditorUtility.SetDirty(inputSettings);
            EditorBuildSettings.AddConfigObject(InputSettingsConfigKey, inputSettings, true);
            AssetDatabase.SaveAssets();
            Debug.Log("[Spatial Tower Defense] Background input configured without changing the scene.");
        }

        [MenuItem("Tools/Spatial Tower Defense/Render Preview")]
        public static void RenderPreview()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SpatialTowerDefenseGame game = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SpatialTowerDefenseGame>(true))
                .Single();
            game.BuildPreviewState();

            Camera camera = game.PreviewCamera;
            Transform arena = game.ArenaRoot;
            camera.transform.position = arena.TransformPoint(new Vector3(0.08f, 2.05f, -2.72f));
            camera.transform.LookAt(arena.TransformPoint(new Vector3(0f, GameDefinitions.TableHeight + 0.03f, 0.04f)));
            camera.fieldOfView = 50f;

            const int width = 1600;
            const int height = 1000;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            camera.targetTexture = renderTexture;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            camera.Render();

            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            string outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "SpatialPrototypePreview.png"));
            File.WriteAllBytes(outputPath, png);

            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(renderTexture);
            Debug.Log($"[Spatial Tower Defense] Preview rendered: {outputPath}");

            // BuildPreviewState creates a complete runtime hierarchy and adopts
            // the saved scene models under it. Reload from disk immediately so
            // none of those temporary objects can be serialized into the scene.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/Spatial Tower Defense/Build PICO Development APK")]
        public static void BuildDevelopmentApk()
        {
            Apply();
            Verify();
            string buildDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "PICO"));
            Directory.CreateDirectory(buildDirectory);
            string apkPath = Path.Combine(buildDirectory, "SpatialTowerDefensePICO.apk");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Android build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            }

            Debug.Log($"[Spatial Tower Defense] PICO development APK built: {apkPath}");
        }

        [MenuItem("Tools/Spatial Tower Defense/Build macOS Desktop Test")]
        public static void BuildDesktopTest()
        {
            Verify();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 1000;
            PlayerSettings.resizableWindow = true;

            string buildDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Desktop"));
            Directory.CreateDirectory(buildDirectory);
            string appPath = Path.Combine(buildDirectory, "SpatialTowerDefenseTest.app");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = appPath,
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"macOS desktop build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            }

            Debug.Log($"[Spatial Tower Defense] macOS desktop test build created: {appPath}");
        }

        private static void CreateMainScene()
        {
            Scene scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SpatialTowerDefenseGame[] games = scene.GetRootGameObjects()
                .SelectMany(sceneRoot => sceneRoot.GetComponentsInChildren<SpatialTowerDefenseGame>(true))
                .ToArray();
            if (games.Length > 1)
            {
                throw new BuildFailedException("Cannot update the scene because it contains multiple game roots.");
            }

            GameObject root;
            if (games.Length == 0)
            {
                root = new GameObject("Spatial Tower Defense");
                root.AddComponent<SpatialTowerDefenseGame>();
            }
            else
            {
                root = games[0].gameObject;
            }

            EditableSceneModelLayout layout = root.GetComponent<EditableSceneModelLayout>();
            if (layout == null)
            {
                layout = root.AddComponent<EditableSceneModelLayout>();
            }
            if (layout.MigrateLayout())
            {
                EditorUtility.SetDirty(layout);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Codex Spatial Lab";
            PlayerSettings.productName = "Yi Nian Lotus City PICO";
            PlayerSettings.bundleVersion = "0.2.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.codex.spatialtowerdefense.pico");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

            SerializedObject playerSettings = new(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            SerializedProperty cameraUsage = playerSettings.FindProperty("cameraUsageDescription");
            if (cameraUsage != null)
            {
                cameraUsage.stringValue = "Uses the camera to preview the floating island in your real room without a headset.";
            }
            SerializedProperty inputHandling = playerSettings.FindProperty("activeInputHandler");
            if (inputHandling != null)
            {
                inputHandling.intValue = 1;
            }
            playerSettings.ApplyModifiedPropertiesWithoutUndo();

            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        private static void ConfigurePicoXr()
        {
            EnsureAssetFolder("Assets/XR/Settings");
            XRGeneralSettingsPerBuildTarget perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XrSettingsPath);
            if (perTarget == null)
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, XrSettingsPath);
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);

            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            {
                perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            }

            XRGeneralSettings general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            general.InitManagerOnStart = true;
            while (general.Manager.activeLoaders.Count > 0)
            {
                string loaderType = general.Manager.activeLoaders[0].GetType().FullName;
                XRPackageMetadataStore.RemoveLoader(general.Manager, loaderType, BuildTargetGroup.Android);
            }

            if (!XRPackageMetadataStore.AssignLoader(general.Manager, PicoXrLoaderType, BuildTargetGroup.Android))
            {
                throw new BuildFailedException("Unable to assign the official PICO XR loader.");
            }

            PXR_Settings picoSettings = PXR_Settings.GetSettings();
            if (picoSettings == null)
            {
                picoSettings = AssetDatabase.LoadAssetAtPath<PXR_Settings>("Assets/XR/Settings/PXR_Settings.asset");
            }
            if (picoSettings == null)
            {
                throw new BuildFailedException("PICO XR settings asset is missing.");
            }

            picoSettings.appMode = PXR_Settings.AppMode.XR;
            picoSettings.stereoRenderingModeAndroid = PXR_Settings.StereoRenderingModeAndroid.Multiview;
            PXR_ProjectSetting projectConfig = PXR_ProjectSetting.GetProjectConfig();
            if (projectConfig == null)
            {
                throw new BuildFailedException("PICO project configuration asset is missing.");
            }
            projectConfig.videoSeeThrough = true;
            projectConfig.mrSafeguard = true;
            PXR_ProjectSetting.SaveAssets();
            EditorBuildSettings.AddConfigObject("ByteDance.PICO.XR.Settings", picoSettings, true);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(general.Manager);
            EditorUtility.SetDirty(picoSettings);
        }

        private static void ConfigureShaders()
        {
            UnityEngine.Object graphicsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];
            var graphicsSettings = new SerializedObject(graphicsAsset);
            SerializedProperty shaders = graphicsSettings.FindProperty("m_AlwaysIncludedShaders");
            foreach (string shaderName in new[] { "Standard", "Sprites/Default", "Skybox/Procedural" })
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    continue;
                }

                bool alreadyIncluded = false;
                for (int i = 0; i < shaders.arraySize; i++)
                {
                    if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    {
                        alreadyIncluded = true;
                        break;
                    }
                }

                if (!alreadyIncluded)
                {
                    int index = shaders.arraySize;
                    shaders.InsertArrayElementAtIndex(index);
                    shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
                }
            }
            graphicsSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAudioImporters()
        {
            ConfigureAudioImporter("Assets/Resources/Audio/LotusCityMusic.wav", true);
            ConfigureAudioImporter("Assets/Resources/Audio/TowerPlace.wav", false);
            ConfigureAudioImporter("Assets/Resources/Audio/Victory.wav", false);
        }

        private static void ConfigureAudioImporter(string assetPath, bool streamingMusic)
        {
            if (AssetImporter.GetAtPath(assetPath) is not AudioImporter importer)
            {
                throw new BuildFailedException($"Audio asset is missing or not importable: {assetPath}");
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            AudioClipLoadType desiredLoadType = streamingMusic
                ? AudioClipLoadType.Streaming
                : AudioClipLoadType.CompressedInMemory;
            float desiredQuality = streamingMusic ? 0.52f : 0.78f;
            bool changed = settings.loadType != desiredLoadType ||
                           settings.compressionFormat != AudioCompressionFormat.Vorbis ||
                           settings.sampleRateSetting != AudioSampleRateSetting.OptimizeSampleRate ||
                           !Mathf.Approximately(settings.quality, desiredQuality) ||
                           importer.forceToMono == streamingMusic ||
                           importer.loadInBackground != streamingMusic ||
                           settings.preloadAudioData == streamingMusic;
            if (!changed)
            {
                return;
            }

            settings.loadType = desiredLoadType;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
            settings.quality = desiredQuality;
            settings.preloadAudioData = !streamingMusic;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !streamingMusic;
            importer.loadInBackground = streamingMusic;
            importer.SaveAndReimport();
        }

        private static void EnsureAssetFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next) && !Directory.Exists(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
