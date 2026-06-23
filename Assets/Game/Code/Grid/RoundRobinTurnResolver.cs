using UnityEngine;
using Windy.Srpg.Runtime.Grid;

namespace Windy.Srpg.Game.Grid
{
    public sealed class RoundRobinTurnResolver : MonoBehaviour, IBattleTurnResolver
    {
        public RoundRobinTurnPlan ResolveStart(CellGrid grid)
        {
            return RoundRobinBattleFlow.ResolveStart(grid);
        }

        public RoundRobinTurnPlan ResolveTurn(CellGrid grid)
        {
            return RoundRobinBattleFlow.ResolveTurn(grid);
        }
    }
}
