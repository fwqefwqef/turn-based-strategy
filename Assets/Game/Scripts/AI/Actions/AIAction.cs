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
        private static CustomPlayer ResolveCustomPlayer(IBattlePlayer player)
        {
            return player as CustomPlayer;
        }

        private static CustomUnit ResolveCustomUnit(IBattleUnit unit)
        {
            if (unit is CustomUnit customUnit)
            {
                return customUnit;
            }

            return (unit as BattleUnit)?.GetComponent<CustomUnit>();
        }

        private static CustomCellGrid ResolveCustomCellGrid(IBattleBoard board)
        {
            if (board is CustomCellGrid customCellGrid)
            {
                return customCellGrid;
            }

            return (board as BattleBoard)?.GetComponent<CustomCellGrid>();
        }

        public sealed override void InitializeDecision(IBattlePlayer player, IBattleUnit unit, IBattleBoard board)
        {
            CustomPlayer customPlayer = ResolveCustomPlayer(player);
            CustomUnit customUnit = ResolveCustomUnit(unit);
            CustomCellGrid customCellGrid = ResolveCustomCellGrid(board);
            if (customPlayer != null && customUnit != null && customCellGrid != null)
            {
                InitializeAction(customPlayer, customUnit, customCellGrid);
            }
        }

        public sealed override bool ShouldExecute(IBattlePlayer player, IBattleUnit unit, IBattleBoard board)
        {
            CustomPlayer customPlayer = ResolveCustomPlayer(player);
            CustomUnit customUnit = ResolveCustomUnit(unit);
            CustomCellGrid customCellGrid = ResolveCustomCellGrid(board);
            return customPlayer != null
                && customUnit != null
                && customCellGrid != null
                && ShouldExecute(customPlayer, customUnit, customCellGrid);
        }

        public sealed override void Precalculate(IBattlePlayer player, IBattleUnit unit, IBattleBoard board)
        {
            CustomPlayer customPlayer = ResolveCustomPlayer(player);
            CustomUnit customUnit = ResolveCustomUnit(unit);
            CustomCellGrid customCellGrid = ResolveCustomCellGrid(board);
            if (customPlayer != null && customUnit != null && customCellGrid != null)
            {
                Precalculate(customPlayer, customUnit, customCellGrid);
            }
        }

        public sealed override IEnumerator ExecuteDecision(IBattlePlayer player, IBattleUnit unit, IBattleBoard board)
        {
            CustomPlayer customPlayer = ResolveCustomPlayer(player);
            CustomUnit customUnit = ResolveCustomUnit(unit);
            CustomCellGrid customCellGrid = ResolveCustomCellGrid(board);
            if (customPlayer != null && customUnit != null && customCellGrid != null)
            {
                return Execute(customPlayer, customUnit, customCellGrid);
            }

            return null;
        }

        public sealed override void CleanUpDecision(IBattlePlayer player, IBattleUnit unit, IBattleBoard board)
        {
            CustomPlayer customPlayer = ResolveCustomPlayer(player);
            CustomUnit customUnit = ResolveCustomUnit(unit);
            CustomCellGrid customCellGrid = ResolveCustomCellGrid(board);
            if (customPlayer != null && customUnit != null && customCellGrid != null)
            {
                CleanUp(customPlayer, customUnit, customCellGrid);
            }
        }

        public sealed override void ShowDebugDecisionInfo(IBattlePlayer player, IBattleUnit unit, IBattleBoard board)
        {
            CustomPlayer customPlayer = ResolveCustomPlayer(player);
            CustomUnit customUnit = ResolveCustomUnit(unit);
            CustomCellGrid customCellGrid = ResolveCustomCellGrid(board);
            if (customPlayer != null && customUnit != null && customCellGrid != null)
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
