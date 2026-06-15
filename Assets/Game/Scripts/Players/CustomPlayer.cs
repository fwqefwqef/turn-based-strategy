using Windy.Srpg.Game.Grid;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Players;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Players
{
    public abstract class CustomPlayer : BattlePlayerController
    {
        public abstract override bool IsHumanControlled { get; }

        public override bool Owns(IBattleUnit unit)
        {
            return unit != null && unit.PlayerId == PlayerId;
        }

        protected virtual void OnInitialize(CustomCellGrid cellGrid)
        {
        }

        public override void InitializeBoard(IBattleBoard board)
        {
            if (board is CustomCellGrid customCellGrid)
            {
                OnInitialize(customCellGrid);
            }
        }

        public override void PlayTurn(IBattleBoard board)
        {
            if (board is CustomCellGrid customCellGrid)
            {
                Play(customCellGrid);
            }
        }

        public abstract void Play(CustomCellGrid cellGrid);
    }
}
