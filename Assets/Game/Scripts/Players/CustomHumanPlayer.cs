using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Grid.States;

namespace Windy.Srpg.Game.Players
{
    public sealed class CustomHumanPlayer : CustomPlayer
    {
        public override bool IsHumanControlled => true;

        public override void Play(CustomCellGrid cellGrid)
        {
            cellGrid.EnterWaitingState();
        }
    }
}
