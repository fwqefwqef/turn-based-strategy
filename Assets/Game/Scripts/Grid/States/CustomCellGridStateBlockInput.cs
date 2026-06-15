namespace Windy.Srpg.Game.Grid.States
{
    public sealed class CustomCellGridStateBlockInput : CustomCellGridState
    {
        public CustomCellGridStateBlockInput(CustomCellGrid cellGrid) : base(cellGrid)
        {
        }

        public override bool BlocksEndTurn => true;
    }
}
