using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Abilities
{
    /// <summary>
    /// Scene-facing ability shell on top of <see cref="BattleAction"/>.
    /// </summary>
    public abstract class Ability : BattleAction
    {
        protected Unit UnitRef => OwnerUnit;
        protected Unit UnitReference => OwnerUnit;

        protected IEnumerator HumanExecute(CellGrid cellGrid)
        {
            if (cellGrid == null)
            {
                yield break;
            }

            yield return ExecuteInline(cellGrid, BattleActionExecutionMode.HumanLocal, false);
        }

        protected IEnumerator RemoteExecute(CellGrid cellGrid)
        {
            if (cellGrid == null)
            {
                yield break;
            }

            yield return StartCoroutine(ExecuteInline(cellGrid, BattleActionExecutionMode.RemoteInvocation, true));
        }

        public IEnumerator AIExecute(CellGrid cellGrid)
        {
            yield return ExecuteInline(cellGrid, BattleActionExecutionMode.AiLocal, false);
        }

        protected override IEnumerator Act(CellGrid cellGrid, bool isNetworkInvoked = false)
        {
            yield break;
        }

        public virtual void Initialize()
        {
        }

        public override void InitializeAction(Unit unit)
        {
            base.InitializeAction(unit);
            Initialize();
        }

        protected virtual void HandleUnitClicked(Unit unit, CellGrid cellGrid) { }

        protected virtual void HandleUnitHighlighted(Unit unit, CellGrid cellGrid) { }

        protected virtual void HandleUnitDehighlighted(Unit unit, CellGrid cellGrid) { }

        protected virtual void OnUnitDestroyed(CellGrid cellGrid) { }

        protected virtual void HandleCellClicked(Cell cell, CellGrid cellGrid) { }

        protected virtual void HandleCellSelected(Cell cell, CellGrid cellGrid) { }

        protected virtual void HandleCellDeselected(Cell cell, CellGrid cellGrid) { }

        protected virtual void Display(CellGrid cellGrid) { }

        public override void DisplayAction(CellGrid grid)
        {
            Display(grid);
        }

        protected virtual void CleanUp(CellGrid cellGrid) { }

        public override void CleanUpAction(CellGrid grid)
        {
            CleanUp(grid);
        }

        protected virtual void OnAbilitySelected(CellGrid cellGrid) { }

        public override void OnActionSelected(CellGrid grid)
        {
            OnAbilitySelected(grid);
        }

        protected virtual void OnAbilityDeselected(CellGrid cellGrid) { }

        public override void OnActionDeselected(CellGrid grid)
        {
            OnAbilityDeselected(grid);
        }

        protected virtual void OnTurnStart(CellGrid cellGrid) { }

        public override void OnTurnStarted(CellGrid grid)
        {
            OnTurnStart(grid);
        }

        protected virtual void OnTurnEnd(CellGrid cellGrid) { }

        public override void OnTurnEnded(CellGrid grid)
        {
            OnTurnEnd(grid);
        }

        public override void OnOwnerDestroyed(CellGrid grid)
        {
            OnUnitDestroyed(grid);
        }

        protected virtual bool CanPerformAbility(CellGrid cellGrid)
        {
            return false;
        }

        protected override bool CanPerform(CellGrid cellGrid)
        {
            return CanPerformAbility(cellGrid);
        }

        public virtual IDictionary<string, string> Encapsulate()
        {
            throw new NotImplementedException();
        }

        protected virtual IEnumerator Apply(CellGrid cellGrid, IDictionary<string, string> actionParams, bool isNetworkInvoked = true)
        {
            throw new NotImplementedException();
        }

        public override void OnCellClicked(Cell cell, CellGrid grid)
        {
            HandleCellClicked(cell, grid);
        }

        public override void OnCellHighlighted(Cell cell, CellGrid grid)
        {
            HandleCellSelected(cell, grid);
        }

        public override void OnCellDehighlighted(Cell cell, CellGrid grid)
        {
            HandleCellDeselected(cell, grid);
        }

        public override void OnUnitClicked(Unit unit, CellGrid grid)
        {
            HandleUnitClicked(unit, grid);
        }

        public override void OnUnitHighlighted(Unit unit, CellGrid grid)
        {
            HandleUnitHighlighted(unit, grid);
        }

        public override void OnUnitDehighlighted(Unit unit, CellGrid grid)
        {
            HandleUnitDehighlighted(unit, grid);
        }

        private IEnumerator ExecuteInline(CellGrid cellGrid, BattleActionExecutionMode executionMode, bool isNetworkInvoked)
        {
            if (cellGrid == null)
            {
                yield break;
            }

            Action beforeAction = null;
            Action afterAction = null;

            switch (executionMode)
            {
                case BattleActionExecutionMode.HumanLocal:
                    beforeAction = cellGrid.EnterBlockedInputState;
                    afterAction = () => cellGrid.EnterSelectedState(UnitRef);
                    break;
                case BattleActionExecutionMode.RemoteInvocation:
                    beforeAction = cellGrid.EnterRemotePlayerTurnState;
                    break;
                case BattleActionExecutionMode.AiLocal:
                    afterAction = () => OnAbilityDeselected(cellGrid);
                    break;
            }

            yield return BattleActionExecutionFlow.ExecuteInline(
                this,
                beforeAction,
                () => Act(cellGrid, isNetworkInvoked),
                afterAction);
        }
    }
}
