using UnityEngine;

namespace PicoTowerDefense
{
    public sealed class BoardCell : MonoBehaviour
    {
        // Leave the imported tile albedo intact when idle. The old full-surface tint
        // washed the white route tile into the same dark green as placement cells.
        private static readonly Color GrassColor = Color.white;
        private static readonly Color PathColor = Color.white;
        private static readonly Color ValidColor = new(0.20f, 1f, 0.53f, 1f);
        private static readonly Color InvalidColor = new(1f, 0.20f, 0.24f, 1f);

        private MaterialPropertyBlock _propertyBlock;
        private Renderer[] _renderers;

        public Vector2Int Coordinates { get; private set; }
        public bool IsPath { get; private set; }
        public bool IsBuildable { get; private set; } = true;
        public bool IsOccupied { get; set; }

        public void Initialize(int column, int row, bool isPath, Renderer[] renderers, bool isBuildable = true)
        {
            Coordinates = new Vector2Int(column, row);
            IsPath = isPath;
            IsBuildable = isBuildable;
            _renderers = renderers ?? GetComponentsInChildren<Renderer>(true);
            _propertyBlock = new MaterialPropertyBlock();
            SetHovered(false, false);
        }

        public void SetHovered(bool hovered, bool valid)
        {
            if (_renderers == null || _propertyBlock == null)
            {
                return;
            }

            Color baseColor = IsPath ? PathColor : GrassColor;
            Color color = hovered ? (valid ? ValidColor : InvalidColor) : baseColor;
            Color emission = hovered ? color * 0.28f : Color.black;
            for (int rendererIndex = 0; rendererIndex < _renderers.Length; rendererIndex++)
            {
                Renderer renderer = _renderers[rendererIndex];
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null)
                    {
                        continue;
                    }

                    _propertyBlock.Clear();
                    renderer.GetPropertyBlock(_propertyBlock, materialIndex);
                    if (material.HasProperty("_Color"))
                    {
                        _propertyBlock.SetColor("_Color", color);
                    }
                    if (material.HasProperty("_BaseColor"))
                    {
                        _propertyBlock.SetColor("_BaseColor", color);
                    }
                    if (material.HasProperty("_EmissionColor"))
                    {
                        _propertyBlock.SetColor("_EmissionColor", emission);
                    }
                    renderer.SetPropertyBlock(_propertyBlock, materialIndex);
                }
            }
        }
    }

    public enum SpatialAction
    {
        SelectArrow,
        SelectCannon,
        SelectFrost,
        StartExperience,
        StartWave,
        RecenterArena
    }

    public sealed class SpatialActionTarget : MonoBehaviour
    {
        private Renderer _renderer;
        private MaterialPropertyBlock _propertyBlock;
        private Color _baseColor;

        public SpatialAction Action { get; private set; }

        public void Initialize(SpatialAction action, Color color)
        {
            Action = action;
            _renderer = GetComponent<Renderer>();
            _propertyBlock = new MaterialPropertyBlock();
            _baseColor = color;
            SetState(false, false, true);
        }

        public void SetState(bool selected, bool hovered, bool enabled)
        {
            if (_renderer == null || _propertyBlock == null)
            {
                return;
            }

            float brightness = !enabled ? 0.28f : hovered ? 1.45f : selected ? 1.18f : 0.82f;
            Color color = _baseColor * brightness;
            color.a = 1f;
            Color emission = (selected || hovered) && enabled ? _baseColor * 0.65f : Color.black;
            Material[] materials = _renderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                _propertyBlock.Clear();
                _renderer.GetPropertyBlock(_propertyBlock, materialIndex);
                if (material.HasProperty("_Color"))
                {
                    _propertyBlock.SetColor("_Color", color);
                }
                if (material.HasProperty("_BaseColor"))
                {
                    _propertyBlock.SetColor("_BaseColor", color);
                }
                if (material.HasProperty("_EmissionColor"))
                {
                    _propertyBlock.SetColor("_EmissionColor", emission);
                }
                _renderer.SetPropertyBlock(_propertyBlock, materialIndex);
            }
        }
    }

    public sealed class TowerMergeTarget : MonoBehaviour
    {
        private Collider _collider;

        public TowerAgent Agent { get; private set; }

        public void Initialize(TowerAgent agent)
        {
            Agent = agent;
            _collider = GetComponent<Collider>();
        }

        public void SetInteractionEnabled(bool enabled)
        {
            if (_collider != null)
            {
                _collider.enabled = enabled;
            }
        }
    }
}
