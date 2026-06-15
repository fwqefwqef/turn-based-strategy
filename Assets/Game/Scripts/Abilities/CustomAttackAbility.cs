using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TbsFramework.Cells;
using Windy.Srpg.Game.Units;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Grid.States;

namespace Windy.Srpg.Game.Abilities
{
    public class CustomAttackAbility : CustomAbility
    {
        public CustomUnit UnitToAttack { get; set; }
        public int UnitToAttackID { get; set; }

        private List<CustomUnit> inAttackRange;

        protected override IEnumerator Act(CustomCellGrid cellGrid, bool isNetworkInvoked = false)
        {
            if (CanPerform(cellGrid) && UnitToAttack != null && CustomUnitRef.IsUnitAttackable(UnitToAttack, CustomUnitRef.Cell))
            {
                CustomUnitRef.AttackHandler(UnitToAttack);
                yield return new WaitUntil(() => CustomUnitRef == null || !CustomUnitRef.IsAttackSequenceRunning);
            }
        }

        protected override void Display(CustomCellGrid cellGrid)
        {
            if (cellGrid != null)
            {
                inAttackRange = cellGrid.GetEnemyUnits(cellGrid.CurrentCustomPlayer)
                    .Where(u => CustomUnitRef.IsUnitAttackable(u, CustomUnitRef.Cell))
                    .ToList();
            }
            else
            {
                inAttackRange = new List<CustomUnit>();
            }
        }

        public override void OnUnitClicked(CustomUnit unit, CustomCellGrid cellGrid)
        {
            if (CustomUnitRef.IsUnitAttackable(unit, CustomUnitRef.Cell))
            {
                UnitToAttack = unit;
                UnitToAttackID = unit.UnitID;
                StartCoroutine(HumanExecute(cellGrid));
            }
            else if (cellGrid != null
                && cellGrid.GetCurrentPlayerCustomUnits().Contains(unit)
                && !unit.IsFinishedForTurn)
            {
                cellGrid.SetState(new CustomUnitSelectedState(cellGrid, unit, unit.GetBattleActions()));
            }
        }

        protected override void OnCellClicked(Cell cell, CustomCellGrid cellGrid)
        {
            cellGrid?.SetState(new CustomCellGridStateWaitingForInput(cellGrid));
        }

        protected override void CleanUp(CustomCellGrid cellGrid)
        {
            inAttackRange?.ForEach(u => u?.UnMark());
        }

        protected override bool CanPerform(CustomCellGrid cellGrid)
        {
            if (!CustomUnitRef.CanStartActionThisTurn || !CustomUnitRef.HasUsableWeapon)
            {
                return false;
            }

            if (cellGrid == null)
            {
                return false;
            }

            inAttackRange = cellGrid.GetEnemyUnits(cellGrid.CurrentCustomPlayer)
                .Where(u => CustomUnitRef.IsUnitAttackable(u, CustomUnitRef.Cell))
                .ToList();
            return inAttackRange.Count > 0;
        }

        public override IDictionary<string, string> Encapsulate()
        {
            return new Dictionary<string, string>
            {
                ["target_id"] = UnitToAttackID.ToString()
            };
        }

        protected override IEnumerator Apply(CustomCellGrid cellGrid, IDictionary<string, string> actionParams, bool isNetworkInvoked = false)
        {
            var targetID = int.Parse(actionParams["target_id"]);
            UnitToAttack = cellGrid?.GetAllCustomUnits().FirstOrDefault(u => u.UnitID == targetID);
            UnitToAttackID = targetID;
            yield return StartCoroutine(RemoteExecute(cellGrid));
        }
    }
}
