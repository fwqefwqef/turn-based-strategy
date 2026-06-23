using System.Collections.Generic;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Pathfinding;

namespace Windy.Srpg.Game.Pathfinding.Algorithms
{
    /// <summary>
    /// Adapter over <see cref="DijkstraPathfinder"/> for grid-cell movement graphs.
    /// </summary>
    public sealed class DijkstraPathfinding
    {
        private static readonly IPathfinder SharedPathfinder = new DijkstraPathfinder();

        public Dictionary<Cell, IList<Cell>> FindAllPaths(
            Dictionary<Cell, Dictionary<Cell, float>> edges,
            Cell originNode)
        {
            Dictionary<Cell, IList<Cell>> result = new Dictionary<Cell, IList<Cell>>();
            if (edges == null || originNode == null)
            {
                return result;
            }

            Dictionary<Cell, IList<Cell>> paths = SharedPathfinder.FindAllPaths(edges, originNode);
            foreach (KeyValuePair<Cell, IList<Cell>> entry in paths)
            {
                result[entry.Key] = ConvertToLegacyPath(entry.Value, originNode);
            }

            return result;
        }

        public IList<T> FindPath<T>(Dictionary<T, Dictionary<T, float>> edges, T originNode, T destinationNode)
            where T : Cell
        {
            IList<T> path = SharedPathfinder.FindPath(edges, originNode, destinationNode);
            return ConvertToLegacyPath(path, originNode);
        }

        private static IList<TNode> ConvertToLegacyPath<TNode>(IList<TNode> path, TNode originNode)
        {
            List<TNode> legacyPath = new List<TNode>();
            if (path == null || path.Count == 0)
            {
                return legacyPath;
            }

            EqualityComparer<TNode> comparer = EqualityComparer<TNode>.Default;
            for (int i = path.Count - 1; i >= 0; i--)
            {
                TNode node = path[i];
                if (node != null && !comparer.Equals(node, originNode))
                {
                    legacyPath.Add(node);
                }
            }

            return legacyPath;
        }
    }
}

