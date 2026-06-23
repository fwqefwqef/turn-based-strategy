using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Players;

namespace Windy.Srpg.Runtime.AI
{
    public abstract class AiDecisionAction : MonoBehaviour
    {
        public abstract void InitializeDecision(IBattlePlayer player, Unit unit, CellGrid grid);
        public abstract bool ShouldExecute(IBattlePlayer player, Unit unit, CellGrid grid);
        public abstract void Precalculate(IBattlePlayer player, Unit unit, CellGrid grid);
        public abstract IEnumerator ExecuteDecision(IBattlePlayer player, Unit unit, CellGrid grid);
        public abstract void CleanUpDecision(IBattlePlayer player, Unit unit, CellGrid grid);
        public abstract void ShowDebugDecisionInfo(IBattlePlayer player, Unit unit, CellGrid grid);
    }

    public static class AiTurnRunner
    {
        public static IEnumerator ExecuteTurn(
            Player player,
            IEnumerable<Unit> orderedUnits,
            CellGrid grid,
            Action onTurnCompleted = null)
        {
            return ExecuteTurn((IBattlePlayer)player, orderedUnits, grid, onTurnCompleted);
        }

        public static IEnumerator ExecuteTurn(
            IBattlePlayer player,
            IEnumerable<Unit> orderedUnits,
            CellGrid grid,
            Action onTurnCompleted = null)
        {
            List<Unit> units = orderedUnits?
                .Where(unit => unit != null)
                .ToList()
                ?? new List<Unit>();

            foreach (Unit unit in units)
            {
                AiDecisionAction[] actions = unit.GetComponentsInChildren<AiDecisionAction>(true)
                    ?? Array.Empty<AiDecisionAction>();

                foreach (AiDecisionAction action in actions)
                {
                    if (action == null || unit == null)
                    {
                        break;
                    }

                    yield return null;

                    action.InitializeDecision(player, unit, grid);
                    bool shouldExecute = action.ShouldExecute(player, unit, grid);
                    if (shouldExecute)
                    {
                        yield return null;
                        action.Precalculate(player, unit, grid);
                        yield return null;

                        IEnumerator execution = action.ExecuteDecision(player, unit, grid);
                        if (execution != null)
                        {
                            yield return execution;
                        }
                    }

                    if (action == null || unit == null)
                    {
                        break;
                    }

                    action.CleanUpDecision(player, unit, grid);
                }
            }

            onTurnCompleted?.Invoke();
        }
    }

    public static class AiTurnOrdering
    {
        public static IReadOnlyList<Unit> OrderByMovementFreedom(IEnumerable<Unit> units, CellGrid cellGrid)
        {
            return units?
                .Where(unit => unit != null)
                .OrderByDescending(unit => CountTraversableNeighbors(unit, cellGrid))
                .ToList()
                ?? new List<Unit>();
        }

        private static int CountTraversableNeighbors(Unit unit, CellGrid cellGrid)
        {
            if (unit?.Cell == null || cellGrid == null)
            {
                return 0;
            }

            return unit.Cell
                .GetNeighbours(cellGrid.GetAllCells())
                .Count(unit.IsCellTraversable);
        }
    }
}
