using TbsFramework.Cells;
using UnityEngine;

namespace Windy.Srpg.Game.Grid
{
    public sealed partial class BattleSquareCell
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

            SyncRegistryAnchorFromBoardCell(cachedFrameworkSquareAnchor);
            return cachedFrameworkSquareAnchor;
        }

        internal void SyncRegistryAnchorFromBoardCell(FrameworkSquareAnchor anchor = null)
        {
            anchor ??= cachedFrameworkSquareAnchor;
            if (anchor == null)
            {
                return;
            }

            Vector2Int coordinates = Coordinates;
            anchor.OffsetCoord = new Vector2(coordinates.x, coordinates.y);
            anchor.MovementCost = TraversalCost;
            anchor.IsTaken = !IsTraversable || IsOccupied;
        }
    }
}
