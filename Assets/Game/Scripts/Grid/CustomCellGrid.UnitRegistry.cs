using System.Collections.Generic;
using TbsFramework.Cells;
using TbsFramework.Players;
using TbsFramework.Units;
using UnityEngine;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Grid
{
    public partial class CustomCellGrid
    {
        internal static CustomUnit ResolveCustomUnitFromRegistryUnit(Unit unit)
        {
            return unit != null ? unit.GetComponent<CustomUnit>() : null;
        }

        internal static IBattleUnit ResolveBattleUnitFromRegistryUnit(Unit unit)
        {
            return ResolveCustomUnitFromRegistryUnit(unit);
        }

        internal void RegisterSceneUnitTransform(Transform unitTransform, Cell targetCell = null, Player ownerPlayer = null)
        {
            CustomUnit customUnit = unitTransform != null ? unitTransform.GetComponent<CustomUnit>() : null;
            if (customUnit == null)
            {
                LegacyGrid.AddUnit(unitTransform, targetCell, ownerPlayer);
                return;
            }

            FrameworkUnitAnchor anchor = customUnit.EnsureFrameworkUnitAnchor();
            int assignedUnitId = AllocateNextUnitId();
            anchor.UnitID = assignedUnitId;
            customUnit.UnitID = assignedUnitId;
            Units.Add(anchor);

            if (targetCell != null)
            {
                targetCell.IsTaken = customUnit.Obstructable;
                customUnit.Cell = targetCell;
                customUnit.transform.localPosition = targetCell.transform.localPosition;
            }

            if (ownerPlayer != null)
            {
                customUnit.PlayerNumber = ownerPlayer.PlayerNumber;
            }

            SyncRegistryAnchorFromCustomUnit(customUnit, anchor);
            customUnit.RegisterLegacyCellOccupancy();
            customUnit.transform.localRotation = Quaternion.Euler(0, 0, 0);
            customUnit.Initialize();

            LegacyGrid.SubscribeUnitInputHandlers(anchor);
            NotifyUnitAdded(unitTransform);
        }

        protected void RegisterSceneUnit(CustomUnit unit, Cell targetCell = null, Player ownerPlayer = null)
        {
            if (unit == null)
            {
                return;
            }

            if (IsUnitRegistered(unit))
            {
                return;
            }

            RegisterSceneUnitTransform(unit.transform, targetCell, ownerPlayer);
        }

        protected bool IsUnitRegistered(CustomUnit unit)
        {
            return unit != null && IsUnitRegistered(unit.LegacyUnit);
        }

        protected void UnregisterSceneUnit(CustomUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            UnregisterSceneUnit(unit.LegacyUnit);
        }

        protected bool IsUnitRegistered(Unit unit)
        {
            return unit != null && Units != null && Units.Contains(unit);
        }

        protected void UnregisterSceneUnit(Unit unit)
        {
            if (unit == null || Units == null || !Units.Remove(unit))
            {
                return;
            }

            LegacyGrid.UnsubscribeUnitInputHandlers(unit);
        }
    }
}
