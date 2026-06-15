using System.Collections;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.AI;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Players;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.AI.Actions
{
    public abstract class AIAction : AiDecisionAction
    {
        public sealed override void InitializeDecision(IBattlePlayer player, IBattleUnit unit, IBattleBoard board)
        {
            if (player is CustomPlayer customPlayer && unit is CustomUnit customUnit && board is CustomCellGrid customCellGrid)
            {
                InitializeAction(customPlayer, customUnit, customCellGrid);
            }
        }

        public sealed override bool ShouldExecute(IBattlePlayer player, IBattleUnit unit, IBattleBoard board)
        {
            return player is CustomPlayer customPlayer
                && unit is CustomUnit customUnit
                && board is CustomCellGrid customCellGrid
                && ShouldExecute(customPlayer, customUnit, customCellGrid);
        }

        public sealed override void Precalculate(IBattlePlayer player, IBattleUnit unit, IBattleBoard board)
        {
            if (player is CustomPlayer customPlayer && unit is CustomUnit customUnit && board is CustomCellGrid customCellGrid)
            {
                Precalculate(customPlayer, customUnit, customCellGrid);
            }
        }

        public sealed override IEnumerator ExecuteDecision(IBattlePlayer player, IBattleUnit unit, IBattleBoard board)
        {
            if (player is CustomPlayer customPlayer && unit is CustomUnit customUnit && board is CustomCellGrid customCellGrid)
            {
                return Execute(customPlayer, customUnit, customCellGrid);
            }

            return null;
        }

        public sealed override void CleanUpDecision(IBattlePlayer player, IBattleUnit unit, IBattleBoard board)
        {
            if (player is CustomPlayer customPlayer && unit is CustomUnit customUnit && board is CustomCellGrid customCellGrid)
            {
                CleanUp(customPlayer, customUnit, customCellGrid);
            }
        }

        public sealed override void ShowDebugDecisionInfo(IBattlePlayer player, IBattleUnit unit, IBattleBoard board)
        {
            if (player is CustomPlayer customPlayer && unit is CustomUnit customUnit && board is CustomCellGrid customCellGrid)
            {
                ShowDebugInfo(customPlayer, customUnit, customCellGrid);
            }
        }

        public abstract void InitializeAction(CustomPlayer player, CustomUnit unit, CustomCellGrid cellGrid);
        public abstract bool ShouldExecute(CustomPlayer player, CustomUnit unit, CustomCellGrid cellGrid);
        public abstract void Precalculate(CustomPlayer player, CustomUnit unit, CustomCellGrid cellGrid);
        public abstract IEnumerator Execute(CustomPlayer player, CustomUnit unit, CustomCellGrid cellGrid);
        public abstract void CleanUp(CustomPlayer player, CustomUnit unit, CustomCellGrid cellGrid);
        public abstract void ShowDebugInfo(CustomPlayer player, CustomUnit unit, CustomCellGrid cellGrid);
    }
}
