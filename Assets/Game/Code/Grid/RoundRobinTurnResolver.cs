using UnityEngine;
using Windy.Srpg.Runtime.Grid;

namespace Windy.Srpg.Game.Grid
{
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
}
