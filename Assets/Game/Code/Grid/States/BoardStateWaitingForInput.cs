using System.Linq;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Board.States
{
    public sealed class BoardStateWaitingForInput : BoardState
    {
        public BoardStateWaitingForInput(BattleBoard board) : base(board)
        {
        }

        public override string DiagnosticStateLabel => "Waiting";

        public override void OnUnitClicked(BoardUnit unit)
        {
            if (unit == null || unit.IsFinishedForTurn)
            {
                return;
            }

            if (!Board.GetCurrentPlayerUnits().Contains(unit))
            {
                return;
            }

            Board.SetState(new BoardStateUnitSelected(Board, unit));
        }
    }
}

