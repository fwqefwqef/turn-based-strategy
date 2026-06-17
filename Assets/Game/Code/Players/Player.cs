using Windy.Srpg.Game.Grid;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Players;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Players
{
    public abstract class Player : BattlePlayerController
    {
        public abstract override bool IsHumanControlled { get; }

        private static CellGrid ResolveCellGrid(IBattleBoard board)
        {
            if (board is CellGrid customCellGrid)
            {
                return customCellGrid;
            }

            return (board as BattleBoard)?.GetComponent<CellGrid>();
        }

        public override bool Owns(IBoardUnit unit)
        {
            return unit != null && unit.PlayerId == PlayerId;
        }

        protected virtual void OnInitialize(CellGrid cellGrid)
        {
        }

        public override void InitializeBoard(IBattleBoard board)
        {
            CellGrid customCellGrid = ResolveCellGrid(board);
            if (customCellGrid != null)
            {
                OnInitialize(customCellGrid);
            }
        }

        public override void PlayTurn(IBattleBoard board)
        {
            CellGrid customCellGrid = ResolveCellGrid(board);
            if (customCellGrid != null)
            {
                Play(customCellGrid);
            }
        }

        public abstract void Play(CellGrid cellGrid);
    }
}

