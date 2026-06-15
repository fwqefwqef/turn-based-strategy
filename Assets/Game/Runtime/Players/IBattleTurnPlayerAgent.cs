using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Runtime.Players
{
    public interface IBattleTurnPlayer : IBattlePlayer
    {
        void InitializeBoard(IBattleBoard board);
        void PlayTurn(IBattleBoard board);
    }
}
