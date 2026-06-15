using System.Linq;
using TbsFramework.Cells;
using UnityEngine;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Board.States;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CustomCellGrid
    {
        private BattleBoard runtimeBoard;
        private bool runtimeBoardCollectionsDirty = true;

        private void NormalizeHumanInputState()
        {
            if (!IsHumanTurn)
            {
                return;
            }

            if (IsLegacyWaitingForInputState(cellGridState))
            {
                SetState(new CustomCellGridStateWaitingForInput(this));
            }
        }

        private static bool IsLegacyWaitingForInputState(object state)
        {
            return state != null
                && state.GetType() != typeof(LegacyCustomCellGridStateAdapter)
                && state.GetType().Name == "CellGridStateWaitingForInput";
        }

        private void ResolveRuntimeBoard()
        {
            if (runtimeBoard == null)
            {
                runtimeBoard = GetComponent<BattleBoard>();
            }
        }

        private void ApplyRuntimeBoardMirror()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            RefreshRuntimeBoardCollectionsIfNeeded();
            UpdateRuntimeBoardMetadata();
        }

        private void RefreshRuntimeBoardCollectionsIfNeeded()
        {
            if (!runtimeBoardCollectionsDirty)
            {
                return;
            }

            runtimeBoard.SetMirroredCollections(
                GetAllCells().Select(GetRuntimeCell),
                GetAllCustomUnits().Select(GetRuntimeUnit),
                GetOrderedCustomPlayers().Cast<Runtime.Players.IBattlePlayer>());
            runtimeBoardCollectionsDirty = false;
        }

        private void UpdateRuntimeBoardMetadata()
        {
            runtimeBoard.SetBattleStarted(!IsPreBattlePhase);

            if (CurrentCustomPlayer != null)
            {
                runtimeBoard.SetCurrentPlayerById(CurrentCustomPlayer.PlayerNumber);
            }
        }

        [ContextMenu("Sync Runtime Mirror Now")]
        public void SyncRuntimeMirrorNow()
        {
            MarkRuntimeBoardDirty();

            foreach (CustomUnit unit in GetAllCustomUnits())
            {
                unit?.SyncMirroredRuntimeNow();
            }

            ApplyRuntimeBoardMirror();
        }

        private void MarkRuntimeBoardDirty()
        {
            runtimeBoardCollectionsDirty = true;
        }

        /// <summary>
        /// Shadow-harness hook: drives the runtime board's real waiting-for-input logic with the
        /// same clicked unit the framework just processed, then logs whether the runtime would
        /// select the same unit the framework selected. Non-authoritative — the runtime board is
        /// only evaluated, never committed.
        /// </summary>
        internal void ShadowCompareSelection(CustomUnit clickedUnit, CustomUnit frameworkSelectedUnit)
        {
            if (!Diagnostics.RuntimeParityDiagnostics.LogSelectionParity)
            {
                return;
            }

            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            SyncRuntimeMirrorNow();

            BattleUnit runtimeClicked = GetRuntimeUnit(clickedUnit);
            BattleUnit runtimeDecided = runtimeBoard.ShadowEvaluateUnitClickFromWaiting(runtimeClicked);
            BattleUnit expectedRuntime = GetRuntimeUnit(frameworkSelectedUnit);

            Diagnostics.RuntimeParityDiagnostics.CompareSelection(
                clickedUnit, frameworkSelectedUnit, runtimeDecided, expectedRuntime);
        }

        internal void ShadowCompareRightClick(CustomUnit previouslySelectedUnit, CustomUnit frameworkSelectedUnitAfterRightClick)
        {
            if (!Diagnostics.RuntimeParityDiagnostics.LogRightClickParity)
            {
                return;
            }

            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            SyncRuntimeMirrorNow();

            BattleUnit runtimePreviouslySelected = GetRuntimeUnit(previouslySelectedUnit);
            BattleUnit runtimeSelectedAfterRightClick = runtimeBoard.ShadowEvaluateRightClickFromSelected(runtimePreviouslySelected);
            BattleUnit expectedRuntimeAfterRightClick = GetRuntimeUnit(frameworkSelectedUnitAfterRightClick);

            Diagnostics.RuntimeParityDiagnostics.CompareRightClick(
                previouslySelectedUnit,
                frameworkSelectedUnitAfterRightClick,
                runtimeSelectedAfterRightClick,
                expectedRuntimeAfterRightClick);
        }

        internal void ShadowCompareSelectedStateUnitClick(
            CustomUnit previouslySelectedUnit,
            CustomUnit clickedUnit,
            string frameworkStateLabel,
            CustomUnit frameworkSelectedUnitAfterClick,
            Cell frameworkPendingDestination)
        {
            if (!Diagnostics.RuntimeParityDiagnostics.LogSelectedMoveParity)
            {
                return;
            }

            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            SyncRuntimeMirrorNow();

            BattleUnit runtimePreviouslySelected = GetRuntimeUnit(previouslySelectedUnit);
            BattleUnit runtimeClicked = GetRuntimeUnit(clickedUnit);
            BoardState runtimeState = runtimeBoard.ShadowEvaluateUnitClickFromSelected(runtimePreviouslySelected, runtimeClicked);
            BattleUnit expectedRuntimeSelected = GetRuntimeUnit(frameworkSelectedUnitAfterClick);
            BoardCell expectedRuntimePendingDestination = GetRuntimeCell(frameworkPendingDestination);

            Diagnostics.RuntimeParityDiagnostics.CompareSelectedStateTransition(
                $"Unit click on {Describe(clickedUnit)}",
                previouslySelectedUnit,
                frameworkStateLabel,
                frameworkSelectedUnitAfterClick,
                frameworkPendingDestination,
                runtimeState,
                expectedRuntimeSelected,
                expectedRuntimePendingDestination);
        }

        internal void ShadowCompareSelectedStateCellClick(
            CustomUnit previouslySelectedUnit,
            Cell clickedCell,
            string frameworkStateLabel,
            CustomUnit frameworkSelectedUnitAfterClick,
            Cell frameworkPendingDestination)
        {
            if (!Diagnostics.RuntimeParityDiagnostics.LogSelectedMoveParity)
            {
                return;
            }

            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            SyncRuntimeMirrorNow();

            BattleUnit runtimePreviouslySelected = GetRuntimeUnit(previouslySelectedUnit);
            BoardCell runtimeClicked = GetRuntimeCell(clickedCell);
            BoardState runtimeState = runtimeBoard.ShadowEvaluateCellClickFromSelected(runtimePreviouslySelected, runtimeClicked);
            BattleUnit expectedRuntimeSelected = GetRuntimeUnit(frameworkSelectedUnitAfterClick);
            BoardCell expectedRuntimePendingDestination = GetRuntimeCell(frameworkPendingDestination);

            Diagnostics.RuntimeParityDiagnostics.CompareSelectedStateTransition(
                $"Cell click on {Describe(clickedCell)}",
                previouslySelectedUnit,
                frameworkStateLabel,
                frameworkSelectedUnitAfterClick,
                frameworkPendingDestination,
                runtimeState,
                expectedRuntimeSelected,
                expectedRuntimePendingDestination);
        }

        private static BattleUnit GetRuntimeUnit(CustomUnit unit)
        {
            return unit != null ? unit.GetComponent<BattleUnit>() : null;
        }

        private static BoardCell GetRuntimeCell(Cell cell)
        {
            return cell != null ? cell.GetComponent<BoardCell>() : null;
        }

        private static string Describe(CustomUnit unit)
        {
            return unit == null ? "<none>" : unit.name;
        }

        private static string Describe(Cell cell)
        {
            return cell == null ? "<none>" : cell.OffsetCoord.ToString();
        }
    }
}
