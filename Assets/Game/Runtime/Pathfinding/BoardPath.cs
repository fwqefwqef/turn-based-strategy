using System.Collections.Generic;

namespace Windy.Srpg.Runtime.Pathfinding
{
    public sealed class BoardPath<TNode>
    {
        public BoardPath(IReadOnlyList<TNode> nodes, float totalCost)
        {
            Nodes = nodes;
            TotalCost = totalCost;
        }

        public IReadOnlyList<TNode> Nodes { get; }
        public float TotalCost { get; }
    }
}
