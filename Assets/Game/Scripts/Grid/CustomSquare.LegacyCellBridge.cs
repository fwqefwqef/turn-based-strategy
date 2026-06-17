using TbsFramework.Cells;
using UnityEngine;

namespace Windy.Srpg.Game.Grid
{
    public abstract partial class CustomSquare
    {
        private FrameworkSquareAnchor cachedFrameworkSquareAnchor;

        public Cell LegacyCell => EnsureFrameworkSquareAnchor();

        public FrameworkSquareAnchor EnsureFrameworkSquareAnchor()
        {
            if (cachedFrameworkSquareAnchor == null)
            {
                cachedFrameworkSquareAnchor = GetComponent<FrameworkSquareAnchor>();
            }

            if (cachedFrameworkSquareAnchor == null)
            {
                cachedFrameworkSquareAnchor = gameObject.AddComponent<FrameworkSquareAnchor>();
            }

            SyncRegistryAnchorFromCustomSquare(cachedFrameworkSquareAnchor);
            return cachedFrameworkSquareAnchor;
        }

        internal void SyncRegistryAnchorFromCustomSquare(FrameworkSquareAnchor anchor = null)
        {
            anchor ??= cachedFrameworkSquareAnchor;
            if (anchor == null)
            {
                return;
            }

            anchor.OffsetCoord = OffsetCoord;
            anchor.IsTaken = IsTaken;
            anchor.MovementCost = MovementCost;
        }
    }
}
