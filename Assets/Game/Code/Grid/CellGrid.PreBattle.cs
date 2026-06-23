using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using Windy.Srpg.Game.Campaign;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Grid;

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
            Debug.Log($"CellGrid: Wrote owned unit save to '{CampaignSaveManager.SavePath}'.");
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
        // --- Pre-battle deployment ---
        public IReadOnlyList<OwnedUnitSaveData> GetOwnedUnitsForPreBattle()
        {
            return (LoadSeededCampaignSave()?.OwnedUnits ?? Array.Empty<OwnedUnitSaveData>()).ToArray();
        }

        public int GetDeploymentSlotCount()
        {
            return GetDeploymentSlots().Length;
        }

        public IReadOnlyList<Cell> GetPreBattleDeploymentSlotCells()
        {
            return GetDeploymentSlots()
                .Where(slot => slot != null && slot.Cell != null)
                .OrderBy(slot => slot.SlotIndex)
                .Select(slot => slot.Cell)
                .ToArray();
        }

        public Cell GetPreferredPreBattleDeploymentCell()
        {
            DeploymentSlot[] deploymentSlots = GetDeploymentSlots();
            if (deploymentSlots.Length == 0)
            {
                return null;
            }

            if (selectedPreBattleDeploymentSlotIndex >= 0 && selectedPreBattleDeploymentSlotIndex < deploymentSlots.Length)
            {
                return deploymentSlots[selectedPreBattleDeploymentSlotIndex]?.Cell;
            }

            return deploymentSlots
                .Where(slot => slot != null && slot.Cell != null)
                .OrderBy(slot => slot.SlotIndex)
                .Select(slot => slot.Cell)
                .FirstOrDefault();
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
            List<Unit> deploymentUnits = GetFriendlyDeploymentUnits();
            if (deploymentUnits.Count == 0)
            {
                Debug.LogWarning("CellGrid: No friendly unit placeholders were found for deployment.");
                return;
            }

            Dictionary<string, OwnedUnitSaveData> ownedUnitsById = BuildOwnedUnitsById(save);
            Dictionary<string, UnitPreset> presetsById = BuildVisualPresetsById();

            foreach (Unit deploymentUnit in deploymentUnits)
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

                Unit deploymentUnit = deploymentUnits[i];
                deploymentUnit.ConfigureFromOwnedUnitSaveData(ownedUnit, visualPreset);
                deploymentUnit.gameObject.SetActive(true);
                RegisterDeploymentUnitForBattle(deploymentUnit, deploymentSlots[i].Cell);
                AssignDeploymentUnitToSlot(deploymentUnit, deploymentSlots[i]);
                deploymentUnit.ExcludedFromBattle = false;
            }

            if (deploymentUnits.Count < deploymentSlots.Length)
            {
                Debug.LogWarning($"CellGrid: Found {deploymentSlots.Length} deployment slots but only {deploymentUnits.Count} friendly unit placeholders.");
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

            Dictionary<Cell, Unit> friendlyUnitsByCell = GetFriendlyDeploymentUnits()
                .Where(unit => unit != null && unit.Cell != null && !unit.ExcludedFromBattle)
                .GroupBy(unit => unit.Cell)
                .ToDictionary(group => group.Key, group => group.First());

            List<string> roster = new List<string>(deploymentSlotCount);
            int slotCount = Mathf.Min(deploymentSlotCount, deploymentSlots.Length);
            for (int i = 0; i < slotCount; i++)
            {
                DeploymentSlot slot = deploymentSlots[i];
                if (slot?.Cell == null || !friendlyUnitsByCell.TryGetValue(slot.Cell, out Unit unit) || string.IsNullOrWhiteSpace(unit.UnitId))
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

        private List<Unit> GetFriendlyDeploymentUnits()
        {
            return GetAllSceneUnitsFromHierarchy(includeExcludedFromBattle: true)
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

            List<Unit> deploymentUnits = GetFriendlyDeploymentUnits();
            if (deploymentUnits.Count >= requiredCount)
            {
                return;
            }

            Unit templateUnit = deploymentUnits.FirstOrDefault();
            if (templateUnit == null)
            {
                return;
            }

            while (deploymentUnits.Count < requiredCount)
            {
                Unit clonedUnit = UnityEngine.Object.Instantiate(templateUnit, unitsParent);
                clonedUnit.name = $"{templateUnit.name}_Deployment_{deploymentUnits.Count}";
                clonedUnit.gameObject.SetActive(false);
                clonedUnit.ExcludedFromBattle = true;
                deploymentUnits.Add(clonedUnit);
            }
        }

        private void RegisterDeploymentUnitForBattle(Unit unit, Cell targetCell)
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
        }

        private void UnregisterDeploymentUnitFromBattle(Unit unit)
        {
            if (unit == null
                || !Application.isPlaying
                || !CanModifyRegisteredSceneUnits()
                || !IsUnitRegistered(unit))
            {
                return;
            }

            unit.CombatDestroyed -= OnCombatDestroyed;
            unit.DestroyedInCombat -= OnUnitDestroyed;
            subscribedUnits.Remove(unit);
            UnregisterSceneUnit(unit);
        }

        private bool CanModifyRegisteredSceneUnits()
        {
            return Players != null && Cells != null && Units != null;
        }

        private static void AssignDeploymentUnitToSlot(Unit unit, DeploymentSlot slot)
        {
            if (unit == null || slot == null || slot.Cell == null)
            {
                return;
            }

            Cell previousCell = unit.Cell;
            Cell nextCell = slot.Cell;
            bool shouldSyncOccupancy = ShouldSyncCellOccupancy(unit);

            if (shouldSyncOccupancy && previousCell != null && previousCell != nextCell)
            {
                previousCell.CurrentUnits.Remove(unit);
                RefreshDeploymentCellOccupancy(previousCell);
            }

            unit.Cell = nextCell;

            if (shouldSyncOccupancy)
            {
                if (!nextCell.CurrentUnits.Contains(unit))
                {
                    nextCell.CurrentUnits.Add(unit);
                }

                RefreshDeploymentCellOccupancy(nextCell);
            }
            else
            {
                nextCell.IsTaken = unit.Obstructable;
            }

            unit.transform.localPosition = nextCell.transform.localPosition;
        }

        private static void ReleaseDeploymentUnitCell(Unit unit)
        {
            if (unit?.Cell == null)
            {
                return;
            }

            Cell currentCell = unit.Cell;
            currentCell.CurrentUnits.Remove(unit);
            RefreshDeploymentCellOccupancy(currentCell);
            unit.Cell = null;
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

        private bool TryGetDeploymentSlotIndexForUnit(Unit unit, out int slotIndex)
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

        private static bool ShouldSyncCellOccupancy(Unit unit)
        {
            return Application.isPlaying && unit != null;
        }

        private static void RefreshDeploymentCellOccupancy(Cell cell)
        {
            Unit.RefreshCellOccupancy(cell);
        }
    }
}
