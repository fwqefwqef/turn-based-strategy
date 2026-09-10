using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Chapters;
using Windy.Srpg.Game.Units;
using RuntimeBuff = Windy.Srpg.Game.Buffs.Buff;

namespace Windy.Srpg.Game.Grid
{
    /// <summary>
    /// Owns battle-local Black Fog coverage, depth, visuals, and positional debuffs.
    /// Damage itself is resolved by the black_fog buff during the shared Pain phase.
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

            Cell cell = grid.ResolveCanonicalCell(unit.Cell);
            return cell != null && depthByCell.TryGetValue(cell, out depth);
        }

        public void RefreshFogDebuffs()
        {
            if (grid == null || !grid.IsBattleStarted)
            {
                return;
            }

            foreach (Unit unit in grid.GetAllUnits().Where(unit => unit != null).ToList())
            {
                bool shouldHaveDebuff = unit.PlayerNumber == PlayerSideId
                    && !unit.ExcludedFromBattle
                    && unit.IsAliveForBattle
                    && TryGetDepth(unit, out _);
                RuntimeBuff currentEntry = unit.BuffList?.GetBuff(BuffId);

                if (shouldHaveDebuff && currentEntry == null)
                {
                    unit.AddBuffById(BuffId);
                }
                else if (!shouldHaveDebuff && currentEntry != null)
                {
                    unit.RemoveBuff(currentEntry);
                }
            }
        }

        public void ClearBattleState()
        {
            foreach (Cell cell in depthByCell.Keys.ToList())
            {
                cell?.ClearBlackFogOverlay();
            }

            depthByCell.Clear();
            coveredLayerCount = 0;
            lastExpandedRound = 0;

            if (grid == null)
            {
                return;
            }

            foreach (Unit unit in grid.GetAllUnits().Where(unit => unit != null).ToList())
            {
                RuntimeBuff entry = unit.BuffList?.GetBuff(BuffId);
                if (entry != null)
                {
                    unit.RemoveBuff(entry);
                }
            }
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
                cell.ClearBlackFogOverlay();
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
                    cell.ApplyBlackFogOverlay();
                }
                else
                {
                    cell.ClearBlackFogOverlay();
                }
            }
        }
    }
}
