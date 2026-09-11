using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Chapters;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Grid
{
    /// <summary>
    /// Owns chapter-driven Black Fog expansion. Covered cells are published through
    /// the shared terrain-effect system; damage remains in the shared Pain phase.
    /// </summary>
    internal sealed class BlackFogSystem
    {
        internal const string BuffId = "black_fog";
        private const int PlayerSideId = 0;

        private readonly CellGrid grid;
        private readonly Dictionary<Cell, int> depthByCell = new Dictionary<Cell, int>();
        private ChapterData chapterData;
        private int coveredLayerCount;
        private int lastExpandedRound;

        public BlackFogSystem(CellGrid grid)
        {
            this.grid = grid;
        }

        public void InitializeForBattle(ChapterData data)
        {
            ClearBattleState();
            chapterData = data;
            coveredLayerCount = 0;
            lastExpandedRound = 0;
        }

        public void PrepareBattleStart(int round)
        {
            if (chapterData == null)
            {
                return;
            }

            ExpandIfNeeded(round);
            RefreshFogDebuffs();
        }

        public void BeforeIncomingTurnDotPhase(int playerId)
        {
            if (playerId == PlayerSideId)
            {
                RefreshFogDebuffs();
            }
        }

        public void AfterIncomingTurnDotPhase(int playerId, int round)
        {
            if (playerId != PlayerSideId || chapterData == null)
            {
                return;
            }

            ExpandIfNeeded(round);
            RefreshFogDebuffs();
        }

        public bool TryGetDepth(Unit unit, out int depth)
        {
            depth = 0;
            if (unit == null || unit.PlayerNumber != PlayerSideId || unit.Cell == null)
            {
                return false;
            }

            return grid.TryGetTerrainEffectIntensity(unit, BuffId, out depth);
        }

        public void RefreshFogDebuffs()
        {
            if (grid == null || !grid.IsBattleStarted)
            {
                return;
            }

            grid.RefreshTerrainEffectsForOccupancyChange();
        }

        public void ClearBattleState()
        {
            foreach (Cell cell in depthByCell.Keys.ToList())
            {
                grid.RemoveTerrainEffectWithoutRefresh(cell, BuffId);
            }

            depthByCell.Clear();
            coveredLayerCount = 0;
            lastExpandedRound = 0;

            if (grid == null)
            {
                return;
            }

            grid.RefreshTerrainEffectsForOccupancyChange();
        }

        private bool ExpandIfNeeded(int round)
        {
            if (chapterData == null
                || round < chapterData.BlackFogTurn
                || lastExpandedRound >= round)
            {
                return false;
            }

            coveredLayerCount += chapterData.BlackFogExpansionDistance;
            lastExpandedRound = round;
            RebuildCoverage();
            BattleLog.Log(
                "BlackFog",
                $"Black Fog expanded {chapterData.BlackFogExpansionDistance} tile(s) from {chapterData.BlackFogDirection} on turn {round}.");
            return true;
        }

        private void RebuildCoverage()
        {
            List<Cell> cells = grid.GetAllCells()
                .Where(cell => cell != null)
                .Distinct()
                .ToList();

            foreach (Cell cell in depthByCell.Keys.Where(cell => cell != null))
            {
                grid.RemoveTerrainEffectWithoutRefresh(cell, BuffId);
            }

            depthByCell.Clear();
            if (cells.Count == 0 || coveredLayerCount <= 0)
            {
                return;
            }

            bool usesHorizontalAxis = chapterData.BlackFogDirection == BlackFogDirection.Left
                || chapterData.BlackFogDirection == BlackFogDirection.Right;
            bool reversesAxis = chapterData.BlackFogDirection == BlackFogDirection.Right
                || chapterData.BlackFogDirection == BlackFogDirection.Up;

            IReadOnlyDictionary<int, int> depthByLayer = BlackFogLayerCalculator.BuildDepthByLayer(
                cells.Select(cell => usesHorizontalAxis ? cell.Coordinates.x : cell.Coordinates.y),
                coveredLayerCount,
                reversesAxis);

            foreach (Cell cell in cells)
            {
                int coordinate = usesHorizontalAxis ? cell.Coordinates.x : cell.Coordinates.y;
                if (depthByLayer.TryGetValue(coordinate, out int depth))
                {
                    depthByCell[cell] = depth;
                    grid.SetTerrainEffectWithoutRefresh(cell, BuffId, durationRounds: 0, intensity: depth);
                }
            }

            grid.RefreshTerrainEffectsForOccupancyChange();
        }
    }
}
