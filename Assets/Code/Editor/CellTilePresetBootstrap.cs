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
        internal const string TilePresetFolder = "Assets/Data/Preset Data (Unit, Tile)/Tiles";
        internal const string SquarePresetPath = TilePresetFolder + "/Square.asset";
        internal const string WallPresetPath = TilePresetFolder + "/Wall.asset";
        internal const string ForestPresetPath = TilePresetFolder + "/Forest.asset";
        internal const string ThronePresetPath = TilePresetFolder + "/Throne.asset";
        internal const string MagicTilePresetPath = TilePresetFolder + "/Magic Tile.asset";

        static CellTilePresetBootstrap()
        {
            EditorApplication.delayCall += () => EnsureDefaults();
        }

        internal static void EnsureDefaults(bool forceRebuild = false)
        {
            if (Application.isPlaying)
            {
                return;
            }

            EnsureFolder(TilePresetFolder);

            CellTilePreset squarePreset = EnsurePreset(
                SquarePresetPath,
                "square",
                ResolveSquareSprite(),
                isTraversable: true,
                traversalCost: 1f,
                terrainEffectIds: System.Array.Empty<string>(),
                forceRebuild);

            CellTilePreset wallPreset = EnsurePreset(
                WallPresetPath,
                "wall",
                ResolveWallSprite(),
                isTraversable: false,
                traversalCost: 1f,
                terrainEffectIds: System.Array.Empty<string>(),
                forceRebuild);

            EnsurePreset(ForestPresetPath, "forest", squarePreset.TileSprite, true, 2f, new[] { "forest" }, forceRebuild);
            EnsurePreset(ThronePresetPath, "throne", squarePreset.TileSprite, true, 1f, new[] { "throne" }, forceRebuild);
            EnsurePreset(MagicTilePresetPath, "magic_tile", squarePreset.TileSprite, true, 1f, new[] { "magic_tile" }, forceRebuild);
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
            string[] terrainEffectIds,
            bool forceRebuild)
        {
            CellTilePreset preset = AssetDatabase.LoadAssetAtPath<CellTilePreset>(assetPath);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<CellTilePreset>();
                AssetDatabase.CreateAsset(preset, assetPath);
            }

            bool terrainEffectsMatch = (preset.StartingTerrainEffectIds ?? new System.Collections.Generic.List<string>())
                .SequenceEqual(terrainEffectIds ?? System.Array.Empty<string>(), System.StringComparer.OrdinalIgnoreCase);
            if (forceRebuild || preset.TileSprite == null || preset.PresetId != presetId
                || preset.IsTraversable != isTraversable || !Mathf.Approximately(preset.TraversalCost, traversalCost)
                || !terrainEffectsMatch)
            {
                preset.PresetId = presetId;
                preset.TileSprite = sprite;
                preset.IsTraversable = isTraversable;
                preset.TraversalCost = traversalCost;
                preset.StartingTerrainEffectIds = (terrainEffectIds ?? System.Array.Empty<string>()).ToList();
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
