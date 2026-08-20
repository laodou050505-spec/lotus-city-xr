#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PicoTowerDefense;

namespace PicoTowerDefenseEditor
{
    /// <summary>
    /// Converts the models that were manually placed in the scene into a
    /// durable, inspectable container. The original scene is backed up before
    /// anything is moved, so the current composition remains recoverable.
    /// </summary>
    public static class SceneModelSnapshotUtility
    {
        // Layout commands intentionally operate on serialized authored poses.
        private const string ScenePath = "Assets/SpatialTowerDefense/Scenes/SpatialDefense.unity";
        private const string SourceFolder = "Assets/模型资产";
        private const string ContainerName = "Saved Scene Model Placements";
        private const string RestoreRequestPath = "Temp/RestoreAuthoredLayout.once";

        [InitializeOnLoadMethod]
        private static void RunPendingAuthoredLayoutRestore()
        {
            if (!File.Exists(RestoreRequestPath))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                File.Delete(RestoreRequestPath);
                RestoreAuthoredModelTransforms();
                OpenSavedSceneModelPlacement();
            };
        }

        private static readonly Dictionary<string, string> ResourceBySourceFolder = new(StringComparer.OrdinalIgnoreCase)
        {
            ["s1"] = "GameplayModels/Scene_S01_FloatingMountainBase",
            ["s2"] = "GameplayModels/Scene_S02_StonePath",
            ["s3"] = "GameplayModels/Scene_S03_LowStoneWall",
            ["s4"] = "GameplayModels/Scene_S04_CliffWell",
            ["s5"] = "GameplayModels/Scene_S05_StoneStairs",
            ["s6"] = "GameplayModels/Scene_S06_ArchBridge",
            ["s7"] = "GameplayModels/Scene_S07_MountainGate",
            ["s8"] = "GameplayModels/Scene_S08_MainTemple",
            ["s9"] = "GameplayModels/Scene_S09_Corridor",
            ["s10"] = "GameplayModels/Scene_S10_SmallGrotto",
            ["s11"] = "GameplayModels/Scene_S11_BuddhaNiche",
            ["s12"] = "GameplayModels/Scene_S12_ReleasePond",
            ["s13"] = "GameplayModels/Scene_S13_AncientCypress",
            ["s14"] = "GameplayModels/Scene_S14_PrayerBanners",
            ["s15"] = "GameplayModels/Scene_S15_StoneLantern",
            ["s16"] = "GameplayModels/Scene_S16_IncenseBrazier",
            ["s17"] = "GameplayModels/Scene_S17_GrassPlatform",
            ["s18"] = "GameplayModels/Scene_S18_BambooRock",
            ["s19"] = "GameplayModels/Scene_S19_Rock",
            ["s20"] = "GameplayModels/Scene_S20_RockB",
            ["s21"] = "GameplayModels/Scene_S21_Bonsai"
        };

        [MenuItem("Tools/Spatial Tower Defense/Save Current Scene Model Placements")]
        public static void SaveCurrentSceneModelPlacements()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            SpatialTowerDefenseGame game = FindGame(scene);
            if (game == null)
            {
                Debug.LogError("Spatial Tower Defense game root was not found.");
                return;
            }

            string backupPath = ScenePath + ".before-model-snapshot";
            File.Copy(ScenePath, backupPath, true);

            Transform container = GetOrCreateContainer(game.transform);
            Dictionary<string, int> counts = new(StringComparer.OrdinalIgnoreCase);
            int imported = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null || root == game.gameObject || root == container.gameObject ||
                    root.name.StartsWith("Editor Scene Preview", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!PrefabUtility.IsAnyPrefabInstanceRoot(root))
                {
                    continue;
                }

                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(root);
                string sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
                if (string.IsNullOrWhiteSpace(sourcePath) || !sourcePath.StartsWith(SourceFolder + "/", StringComparison.Ordinal))
                {
                    continue;
                }

                string sourceFolder = Path.GetFileName(Path.GetDirectoryName(sourcePath));
                if (!ResourceBySourceFolder.TryGetValue(sourceFolder, out string resourcePath))
                {
                    Debug.LogWarning($"[Scene Snapshot] No Resources mapping for {sourcePath}; leaving it in place.");
                    continue;
                }

                if (resourcePath == ProceduralFactory.BoardPlacementVisualResourcePath ||
                    resourcePath == ProceduralFactory.BoardRouteVisualResourcePath)
                {
                    Debug.LogWarning($"[Scene Snapshot] Gameplay board tile was ignored: {sourcePath}");
                    continue;
                }

                Undo.RegisterFullObjectHierarchyUndo(root, "Save Scene Model Placement");
                SceneModelAnchor anchor = root.GetComponent<SceneModelAnchor>();
                if (anchor == null)
                {
                    anchor = Undo.AddComponent<SceneModelAnchor>(root);
                }
                anchor.Configure(resourcePath, sourcePath);

                root.transform.SetParent(container, true);
                // Preserve the authored Transform exactly.  The designer's
                // position, rotation, and (when intentional) non-uniform
                // scale are part of the scene layout and must not be
                // normalized by a snapshot operation.
                MakeDecorationOnly(root);
                RepairMaterials(root, sourceFolder);

                int ordinal = counts.TryGetValue(sourceFolder, out int previous) ? previous + 1 : 1;
                counts[sourceFolder] = ordinal;
                root.name = $"Saved {sourceFolder.ToUpperInvariant()} {ordinal:00}";
                EditorUtility.SetDirty(root);
                imported++;
            }

            EditorUtility.SetDirty(container);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditableSceneModelLayout layout = game.GetComponent<EditableSceneModelLayout>();
            if (layout != null)
            {
                layout.BuildEditorPreview();
                Selection.activeObject = container.gameObject;
                EditorGUIUtility.PingObject(container.gameObject);
            }

            Debug.Log($"[Scene Snapshot] Saved {imported} manually placed models under '{ContainerName}'. Backup: {backupPath}");
        }

        [MenuItem("Tools/Spatial Tower Defense/Repair Saved Scene Model Materials")]
        public static void RepairSavedSceneModelMaterials()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            Transform container = FindContainer(scene);
            if (container == null)
            {
                Debug.LogWarning("Saved scene model container was not found. Run Save Current Scene Model Placements first.");
                return;
            }

            int repaired = 0;
            SceneModelAnchor[] anchors = container.GetComponentsInChildren<SceneModelAnchor>(true);
            for (int i = 0; i < anchors.Length; i++)
            {
                SceneModelAnchor anchor = anchors[i];
                string sourceFolder = Path.GetFileName(Path.GetDirectoryName(anchor.SourceAssetPath));
                RepairMaterials(anchor.gameObject, sourceFolder);
                EditorUtility.SetDirty(anchor.gameObject);
                repaired++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Scene Snapshot] Repaired materials and grounding for {repaired} saved models.");
        }

        [MenuItem("Tools/Spatial Tower Defense/Open Saved Scene Model Placement")]
        public static void OpenSavedSceneModelPlacement()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            Transform container = FindContainer(scene);
            if (container == null)
            {
                SaveCurrentSceneModelPlacements();
                container = FindContainer(scene);
            }

            if (container == null)
            {
                return;
            }

            SpatialTowerDefenseGame game = FindGame(scene);
            if (game == null)
            {
                Debug.LogError("Spatial Tower Defense game root was not found.");
                return;
            }

            // Build the non-persistent preview as part of opening the tool so
            // the user immediately sees the complete island, including the
            // serialized Buddha, bridge, and temple entries.
            EditableSceneModelLayout layout = game.GetComponent<EditableSceneModelLayout>();
            if (!Application.isPlaying && layout != null)
            {
                layout.BuildEditorPreview();
            }

            // Select the layout component so the editable model list is
            // immediately available in the Inspector. The saved placement
            // container remains in the Hierarchy for direct transform edits.
            UnityEngine.Object selection = layout != null ? layout : container.gameObject;
            Selection.activeObject = selection;
            EditorGUIUtility.PingObject(selection);

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                if (TryCalculateLayoutBounds(game.transform, out Bounds layoutBounds))
                {
                    float size = Mathf.Max(layoutBounds.size.x, Mathf.Max(layoutBounds.size.y, layoutBounds.size.z));
                    // A single imported roof can have an oversized renderer
                    // bound and make the complete island appear tiny. Keep the
                    // initial view centered on the authored composition while
                    // allowing the user to zoom out manually when needed.
                    float viewSize = Mathf.Clamp(size * 0.88f, 1.6f, 3.6f);
                    sceneView.LookAt(layoutBounds.center, Quaternion.Euler(52f, -32f, 0f), viewSize);
                }
                else
                {
                    sceneView.FrameSelected();
                }
                sceneView.Repaint();
            }
        }

        [MenuItem("Tools/Spatial Tower Defense/Restore Authored Model Transforms")]
        public static void RestoreAuthoredModelTransforms()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            Transform container = FindContainer(scene);
            if (container == null)
            {
                Debug.LogWarning("Saved scene model container was not found. Nothing to restore.");
                return;
            }

            string backupPath = ScenePath + ".before-model-snapshot";
            if (!File.Exists(backupPath))
            {
                Debug.LogError($"Original authored layout backup was not found: {backupPath}");
                return;
            }

            Dictionary<string, List<AuthoredPose>> posesByGuid = ParseAuthoredPoses(File.ReadAllText(backupPath));
            Dictionary<string, List<SceneModelAnchor>> anchorsByGuid = new(StringComparer.OrdinalIgnoreCase);
            SceneModelAnchor[] anchors = container.GetComponentsInChildren<SceneModelAnchor>(true);
            if (anchors.Length == 0 || posesByGuid.Count == 0)
            {
                Debug.LogError("Authored layout restore aborted: the scene or backup contains no model anchors.");
                return;
            }
            for (int i = 0; i < anchors.Length; i++)
            {
                SceneModelAnchor anchor = anchors[i];
                string guid = string.IsNullOrWhiteSpace(anchor.SourceAssetPath)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(anchor.SourceAssetPath);
                if (string.IsNullOrWhiteSpace(guid))
                {
                    continue;
                }

                if (!anchorsByGuid.TryGetValue(guid, out List<SceneModelAnchor> list))
                {
                    list = new List<SceneModelAnchor>();
                    anchorsByGuid.Add(guid, list);
                }
                list.Add(anchor);
            }

            List<(SceneModelAnchor anchor, AuthoredPose pose)> assignments = new();
            foreach (KeyValuePair<string, List<SceneModelAnchor>> group in anchorsByGuid)
            {
                if (!posesByGuid.TryGetValue(group.Key, out List<AuthoredPose> poses) || poses.Count == 0)
                {
                    continue;
                }

                bool[] used = new bool[poses.Count];
                foreach (SceneModelAnchor anchor in group.Value)
                {
                    int bestIndex = -1;
                    float bestScore = float.PositiveInfinity;
                    for (int poseIndex = 0; poseIndex < poses.Count; poseIndex++)
                    {
                        if (used[poseIndex])
                        {
                            continue;
                        }

                        AuthoredPose pose = poses[poseIndex];
                        float score = (anchor.transform.localPosition - pose.Position).sqrMagnitude;
                        score += (anchor.transform.localScale - pose.Scale).sqrMagnitude * 0.25f;
                        score += Quaternion.Angle(anchor.transform.localRotation, pose.Rotation) * 0.0001f;
                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestIndex = poseIndex;
                        }
                    }

                    if (bestIndex < 0)
                    {
                        continue;
                    }

                    AuthoredPose selected = poses[bestIndex];
                    used[bestIndex] = true;
                    assignments.Add((anchor, selected));
                }
            }

            if (assignments.Count != anchors.Length)
            {
                Debug.LogError($"Authored layout restore aborted: matched {assignments.Count} of {anchors.Length} anchors.");
                return;
            }

            int restored = 0;
            foreach (var assignment in assignments)
            {
                SceneModelAnchor anchor = assignment.anchor;
                AuthoredPose selected = assignment.pose;
                Undo.RecordObject(anchor.transform, "Restore Authored Model Transform");
                anchor.transform.localPosition = selected.Position;
                anchor.transform.localRotation = selected.Rotation;
                anchor.transform.localScale = selected.Scale;
                EditorUtility.SetDirty(anchor.transform);
                restored++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Scene Layout] Restored {restored} authored model transforms from {backupPath}.");
        }

        private readonly struct AuthoredPose
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 Scale;

            public AuthoredPose(Vector3 position, Quaternion rotation, Vector3 scale)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }
        }

        private static Dictionary<string, List<AuthoredPose>> ParseAuthoredPoses(string yaml)
        {
            Dictionary<string, List<AuthoredPose>> result = new(StringComparer.OrdinalIgnoreCase);
            MatchCollection blocks = Regex.Matches(yaml, @"^--- !u!1001 .*?(?=^--- !u!|\z)", RegexOptions.Multiline | RegexOptions.Singleline);
            foreach (Match block in blocks)
            {
                string sourceGuid = MatchValue(block.Value, @"m_SourcePrefab: \{fileID: 100100000, guid: ([0-9a-f]+), type: 3\}");
                if (string.IsNullOrWhiteSpace(sourceGuid))
                {
                    continue;
                }

                Vector3 position = new(
                    ParseFloat(MatchValue(block.Value, @"propertyPath: m_LocalPosition.x\s+value: ([^\r\n]+)")),
                    ParseFloat(MatchValue(block.Value, @"propertyPath: m_LocalPosition.y\s+value: ([^\r\n]+)")),
                    ParseFloat(MatchValue(block.Value, @"propertyPath: m_LocalPosition.z\s+value: ([^\r\n]+)")));
                Quaternion rotation = new(
                    ParseFloat(MatchValue(block.Value, @"propertyPath: m_LocalRotation.x\s+value: ([^\r\n]+)")),
                    ParseFloat(MatchValue(block.Value, @"propertyPath: m_LocalRotation.y\s+value: ([^\r\n]+)")),
                    ParseFloat(MatchValue(block.Value, @"propertyPath: m_LocalRotation.z\s+value: ([^\r\n]+)")),
                    ParseFloat(MatchValue(block.Value, @"propertyPath: m_LocalRotation.w\s+value: ([^\r\n]+)"), 1f));
                Vector3 scale = new(
                    ParseFloat(MatchValue(block.Value, @"propertyPath: m_LocalScale.x\s+value: ([^\r\n]+)"), 1f),
                    ParseFloat(MatchValue(block.Value, @"propertyPath: m_LocalScale.y\s+value: ([^\r\n]+)"), 1f),
                    ParseFloat(MatchValue(block.Value, @"propertyPath: m_LocalScale.z\s+value: ([^\r\n]+)"), 1f));

                if (!result.TryGetValue(sourceGuid, out List<AuthoredPose> poses))
                {
                    poses = new List<AuthoredPose>();
                    result.Add(sourceGuid, poses);
                }
                poses.Add(new AuthoredPose(position, rotation, scale));
            }

            return result;
        }

        private static string MatchValue(string source, string pattern)
        {
            Match match = Regex.Match(source, pattern, RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private static float ParseFloat(string value, float fallback = 0f)
        {
            return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : fallback;
        }

        private static bool TryCalculateLayoutBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = default;
            bool found = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private static SpatialTowerDefenseGame FindGame(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                SpatialTowerDefenseGame game = roots[i].GetComponentInChildren<SpatialTowerDefenseGame>(true);
                if (game != null)
                {
                    return game;
                }
            }

            return null;
        }

        private static Transform GetOrCreateContainer(Transform gameRoot)
        {
            Transform existing = gameRoot.Find(ContainerName);
            if (existing != null)
            {
                return existing;
            }

            GameObject container = new(ContainerName);
            Undo.RegisterCreatedObjectUndo(container, "Create Saved Scene Model Container");
            container.transform.SetParent(gameRoot, false);
            container.layer = 2;
            return container.transform;
        }

        private static Transform FindContainer(Scene scene)
        {
            SpatialTowerDefenseGame game = FindGame(scene);
            return game != null ? game.transform.Find(ContainerName) : null;
        }

        private static void MakeDecorationOnly(GameObject root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.layer = 2;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].isKinematic = true;
                bodies[i].detectCollisions = false;
            }
        }

        private static void UniformlyScaleAndGround(Transform model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds previous = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                previous.Encapsulate(renderers[i].bounds);
            }

            float uniform = Mathf.Max(Mathf.Abs(model.localScale.x),
                Mathf.Max(Mathf.Abs(model.localScale.y), Mathf.Abs(model.localScale.z)));
            model.localScale = Vector3.one * Mathf.Max(0.0001f, uniform);

            Physics.SyncTransforms();
            renderers = model.GetComponentsInChildren<Renderer>(true);
            Bounds current = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                current.Encapsulate(renderers[i].bounds);
            }

            model.position += new Vector3(
                previous.center.x - current.center.x,
                previous.min.y - current.min.y,
                previous.center.z - current.center.z);
        }

        private static void RepairMaterials(GameObject root, string sourceFolder)
        {
            if (string.IsNullOrWhiteSpace(sourceFolder))
            {
                return;
            }

            string folder = SourceFolder + "/" + sourceFolder;
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            List<Texture2D> textures = new();
            for (int i = 0; i < textureGuids.Length; i++)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(textureGuids[i]));
                if (texture != null)
                {
                    textures.Add(texture);
                }
            }

            if (textures.Count == 0)
            {
                return;
            }

            Texture2D fallback = textures.Find(texture => texture.name.IndexOf("basecolor", StringComparison.OrdinalIgnoreCase) >= 0) ?? textures[0];
            Shader fallbackShader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    materials = new[] { new Material(fallbackShader) };
                }

                Material[] repaired = new Material[materials.Length];
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material source = materials[materialIndex];
                    Material material = source != null ? new Material(source) : new Material(fallbackShader);
                    if (material.shader == null || material.shader == Shader.Find("Hidden/InternalErrorShader"))
                    {
                        material.shader = fallbackShader;
                    }

                    Texture2D chosen = FindMatchingTexture(textures, source, fallback);
                    if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", chosen);
                    if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", chosen);
                    if (material.HasProperty("_BaseColorMap")) material.SetTexture("_BaseColorMap", chosen);
                    if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
                    if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
                    repaired[materialIndex] = material;
                }

                renderer.sharedMaterials = repaired;
                EditorUtility.SetDirty(renderer);
            }
        }

        private static Texture2D FindMatchingTexture(List<Texture2D> textures, Material source, Texture2D fallback)
        {
            if (source != null)
            {
                Texture existing = null;
                if (source.HasProperty("_MainTex")) existing = source.GetTexture("_MainTex");
                if (existing == null && source.HasProperty("_BaseMap")) existing = source.GetTexture("_BaseMap");
                if (existing is Texture2D existingTexture)
                {
                    return existingTexture;
                }

                string materialName = source.name ?? string.Empty;
                for (int i = 0; i < textures.Count; i++)
                {
                    if (materialName.IndexOf(textures[i].name, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return textures[i];
                    }
                }
            }

            return fallback;
        }
    }
}
#endif
