using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Game.Units;
using UnityEngine;
using Windy.Srpg.Runtime.Actions;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Grid.States
{
    public class UnitSelectedState : CellGridState
    {
        private readonly List<IBattleAction> abilities;
        private readonly Unit selectedUnit;

        public UnitSelectedState(CellGrid cellGrid, Unit unit, IEnumerable<IBattleAction> abilities) : base(cellGrid)
        {
            List<IBattleAction> resolvedAbilities = abilities?
                .Where(ability => ability != null)
                .ToList()
                ?? new List<IBattleAction>();

            if (resolvedAbilities.Count == 0)
            {
                Debug.LogError("No abilities were selected, check if your unit has any abilities attached to it");
            }

            this.abilities = resolvedAbilities;
            selectedUnit = unit;
        }

        public UnitSelectedState(CellGrid cellGrid, Unit unit, IBattleAction ability)
            : this(cellGrid, unit, new[] { ability })
        {
        }

        public Unit SelectedUnit => selectedUnit;

        public override void OnStateEnter()
        {
            base.OnStateEnter();
            selectedUnit.OnUnitSelected();
            abilities.ForEach(action => action.OnActionSelected(_cellGrid));
            abilities.ForEach(action => action.DisplayAction(_cellGrid));
        }

        public override void OnStateExit()
        {
            abilities.ForEach(action => action.CleanUpAction(_cellGrid));
            selectedUnit.OnUnitDeselected();
        }

        public override void OnUnitClicked(Unit unit)
        {
            if (_cellGrid.ShouldRouteHumanMovementThroughRuntime)
            {
                return;
            }

            HandleLegacyUnitClick(unit);
        }

        public override void OnUnitHighlighted(Unit unit)
        {
            IBoardUnit BoardUnit = unit;
            abilities.ForEach(action => action.OnUnitHighlighted(BoardUnit, _cellGrid));
        }

        public override void OnUnitDehighlighted(Unit unit)
        {
            IBoardUnit BoardUnit = unit;
            abilities.ForEach(action => action.OnUnitDehighlighted(BoardUnit, _cellGrid));
        }

        public override void OnCellClicked(IBattleCell cell)
        {
            if (_cellGrid.ShouldRouteHumanMovementThroughRuntime)
            {
                return;
            }

            abilities.ForEach(action => action.OnCellClicked(cell, _cellGrid));
        }

        public override void OnCellSelected(IBattleCell cell)
        {
            base.OnCellSelected(cell);
            abilities.ForEach(action => action.OnCellHighlighted(cell, _cellGrid));
        }

        public override void OnCellDeselected(IBattleCell cell)
        {
            base.OnCellDeselected(cell);
            abilities.ForEach(action => action.OnCellDehighlighted(cell, _cellGrid));
        }

        public override void OnRightClick()
        {
            if (_cellGrid.ShouldRouteHumanMovementThroughRuntime)
            {
                return;
            }

            _cellGrid.EnterWaitingState();
        }

        private void HandleLegacyUnitClick(Unit unit)
        {
            if (unit == selectedUnit)
            {
                var customMoveAbility = abilities.OfType<MoveAbility>().FirstOrDefault();
                if (customMoveAbility != null)
                {
                    customMoveAbility.OnSelectedUnitClicked(_cellGrid);
                    return;
                }
            }

            if (unit != null
                && _cellGrid.GetCurrentPlayerUnits().Contains(unit)
                && !unit.IsFinishedForTurn)
            {
                _cellGrid.EnterSelectedState(unit);
                return;
            }

            _cellGrid.EnterWaitingState();
        }
    }
}

