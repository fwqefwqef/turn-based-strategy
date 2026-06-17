using System;
using TbsFramework.Cells;
using TbsFramework.Units;
using TbsFramework.Units.Highlighters;
using UnityEngine;

namespace Windy.Srpg.Game.Units
{
    public partial class CustomUnit
    {
        public event EventHandler UnitClicked;
        public event EventHandler UnitHighlighted;
        public event EventHandler UnitDehighlighted;
        public event EventHandler UnitSelected;
        public event EventHandler UnitDeselected;
        public event EventHandler<AttackEventArgs> UnitAttacked;
        public event EventHandler<AttackEventArgs> UnitDestroyed;
        public event EventHandler<MovementEventArgs> UnitMoved;

        public UnitHighlighterAggregator unitHighlighter;
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
        private Cell cell;

        public Cell Cell
        {
            get => cell;
            set
            {
                cell = value;
                FrameworkUnitAnchor anchor = cachedFrameworkAnchor;
                if (anchor != null)
                {
                    anchor.Cell = value;
                }
            }
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
    }
}
