using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Abilities
{
    /// <summary>
    /// MonoBehaviour attached to a unit that receives grid input while selected and can run as a coroutine (human, AI, remote).
    /// </summary>
    public abstract class Ability : MonoBehaviour
    {
        protected Unit OwnerUnit { get; private set; }
        protected Unit UnitRef => OwnerUnit;
        protected Unit UnitReference => OwnerUnit;

        public virtual void InitializeAction(Unit unit)
        {
            OwnerUnit = unit;
            Initialize();
        }

        public virtual void Initialize()
        {
        }

        protected virtual IEnumerator Act(CellGrid grid, bool isRemoteInvocation = false)
        {
            yield break;
        }

        public virtual IEnumerator ExecuteAction(CellGrid grid, bool isRemoteInvocation = false)
        {
            return Act(grid, isRemoteInvocation);
        }

        protected virtual bool CanPerform(CellGrid grid)
        {
            return CanPerformAbility(grid);
        }

        public virtual bool CanPerformAction(CellGrid grid)
        {
            return CanPerform(grid);
        }

        protected virtual bool CanPerformAbility(CellGrid grid)
        {
            return OwnerUnit != null;
        }

        public virtual void DisplayAction(CellGrid grid)
        {
            Display(grid);
        }

        public virtual void CleanUpAction(CellGrid grid)
        {
            CleanUp(grid);
        }

        public virtual void OnActionSelected(CellGrid grid)
        {
            OnAbilitySelected(grid);
        }

        public virtual void OnActionDeselected(CellGrid grid)
        {
            OnAbilityDeselected(grid);
        }

        public virtual void OnCellClicked(Cell cell, CellGrid grid)
        {
            HandleCellClicked(cell, grid);
        }

        public virtual void OnCellHighlighted(Cell cell, CellGrid grid)
        {
            HandleCellSelected(cell, grid);
        }

        public virtual void OnCellDehighlighted(Cell cell, CellGrid grid)
        {
            HandleCellDeselected(cell, grid);
        }

        public virtual void OnUnitClicked(Unit unit, CellGrid grid)
        {
            HandleUnitClicked(unit, grid);
        }

        public virtual void OnUnitHighlighted(Unit unit, CellGrid grid)
        {
            HandleUnitHighlighted(unit, grid);
        }

        public virtual void OnUnitDehighlighted(Unit unit, CellGrid grid)
        {
            HandleUnitDehighlighted(unit, grid);
        }

        public virtual void OnTurnStarted(CellGrid grid)
        {
            OnTurnStart(grid);
        }

        public virtual void OnTurnEnded(CellGrid grid)
        {
            OnTurnEnd(grid);
        }

        public virtual void OnOwnerDestroyed(CellGrid grid)
        {
            OnUnitDestroyed(grid);
        }

        protected virtual void Display(CellGrid cellGrid)
        {
        }

        protected virtual void CleanUp(CellGrid cellGrid)
        {
        }

        protected virtual void OnAbilitySelected(CellGrid cellGrid)
        {
        }

        protected virtual void OnAbilityDeselected(CellGrid cellGrid)
        {
        }

        protected virtual void OnTurnStart(CellGrid cellGrid)
        {
        }

        protected virtual void OnTurnEnd(CellGrid cellGrid)
        {
        }

        protected virtual void OnUnitDestroyed(CellGrid cellGrid)
        {
        }

        protected virtual void HandleCellClicked(Cell cell, CellGrid cellGrid)
        {
        }

        protected virtual void HandleCellSelected(Cell cell, CellGrid cellGrid)
        {
        }

        protected virtual void HandleCellDeselected(Cell cell, CellGrid cellGrid)
        {
        }

        protected virtual void HandleUnitClicked(Unit unit, CellGrid cellGrid)
        {
        }

        protected virtual void HandleUnitHighlighted(Unit unit, CellGrid cellGrid)
        {
        }

        protected virtual void HandleUnitDehighlighted(Unit unit, CellGrid cellGrid)
        {
        }

        protected IEnumerator HumanExecute(CellGrid cellGrid)
        {
            if (cellGrid == null)
            {
                yield break;
            }

            yield return ExecuteInline(cellGrid, AbilityExecutionMode.HumanLocal, false);
        }

        protected IEnumerator RemoteExecute(CellGrid cellGrid)
        {
            if (cellGrid == null)
            {
                yield break;
            }

            yield return StartCoroutine(ExecuteInline(cellGrid, AbilityExecutionMode.RemoteInvocation, true));
        }

        public IEnumerator AIExecute(CellGrid cellGrid)
        {
            yield return ExecuteInline(cellGrid, AbilityExecutionMode.AiLocal, false);
        }

        public virtual IDictionary<string, string> Encapsulate()
        {
            throw new NotImplementedException();
        }

        protected virtual IEnumerator Apply(CellGrid cellGrid, IDictionary<string, string> actionParams, bool isNetworkInvoked = true)
        {
            throw new NotImplementedException();
        }

        private IEnumerator ExecuteInline(CellGrid cellGrid, AbilityExecutionMode executionMode, bool isNetworkInvoked)
        {
            if (cellGrid == null)
            {
                yield break;
            }

            Action beforeAction = null;
            Action afterAction = null;

            switch (executionMode)
            {
                case AbilityExecutionMode.HumanLocal:
                    beforeAction = cellGrid.EnterBlockedInputState;
                    afterAction = () => cellGrid.EnterSelectedState(UnitRef);
                    break;
                case AbilityExecutionMode.RemoteInvocation:
                    beforeAction = cellGrid.EnterRemotePlayerTurnState;
                    break;
                case AbilityExecutionMode.AiLocal:
                    afterAction = () => OnAbilityDeselected(cellGrid);
                    break;
            }

            yield return AbilityExecutionFlow.ExecuteInline(
                this,
                beforeAction,
                () => Act(cellGrid, isNetworkInvoked),
                afterAction);
        }
    }

    public enum AbilityExecutionMode
    {
        HumanLocal,
        RemoteInvocation,
        AiLocal
    }

    public static class AbilityExecutionFlow
    {
        public static IEnumerator ExecuteInline(
            MonoBehaviour host,
            Action beforeAction,
            Func<IEnumerator> executeAction,
            Action afterAction)
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
}
