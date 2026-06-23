using UnityEngine;
using Windy.Srpg.Game.Grid;

namespace Windy.Srpg.Game.Grid
{
    public sealed class LastSideStandingCondition : MonoBehaviour, IBattleEndCondition
    {
        public BattleOutcome Evaluate(CellGrid grid)
        {
            return RoundRobinBattleFlow.EvaluateLastSideStanding(grid);
        }
    }
}
