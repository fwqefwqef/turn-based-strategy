using System;
using System.Collections;
using System.Collections.Generic;
using TbsFramework.Cells;
using TbsFramework.Grid;
using Windy.Srpg.Game.Units;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Runtime.Actions;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Abilities
{
    public abstract class CustomAbility : BattleAction
    {
        protected CustomUnit CustomUnitRef
        {
            get
            {
                CustomUnit customUnit = GetUnit<CustomUnit>();
                if (customUnit != null)
                {
                    return customUnit;
                }

                return GetUnit<BattleUnit>()?.GetComponent<CustomUnit>();
            }
        }

        protected CustomUnit UnitReference => CustomUnitRef;

        protected static CustomCellGrid ResolveCustomCellGrid(IBattleBoard board)
        {
            if (board is CustomCellGrid customCellGrid)
            {
                return customCellGrid;
            }

            return (board as BattleBoard)?.GetComponent<CustomCellGrid>();
        }

        protected IEnumerator HumanExecute(CustomCellGrid cellGrid)
        {
            if (cellGrid == null)
            {
                yield break;
            }

            yield return ExecuteInline(cellGrid, BattleActionExecutionMode.HumanLocal, false);
        }

        protected IEnumerator RemoteExecute(CustomCellGrid cellGrid)
        {
            if (cellGrid == null)
            {
                yield break;
            }

            yield return StartCoroutine(ExecuteInline(cellGrid, BattleActionExecutionMode.RemoteInvocation, true));
        }

        public IEnumerator AIExecute(CustomCellGrid cellGrid)
        {
            yield return ExecuteInline(cellGrid, BattleActionExecutionMode.AiLocal, false);
        }

        protected sealed override IEnumerator Act(IBattleBoard board, bool isRemoteInvocation = false)
        {
            CustomCellGrid customCellGrid = ResolveCustomCellGrid(board);
            if (customCellGrid == null)
            {
                yield break;
            }

            yield return StartCoroutine(Act(customCellGrid, isRemoteInvocation));
        }

        protected virtual IEnumerator Act(CustomCellGrid cellGrid, bool isNetworkInvoked = false)
        {
            yield break;
        }

        public virtual void Initialize()
        {
        }

        public override void InitializeAction(IBattleUnit unit)
        {
            base.InitializeAction(unit);
            Initialize();
        }

        public override bool CanPerformAction(IBattleBoard board)
        {
            return CanPerform(ResolveCustomCellGrid(board));
        }

        public void OnUnitClicked(CustomUnit unit, CellGrid cellGrid)
        {
            OnUnitClicked(unit, cellGrid as CustomCellGrid);
        }

        public virtual void OnUnitClicked(CustomUnit unit, CustomCellGrid cellGrid) { }

        public void OnUnitHighlighted(CustomUnit unit, CellGrid cellGrid)
        {
            OnUnitHighlighted(unit, cellGrid as CustomCellGrid);
        }

        public virtual void OnUnitHighlighted(CustomUnit unit, CustomCellGrid cellGrid) { }

        public void OnUnitDehighlighted(CustomUnit unit, CellGrid cellGrid)
        {
            OnUnitDehighlighted(unit, cellGrid as CustomCellGrid);
        }

        public virtual void OnUnitDehighlighted(CustomUnit unit, CustomCellGrid cellGrid) { }

        protected virtual void OnUnitDestroyed(CustomCellGrid cellGrid) { }

        protected virtual void OnCellClicked(Cell cell, CustomCellGrid cellGrid) { }

        protected virtual void OnCellSelected(Cell cell, CustomCellGrid cellGrid) { }

        protected virtual void OnCellDeselected(Cell cell, CustomCellGrid cellGrid) { }

        protected virtual void Display(CustomCellGrid cellGrid) { }

        public override void DisplayAction(IBattleBoard board)
        {
            Display(ResolveCustomCellGrid(board));
        }

        protected virtual void CleanUp(CustomCellGrid cellGrid) { }

        public override void CleanUpAction(IBattleBoard board)
        {
            CleanUp(ResolveCustomCellGrid(board));
        }

        protected virtual void OnAbilitySelected(CustomCellGrid cellGrid) { }

        public override void OnActionSelected(IBattleBoard board)
        {
            OnAbilitySelected(ResolveCustomCellGrid(board));
        }

        protected virtual void OnAbilityDeselected(CustomCellGrid cellGrid) { }

        public override void OnActionDeselected(IBattleBoard board)
        {
            OnAbilityDeselected(ResolveCustomCellGrid(board));
        }

        protected virtual void OnTurnStart(CustomCellGrid cellGrid) { }

        public override void OnTurnStarted(IBattleBoard board)
        {
            OnTurnStart(ResolveCustomCellGrid(board));
        }

        protected virtual void OnTurnEnd(CustomCellGrid cellGrid) { }

        public override void OnTurnEnded(IBattleBoard board)
        {
            OnTurnEnd(ResolveCustomCellGrid(board));
        }

        public override void OnOwnerDestroyed(IBattleBoard board)
        {
            OnUnitDestroyed(ResolveCustomCellGrid(board));
        }

        protected virtual bool CanPerform(CustomCellGrid cellGrid) { return false; }

        public virtual IDictionary<string, string> Encapsulate() { throw new NotImplementedException(); }

        protected virtual IEnumerator Apply(CustomCellGrid cellGrid, IDictionary<string, string> actionParams, bool isNetworkInvoked = true) { throw new NotImplementedException(); }

        public override void OnCellClicked(IBattleCell cell, IBattleBoard board)
        {
            OnCellClicked(cell as Cell, ResolveCustomCellGrid(board));
        }

        public override void OnCellHighlighted(IBattleCell cell, IBattleBoard board)
        {
            OnCellSelected(cell as Cell, ResolveCustomCellGrid(board));
        }

        public override void OnCellDehighlighted(IBattleCell cell, IBattleBoard board)
        {
            OnCellDeselected(cell as Cell, ResolveCustomCellGrid(board));
        }

        public override void OnUnitClicked(IBattleUnit unit, IBattleBoard board)
        {
            OnUnitClicked(unit as CustomUnit, ResolveCustomCellGrid(board));
        }

        public override void OnUnitHighlighted(IBattleUnit unit, IBattleBoard board)
        {
            OnUnitHighlighted(unit as CustomUnit, ResolveCustomCellGrid(board));
        }

        public override void OnUnitDehighlighted(IBattleUnit unit, IBattleBoard board)
        {
            OnUnitDehighlighted(unit as CustomUnit, ResolveCustomCellGrid(board));
        }

        private IEnumerator ExecuteInline(CustomCellGrid cellGrid, BattleActionExecutionMode executionMode, bool isNetworkInvoked)
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
                    afterAction = () => cellGrid.EnterSelectedState(CustomUnitRef);
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
