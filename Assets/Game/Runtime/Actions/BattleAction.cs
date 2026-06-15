using System.Collections;
using UnityEngine;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Runtime.Actions
{
    public abstract class BattleAction : MonoBehaviour, IBattleAction
    {
        protected IBattleUnit Unit { get; private set; }

        protected T GetUnit<T>() where T : class, IBattleUnit
        {
            return Unit as T;
        }

        public virtual void InitializeAction(IBattleUnit unit)
        {
            Unit = unit;
        }

        protected abstract IEnumerator Act(IBattleBoard board, bool isRemoteInvocation = false);

        public virtual IEnumerator ExecuteAction(IBattleBoard board, bool isRemoteInvocation = false)
        {
            return Act(board, isRemoteInvocation);
        }

        protected virtual bool CanPerform(IBattleBoard board)
        {
            return Unit != null;
        }

        public virtual bool CanPerformAction(IBattleBoard board)
        {
            return CanPerform(board);
        }

        public virtual void DisplayAction(IBattleBoard board)
        {
        }

        public virtual void CleanUpAction(IBattleBoard board)
        {
        }

        public virtual void OnActionSelected(IBattleBoard board)
        {
        }

        public virtual void OnActionDeselected(IBattleBoard board)
        {
        }

        public virtual void OnCellClicked(IBattleCell cell, IBattleBoard board)
        {
        }

        public virtual void OnCellHighlighted(IBattleCell cell, IBattleBoard board)
        {
        }

        public virtual void OnCellDehighlighted(IBattleCell cell, IBattleBoard board)
        {
        }

        public virtual void OnUnitClicked(IBattleUnit unit, IBattleBoard board)
        {
        }

        public virtual void OnUnitHighlighted(IBattleUnit unit, IBattleBoard board)
        {
        }

        public virtual void OnUnitDehighlighted(IBattleUnit unit, IBattleBoard board)
        {
        }

        public virtual void OnTurnStarted(IBattleBoard board)
        {
        }

        public virtual void OnTurnEnded(IBattleBoard board)
        {
        }

        public virtual void OnOwnerDestroyed(IBattleBoard board)
        {
        }
    }
}
