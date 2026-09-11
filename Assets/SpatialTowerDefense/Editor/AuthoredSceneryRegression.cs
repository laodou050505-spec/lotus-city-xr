using System;
using System.IO;
using System.Linq;
using PicoTowerDefense;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PicoTowerDefenseEditor
{
    public static class AuthoredSceneryRegression
    {
        private const string ScenePath = "Assets/SpatialTowerDefense/Scenes/SpatialDefense.unity";
        private const string TempleName = "tripo_convert_dabf4fdf-9e52-4695-ad96-a0409841049c";

        public sealed class Snapshot
        {
            private readonly Renderer[] renderers;
            private readonly Matrix4x4[] expected;
            private readonly Material[][] materials;
            private readonly Transform temple;
            private readonly Transform water;

            public Snapshot(Scene scene)
            {
                renderers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>(true)).ToArray();
                Matrix4x4 conversion = Matrix4x4.TRS(GameDefinitions.AuthoredScenePosition, Quaternion.identity,
                    Vector3.one * GameDefinitions.AuthoredSceneScale).inverse;
                expected = renderers.Select(renderer => conversion * renderer.transform.localToWorldMatrix).ToArray();
                materials = renderers.Select(renderer => renderer.sharedMaterials).ToArray();
                temple = GameObject.Find(TempleName)?.transform;
                water = GameObject.Find("Plane")?.transform;
                if (temple == null || water == null)
                    throw new BuildFailedException("Production scene must retain the authored protection tower and water plane.");
            }

            public void Verify(SpatialTowerDefenseGame game)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || !renderer.transform.IsChildOf(game.ArenaRoot))
                        throw new BuildFailedException("Authored scenery missing from the room-scale arena: " + (renderer != null ? renderer.name : "destroyed renderer"));
                    Matrix4x4 actual = renderer.transform.localToWorldMatrix;
                    for (int n = 0; n < 16; n++)
                        if (Mathf.Abs(expected[i][n] - actual[n]) > 0.0002f)
                            throw new BuildFailedException("Authored scenery pose/size changed during meter conversion: " + renderer.name);
                    if (!materials[i].SequenceEqual(renderer.sharedMaterials))
                        throw new BuildFailedException("Authored scenery materials changed: " + renderer.name);
                }
                var protectedField = typeof(SpatialTowerDefenseGame).GetField("_protectedStructure", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if ((Transform)protectedField.GetValue(game) != temple)
                    throw new BuildFailedException("Protection objective must use the saved rear tower, not the fallback temple.");
                Renderer pond = water.GetComponent<Renderer>();
                if (pond.sharedMaterial.shader == null || !pond.sharedMaterial.shader.isSupported || pond.bounds.size.x < 0.1f)
                    throw new BuildFailedException("Authored water must have a supported material and visible meter-scale bounds.");
                if (pond.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off ||
                    (game.PreviewCamera.depthTextureMode & DepthTextureMode.Depth) == 0)
                    throw new BuildFailedException("Water requires camera depth and must not cast dithered shadows on the pond bed.");
                Debug.Log($"[Authored Scenery] PASS: all {renderers.Length} saved renderers retain their poses, sizes and materials; rear protection tower and pond belong to the arena. Tower={temple.position:F3}, water={water.position:F3}, water bounds={pond.bounds.size:F3}.");
            }
        }

        // One-time, idempotent enrollment of the two manually placed scene roots.
        // Adds metadata only; their serialized transforms, meshes and materials stay intact.
        public static void RestoreAnchors()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (string name in new[] { TempleName, "Plane" })
            {
                GameObject target = GameObject.Find(name);
                if (target == null) throw new BuildFailedException("Missing authored object: " + name);
                SceneModelAnchor anchor = target.GetComponent<SceneModelAnchor>() ?? target.AddComponent<SceneModelAnchor>();
                anchor.Configure(string.Empty, name == TempleName
                    ? "Assets/模型资产/s1/tripo_convert_dabf4fdf-9e52-4695-ad96-a0409841049c.fbx"
                    : "Assets/PolygonNatureBiomes/PNB_Core/Materials/Water_Mat_01.mat");
                var serialized = new SerializedObject(anchor);
                serialized.FindProperty("suppressMatchingLayoutEntry").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(anchor);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ProjectSetup.Verify();
            Render();
        }

        public static void Render()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            try
            {
                SpatialTowerDefenseGame game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpatialTowerDefenseGame>(true)).Single();
                game.BuildWorld();
                Camera camera = game.PreviewCamera;
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Evidence", "scenery-restored-20260908"));
                Directory.CreateDirectory(output);
                foreach (string subject in new[] { "overview", "rear-protection-tower", "right-lower-water" })
                {
                    Vector3 target = subject == "rear-protection-tower" ? GameObject.Find(TempleName).GetComponentInChildren<Renderer>().bounds.center :
                        subject == "right-lower-water" ? GameObject.Find("Plane").transform.position : new Vector3(0f, 1.10f, 0.1f);
                    camera.transform.position = target + (subject == "overview" ? new Vector3(1.4f, 2.1f, -2.7f) :
                        subject == "rear-protection-tower" ? new Vector3(-0.6f, 0.65f, -0.9f) : new Vector3(0.65f, 0.85f, -1.05f));
                    camera.transform.LookAt(target);
                    foreach (BillboardText panel in game.ArenaRoot.GetComponentsInChildren<BillboardText>(true)) panel.FaceTarget();
                    var render = new RenderTexture(1400, 1050, 24) { antiAliasing = 2 };
                    RenderTexture previous = RenderTexture.active;
                    camera.targetTexture = render;
                    RenderTexture.active = render;
                    camera.Render();
                    var texture = new Texture2D(1400, 1050, TextureFormat.RGB24, false);
                    texture.ReadPixels(new Rect(0, 0, 1400, 1050), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(output, subject + ".png"), texture.EncodeToPNG());
                    camera.targetTexture = null;
                    RenderTexture.active = previous;
                    UnityEngine.Object.DestroyImmediate(texture);
                    UnityEngine.Object.DestroyImmediate(render);
                }
            }
            finally { EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); }
        }
    }
}
