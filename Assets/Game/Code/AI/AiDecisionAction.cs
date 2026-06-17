using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Players;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.AI
{
    public abstract class AiDecisionAction : MonoBehaviour
    {
        public abstract void InitializeDecision(IBattlePlayer player, IBoardUnit unit, IBattleBoard board);
        public abstract bool ShouldExecute(IBattlePlayer player, IBoardUnit unit, IBattleBoard board);
        public abstract void Precalculate(IBattlePlayer player, IBoardUnit unit, IBattleBoard board);
        public abstract IEnumerator ExecuteDecision(IBattlePlayer player, IBoardUnit unit, IBattleBoard board);
        public abstract void CleanUpDecision(IBattlePlayer player, IBoardUnit unit, IBattleBoard board);
        public abstract void ShowDebugDecisionInfo(IBattlePlayer player, IBoardUnit unit, IBattleBoard board);
    }

    public static class AiTurnRunner
    {
        public static IEnumerator ExecuteTurn(
            IBattlePlayer player,
            IEnumerable<IBoardUnit> orderedUnits,
            IBattleBoard board,
            Action onTurnCompleted = null)
        {
            List<IBoardUnit> units = orderedUnits?
                .Where(unit => unit != null)
                .ToList()
                ?? new List<IBoardUnit>();

            foreach (IBoardUnit unit in units)
            {
                AiDecisionAction[] actions = ResolveActions(unit);
                foreach (AiDecisionAction action in actions)
                {
                    if (action == null || unit == null)
                    {
                        break;
                    }

                    yield return null;

                    action.InitializeDecision(player, unit, board);
                    bool shouldExecute = action.ShouldExecute(player, unit, board);
                    if (shouldExecute)
                    {
                        yield return null;
                        action.Precalculate(player, unit, board);
                        yield return null;

                        IEnumerator execution = action.ExecuteDecision(player, unit, board);
                        if (execution != null)
                        {
                            yield return execution;
                        }
                    }

                    if (action == null || unit == null)
                    {
                        break;
                    }

                    action.CleanUpDecision(player, unit, board);
                }
            }

            onTurnCompleted?.Invoke();
        }

        private static AiDecisionAction[] ResolveActions(IBoardUnit unit)
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
        public static IReadOnlyList<BoardUnit> OrderByMovementFreedom(IEnumerable<BoardUnit> units)
        {
            return units?
                .Where(unit => unit != null)
                .OrderByDescending(CountTraversableNeighbors)
                .ToList()
                ?? new List<BoardUnit>();
        }

        private static int CountTraversableNeighbors(BoardUnit unit)
        {
            if (unit?.CurrentCell == null || unit.Board == null)
            {
                return 0;
            }

            return unit.CurrentCell
                .GetNeighbours(unit.Board.Cells)
                .Count(unit.CanTraverse);
        }
    }
}

