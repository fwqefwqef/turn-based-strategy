using System.Collections.Generic;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CustomCellGrid
    {
        internal void PrepareRuntimeTurnStartForPlan(RoundRobinTurnPlan plan)
        {
            if (plan.PlayableUnits == null)
            {
                return;
            }

            for (int i = 0; i < plan.PlayableUnits.Count; i++)
            {
                ResolveCustomUnitFromBattleUnit(plan.PlayableUnits[i])?.PrepareRuntimeForTurnStart();
            }
        }

        internal void ApplyLegacyTurnEndToCurrentPlayerCustomUnits()
        {
            List<CustomUnit> playableUnits = GetCurrentPlayerCustomUnits();
            for (int i = 0; i < playableUnits.Count; i++)
            {
                CustomUnit unit = playableUnits[i];
                if (unit == null)
                {
                    continue;
                }

                unit.OnTurnEnd();
                NotifyBattleActionsTurnEnded(unit);
            }
        }

        internal void ApplyRuntimeTurnStartToLegacyPlayableUnits()
        {
            List<CustomUnit> playableUnits = GetCurrentPlayerCustomUnits();
            for (int i = 0; i < playableUnits.Count; i++)
            {
                CustomUnit customUnit = playableUnits[i];
                if (customUnit == null)
                {
                    continue;
                }

                NotifyBattleActionsTurnStarted(customUnit);
                customUnit.ApplyLegacyTurnStartFromRuntime();
            }
        }
    }
}
