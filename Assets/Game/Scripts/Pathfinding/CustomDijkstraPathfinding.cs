using System.Collections.Generic;
using TbsFramework.Cells;
using Windy.Srpg.Runtime.Pathfinding;

namespace Windy.Srpg.Game.Pathfinding.Algorithms
{
    /// <summary>
    /// Legacy adapter over the public runtime pathfinder.
    /// It preserves the path shape CustomUnit already expects:
    /// destination-first, origin omitted.
    /// </summary>
    public sealed class CustomDijkstraPathfinding
    {
        private static readonly IPathfinder RuntimePathfinder = new DijkstraPathfinder();

        public Dictionary<Cell, IList<Cell>> FindAllPaths(Dictionary<Cell, Dictionary<Cell, float>> edges, Cell originNode)
        {
            var result = new Dictionary<Cell, IList<Cell>>();
            if (edges == null || originNode == null)
            {
                return result;
            }

            var runtimePaths = RuntimePathfinder.FindAllPaths(edges, originNode);
            foreach (var entry in runtimePaths)
            {
                result[entry.Key] = ConvertToLegacyPath(entry.Value, originNode);
            }

            return result;
        }

        public IList<T> FindPath<T>(Dictionary<T, Dictionary<T, float>> edges, T originNode, T destinationNode)
        {
            var runtimePath = RuntimePathfinder.FindPath(edges, originNode, destinationNode);
            return ConvertToLegacyPath(runtimePath, originNode);
        }

        private static IList<TNode> ConvertToLegacyPath<TNode>(IList<TNode> runtimePath, TNode originNode)
        {
            var legacyPath = new List<TNode>();
            if (runtimePath == null || runtimePath.Count == 0)
            {
                return legacyPath;
            }

            var comparer = EqualityComparer<TNode>.Default;
            for (int i = runtimePath.Count - 1; i >= 0; i--)
            {
                var node = runtimePath[i];
                if (comparer.Equals(node, originNode))
                {
                    continue;
                }

                legacyPath.Add(node);
            }

            return legacyPath;
        }
    }
}
