using System;
using System.Collections.Generic;
using System.Linq;

namespace Windy.Srpg.Game.Grid
{
    internal static class BlackFogLayerCalculator
    {
        public static IReadOnlyDictionary<int, int> BuildDepthByLayer(
            IEnumerable<int> layerCoordinates,
            int coveredLayerCount,
            bool reverseDirection)
        {
            if (layerCoordinates == null || coveredLayerCount <= 0)
            {
                return new Dictionary<int, int>();
            }

            IEnumerable<int> orderedLayers = layerCoordinates
                .Distinct()
                .OrderBy(value => value);
            if (reverseDirection)
            {
                orderedLayers = orderedLayers.Reverse();
            }

            List<int> coveredLayers = orderedLayers
                .Take(Math.Max(0, coveredLayerCount))
                .ToList();
            return coveredLayers
                .Select((coordinate, index) => new { coordinate, depth = coveredLayers.Count - index - 1 })
                .ToDictionary(entry => entry.coordinate, entry => entry.depth);
        }
    }
}
