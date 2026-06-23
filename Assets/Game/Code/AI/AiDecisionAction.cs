using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Grid;
using Windy.Srpg.Runtime.Players;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.AI
{
    public abstract class AiDecisionAction : MonoBehaviour
    {
        public abstract void InitializeDecision(IBattlePlayer player, IGridUnit unit, IGridContext grid);
        public abstract bool ShouldExecute(IBattlePlayer player, IGridUnit unit, IGridContext grid);
        public abstract void Precalculate(IBattlePlayer player, IGridUnit unit, IGridContext grid);
        public abstract IEnumerator ExecuteDecision(IBattlePlayer player, IGridUnit unit, IGridContext grid);
        public abstract void CleanUpDecision(IBattlePlayer player, IGridUnit unit, IGridContext grid);
        public abstract void ShowDebugDecisionInfo(IBattlePlayer player, IGridUnit unit, IGridContext grid);
    }

    public static class AiTurnRunner
    {
        public static IEnumerator ExecuteTurn(
            Player player,
            IEnumerable<Unit> orderedUnits,
            CellGrid grid,
            Action onTurnCompleted = null)
        {
            return ExecuteTurn(
                (IBattlePlayer)player,
                orderedUnits?.Where(unit => unit != null).Cast<IGridUnit>(),
                grid,
                onTurnCompleted);
        }

        public static IEnumerator ExecuteTurn(
            IBattlePlayer player,
            IEnumerable<Unit> orderedUnits,
            CellGrid grid,
            Action onTurnCompleted = null)
        {
            return ExecuteTurn(
                player,
                orderedUnits?.Where(unit => unit != null).Cast<IGridUnit>(),
                grid,
                onTurnCompleted);
        }

        public static IEnumerator ExecuteTurn(
            IBattlePlayer player,
            IEnumerable<IGridUnit> orderedUnits,
            IGridContext grid,
            Action onTurnCompleted = null)
        {
            List<IGridUnit> units = orderedUnits?
                .Where(unit => unit != null)
                .ToList()
                ?? new List<IGridUnit>();

            foreach (IGridUnit unit in units)
            {
                AiDecisionAction[] actions = ResolveActions(unit);
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

        private static AiDecisionAction[] ResolveActions(IGridUnit unit)
        {
            if (unit is not Component component)
            {
                return Array.Empty<AiDecisionAction>();
            }

            return component.GetComponentsInChildren<AiDecisionAction>(true)
                ?? Array.Empty<AiDecisionAction>();
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

        public static IReadOnlyList<GridUnit> OrderByMovementFreedom(IEnumerable<GridUnit> units)
        {
            return units?
                .Where(unit => unit != null)
                .OrderByDescending(CountTraversableNeighbors)
                .ToList()
                ?? new List<GridUnit>();
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

        private static int CountTraversableNeighbors(GridUnit unit)
        {
            if (unit?.CurrentCell == null || unit.Grid == null)
            {
                return 0;
            }

            return unit.CurrentCell
                .GetNeighbours(unit.Grid.Cells)
                .Count(unit.CanTraverse);
        }
    }
}
