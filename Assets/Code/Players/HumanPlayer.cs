using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Grid.States;

namespace Windy.Srpg.Game.Players
{
    public sealed class HumanPlayer : Player
    {
        public override bool IsHumanControlled => true;

        public override void Play(CellGrid cellGrid)
        {
            cellGrid.EnterWaitingState();
        }
    }
}

