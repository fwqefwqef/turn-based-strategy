using UnityEngine;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid
{
    public sealed class LastSideStandingCondition : MonoBehaviour, IBattleEndCondition
    {
        public BattleOutcome Evaluate(IBattleBoard board)
        {
            return RoundRobinBattleFlow.EvaluateLastSideStanding(board);
        }
    }
}

