using PicoTowerDefense;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PicoTowerDefenseEditor
{
    [CustomEditor(typeof(EditableSceneModelLayout))]
    public sealed class EditableSceneModelLayoutEditor : Editor
    {
        private SerializedProperty _normalizeImportedModels;
        private SerializedProperty _skipSavedSceneDuplicates;
        private SerializedProperty _entries;

        private void OnEnable()
        {
            _normalizeImportedModels = serializedObject.FindProperty("normalizeImportedModels");
            _skipSavedSceneDuplicates = serializedObject.FindProperty("skipSavedSceneDuplicates");
            _entries = serializedObject.FindProperty("entries");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Keep the editable model list visible when the placement tool opens;
            // this is the primary desktop level-dressing workflow.
            _entries.isExpanded = true;

            EditorGUILayout.HelpBox(
                "Edit-mode preview: build a temporary scene view of the board and scenery, then adjust entries below. Expand 'Saved Scene Model Placements' in the Hierarchy to move, duplicate, or delete the authored FBX models. The preview is removed automatically before Play mode. Board tiles are reserved for gameplay.",
                MessageType.Info);
            EditorGUILayout.PropertyField(_normalizeImportedModels, new GUIContent("Normalize Imported Models"));
            if (_skipSavedSceneDuplicates != null)
            {
                EditorGUILayout.PropertyField(_skipSavedSceneDuplicates, new GUIContent("Skip Saved Scene Duplicates"));
            }
            EditorGUILayout.PropertyField(_entries, new GUIContent("Scene Models"), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Scene Model"))
                {
                    int index = _entries.arraySize;
                    _entries.InsertArrayElementAtIndex(index);
                    SerializedProperty entry = _entries.GetArrayElementAtIndex(index);
                    entry.FindPropertyRelative("visible").boolValue = true;
                    entry.FindPropertyRelative("label").stringValue = "New Scene Model";
                    entry.FindPropertyRelative("modelPath").stringValue = string.Empty;
                    entry.FindPropertyRelative("position").vector3Value = Vector3.zero;
                    entry.FindPropertyRelative("rotation").vector3Value = Vector3.zero;
                    entry.FindPropertyRelative("scale").vector3Value = Vector3.one;
                }

                using (new EditorGUI.DisabledScope(_entries.arraySize == 0))
                {
                    if (GUILayout.Button("Remove Last"))
                    {
                        _entries.DeleteArrayElementAtIndex(_entries.arraySize - 1);
                    }
                }
            }

            bool propertiesChanged = serializedObject.ApplyModifiedProperties();
            if (propertiesChanged && !Application.isPlaying)
            {
                foreach (Object selectedTarget in targets)
                {
                    ((EditableSceneModelLayout)selectedTarget).BuildEditorPreview();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Make All Scales Uniform"))
                {
                    foreach (Object selectedTarget in targets)
                    {
                        var layout = (EditableSceneModelLayout)selectedTarget;
                        Undo.RecordObject(layout, "Make Decorative Scales Uniform");
                        layout.MakeDecorativeScalesUniform();
                        EditorUtility.SetDirty(layout);
                    }
                }

                if (GUILayout.Button("Rebuild Preview"))
                {
                    foreach (Object selectedTarget in targets)
                    {
                        var layout = (EditableSceneModelLayout)selectedTarget;
                        if (Application.isPlaying)
                        {
                            layout.RebuildDecorativeModels();
                        }
                        else
                        {
                            layout.BuildEditorPreview();
                        }
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build Edit Mode Preview"))
                {
                    foreach (Object selectedTarget in targets)
                    {
                        ((EditableSceneModelLayout)selectedTarget).BuildEditorPreview();
                    }

                    FramePreview();
                }

                if (GUILayout.Button("Clear Preview"))
                {
                    foreach (Object selectedTarget in targets)
                    {
                        ((EditableSceneModelLayout)selectedTarget).ClearEditorPreview();
                    }
                }
            }

            if (GUILayout.Button("Open Saved Scene Model Placements"))
            {
                SceneModelSnapshotUtility.OpenSavedSceneModelPlacement();
            }

            if (GUILayout.Button("Restore Authored Model Transforms"))
            {
                SceneModelSnapshotUtility.RestoreAuthoredModelTransforms();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Frame Preview"))
                {
                    FramePreview();
                }

                EditorGUILayout.LabelField(
                    Application.isPlaying ? "Live game scene" : "Temporary edit scene",
                    EditorStyles.miniLabel);
            }

            if (GUILayout.Button("Reset Suggested Temple Layout"))
            {
                foreach (Object selectedTarget in targets)
                {
                    var layout = (EditableSceneModelLayout)selectedTarget;
                    Undo.RecordObject(layout, "Reset Decorative Model Layout");
                    layout.ResetDecorativeModelEntries();
                    EditorUtility.SetDirty(layout);
                }
            }
        }

        [MenuItem("Tools/Spatial Tower Defense/Open Scene Model Placement")]
        private static void OpenSceneModelPlacement()
        {
            const string scenePath = "Assets/SpatialTowerDefense/Scenes/SpatialDefense.unity";
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EditableSceneModelLayout layout = Object.FindAnyObjectByType<EditableSceneModelLayout>(FindObjectsInactive.Include);
            if (layout == null)
            {
                Debug.LogError("Scene model layout component was not found.");
                return;
            }

            Selection.activeObject = layout;
            EditorGUIUtility.PingObject(layout);
            if (!Application.isPlaying)
            {
                layout.BuildEditorPreview();
                FramePreview();
            }
        }

        [MenuItem("Tools/Spatial Tower Defense/Build Edit Mode Scene Preview")]
        private static void BuildEditModeScenePreview()
        {
            EditableSceneModelLayout layout = FindLayoutInOpenScene();
            if (layout == null)
            {
                Debug.LogError("Scene model layout component was not found.");
                return;
            }

            layout.BuildEditorPreview();
            Selection.activeObject = layout;
            FramePreview();
        }

        [MenuItem("Tools/Spatial Tower Defense/Save Enlarged Second Gate Size")]
        private static void SaveEnlargedSecondGateSize()
        {
            EditableSceneModelLayout layout = FindLayoutInOpenScene();
            Transform previewRoot = layout != null ? layout.EditorPreviewRoot : null;
            Transform modelsRoot = previewRoot != null
                ? previewRoot.Find("Editable Decorative Scene Models")
                : null;
            Transform gate = modelsRoot != null
                ? modelsRoot.Find("Pilgrim Mountain Gate")
                : null;
            if (layout == null || modelsRoot == null || gate == null)
            {
                Debug.LogError("[Editable Scene Layout] Build the edit-mode preview before saving the enlarged second gate.");
                return;
            }

            float visibleSize = CalculateLongestRenderedSize(gate, modelsRoot);
            if (visibleSize <= 0.0001f)
            {
                Debug.LogError("[Editable Scene Layout] The second gate has no visible renderer bounds to save.");
                return;
            }

            var serializedLayout = new SerializedObject(layout);
            SerializedProperty entries = serializedLayout.FindProperty("entries");
            bool found = false;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("label").stringValue != "Pilgrim Mountain Gate")
                {
                    continue;
                }

                entry.FindPropertyRelative("scale").vector3Value = Vector3.one * visibleSize;
                found = true;
                break;
            }

            if (!found)
            {
                Debug.LogError("[Editable Scene Layout] The Pilgrim Mountain Gate entry was not found.");
                return;
            }

            serializedLayout.ApplyModifiedProperties();
            EditorUtility.SetDirty(layout);
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            EditorSceneManager.SaveScene(layout.gameObject.scene);
            layout.BuildEditorPreview();
            Debug.Log($"[Editable Scene Layout] Saved the enlarged second gate at uniform scale {visibleSize:F3} without changing its position.");
        }

        private static float CalculateLongestRenderedSize(Transform target, Transform space)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            Vector3 minimum = Vector3.positiveInfinity;
            Vector3 maximum = Vector3.negativeInfinity;
            bool found = false;
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Bounds bounds = renderers[rendererIndex].bounds;
                Vector3 center = bounds.center;
                Vector3 extents = bounds.extents;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 worldCorner = center + Vector3.Scale(
                        extents,
                        new Vector3(
                            (corner & 1) == 0 ? -1f : 1f,
                            (corner & 2) == 0 ? -1f : 1f,
                            (corner & 4) == 0 ? -1f : 1f));
                    Vector3 localCorner = space.InverseTransformPoint(worldCorner);
                    minimum = Vector3.Min(minimum, localCorner);
                    maximum = Vector3.Max(maximum, localCorner);
                    found = true;
                }
            }

            if (!found)
            {
                return 0f;
            }

            Vector3 size = maximum - minimum;
            return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        }

        [MenuItem("Tools/Spatial Tower Defense/Clear Edit Mode Scene Preview")]
        private static void ClearEditModeScenePreview()
        {
            EditableSceneModelLayout layout = FindLayoutInOpenScene();
            if (layout != null)
            {
                layout.ClearEditorPreview();
            }
        }

        [MenuItem("Tools/Spatial Tower Defense/Play Scene Preview")]
        private static void PlayScenePreview()
        {
            if (!Application.isPlaying)
            {
                EditorApplication.isPlaying = true;
            }
        }

        private static EditableSceneModelLayout FindLayoutInOpenScene()
        {
            return Object.FindAnyObjectByType<EditableSceneModelLayout>(FindObjectsInactive.Include);
        }

        internal static void FramePreview()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return;
            }

            EditableSceneModelLayout layout = FindLayoutInOpenScene();
            Transform previewRoot = layout != null ? layout.EditorPreviewRoot : null;
            Object previousSelection = Selection.activeObject;
            if (previewRoot != null)
            {
                Selection.activeObject = previewRoot.gameObject;
                Bounds bounds = CalculatePreviewBounds(previewRoot);
                if (bounds.size.sqrMagnitude > 0.0001f)
                {
                    float largestExtent = Mathf.Max(
                        bounds.size.x,
                        Mathf.Max(bounds.size.y, bounds.size.z));
                    // Keep the tabletop readable even when an imported FBX has a
                    // conservative renderer bound. The user can still zoom out
                    // from this centered starting view in the Scene window.
                    float viewSize = Mathf.Max(largestExtent * 0.70f, 1.35f);
                    sceneView.LookAt(
                        bounds.center,
                        Quaternion.Euler(28f, -32f, 0f),
                        viewSize,
                        false,
                        true);
                }
            }
            else
            {
                sceneView.FrameSelected();
            }

            sceneView.Repaint();
            if (previousSelection != null)
            {
                Selection.activeObject = previousSelection;
            }
        }

        private static Bounds CalculatePreviewBounds(Transform previewRoot)
        {
            Renderer[] renderers = previewRoot.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default;
            bool found = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!found)
                {
                    bounds = renderers[i].bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            return bounds;
        }

    }

    // Preview objects are deliberately temporary.  Removing them at the edit/play
    // boundary prevents duplicate arenas and keeps runtime behaviour untouched.
    [InitializeOnLoad]
    internal static class EditableSceneModelLayoutPreviewLifecycle
    {
        private static bool _restorePreview;
        private static bool _isEnsuringPreview;

        static EditableSceneModelLayoutPreviewLifecycle()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            SceneView.duringSceneGui -= EnsurePreviewVisible;
            SceneView.duringSceneGui += EnsurePreviewVisible;
        }

        private static void EnsurePreviewVisible(SceneView sceneView)
        {
            if (_isEnsuringPreview || Application.isPlaying ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EditableSceneModelLayout layout = Object.FindAnyObjectByType<EditableSceneModelLayout>(
                FindObjectsInactive.Include);
            if (layout == null || layout.HasEditorPreview)
            {
                return;
            }

            _isEnsuringPreview = true;
            try
            {
                layout.BuildEditorPreview();
                EditableSceneModelLayoutEditor.FramePreview();
                sceneView.Repaint();
            }
            finally
            {
                _isEnsuringPreview = false;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                _restorePreview = false;
                EditableSceneModelLayout[] layouts = Object.FindObjectsByType<EditableSceneModelLayout>(
                    FindObjectsInactive.Include);
                for (int i = 0; i < layouts.Length; i++)
                {
                    if (layouts[i].HasEditorPreview)
                    {
                        _restorePreview = true;
                    }

                    layouts[i].ClearEditorPreview();
                }
            }
            else if (state == PlayModeStateChange.EnteredEditMode && _restorePreview)
            {
                EditorApplication.delayCall += RestorePreview;
            }
        }

        private static void RestorePreview()
        {
            _restorePreview = false;
            EditableSceneModelLayout[] layouts = Object.FindObjectsByType<EditableSceneModelLayout>(
                FindObjectsInactive.Include);
            for (int i = 0; i < layouts.Length; i++)
            {
                layouts[i].BuildEditorPreview();
            }
        }
    }
}
