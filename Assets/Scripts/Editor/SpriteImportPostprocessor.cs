using UnityEditor;
using UnityEngine;

namespace SlotGame.EditorTools
{
    /// <summary>
    /// Imports everything under Assets/UI as a crisp, uncompressed UI sprite automatically, so the
    /// project works straight after cloning without manual import-setting clicks.
    /// </summary>
    public sealed class SpriteImportPostprocessor : AssetPostprocessor
    {
        private const string UiRoot = "Assets/UI/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(UiRoot)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // 9-slice the popup frame so it can be resized without stretching its rounded corners.
            if (assetPath.EndsWith("Popup_Frame.png"))
                importer.spriteBorder = new Vector4(80f, 80f, 80f, 80f);
        }
    }
}
