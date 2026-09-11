using System;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.Buffs;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Grid
{
    /// <summary>
    /// Owns battle-local terrain instances, duration aging, visuals, and occupant effects.
    /// Every occupant effect is represented by a BuffList entry; terrain definitions only
    /// describe placement, visuals, targeting, and the buff they maintain.
    /// </summary>
    internal sealed class TerrainEffectSystem
    {
        private readonly CellGrid grid;

        public TerrainEffectSystem(CellGrid grid)
        {
            this.grid = grid;
        }

        public void InitializeForBattle()
        {
            TerrainEffectRegistry.EnsureRegistered();
            ClearBattleState();

            foreach (Cell cell in grid.GetAllCells().Where(cell => cell != null))
            {
                IEnumerable<string> startingIds = cell.TilePreset?.StartingTerrainEffectIds;
                startingIds ??= Array.Empty<string>();
                foreach (string effectId in startingIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    SetEffect(cell, effectId, remainingRounds: 0, intensity: 0, sourcePlayerNumber: -1, refreshUnits: false);
                }
            }

            RefreshAllUnitEffects();
        }

        public bool SetEffect(Cell cell, string effectId, int remainingRounds, int intensity, int sourcePlayerNumber, bool refreshUnits = true)
        {
            Cell canonicalCell = grid?.ResolveCanonicalCell(cell);
            if (canonicalCell == null || !TerrainEffectRegistry.TryGet(effectId, out TerrainEffectData data))
            {
                return false;
            }

            TerrainEffectInstance existing = canonicalCell.GetTerrainEffect(effectId);
            if (existing == null)
            {
                canonicalCell.AddTerrainEffect(new TerrainEffectInstance(data, remainingRounds, intensity, sourcePlayerNumber));
            }
            else
            {
                int incomingRounds = Math.Max(0, remainingRounds);
                existing.RemainingRounds = existing.IsPermanent || incomingRounds == 0
                    ? 0
                    : Math.Max(existing.RemainingRounds, incomingRounds);
                existing.Intensity = Math.Max(0, intensity);
                existing.SourcePlayerNumber = sourcePlayerNumber;
                canonicalCell.NotifyTerrainEffectChanged();
            }

            if (refreshUnits)
            {
                RefreshAllUnitEffects();
            }

            return true;
        }

        public bool RemoveEffect(Cell cell, string effectId, bool refreshUnits = true)
        {
            Cell canonicalCell = grid?.ResolveCanonicalCell(cell);
            bool removed = canonicalCell != null && canonicalCell.RemoveTerrainEffect(effectId);
            if (removed && refreshUnits)
            {
                RefreshAllUnitEffects();
            }

            return removed;
        }

        public void AdvanceRound()
        {
            bool changed = false;
            foreach (Cell cell in grid.GetAllCells().Where(cell => cell != null))
            {
                foreach (TerrainEffectInstance effect in cell.TerrainEffects.ToList())
                {
                    if (effect == null || effect.IsPermanent)
                    {
                        continue;
                    }

                    effect.RemainingRounds--;
                    if (effect.RemainingRounds <= 0)
                    {
                        cell.RemoveTerrainEffect(effect.Data?.Id);
                    }
                    else
                    {
                        cell.NotifyTerrainEffectChanged();
                    }

                    changed = true;
                }
            }

            if (changed)
            {
                RefreshAllUnitEffects();
            }
        }

        public void RefreshAllUnitEffects()
        {
            if (grid == null)
            {
                return;
            }

            foreach (Unit unit in grid.GetAllUnits().Where(unit => unit != null).ToList())
            {
                RefreshUnitEffects(unit);
            }
        }

        public bool TryGetIntensity(Unit unit, string effectId, out int intensity)
        {
            intensity = 0;
            if (unit == null || unit.Cell == null)
            {
                return false;
            }

            bool found = false;
            foreach (Cell cell in unit.GetFootprintCells(unit.Cell, grid))
            {
                TerrainEffectInstance effect = cell?.GetTerrainEffect(effectId);
                if (effect == null || !AffectsUnit(effect.Data, unit))
                {
                    continue;
                }

                intensity = Math.Max(intensity, effect.Intensity);
                found = true;
            }

            return found;
        }

        public void ClearBattleState()
        {
            if (grid == null)
            {
                return;
            }

            foreach (Cell cell in grid.GetAllCells().Where(cell => cell != null))
            {
                cell.ClearTerrainEffects();
            }

            foreach (Unit unit in grid.GetAllUnits().Where(unit => unit != null))
            {
                unit.SetTerrainStatModifiers(default, default);
                RemoveExitedTerrainStatuses(unit, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
        }

        private void RefreshUnitEffects(Unit unit)
        {
            HashSet<string> activeEffectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> desiredBuffIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<Cell> footprint = unit.GetFootprintCells(unit.Cell, grid);

            if (unit.ExcludedFromBattle || !unit.IsAliveForBattle || footprint.Count != unit.FootprintTileCount
                || !footprint.All(cell => cell?.CurrentUnits != null && cell.CurrentUnits.Contains(unit)))
            {
                unit.SetTerrainStatModifiers(default, default);
                RemoveExitedTerrainStatuses(unit, desiredBuffIds);
                return;
            }

            foreach (TerrainEffectInstance effect in footprint
                .Where(cell => cell != null)
                .SelectMany(cell => cell.TerrainEffects ?? Array.Empty<TerrainEffectInstance>()))
            {
                TerrainEffectData data = effect?.Data;
                if (data == null || !AffectsUnit(data, unit) || !activeEffectIds.Add(data.Id))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(data.OccupantBuffId))
                {
                    desiredBuffIds.Add(data.OccupantBuffId);
                    if (unit.BuffList?.GetBuff(data.OccupantBuffId) == null)
                    {
                        unit.AddBuffById(data.OccupantBuffId);
                    }
                }

                if (!string.IsNullOrWhiteSpace(data.AppliedBuffId))
                {
                    if (data.RemoveAppliedBuffOnExit)
                    {
                        desiredBuffIds.Add(data.AppliedBuffId);
                    }

                    if (unit.BuffList?.GetBuff(data.AppliedBuffId) == null)
                    {
                        unit.AddBuffById(data.AppliedBuffId);
                    }
                }
            }

            unit.SetTerrainStatModifiers(default, default);
            RemoveExitedTerrainStatuses(unit, desiredBuffIds);
        }

        private static void RemoveExitedTerrainStatuses(Unit unit, ISet<string> desiredBuffIds)
        {
            foreach (TerrainEffectData data in TerrainEffectRegistry.Entries)
            {
                if (data == null)
                {
                    continue;
                }

                RemoveExitedBuff(unit, data.OccupantBuffId, data.RemoveOccupantBuffOnExit, desiredBuffIds);
                RemoveExitedBuff(unit, data.AppliedBuffId, data.RemoveAppliedBuffOnExit, desiredBuffIds);
            }
        }

        private static void RemoveExitedBuff(Unit unit, string buffId, bool removeOnExit, ISet<string> desiredBuffIds)
        {
            if (!removeOnExit || string.IsNullOrWhiteSpace(buffId) || desiredBuffIds.Contains(buffId))
            {
                return;
            }

            Buff buff = unit.BuffList?.GetBuff(buffId);
            if (buff != null)
            {
                unit.RemoveBuff(buff);
            }
        }

        private static bool AffectsUnit(TerrainEffectData data, Unit unit)
        {
            return data != null && unit != null
                && (data.Targeting != TerrainEffectTargeting.PlayerSide || unit.PlayerNumber == data.TargetPlayerNumber);
        }
    }
}
