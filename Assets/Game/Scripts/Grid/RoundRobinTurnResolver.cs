using UnityEngine;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid
{
    public sealed class RoundRobinTurnResolver : MonoBehaviour, IBattleTurnResolver
    {
        public RoundRobinTurnPlan ResolveStart(IBattleBoard board)
        {
            return RoundRobinBattleFlow.ResolveStart(board);
        }

        public RoundRobinTurnPlan ResolveTurn(IBattleBoard board)
        {
            return RoundRobinBattleFlow.ResolveTurn(board);
        }
    }
}
