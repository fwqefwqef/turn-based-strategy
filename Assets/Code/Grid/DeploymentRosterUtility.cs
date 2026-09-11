using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Campaign;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Grid
{
    internal static class DeploymentRosterUtility
    {
        public static string[] NormalizeRoster(IEnumerable<string> rosterUnitIds, CampaignSaveData save, int deploymentSlotCount)
        {
            if (deploymentSlotCount <= 0)
            {
                return Array.Empty<string>();
            }

            HashSet<string> ownedUnitIds = new HashSet<string>(
                (save?.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>())
                    .Where(unit => unit != null && !string.IsNullOrWhiteSpace(unit.UnitId))
                    .Select(unit => unit.UnitId),
                StringComparer.OrdinalIgnoreCase);

            List<string> normalizedRoster = new List<string>(deploymentSlotCount);
            HashSet<string> seenUnitIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string rosterUnitId in rosterUnitIds ?? Array.Empty<string>())
            {
                if (normalizedRoster.Count >= deploymentSlotCount)
                {
                    break;
                }

                string normalizedUnitId = rosterUnitId?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedUnitId))
                {
                    normalizedRoster.Add(string.Empty);
                    continue;
                }

                if (!ownedUnitIds.Contains(normalizedUnitId) || !seenUnitIds.Add(normalizedUnitId))
                {
                    normalizedRoster.Add(string.Empty);
                    continue;
                }

                normalizedRoster.Add(normalizedUnitId);
            }

            while (normalizedRoster.Count < deploymentSlotCount)
            {
                normalizedRoster.Add(string.Empty);
            }

            return normalizedRoster.ToArray();
        }

        public static bool AreEqual(IEnumerable<string> left, IEnumerable<string> right)
        {
            string[] leftArray = left?.ToArray() ?? Array.Empty<string>();
            string[] rightArray = right?.ToArray() ?? Array.Empty<string>();
            if (leftArray.Length != rightArray.Length)
            {
                return false;
            }

            for (int i = 0; i < leftArray.Length; i++)
            {
                if (!string.Equals(leftArray[i], rightArray[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        public static Dictionary<string, OwnedUnitSaveData> BuildOwnedUnitsById(CampaignSaveData save)
        {
            return (save?.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>())
                .Where(unit => unit != null && !string.IsNullOrWhiteSpace(unit.UnitId))
                .GroupBy(unit => unit.UnitId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        }

        public static Dictionary<string, UnitPreset> BuildVisualPresetsById(IEnumerable<UnitPreset> presets)
        {
            return (presets ?? Enumerable.Empty<UnitPreset>())
                .Where(preset => preset != null && !string.IsNullOrWhiteSpace(preset.PresetId))
                .GroupBy(preset => preset.PresetId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        }
    }
}
