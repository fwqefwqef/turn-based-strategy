using TbsFramework.Units;
using TbsFramework.Cells;

namespace Windy.Srpg.Game.Units
{
    public partial class CustomUnit
    {
        private FrameworkUnitAnchor cachedFrameworkAnchor;

        internal FrameworkUnitAnchor EnsureFrameworkUnitAnchor()
        {
            if (cachedFrameworkAnchor == null)
            {
                cachedFrameworkAnchor = GetComponent<FrameworkUnitAnchor>();
            }

            if (cachedFrameworkAnchor == null)
            {
                cachedFrameworkAnchor = gameObject.AddComponent<FrameworkUnitAnchor>();
            }

            return cachedFrameworkAnchor;
        }

        public Unit LegacyUnit => EnsureFrameworkUnitAnchor();

        internal void RegisterLegacyCellOccupancy(Cell targetCell = null)
        {
            Cell resolvedCell = targetCell ?? this.Cell;
            Unit token = LegacyUnit;
            if (resolvedCell != null && token != null && !resolvedCell.CurrentUnits.Contains(token))
            {
                resolvedCell.CurrentUnits.Add(token);
            }
        }

        internal void UnregisterLegacyCellOccupancy(Cell targetCell = null)
        {
            Cell resolvedCell = targetCell ?? this.Cell;
            Unit token = LegacyUnit;
            resolvedCell?.CurrentUnits.Remove(token);
        }

        private void Awake()
        {
            EnsureFrameworkUnitAnchor();
        }
    }
}
