using Windy.Srpg.Game.Grid;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Players;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Players
{
    public abstract class CustomPlayer : BattlePlayerController
    {
        public abstract override bool IsHumanControlled { get; }

        private static CustomCellGrid ResolveCustomCellGrid(IBattleBoard board)
        {
            if (board is CustomCellGrid customCellGrid)
            {
                return customCellGrid;
            }

            return (board as BattleBoard)?.GetComponent<CustomCellGrid>();
        }

        public override bool Owns(IBattleUnit unit)
        {
            return unit != null && unit.PlayerId == PlayerId;
        }

        protected virtual void OnInitialize(CustomCellGrid cellGrid)
        {
        }

        public override void InitializeBoard(IBattleBoard board)
        {
            CustomCellGrid customCellGrid = ResolveCustomCellGrid(board);
            if (customCellGrid != null)
            {
                OnInitialize(customCellGrid);
            }
        }

        public override void PlayTurn(IBattleBoard board)
        {
            CustomCellGrid customCellGrid = ResolveCustomCellGrid(board);
            if (customCellGrid != null)
            {
                Play(customCellGrid);
            }
        }

        public abstract void Play(CustomCellGrid cellGrid);
    }
}
