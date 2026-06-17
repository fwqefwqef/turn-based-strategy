using System.Collections.Generic;
using UnityEngine;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Pathfinding;

namespace Windy.Srpg.Game.Pathfinding.Algorithms
{
    /// <summary>
    /// Adapter over the public runtime pathfinder for board-cell movement graphs.
    /// </summary>
    public sealed class DijkstraPathfinding
    {
        private static readonly IPathfinder RuntimePathfinder = new DijkstraPathfinder();

        public Dictionary<BoardCell, IList<BoardCell>> FindAllPaths(
            Dictionary<BoardCell, Dictionary<BoardCell, float>> edges,
            BoardCell originNode)
        {
            Dictionary<BoardCell, IList<BoardCell>> result = new Dictionary<BoardCell, IList<BoardCell>>();
            if (edges == null || originNode == null)
            {
                return result;
            }

            Dictionary<BoardCell, IList<BoardCell>> runtimePaths = RuntimePathfinder.FindAllPaths(edges, originNode);
            foreach (KeyValuePair<BoardCell, IList<BoardCell>> entry in runtimePaths)
            {
                result[entry.Key] = ConvertToLegacyPath(entry.Value, originNode);
            }

            return result;
        }

        public IList<T> FindPath<T>(Dictionary<T, Dictionary<T, float>> edges, T originNode, T destinationNode)
            where T : BoardCell
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

