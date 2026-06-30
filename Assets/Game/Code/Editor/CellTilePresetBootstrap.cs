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
        internal const string SquarePrefabPath = "Assets/Scenes/Square.prefab";
        internal const string WallPrefabPath = "Assets/Scenes/Wall.prefab";
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

            UpgradeLegacyPrefab(SquarePrefabPath, squarePreset, destroyChildren: false);
            UpgradeLegacyPrefab(WallPrefabPath, wallPreset, destroyChildren: true);
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

        private static void UpgradeLegacyPrefab(string prefabPath, CellTilePreset preset, bool destroyChildren)
        {
            if (preset == null)
            {
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                return;
            }

            try
            {
                if (destroyChildren)
                {
                    foreach (Transform child in prefabRoot.transform.Cast<Transform>().ToList())
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }
                }

                if (prefabRoot.TryGetComponent(out Cell cell))
                {
                    cell.SetTilePreset(preset);
                }

                if (prefabRoot.TryGetComponent(out SpriteRenderer spriteRenderer))
                {
                    spriteRenderer.sprite = preset.TileSprite;
                    spriteRenderer.color = Color.white;
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static Sprite ResolveSquareSprite()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SquarePrefabPath);
            return prefab != null && prefab.TryGetComponent(out SpriteRenderer spriteRenderer)
                ? spriteRenderer.sprite
                : null;
        }

        private static Sprite ResolveWallSprite()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabPath);
            if (prefab == null)
            {
                return null;
            }

            SpriteRenderer childRenderer = prefab.GetComponentsInChildren<SpriteRenderer>(true)
                .FirstOrDefault(renderer => renderer != null && renderer.gameObject != prefab);
            if (childRenderer != null)
            {
                return childRenderer.sprite;
            }

            return prefab.TryGetComponent(out SpriteRenderer spriteRenderer)
                ? spriteRenderer.sprite
                : null;
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
