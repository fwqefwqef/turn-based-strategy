using System;
using System.Collections;
using System.Collections.Generic;
using Windy.Srpg.Game.Units;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Runtime.Actions;
using Windy.Srpg.Runtime.Board;
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

                return GetUnit<BoardUnit>()?.GetComponent<Unit>();
            }
        }

        protected Unit UnitReference => UnitRef;

        protected static CellGrid ResolveCellGrid(IBattleBoard board)
        {
            if (board is CellGrid customCellGrid)
            {
                return customCellGrid;
            }

            return (board as BattleBoard)?.GetComponent<CellGrid>();
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

        protected sealed override IEnumerator Act(IBattleBoard board, bool isRemoteInvocation = false)
        {
            CellGrid customCellGrid = ResolveCellGrid(board);
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

        public override void InitializeAction(IBoardUnit unit)
        {
            base.InitializeAction(unit);
            Initialize();
        }

        public override bool CanPerformAction(IBattleBoard board)
        {
            return CanPerform(ResolveCellGrid(board));
        }

        public virtual void OnUnitClicked(Unit unit, CellGrid cellGrid) { }

        public virtual void OnUnitHighlighted(Unit unit, CellGrid cellGrid) { }

        public virtual void OnUnitDehighlighted(Unit unit, CellGrid cellGrid) { }

        protected virtual void OnUnitDestroyed(CellGrid cellGrid) { }

        protected virtual void OnCellClicked(BattleSquareCell cell, CellGrid cellGrid) { }

        protected virtual void OnCellSelected(BattleSquareCell cell, CellGrid cellGrid) { }

        protected virtual void OnCellDeselected(BattleSquareCell cell, CellGrid cellGrid) { }

        protected virtual void Display(CellGrid cellGrid) { }

        public override void DisplayAction(IBattleBoard board)
        {
            Display(ResolveCellGrid(board));
        }

        protected virtual void CleanUp(CellGrid cellGrid) { }

        public override void CleanUpAction(IBattleBoard board)
        {
            CleanUp(ResolveCellGrid(board));
        }

        protected virtual void OnAbilitySelected(CellGrid cellGrid) { }

        public override void OnActionSelected(IBattleBoard board)
        {
            OnAbilitySelected(ResolveCellGrid(board));
        }

        protected virtual void OnAbilityDeselected(CellGrid cellGrid) { }

        public override void OnActionDeselected(IBattleBoard board)
        {
            OnAbilityDeselected(ResolveCellGrid(board));
        }

        protected virtual void OnTurnStart(CellGrid cellGrid) { }

        public override void OnTurnStarted(IBattleBoard board)
        {
            OnTurnStart(ResolveCellGrid(board));
        }

        protected virtual void OnTurnEnd(CellGrid cellGrid) { }

        public override void OnTurnEnded(IBattleBoard board)
        {
            OnTurnEnd(ResolveCellGrid(board));
        }

        public override void OnOwnerDestroyed(IBattleBoard board)
        {
            OnUnitDestroyed(ResolveCellGrid(board));
        }

        protected virtual bool CanPerform(CellGrid cellGrid) { return false; }

        public virtual IDictionary<string, string> Encapsulate() { throw new NotImplementedException(); }

        protected virtual IEnumerator Apply(CellGrid cellGrid, IDictionary<string, string> actionParams, bool isNetworkInvoked = true) { throw new NotImplementedException(); }

        public override void OnCellClicked(IBattleCell cell, IBattleBoard board)
        {
            OnCellClicked(ResolveRegistryCellFromBattleCell(cell), ResolveCellGrid(board));
        }

        public override void OnCellHighlighted(IBattleCell cell, IBattleBoard board)
        {
            OnCellSelected(ResolveRegistryCellFromBattleCell(cell), ResolveCellGrid(board));
        }

        public override void OnCellDehighlighted(IBattleCell cell, IBattleBoard board)
        {
            OnCellDeselected(ResolveRegistryCellFromBattleCell(cell), ResolveCellGrid(board));
        }

        private static BattleSquareCell ResolveRegistryCellFromBattleCell(IBattleCell cell)
        {
            return CellGrid.ResolveRegistryCellFromBattleCell(cell);
        }

        public override void OnUnitClicked(IBoardUnit unit, IBattleBoard board)
        {
            OnUnitClicked(unit as Unit, ResolveCellGrid(board));
        }

        public override void OnUnitHighlighted(IBoardUnit unit, IBattleBoard board)
        {
            OnUnitHighlighted(unit as Unit, ResolveCellGrid(board));
        }

        public override void OnUnitDehighlighted(IBoardUnit unit, IBattleBoard board)
        {
            OnUnitDehighlighted(unit as Unit, ResolveCellGrid(board));
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

