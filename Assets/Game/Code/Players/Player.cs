using Windy.Srpg.Game.Grid;
using Windy.Srpg.Runtime.Grid;
using Windy.Srpg.Runtime.Players;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Players
{
    public abstract class Player : BattlePlayerController
    {
        public abstract override bool IsHumanControlled { get; }

        private static CellGrid ResolveCellGrid(IGridContext grid)
        {
            if (grid is CellGrid customCellGrid)
            {
                return customCellGrid;
            }

            return (grid as RuntimeGrid)?.GetComponent<CellGrid>();
        }

        public override bool Owns(IGridUnit unit)
        {
            return unit != null && unit.PlayerId == PlayerId;
        }

        protected virtual void OnInitialize(CellGrid cellGrid)
        {
        }

        public override void BindToGrid(IGridContext grid)
        {
            CellGrid customCellGrid = ResolveCellGrid(grid);
            if (customCellGrid != null)
            {
                OnInitialize(customCellGrid);
            }
        }

        public override void PlayTurn(IGridContext grid)
        {
            CellGrid customCellGrid = ResolveCellGrid(grid);
            if (customCellGrid != null)
            {
                Play(customCellGrid);
            }
        }

        public abstract void Play(CellGrid cellGrid);
    }
}

