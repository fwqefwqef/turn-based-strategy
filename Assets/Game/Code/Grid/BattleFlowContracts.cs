using System.Collections.Generic;
using UnityEngine;
using Windy.Srpg.Runtime.Grid;

namespace Windy.Srpg.Game.Grid
{
    public interface IBattleSceneUnitSource
    {
        IReadOnlyList<Transform> GetInitialUnitTransforms(CellGrid grid);
    }

    public interface IBattleTurnResolver
    {
        RoundRobinTurnPlan ResolveStart(CellGrid grid);
        RoundRobinTurnPlan ResolveTurn(CellGrid grid);
    }

    public interface IBattleEndCondition
    {
        BattleOutcome Evaluate(CellGrid grid);
    }
}
