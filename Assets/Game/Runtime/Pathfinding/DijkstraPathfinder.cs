using System;
using System.Collections.Generic;

namespace Windy.Srpg.Runtime.Pathfinding
{
    public sealed class DijkstraPathfinder : IPathfinder
    {
        public Dictionary<TNode, IList<TNode>> FindAllPaths<TNode>(Dictionary<TNode, Dictionary<TNode, float>> edges, TNode originNode)
        {
            var result = new Dictionary<TNode, IList<TNode>>();
            if (edges == null || !edges.ContainsKey(originNode))
            {
                return result;
            }

            ComputeShortestPaths(edges, originNode, out var cameFrom, out _);

            foreach (var destination in cameFrom.Keys)
            {
                result[destination] = BuildPath(originNode, destination, cameFrom);
            }

            return result;
        }

        public IList<TNode> FindPath<TNode>(Dictionary<TNode, Dictionary<TNode, float>> edges, TNode originNode, TNode destinationNode)
        {
            if (edges == null)
            {
                return Array.Empty<TNode>();
            }

            if (!edges.ContainsKey(originNode) || !edges.ContainsKey(destinationNode))
            {
                return Array.Empty<TNode>();
            }

            ComputeShortestPaths(edges, originNode, out var cameFrom, out _);
            if (!cameFrom.ContainsKey(destinationNode))
            {
                return Array.Empty<TNode>();
            }

            return BuildPath(originNode, destinationNode, cameFrom);
        }

        private static void ComputeShortestPaths<TNode>(
            Dictionary<TNode, Dictionary<TNode, float>> edges,
            TNode originNode,
            out Dictionary<TNode, TNode> cameFrom,
            out Dictionary<TNode, float> costSoFar)
        {
            cameFrom = new Dictionary<TNode, TNode>();
            costSoFar = new Dictionary<TNode, float>();
            var frontier = new List<TNode>();
            var frontierCosts = new Dictionary<TNode, float>();

            cameFrom[originNode] = default;
            costSoFar[originNode] = 0f;
            frontier.Add(originNode);
            frontierCosts[originNode] = 0f;

            while (frontier.Count > 0)
            {
                var current = DequeueLowestCost(frontier, frontierCosts);
                var currentCost = costSoFar[current];
                var currentEdges = edges[current];

                foreach (var edge in currentEdges)
                {
                    var neighbour = edge.Key;
                    var newCost = currentCost + edge.Value;
                    if (!costSoFar.TryGetValue(neighbour, out var knownCost) || newCost < knownCost)
                    {
                        costSoFar[neighbour] = newCost;
                        cameFrom[neighbour] = current;
                        if (!frontier.Contains(neighbour))
                        {
                            frontier.Add(neighbour);
                        }

                        frontierCosts[neighbour] = newCost;
                    }
                }
            }
        }

        private static TNode DequeueLowestCost<TNode>(List<TNode> frontier, Dictionary<TNode, float> frontierCosts)
        {
            var bestIndex = 0;
            var bestCost = frontierCosts[frontier[0]];

            for (var i = 1; i < frontier.Count; i++)
            {
                var node = frontier[i];
                var cost = frontierCosts[node];
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestIndex = i;
                }
            }

            var bestNode = frontier[bestIndex];
            frontier.RemoveAt(bestIndex);
            frontierCosts.Remove(bestNode);
            return bestNode;
        }

        private static IList<TNode> BuildPath<TNode>(TNode originNode, TNode destinationNode, Dictionary<TNode, TNode> cameFrom)
        {
            var path = new List<TNode>();
            if (!cameFrom.ContainsKey(destinationNode))
            {
                return path;
            }

            var current = destinationNode;
            path.Add(current);

            while (!EqualityComparer<TNode>.Default.Equals(current, originNode))
            {
                current = cameFrom[current];
                if (EqualityComparer<TNode>.Default.Equals(current, default) &&
                    !EqualityComparer<TNode>.Default.Equals(originNode, default))
                {
                    break;
                }

                path.Add(current);
            }

            path.Reverse();
            return path;
        }
    }
}
