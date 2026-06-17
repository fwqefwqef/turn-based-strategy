using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CellGrid
    {
        internal static BattleSquareCell ResolveBattleSquareFromRegistryCell(object cell)
        {
            return cell as BattleSquareCell;
        }

        internal static IBattleCell ResolveBattleCellFromRegistryCell(object cell)
        {
            return ResolveBattleSquareFromRegistryCell(cell);
        }

        internal static BattleSquareCell ResolveRegistryCellFromBattleCell(IBattleCell cell)
        {
            return cell as BattleSquareCell;
        }

        internal static Unit ResolveUnitFromRegistryUnit(object unit)
        {
            return unit as Unit;
        }

        internal static IBoardUnit ResolveBoardUnitFromRegistryUnit(object unit)
        {
            return ResolveUnitFromRegistryUnit(unit);
        }

        public void RefreshSceneCellOccupancyNow()
        {
            RebuildSceneCellOccupancy();
        }

        internal void RegisterSceneUnitTransform(
            Transform unitTransform,
            BattleSquareCell targetCell = null,
            Player ownerPlayer = null)
        {
            Unit customUnit = unitTransform != null ? unitTransform.GetComponent<Unit>() : null;
            if (customUnit == null)
            {
                Debug.LogError("CellGrid: RegisterSceneUnitTransform requires a Unit component.");
                return;
            }

            if (IsUnitRegistered(customUnit))
            {
                return;
            }

            int assignedUnitId = AllocateNextUnitId();
            customUnit.UnitID = assignedUnitId;
            registeredUnits.Add(customUnit);

            if (targetCell != null)
            {
                customUnit.Cell = targetCell;
                customUnit.transform.localPosition = targetCell.transform.localPosition;
            }

            if (ownerPlayer != null)
            {
                customUnit.PlayerNumber = ownerPlayer.PlayerNumber;
            }

            customUnit.RegisterCellOccupancy();
            customUnit.transform.localRotation = Quaternion.Euler(0, 0, 0);
            customUnit.Initialize();

            customUnit.UnitClicked += OnSceneUnitClicked;
            customUnit.UnitHighlighted += OnSceneUnitHighlighted;
            customUnit.UnitDehighlighted += OnSceneUnitDehighlighted;
            customUnit.UnitDestroyed += OnSceneUnitDestroyed;

            NotifySceneUnitAdded(unitTransform);
        }

        protected void RegisterSceneUnit(Unit unit, BattleSquareCell targetCell = null, Player ownerPlayer = null)
        {
            if (unit == null || IsUnitRegistered(unit))
            {
                return;
            }

            RegisterSceneUnitTransform(unit.transform, targetCell, ownerPlayer);
        }

        protected bool IsUnitRegistered(Unit unit)
        {
            return unit != null && registeredUnits.Contains(unit);
        }

        protected void UnregisterSceneUnit(Unit unit)
        {
            if (unit == null || !registeredUnits.Remove(unit))
            {
                return;
            }

            unit.UnitClicked -= OnSceneUnitClicked;
            unit.UnitHighlighted -= OnSceneUnitHighlighted;
            unit.UnitDehighlighted -= OnSceneUnitDehighlighted;
            unit.UnitDestroyed -= OnSceneUnitDestroyed;
        }

        private void EnsureSceneCellAnchors()
        {
            foreach (BattleSquareCell tile in GetComponentsInChildren<BattleSquareCell>(true))
            {
                if (tile == null)
                {
                    continue;
                }
            }

            EnsureDeploymentSlotCellBindings();
        }

        private void EnsureDeploymentSlotCellBindings()
        {
            BattleSquareCell[] tiles = GetComponentsInChildren<BattleSquareCell>(true);
            foreach (DeploymentSlot slot in GetDeploymentSlots())
            {
                slot?.EnsureRegistryCellBinding(tiles);
            }
        }

        private void RebuildSceneCellOccupancy()
        {
            List<BattleSquareCell> allCells = GetAllBoardCells();
            if (allCells.Count == 0)
            {
                return;
            }

            foreach (BattleSquareCell cell in allCells)
            {
                if (cell == null)
                {
                    continue;
                }

                cell.ClearOccupants();
            }

            foreach (Unit unit in GetAllSceneUnitsFromHierarchy())
            {
                if (unit == null || unit.ExcludedFromBattle || unit.Cell == null)
                {
                    continue;
                }

                BoardUnit runtimeUnit = unit.GetComponent<BoardUnit>();
                runtimeUnit?.AssignCellImmediate(unit.Cell, syncTransform: false);
            }

            occupancyRevision++;
            Unit.InvalidateAllCachedPaths();
        }

        private static bool IsCellBlockedByTerrain(BattleSquareCell cell)
        {
            return cell != null && !cell.IsTraversable;
        }
    }
}
