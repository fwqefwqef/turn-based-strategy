using System;
using System.Collections.Generic;
using TbsFramework.Cells;
using TbsFramework.Grid;
using TbsFramework.Players;
using TbsFramework.Units;
using UnityEngine;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Grid
{
    /// <summary>
    /// Thin framework <see cref="CellGrid"/> token on the battle scene host GameObject.
    /// Keeps legacy cell/unit registries and turn-transition hooks while <see cref="CustomCellGrid"/>
    /// owns game behaviour.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CustomCellGrid))]
    public sealed class FrameworkCellGridAnchor : CellGrid
    {
        private CustomCellGrid Owner => GetComponent<CustomCellGrid>();

        internal void SyncHostSettings(bool is2D, Transform playersParent, bool shouldStartGameImmediately)
        {
            Is2D = is2D;
            PlayersParent = playersParent;
            // Host CustomCellGrid owns initialize/start; never auto-start from the anchor token.
            ShouldStartGameImmediately = false;
        }

        private void Start()
        {
        }

        public override void AddUnit(Transform unitTransform, Cell targetCell = null, Player ownerPlayer = null)
        {
            Owner?.RegisterSceneUnitTransform(unitTransform, targetCell, ownerPlayer);
        }

        protected override void DispatchCellDeselected(Cell cell)
        {
            if (Owner != null && Owner.TryDispatchCellDeselected(cell))
            {
                return;
            }

            base.DispatchCellDeselected(cell);
        }

        protected override void DispatchCellSelected(Cell cell)
        {
            if (Owner != null && Owner.TryDispatchCellSelected(cell))
            {
                return;
            }

            base.DispatchCellSelected(cell);
        }

        protected override void DispatchCellClicked(Cell cell)
        {
            if (Owner != null && Owner.TryDispatchCellClicked(cell))
            {
                return;
            }

            base.DispatchCellClicked(cell);
        }

        protected override void DispatchUnitClicked(Unit unit)
        {
            if (Owner != null && Owner.TryDispatchUnitClicked(unit))
            {
                return;
            }

            base.DispatchUnitClicked(unit);
        }

        protected override void DispatchUnitHighlighted(Unit unit)
        {
            if (Owner != null && Owner.TryDispatchUnitHighlighted(unit))
            {
                return;
            }

            base.DispatchUnitHighlighted(unit);
        }

        protected override void DispatchUnitDehighlighted(Unit unit)
        {
            if (Owner != null && Owner.TryDispatchUnitDehighlighted(unit))
            {
                return;
            }

            base.DispatchUnitDehighlighted(unit);
        }

        protected override void NotifyTurnStarted(Unit unit)
        {
            if (Owner != null && Owner.TryNotifyTurnStarted(unit))
            {
                return;
            }

            base.NotifyTurnStarted(unit);
        }

        protected override void NotifyTurnEnded(Unit unit)
        {
            if (Owner != null && Owner.TryNotifyTurnEnded(unit))
            {
                return;
            }

            base.NotifyTurnEnded(unit);
        }

        protected override void NotifyOwnerDestroyed(Unit unit)
        {
            if (Owner != null && Owner.TryNotifyOwnerDestroyed(unit))
            {
                return;
            }

            base.NotifyOwnerDestroyed(unit);
        }

        internal int AllocateNextUnitIdInternal() => AllocateNextUnitId();

        internal void NotifyUnitAddedInternal(Transform unitTransform) => NotifyUnitAdded(unitTransform);

        internal void CommitTurnTransitionInternal(
            RoundRobinTurnPlan plan,
            bool isNetworkInvoked = false,
            bool kickPlayerPlay = true,
            bool syncUnitTurnHooks = true)
        {
            CommitTurnTransition(plan, isNetworkInvoked, kickPlayerPlay, syncUnitTurnHooks);
        }

        internal void SyncBattleStartFromPlanInternal(
            RoundRobinTurnPlan plan,
            bool kickPlayerPlay = true,
            bool syncUnitTurnHooks = true)
        {
            SyncBattleStartFromPlan(plan, kickPlayerPlay, syncUnitTurnHooks);
        }

        internal void EndUnitsForCurrentPlayerTurnInternal() => EndUnitsForCurrentPlayerTurn();

        internal bool CheckGameFinishedInternal() => CheckGameFinished();

        internal bool TryApplyBattleOutcomeInternal(BattleOutcome outcome) => TryApplyBattleOutcome(outcome);

        internal void InstallEndTurnRouter(CustomCellGridEndTurnRouter router)
        {
            cellGridState = router;
        }

        internal void SubscribeUnitInputHandlers(Unit unit)
        {
            if (unit == null)
            {
                return;
            }

            unit.UnitClicked += OnUnitClicked;
            unit.UnitHighlighted += OnUnitHighlighted;
            unit.UnitDehighlighted += OnUnitDehighlighted;
            unit.UnitDestroyed += OnUnitDestroyed;
            unit.UnitMoved += OnUnitMoved;
        }

        internal void UnsubscribeUnitInputHandlers(Unit unit)
        {
            if (unit == null)
            {
                return;
            }

            unit.UnitClicked -= OnUnitClicked;
            unit.UnitHighlighted -= OnUnitHighlighted;
            unit.UnitDehighlighted -= OnUnitDehighlighted;
            unit.UnitDestroyed -= OnUnitDestroyed;
            unit.UnitMoved -= OnUnitMoved;
        }
    }
}
