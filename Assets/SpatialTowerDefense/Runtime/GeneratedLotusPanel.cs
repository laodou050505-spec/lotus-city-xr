using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PicoTowerDefense
{
    // Art and fixed Chinese typography live in generated textures. Only the
    // numeric slots change; no TextMesh/Canvas layout is used by these panels.
    public static class GeneratedLotusPanel
    {
        private static readonly Dictionary<string, Material> Materials = new();

        public static Material Material(string asset, int column = -1)
        {
            string key = asset + column;
            if (Materials.TryGetValue(key, out Material existing) && existing != null) return existing;
            Texture2D texture = Resources.Load<Texture2D>("UI/GeneratedV5/" + asset);
            if (texture == null) throw new InvalidOperationException("Missing generated panel: " + asset);
            var material = new Material(Resources.Load<Shader>("UI/GeneratedV5/PanelSurface")) { name = "Generated " + key, mainTexture = texture, color = Color.white };
            // Draw number slots after all panel artwork, independent of their
            // bounding-box centers and the viewer's angle.
            if (asset == "Digits")
            {
                material.renderQueue = 3010;
                material.SetFloat("_ZWrite", 0f);
            }
            else material.renderQueue = 2450;
            if (column >= 0)
            {
                // Half-texel insets keep neighbouring cards out of mip edges.
                float columnWidth = asset == "WaveStates" ? 1f / 3f : 0.25f;
                material.mainTextureScale = new Vector2(columnWidth - 2f / texture.width, 1f - 2f / texture.height);
                material.mainTextureOffset = new Vector2(column * columnWidth + 1f / texture.width, 1f / texture.height);
            }
            Materials[key] = material;
            return material;
        }

        public static GameObject Create(Transform parent, string name, string asset, Vector2 size, Vector3 position, int column = -1)
        {
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = name;
            panel.layer = 2;
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = position;
            panel.transform.localScale = new Vector3(size.x, size.y, 1f);
            Collider oldCollider = panel.GetComponent<Collider>();
            oldCollider.enabled = false;
            if (Application.isPlaying) UnityEngine.Object.Destroy(oldCollider);
            else UnityEngine.Object.DestroyImmediate(oldCollider);
            Renderer renderer = panel.GetComponent<Renderer>();
            renderer.sharedMaterial = Material(asset, column);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return panel;
        }

        public static LotusDigitField Digits(Transform parent, Vector3 position, float height, int capacity = 6)
        {
            var go = new GameObject("Baked numeric sprite field");
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var field = go.AddComponent<LotusDigitField>();
            field.Initialize(height, capacity);
            return field;
        }
    }

    public sealed class LotusDigitField : MonoBehaviour
    {
        private const string Glyphs = "0123456789./-";
        private Mesh _mesh;
        private float _height;
        private int _capacity;
        public string Value { get; private set; }

        public void Initialize(float height, int capacity)
        {
            _height = height;
            _capacity = capacity;
            _mesh = new Mesh { name = "Fixed numeric slots" };
            _mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GeneratedLotusPanel.Material("Digits");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public void Set(string value)
        {
            if (value == Value) return;
            Value = value;
            int count = Mathf.Min(value.Length, _capacity);
            var vertices = new Vector3[count * 4];
            var uv = new Vector2[count * 4];
            var triangles = new int[count * 6];
            float width = _height * 0.60f;
            for (int i = 0; i < count; i++)
            {
                int glyph = Mathf.Max(0, Glyphs.IndexOf(value[i]));
                float x = (i - count * 0.5f) * width;
                int v = i * 4, t = i * 6;
                vertices[v] = new Vector3(x, -_height * 0.5f, 0f);
                vertices[v+1] = new Vector3(x + width, -_height * 0.5f, 0f);
                vertices[v+2] = new Vector3(x + width, _height * 0.5f, 0f);
                vertices[v+3] = new Vector3(x, _height * 0.5f, 0f);
                float left = glyph / (float)Glyphs.Length, right = (glyph + 1f) / Glyphs.Length;
                uv[v] = new Vector2(left, 0f); uv[v+1] = new Vector2(right, 0f);
                uv[v+2] = new Vector2(right, 1f); uv[v+3] = new Vector2(left, 1f);
                triangles[t] = v; triangles[t+1] = v+2; triangles[t+2] = v+1;
                triangles[t+3] = v; triangles[t+4] = v+3; triangles[t+5] = v+2;
            }
            _mesh.Clear(); _mesh.vertices = vertices; _mesh.uv = uv; _mesh.triangles = triangles;
            _mesh.RecalculateBounds();
        }

        private void OnDestroy() { if (_mesh == null) return; if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh); }
    }
}
