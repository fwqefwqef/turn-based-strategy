using System;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Units
{
    public partial class Unit
    {
        public event EventHandler UnitClicked;
        public event EventHandler UnitHighlighted;
        public event EventHandler UnitDehighlighted;
        public event EventHandler UnitSelected;
        public event EventHandler UnitDeselected;
        public event EventHandler<AttackEventArgs> UnitDestroyed;

        public int UnitID { get; set; }
        public bool Obstructable = true;

        [SerializeField, HideInInspector]
        private bool excludedFromBattle;

        public bool ExcludedFromBattle
        {
            get => excludedFromBattle;
            set => excludedFromBattle = value;
        }

        [SerializeField, HideInInspector]
        private BattleSquareCell cell;

        public BattleSquareCell Cell
        {
            get => cell;
            set => cell = value;
        }

        [SerializeField]
        private float movementPointsStorage;

        public int PlayerNumber;
        public float MovementAnimationSpeed;

        public virtual float MovementPoints
        {
            get => movementPointsStorage;
            set => SetMovementPoints(value, syncRuntimeMirror: true);
        }

        internal void SetMovementPoints(float value, bool syncRuntimeMirror)
        {
            movementPointsStorage = value;
            if (syncRuntimeMirror)
            {
                SyncMirroredRuntimeMovementPoints();
            }
        }

        internal void RaiseUnitClicked() => UnitClicked?.Invoke(this, EventArgs.Empty);

        internal void RaiseUnitHighlighted() => UnitHighlighted?.Invoke(this, EventArgs.Empty);

        internal void RaiseUnitDehighlighted() => UnitDehighlighted?.Invoke(this, EventArgs.Empty);

        internal void RaiseUnitDestroyed(AttackEventArgs args) => UnitDestroyed?.Invoke(this, args);

        internal void RegisterCellOccupancy(BattleSquareCell targetCell = null)
        {
            BattleSquareCell resolvedCell = targetCell ?? Cell;
            if (resolvedCell == null)
            {
                return;
            }

            BoardUnit runtimeUnit = GetComponent<BoardUnit>();
            runtimeUnit?.AssignCellImmediate(resolvedCell, syncTransform: false);
        }

        internal void UnregisterCellOccupancy(BattleSquareCell targetCell = null)
        {
            BattleSquareCell resolvedCell = targetCell ?? Cell;
            BoardUnit runtimeUnit = GetComponent<BoardUnit>();
            if (runtimeUnit != null && runtimeUnit.CurrentCell == resolvedCell)
            {
                runtimeUnit.ClearCurrentCell();
            }
        }
    }
}

