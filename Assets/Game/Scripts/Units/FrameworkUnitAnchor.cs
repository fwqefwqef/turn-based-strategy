using System;
using TbsFramework.Units;
using UnityEngine;

namespace Windy.Srpg.Game.Units
{
    /// <summary>
    /// Thin framework <see cref="Unit"/> token kept on the same GameObject as <see cref="CustomUnit"/>
    /// so legacy <see cref="TbsFramework.Grid.CellGrid"/> registries and cell occupancy lists still work.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CustomUnit))]
    [ExecuteInEditMode]
    public sealed class FrameworkUnitAnchor : Unit
    {
        private CustomUnit Owner => GetComponent<CustomUnit>();

        internal void RaiseUnitClicked() => base.RaiseUnitClicked();

        internal void RaiseUnitHighlighted() => base.RaiseUnitHighlighted();

        internal void RaiseUnitDehighlighted() => base.RaiseUnitDehighlighted();

        internal void RaiseUnitDestroyed(AttackEventArgs args) => base.RaiseUnitDestroyed(args);

        internal void RaiseUnitMoved(MovementEventArgs args) => base.RaiseUnitMoved(args);

        public override void Initialize()
        {
        }

        public override void OnTurnStart()
        {
            Owner?.OnTurnStart();
        }

        public override void OnTurnEnd()
        {
            Owner?.OnTurnEnd();
        }

        public override void OnUnitSelected()
        {
            Owner?.OnUnitSelected();
        }

        public override void OnUnitDeselected()
        {
            Owner?.OnUnitDeselected();
        }

        public override void OnMouseDown()
        {
        }

        public override void OnMouseEnter()
        {
        }

        public override void OnMouseExit()
        {
        }

        private new void Reset()
        {
        }
    }
}
