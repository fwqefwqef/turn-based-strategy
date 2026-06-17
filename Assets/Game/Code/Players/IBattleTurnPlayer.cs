using Windy.Srpg.Runtime.Grid;

namespace Windy.Srpg.Runtime.Players
{
    public interface IBattleTurnPlayer : IBattlePlayer
    {
        void BindToGrid(IGridContext grid);
        void PlayTurn(IGridContext grid);
    }
}

