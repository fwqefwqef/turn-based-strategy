using System.Collections;
using UnityEngine;
using Windy.Srpg.Runtime.Grid;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Actions
{
    // --- IBattleAction contract ---
    public interface IBattleAction
    {
        void InitializeAction(IGridUnit unit);
        bool CanPerformAction(IGridContext grid);
        IEnumerator ExecuteAction(IGridContext grid, bool isRemoteInvocation = false);
        void DisplayAction(IGridContext grid);
        void CleanUpAction(IGridContext grid);
        void OnActionSelected(IGridContext grid);
        void OnActionDeselected(IGridContext grid);
        void OnCellClicked(Cell cell, IGridContext grid);
        void OnCellHighlighted(Cell cell, IGridContext grid);
        void OnCellDehighlighted(Cell cell, IGridContext grid);
        void OnUnitClicked(IGridUnit unit, IGridContext grid);
        void OnUnitHighlighted(IGridUnit unit, IGridContext grid);
        void OnUnitDehighlighted(IGridUnit unit, IGridContext grid);
        void OnTurnStarted(IGridContext grid);
        void OnTurnEnded(IGridContext grid);
        void OnOwnerDestroyed(IGridContext grid);
    }

    // --- BattleAction base ---
    public abstract class BattleAction : MonoBehaviour, IBattleAction
    {
        protected IGridUnit Unit { get; private set; }

        protected T GetUnit<T>() where T : class, IGridUnit
        {
            return Unit as T;
        }

        public virtual void InitializeAction(IGridUnit unit)
        {
            Unit = unit;
        }

        protected abstract IEnumerator Act(IGridContext grid, bool isRemoteInvocation = false);

        public virtual IEnumerator ExecuteAction(IGridContext grid, bool isRemoteInvocation = false)
        {
            return Act(grid, isRemoteInvocation);
        }

        protected virtual bool CanPerform(IGridContext grid)
        {
            return Unit != null;
        }

        public virtual bool CanPerformAction(IGridContext grid)
        {
            return CanPerform(grid);
        }

        public virtual void DisplayAction(IGridContext grid)
        {
        }

        public virtual void CleanUpAction(IGridContext grid)
        {
        }

        public virtual void OnActionSelected(IGridContext grid)
        {
        }

        public virtual void OnActionDeselected(IGridContext grid)
        {
        }

        public virtual void OnCellClicked(Cell cell, IGridContext grid)
        {
        }

        public virtual void OnCellHighlighted(Cell cell, IGridContext grid)
        {
        }

        public virtual void OnCellDehighlighted(Cell cell, IGridContext grid)
        {
        }

        public virtual void OnUnitClicked(IGridUnit unit, IGridContext grid)
        {
        }

        public virtual void OnUnitHighlighted(IGridUnit unit, IGridContext grid)
        {
        }

        public virtual void OnUnitDehighlighted(IGridUnit unit, IGridContext grid)
        {
        }

        public virtual void OnTurnStarted(IGridContext grid)
        {
        }

        public virtual void OnTurnEnded(IGridContext grid)
        {
        }

        public virtual void OnOwnerDestroyed(IGridContext grid)
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

    // --- MoveActionBase ---
    public abstract class MoveActionBase : BattleAction
    {
        public virtual bool CanTraverse(Cell cell)
        {
            var unit = GetUnit<GridUnit>();
            return unit != null && unit.CanTraverse(cell);
        }

        public virtual bool CanStopOn(Cell cell)
        {
            var unit = GetUnit<GridUnit>();
            return unit != null && unit.CanStopOn(cell);
        }
    }

    // --- AttackActionBase ---
    public abstract class AttackActionBase : BattleAction
    {
        [SerializeField] private int minRange = 1;
        [SerializeField] private int maxRange = 1;

        public int MinRange => Mathf.Max(0, minRange);
        public int MaxRange => Mathf.Max(MinRange, maxRange);
    }

    // --- AttackRangeHighlightActionBase ---
    public abstract class AttackRangeHighlightActionBase : BattleAction
    {
    }

}
