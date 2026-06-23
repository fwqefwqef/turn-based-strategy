using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Game.Grid;

namespace Windy.Srpg.Game.Abilities
{
    public class AttackAbility : Ability
    {
        public Unit UnitToAttack { get; set; }
        public int UnitToAttackID { get; set; }

        private List<Unit> inAttackRange;

        protected override IEnumerator Act(CellGrid cellGrid, bool isNetworkInvoked = false)
        {
            if (CanPerform(cellGrid) && UnitToAttack != null && UnitRef.IsUnitAttackable(UnitToAttack, UnitRef.Cell))
            {
                UnitRef.AttackHandler(UnitToAttack);
                yield return new WaitUntil(() => UnitRef == null || !UnitRef.IsAttackSequenceRunning);
            }
        }

        protected override void Display(CellGrid cellGrid)
        {
            if (cellGrid != null)
            {
                inAttackRange = cellGrid.GetEnemyUnits(cellGrid.CurrentPlayer)
                    .Where(u => UnitRef.IsUnitAttackable(u, UnitRef.Cell))
                    .ToList();
            }
            else
            {
                inAttackRange = new List<Unit>();
            }
        }

        protected override void HandleUnitClicked(Unit unit, CellGrid cellGrid)
        {
            if (cellGrid != null
                && cellGrid.GetCurrentPlayerUnits().Contains(unit)
                && !unit.IsFinishedForTurn)
            {
                cellGrid.EnterSelectedState(unit);
            }
        }

        protected override void HandleCellClicked(Cell cell, CellGrid cellGrid)
        {
            if (cell == null)
            {
                return;
            }

            cellGrid?.EnterWaitingState();
        }

        protected override void CleanUp(CellGrid cellGrid)
        {
            inAttackRange?.ForEach(u => u?.UnMark());
        }

        protected override bool CanPerformAbility(CellGrid cellGrid)
        {
            if (!UnitRef.CanStartActionThisTurn || !UnitRef.HasUsableWeapon)
            {
                return false;
            }

            if (cellGrid == null)
            {
                return false;
            }

            inAttackRange = cellGrid.GetEnemyUnits(cellGrid.CurrentPlayer)
                .Where(u => UnitRef.IsUnitAttackable(u, UnitRef.Cell))
                .ToList();

            return inAttackRange.Count > 0;
        }
    }
}
