using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.AI;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Game.Players;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Actions;
using Windy.Srpg.Runtime.Grid;
using Windy.Srpg.Runtime.Rendering;

namespace Windy.Srpg.Game.Grid.States
{
    // --- Base state + right-click contract ---
    public interface IRightClickHandler
    {
        void OnRightClick();
    }

    public abstract class CellGridState : IRightClickHandler
    {
        protected readonly CellGrid _cellGrid;

        protected CellGridState(CellGrid cellGrid)
        {
            _cellGrid = cellGrid;
        }

        public virtual bool BlocksEndTurn => false;

        public virtual CellGridState MakeTransition(CellGridState nextState)
        {
            return nextState;
        }

        public virtual void OnUnitClicked(Unit unit)
        {
        }

        public virtual void OnUnitHighlighted(Unit unit)
        {
        }

        public virtual void OnUnitDehighlighted(Unit unit)
        {
        }

        public virtual void OnCellDeselected(Cell cell)
        {
            cell?.ClearHighlight();
        }

        public virtual void OnCellSelected(Cell cell)
        {
            // Hover selection is represented by the shared cursor border now.
            // Tile fills should only come from explicit gameplay overlays.
        }

        public virtual void OnCellClicked(Cell cell)
        {
        }

        public virtual void OnStateEnter()
        {
            _cellGrid?.ClearAllCellHighlights();
        }

        public virtual void OnStateExit()
        {
        }

        public virtual void OnRightClick()
        {
        }

    }

    // --- Waiting for input ---
    public class CellGridStateWaitingForInput : CellGridState
    {
        public CellGridStateWaitingForInput(CellGrid cellGrid) : base(cellGrid)
        {
        }

        public override void OnUnitClicked(Unit customUnit)
        {
            bool willSelect = _cellGrid.GetCurrentPlayerUnits().Contains(customUnit)
                && !customUnit.IsFinishedForTurn;

            if (willSelect)
            {
                _cellGrid.EnterSelectedState(customUnit);
            }
        }
    }

    // --- Blocked input + game over ---
    public sealed class CellGridStateBlockInput : CellGridState
    {
        public CellGridStateBlockInput(CellGrid cellGrid) : base(cellGrid)
        {
        }

        public override bool BlocksEndTurn => true;
    }

    public sealed class CellGridStateGameOver : CellGridState
    {
        public CellGridStateGameOver(CellGrid cellGrid) : base(cellGrid)
        {
        }
    }

    // --- Remote player turn ---
    public sealed class CellGridStateRemotePlayerTurn : CellGridState
    {
        public CellGridStateRemotePlayerTurn(CellGrid cellGrid) : base(cellGrid)
        {
        }
    }

    // --- AI turn ---
    public sealed class CellGridStateAiTurn : CellGridState
    {
        private readonly AiPlayer aiPlayer;
        private Dictionary<Cell, AiDebugInfo> cellDebugInfo;

        public CellGridStateAiTurn(CellGrid cellGrid, AiPlayer aiPlayer) : base(cellGrid)
        {
            this.aiPlayer = aiPlayer;
        }

        public Dictionary<Cell, AiDebugInfo> CellDebugInfo
        {
            get => cellDebugInfo;
            set
            {
                cellDebugInfo = value;
                if (!IsDebugEnabled || cellDebugInfo == null)
                {
                    return;
                }

                foreach ((Cell cell, AiDebugInfo debugInfo) in cellDebugInfo)
                {
                    if (cell != null && debugInfo != null)
                    {
                        cell.SetColor(debugInfo.Color);
                    }
                }
            }
        }

        public Dictionary<Unit, string> UnitDebugInfo { get; set; }

        public override void OnCellDeselected(Cell cell)
        {
            base.OnCellDeselected(cell);
            Cell boardCell = cell;
            if (IsDebugEnabled
                && boardCell != null
                && CellDebugInfo != null
                && CellDebugInfo.TryGetValue(boardCell, out AiDebugInfo debugInfo)
                && debugInfo != null)
            {
                boardCell.SetColor(debugInfo.Color);
            }
        }

        public override void OnCellClicked(Cell cell)
        {
            Cell boardCell = cell;
            if (IsDebugEnabled
                && boardCell != null
                && CellDebugInfo != null
                && CellDebugInfo.TryGetValue(boardCell, out AiDebugInfo debugInfo)
                && debugInfo != null
                && !string.IsNullOrWhiteSpace(debugInfo.Metadata))
            {
                Debug.Log(debugInfo.Metadata);
            }
        }

        public override void OnUnitClicked(Unit customUnit)
        {
            if (IsDebugEnabled
                && customUnit != null
                && UnitDebugInfo != null
                && UnitDebugInfo.TryGetValue(customUnit, out string debugText)
                && !string.IsNullOrWhiteSpace(debugText))
            {
                Debug.Log(debugText);
            }
        }

        private bool IsDebugEnabled => aiPlayer != null && aiPlayer.DebugMode;
    }

    // --- Pending move confirm ---
    public class CellGridStateMovePendingConfirm : CellGridState
    {
        private readonly MoveAbility customMoveAbility;

        public CellGridStateMovePendingConfirm(CellGrid cellGrid, MoveAbility customMoveAbility) : base(cellGrid)
        {
            this.customMoveAbility = customMoveAbility;
        }

        public MoveAbility MoveAbility => customMoveAbility;

        public override void OnStateEnter()
        {
            base.OnStateEnter();
            customMoveAbility?.OnPendingMoveStateEnter(_cellGrid);
        }

        public override void OnStateExit()
        {
            customMoveAbility?.OnPendingMoveStateExit(_cellGrid);
        }

        public override void OnUnitClicked(Unit customUnit)
        {
            customMoveAbility?.OnPendingMoveUnitClicked(customUnit, _cellGrid);
        }

        public override void OnUnitHighlighted(Unit customUnit)
        {
            customMoveAbility?.OnPendingMoveUnitHighlighted(customUnit, _cellGrid);
        }

        public override void OnUnitDehighlighted(Unit customUnit)
        {
            customMoveAbility?.OnPendingMoveUnitDehighlighted(customUnit, _cellGrid);
        }

        public override void OnCellClicked(Cell cell)
        {
            customMoveAbility?.OnPendingMoveCellClicked(cell, _cellGrid);
        }

        public override void OnCellSelected(Cell cell)
        {
            customMoveAbility?.OnPendingMoveCellSelected(cell, _cellGrid);
        }

        public override void OnCellDeselected(Cell cell)
        {
            customMoveAbility?.OnPendingMoveCellDeselected(cell, _cellGrid);
        }

        public override void OnRightClick()
        {
            customMoveAbility?.OnPendingMoveRightClicked(_cellGrid);
        }
    }

    // --- Unit selected ---
    public class UnitSelectedState : CellGridState
    {
        private readonly List<BattleAction> abilities;
        private readonly Unit selectedUnit;

        public UnitSelectedState(CellGrid cellGrid, Unit unit, IEnumerable<BattleAction> abilities) : base(cellGrid)
        {
            List<BattleAction> resolvedAbilities = abilities?
                .Where(ability => ability != null)
                .ToList()
                ?? new List<BattleAction>();

            if (resolvedAbilities.Count == 0)
            {
                Debug.LogError("No abilities were selected, check if your unit has any abilities attached to it");
            }

            this.abilities = resolvedAbilities;
            selectedUnit = unit;
        }

        public UnitSelectedState(CellGrid cellGrid, Unit unit, BattleAction ability)
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
            HandleUnitClick(unit);
        }

        public override void OnUnitHighlighted(Unit unit)
        {
            abilities.ForEach(action => action.OnUnitHighlighted(unit, _cellGrid));
        }

        public override void OnUnitDehighlighted(Unit unit)
        {
            abilities.ForEach(action => action.OnUnitDehighlighted(unit, _cellGrid));
        }

        public override void OnCellClicked(Cell cell)
        {
            abilities.ForEach(action => action.OnCellClicked(cell, _cellGrid));
        }

        public override void OnCellSelected(Cell cell)
        {
            base.OnCellSelected(cell);
            abilities.ForEach(action => action.OnCellHighlighted(cell, _cellGrid));
        }

        public override void OnCellDeselected(Cell cell)
        {
            base.OnCellDeselected(cell);
            abilities.ForEach(action => action.OnCellDehighlighted(cell, _cellGrid));
        }

        public override void OnRightClick()
        {
            _cellGrid.EnterWaitingState();
        }

        private void HandleUnitClick(Unit unit)
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

    // --- Pre-battle deployment swap ---
    public sealed class PreBattleDeploymentSwapState : CellGridState
    {
        private readonly CellGrid customCellGrid;

        public PreBattleDeploymentSwapState(CellGrid cellGrid) : base(cellGrid)
        {
            customCellGrid = cellGrid;
        }

        public override void OnUnitClicked(Unit customUnit)
        {
            customCellGrid.HandlePreBattleDeploymentUnitClicked(customUnit);
        }

        public override void OnCellClicked(Cell cell)
        {
            customCellGrid.HandlePreBattleDeploymentCellClicked(cell);
        }

        public override void OnRightClick()
        {
            customCellGrid.CancelPreBattleDeploymentSelection();
        }
    }

}
