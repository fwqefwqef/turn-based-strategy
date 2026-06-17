using System.Collections.Generic;

namespace Windy.Srpg.Runtime.Pathfinding
{
    public interface IPathfinder
    {
        Dictionary<TNode, IList<TNode>> FindAllPaths<TNode>(Dictionary<TNode, Dictionary<TNode, float>> edges, TNode originNode);
        IList<TNode> FindPath<TNode>(Dictionary<TNode, Dictionary<TNode, float>> edges, TNode originNode, TNode destinationNode);
    }
}

