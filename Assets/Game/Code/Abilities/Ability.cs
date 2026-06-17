using System;
using System.Collections;
using System.Collections.Generic;
using Windy.Srpg.Game.Units;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Runtime.Actions;
using Windy.Srpg.Runtime.Grid;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Abilities
{
    public abstract class Ability : BattleAction
    {
        protected Unit UnitRef
        {
            get
            {
                Unit customUnit = GetUnit<Unit>();
                if (customUnit != null)
                {
                    return customUnit;
                }

                return GetUnit<GridUnit>()?.GetComponent<Unit>();
            }
        }

        protected Unit UnitReference => UnitRef;

        protected static CellGrid ResolveCellGrid(IGridContext grid)
        {
            if (grid is CellGrid customCellGrid)
            {
                return customCellGrid;
            }

            return (grid as RuntimeGrid)?.GetComponent<CellGrid>();
        }

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

        protected sealed override IEnumerator Act(IGridContext grid, bool isRemoteInvocation = false)
        {
            CellGrid customCellGrid = ResolveCellGrid(grid);
            if (customCellGrid == null)
            {
                yield break;
            }

            yield return StartCoroutine(Act(customCellGrid, isRemoteInvocation));
        }

        protected virtual IEnumerator Act(CellGrid cellGrid, bool isNetworkInvoked = false)
        {
            yield break;
        }

        public virtual void Initialize()
        {
        }

        public override void InitializeAction(IGridUnit unit)
        {
            base.InitializeAction(unit);
            Initialize();
        }

        public override bool CanPerformAction(IGridContext grid)
        {
            return CanPerform(ResolveCellGrid(grid));
        }

        public virtual void OnUnitClicked(Unit unit, CellGrid cellGrid) { }

        public virtual void OnUnitHighlighted(Unit unit, CellGrid cellGrid) { }

        public virtual void OnUnitDehighlighted(Unit unit, CellGrid cellGrid) { }

        protected virtual void OnUnitDestroyed(CellGrid cellGrid) { }

        protected virtual void OnCellClicked(Cell cell, CellGrid cellGrid) { }

        protected virtual void OnCellSelected(Cell cell, CellGrid cellGrid) { }

        protected virtual void OnCellDeselected(Cell cell, CellGrid cellGrid) { }

        protected virtual void Display(CellGrid cellGrid) { }

        public override void DisplayAction(IGridContext grid)
        {
            Display(ResolveCellGrid(grid));
        }

        protected virtual void CleanUp(CellGrid cellGrid) { }

        public override void CleanUpAction(IGridContext grid)
        {
            CleanUp(ResolveCellGrid(grid));
        }

        protected virtual void OnAbilitySelected(CellGrid cellGrid) { }

        public override void OnActionSelected(IGridContext grid)
        {
            OnAbilitySelected(ResolveCellGrid(grid));
        }

        protected virtual void OnAbilityDeselected(CellGrid cellGrid) { }

        public override void OnActionDeselected(IGridContext grid)
        {
            OnAbilityDeselected(ResolveCellGrid(grid));
        }

        protected virtual void OnTurnStart(CellGrid cellGrid) { }

        public override void OnTurnStarted(IGridContext grid)
        {
            OnTurnStart(ResolveCellGrid(grid));
        }

        protected virtual void OnTurnEnd(CellGrid cellGrid) { }

        public override void OnTurnEnded(IGridContext grid)
        {
            OnTurnEnd(ResolveCellGrid(grid));
        }

        public override void OnOwnerDestroyed(IGridContext grid)
        {
            OnUnitDestroyed(ResolveCellGrid(grid));
        }

        protected virtual bool CanPerform(CellGrid cellGrid) { return false; }

        public virtual IDictionary<string, string> Encapsulate() { throw new NotImplementedException(); }

        protected virtual IEnumerator Apply(CellGrid cellGrid, IDictionary<string, string> actionParams, bool isNetworkInvoked = true) { throw new NotImplementedException(); }

        public override void OnCellClicked(Cell cell, IGridContext grid)
        {
            OnCellClicked(cell, ResolveCellGrid(grid));
        }

        public override void OnCellHighlighted(Cell cell, IGridContext grid)
        {
            OnCellSelected(cell, ResolveCellGrid(grid));
        }

        public override void OnCellDehighlighted(Cell cell, IGridContext grid)
        {
            OnCellDeselected(cell, ResolveCellGrid(grid));
        }

        public override void OnUnitClicked(IGridUnit unit, IGridContext grid)
        {
            OnUnitClicked(unit as Unit, ResolveCellGrid(grid));
        }

        public override void OnUnitHighlighted(IGridUnit unit, IGridContext grid)
        {
            OnUnitHighlighted(unit as Unit, ResolveCellGrid(grid));
        }

        public override void OnUnitDehighlighted(IGridUnit unit, IGridContext grid)
        {
            OnUnitDehighlighted(unit as Unit, ResolveCellGrid(grid));
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

