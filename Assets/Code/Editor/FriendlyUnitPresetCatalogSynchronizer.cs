using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Editor
{
    [InitializeOnLoad]
    internal static class FriendlyUnitPresetCatalogSynchronizer
    {
        internal const string PresetFolder = "Assets/Data/Preset Data (Unit, Tile)/friendly preset";
        private const string CatalogPath = "Assets/Data/Resources/FriendlyUnitPresetCatalog.asset";
        private static bool synchronizationQueued;

        static FriendlyUnitPresetCatalogSynchronizer()
        {
            QueueSynchronization();
        }

        internal static void QueueSynchronization()
        {
            if (synchronizationQueued)
            {
                return;
            }

            synchronizationQueued = true;
            EditorApplication.delayCall += Synchronize;
        }

        private static void Synchronize()
        {
            synchronizationQueued = false;
            UnitPreset[] presets = AssetDatabase.FindAssets("t:UnitPreset", new[] { PresetFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UnitPreset>)
                .Where(preset => preset != null)
                .OrderBy(preset => preset.PresetId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(preset => preset.name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            FriendlyUnitPresetCatalog catalog = AssetDatabase.LoadAssetAtPath<FriendlyUnitPresetCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<FriendlyUnitPresetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            if ((catalog.Presets ?? Array.Empty<UnitPreset>()).SequenceEqual(presets))
            {
                return;
            }

            catalog.Presets = presets;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }
    }

    internal sealed class FriendlyUnitPresetCatalogPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (TouchesFriendlyPresetFolder(importedAssets)
                || TouchesFriendlyPresetFolder(deletedAssets)
                || TouchesFriendlyPresetFolder(movedAssets)
                || TouchesFriendlyPresetFolder(movedFromAssetPaths))
            {
                FriendlyUnitPresetCatalogSynchronizer.QueueSynchronization();
            }
        }

        private static bool TouchesFriendlyPresetFolder(string[] paths)
        {
            return (paths ?? Array.Empty<string>()).Any(path =>
                !string.IsNullOrWhiteSpace(path)
                && path.StartsWith(FriendlyUnitPresetCatalogSynchronizer.PresetFolder + "/", StringComparison.OrdinalIgnoreCase));
        }
    }
}
