using System.Collections.Generic;
using UnityEngine;
using Windy.Srpg.Runtime.Players;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Grid
{
    public interface IGridContext
    {
        int CurrentPlayerId { get; }
        IReadOnlyList<IBattlePlayer> Players { get; }
        IReadOnlyList<IGridUnit> Units { get; }
    }

    public interface IBattleSceneUnitSource
    {
        IReadOnlyList<Transform> GetInitialUnitTransforms(IGridContext grid);
    }

    public interface IBattleTurnResolver
    {
        RoundRobinTurnPlan ResolveStart(IGridContext grid);
        RoundRobinTurnPlan ResolveTurn(IGridContext grid);
    }

    public interface IBattleEndCondition
    {
        BattleOutcome Evaluate(IGridContext grid);
    }
}

