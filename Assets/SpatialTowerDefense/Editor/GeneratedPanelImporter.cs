using UnityEditor;

namespace PicoTowerDefenseEditor
{
    public sealed class GeneratedPanelImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/UI/GeneratedV5/")) return;
            var texture = (TextureImporter)assetImporter;
            texture.textureType = TextureImporterType.Default;
            texture.npotScale = TextureImporterNPOTScale.None;
            texture.maxTextureSize = 4096;
            texture.textureCompression = TextureImporterCompression.Uncompressed;
            texture.mipmapEnabled = true;
            texture.alphaIsTransparency = true;
            texture.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            texture.filterMode = UnityEngine.FilterMode.Trilinear;
            texture.anisoLevel = 4;
            texture.isReadable = false;
        }
    }
}
