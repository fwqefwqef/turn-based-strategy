using System.Collections.Generic;
using UnityEngine;
using Windy.Srpg.Runtime.Players;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Board
{
    public interface IBattleBoard
    {
        int CurrentPlayerId { get; }
        IReadOnlyList<IBattlePlayer> Players { get; }
        IReadOnlyList<IBattleUnit> Units { get; }
    }

    public interface IBattleSceneUnitSource
    {
        IReadOnlyList<Transform> GetInitialUnitTransforms(IBattleBoard board);
    }

    public interface IBattleTurnResolver
    {
        RoundRobinTurnPlan ResolveStart(IBattleBoard board);
        RoundRobinTurnPlan ResolveTurn(IBattleBoard board);
    }

    public interface IBattleEndCondition
    {
        BattleOutcome Evaluate(IBattleBoard board);
    }
}
