using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Players
{
    public abstract class Player : BattlePlayerController
    {
        public abstract override bool IsHumanControlled { get; }

        public override bool Owns(Unit unit)
        {
            return unit != null && unit.PlayerId == PlayerId;
        }

        protected virtual void OnInitialize(CellGrid cellGrid)
        {
        }

        public override void BindToGrid(CellGrid grid)
        {
            if (grid != null)
            {
                OnInitialize(grid);
            }
        }

        public override void PlayTurn(CellGrid grid)
        {
            if (grid != null)
            {
                Play(grid);
            }
        }

        public abstract void Play(CellGrid cellGrid);
    }
}
