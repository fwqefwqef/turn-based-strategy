using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Grid;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CellGrid
    {
        // --- Runtime grid collection/metadata mirror (push-only until Phase 8c) ---
        private RuntimeGrid runtimeGrid;
        private bool runtimeGridCollectionsDirty = true;

        private void ResolveRuntimeGrid()
        {
            if (runtimeGrid == null)
            {
                runtimeGrid = GetComponent<RuntimeGrid>();
            }
        }

        private void SyncRuntimeGrid()
        {
            ResolveRuntimeGrid();
            if (runtimeGrid == null)
            {
                return;
            }

            RefreshRuntimeGridCollections();
            UpdateRuntimeGridMetadata();
        }

        private void RefreshRuntimeGridCollections()
        {
            if (!runtimeGridCollectionsDirty)
            {
                return;
            }

            runtimeGrid.SetMirroredCollections(
                GetAllCells(),
                GetAllUnits().Select(GetRuntimeUnit),
                GetOrderedPlayers().Cast<Runtime.Players.IBattlePlayer>());
            runtimeGridCollectionsDirty = false;
        }

        private void UpdateRuntimeGridMetadata()
        {
            runtimeGrid.SetBattleStarted(!IsPreBattlePhase);

            if (CurrentPlayer != null)
            {
                runtimeGrid.SetCurrentPlayerById(CurrentPlayer.PlayerNumber);
            }
        }

        [ContextMenu("Sync Runtime Mirror Now")]
        public void SyncRuntimeMirrorNow()
        {
            MarkRuntimeGridDirty();

            foreach (Unit unit in GetAllUnits())
            {
                unit?.SyncMirroredRuntimeNow();
            }

            SyncRuntimeGrid();
        }

        public void ClearAllCellHighlights()
        {
            foreach (Cell cell in GetAllCells())
            {
                if (cell == null)
                {
                    continue;
                }

                cell.UnMark();
                cell?.ClearHighlight();
            }
        }

        private void MarkRuntimeGridDirty()
        {
            runtimeGridCollectionsDirty = true;
        }

        private static GridUnit GetRuntimeUnit(Unit unit)
        {
            return unit != null ? unit.GetComponent<GridUnit>() : null;
        }

        internal void SyncRuntimeMirrorForAiTurn()
        {
            SyncRuntimeMirrorNow();
        }

        internal void CommitPendingMoveOnSceneUnit(Unit unit, bool consumeAllRemainingMovement = false)
        {
            if (unit == null || !unit.HasPendingMove)
            {
                return;
            }

            if (!unit.ConfirmPendingMove(consumeAllRemainingMovement))
            {
                return;
            }

            unit.SyncMirroredRuntimeNow();
            RefreshSceneCellOccupancyNow();
        }

        internal List<Unit> GetAttackableEnemiesFromActingCell(Unit actor, Cell actingCell)
        {
            if (actor == null || actingCell == null)
            {
                return new List<Unit>();
            }

            return GetEnemyUnits(CurrentPlayer)
                .Where(enemy => enemy != null && actor.CanAttackTargetWithAnyWeapon(enemy, actingCell))
                .ToList();
        }

        public void ProcessSceneRightClick()
        {
            if (CurrentState is States.IRightClickHandler handler)
            {
                handler.OnRightClick();
            }
        }
    }
}
