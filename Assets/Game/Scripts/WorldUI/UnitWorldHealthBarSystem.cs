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
        [SerializeField] private CustomCellGrid cellGrid;

        private readonly HashSet<CustomUnit> _trackedUnits = new HashSet<CustomUnit>();

        private void OnEnable()
        {
            if (cellGrid == null)
            {
                cellGrid = FindAnyObjectByType<CustomCellGrid>();
            }

            if (cellGrid == null)
            {
                Debug.LogWarning("UnitWorldHealthBarSystem: CellGrid reference is missing.");
                return;
            }

            cellGrid.LevelInitialized += OnLevelLoadingDone;
            cellGrid.CustomUnitAdded += OnUnitAdded;

            AttachBarsToExistingUnits();
        }

        private void OnDisable()
        {
            if (cellGrid != null)
            {
                cellGrid.LevelInitialized -= OnLevelLoadingDone;
                cellGrid.CustomUnitAdded -= OnUnitAdded;
            }

            _trackedUnits.Clear();
        }

        private void OnLevelLoadingDone(object sender, EventArgs e)
        {
            AttachBarsToExistingUnits();
        }

        private void OnUnitAdded(object sender, CustomUnitAddedEventArgs e)
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

            var units = cellGrid.GetAllCustomUnits();
            for (int i = 0; i < units.Count; i++)
            {
                CustomUnit unit = units[i];
                if (unit != null)
                {
                    AttachBar(unit);
                }
            }
        }

        private void AttachBar(CustomUnit unit)
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


