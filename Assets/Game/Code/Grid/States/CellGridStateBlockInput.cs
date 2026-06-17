namespace Windy.Srpg.Game.Grid.States
{
    public sealed class CellGridStateBlockInput : CellGridState
    {
        public CellGridStateBlockInput(CellGrid cellGrid) : base(cellGrid)
        {
        }

        public override bool BlocksEndTurn => true;
    }

    public sealed class CellGridStateGameOver : CellGridState
    {
        public CellGridStateGameOver(CellGrid cellGrid) : base(cellGrid)
        {
        }
    }
}

