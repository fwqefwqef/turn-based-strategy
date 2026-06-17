namespace Windy.Srpg.Runtime.Board.States
{
    public sealed class BoardStateAiTurn : BoardState
    {
        public BoardStateAiTurn(BattleBoard board) : base(board)
        {
        }

        public override string DiagnosticStateLabel => "AiTurn";
    }
}
