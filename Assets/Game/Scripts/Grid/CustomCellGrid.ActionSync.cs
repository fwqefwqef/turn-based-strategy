using System.Collections.Generic;
using TbsFramework.Units;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Actions;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CustomCellGrid
    {
        internal void NotifyBattleActionsTurnStarted(CustomUnit unit)
        {
            NotifyBattleActions(unit, action => action.OnTurnStarted(this));
        }

        internal void NotifyBattleActionsTurnEnded(CustomUnit unit)
        {
            NotifyBattleActions(unit, action => action.OnTurnEnded(this));
        }

        internal void NotifyBattleActionsOwnerDestroyed(CustomUnit unit)
        {
            NotifyBattleActions(unit, action => action.OnOwnerDestroyed(this));
        }

        internal bool TryNotifyTurnStarted(Unit unit)
        {
            CustomUnit customUnit = ResolveCustomUnitFromRegistryUnit(unit);
            if (customUnit == null)
            {
                return false;
            }

            NotifyBattleActionsTurnStarted(customUnit);
            return true;
        }

        internal bool TryNotifyTurnEnded(Unit unit)
        {
            CustomUnit customUnit = ResolveCustomUnitFromRegistryUnit(unit);
            if (customUnit == null)
            {
                return false;
            }

            NotifyBattleActionsTurnEnded(customUnit);
            return true;
        }

        internal bool TryNotifyOwnerDestroyed(Unit unit)
        {
            CustomUnit customUnit = ResolveCustomUnitFromRegistryUnit(unit);
            if (customUnit == null)
            {
                return false;
            }

            NotifyBattleActionsOwnerDestroyed(customUnit);
            return true;
        }

        private void NotifyBattleActions(CustomUnit unit, System.Action<IBattleAction> notify)
        {
            if (unit == null || notify == null)
            {
                return;
            }

            List<IBattleAction> actions = unit.GetBattleActions();
            for (int i = 0; i < actions.Count; i++)
            {
                notify(actions[i]);
            }
        }

        private static CustomUnit ResolveCustomUnitFromBattleUnit(IBattleUnit unit)
        {
            if (unit is CustomUnit customUnit)
            {
                return customUnit;
            }

            return unit is UnityEngine.Component component
                ? component.GetComponent<CustomUnit>()
                : null;
        }
    }
}
