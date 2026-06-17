using System;
using System.Collections.Generic;
using System.Linq;
using TbsFramework.Cells;
using UnityEngine;
using Windy.Srpg.Game.Campaign;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CustomCellGrid
    {
        public IReadOnlyList<OwnedUnitSaveData> GetOwnedUnitsForPreBattle()
        {
            return (LoadSeededCampaignSave()?.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>()).ToArray();
        }

        public int GetDeploymentSlotCount()
        {
            return GetDeploymentSlots().Length;
        }

        public IReadOnlyList<string> GetDeploymentRosterForPreBattle()
        {
            CampaignSaveData save = LoadSeededCampaignSave();
            return GetEditableDeploymentRoster(save, GetDeploymentSlots().Length);
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

        public void HandlePreBattleDeploymentUnitClicked(CustomUnit unit)
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

        private void HandlePreBattleDeploymentSlotClicked(int slotIndex, CustomUnit clickedUnit)
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
            string[] roster = NormalizeDeploymentRoster(rosterUnitIds, save, GetDeploymentSlots().Length);
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
            int slotCount = GetDeploymentSlots().Length;
            string[] roster = GetEditableDeploymentRoster(save, slotCount);
            bool changed = !AreStringSequencesEqual(save.DeploymentRosterUnitIds, roster);
            save.DeploymentRosterUnitIds = roster;
            stagedDeploymentRosterUnitIds = roster.ToArray();
            hasUnsavedDeploymentRosterChanges = false;
            if (changed)
            {
                SaveCampaignDataImmediate(save);
            }

            DeploymentRosterChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyFriendlyDeployment(DeploymentSlot[] deploymentSlots, CampaignSaveData save, IReadOnlyList<string> rosterOverride = null)
        {
            EnsureFriendlyDeploymentUnitCapacity(deploymentSlots?.Length ?? 0);
            List<CustomUnit> deploymentUnits = GetFriendlyDeploymentUnits();
            if (deploymentUnits.Count == 0)
            {
                Debug.LogWarning("CustomCellGrid: No friendly unit placeholders were found for deployment.");
                return;
            }

            Dictionary<string, OwnedUnitSaveData> ownedUnitsById = BuildOwnedUnitsById(save);
            Dictionary<string, UnitPreset> presetsById = BuildVisualPresetsById();

            foreach (CustomUnit deploymentUnit in deploymentUnits)
            {
                deploymentUnit.gameObject.SetActive(false);
                ReleaseDeploymentUnitCell(deploymentUnit);
                deploymentUnit.ExcludedFromBattle = true;
                UnregisterDeploymentUnitFromBattle(deploymentUnit);
            }

            IReadOnlyList<string> activeRoster = rosterOverride ?? save?.DeploymentRosterUnitIds ?? Array.Empty<string>();
            int slotCount = Mathf.Min(deploymentSlots.Length, deploymentUnits.Count);
            for (int i = 0; i < slotCount; i++)
            {
                string unitId = i < activeRoster.Count ? activeRoster[i] : string.Empty;
                if (string.IsNullOrWhiteSpace(unitId) || !ownedUnitsById.TryGetValue(unitId, out OwnedUnitSaveData ownedUnit))
                {
                    continue;
                }

                UnitPreset visualPreset = null;
                if (!string.IsNullOrWhiteSpace(ownedUnit.VisualId))
                {
                    presetsById.TryGetValue(ownedUnit.VisualId, out visualPreset);
                }

                CustomUnit deploymentUnit = deploymentUnits[i];
                deploymentUnit.ConfigureFromOwnedUnitSaveData(ownedUnit, visualPreset);
                deploymentUnit.gameObject.SetActive(true);
                RegisterDeploymentUnitForBattle(deploymentUnit, deploymentSlots[i].Cell);
                AssignDeploymentUnitToSlot(deploymentUnit, deploymentSlots[i]);
                deploymentUnit.ExcludedFromBattle = false;
            }

            if (deploymentUnits.Count < deploymentSlots.Length)
            {
                Debug.LogWarning($"CustomCellGrid: Found {deploymentSlots.Length} deployment slots but only {deploymentUnits.Count} friendly unit placeholders.");
            }

            RebuildSceneCellOccupancy();
        }
        private string[] GetResolvedDeploymentRosterForCurrentScene(CampaignSaveData save, int deploymentSlotCount)
        {
            if (save?.DeploymentRosterUnitIds == null || save.DeploymentRosterUnitIds.Length == 0)
            {
                return NormalizeDeploymentRoster(GetCurrentSceneDeploymentRosterRaw(deploymentSlotCount), save, deploymentSlotCount);
            }

            return NormalizeDeploymentRoster(save?.DeploymentRosterUnitIds, save, deploymentSlotCount);
        }

        public string[] ResolveDeploymentRosterForCurrentChapter(IEnumerable<string> rosterUnitIds = null)
        {
            CampaignSaveData save = LoadSeededCampaignSave();
            return CampaignSaveFactory.ResolveDeploymentRosterForChapter(
                CampaignSaveFactory.CompactDeploymentRoster(rosterUnitIds ?? stagedDeploymentRosterUnitIds),
                save,
                GetDeploymentSlots().Length);
        }

        private string[] GetCurrentSceneDeploymentRoster(CampaignSaveData save, int deploymentSlotCount)
        {
            string[] sceneRoster = GetCurrentSceneDeploymentRosterRaw(deploymentSlotCount);
            return sceneRoster.Length > 0
                ? sceneRoster
                : GetResolvedDeploymentRosterForCurrentScene(save, deploymentSlotCount);
        }

        private string[] GetCurrentSceneDeploymentRosterRaw(int deploymentSlotCount)
        {
            if (deploymentSlotCount <= 0)
            {
                return Array.Empty<string>();
            }

            DeploymentSlot[] deploymentSlots = GetDeploymentSlots();
            if (deploymentSlots.Length == 0)
            {
                return Array.Empty<string>();
            }

            Dictionary<Cell, CustomUnit> friendlyUnitsByCell = GetFriendlyDeploymentUnits()
                .Where(unit => unit != null && unit.Cell != null && !unit.ExcludedFromBattle)
                .GroupBy(unit => unit.Cell)
                .ToDictionary(group => group.Key, group => group.First());

            List<string> roster = new List<string>(deploymentSlotCount);
            int slotCount = Mathf.Min(deploymentSlotCount, deploymentSlots.Length);
            for (int i = 0; i < slotCount; i++)
            {
                DeploymentSlot slot = deploymentSlots[i];
                if (slot?.Cell == null || !friendlyUnitsByCell.TryGetValue(slot.Cell, out CustomUnit unit) || string.IsNullOrWhiteSpace(unit.UnitId))
                {
                    roster.Add(string.Empty);
                    continue;
                }

                roster.Add(unit.UnitId);
            }

            while (roster.Count < deploymentSlotCount)
            {
                roster.Add(string.Empty);
            }

            return roster.ToArray();
        }

        private DeploymentSlot[] GetDeploymentSlots()
        {
            return Resources.FindObjectsOfTypeAll<DeploymentSlot>()
                .Where(slot => slot != null && slot.gameObject.scene.IsValid())
                .OrderBy(slot => slot.SlotIndex)
                .ThenBy(slot => slot.name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private List<CustomUnit> GetFriendlyDeploymentUnits()
        {
            return GetAllSceneCustomUnitsFromHierarchy(includeExcludedFromBattle: true)
                .Where(unit => unit != null && unit.PlayerNumber == 0)
                .ToList();
        }

        private void EnsureFriendlyDeploymentUnitCapacity(int requiredCount)
        {
            if (requiredCount <= 0)
            {
                return;
            }

            Transform unitsParent = GetSceneUnitsParent();
            if (unitsParent == null)
            {
                return;
            }

            List<CustomUnit> deploymentUnits = GetFriendlyDeploymentUnits();
            if (deploymentUnits.Count >= requiredCount)
            {
                return;
            }

            CustomUnit templateUnit = deploymentUnits.FirstOrDefault();
            if (templateUnit == null)
            {
                return;
            }

            while (deploymentUnits.Count < requiredCount)
            {
                CustomUnit clonedUnit = UnityEngine.Object.Instantiate(templateUnit, unitsParent);
                clonedUnit.name = $"{templateUnit.name}_Deployment_{deploymentUnits.Count}";
                clonedUnit.gameObject.SetActive(false);
                clonedUnit.ExcludedFromBattle = true;
                deploymentUnits.Add(clonedUnit);
            }
        }

        private void RegisterDeploymentUnitForBattle(CustomUnit unit, Cell targetCell)
        {
            if (unit == null
                || targetCell == null
                || !Application.isPlaying
                || !CanModifyRegisteredSceneUnits()
                || IsUnitRegistered(unit))
            {
                return;
            }

            RegisterSceneUnit(unit, targetCell);
            MarkRuntimeBoardDirty();
        }

        private void UnregisterDeploymentUnitFromBattle(CustomUnit unit)
        {
            if (unit == null
                || !Application.isPlaying
                || !CanModifyRegisteredSceneUnits()
                || !IsUnitRegistered(unit))
            {
                return;
            }

            unit.CombatDestroyed -= OnCombatDestroyed;
            unit.DestroyedInCombat -= OnCustomUnitDestroyed;
            subscribedUnits.Remove(unit);
            UnregisterSceneUnit(unit);
            MarkRuntimeBoardDirty();
        }

        private bool CanModifyRegisteredSceneUnits()
        {
            return Players != null && Cells != null && Units != null;
        }

        private static void AssignDeploymentUnitToSlot(CustomUnit unit, DeploymentSlot slot)
        {
            if (unit == null || slot == null || slot.Cell == null)
            {
                return;
            }

            Cell previousCell = unit.Cell;
            Cell nextCell = slot.Cell;
            bool shouldSyncOccupancy = ShouldSyncRuntimeCellOccupancy(unit);

            if (shouldSyncOccupancy && previousCell != null && previousCell != nextCell)
            {
                previousCell.CurrentUnits.Remove(unit);
                RefreshCellOccupancy(previousCell);
            }

            unit.Cell = nextCell;

            if (shouldSyncOccupancy)
            {
                if (!nextCell.CurrentUnits.Contains(unit))
                {
                    nextCell.CurrentUnits.Add(unit);
                }

                RefreshCellOccupancy(nextCell);
            }
            else
            {
                nextCell.IsTaken = unit.Obstructable;
            }

            unit.transform.localPosition = nextCell.transform.localPosition;
            unit.SyncMirroredRuntimeCell(nextCell);
        }

        private static void ReleaseDeploymentUnitCell(CustomUnit unit)
        {
            if (unit?.Cell == null)
            {
                return;
            }

            Cell currentCell = unit.Cell;
            currentCell.CurrentUnits.Remove(unit);
            RefreshCellOccupancy(currentCell);
            unit.Cell = null;
            unit.ClearMirroredRuntimeCell();
        }

        private void SetDeploymentSlotVisibility(bool isVisible)
        {
            foreach (DeploymentSlot slot in GetDeploymentSlots())
            {
                if (slot != null)
                {
                    slot.SyncToCell();
                    slot.gameObject.SetActive(isVisible);
                }
            }

            UpdateDeploymentSlotSelectionVisuals();
        }

        private bool TryGetDeploymentSlotIndexForUnit(CustomUnit unit, out int slotIndex)
        {
            slotIndex = -1;
            if (unit == null)
            {
                return false;
            }

            DeploymentSlot[] deploymentSlots = GetDeploymentSlots();
            if (deploymentSlots.Length == 0)
            {
                return false;
            }

            if (unit.Cell != null)
            {
                for (int i = 0; i < deploymentSlots.Length; i++)
                {
                    DeploymentSlot slot = deploymentSlots[i];
                    if (slot != null && slot.Cell == unit.Cell)
                    {
                        slotIndex = i;
                        return true;
                    }
                }
            }

            Vector3 unitPosition = unit.transform.position;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < deploymentSlots.Length; i++)
            {
                DeploymentSlot slot = deploymentSlots[i];
                if (slot?.Cell == null)
                {
                    continue;
                }

                float distance = (slot.Cell.transform.position - unitPosition).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    slotIndex = i;
                }
            }

            return slotIndex >= 0;
        }

        private bool TryGetDeploymentSlotIndexForCell(Cell cell, out int slotIndex)
        {
            slotIndex = -1;
            if (cell == null)
            {
                return false;
            }

            DeploymentSlot[] deploymentSlots = GetDeploymentSlots();
            for (int i = 0; i < deploymentSlots.Length; i++)
            {
                DeploymentSlot slot = deploymentSlots[i];
                if (slot != null && slot.Cell == cell)
                {
                    slotIndex = i;
                    return true;
                }
            }

            return false;
        }

        private void UpdateDeploymentSlotSelectionVisuals()
        {
            DeploymentSlot[] deploymentSlots = GetDeploymentSlots();
            for (int i = 0; i < deploymentSlots.Length; i++)
            {
                DeploymentSlot slot = deploymentSlots[i];
                if (slot != null)
                {
                    bool isSelected = IsPreBattleDeploymentSwapModeActive && i == selectedPreBattleDeploymentSlotIndex;
                    slot.SetSelected(isSelected);
                }
            }
        }

        private void ApplyStagedDeploymentRoster(IEnumerable<string> rosterUnitIds)
        {
            CampaignSaveData save = LoadSeededCampaignSave();
            string[] previousRoster = GetEditableDeploymentRoster(save, GetDeploymentSlots().Length);
            string[] roster = NormalizeDeploymentRoster(rosterUnitIds, save, GetDeploymentSlots().Length);
            if (AreStringSequencesEqual(previousRoster, roster))
            {
                return;
            }

            stagedDeploymentRosterUnitIds = roster;
            hasUnsavedDeploymentRosterChanges = !AreStringSequencesEqual(save.DeploymentRosterUnitIds, roster);
            ApplyFriendlyDeployment(GetDeploymentSlots(), save, roster);
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

            string[] normalizedRoster = NormalizeDeploymentRoster(stagedDeploymentRosterUnitIds, save, deploymentSlotCount);
            if (!AreStringSequencesEqual(stagedDeploymentRosterUnitIds, normalizedRoster))
            {
                stagedDeploymentRosterUnitIds = normalizedRoster;
            }

            return normalizedRoster;
        }

        private static Dictionary<string, OwnedUnitSaveData> BuildOwnedUnitsById(CampaignSaveData save)
        {
            return (save?.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>())
                .Where(unit => unit != null && !string.IsNullOrWhiteSpace(unit.UnitId))
                .GroupBy(unit => unit.UnitId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        }

        private Dictionary<string, UnitPreset> BuildVisualPresetsById()
        {
            return starterOwnedUnitPresets
                .Where(preset => preset != null && !string.IsNullOrWhiteSpace(preset.PresetId))
                .GroupBy(preset => preset.PresetId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        }

    }
}
