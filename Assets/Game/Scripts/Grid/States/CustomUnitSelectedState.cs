using System.Collections.Generic;
using System.Linq;
using TbsFramework.Cells;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Game.Units;
using UnityEngine;
using Windy.Srpg.Runtime.Actions;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Grid.States
{
    public class CustomUnitSelectedState : CustomCellGridState
    {
        private readonly List<IBattleAction> abilities;
        private readonly CustomUnit selectedUnit;

        public CustomUnitSelectedState(CustomCellGrid cellGrid, CustomUnit unit, IEnumerable<IBattleAction> abilities) : base(cellGrid)
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

        public CustomUnitSelectedState(CustomCellGrid cellGrid, CustomUnit unit, IBattleAction ability)
            : this(cellGrid, unit, new[] { ability })
        {
        }

        public CustomUnit SelectedUnit => selectedUnit;

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

        public override void OnCustomUnitClicked(CustomUnit unit)
        {
            if (_cellGrid.ShouldRouteHumanMovementThroughRuntime)
            {
                return;
            }

            HandleLegacyUnitClick(unit);
        }

        public override void OnCustomUnitHighlighted(CustomUnit unit)
        {
            IBattleUnit battleUnit = unit;
            abilities.ForEach(action => action.OnUnitHighlighted(battleUnit, _cellGrid));
        }

        public override void OnCustomUnitDehighlighted(CustomUnit unit)
        {
            IBattleUnit battleUnit = unit;
            abilities.ForEach(action => action.OnUnitDehighlighted(battleUnit, _cellGrid));
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
            _cellGrid.ShadowCompareRightClick(selectedUnit, null);
            _cellGrid.EnterWaitingState();
        }

        private void HandleLegacyUnitClick(CustomUnit unit)
        {
            if (unit == selectedUnit)
            {
                var customMoveAbility = abilities.OfType<CustomMoveAbility>().FirstOrDefault();
                if (customMoveAbility != null)
                {
                    _cellGrid.ShadowCompareSelectedStateUnitClick(
                        selectedUnit,
                        unit,
                        frameworkStateLabel: "PendingMoveConfirm",
                        frameworkSelectedUnitAfterClick: selectedUnit,
                        frameworkPendingDestination: selectedUnit.Cell);
                    customMoveAbility.OnSelectedUnitClicked(_cellGrid);
                    return;
                }
            }

            bool willSelectAnotherFriendly =
                unit != null
                && _cellGrid.GetCurrentPlayerCustomUnits().Contains(unit)
                && !unit.IsFinishedForTurn;

            _cellGrid.ShadowCompareSelectedStateUnitClick(
                selectedUnit,
                unit,
                frameworkStateLabel: willSelectAnotherFriendly ? "Selected" : "Waiting",
                frameworkSelectedUnitAfterClick: willSelectAnotherFriendly ? unit : null,
                frameworkPendingDestination: null);

            IBattleUnit battleUnit = unit;
            abilities.ForEach(action => action.OnUnitClicked(battleUnit, _cellGrid));
        }

        private static string Describe(Cell cell)
        {
            return cell == null ? "<none>" : cell.OffsetCoord.ToString();
        }
    }
}
