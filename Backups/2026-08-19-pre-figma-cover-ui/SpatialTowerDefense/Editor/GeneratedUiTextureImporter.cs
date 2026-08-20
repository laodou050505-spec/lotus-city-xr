using UnityEditor;
using UnityEngine;

namespace PicoTowerDefenseEditor
{
    public sealed class GeneratedUiTextureImporter : AssetPostprocessor
    {
        private const string GeneratedUiFolder = "Assets/SpatialTowerDefense/Resources/UI/Generated/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(GeneratedUiFolder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
