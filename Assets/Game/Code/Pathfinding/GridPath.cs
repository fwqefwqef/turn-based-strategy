using System.Collections.Generic;

namespace Windy.Srpg.Runtime.Pathfinding
{
    public sealed class GridPath<TNode>
    {
        public GridPath(IReadOnlyList<TNode> nodes, float totalCost)
        {
            Nodes = nodes;
            TotalCost = totalCost;
        }

        public IReadOnlyList<TNode> Nodes { get; }
        public float TotalCost { get; }
    }
}

