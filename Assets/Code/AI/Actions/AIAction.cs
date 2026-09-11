using System.Collections;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Game.AI;

namespace Windy.Srpg.Game.AI.Actions
{
    public abstract class AIAction : AiDecisionAction
    {
        public sealed override void InitializeDecision(IBattlePlayer player, Unit unit, CellGrid grid)
        {
            if (player is Player customPlayer && unit != null && grid != null)
            {
                InitializeAction(customPlayer, unit, grid);
            }
        }

        public sealed override bool ShouldExecute(IBattlePlayer player, Unit unit, CellGrid grid)
        {
            return player is Player customPlayer
                && unit != null
                && grid != null
                && ShouldExecute(customPlayer, unit, grid);
        }

        public sealed override void Precalculate(IBattlePlayer player, Unit unit, CellGrid grid)
        {
            if (player is Player customPlayer && unit != null && grid != null)
            {
                Precalculate(customPlayer, unit, grid);
            }
        }

        public sealed override IEnumerator ExecuteDecision(IBattlePlayer player, Unit unit, CellGrid grid)
        {
            if (player is Player customPlayer && unit != null && grid != null)
            {
                return Execute(customPlayer, unit, grid);
            }

            return null;
        }

        public sealed override void CleanUpDecision(IBattlePlayer player, Unit unit, CellGrid grid)
        {
            if (player is Player customPlayer && unit != null && grid != null)
            {
                CleanUp(customPlayer, unit, grid);
            }
        }

        public sealed override void ShowDebugDecisionInfo(IBattlePlayer player, Unit unit, CellGrid grid)
        {
            if (player is Player customPlayer && unit != null && grid != null)
            {
                ShowDebugInfo(customPlayer, unit, grid);
            }
        }

        public abstract void InitializeAction(Player player, Unit unit, CellGrid cellGrid);
        public abstract bool ShouldExecute(Player player, Unit unit, CellGrid cellGrid);
        public abstract void Precalculate(Player player, Unit unit, CellGrid cellGrid);
        public abstract IEnumerator Execute(Player player, Unit unit, CellGrid cellGrid);
        public abstract void CleanUp(Player player, Unit unit, CellGrid cellGrid);
        public abstract void ShowDebugInfo(Player player, Unit unit, CellGrid cellGrid);
    }
}
