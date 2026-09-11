using System;
using System.Collections.Generic;
using Windy.Srpg.Game.Units;
using UnityEngine;
using Windy.Srpg.Game.Grid;

namespace Windy.Srpg.Game.UI
{
    [DisallowMultipleComponent]
    public class UnitWorldHealthBarSystem : MonoBehaviour
    {
        [SerializeField] private CellGrid cellGrid;

        private readonly HashSet<Unit> _trackedUnits = new HashSet<Unit>();

        private void OnEnable()
        {
            if (cellGrid == null)
            {
                cellGrid = FindAnyObjectByType<CellGrid>();
            }

            if (cellGrid == null)
            {
                Debug.LogWarning("UnitWorldHealthBarSystem: CellGrid reference is missing.");
                return;
            }

            cellGrid.LevelInitialized += OnLevelLoadingDone;
            cellGrid.UnitAdded += OnUnitAdded;

            AttachBarsToExistingUnits();
        }

        private void OnDisable()
        {
            if (cellGrid != null)
            {
                cellGrid.LevelInitialized -= OnLevelLoadingDone;
                cellGrid.UnitAdded -= OnUnitAdded;
            }

            _trackedUnits.Clear();
        }

        private void OnLevelLoadingDone(object sender, EventArgs e)
        {
            AttachBarsToExistingUnits();
        }

        private void OnUnitAdded(object sender, UnitAddedEventArgs e)
        {
            if (e?.Unit == null)
            {
                return;
            }

            AttachBar(e.Unit);
        }

        private void AttachBarsToExistingUnits()
        {
            if (cellGrid == null)
            {
                return;
            }

            var units = cellGrid.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                Unit unit = units[i];
                if (unit != null)
                {
                    AttachBar(unit);
                }
            }
        }

        private void AttachBar(Unit unit)
        {
            if (!_trackedUnits.Add(unit))
            {
                return;
            }

            UnitWorldHealthBar existingBar = unit.GetComponent<UnitWorldHealthBar>();
            if (existingBar == null)
            {
                existingBar = unit.gameObject.AddComponent<UnitWorldHealthBar>();
            }

            unit.RefreshHealthState();
        }
    }
}



