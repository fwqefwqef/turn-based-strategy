using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Players
{
    public interface IBattlePlayer
    {
        int PlayerId { get; }
        bool IsHumanControlled { get; }

        bool Owns(Unit unit);
    }
}
