using UnityEngine;
using Windy.Srpg.Runtime.Grid;

namespace Windy.Srpg.Game.Grid
{
    public sealed class LastSideStandingCondition : MonoBehaviour, IBattleEndCondition
    {
        public BattleOutcome Evaluate(IGridContext grid)
        {
            return RoundRobinBattleFlow.EvaluateLastSideStanding(grid);
        }
    }
}
