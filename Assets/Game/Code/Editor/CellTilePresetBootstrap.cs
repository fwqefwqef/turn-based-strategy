using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Windy.Srpg.Game.Grid;

namespace Windy.Srpg.Game.Editor
{
    [InitializeOnLoad]
    internal static class CellTilePresetBootstrap
    {
        internal const string SquarePresetPath = "Assets/Game/Tiles/Square.asset";
        internal const string WallPresetPath = "Assets/Game/Tiles/Wall.asset";

        static CellTilePresetBootstrap()
        {
            EditorApplication.delayCall += () => EnsureDefaults();
        }

        [MenuItem("Tools/Windy SRPG/Rebuild Default Tile Presets")]
        private static void RebuildDefaultsMenu()
        {
            EnsureDefaults(forceRebuild: true);
        }

        internal static void EnsureDefaults(bool forceRebuild = false)
        {
            if (Application.isPlaying)
            {
                return;
            }

            EnsureFolder("Assets/Game");
            EnsureFolder("Assets/Game/Tiles");

            CellTilePreset squarePreset = EnsurePreset(
                SquarePresetPath,
                "square",
                ResolveSquareSprite(),
                isTraversable: true,
                traversalCost: 1f,
                forceRebuild);

            CellTilePreset wallPreset = EnsurePreset(
                WallPresetPath,
                "wall",
                ResolveWallSprite(),
                isTraversable: false,
                traversalCost: 1f,
                forceRebuild);
            AssetDatabase.SaveAssets();
        }

        internal static CellTilePreset LoadSquarePreset()
        {
            EnsureDefaults();
            return AssetDatabase.LoadAssetAtPath<CellTilePreset>(SquarePresetPath);
        }

        internal static CellTilePreset LoadWallPreset()
        {
            EnsureDefaults();
            return AssetDatabase.LoadAssetAtPath<CellTilePreset>(WallPresetPath);
        }

        private static CellTilePreset EnsurePreset(
            string assetPath,
            string presetId,
            Sprite sprite,
            bool isTraversable,
            float traversalCost,
            bool forceRebuild)
        {
            CellTilePreset preset = AssetDatabase.LoadAssetAtPath<CellTilePreset>(assetPath);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<CellTilePreset>();
                AssetDatabase.CreateAsset(preset, assetPath);
            }

            if (forceRebuild || preset.TileSprite == null || preset.PresetId != presetId)
            {
                preset.PresetId = presetId;
                preset.TileSprite = sprite;
                preset.IsTraversable = isTraversable;
                preset.TraversalCost = traversalCost;
                EditorUtility.SetDirty(preset);
            }

            return preset;
        }

        private static Sprite ResolveSquareSprite()
        {
            CellTilePreset existingPreset = AssetDatabase.LoadAssetAtPath<CellTilePreset>(SquarePresetPath);
            return existingPreset != null ? existingPreset.TileSprite : null;
        }

        private static Sprite ResolveWallSprite()
        {
            CellTilePreset existingPreset = AssetDatabase.LoadAssetAtPath<CellTilePreset>(WallPresetPath);
            return existingPreset != null ? existingPreset.TileSprite : null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
