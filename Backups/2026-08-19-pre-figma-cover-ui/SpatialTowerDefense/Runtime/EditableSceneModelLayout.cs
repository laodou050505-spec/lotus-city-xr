using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PicoTowerDefense
{
    [DisallowMultipleComponent]
    public sealed class EditableSceneModelLayout : MonoBehaviour
    {
        [Serializable]
        public sealed class SceneModelEntry
        {
            [Tooltip("Show this decorative model in the arena without enabling gameplay interaction.")]
            public bool visible = true;

            [Tooltip("A label used only to keep the layout readable in the Inspector and Hierarchy.")]
            public string label;

            [Tooltip("Resources path without a file extension, for example GameplayModels/Scene_S08_MainTemple.")]
            public string modelPath;

            [Tooltip("Local position under Room Anchored Arena, in meters.")]
            public Vector3 position;

            [Tooltip("Local XYZ rotation under Room Anchored Arena, in degrees.")]
            public Vector3 rotation;

            [Tooltip("Uniform scale after the imported model is normalized to a one-meter longest axis. Keep X, Y, and Z equal to avoid deforming the model.")]
            public Vector3 scale = Vector3.one;

            public SceneModelEntry(
                string label,
                string modelPath,
                Vector3 position,
                Vector3 rotation,
                Vector3 scale,
                bool visible = true)
            {
                this.visible = visible;
                this.label = label;
                this.modelPath = modelPath;
                this.position = position;
                this.rotation = rotation;
                this.scale = scale;
            }
        }

        private const string ContainerName = "Editable Decorative Scene Models";
        private const int IgnoreRaycastLayer = 2;

        [SerializeField]
        [Tooltip("Normalizes each imported FBX to a one-meter longest axis before applying its entry Scale.")]
        private bool normalizeImportedModels = true;

        [SerializeField]
        [Tooltip("When enabled, a saved scene-authored model suppresses a duplicate entry with the same Resources path. Disable this to intentionally place another copy.")]
        private bool skipSavedSceneDuplicates = true;

        [SerializeField]
        [Tooltip("Decorative models only. These entries do not alter paths, cells, towers, enemies, or rules.")]
        private List<SceneModelEntry> entries = CreateDefaultEntries();

        private Transform _arenaRoot;
        private Transform _instancesRoot;
        private readonly Dictionary<string, Transform> _builtEntryRoots = new();

        public IReadOnlyList<SceneModelEntry> Entries => entries;

        /// <summary>
        /// Returns the instantiated root for a saved decorative entry. Gameplay
        /// uses this only to attach the protection state to the user's temple;
        /// entry position, rotation and scale remain owned by the Inspector.
        /// </summary>
        public bool TryGetBuiltEntry(string label, out Transform entryRoot)
        {
            if (!string.IsNullOrWhiteSpace(label) &&
                _builtEntryRoots.TryGetValue(label, out entryRoot) && entryRoot != null)
            {
                return true;
            }

            entryRoot = null;
            return false;
        }

        public bool ValidateModels(out string error)
        {
            if (entries == null || entries.Count == 0)
            {
                error = "Editable scene layout has no decorative model entries.";
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                SceneModelEntry entry = entries[i];
                if (entry == null || !entry.visible || string.IsNullOrWhiteSpace(entry.modelPath))
                {
                    continue;
                }

                if (IsBoardTileResource(entry.modelPath))
                {
                    error = $"Board tile models are gameplay-only and cannot be used as decoration: Resources/{entry.modelPath}";
                    return false;
                }

                GameObject prefab = Resources.Load<GameObject>(entry.modelPath);
                if (prefab == null || prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
                {
                    error = $"Decorative scene model is missing or has no mesh: Resources/{entry.modelPath}";
                    return false;
                }
                if (Resources.LoadAll<Texture2D>(entry.modelPath + ".fbm").Length == 0)
                {
                    error = $"Decorative scene model base-color texture is missing: Resources/{entry.modelPath}.fbm";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private void OnEnable()
        {
            MigrateLayout();
        }

        public bool MigrateLayout()
        {
            // Migrate only the untouched first-pass defaults so later Inspector edits remain intact.
            if (entries == null || UsesLegacyDefaultEntries(entries) || UsesFirstPassDecorativeEntries(entries))
            {
                entries = CreateDefaultEntries();
                return true;
            }

            return entries.RemoveAll(entry => entry != null && IsBoardTileResource(entry.modelPath)) > 0;
        }

        public void Build(Transform arenaRoot)
        {
            if (arenaRoot == null)
            {
                throw new ArgumentNullException(nameof(arenaRoot));
            }

            _arenaRoot = arenaRoot;
            RemoveExistingInstances(arenaRoot);
            _builtEntryRoots.Clear();

            _instancesRoot = new GameObject(ContainerName).transform;
            _instancesRoot.SetParent(arenaRoot, false);
            _instancesRoot.gameObject.layer = IgnoreRaycastLayer;

            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                SceneModelEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.modelPath))
                {
                    continue;
                }

                if (IsBoardTileResource(entry.modelPath))
                {
                    Debug.LogWarning($"[Editable Scene Layout] Board tile skipped because it is reserved for gameplay: {entry.modelPath}", this);
                    continue;
                }

                if (skipSavedSceneDuplicates && SceneModelAnchor.HasSavedModelForPath(entry.modelPath))
                {
                    // The scene snapshot is the authoritative placement for
                    // this resource. Keeping the entry serialized makes it
                    // easy to re-enable later, while avoiding a second model
                    // at runtime and in the edit-mode preview. Keep a lookup
                    // to the authored object as well, so gameplay can use it
                    // (for example, as the protected temple) without cloning.
                    if (!string.IsNullOrWhiteSpace(entry.label) &&
                        SceneModelAnchor.TryGetSavedModelForPath(entry.modelPath, out Transform savedModel))
                    {
                        _builtEntryRoots[entry.label] = savedModel;
                    }
                    continue;
                }

                GameObject prefab = Resources.Load<GameObject>(entry.modelPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[Editable Scene Layout] Model not found in Resources: {entry.modelPath}", this);
                    continue;
                }

                string entryName = string.IsNullOrWhiteSpace(entry.label) ? prefab.name : entry.label;
                Transform entryRoot = new GameObject(entryName).transform;
                entryRoot.SetParent(_instancesRoot, false);
                entryRoot.localPosition = entry.position;
                entryRoot.localRotation = Quaternion.Euler(entry.rotation);
                // Decorative models are never stretched: choose the largest entered
                // axis so accidental Inspector edits preserve the model's proportions.
                entryRoot.localScale = SanitizeUniformScale(entry.scale);
                entryRoot.gameObject.layer = IgnoreRaycastLayer;

                Transform normalizationRoot = new GameObject(prefab.name + " Visual").transform;
                normalizationRoot.SetParent(entryRoot, false);
                normalizationRoot.gameObject.layer = IgnoreRaycastLayer;

                // Preserve the FBX root transform. Several supplied models use it for their axis conversion;
                // clearing it would turn an upright temple or grotto onto its side.
                GameObject visual = Instantiate(prefab, normalizationRoot, false);
                visual.name = prefab.name + " Model";

                MakeDecorationOnly(visual);
                ProceduralFactory.ApplyImportedBaseColorTexture(visual, entry.modelPath);
                if (normalizeImportedModels)
                {
                    NormalizeAndGround(normalizationRoot);
                }

                entryRoot.gameObject.SetActive(entry.visible);
                string builtKey = string.IsNullOrWhiteSpace(entry.label) ? entryName : entry.label;
                _builtEntryRoots[builtKey] = entryRoot;
            }
        }

        [ContextMenu("Rebuild Decorative Models")]
        public void RebuildDecorativeModels()
        {
            Transform arenaRoot = _arenaRoot != null ? _arenaRoot : transform.Find("Room Anchored Arena");
            if (arenaRoot == null)
            {
                Debug.LogWarning("[Editable Scene Layout] Enter Play mode or build a preview before rebuilding the decorative models.", this);
                return;
            }

            Build(arenaRoot);
        }

        [ContextMenu("Reset Decorative Model Entries")]
        public void ResetDecorativeModelEntries()
        {
            entries = CreateDefaultEntries();
            RebuildDecorativeModels();
        }

        [ContextMenu("Make Decorative Scales Uniform")]
        public void MakeDecorativeScalesUniform()
        {
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                SceneModelEntry entry = entries[i];
                if (entry != null)
                {
                    entry.scale = SanitizeUniformScale(entry.scale);
                }
            }

            RebuildDecorativeModels();
        }

        private void Reset()
        {
            entries = CreateDefaultEntries();
        }

        private static void MakeDecorationOnly(GameObject visual)
        {
            foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = IgnoreRaycastLayer;
            }

            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                DestroyComponent(collider);
            }

            foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                body.detectCollisions = false;
                body.isKinematic = true;
                DestroyComponent(body);
            }
        }

        private static void NormalizeAndGround(Transform visual)
        {
            if (!TryCalculateLocalRendererBounds(visual, out Bounds bounds))
            {
                return;
            }

            float longestAxis = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (longestAxis <= 0.0001f)
            {
                return;
            }

            float normalizationScale = 1f / longestAxis;
            visual.localScale = Vector3.one * normalizationScale;
            visual.localPosition = new Vector3(
                -bounds.center.x * normalizationScale,
                -bounds.min.y * normalizationScale,
                -bounds.center.z * normalizationScale);
        }

        private static bool TryCalculateLocalRendererBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = default;
            bool foundPoint = false;

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Bounds rendererBounds = renderers[rendererIndex].localBounds;
                Vector3 minimum = rendererBounds.min;
                Vector3 maximum = rendererBounds.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 rendererPoint = new(
                        (corner & 1) == 0 ? minimum.x : maximum.x,
                        (corner & 2) == 0 ? minimum.y : maximum.y,
                        (corner & 4) == 0 ? minimum.z : maximum.z);
                    Vector3 worldPoint = renderers[rendererIndex].transform.TransformPoint(rendererPoint);
                    Vector3 localPoint = root.InverseTransformPoint(worldPoint);
                    if (!foundPoint)
                    {
                        bounds = new Bounds(localPoint, Vector3.zero);
                        foundPoint = true;
                    }
                    else
                    {
                        bounds.Encapsulate(localPoint);
                    }
                }
            }

            return foundPoint;
        }

        private static Vector3 SanitizeUniformScale(Vector3 scale)
        {
            const float minimumMagnitude = 0.0001f;
            float uniformScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            return Vector3.one * Mathf.Max(minimumMagnitude, uniformScale);
        }

#if UNITY_EDITOR
        // The game builds its live arena in Play mode.  This separate, non-persistent
        // preview lets level dressing be inspected and adjusted from the Scene view
        // without changing the runtime hierarchy or serialized scene data.
        private const string EditorPreviewRootName = "Editor Scene Preview (temporary)";
        private const string EditorPreviewCameraName = "Editor Scene Preview Camera";

        private Transform _editorPreviewRoot;

        public bool HasEditorPreview => FindEditorPreviewRoot() != null;
        public Transform EditorPreviewRoot => FindEditorPreviewRoot();

        public void BuildEditorPreview()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Editable Scene Layout] Stop Play mode before building the edit-mode preview.", this);
                return;
            }

            ClearEditorPreview();

            _editorPreviewRoot = new GameObject(EditorPreviewRootName).transform;
            _editorPreviewRoot.SetParent(transform, false);
            GameDefinitions.ApplyAuthoredSceneTransform(_editorPreviewRoot);
            MarkEditorPreviewObject(_editorPreviewRoot.gameObject);

            BuildEditorPreviewFoundation(_editorPreviewRoot);
            BuildEditorPreviewLighting(_editorPreviewRoot);
            BuildEditorPreviewBoard(_editorPreviewRoot);

            // Reuse the same serialized entries and import normalization used at runtime.
            Build(_editorPreviewRoot);
            BuildEditorPreviewCamera(_editorPreviewRoot);

            MarkEditorPreviewHierarchy(_editorPreviewRoot);
        }

        private static void BuildEditorPreviewLighting(Transform root)
        {
            // Scene view has no runtime lights because the live arena creates
            // them in Play mode. A temporary soft key makes the saved layout
            // and imported grass/rock textures readable while editing.
            var lightObject = new GameObject("Preview Soft Key Light");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.localPosition = new Vector3(-1.8f, 3.4f, -2.4f);
            lightObject.transform.localRotation = Quaternion.Euler(46f, -28f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.90f, 0.76f);
            light.intensity = 0.72f;
            light.shadows = LightShadows.Soft;
            light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;
            MarkEditorPreviewObject(lightObject);
        }

        public void ClearEditorPreview()
        {
            Transform preview = FindEditorPreviewRoot();
            _editorPreviewRoot = null;
            if (preview != null)
            {
                DestroyPreviewMaterials(preview);
                DestroyImmediate(preview.gameObject);
            }

            if (_arenaRoot == preview)
            {
                _arenaRoot = null;
            }

            _instancesRoot = null;
            _builtEntryRoots.Clear();
        }

        private Transform FindEditorPreviewRoot()
        {
            if (_editorPreviewRoot != null)
            {
                return _editorPreviewRoot;
            }

            return transform.Find(EditorPreviewRootName);
        }

        private static void BuildEditorPreviewFoundation(Transform root)
        {
            // The screenshot-approved scene snapshot already owns the visible
            // water islands. Do not add a second board-sized Meadow/rock base in
            // edit mode, because it covers the saved bridge and lower buildings.
            if (SceneModelAnchor.HasSavedModelForPath("GameplayModels/Scene_S12_ReleasePond"))
            {
                return;
            }

            float width = GameDefinitions.GridColumns * GameDefinitions.CellSize;
            float depth = GameDefinitions.GridRows * GameDefinitions.CellSize;
            float halfWidth = width * 0.5f;
            float halfDepth = depth * 0.5f;
            float pondX = halfWidth * 0.62f;
            float pondZ = -halfDepth - 0.055f;
            float pondSurface = GameDefinitions.TableHeight + 0.105f;
            float middleSurface = GameDefinitions.CellSurfaceHeight(3);
            float upperSurface = GameDefinitions.CellSurfaceHeight(6);
            float middleCenterZ = (GameDefinitions.CellLocalPosition(0, 2).z + GameDefinitions.CellLocalPosition(0, 4).z) * 0.5f;
            float upperCenterZ = (GameDefinitions.CellLocalPosition(0, 5).z + GameDefinitions.CellLocalPosition(0, 7).z) * 0.5f;
            float tierDepth = GameDefinitions.CellSize * 3.08f;

            bool islandLoaded = ProceduralFactory.BuildImportedGroundPatch(
                "GameplayModels/Scene_S01_FloatingMountainBase",
                "Preview Saved Floating Island Underside",
                root,
                new Vector3(0f, 0f, 0.02f),
                width * 1.22f,
                depth * 1.18f,
                GameDefinitions.TableHeight - 0.085f,
                0f);

            ProceduralFactory.BuildImportedGroundPatch(
                "PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Ground_Mound_Large_01",
                "Preview Meadow Forest Grass Underlay",
                root,
                new Vector3(0f, -0.004f, 0.02f),
                width * 1.20f,
                depth * 1.16f,
                GameDefinitions.TableHeight - 0.092f,
                0f);

            var pondRoot = new GameObject("Preview Meadow Forest Right Lower Pond Water").transform;
            pondRoot.SetParent(root, false);
            MarkEditorPreviewObject(pondRoot.gameObject);
            Material pondShore = CreateEditorPreviewMaterial(new Color(0.08f, 0.20f, 0.16f), 0f, 0.32f);
            ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cylinder,
                "Preview Meadow Forest Pond Shore",
                pondRoot,
                new Vector3(pondX, pondSurface - 0.010f, pondZ),
                new Vector3(0.66f, 0.006f, 0.40f),
                pondShore);
            Material readableWater = CreateEditorPreviewMaterial(new Color(0.10f, 0.42f, 0.50f), 0.12f, 0.58f);
            ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cylinder,
                "Preview Meadow Forest Pond Readable Surface",
                pondRoot,
                new Vector3(pondX, pondSurface, pondZ),
                new Vector3(0.58f, 0.006f, 0.34f),
                readableWater);
            Material ripple = CreateEditorPreviewMaterial(new Color(0.34f, 0.72f, 0.74f), 0.08f, 0.64f);
            ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cylinder,
                "Preview Meadow Forest Pond Ripple",
                pondRoot,
                new Vector3(pondX, pondSurface + 0.008f, pondZ),
                new Vector3(0.28f, 0.003f, 0.12f),
                ripple);

            Material lower = CreateEditorPreviewMaterial(new Color(0.30f, 0.32f, 0.29f), 0.01f, 0.18f);
            Material middle = CreateEditorPreviewMaterial(new Color(0.44f, 0.45f, 0.39f), 0.01f, 0.22f);
            Material upper = CreateEditorPreviewMaterial(new Color(0.58f, 0.56f, 0.48f), 0.01f, 0.28f);
            ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cylinder,
                "Preview Lower Tier Support",
                root,
                new Vector3(0f, GameDefinitions.TableHeight - 0.11f, 0f),
                new Vector3(width * 0.68f, 0.07f, depth * 0.74f),
                lower);
            ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cylinder,
                "Preview Middle Tier Support",
                root,
                new Vector3(0f, middleSurface - 0.055f, middleCenterZ),
                new Vector3(width * 0.56f, 0.05f, tierDepth * 0.50f),
                middle);
            ProceduralFactory.VisualPrimitive(
                PrimitiveType.Cylinder,
                "Preview Upper Tier Support",
                root,
                new Vector3(0f, upperSurface - 0.055f, upperCenterZ),
                new Vector3(width * 0.54f, 0.05f, tierDepth * 0.50f),
                upper);

            if (!islandLoaded)
            {
                Material fallback = CreateEditorPreviewMaterial(new Color(0.33f, 0.40f, 0.30f), 0f, 0.25f);
                ProceduralFactory.VisualPrimitive(
                    PrimitiveType.Cylinder,
                    "Preview Island Import Pending",
                    root,
                    new Vector3(0f, GameDefinitions.TableHeight - 0.12f, 0f),
                    new Vector3(width * 0.88f, 0.025f, depth * 0.88f),
                    fallback);
            }

        }

        private static void BuildEditorPreviewBoard(Transform root)
        {
            GameObject placementPrefab = Resources.Load<GameObject>(ProceduralFactory.BoardPlacementVisualResourcePath);
            GameObject routePrefab = Resources.Load<GameObject>(ProceduralFactory.BoardRouteVisualResourcePath);
            if (placementPrefab == null || routePrefab == null)
            {
                Debug.LogWarning("[Editable Scene Layout] Board tile models are missing; preview will contain scenery only.");
                return;
            }

            HashSet<Vector2Int> pathCells = GameDefinitions.BuildPathCellSet();
            HashSet<Vector2Int> placementCells = GameDefinitions.BuildPlacementCellSet();
            for (int column = 0; column < GameDefinitions.GridColumns; column++)
            {
                for (int row = 0; row < GameDefinitions.GridRows; row++)
                {
                    Vector2Int coordinates = new(column, row);
                    bool isPath = pathCells.Contains(coordinates);
                    if (!isPath && !placementCells.Contains(coordinates))
                    {
                        continue;
                    }

                    Transform cell = new GameObject($"Preview {(isPath ? "Route" : "Placement")} Tile {column},{row}").transform;
                    cell.SetParent(root, false);
                    Vector3 local = GameDefinitions.CellLocalPosition(column, row);
                    float surface = GameDefinitions.CellSurfaceHeight(row);
                    cell.localPosition = new Vector3(local.x, surface + 0.001f, local.z);
                    GameObject prefab = isPath ? routePrefab : placementPrefab;
                    string resource = isPath
                        ? ProceduralFactory.BoardRouteVisualResourcePath
                        : ProceduralFactory.BoardPlacementVisualResourcePath;
                    ProceduralFactory.BuildBoardCellVisual(prefab, cell, resource);
                }
            }
        }

        private static void BuildEditorPreviewCamera(Transform root)
        {
            var cameraObject = new GameObject(EditorPreviewCameraName);
            cameraObject.transform.SetParent(root, false);
            cameraObject.transform.localPosition = new Vector3(0f, 2.75f, -3.25f);
            cameraObject.transform.localRotation = Quaternion.LookRotation(
                new Vector3(0f, 0.98f, 0.10f) - cameraObject.transform.localPosition,
                Vector3.up);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.055f, 0.065f, 1f);
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 30f * GameDefinitions.AuthoredSceneScale;
            camera.fieldOfView = 52f;
            camera.enabled = true;
            MarkEditorPreviewObject(cameraObject);
        }

        private static Material CreateEditorPreviewMaterial(Color color, float metallic, float smoothness)
        {
            Material material = ProceduralFactory.CreateMaterial(color, metallic, smoothness);
            material.hideFlags = HideFlags.DontSave;
            return material;
        }

        private static void MarkEditorPreviewObject(GameObject target)
        {
            target.hideFlags = HideFlags.DontSave;
        }

        private static void MarkEditorPreviewHierarchy(Transform root)
        {
            Transform[] objects = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < objects.Length; i++)
            {
                objects[i].gameObject.hideFlags = HideFlags.DontSave;
            }
        }

        private static void DestroyPreviewMaterials(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            var destroyed = new HashSet<Material>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                for (int j = 0; j < materials.Length; j++)
                {
                    Material material = materials[j];
                    if (material == null || (material.hideFlags & HideFlags.DontSave) == 0 || !destroyed.Add(material))
                    {
                        continue;
                    }

                    DestroyImmediate(material);
                }
            }
        }
#endif

        private static bool IsBoardTileResource(string modelPath)
        {
            return modelPath == ProceduralFactory.BoardPlacementVisualResourcePath ||
                   modelPath == ProceduralFactory.BoardRouteVisualResourcePath;
        }

        private void RemoveExistingInstances(Transform arenaRoot)
        {
            Transform existing = _instancesRoot != null ? _instancesRoot : arenaRoot.Find(ContainerName);
            _instancesRoot = null;
            if (existing == null)
            {
                return;
            }

            existing.gameObject.SetActive(false);
            DestroyObject(existing.gameObject);
        }

        private static void DestroyComponent(Component component)
        {
            if (Application.isPlaying)
            {
                Destroy(component);
            }
            else
            {
                DestroyImmediate(component);
            }
        }

        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static bool UsesLegacyDefaultEntries(IReadOnlyList<SceneModelEntry> layout)
        {
            if (layout == null || layout.Count != 16)
            {
                return false;
            }

            SceneModelEntry mountain = layout[0];
            SceneModelEntry grotto = layout[6];
            SceneModelEntry niche = layout[7];
            SceneModelEntry corridor = layout[8];
            SceneModelEntry temple = layout[9];
            return mountain != null && mountain.modelPath == "GameplayModels/Scene_S01_FloatingMountainBase" &&
                   Mathf.Approximately(mountain.position.y, -1.90f) &&
                   grotto != null && Mathf.Approximately(grotto.rotation.y, 90f) &&
                   niche != null && Mathf.Approximately(niche.rotation.y, 90f) &&
                   corridor != null && Mathf.Approximately(corridor.rotation.y, 90f) &&
                   temple != null && Mathf.Approximately(temple.rotation.y, 90f);
        }

        private static bool UsesFirstPassDecorativeEntries(IReadOnlyList<SceneModelEntry> layout)
        {
            if (layout == null || layout.Count != 19)
            {
                return false;
            }

            SceneModelEntry foundation = layout[0];
            SceneModelEntry grottoShelf = layout[7];
            SceneModelEntry templeShelf = layout[8];
            SceneModelEntry buddhaNiche = layout[10];
            SceneModelEntry corridor = layout[11];
            return foundation != null && !foundation.visible &&
                   foundation.modelPath == "GameplayModels/Scene_S01_FloatingMountainBase" &&
                   Mathf.Approximately(foundation.scale.x, 2.35f) &&
                   grottoShelf != null && grottoShelf.modelPath == "GameplayModels/Scene_S02_StonePath" &&
                   Mathf.Approximately(grottoShelf.scale.y, 0.10f) &&
                   templeShelf != null && templeShelf.modelPath == "GameplayModels/Scene_S02_StonePath" &&
                   Mathf.Approximately(templeShelf.scale.y, 0.10f) &&
                   buddhaNiche != null && Mathf.Approximately(buddhaNiche.position.z, 1.32f) &&
                   corridor != null && !Mathf.Approximately(corridor.scale.x, corridor.scale.y);
        }

        private static List<SceneModelEntry> CreateDefaultEntries()
        {
            return new List<SceneModelEntry>
            {
                new("Lower Water Arch Bridge", "GameplayModels/Scene_S06_ArchBridge",
                    new Vector3(-1.24f, 0.92f, -0.55f), new Vector3(0f, 90f, 0f), Vector3.one * 0.22f),
                new("Outer Enemy Spawn Gate", "GameplayModels/Scene_S07_MountainGate",
                    new Vector3(-1.47f, 0.92f, -0.55f), Vector3.zero, Vector3.one * 0.27f),
                new("Pilgrim Mountain Gate", "GameplayModels/Scene_S07_MountainGate",
                    new Vector3(-1.07f, 0.92f, -0.55f), Vector3.zero, Vector3.one * 0.27f),
                new("Lower Prayer Banners", "GameplayModels/Scene_S14_PrayerBanners",
                    new Vector3(-1.17f, 0.92f, -0.16f), new Vector3(0f, 14f, 0f), Vector3.one * 0.18f),
                new("Lower Ancient Cypress", "GameplayModels/Scene_S13_AncientCypress",
                    new Vector3(-1.20f, 0.92f, 0.34f), new Vector3(0f, 24f, 0f), Vector3.one * 0.27f),

                new("Left Mountain Grotto", "GameplayModels/Scene_S10_SmallGrotto",
                    new Vector3(-0.96f, 1.11f, 1.04f), new Vector3(0f, -90f, 0f), Vector3.one * 0.38f),
                new("Main Buddha Niche Backdrop", "GameplayModels/Scene_S11_BuddhaNiche",
                    new Vector3(-0.46f, 1.11f, 1.12f), new Vector3(0f, -90f, 0f), Vector3.one * 0.62f),
                new("Upper Temple Corridor", "GameplayModels/Scene_S09_Corridor",
                    new Vector3(0.12f, 1.11f, 1.04f), new Vector3(0f, -90f, 0f), Vector3.one * 0.36f),
                new("Upper Main Temple", "GameplayModels/Scene_S08_MainTemple",
                    new Vector3(0.72f, 1.11f, 1.10f), new Vector3(0f, -90f, 0f), Vector3.one * 0.52f),

                new("Upper Ancient Cypress", "GameplayModels/Scene_S13_AncientCypress",
                    new Vector3(1.16f, 1.11f, 0.88f), new Vector3(0f, -28f, 0f), Vector3.one * 0.28f),
                new("Middle Ancient Cypress", "GameplayModels/Scene_S13_AncientCypress",
                    new Vector3(1.17f, 1.015f, 0.02f), new Vector3(0f, 24f, 0f), Vector3.one * 0.23f),
                new("Middle Prayer Banners", "GameplayModels/Scene_S14_PrayerBanners",
                    new Vector3(1.14f, 1.015f, 0.38f), new Vector3(0f, -16f, 0f), Vector3.one * 0.18f),
                new("Left Restoration Lantern", "GameplayModels/Scene_S15_StoneLantern",
                    new Vector3(-0.92f, 1.11f, 0.86f), new Vector3(0f, 180f, 0f), Vector3.one * 0.15f),
                new("Right Restoration Lantern", "GameplayModels/Scene_S15_StoneLantern",
                    new Vector3(1.02f, 1.11f, 0.86f), new Vector3(0f, 180f, 0f), Vector3.one * 0.15f),
                new("Temple Incense Brazier", "GameplayModels/Scene_S16_IncenseBrazier",
                    new Vector3(0.49f, 1.11f, 0.90f), new Vector3(0f, 180f, 0f), Vector3.one * 0.15f)
            };
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(EditableSceneModelLayout.SceneModelEntry))]
    internal sealed class SceneModelEntryDrawer : PropertyDrawer
    {
        private const float DeleteButtonWidth = 48f;
        private const float VisibilityToggleWidth = 52f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int lineCount = property.isExpanded ? 6 : 1;
            return lineCount * EditorGUIUtility.singleLineHeight +
                   (lineCount - 1) * EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty visible = property.FindPropertyRelative("visible");
            SerializedProperty entryLabel = property.FindPropertyRelative("label");
            SerializedProperty modelPath = property.FindPropertyRelative("modelPath");
            SerializedProperty localPosition = property.FindPropertyRelative("position");
            SerializedProperty localRotation = property.FindPropertyRelative("rotation");
            SerializedProperty scale = property.FindPropertyRelative("scale");

            Rect line = NextLine(ref position);
            Rect foldoutRect = line;
            foldoutRect.width -= VisibilityToggleWidth + DeleteButtonWidth + 8f;
            string displayLabel = string.IsNullOrWhiteSpace(entryLabel.stringValue)
                ? $"Scene Model {ExtractArrayIndex(property.propertyPath) + 1}"
                : entryLabel.stringValue;
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, displayLabel, true);

            Rect visibilityRect = line;
            visibilityRect.x = foldoutRect.xMax + 4f;
            visibilityRect.width = VisibilityToggleWidth;
            visible.boolValue = EditorGUI.ToggleLeft(visibilityRect, "Show", visible.boolValue);

            Rect deleteRect = line;
            deleteRect.x = visibilityRect.xMax + 4f;
            deleteRect.width = DeleteButtonWidth;
            if (GUI.Button(deleteRect, "Delete"))
            {
                DeleteEntry(property);
                EditorGUI.EndProperty();
                GUIUtility.ExitGUI();
                return;
            }

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUI.PropertyField(NextLine(ref position), entryLabel, new GUIContent("Name"));
                EditorGUI.PropertyField(NextLine(ref position), modelPath, new GUIContent("Resource Path"));
                EditorGUI.PropertyField(NextLine(ref position), localPosition, new GUIContent("Position"));
                EditorGUI.PropertyField(NextLine(ref position), localRotation, new GUIContent("Rotation"));

                float uniformScale = Mathf.Max(
                    Mathf.Abs(scale.vector3Value.x),
                    Mathf.Max(Mathf.Abs(scale.vector3Value.y), Mathf.Abs(scale.vector3Value.z)));
                EditorGUI.BeginChangeCheck();
                uniformScale = EditorGUI.FloatField(NextLine(ref position), new GUIContent("Uniform Scale"), uniformScale);
                if (EditorGUI.EndChangeCheck())
                {
                    scale.vector3Value = Vector3.one * Mathf.Max(0.0001f, Mathf.Abs(uniformScale));
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private static Rect NextLine(ref Rect remaining)
        {
            Rect result = new(
                remaining.x,
                remaining.y,
                remaining.width,
                EditorGUIUtility.singleLineHeight);
            remaining.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            return result;
        }

        private static void DeleteEntry(SerializedProperty property)
        {
            string propertyPath = property.propertyPath;
            const string arrayMarker = ".Array.data[";
            int markerIndex = propertyPath.LastIndexOf(arrayMarker, StringComparison.Ordinal);
            int elementIndex = ExtractArrayIndex(propertyPath);
            if (markerIndex < 0 || elementIndex < 0)
            {
                return;
            }

            string arrayPath = propertyPath.Substring(0, markerIndex);
            SerializedObject owner = property.serializedObject;
            SerializedProperty array = owner.FindProperty(arrayPath);
            if (array == null || !array.isArray || elementIndex >= array.arraySize)
            {
                return;
            }

            array.DeleteArrayElementAtIndex(elementIndex);
            owner.ApplyModifiedProperties();
        }

        private static int ExtractArrayIndex(string propertyPath)
        {
            const string marker = ".Array.data[";
            int markerIndex = propertyPath.LastIndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return -1;
            }

            int numberStart = markerIndex + marker.Length;
            int numberEnd = propertyPath.IndexOf(']', numberStart);
            return numberEnd > numberStart &&
                   int.TryParse(propertyPath.Substring(numberStart, numberEnd - numberStart), out int index)
                ? index
                : -1;
        }
    }
#endif
}
