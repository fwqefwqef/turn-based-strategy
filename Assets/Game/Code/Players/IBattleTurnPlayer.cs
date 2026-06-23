using Windy.Srpg.Game.Grid;

namespace Windy.Srpg.Runtime.Players
{
    public interface IBattleTurnPlayer : IBattlePlayer
    {
        void BindToGrid(CellGrid grid);
        void PlayTurn(CellGrid grid);
    }
}
