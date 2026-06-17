using UnityEngine;
using Windy.Srpg.Runtime.Grid;

namespace Windy.Srpg.Game.Grid
{
    // --- Round-robin turn resolver (scene component) ---
    public sealed class RoundRobinTurnResolver : MonoBehaviour, IBattleTurnResolver
    {
        public RoundRobinTurnPlan ResolveStart(IGridContext grid)
        {
            return RoundRobinBattleFlow.ResolveStart(grid);
        }

        public RoundRobinTurnPlan ResolveTurn(IGridContext grid)
        {
            return RoundRobinBattleFlow.ResolveTurn(grid);
        }
    }

    // --- Last-side-standing win condition (scene component) ---
    public sealed class LastSideStandingCondition : MonoBehaviour, IBattleEndCondition
    {
        public BattleOutcome Evaluate(IGridContext grid)
        {
            return RoundRobinBattleFlow.EvaluateLastSideStanding(grid);
        }
    }
}
