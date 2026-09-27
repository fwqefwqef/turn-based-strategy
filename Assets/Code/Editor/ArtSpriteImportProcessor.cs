using System;
using UnityEditor;
using UnityEngine;

namespace Windy.Srpg.Game.Editor
{
    /// <summary>
    /// Keeps character and UI artwork ready for direct use as single sprites.
    /// Other texture import settings remain under artist control.
    /// </summary>
    internal sealed class ArtSpriteImportProcessor : AssetPostprocessor
    {
        private const string ArtFolderPrefix = "Assets/Art/";

        private void OnPreprocessTexture()
        {
            if (string.IsNullOrWhiteSpace(assetPath)
                || !assetPath.StartsWith(ArtFolderPrefix, StringComparison.OrdinalIgnoreCase)
                || assetImporter is not TextureImporter textureImporter)
            {
                return;
            }

            textureImporter.textureType = TextureImporterType.Sprite;
            textureImporter.spriteImportMode = SpriteImportMode.Single;
        }
    }
}
