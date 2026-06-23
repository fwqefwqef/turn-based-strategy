using System.Collections;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Abilities
{
    public abstract class BattleAction : MonoBehaviour
    {
        protected Unit OwnerUnit { get; private set; }

        public virtual void InitializeAction(Unit unit)
        {
            OwnerUnit = unit;
        }

        protected abstract IEnumerator Act(CellGrid grid, bool isRemoteInvocation = false);

        public virtual IEnumerator ExecuteAction(CellGrid grid, bool isRemoteInvocation = false)
        {
            return Act(grid, isRemoteInvocation);
        }

        protected virtual bool CanPerform(CellGrid grid)
        {
            return OwnerUnit != null;
        }

        public virtual bool CanPerformAction(CellGrid grid)
        {
            return CanPerform(grid);
        }

        public virtual void DisplayAction(CellGrid grid)
        {
        }

        public virtual void CleanUpAction(CellGrid grid)
        {
        }

        public virtual void OnActionSelected(CellGrid grid)
        {
        }

        public virtual void OnActionDeselected(CellGrid grid)
        {
        }

        public virtual void OnCellClicked(Cell cell, CellGrid grid)
        {
        }

        public virtual void OnCellHighlighted(Cell cell, CellGrid grid)
        {
        }

        public virtual void OnCellDehighlighted(Cell cell, CellGrid grid)
        {
        }

        public virtual void OnUnitClicked(Unit unit, CellGrid grid)
        {
        }

        public virtual void OnUnitHighlighted(Unit unit, CellGrid grid)
        {
        }

        public virtual void OnUnitDehighlighted(Unit unit, CellGrid grid)
        {
        }

        public virtual void OnTurnStarted(CellGrid grid)
        {
        }

        public virtual void OnTurnEnded(CellGrid grid)
        {
        }

        public virtual void OnOwnerDestroyed(CellGrid grid)
        {
        }
    }

    public enum BattleActionExecutionMode
    {
        HumanLocal,
        RemoteInvocation,
        AiLocal
    }

    public static class BattleActionExecutionFlow
    {
        public static IEnumerator ExecuteInline(
            MonoBehaviour host,
            System.Action beforeAction,
            System.Func<IEnumerator> executeAction,
            System.Action afterAction)
        {
            if (host == null || executeAction == null)
            {
                yield break;
            }

            beforeAction?.Invoke();
            yield return host.StartCoroutine(executeAction());
            afterAction?.Invoke();
        }
    }

    public abstract class MoveActionBase : BattleAction
    {
        public virtual bool CanTraverse(Cell cell)
        {
            return OwnerUnit != null && OwnerUnit.IsCellTraversable(cell);
        }

        public virtual bool CanStopOn(Cell cell)
        {
            return OwnerUnit != null && OwnerUnit.IsCellMovableTo(cell);
        }
    }

    public abstract class AttackActionBase : BattleAction
    {
        [SerializeField] private int minRange = 1;
        [SerializeField] private int maxRange = 1;

        public int MinRange => Mathf.Max(0, minRange);
        public int MaxRange => Mathf.Max(MinRange, maxRange);
    }

    public abstract class AttackRangeHighlightActionBase : BattleAction
    {
    }
}
