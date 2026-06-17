using System.Collections.Generic;
using TbsFramework.Cells;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Units
{
    public partial class CustomUnit
    {
        internal void SyncMirroredRuntimeCell(Cell legacyCell)
        {
            BattleUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            if (legacyCell == null)
            {
                runtimeUnit.ClearCurrentCell();
                return;
            }

            BoardCell runtimeCell = ResolveLinkedRuntimeCell(legacyCell);
            if (runtimeCell == null)
            {
                runtimeUnit.ClearCurrentCell();
                return;
            }

            runtimeUnit.AssignCellImmediate(runtimeCell, syncTransform: false);
        }

        internal void ClearMirroredRuntimeCell()
        {
            BattleUnit runtimeUnit = ResolveRuntimeUnit();
            runtimeUnit?.ClearCurrentCell();
        }

        internal void SyncMirroredRuntimeNow()
        {
            SyncMirroredRuntimeMovementPoints();
            SyncMirroredRuntimeTurnState();
            SyncMirroredRuntimeCell(Cell);
            SyncMirroredRuntimePendingMove();
        }

        private void SyncMirroredRuntimePendingMove()
        {
            BattleUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            if (!HasPendingMove)
            {
                if (runtimeUnit.HasPendingMove)
                {
                    runtimeUnit.CancelPendingMove();
                }

                return;
            }

            PendingMove pending = _pendingMove.Value;
            BoardCell runtimeDestination = ResolveLinkedRuntimeCell(pending.ToCell);
            if (runtimeDestination == null)
            {
                return;
            }

            if (runtimeUnit.HasPendingMove && runtimeUnit.PreviewCell == runtimeDestination)
            {
                return;
            }

            if (pending.ToCell == pending.FromCell)
            {
                runtimeUnit.BeginPendingMoveInPlace();
                return;
            }

            if (TryBuildRuntimeMovementPath(pending.Path, out List<BoardCell> runtimePath))
            {
                runtimeUnit.BeginPendingMove(runtimeDestination, runtimePath);
            }
        }

        private void SyncMirroredRuntimeTurnState()
        {
            BattleUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            UnitTurnState runtimeState = CurrentTurnStateKind switch
            {
                UnitTurnStateKind.Selected => new UnitTurnStateSelected(runtimeUnit),
                UnitTurnStateKind.ReachableEnemy => new UnitTurnStateReachableEnemy(runtimeUnit),
                UnitTurnStateKind.Friendly => new UnitTurnStateFriendly(runtimeUnit),
                UnitTurnStateKind.Finished => new UnitTurnStateFinished(runtimeUnit),
                _ => new UnitTurnStateNormal(runtimeUnit)
            };

            runtimeUnit.SetState(runtimeState);
        }

        private void SyncMirroredRuntimeMovementPoints()
        {
            BattleUnit runtimeUnit = ResolveRuntimeUnit();
            if (runtimeUnit == null)
            {
                return;
            }

            runtimeUnit.SetMovementPointsRemaining(MovementPoints);
        }

        private BattleUnit ResolveRuntimeUnit()
        {
            return GetComponent<BattleUnit>();
        }

        private static BoardCell ResolveLinkedRuntimeCell(Cell cell)
        {
            return cell != null ? cell.GetComponent<BoardCell>() : null;
        }

        private static Cell ResolveLinkedLegacyCell(BoardCell cell)
        {
            return cell != null ? cell.GetComponent<Cell>() : null;
        }
    }
}
