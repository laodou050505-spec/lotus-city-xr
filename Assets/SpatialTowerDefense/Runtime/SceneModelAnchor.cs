using UnityEngine;

namespace PicoTowerDefense
{
    /// <summary>
    /// Marks a scene-authored decorative model that should travel with the
    /// tabletop arena. The resource path is also used to prevent the runtime
    /// layout from instantiating a second copy of the same saved model.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneModelAnchor : MonoBehaviour
    {
        [SerializeField]
        private string resourcePath;

        [SerializeField]
        private string sourceAssetPath;

        [SerializeField]
        private bool suppressMatchingLayoutEntry = true;

        public string ResourcePath => resourcePath;
        public string SourceAssetPath => sourceAssetPath;
        public bool SuppressMatchingLayoutEntry => suppressMatchingLayoutEntry;

        public void Configure(string path, string sourcePath)
        {
            resourcePath = path ?? string.Empty;
            sourceAssetPath = sourcePath ?? string.Empty;
        }

        public static bool HasSavedModelForPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            SceneModelAnchor[] anchors = FindObjectsByType<SceneModelAnchor>(FindObjectsInactive.Include);
            for (int i = 0; i < anchors.Length; i++)
            {
                SceneModelAnchor anchor = anchors[i];
                if (anchor != null && anchor.suppressMatchingLayoutEntry &&
                    string.Equals(anchor.resourcePath, path, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the first authored scene model for a Resources path. Runtime
        /// layout entries use this when a saved model suppresses its duplicate,
        /// so gameplay systems can still address the designer's actual object.
        /// </summary>
        public static bool TryGetSavedModelForPath(string path, out Transform model)
        {
            model = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            SceneModelAnchor[] anchors = FindObjectsByType<SceneModelAnchor>(FindObjectsInactive.Include);
            for (int i = 0; i < anchors.Length; i++)
            {
                SceneModelAnchor anchor = anchors[i];
                if (anchor != null && anchor.suppressMatchingLayoutEntry &&
                    string.Equals(anchor.resourcePath, path, System.StringComparison.Ordinal))
                {
                    model = anchor.transform;
                    return model != null;
                }
            }

            return false;
        }

        /// <summary>
        /// Moves saved scene-authored models under the runtime arena without
        /// changing their world pose. The parent is moved in front of the
        /// player's head for XR, so every saved model remains spatially anchored.
        /// </summary>
        public static int AdoptSavedModels(Transform arenaRoot)
        {
            if (arenaRoot == null)
            {
                return 0;
            }

            int adopted = 0;
            SceneModelAnchor[] anchors = FindObjectsByType<SceneModelAnchor>(FindObjectsInactive.Include);
            for (int i = 0; i < anchors.Length; i++)
            {
                SceneModelAnchor anchor = anchors[i];
                if (anchor == null || anchor.transform == arenaRoot || anchor.transform.IsChildOf(arenaRoot))
                {
                    continue;
                }

                anchor.transform.SetParent(arenaRoot, true);
                SetDecorationOnly(anchor.gameObject);
                adopted++;
            }

            return adopted;
        }

        private static void SetDecorationOnly(GameObject root)
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
    }
}
