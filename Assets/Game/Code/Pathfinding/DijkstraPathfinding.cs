using System.Collections.Generic;
using UnityEngine;
using Windy.Srpg.Runtime.Grid;
using Windy.Srpg.Runtime.Pathfinding;

namespace Windy.Srpg.Game.Pathfinding.Algorithms
{
    /// <summary>
    /// Adapter over the public runtime pathfinder for grid-cell movement graphs.
    /// </summary>
    public sealed class DijkstraPathfinding
    {
        private static readonly IPathfinder RuntimePathfinder = new DijkstraPathfinder();

        public Dictionary<Cell, IList<Cell>> FindAllPaths(
            Dictionary<Cell, Dictionary<Cell, float>> edges,
            Cell originNode)
        {
            Dictionary<Cell, IList<Cell>> result = new Dictionary<Cell, IList<Cell>>();
            if (edges == null || originNode == null)
            {
                return result;
            }

            Dictionary<Cell, IList<Cell>> runtimePaths = RuntimePathfinder.FindAllPaths(edges, originNode);
            foreach (KeyValuePair<Cell, IList<Cell>> entry in runtimePaths)
            {
                result[entry.Key] = ConvertToLegacyPath(entry.Value, originNode);
            }

            return result;
        }

        public IList<T> FindPath<T>(Dictionary<T, Dictionary<T, float>> edges, T originNode, T destinationNode)
            where T : Cell
        {
            IList<T> runtimePath = RuntimePathfinder.FindPath(edges, originNode, destinationNode);
            return ConvertToLegacyPath(runtimePath, originNode);
        }

        private static IList<TNode> ConvertToLegacyPath<TNode>(IList<TNode> runtimePath, TNode originNode)
        {
            List<TNode> legacyPath = new List<TNode>();
            if (runtimePath == null || runtimePath.Count == 0)
            {
                return legacyPath;
            }

            EqualityComparer<TNode> comparer = EqualityComparer<TNode>.Default;
            for (int i = runtimePath.Count - 1; i >= 0; i--)
            {
                TNode node = runtimePath[i];
                if (node != null && !comparer.Equals(node, originNode))
                {
                    legacyPath.Add(node);
                }
            }

            return legacyPath;
        }
    }
}

