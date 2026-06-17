using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Players
{
    public interface IBattlePlayer
    {
        int PlayerId { get; }
        bool IsHumanControlled { get; }

        bool Owns(IBoardUnit unit);
    }
}

