using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Campaign;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CustomCellGrid
    {
        [ContextMenu("Write Owned Unit Save From Current Friendlies")]
        public void WriteOwnedUnitSaveFromCurrentFriendlies()
        {
            SaveOwnedUnits(overwriteExistingSave: true);
        }

        private void TryPersistOwnedUnitSave()
        {
            if (!autoCreateOwnedUnitSaveIfMissing && !overwriteOwnedUnitSaveOnGameStarted)
            {
                return;
            }

            if (!overwriteOwnedUnitSaveOnGameStarted && CampaignSaveManager.SaveExists)
            {
                return;
            }

            SaveOwnedUnits(overwriteOwnedUnitSaveOnGameStarted);
        }

        private void SaveOwnedUnits(bool overwriteExistingSave)
        {
            List<CustomUnit> deployedUnits = GetAllCustomUnits()
                .Where(unit => unit != null && unit.PlayerNumber == 0)
                .ToList()
                ;

            if (deployedUnits.Count == 0)
            {
                return;
            }

            CampaignSaveData existingSave = LoadSeededCampaignSave();
            CampaignSaveData save = CampaignSaveFactory.CreateFromOwnedUnits(
                deployedUnits,
                existingSave,
                GetCurrentSceneDeploymentRoster(existingSave, GetDeploymentSlots().Length));
            SaveCampaignDataImmediate(save);
            Debug.Log($"CustomCellGrid: Wrote owned unit save to '{CampaignSaveManager.SavePath}'.");
        }

        private void PrepareFriendlyDeploymentFromSave()
        {
            CampaignSaveData save = CampaignSaveManager.Load();
            CampaignSaveData seededSave = CampaignSaveFactory.EnsureStarterOwnedUnits(save, starterOwnedUnitPresets);
            cachedCampaignSave = seededSave;
            campaignSaveDirty = false;
            hasUnsavedDeploymentRosterChanges = false;
            DeploymentSlot[] deploymentSlots = GetDeploymentSlots();
            if (deploymentSlots.Length > 0)
            {
                stagedDeploymentRosterUnitIds = GetResolvedDeploymentRosterForCurrentScene(seededSave, deploymentSlots.Length);
                ApplyFriendlyDeployment(deploymentSlots, seededSave, stagedDeploymentRosterUnitIds);
            }
            else if (seededSave.DeploymentRosterUnitIds == null)
            {
                stagedDeploymentRosterUnitIds = Array.Empty<string>();
                seededSave.DeploymentRosterUnitIds = Array.Empty<string>();
            }
            else
            {
                stagedDeploymentRosterUnitIds = seededSave.DeploymentRosterUnitIds.ToArray();
            }

            if (save == null || overwriteOwnedUnitSaveOnGameStarted || autoCreateOwnedUnitSaveIfMissing || !CampaignSaveManager.SaveExists || !AreSavesEquivalent(save, seededSave))
            {
                SaveCampaignDataImmediate(seededSave);
            }
        }

        private CampaignSaveData LoadSeededCampaignSave()
        {
            if (cachedCampaignSave != null)
            {
                return cachedCampaignSave;
            }

            CampaignSaveData save = CampaignSaveManager.Load();
            cachedCampaignSave = CampaignSaveFactory.EnsureStarterOwnedUnits(save, starterOwnedUnitPresets);
            return cachedCampaignSave;
        }

        private void MarkCampaignSaveDirty()
        {
            campaignSaveDirty = true;
            if (!Application.isPlaying)
            {
                FlushCampaignSaveImmediate();
                return;
            }

            if (pendingCampaignSaveFlushCoroutine != null)
            {
                StopCoroutine(pendingCampaignSaveFlushCoroutine);
            }

            pendingCampaignSaveFlushCoroutine = StartCoroutine(FlushCampaignSaveAfterDelay());
        }

        private System.Collections.IEnumerator FlushCampaignSaveAfterDelay()
        {
            yield return new WaitForSecondsRealtime(CampaignSaveFlushDelaySeconds);
            pendingCampaignSaveFlushCoroutine = null;
            FlushCampaignSaveImmediate();
        }

        private void FlushCampaignSaveImmediate()
        {
            if (!campaignSaveDirty || cachedCampaignSave == null)
            {
                return;
            }

            campaignSaveDirty = false;
            if (pendingCampaignSaveFlushCoroutine != null)
            {
                StopCoroutine(pendingCampaignSaveFlushCoroutine);
                pendingCampaignSaveFlushCoroutine = null;
            }

            CampaignSaveManager.Save(cachedCampaignSave);
        }

        private void SaveCampaignDataImmediate(CampaignSaveData save)
        {
            cachedCampaignSave = save;
            campaignSaveDirty = false;
            if (pendingCampaignSaveFlushCoroutine != null)
            {
                StopCoroutine(pendingCampaignSaveFlushCoroutine);
                pendingCampaignSaveFlushCoroutine = null;
            }

            if (cachedCampaignSave != null)
            {
                CampaignSaveManager.Save(cachedCampaignSave);
            }
        }

        private static string[] NormalizeDeploymentRoster(IEnumerable<string> rosterUnitIds, CampaignSaveData save, int deploymentSlotCount)
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

        private static bool AreSavesEquivalent(CampaignSaveData left, CampaignSaveData right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            if (left.Version != right.Version || left.Gold != right.Gold)
            {
                return false;
            }

            if (!AreStringSequencesEqual(left.DeploymentRosterUnitIds, right.DeploymentRosterUnitIds))
            {
                return false;
            }

            if (left.OwnedUnits?.Length != right.OwnedUnits?.Length || left.StorageItems?.Length != right.StorageItems?.Length)
            {
                return false;
            }

            string leftJson = JsonUtility.ToJson(left, false);
            string rightJson = JsonUtility.ToJson(right, false);
            return string.Equals(leftJson, rightJson, StringComparison.Ordinal);
        }

        private static bool AreStringSequencesEqual(IEnumerable<string> left, IEnumerable<string> right)
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
    }
}
