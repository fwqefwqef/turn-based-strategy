using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using Windy.Srpg.Game.Campaign;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Game.Grid;

namespace Windy.Srpg.Game.Grid
{
    public partial class CellGrid
    {
        // --- Campaign save I/O ---
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
            List<Unit> deployedUnits = GetAllUnits()
                .Where(unit => unit != null && unit.PlayerNumber == 0 && unit.IncludeInOwnedUnitSave)
                .ToList()
                ;

            if (deployedUnits.Count == 0)
            {
                return;
            }

            CampaignSaveData existingSave = LoadSeededCampaignSave();
            string[] authoredRoster = GetEditableDeploymentRoster(existingSave, GetDeploymentSlotCount());
            CampaignSaveData save = CampaignSaveFactory.CreateFromOwnedUnits(
                deployedUnits,
                existingSave,
                authoredRoster);
            SaveCampaignDataImmediate(save);
            Debug.Log($"CellGrid: Wrote owned unit save to '{CampaignSaveManager.SavePath}'.");
        }

        // Pre-battle deployment has one source-of-truth chain:
        // save roster -> staged roster -> scene deployment units.
        // The board is only a visual result of the roster. It should not invent a roster on its own.

        private void PrepareFriendlyDeploymentFromSave()
        {
            CampaignSaveData save = CampaignSaveManager.Load();
            CampaignSaveData seededSave = CampaignSaveFactory.EnsureStarterOwnedUnits(save, starterOwnedUnitPresets);
            cachedCampaignSave = seededSave;
            campaignSaveDirty = false;
            hasUnsavedDeploymentRosterChanges = false;
            DeploymentSlot[] deploymentSlots = DeploymentScene.GetDeploymentSlots();
            if (deploymentSlots.Length > 0)
            {
                stagedDeploymentRosterUnitIds = GetResolvedDeploymentRosterForCurrentScene(seededSave, deploymentSlots.Length);
                ApplyFriendlyDeployment(seededSave, stagedDeploymentRosterUnitIds);
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

            if (!DeploymentRosterUtility.AreEqual(left.DeploymentRosterUnitIds, right.DeploymentRosterUnitIds))
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

        // --- Pre-battle deployment ---
        public IReadOnlyList<OwnedUnitSaveData> GetOwnedUnitsForPreBattle()
        {
            return (LoadSeededCampaignSave()?.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>()).ToArray();
        }

        public int GetDeploymentSlotCount()
        {
            return DeploymentScene.GetDeploymentSlots().Length;
        }

        public IReadOnlyList<Cell> GetPreBattleDeploymentSlotCells()
        {
            return DeploymentScene.GetDeploymentSlotCells();
        }

        public Cell GetPreferredPreBattleDeploymentCell()
        {
            return DeploymentScene.GetPreferredDeploymentCell(selectedPreBattleDeploymentSlotIndex);
        }

        public IReadOnlyList<string> GetDeploymentRosterForPreBattle()
        {
            CampaignSaveData save = LoadSeededCampaignSave();
            return GetEditableDeploymentRoster(save, GetDeploymentSlotCount());
        }

        public void EnterPreBattleDeploymentSwapMode()
        {
            if (!IsPreBattlePhase)
            {
                return;
            }

            isPreBattleDeploymentSwapMode = true;
            selectedPreBattleDeploymentSlotIndex = -1;
            selectedPreBattleDeploymentUnit = null;
            preBattleDeploymentSelectionFrame = -1;
            UpdateDeploymentSlotSelectionVisuals();
            SetState(new PreBattleDeploymentSwapState(this));
            PreBattleStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ExitPreBattleDeploymentSwapMode()
        {
            bool hadSelection = selectedPreBattleDeploymentSlotIndex >= 0;
            bool wasActive = isPreBattleDeploymentSwapMode;

            isPreBattleDeploymentSwapMode = false;
            selectedPreBattleDeploymentSlotIndex = -1;
            selectedPreBattleDeploymentUnit = null;
            preBattleDeploymentSelectionFrame = -1;
            UpdateDeploymentSlotSelectionVisuals();

            if (IsPreBattlePhase)
            {
                EnterBlockedInputState();
            }

            if (hadSelection || wasActive)
            {
                PreBattleStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void CancelPreBattleDeploymentSelection()
        {
            if (!IsPreBattleDeploymentSwapModeActive || selectedPreBattleDeploymentSlotIndex < 0)
            {
                return;
            }

            selectedPreBattleDeploymentSlotIndex = -1;
            selectedPreBattleDeploymentUnit = null;
            preBattleDeploymentSelectionFrame = -1;
            UpdateDeploymentSlotSelectionVisuals();
            PreBattleStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void HandlePreBattleDeploymentUnitClicked(Unit unit)
        {
            if (!IsPreBattleDeploymentSwapModeActive || unit == null || unit.PlayerNumber != 0 || unit.ExcludedFromBattle)
            {
                return;
            }

            if (!TryGetDeploymentSlotIndexForUnit(unit, out int slotIndex))
            {
                return;
            }

            HandlePreBattleDeploymentSlotClicked(slotIndex, unit);
        }

        public void HandlePreBattleDeploymentSlotClicked(int slotIndex)
        {
            HandlePreBattleDeploymentSlotClicked(slotIndex, null);
        }

        public void HandlePreBattleDeploymentCellClicked(Cell cell)
        {
            if (cell == null || !TryGetDeploymentSlotIndexForCell(cell, out int slotIndex))
            {
                return;
            }

            HandlePreBattleDeploymentSlotClicked(slotIndex, null);
        }

        private void HandlePreBattleDeploymentSlotClicked(int slotIndex, Unit clickedUnit)
        {
            if (!IsPreBattleDeploymentSwapModeActive || slotIndex < 0)
            {
                return;
            }

            IReadOnlyList<string> roster = GetDeploymentRosterForPreBattle();
            if (slotIndex >= roster.Count)
            {
                return;
            }

            if (selectedPreBattleDeploymentSlotIndex < 0)
            {
                selectedPreBattleDeploymentSlotIndex = slotIndex;
                selectedPreBattleDeploymentUnit = clickedUnit;
                preBattleDeploymentSelectionFrame = Time.frameCount;
                UpdateDeploymentSlotSelectionVisuals();
                PreBattleStateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (selectedPreBattleDeploymentSlotIndex == slotIndex)
            {
                if (preBattleDeploymentSelectionFrame == Time.frameCount)
                {
                    return;
                }

                selectedPreBattleDeploymentSlotIndex = -1;
                selectedPreBattleDeploymentUnit = null;
                preBattleDeploymentSelectionFrame = -1;
                UpdateDeploymentSlotSelectionVisuals();
                PreBattleStateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            int firstSlotIndex = selectedPreBattleDeploymentSlotIndex;
            selectedPreBattleDeploymentSlotIndex = -1;
            selectedPreBattleDeploymentUnit = null;
            preBattleDeploymentSelectionFrame = -1;
            UpdateDeploymentSlotSelectionVisuals();
            SwapDeploymentSlots(firstSlotIndex, slotIndex);
            PreBattleStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ReplaceDeploymentSlotUnit(int slotIndex, string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return;
            }

            CampaignSaveData save = LoadSeededCampaignSave();
            OwnedUnitSaveData[] ownedUnits = save?.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>();
            if (!ownedUnits.Any(unit => unit != null && string.Equals(unit.UnitId, unitId, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            string[] roster = GetDeploymentRosterForPreBattle().ToArray();
            if (slotIndex < 0 || slotIndex >= roster.Length)
            {
                return;
            }

            int existingIndex = Array.FindIndex(roster, rosterUnitId => string.Equals(rosterUnitId, unitId, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0 && existingIndex != slotIndex)
            {
                (roster[slotIndex], roster[existingIndex]) = (roster[existingIndex], roster[slotIndex]);
            }
            else
            {
                roster[slotIndex] = unitId;
            }

            ApplyStagedDeploymentRoster(roster);
        }

        public void ClearDeploymentSlotUnit(int slotIndex)
        {
            string[] roster = GetDeploymentRosterForPreBattle().ToArray();
            if (slotIndex < 0 || slotIndex >= roster.Length)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(roster[slotIndex]))
            {
                return;
            }

            roster[slotIndex] = string.Empty;
            ApplyStagedDeploymentRoster(roster);
        }

        public void SetDeploymentRoster(IEnumerable<string> rosterUnitIds)
        {
            CampaignSaveData save = LoadSeededCampaignSave();
            string[] roster = DeploymentRosterUtility.NormalizeRoster(rosterUnitIds, save, GetDeploymentSlotCount());
            ApplyStagedDeploymentRoster(roster);
        }

        public void SwapDeploymentSlots(int firstSlotIndex, int secondSlotIndex)
        {
            string[] roster = GetDeploymentRosterForPreBattle().ToArray();
            if (firstSlotIndex < 0 || secondSlotIndex < 0 || firstSlotIndex >= roster.Length || secondSlotIndex >= roster.Length || firstSlotIndex == secondSlotIndex)
            {
                return;
            }

            (roster[firstSlotIndex], roster[secondSlotIndex]) = (roster[secondSlotIndex], roster[firstSlotIndex]);
            ApplyStagedDeploymentRoster(roster);
        }

        public void BeginBattleFromPreBattle()
        {
            if (!IsPreBattlePhase)
            {
                return;
            }

            ExitPreBattleDeploymentSwapMode();
            battleStarted = true;
            SetDeploymentSlotVisibility(false);
            RebuildSceneCellOccupancy();
            PreBattleStateChanged?.Invoke(this, EventArgs.Empty);
            RequestFrameworkBattleStart();
        }

        public void SaveDeploymentRosterChanges()
        {
            CampaignSaveData save = LoadSeededCampaignSave();
            int slotCount = GetDeploymentSlotCount();
            string[] roster = GetEditableDeploymentRoster(save, slotCount);
            bool changed = !DeploymentRosterUtility.AreEqual(save.DeploymentRosterUnitIds, roster);
            save.DeploymentRosterUnitIds = roster;
            stagedDeploymentRosterUnitIds = roster.ToArray();
            hasUnsavedDeploymentRosterChanges = false;
            if (changed)
            {
                SaveCampaignDataImmediate(save);
            }

            DeploymentRosterChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyFriendlyDeployment(CampaignSaveData save, IReadOnlyList<string> rosterOverride = null)
        {
            Dictionary<string, UnitPreset> presetsById = DeploymentRosterUtility.BuildVisualPresetsById(starterOwnedUnitPresets);
            DeploymentScene.ApplyFriendlyDeployment(save, rosterOverride, presetsById);
        }
        private string[] GetResolvedDeploymentRosterForCurrentScene(CampaignSaveData save, int deploymentSlotCount)
        {
            string[] savedRoster = DeploymentRosterUtility.NormalizeRoster(save?.DeploymentRosterUnitIds, save, deploymentSlotCount);
            bool hasExplicitSavedRoster = save?.DeploymentRosterUnitIds != null && save.DeploymentRosterUnitIds.Length > 0;
            if (hasExplicitSavedRoster)
            {
                return savedRoster;
            }

            string[] chapterDefaultRoster = CampaignSaveFactory.ResolveDeploymentRosterForChapter(null, save, deploymentSlotCount);
            return DeploymentRosterUtility.NormalizeRoster(chapterDefaultRoster, save, deploymentSlotCount);
        }

        public string[] ResolveDeploymentRosterForCurrentChapter(IEnumerable<string> rosterUnitIds = null)
        {
            CampaignSaveData save = LoadSeededCampaignSave();
            return CampaignSaveFactory.ResolveDeploymentRosterForChapter(
                CampaignSaveFactory.CompactDeploymentRoster(rosterUnitIds ?? stagedDeploymentRosterUnitIds),
                save,
                GetDeploymentSlotCount());
        }

        private void SetDeploymentSlotVisibility(bool isVisible)
        {
            DeploymentScene.SetDeploymentSlotVisibility(isVisible);
            UpdateDeploymentSlotSelectionVisuals();
        }

        private bool TryGetDeploymentSlotIndexForUnit(Unit unit, out int slotIndex)
        {
            return DeploymentScene.TryGetDeploymentSlotIndexForUnit(unit, out slotIndex);
        }

        private bool TryGetDeploymentSlotIndexForCell(Cell cell, out int slotIndex)
        {
            return DeploymentScene.TryGetDeploymentSlotIndexForCell(cell, out slotIndex);
        }

        private void UpdateDeploymentSlotSelectionVisuals()
        {
            DeploymentScene.UpdateDeploymentSlotSelectionVisuals(
                IsPreBattleDeploymentSwapModeActive,
                selectedPreBattleDeploymentSlotIndex);
        }

        private void ApplyStagedDeploymentRoster(IEnumerable<string> rosterUnitIds)
        {
            CampaignSaveData save = LoadSeededCampaignSave();
            int slotCount = GetDeploymentSlotCount();
            string[] previousRoster = GetEditableDeploymentRoster(save, slotCount);
            string[] roster = DeploymentRosterUtility.NormalizeRoster(rosterUnitIds, save, slotCount);
            if (DeploymentRosterUtility.AreEqual(previousRoster, roster))
            {
                return;
            }

            stagedDeploymentRosterUnitIds = roster;
            hasUnsavedDeploymentRosterChanges = !DeploymentRosterUtility.AreEqual(save.DeploymentRosterUnitIds, roster);
            ApplyFriendlyDeployment(save, roster);
            DeploymentRosterChanged?.Invoke(this, EventArgs.Empty);
        }

        private string[] GetEditableDeploymentRoster(CampaignSaveData save, int deploymentSlotCount)
        {
            if (deploymentSlotCount <= 0)
            {
                return Array.Empty<string>();
            }

            if (stagedDeploymentRosterUnitIds == null || stagedDeploymentRosterUnitIds.Length == 0)
            {
                stagedDeploymentRosterUnitIds = GetResolvedDeploymentRosterForCurrentScene(save, deploymentSlotCount);
            }

            string[] normalizedRoster = DeploymentRosterUtility.NormalizeRoster(stagedDeploymentRosterUnitIds, save, deploymentSlotCount);
            if (!DeploymentRosterUtility.AreEqual(stagedDeploymentRosterUnitIds, normalizedRoster))
            {
                stagedDeploymentRosterUnitIds = normalizedRoster;
            }

            return normalizedRoster;
        }
    }
}
