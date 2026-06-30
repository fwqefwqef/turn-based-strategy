using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Campaign;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Grid
{
    internal sealed class DeploymentSceneService
    {
        private readonly CellGrid grid;

        public DeploymentSceneService(CellGrid grid)
        {
            this.grid = grid;
        }

        public DeploymentSlot[] GetDeploymentSlots()
        {
            Transform root = grid.ResolveDeploymentSlotsParent();
            if (root != null)
            {
                return root.GetComponentsInChildren<DeploymentSlot>(true)
                    .Where(slot => slot != null && slot.gameObject.scene.IsValid())
                    .OrderBy(slot => slot.SlotIndex)
                    .ThenBy(slot => slot.name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            return Resources.FindObjectsOfTypeAll<DeploymentSlot>()
                .Where(slot => slot != null && slot.gameObject.scene.IsValid())
                .OrderBy(slot => slot.SlotIndex)
                .ThenBy(slot => slot.name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyList<Cell> GetDeploymentSlotCells()
        {
            return GetDeploymentSlots()
                .Where(slot => slot != null && slot.Cell != null)
                .OrderBy(slot => slot.SlotIndex)
                .Select(slot => slot.Cell)
                .ToArray();
        }

        public Cell GetPreferredDeploymentCell(int selectedSlotIndex)
        {
            DeploymentSlot[] slots = GetDeploymentSlots();
            if (slots.Length == 0)
            {
                return null;
            }

            if (selectedSlotIndex >= 0 && selectedSlotIndex < slots.Length)
            {
                return slots[selectedSlotIndex]?.Cell;
            }

            return slots
                .Where(slot => slot != null && slot.Cell != null)
                .OrderBy(slot => slot.SlotIndex)
                .Select(slot => slot.Cell)
                .FirstOrDefault();
        }

        public void ApplyFriendlyDeployment(CampaignSaveData save, IReadOnlyList<string> rosterOverride, IReadOnlyDictionary<string, UnitPreset> visualPresetsById)
        {
            DeploymentSlot[] deploymentSlots = GetDeploymentSlots();
            EnsureFriendlyDeploymentUnitCapacity(deploymentSlots.Length);
            List<Unit> deploymentUnits = GetFriendlyDeploymentUnits();
            if (deploymentUnits.Count == 0)
            {
                Debug.LogWarning("CellGrid: No friendly unit placeholders were found for deployment.");
                return;
            }

            Dictionary<string, OwnedUnitSaveData> ownedUnitsById = DeploymentRosterUtility.BuildOwnedUnitsById(save);

            foreach (Unit deploymentUnit in deploymentUnits)
            {
                deploymentUnit.gameObject.SetActive(false);
                ReleaseDeploymentUnitCell(deploymentUnit);
                deploymentUnit.ExcludedFromBattle = true;
                grid.UnregisterDeploymentUnitFromBattleInternal(deploymentUnit);
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
                    visualPresetsById?.TryGetValue(ownedUnit.VisualId, out visualPreset);
                }

                Unit deploymentUnit = deploymentUnits[i];
                deploymentUnit.ConfigureFromOwnedUnitSaveData(ownedUnit, visualPreset);
                deploymentUnit.gameObject.SetActive(true);
                grid.RegisterDeploymentUnitForBattleInternal(deploymentUnit, deploymentSlots[i].Cell);
                AssignDeploymentUnitToSlot(deploymentUnit, deploymentSlots[i]);
                deploymentUnit.ExcludedFromBattle = false;
            }

            if (deploymentUnits.Count < deploymentSlots.Length)
            {
                Debug.LogWarning($"CellGrid: Found {deploymentSlots.Length} deployment slots but only {deploymentUnits.Count} friendly unit placeholders.");
            }

            grid.RebuildSceneCellOccupancyForDeploymentInternal();
        }

        public bool TryGetDeploymentSlotIndexForUnit(Unit unit, out int slotIndex)
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

        public bool TryGetDeploymentSlotIndexForCell(Cell cell, out int slotIndex)
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

        public void SetDeploymentSlotVisibility(bool isVisible)
        {
            foreach (DeploymentSlot slot in GetDeploymentSlots())
            {
                if (slot == null)
                {
                    continue;
                }

                slot.SyncToCell();
                slot.gameObject.SetActive(isVisible);
            }
        }

        public void UpdateDeploymentSlotSelectionVisuals(bool isSwapModeActive, int selectedSlotIndex)
        {
            DeploymentSlot[] deploymentSlots = GetDeploymentSlots();
            for (int i = 0; i < deploymentSlots.Length; i++)
            {
                DeploymentSlot slot = deploymentSlots[i];
                if (slot != null)
                {
                    slot.SetSelected(isSwapModeActive && i == selectedSlotIndex);
                }
            }
        }

        private List<Unit> GetFriendlyDeploymentUnits()
        {
            return grid.GetAllSceneUnitsFromHierarchy(includeExcludedFromBattle: true)
                .Where(unit => unit != null && unit.PlayerNumber == 0 && unit.ParticipatesInDeploymentRoster)
                .ToList();
        }

        private void EnsureFriendlyDeploymentUnitCapacity(int requiredCount)
        {
            if (requiredCount <= 0)
            {
                return;
            }

            Transform unitsParent = grid.GetSceneUnitsParent();
            if (unitsParent == null)
            {
                return;
            }

            List<Unit> deploymentUnits = GetFriendlyDeploymentUnits();
            if (deploymentUnits.Count >= requiredCount)
            {
                return;
            }

            Unit templateUnit = ResolveDeploymentUnitTemplate(deploymentUnits);
            if (templateUnit == null)
            {
                Debug.LogWarning("CellGrid: No deployment unit prefab or existing friendly deployment template was found.");
                return;
            }

            while (deploymentUnits.Count < requiredCount)
            {
                Unit clonedUnit = UnityEngine.Object.Instantiate(templateUnit, unitsParent);
                clonedUnit.name = $"{templateUnit.name}_Deployment_{deploymentUnits.Count}";
                clonedUnit.gameObject.SetActive(false);
                clonedUnit.ExcludedFromBattle = true;
                clonedUnit.ParticipatesInDeploymentRoster = true;
                clonedUnit.IncludeInOwnedUnitSave = true;
                deploymentUnits.Add(clonedUnit);
            }
        }

        private Unit ResolveDeploymentUnitTemplate(IReadOnlyList<Unit> existingDeploymentUnits)
        {
            Unit prefab = grid.GetDeploymentRosterUnitPrefab();
            if (prefab != null)
            {
                return prefab;
            }

            return existingDeploymentUnits?.FirstOrDefault();
        }

        private static void AssignDeploymentUnitToSlot(Unit unit, DeploymentSlot slot)
        {
            if (unit == null || slot == null || slot.Cell == null)
            {
                return;
            }

            Cell previousCell = unit.Cell;
            Cell nextCell = slot.Cell;
            bool shouldSyncOccupancy = Application.isPlaying;

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

        private static void RefreshDeploymentCellOccupancy(Cell cell)
        {
            Unit.RefreshCellOccupancy(cell);
        }
    }
}
