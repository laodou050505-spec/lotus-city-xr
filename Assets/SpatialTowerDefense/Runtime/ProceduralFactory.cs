using UnityEngine;
using UnityEngine.Rendering;

namespace PicoTowerDefense
{
    public static class ProceduralFactory
    {
        private const int IgnoreRaycastLayer = 2;
        private const string RuntimeGlyphs = " ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789/:-";
        // The authored board uses white stone tiles for the enemy route and
        // green moss tiles exclusively for tower placement. Keep these
        // resources reserved for gameplay so the free-form scenery tool cannot
        // duplicate them in the decorative layout.
        public const string BoardPlacementVisualResourcePath = "GameplayModels/Scene_S17_GrassPlatform";
        public const string BoardRouteVisualResourcePath = "GameplayModels/Scene_S02_StonePath";
        public const string BoardCellVisualResourcePath = BoardPlacementVisualResourcePath;
        public const string CoinVisualResourcePath = "GameplayModels/UI_AncientCoin";
        public const float PlacementTileTopOffset = 0.070f;
        public const float RouteTileTopOffset = 0.080f;

        private readonly struct ImportedVisualSpec
        {
            public readonly string ResourcePath;
            public readonly float MaxWidth;
            public readonly float MaxHeight;
            public readonly float Yaw;

            public ImportedVisualSpec(string resourcePath, float maxWidth, float maxHeight, float yaw = 0f)
            {
                ResourcePath = resourcePath;
                MaxWidth = maxWidth;
                MaxHeight = maxHeight;
                Yaw = yaw;
            }
        }

        public static Material CreateMaterial(Color color, float metallic = 0f, float smoothness = 0.25f)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader)
            {
                color = color
            };
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            return material;
        }

        public static Material CreateTransparentMaterial(Color color)
        {
            Material material = CreateMaterial(color);
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            return material;
        }

        public static Material CreateUnlitMaterial(Color color)
        {
            Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader)
            {
                color = color
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        public static GameObject VisualPrimitive(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Quaternion? localRotation = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = IgnoreRaycastLayer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = go.GetComponent<Collider>();
            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }

            return go;
        }

        public static TextMesh WorldText(
            string name,
            string text,
            Transform parent,
            Vector3 localPosition,
            float characterSize,
            Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter,
            bool overlay = false)
        {
            var go = new GameObject(name);
            go.layer = IgnoreRaycastLayer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var mesh = go.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                mesh.font = font;
            }
            mesh.text = text;
            mesh.anchor = anchor;
            mesh.alignment = TextAlignment.Center;
            // Generate glyphs at 4x the previous atlas resolution, then counter-scale
            // the mesh so its world-space size stays unchanged in both eyes.
            mesh.characterSize = characterSize * 0.125f;
            mesh.fontSize = 256;
            mesh.fontStyle = FontStyle.Bold;
            mesh.color = color;
            if (font != null)
            {
                font.RequestCharactersInTexture(RuntimeGlyphs, mesh.fontSize, mesh.fontStyle);
                if (font.material.mainTexture != null)
                {
                    font.material.mainTexture.filterMode = FilterMode.Bilinear;
                    font.material.mainTexture.anisoLevel = 16;
                }
                MeshRenderer renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    Shader textShader = Resources.Load<Shader>(overlay
                        ? "UI/WorldTextOverlay"
                        : "UI/WorldTextTransparent");
                    textShader ??= Shader.Find(overlay
                        ? "SpatialTowerDefense/WorldTextOverlay"
                        : "SpatialTowerDefense/WorldTextTransparent");
                    if (textShader != null)
                    {
                        var textMaterial = new Material(textShader)
                        {
                            mainTexture = font.material.mainTexture,
                            color = Color.white
                        };
                        textMaterial.renderQueue = overlay ? 4100 : 3000;
                        renderer.sharedMaterial = textMaterial;
                    }
                    else
                    {
                        renderer.sharedMaterial = font.material;
                    }
                    renderer.sortingOrder = overlay ? 100 : 0;
                }
            }
            return mesh;
        }

        public static Transform BuildTowerVisual(TowerKind kind, Transform parent, int level = 1)
        {
            int clampedLevel = Mathf.Clamp(level, 1, GameDefinitions.MaxTowerLevel);
            TowerDefinition definition = GameDefinitions.Tower(kind, clampedLevel);
            var root = new GameObject($"{definition.Name} Tower Visual").transform;
            root.gameObject.layer = IgnoreRaycastLayer;
            root.SetParent(parent, false);

            if (TryBuildImportedTowerVisual(kind, clampedLevel, root, out Transform importedAimPivot))
            {
                return importedAimPivot;
            }

            root.localScale = Vector3.one * GameDefinitions.SpatialScale * (1f + (clampedLevel - 1) * 0.10f);

            Material stone = CreateMaterial(new Color(0.70f, 0.68f, 0.60f), 0.03f, 0.45f);
            Material oldWood = CreateMaterial(new Color(0.20f, 0.13f, 0.09f), 0.05f, 0.28f);
            Material jadeTile = CreateMaterial(new Color(0.12f, 0.34f, 0.30f), 0.12f, 0.35f);
            Material accent = CreateMaterial(definition.Color, 0.28f, 0.62f);
            Material gold = CreateMaterial(new Color(0.93f, 0.70f, 0.25f), 0.55f, 0.68f);
            VisualPrimitive(PrimitiveType.Cylinder, "Stone Foundation", root, new Vector3(0f, 0.022f, 0f), new Vector3(0.072f, 0.022f, 0.072f), stone);
            for (int marker = 0; marker < clampedLevel; marker++)
            {
                float angle = marker / (float)clampedLevel * Mathf.PI * 2f;
                VisualPrimitive(
                    PrimitiveType.Sphere,
                    $"Level Marker {marker + 1}",
                    root,
                    new Vector3(Mathf.Cos(angle) * 0.055f, 0.052f, Mathf.Sin(angle) * 0.055f),
                    Vector3.one * 0.016f,
                    accent);
            }

            switch (kind)
            {
                case TowerKind.Arrow:
                {
                    Transform turret = new GameObject("Turret").transform;
                    turret.gameObject.layer = IgnoreRaycastLayer;
                    turret.SetParent(root, false);
                    turret.localPosition = new Vector3(0f, 0.10f, 0f);
                    if (clampedLevel == 1)
                    {
                        VisualPrimitive(PrimitiveType.Cube, "Woodfish Pavilion Post", turret, Vector3.zero, new Vector3(0.085f, 0.09f, 0.085f), oldWood);
                        VisualPrimitive(PrimitiveType.Sphere, "Floating Woodfish", turret, new Vector3(0f, 0.07f, 0f), new Vector3(0.075f, 0.045f, 0.052f), accent);
                        VisualPrimitive(PrimitiveType.Cube, "Mallet", turret, new Vector3(0f, 0.10f, 0.055f), new Vector3(0.012f, 0.012f, 0.095f), gold, Quaternion.Euler(28f, 0f, 0f));
                    }
                    else
                    {
                        VisualPrimitive(PrimitiveType.Cube, "Bell Drum Tower", turret, Vector3.zero, new Vector3(0.10f, 0.12f, 0.10f), oldWood);
                        VisualPrimitive(PrimitiveType.Cube, "Green Tile Roof", turret, new Vector3(0f, 0.12f, 0f), new Vector3(0.15f, 0.03f, 0.14f), jadeTile, Quaternion.Euler(0f, 45f, 0f));
                        VisualPrimitive(PrimitiveType.Cylinder, clampedLevel == 2 ? "Morning Bell" : "Evening Drum", turret, new Vector3(0f, 0.07f, 0.055f), new Vector3(0.052f, 0.042f, 0.052f), clampedLevel == 2 ? gold : accent, Quaternion.Euler(90f, 0f, 0f));
                        if (clampedLevel == 3)
                        {
                            VisualPrimitive(PrimitiveType.Cube, "Prayer Banner", turret, new Vector3(0.085f, 0.10f, 0f), new Vector3(0.014f, 0.11f, 0.07f), CreateMaterial(new Color(0.48f, 0.12f, 0.08f)));
                        }
                    }
                    return turret;
                }
                case TowerKind.Cannon:
                {
                    Transform turret = new GameObject("Turret").transform;
                    turret.gameObject.layer = IgnoreRaycastLayer;
                    turret.SetParent(root, false);
                    turret.localPosition = new Vector3(0f, 0.085f, 0f);
                    if (clampedLevel == 1)
                    {
                        VisualPrimitive(PrimitiveType.Cylinder, "Sutra Banner Stone", turret, Vector3.zero, new Vector3(0.042f, 0.10f, 0.042f), stone);
                        VisualPrimitive(PrimitiveType.Cube, "Sutra Banner", turret, new Vector3(0f, 0.11f, 0.025f), new Vector3(0.075f, 0.08f, 0.012f), accent);
                    }
                    else if (clampedLevel == 2)
                    {
                        VisualPrimitive(PrimitiveType.Cylinder, "Dharma Wheel Stone", turret, Vector3.zero, new Vector3(0.07f, 0.06f, 0.07f), stone);
                        VisualPrimitive(PrimitiveType.Cylinder, "Dharma Wheel", turret, new Vector3(0f, 0.10f, 0f), new Vector3(0.085f, 0.010f, 0.085f), gold);
                        for (int spoke = 0; spoke < 4; spoke++)
                        {
                            VisualPrimitive(PrimitiveType.Cube, $"Wheel Spoke {spoke + 1}", turret, new Vector3(0f, 0.115f, 0f), new Vector3(0.012f, 0.012f, 0.14f), gold, Quaternion.Euler(0f, spoke * 45f, 0f));
                        }
                    }
                    else
                    {
                        VisualPrimitive(PrimitiveType.Cube, "Sutra Pagoda Base", turret, Vector3.zero, new Vector3(0.13f, 0.10f, 0.13f), stone);
                        VisualPrimitive(PrimitiveType.Cube, "Sutra Pagoda Body", turret, new Vector3(0f, 0.10f, 0f), new Vector3(0.085f, 0.15f, 0.085f), stone);
                        VisualPrimitive(PrimitiveType.Cube, "Pagoda Roof One", turret, new Vector3(0f, 0.15f, 0f), new Vector3(0.15f, 0.018f, 0.14f), jadeTile, Quaternion.Euler(0f, 45f, 0f));
                        VisualPrimitive(PrimitiveType.Cube, "Pagoda Roof Two", turret, new Vector3(0f, 0.22f, 0f), new Vector3(0.11f, 0.016f, 0.10f), jadeTile, Quaternion.Euler(0f, 45f, 0f));
                        VisualPrimitive(PrimitiveType.Sphere, "Pagoda Heart Lamp", turret, new Vector3(0f, 0.25f, 0f), Vector3.one * 0.032f, gold);
                    }
                    return turret;
                }
                default:
                {
                    Transform turret = new GameObject("Turret").transform;
                    turret.gameObject.layer = IgnoreRaycastLayer;
                    turret.SetParent(root, false);
                    turret.localPosition = new Vector3(0f, 0.08f, 0f);
                    if (clampedLevel == 1)
                    {
                        VisualPrimitive(PrimitiveType.Cylinder, "Lotus Stone", turret, Vector3.zero, new Vector3(0.07f, 0.022f, 0.07f), stone);
                        for (int petal = 0; petal < 6; petal++)
                        {
                            float angle = petal * Mathf.PI / 3f;
                            VisualPrimitive(PrimitiveType.Sphere, $"Lotus Petal {petal + 1}", turret, new Vector3(Mathf.Cos(angle) * 0.046f, 0.035f, Mathf.Sin(angle) * 0.046f), new Vector3(0.05f, 0.022f, 0.035f), accent);
                        }
                        VisualPrimitive(PrimitiveType.Sphere, "Lotus Flame", turret, new Vector3(0f, 0.075f, 0f), Vector3.one * 0.038f, gold);
                    }
                    else if (clampedLevel == 2)
                    {
                        VisualPrimitive(PrimitiveType.Cylinder, "Bronze Censer", turret, new Vector3(0f, 0.045f, 0f), new Vector3(0.07f, 0.065f, 0.07f), oldWood);
                        VisualPrimitive(PrimitiveType.Sphere, "Incense Smoke", turret, new Vector3(0f, 0.15f, 0f), Vector3.one * 0.075f, CreateTransparentMaterial(new Color(0.72f, 0.92f, 0.88f, 0.38f)));
                    }
                    else
                    {
                        VisualPrimitive(PrimitiveType.Cylinder, "Bodhi Tree Altar", turret, Vector3.zero, new Vector3(0.095f, 0.04f, 0.095f), stone);
                        VisualPrimitive(PrimitiveType.Cylinder, "Bodhi Trunk", turret, new Vector3(0f, 0.11f, 0f), new Vector3(0.034f, 0.12f, 0.034f), oldWood);
                        for (int branch = 0; branch < 5; branch++)
                        {
                            float angle = branch * Mathf.PI * 0.4f;
                            VisualPrimitive(PrimitiveType.Sphere, $"Bodhi Canopy {branch + 1}", turret, new Vector3(Mathf.Cos(angle) * 0.07f, 0.22f, Mathf.Sin(angle) * 0.07f), Vector3.one * 0.10f, accent);
                        }
                    }
                    return turret;
                }
            }
        }

        public static GameObject BuildEnemyVisual(EnemyKind kind, Transform parent)
        {
            EnemyDefinition definition = GameDefinitions.Enemy(kind);
            var root = new GameObject($"{definition.Name} Mind Shadow");
            root.layer = IgnoreRaycastLayer;
            root.transform.SetParent(parent, false);

            if (TryBuildImportedEnemyVisual(kind, definition, root.transform))
            {
                return root;
            }

            Material bodyMaterial = CreateMaterial(definition.Color, kind == EnemyKind.Tank ? 0.4f : 0.08f, 0.35f);
            Material smokeMaterial = CreateTransparentMaterial(new Color(0.07f, 0.08f, 0.12f, 0.44f));

            PrimitiveType bodyType = kind switch
            {
                EnemyKind.Tank => PrimitiveType.Cube,
                EnemyKind.Runner => PrimitiveType.Capsule,
                EnemyKind.Support => PrimitiveType.Cylinder,
                _ => PrimitiveType.Sphere
            };
            Vector3 scale = kind switch
            {
                EnemyKind.Tank => new Vector3(0.11f, 0.10f, 0.12f),
                EnemyKind.Runner => new Vector3(0.055f, 0.055f, 0.055f),
                EnemyKind.Splitter => new Vector3(0.095f, 0.075f, 0.075f),
                EnemyKind.Support => new Vector3(0.07f, 0.075f, 0.07f),
                _ => Vector3.one * 0.085f
            };
            scale *= GameDefinitions.SpatialScale;
            VisualPrimitive(bodyType, "Body", root.transform, new Vector3(0f, definition.Radius, 0f), scale, bodyMaterial);
            VisualPrimitive(PrimitiveType.Sphere, "Dark Mist", root.transform, new Vector3(0f, definition.Radius, 0f), scale * 1.35f, smokeMaterial);

            if (kind == EnemyKind.Shield)
            {
                Color shellColor = new Color(0.25f, 1f, 0.94f, 0.26f);
                VisualPrimitive(
                    PrimitiveType.Sphere,
                    "Broken Mirror Barrier",
                    root.transform,
                    new Vector3(0f, definition.Radius, 0f),
                    Vector3.one * (0.13f * GameDefinitions.SpatialScale),
                    CreateTransparentMaterial(shellColor));
            }
            CombatVisualEffects.AddEnemySkillIndicator(root, kind, definition.Radius);

            Material gold = CreateMaterial(new Color(0.90f, 0.68f, 0.22f), 0.35f, 0.72f);
            VisualPrimitive(PrimitiveType.Sphere, "Inner Ember", root.transform, new Vector3(0f, definition.Radius * 1.45f, -definition.Radius * 0.75f), Vector3.one * (definition.Radius * 0.26f), gold);
            return root;
        }

        public static Renderer[] BuildBoardCellVisual(GameObject prefab, Transform cellRoot)
        {
            return BuildBoardCellVisual(prefab, cellRoot, BoardCellVisualResourcePath);
        }

        public static Renderer[] BuildBoardCellVisual(GameObject prefab, Transform cellRoot, string resourcePath)
        {
            if (prefab == null)
            {
                throw new System.ArgumentNullException(nameof(prefab));
            }
            if (cellRoot == null)
            {
                throw new System.ArgumentNullException(nameof(cellRoot));
            }

            string visualLabel = resourcePath == BoardRouteVisualResourcePath ? "White Stone Enemy Route Visual" : "Green Tower Placement Visual";
            var modelMount = new GameObject(visualLabel).transform;
            modelMount.gameObject.layer = IgnoreRaycastLayer;
            modelMount.SetParent(cellRoot, false);

            GameObject instance = Object.Instantiate(prefab, modelMount, false);
            instance.name = prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = Vector3.one;
            PrepareImportedHierarchy(instance, resourcePath);

            if (!FitImportedBoardTile(instance.transform, GameDefinitions.CellSize * 0.955f))
            {
                DestroyObject(modelMount.gameObject);
                throw new System.InvalidOperationException("Board tile model has invalid renderer bounds.");
            }

            return instance.GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>
        /// Places an imported grass/rock chunk with a uniform scale and its top
        /// surface aligned to <paramref name="topHeight"/>. This is used for the
        /// three visible island terraces; it deliberately has no gameplay collider.
        /// </summary>
        public static bool BuildImportedGroundPatch(
            string resourcePath,
            string name,
            Transform parent,
            Vector3 localCenter,
            float maxWidth,
            float maxDepth,
            float topHeight,
            float yaw = 0f)
        {
            if (parent == null)
            {
                return false;
            }

            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                return false;
            }

            var patchRoot = new GameObject(name).transform;
            patchRoot.gameObject.layer = IgnoreRaycastLayer;
            patchRoot.SetParent(parent, false);
            patchRoot.localPosition = localCenter;

            var mount = new GameObject(prefab.name + " Ground Visual").transform;
            mount.gameObject.layer = IgnoreRaycastLayer;
            mount.SetParent(patchRoot, false);
            GameObject instance = Object.Instantiate(prefab, mount, false);
            instance.name = prefab.name + " Ground Model";
            PrepareImportedHierarchy(instance, resourcePath);

            Quaternion sourceRotation = instance.transform.localRotation;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * sourceRotation;
            if (!TryCalculateParentRendererBounds(instance.transform, out Bounds bounds) ||
                bounds.size.y <= 0.00001f || bounds.size.x <= 0.00001f || bounds.size.z <= 0.00001f)
            {
                DestroyObject(patchRoot.gameObject);
                return false;
            }

            float uniformScale = Mathf.Min(maxWidth / bounds.size.x, maxDepth / bounds.size.z);
            if (float.IsNaN(uniformScale) || float.IsInfinity(uniformScale) || uniformScale <= 0f)
            {
                DestroyObject(patchRoot.gameObject);
                return false;
            }

            instance.transform.localScale = Vector3.one * uniformScale;
            // Bounds are in the mount's local space before scale. Align the upper
            // face instead of the minimum, so the mesh cannot float above a tile.
            instance.transform.localPosition = new Vector3(
                -bounds.center.x * uniformScale,
                topHeight - bounds.max.y * uniformScale,
                -bounds.center.z * uniformScale);
            return true;
        }

        public static Transform BuildImportedUiVisual(
            string resourcePath,
            string name,
            Transform parent,
            Vector3 localPosition,
            float maxWidth,
            float maxHeight,
            float yaw = 0f,
            float pitch = 0f,
            float roll = 0f)
        {
            if (parent == null)
            {
                return null;
            }

            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                return null;
            }

            var visualRoot = new GameObject(name).transform;
            visualRoot.gameObject.layer = IgnoreRaycastLayer;
            visualRoot.SetParent(parent, false);
            visualRoot.localPosition = localPosition;

            GameObject instance = Object.Instantiate(prefab, visualRoot, false);
            instance.name = prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = Vector3.one;
            PrepareImportedHierarchy(instance, resourcePath);

            Quaternion sourceRotation = Quaternion.Euler(pitch, 0f, roll) * instance.transform.localRotation;
            if (!FitImportedModel(instance.transform, maxWidth, maxHeight, yaw, sourceRotation))
            {
                DestroyObject(visualRoot.gameObject);
                return null;
            }

            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = ShadowCastingMode.Off;
                renderers[i].receiveShadows = false;
                renderers[i].lightProbeUsage = LightProbeUsage.Off;
                renderers[i].reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderers[i].motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }

            return visualRoot;
        }

        public static bool ValidateImportedGameplayModels(out string error)
        {
            if (!ValidateImportedModel(BoardCellVisualResourcePath, out error))
            {
                return false;
            }
            if (BoardRouteVisualResourcePath != BoardCellVisualResourcePath &&
                !ValidateImportedModel(BoardRouteVisualResourcePath, out error))
            {
                return false;
            }
            if (!ValidateImportedModel(CoinVisualResourcePath, out error))
            {
                return false;
            }

            foreach (TowerKind kind in System.Enum.GetValues(typeof(TowerKind)))
            {
                for (int level = 1; level <= GameDefinitions.MaxTowerLevel; level++)
                {
                    ImportedVisualSpec spec = TowerVisualSpec(kind, level);
                    if (!ValidateImportedModel(spec.ResourcePath, out error))
                    {
                        return false;
                    }
                }
            }

            foreach (EnemyKind kind in System.Enum.GetValues(typeof(EnemyKind)))
            {
                ImportedVisualSpec spec = EnemyVisualSpec(kind, GameDefinitions.Enemy(kind));
                if (!ValidateImportedModel(spec.ResourcePath, out error))
                {
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryBuildImportedTowerVisual(TowerKind kind, int level, Transform root, out Transform aimPivot)
        {
            ImportedVisualSpec spec = TowerVisualSpec(kind, level);
            GameObject prefab = Resources.Load<GameObject>(spec.ResourcePath);
            if (prefab == null)
            {
                aimPivot = null;
                return false;
            }

            root.localScale = Vector3.one;

            // Keep the imported artwork and the muzzle on one rotation pivot.
            // TowerAgent turns this pivot toward the route target; previously
            // the artwork was a sibling of the pivot, so only the invisible
            // muzzle rotated while the visible tower stayed fixed.
            aimPivot = new GameObject("Aim Pivot").transform;
            aimPivot.gameObject.layer = IgnoreRaycastLayer;
            aimPivot.SetParent(root, false);
            aimPivot.localPosition = Vector3.zero;

            var modelMount = new GameObject("Imported Gameplay Model").transform;
            modelMount.gameObject.layer = IgnoreRaycastLayer;
            modelMount.SetParent(aimPivot, false);
            GameObject instance = Object.Instantiate(prefab, modelMount, false);
            instance.name = prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = Vector3.one;
            PrepareImportedHierarchy(instance, spec.ResourcePath);

            Quaternion sourceRotation = instance.transform.localRotation;
            if (!FitImportedModel(instance.transform, spec.MaxWidth, spec.MaxHeight, spec.Yaw, sourceRotation))
            {
                DestroyObject(aimPivot.gameObject);
                aimPivot = null;
                return false;
            }

            var muzzlePoint = new GameObject("Muzzle Point").transform;
            muzzlePoint.gameObject.layer = IgnoreRaycastLayer;
            muzzlePoint.SetParent(aimPivot, false);
            muzzlePoint.localPosition = new Vector3(0f, spec.MaxHeight * 0.62f, 0f);
            return true;
        }

        private static bool TryBuildImportedEnemyVisual(EnemyKind kind, EnemyDefinition definition, Transform root)
        {
            ImportedVisualSpec spec = EnemyVisualSpec(kind, definition);
            GameObject prefab = Resources.Load<GameObject>(spec.ResourcePath);
            if (prefab == null)
            {
                return false;
            }

            var modelMount = new GameObject("Imported Gameplay Model").transform;
            modelMount.gameObject.layer = IgnoreRaycastLayer;
            modelMount.SetParent(root, false);
            GameObject instance = Object.Instantiate(prefab, modelMount, false);
            instance.name = prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = Vector3.one;
            PrepareImportedHierarchy(instance, spec.ResourcePath);

            Quaternion sourceRotation = instance.transform.localRotation;
            if (!FitImportedModel(instance.transform, spec.MaxWidth, spec.MaxHeight, spec.Yaw, sourceRotation))
            {
                DestroyObject(modelMount.gameObject);
                return false;
            }

            if (kind == EnemyKind.Shield)
            {
                BuildOpaqueShieldIndicator(root, spec.MaxWidth, spec.MaxHeight);
            }

            // Imported Tripo models are the primary enemy artwork in the saved
            // scene. Attach the small, animated skill cue after fitting the mesh
            // so every authored creature keeps its original transform.
            CombatVisualEffects.AddEnemySkillIndicator(root.gameObject, kind, definition.Radius);

            return true;
        }

        private static ImportedVisualSpec TowerVisualSpec(TowerKind kind, int level)
        {
            int clampedLevel = Mathf.Clamp(level, 1, GameDefinitions.MaxTowerLevel);
            float maxWidth = clampedLevel switch { 1 => 0.17f, 2 => 0.19f, _ => 0.21f };
            float maxHeight = clampedLevel switch { 1 => 0.19f, 2 => 0.25f, _ => 0.31f };
            string resourcePath = kind switch
            {
                TowerKind.Arrow => $"GameplayModels/Tower_A{clampedLevel}_MorningBell",
                TowerKind.Frost => $"GameplayModels/Tower_B{clampedLevel}_LotusLamp",
                TowerKind.Cannon => clampedLevel switch
                {
                    1 => "GameplayModels/Tower_C1_SutraPillar",
                    2 => "GameplayModels/Tower_C2_SutraWheel",
                    _ => "GameplayModels/Tower_C3_SutraGuardianGate"
                },
                _ => string.Empty
            };
            return new ImportedVisualSpec(resourcePath, maxWidth, maxHeight);
        }

        private static ImportedVisualSpec EnemyVisualSpec(EnemyKind kind, EnemyDefinition definition)
        {
            string resourcePath = kind switch
            {
                EnemyKind.Grunt => "GameplayModels/Enemy_E01_RestlessDust",
                EnemyKind.Runner => "GameplayModels/Enemy_E02_GraspingBurden",
                EnemyKind.Splitter => "GameplayModels/Enemy_E03_AngerCrag",
                EnemyKind.Support => "GameplayModels/Enemy_E04_DelusionCloud",
                EnemyKind.Shield => "GameplayModels/Enemy_E05_DoubtCarapace",
                EnemyKind.Tank => "GameplayModels/Enemy_E06_IgnoranceBeast",
                _ => string.Empty
            };
            // The supplied Tripo creatures use a side axis as artwork-forward.
            // Correct it once here so EnemyAgent's +Z route heading makes the
            // visible face look toward the next waypoint, never at the camera.
            const float artworkFacingYaw = 90f;
            return new ImportedVisualSpec(
                resourcePath,
                definition.Radius * 2.2f,
                definition.Radius * 2.1f,
                artworkFacingYaw);
        }

        private static bool ValidateImportedModel(string resourcePath, out string error)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                error = $"Imported gameplay model is missing or not ready: Resources/{resourcePath}";
                return false;
            }

            if (prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                error = $"Imported gameplay model has no renderable mesh: Resources/{resourcePath}";
                return false;
            }

            if (Resources.LoadAll<Texture2D>(resourcePath + ".fbm").Length == 0)
            {
                error = $"Imported gameplay model base-color texture is missing: Resources/{resourcePath}.fbm";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static void ApplyImportedBaseColorTexture(GameObject instance, string resourcePath)
        {
            Texture2D[] textures = Resources.LoadAll<Texture2D>(resourcePath + ".fbm");
            if (textures.Length == 0)
            {
                return;
            }

            System.Array.Sort(textures, (left, right) => string.Compare(left.name, right.name, System.StringComparison.OrdinalIgnoreCase));
            Texture2D fallbackBaseColor = FindBaseColorTexture(textures);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    materials = new[] { CreateFallbackImportedMaterial(fallbackBaseColor) };
                    renderer.sharedMaterials = materials;
                }

                Material[] repairedMaterials = new Material[materials.Length];
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material source = materials[materialIndex];
                    Material repaired = source != null ? new Material(source) : CreateFallbackImportedMaterial(fallbackBaseColor);
                    repaired.name = (source != null ? source.name : instance.name) + " (Recovered Material)";
                    Texture2D baseColor = FindMatchingBaseColor(textures, source, fallbackBaseColor);
                    ApplyTextureToMaterial(repaired, baseColor);
                    repairedMaterials[materialIndex] = repaired;
                }

                renderer.sharedMaterials = repairedMaterials;
            }
        }

        private static Texture2D FindBaseColorTexture(Texture2D[] textures)
        {
            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] != null && textures[i].name.IndexOf("basecolor", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return textures[i];
                }
            }

            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] != null && (textures[i].name.IndexOf("albedo", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    textures[i].name.IndexOf("diffuse", System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return textures[i];
                }
            }

            return textures.Length > 0 ? textures[0] : null;
        }

        private static Texture2D FindMatchingBaseColor(Texture2D[] textures, Material source, Texture2D fallback)
        {
            if (source != null)
            {
                Texture existing = null;
                if (source.HasProperty("_MainTex")) existing = source.GetTexture("_MainTex");
                if (existing == null && source.HasProperty("_BaseMap")) existing = source.GetTexture("_BaseMap");
                if (existing == null && source.HasProperty("_BaseColorMap")) existing = source.GetTexture("_BaseColorMap");
                if (existing is Texture2D existingTexture)
                {
                    return existingTexture;
                }

                string materialName = source.name ?? string.Empty;
                for (int i = 0; i < textures.Length; i++)
                {
                    Texture2D candidate = textures[i];
                    if (candidate != null && materialName.IndexOf(candidate.name, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return candidate;
                    }
                }
            }

            return fallback;
        }

        private static void ApplyTextureToMaterial(Material material, Texture2D baseColor)
        {
            if (material == null)
            {
                return;
            }

            Shader fallbackShader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (material.shader == null || material.shader == Shader.Find("Hidden/InternalErrorShader"))
            {
                material.shader = fallbackShader;
            }

            if (baseColor != null)
            {
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", baseColor);
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", baseColor);
                if (material.HasProperty("_BaseColorMap")) material.SetTexture("_BaseColorMap", baseColor);
            }

            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        private static Material CreateFallbackImportedMaterial(Texture2D baseColor)
        {
            Material material = CreateMaterial(Color.white, 0f, 0.28f);
            ApplyTextureToMaterial(material, baseColor);
            return material;
        }

        private static void PrepareImportedHierarchy(GameObject instance, string resourcePath)
        {
            Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.layer = IgnoreRaycastLayer;
            }

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
                DestroyObject(colliders[i]);
            }

            Camera[] cameras = instance.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                cameras[i].enabled = false;
            }

            Light[] lights = instance.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                lights[i].enabled = false;
            }

            ApplyImportedBaseColorTexture(instance, resourcePath);
        }

        private static bool FitImportedModel(
            Transform model,
            float maxWidth,
            float maxHeight,
            float yaw,
            Quaternion sourceRotation)
        {
            model.localRotation = Quaternion.Euler(0f, yaw, 0f) * sourceRotation;
            if (!TryCalculateParentRendererBounds(model, out Bounds bounds) ||
                bounds.size.y <= 0.00001f || Mathf.Max(bounds.size.x, bounds.size.z) <= 0.00001f)
            {
                return false;
            }

            float scaleForHeight = maxHeight / bounds.size.y;
            float scaleForWidth = maxWidth / Mathf.Max(bounds.size.x, bounds.size.z);
            float uniformScale = Mathf.Min(scaleForHeight, scaleForWidth);
            if (float.IsNaN(uniformScale) || float.IsInfinity(uniformScale) || uniformScale <= 0f)
            {
                return false;
            }

            model.localScale = Vector3.one * uniformScale;
            model.localPosition = new Vector3(
                -bounds.center.x * uniformScale,
                -bounds.min.y * uniformScale,
                -bounds.center.z * uniformScale);
            return true;
        }

        private static bool FitImportedBoardTile(Transform model, float width)
        {
            if (!TryCalculateParentRendererBounds(model, out Bounds bounds) ||
                bounds.size.y <= 0.00001f ||
                Mathf.Max(bounds.size.x, bounds.size.z) <= 0.00001f)
            {
                return false;
            }

            float uniformScale = width / Mathf.Max(bounds.size.x, bounds.size.z);
            if (float.IsNaN(uniformScale) || float.IsInfinity(uniformScale) || uniformScale <= 0f)
            {
                return false;
            }

            model.localScale = Vector3.one * uniformScale;
            model.localPosition = new Vector3(
                -bounds.center.x * uniformScale,
                -bounds.min.y * uniformScale,
                -bounds.center.z * uniformScale);
            return true;
        }

        private static bool TryCalculateParentRendererBounds(Transform root, out Bounds result)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Transform parent = root.parent;
            bool initialized = false;
            result = default;
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                Bounds localBounds = renderer.localBounds;
                Vector3 min = localBounds.min;
                Vector3 max = localBounds.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 rendererPoint = new(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);
                    Vector3 worldPoint = renderer.transform.TransformPoint(rendererPoint);
                    Vector3 parentPoint = parent != null ? parent.InverseTransformPoint(worldPoint) : worldPoint;
                    if (!initialized)
                    {
                        result = new Bounds(parentPoint, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        result.Encapsulate(parentPoint);
                    }
                }
            }

            return initialized;
        }

        private static bool TryCalculateLocalRendererBounds(Transform root, out Bounds result)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool initialized = false;
            result = default;
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                Bounds localBounds = renderer.localBounds;
                Vector3 min = localBounds.min;
                Vector3 max = localBounds.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 rendererPoint = new(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);
                    Vector3 rootPoint = root.InverseTransformPoint(renderer.transform.TransformPoint(rendererPoint));
                    if (!initialized)
                    {
                        result = new Bounds(rootPoint, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        result.Encapsulate(rootPoint);
                    }
                }
            }

            return initialized;
        }

        private static void BuildOpaqueShieldIndicator(Transform root, float width, float height)
        {
            var barrier = new GameObject("Broken Mirror Barrier").transform;
            barrier.gameObject.layer = IgnoreRaycastLayer;
            barrier.SetParent(root, false);
            Material material = CreateMaterial(new Color(0.34f, 0.72f, 0.70f), 0.62f, 0.78f);
            float radius = width * 0.57f;
            for (int plate = 0; plate < 4; plate++)
            {
                float angle = plate * 90f;
                Vector3 position = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, height * 0.52f, radius);
                VisualPrimitive(
                    PrimitiveType.Cube,
                    $"Mirror Plate {plate + 1}",
                    barrier,
                    position,
                    new Vector3(width * 0.34f, height * 0.34f, width * 0.055f),
                    material,
                    Quaternion.Euler(0f, angle, 0f));
            }
        }

        private static void DestroyObject(Object target)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
