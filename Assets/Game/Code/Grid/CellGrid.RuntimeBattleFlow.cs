using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CellGrid
    {
        internal bool ProcessRuntimeRoutedBattleOutcomeEvaluation()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                bool legacyFinished = CheckGameFinished();
                if (legacyFinished)
                {
                    SyncStateToGameOver();
                }

                return legacyFinished;
            }

            if (GameFinished)
            {
                SyncStateToGameOver();
                return true;
            }

            SyncRuntimeMirrorNow();
            BattleOutcome runtimeOutcomeAuthority = runtimeBoard.EvaluateBattleOutcome();
            bool finished = TryApplyBattleOutcome(runtimeOutcomeAuthority);
            if (finished)
            {
                SyncStateToGameOver();
            }

            return finished;
        }

        internal void ProcessRuntimeRoutedCombatPresentationBegan()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            SyncRuntimeMirrorNow();

            if (IsHumanTurn && CurrentState is not States.CellGridStateBlockInput)
            {
                EnterLegacyBlockedInputState();
            }
        }

        internal void ProcessRuntimeRoutedCombatPresentationEnded()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                return;
            }

            SyncRuntimeMirrorNow();
            RefreshSceneCellOccupancyNow();
            TryFlushDeferredDestroyQueue();

            if (ShouldRouteBattleOutcomeThroughRuntime)
            {
                RequestBattleOutcomeEvaluation();
            }
        }

        internal void ProcessRuntimeRoutedPostCombatRecovery()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                EnterWaitingState();
                return;
            }

            SyncRuntimeMirrorNow();
            TryFlushDeferredDestroyQueue();

            if (RequestBattleOutcomeEvaluation())
            {
                return;
            }

            ApplyRuntimeDrivenState(
                new Runtime.Board.States.BoardStateWaitingForInput(runtimeBoard),
                EnterWaitingState);
        }

        internal void PrepareRuntimeRoutedAiTurn()
        {
            SyncRuntimeMirrorNow();
        }

        internal void EndUnitsForCurrentPlayerTurnViaUnits()
        {
            if (ShouldRouteTurnLoopThroughRuntime)
            {
                ApplyLegacyTurnEndToCurrentPlayerUnits();
                return;
            }

            EndUnitsForCurrentPlayerTurn();
        }

        internal void ProcessRuntimeRoutedEndTurn()
        {
            ResolveRuntimeBoard();
            if (runtimeBoard == null)
            {
                EndTurn();
                return;
            }

            EnterBlockedInputState();
            if (RequestBattleOutcomeEvaluation())
            {
                return;
            }

            EndUnitsForCurrentPlayerTurnViaUnits();

            RoundRobinTurnPlan plan = RoundRobinBattleFlow.ResolveTurn(this);
            if (plan.NextPlayer == null)
            {
                Debug.LogError("CellGrid: No valid battle turn resolver or next player was found.");
                return;
            }

            PrepareRuntimeTurnStartForPlan(plan);
            runtimeBoard.EndCurrentTurn(kickTurnPlayerPlay: false);
            ApplyLegacyStateFromRuntime(() => CommitTurnTransition(plan, kickPlayerPlay: false, syncUnitTurnHooks: false));
            ApplyRuntimeTurnStartToLegacyPlayableUnits();
            SyncRuntimeMirrorNow();
            runtimeBoard.KickCurrentTurnPlay();
        }

        internal BattleSquareCell ResolveRuntimeActingCell(Unit unit)
        {
            BoardUnit runtimeUnit = GetRuntimeUnit(unit);
            if (runtimeUnit == null)
            {
                return unit?.Cell;
            }

            BoardCell runtimeActingCell = runtimeUnit.PreviewCell;
            if (runtimeActingCell != null)
            {
                return ResolveSceneCell(runtimeActingCell) ?? unit?.PreviewCell ?? unit?.Cell;
            }

            return unit?.Cell;
        }

        internal List<Unit> GetAttackableEnemiesFromActingCell(Unit actor, BattleSquareCell actingCell)
        {
            if (actor == null || actingCell == null)
            {
                return new List<Unit>();
            }

            return GetEnemyUnits(CurrentPlayer)
                .Where(enemy => enemy != null && actor.CanAttackTargetWithAnyWeapon(enemy, actingCell))
                .ToList();
        }
    }
}
