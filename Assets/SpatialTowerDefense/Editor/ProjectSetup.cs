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
        private const string TitleScenePath = SceneFolder + "/LotusTitleStage.unity";
        private const string ScenePath = SceneFolder + "/SpatialDefense.unity";
        private const string XrSettingsPath = "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset";
        private const string AndroidManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string InputSettingsPath = "Assets/SpatialTowerDefense/Settings/InputSystemSettings.asset";
        private const string PreferredIconPath = "Assets/SpatialTowerDefense/Art/GeneratedIcons/2026-08-19/YiNianLotusCity_AppIcon_2048.png";
        private const string StartupCoverPath = "Assets/SpatialTowerDefense/Resources/UI/Startup/LotusCityStartCover.png";
        private const string StartupCoverPressedPath = "Assets/SpatialTowerDefense/Resources/UI/Startup/LotusCityStartCoverPressed.png";
        private const string InputSettingsConfigKey = "com.unity.input.settings";
        private const string PicoXrLoaderType = "ByteDance.PICO.XR.PXR_Loader";

        [MenuItem("Tools/Spatial Tower Defense/Apply PICO Project Setup")]
        public static void Apply()
        {
            EnsureAssetFolder(SceneFolder);
            CreateMainScene();
            CreateTitleScene();
            ConfigurePlayer();
            ConfigureShaders();
            ConfigureAudioImporters();
            ConfigurePicoXr();
            ConfigureApplicationIcon();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Spatial Tower Defense] Scene, Android player settings, official PICO XR loader, and controller input configured.");
        }

        private static void ConfigureApplicationIcon()
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(PreferredIconPath);
            if (icon == null)
            {
                Debug.LogWarning($"[Yi Nian Lotus City] App icon not assigned because the generated icon is missing: {PreferredIconPath}");
                return;
            }

            TextureImporter importer = AssetImporter.GetAtPath(PreferredIconPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                importer.SaveAndReimport();
                icon = AssetDatabase.LoadAssetAtPath<Texture2D>(PreferredIconPath);
            }

            NamedBuildTarget[] targets = { NamedBuildTarget.Android, NamedBuildTarget.Standalone };
            for (int i = 0; i < targets.Length; i++)
            {
                int[] sizes = PlayerSettings.GetIconSizes(targets[i], IconKind.Application);
                Texture2D[] icons = new Texture2D[sizes.Length];
                for (int iconIndex = 0; iconIndex < icons.Length; iconIndex++)
                {
                    icons[iconIndex] = icon;
                }
                PlayerSettings.SetIcons(targets[i], icons, IconKind.Application);
            }
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

            if (Resources.Load<Font>("Fonts/NotoSansSC-Regular") == null)
            {
                throw new BuildFailedException("Required Noto Sans SC font is missing; Chinese world-space UI cannot render.");
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
            ValidateFigmaStartupFrame(StartupCoverPath, "default");
            ValidateFigmaStartupFrame(StartupCoverPressedPath, "Start interaction");
            Shader titleFeatherShader = Resources.Load<Shader>(LotusTitleStage.TitleFeatherShaderResource);
            if (titleFeatherShader == null || !titleFeatherShader.isSupported ||
                titleFeatherShader.name != LotusTitleStage.TitleFeatherShaderName)
            {
                throw new BuildFailedException("The title edge-feather shader is missing, unsupported, or has the wrong shader name.");
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

            if (!File.Exists(TitleScenePath) || !File.Exists(ScenePath))
            {
                throw new BuildFailedException($"Title or gameplay scene is missing: {TitleScenePath} / {ScenePath}");
            }

            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length != 2 || buildScenes[0].path != TitleScenePath || buildScenes[1].path != ScenePath ||
                !buildScenes[0].enabled || !buildScenes[1].enabled)
            {
                throw new BuildFailedException("Build Settings must start with the separate LotusTitleStage scene, followed by SpatialDefense gameplay.");
            }

            Scene titleScene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            LotusTitleStage[] titleRoots = titleScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LotusTitleStage>(true))
                .ToArray();
            if (titleRoots.Length != 1 || titleScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SpatialTowerDefenseGame>(true)).Any())
            {
                throw new BuildFailedException("LotusTitleStage must be a standalone title-only scene with no gameplay root.");
            }

            var titleSmokeRoot = new GameObject("Independent Title Scene Smoke Test");
            try
            {
                LotusTitleStage titleSmoke = titleSmokeRoot.AddComponent<LotusTitleStage>();
                if (!titleSmoke.RunTitleStageSmokeTest(out string titleError))
                {
                    throw new BuildFailedException(titleError);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(titleSmokeRoot);
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
            if (!manifest.Contains("com.unity3d.player.UnityPlayerActivity") ||
                manifest.Contains("com.unity3d.player.appui.AppUIActivity"))
            {
                throw new BuildFailedException("PICO Android manifest must expose the packaged UnityPlayerActivity as its launcher, not the unavailable AppUIActivity.");
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

            var startupCoverCleanupRoot = new GameObject("Startup Cover Cleanup Smoke Test");
            try
            {
                SpatialTowerDefenseGame startupCoverGame = startupCoverCleanupRoot.AddComponent<SpatialTowerDefenseGame>();
                if (!startupCoverGame.RunStartupCoverCleanupSmokeTest(out string startupCoverError))
                {
                    throw new BuildFailedException(startupCoverError);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(startupCoverCleanupRoot);
            }

            var interactionGuideRoot = new GameObject("Two Hand Guide Lifecycle Smoke Test");
            try
            {
                SpatialTowerDefenseGame interactionGuideGame = interactionGuideRoot.AddComponent<SpatialTowerDefenseGame>();
                if (!interactionGuideGame.RunInteractionGuideLifecycleSmokeTest(out string interactionGuideError))
                {
                    throw new BuildFailedException(interactionGuideError);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(interactionGuideRoot);
            }

            var worldSpacePresentationRoot = new GameObject("World Space Presentation Smoke Test");
            try
            {
                SpatialTowerDefenseGame worldSpaceGame = worldSpacePresentationRoot.AddComponent<SpatialTowerDefenseGame>();
                if (!worldSpaceGame.RunWorldSpacePresentationSmokeTest(out string worldSpaceError))
                {
                    throw new BuildFailedException(worldSpaceError);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(worldSpacePresentationRoot);
            }

            VerifyRoomScaleWalkaround();

            Debug.Log("[Yi Nian Lotus City] VERIFY PASSED: room-anchored tabletop, left-controller selection/placement/wave controls, right-controller purification/recenter controls, world-space two-hand guide, movable restored towers, manual merges through level three, 25 waves with one boss every five waves, PICO immersive VR manifest, PICO XR 6.0.0 loader, Multiview, ARM64/IL2CPP, and Input System-only controls are valid.");
        }

        // Exercise the serialized production scene, not an empty smoke-test host.
        public static void VerifyRoomScaleWalkaround()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            try
            {
                SpatialTowerDefenseGame game = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SpatialTowerDefenseGame>(true)).Single();
                var scenery = new AuthoredSceneryRegression.Snapshot(scene);
                game.BuildWorld();
                scenery.Verify(game);
                Transform arena = game.ArenaRoot;
                float width = arena.TransformVector(Vector3.right * GameDefinitions.GridColumns * GameDefinitions.CellSize).magnitude;
                if (Mathf.Abs(width - 2.2f) > 0.01f)
                    throw new BuildFailedException($"Room-scale board must be 2.2 meters wide; production scene is {width:F3} meters (scale {arena.lossyScale}).");
                if (!game.RunWorldSpacePresentationSmokeTest(out string error))
                    throw new BuildFailedException(error);
                SpatialInputRig rig = game.GetComponentInChildren<SpatialInputRig>();
                var applyPose = typeof(SpatialInputRig).GetMethod("ApplyTrackedHeadPose",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Vector3 fixedPosition = arena.position;
                Quaternion fixedRotation = arena.rotation;
                Vector3 target = arena.TransformPoint(GameDefinitions.DesignPlayerViewTargetLocal);
                Vector3 startingHead = new Vector3(0f, 1.62f, 0f);
                applyPose.Invoke(rig, new object[] { startingHead, Quaternion.identity });
                Vector3 beforeStep = rig.Camera.transform.position;
                applyPose.Invoke(rig, new object[] { startingHead + Vector3.right, Quaternion.identity });
                float step = Vector3.Distance(beforeStep, rig.Camera.transform.position);
                float parallax = Vector3.Angle(target - beforeStep, target - rig.Camera.transform.position);
                if (Mathf.Abs(step - 1f) > 0.001f || parallax < 15f)
                    throw new BuildFailedException($"One-meter head step failed: displacement={step:F3}m, parallax={parallax:F2} degrees.");
                SpatialActionTarget firstCard = arena.GetComponentsInChildren<SpatialActionTarget>(true)
                    .First(action => action.Action == SpatialAction.SelectArrow);
                typeof(SpatialTowerDefenseGame).GetMethod("UpdateTowerCardHover",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(game, new object[] { TowerKind.Arrow, firstCard.transform });
                for (int angle = 0; angle < 360; angle += 45)
                {
                    Vector3 eye = target + Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0.8f, -2.4f);
                    Quaternion look = Quaternion.LookRotation(target - eye, Vector3.up);
                    applyPose.Invoke(rig, new object[] { rig.transform.InverseTransformPoint(eye), Quaternion.Inverse(rig.transform.rotation) * look });
                    foreach (BillboardText panel in arena.GetComponentsInChildren<BillboardText>(true))
                    {
                        Vector3 position = panel.transform.position;
                        panel.FaceTarget();
                        if (Vector3.Distance(panel.transform.position, position) > 0.0001f ||
                            Vector3.Dot(panel.transform.forward, (panel.transform.position - eye).normalized) < 0.999f)
                            throw new BuildFailedException("Panel must rotate toward viewer without moving: " + panel.name);
                    }
                    Vector3 projected = rig.Camera.WorldToViewportPoint(target);
                    if (Vector3.Distance(rig.Camera.transform.position, eye) > 0.001f ||
                        Vector3.Distance(arena.position, fixedPosition) > 0.001f ||
                        Quaternion.Angle(arena.rotation, fixedRotation) > 0.001f ||
                        Mathf.Abs(projected.x - 0.5f) > 0.001f || projected.z < 0f)
                        throw new BuildFailedException($"Tracked walkaround failed at {angle} degrees.");
                }
                if (arena.GetComponentsInChildren<TextMesh>(true).Length != 0)
                    throw new BuildFailedException("Generated gameplay panels must not contain runtime TextMesh labels.");
                CombatVisualEffects.SpawnKeeperImpact(Vector3.zero, arena);
                TransientPurificationRing impact = arena.GetComponentInChildren<TransientPurificationRing>();
                foreach (LineRenderer line in impact.GetComponentsInChildren<LineRenderer>())
                    if (line.startColor.a > 0.001f || line.startWidth > 0.0061f)
                        throw new BuildFailedException("Impact first frame must be transparent and thin; no default white flash.");
                UnityEngine.Object.DestroyImmediate(impact.gameObject);
                Debug.Log("[Lotus UI V5] PASS: baked art, zero TextMesh labels, facing panels anchored during 360-degree circuit, no white first-frame impact.");
                Debug.Log($"[Lotus Walkaround] PASS: production board={width:F3}m, head step={step:F3}m, parallax={parallax:F2} degrees, full 360-degree circuit in 8 tracked poses, arena unchanged.");
            }
            finally
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        public static void BuildUiV5Packages()
        {
            BuildDesktopTest();
            BuildDevelopmentApk();
        }

        public static void VerifyAndRenderUiV5()
        {
            Verify();
            RenderWalkaroundPreview();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SpatialTowerDefenseGame game = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SpatialTowerDefenseGame>(true)).Single();
            game.BuildPreviewState();
            Camera camera = game.PreviewCamera;
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Evidence", "ui-v5-20260908"));
            foreach (string name in new[] { "Spatial Arsenal UI", "World Status HUD", "Next Wave Enemy Skill Briefing", "Two Hand World Interaction Guide", "Tower Hover Information Panel" })
            {
                Transform panel = game.ArenaRoot.Find(name);
                panel.gameObject.SetActive(true);
                if (name == "Tower Hover Information Panel")
                {
                    typeof(SpatialTowerDefenseGame).GetMethod("SetTowerHoverText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(game, new object[] { TowerKind.Arrow, 1 });
                    panel.position = new Vector3(0f, 1.6f, -1.4f);
                }
                camera.transform.position = panel.position + new Vector3(0f, 0.08f, -1.1f);
                camera.transform.LookAt(panel.position);
                foreach (BillboardText face in game.ArenaRoot.GetComponentsInChildren<BillboardText>(true)) face.FaceTarget();
                var render = new RenderTexture(1600, 1200, 24) { antiAliasing = 2 };
                camera.targetTexture = render;
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = render;
                camera.Render();
                var texture = new Texture2D(1600, 1200, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1600, 1200), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(output, name.Replace(" ", "-") + ".png"), texture.EncodeToPNG());
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(render);
            }
            RenderTitleInteractionPreviews();
        }

        public static void RenderWalkaroundPreview()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            try
            {
                SpatialTowerDefenseGame game = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SpatialTowerDefenseGame>(true)).Single();
                game.BuildPreviewState();
                Camera camera = game.PreviewCamera;
                Vector3 target = game.ArenaRoot.TransformPoint(GameDefinitions.DesignPlayerViewTargetLocal);
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Evidence", "ui-v5-20260908"));
                Directory.CreateDirectory(output);
                foreach (int angle in new[] { 0, 90, 180, 270 })
                {
                    camera.transform.position = target + Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 1.12f, -2.4f);
                    camera.transform.LookAt(target);
                    foreach (BillboardText panel in game.ArenaRoot.GetComponentsInChildren<BillboardText>(true)) panel.FaceTarget();
                    var render = new RenderTexture(1200, 900, 24) { antiAliasing = 4 };
                    camera.targetTexture = render;
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = render;
                    camera.Render();
                    var texture = new Texture2D(1200, 900, TextureFormat.RGB24, false);
                    texture.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(output, $"view-{angle:D3}.png"), texture.EncodeToPNG());
                    camera.targetTexture = null;
                    RenderTexture.active = previous;
                    UnityEngine.Object.DestroyImmediate(texture);
                    UnityEngine.Object.DestroyImmediate(render);
                }
                Debug.Log("[Lotus Walkaround] Four production-scene views rendered: " + output);
            }
            finally { EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); }
        }

        private static void ValidateFigmaStartupFrame(string assetPath, string frameName)
        {
            Texture2D frame = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (frame == null || frame.width != 1920 || frame.height != 1080)
            {
                throw new BuildFailedException($"Figma {frameName} startup frame must remain the native 1920x1080 export: {assetPath}");
            }
            if (importer == null || importer.mipmapEnabled ||
                importer.npotScale != TextureImporterNPOTScale.None ||
                importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                throw new BuildFailedException($"Figma {frameName} startup frame import settings must keep mipmaps off, NPOT scaling disabled, and compression uncompressed: {assetPath}");
            }
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

        public static void RenderInteractionGuidePreview()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SpatialTowerDefenseGame game = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SpatialTowerDefenseGame>(true))
                .Single();
            game.BuildPreviewState();

            Camera camera = game.PreviewCamera;
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
            string evidenceDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Evidence"));
            Directory.CreateDirectory(evidenceDirectory);
            string outputPath = Path.Combine(evidenceDirectory, "desktop-two-hand-guide-and-world-anchor.png");
            File.WriteAllBytes(outputPath, png);

            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(renderTexture);
            Debug.Log($"[Spatial Tower Defense] Interaction guide preview rendered: {outputPath}");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        public static void RenderTitleInteractionPreviews()
        {
            Scene titleScene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            LotusTitleStage title = titleScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LotusTitleStage>(true))
                .Single();
            title.BuildTitleStage();

            string evidenceDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Evidence", "ui-v5-20260908"));
            Directory.CreateDirectory(evidenceDirectory);
            RenderTitleState(title, false, false, "desktop-title-default-20260908.png", evidenceDirectory);
            RenderTitleState(title, true, false, "desktop-title-start-hover-20260908.png", evidenceDirectory);
            RenderTitleState(title, false, true, "desktop-title-exit-hover-20260908.png", evidenceDirectory);
            Debug.Log($"[Spatial Tower Defense] Title interaction previews rendered: {evidenceDirectory}");
            EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
        }

        private static void RenderTitleState(
            LotusTitleStage title,
            bool startHover,
            bool exitHover,
            string fileName,
            string evidenceDirectory)
        {
            title.SetPreviewButtonFeedback(startHover, exitHover, false);
            Camera camera = title.TitleCamera;
            const int width = 1920;
            const int height = 1080;
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
            File.WriteAllBytes(Path.Combine(evidenceDirectory, fileName), texture.EncodeToPNG());

            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }

        [MenuItem("Tools/Spatial Tower Defense/Build PICO APK")]
        public static void BuildDevelopmentApk()
        {
            Apply();
            Verify();
            string buildDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "PICO"));
            Directory.CreateDirectory(buildDirectory);
            string apkPath = Path.Combine(buildDirectory, "SpatialTowerDefensePICO.apk");
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                // PICO Emulator 0.13 raises the Unity log level shortly after
                // launch. Unity 6 development players abort in that native SDK
                // callback, so the distributable validation APK must use the
                // normal player configuration.
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Android build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            }

            Debug.Log($"[Spatial Tower Defense] PICO APK built: {apkPath}");
        }

        [MenuItem("Tools/Spatial Tower Defense/Build EN Release APK")]
        public static void BuildEnPicoApk()
        {
            Apply();
            Verify();
            string buildDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "EN"));
            Directory.CreateDirectory(buildDirectory);
            string apkPath = Path.Combine(buildDirectory, "LotusCity-EN-PICO.apk");
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Android build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            }
            Debug.Log($"[Lotus City EN] PICO release APK: {apkPath}");
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
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
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
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true)
            };
        }

        private static void CreateTitleScene()
        {
            Scene titleScene = File.Exists(TitleScenePath)
                ? EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            LotusTitleStage[] titleStages = titleScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LotusTitleStage>(true))
                .ToArray();
            if (titleStages.Length > 1)
            {
                throw new BuildFailedException("Title scene cannot contain more than one LotusTitleStage root.");
            }
            if (titleScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SpatialTowerDefenseGame>(true)).Any())
            {
                throw new BuildFailedException("Refusing to add gameplay objects to the independent title scene.");
            }

            if (titleStages.Length == 0)
            {
                var titleRoot = new GameObject("Lotus Title Stage");
                titleRoot.AddComponent<LotusTitleStage>();
            }

            EditorSceneManager.SaveScene(titleScene, TitleScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true)
            };
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Codex Spatial Lab";
            PlayerSettings.productName = "Lotus City";
            PlayerSettings.bundleVersion = "0.2.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.codex.spatialtowerdefense.pico.en");
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
            foreach (string shaderName in new[] { "Standard", "Sprites/Default", "Unlit/Transparent", "Skybox/Procedural" })
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

        // ---------------------------------------------------------------------------
        // Polish-20261022: batchmode-safe import + scene-save step
        // Run via: unity -buildTarget Android -executeMethod PicoTowerDefenseEditor.ProjectSetup.ApplyPolish20261022
        // ---------------------------------------------------------------------------
        [MenuItem("Tools/Spatial Tower Defense/Apply Polish 2026-10-22")]
        public static void ApplyPolish20261022()
        {
            // 1. Force reimport regenerated panel textures (Status, Briefing).
            //    GeneratedPanelImporter.OnPreprocessTexture applies the correct
            //    import settings automatically on reimport.
            foreach (string texPath in new[]
            {
                "Assets/SpatialTowerDefense/Resources/UI/GeneratedV5/Status.png",
                "Assets/SpatialTowerDefense/Resources/UI/GeneratedV5/Briefing.png",
                "Assets/SpatialTowerDefense/Resources/UI/GeneratedV5/Guide.png",
                "Assets/SpatialTowerDefense/Resources/UI/GeneratedV5/Stats.png",
                "Assets/SpatialTowerDefense/Resources/UI/GeneratedV5/WaveStates.png",
                "Assets/SpatialTowerDefense/Resources/UI/GeneratedV5/Arsenal.png",
            })
            {
                if (AssetImporter.GetAtPath(texPath) is TextureImporter ti)
                {
                    ti.SaveAndReimport();
                    Debug.Log($"[Polish] Reimported texture: {texPath}");
                }
                else
                {
                    Debug.LogWarning($"[Polish] Texture not found (will be imported on next refresh): {texPath}");
                }
            }

            // 2. Import and configure the new UI button audio clips.
            foreach (string audioPath in new[]
            {
                "Assets/SpatialTowerDefense/Resources/UI/Audio/med_ui_soft_02.mp3",
                "Assets/SpatialTowerDefense/Resources/UI/Audio/med_pos_hard_01.mp3",
            })
            {
                if (AssetImporter.GetAtPath(audioPath) is AudioImporter ai)
                {
                    var settings = ai.defaultSampleSettings;
                    settings.loadType = AudioClipLoadType.CompressedInMemory;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
                    settings.quality = 0.78f;
                    settings.preloadAudioData = true;
                    ai.defaultSampleSettings = settings;
                    ai.forceToMono = false;
                    ai.loadInBackground = false;
                    ai.SaveAndReimport();
                    Debug.Log($"[Polish] Imported audio: {audioPath}");
                }
                else
                {
                    // Asset not yet tracked — refresh first then retry.
                    AssetDatabase.ImportAsset(audioPath, ImportAssetOptions.ForceSynchronousImport);
                    Debug.Log($"[Polish] Force-imported audio: {audioPath}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Polish-20261022] Texture reimport and audio import complete (Status, Briefing, Guide, Stats, WaveStates). Run Verify to check all panels.");
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
