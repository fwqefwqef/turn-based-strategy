using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Skills;
using Windy.Srpg.Game.Units;

namespace Windy.Srpg.Game.Grid
{
    /// <summary>
    /// Computes persistent enemy threat overlays from the same scene-side movement and skill rules
    /// the battle uses at runtime. This is intentionally scene-owned so the input layer can ask a
    /// simple question: "which cells can this enemy threaten right now?"
    /// </summary>
    internal static class EnemyRangeOverlayUtility
    {
        public static Dictionary<Unit, HashSet<Cell>> GetThreatenedCellsByUnit(IEnumerable<Unit> units, CellGrid grid)
        {
            Dictionary<Unit, HashSet<Cell>> results = new Dictionary<Unit, HashSet<Cell>>();
            List<Unit> resolvedUnits = units?
                .Where(unit => unit != null && unit.HitPoints > 0 && !unit.ExcludedFromBattle)
                .Distinct()
                .ToList()
                ?? new List<Unit>();

            if (grid == null || resolvedUnits.Count == 0)
            {
                return results;
            }

            using PreviewOccupancyScope scope = PreviewOccupancyScope.Create(grid);
            foreach (Unit unit in resolvedUnits)
            {
                results[unit] = GetThreatenedCells(unit, grid);
            }

            return results;
        }

        public static HashSet<Cell> GetThreatenedCells(Unit unit, CellGrid grid)
        {
            HashSet<Cell> threatenedCells = new HashSet<Cell>();
            if (unit == null || grid == null || unit.HitPoints <= 0)
            {
                return threatenedCells;
            }

            List<Cell> allCells = grid.GetAllCells();
            if (allCells.Count == 0)
            {
                return threatenedCells;
            }

            HashSet<Cell> originCells = unit.GetAvailableDestinations(allCells) ?? new HashSet<Cell>();
            if (unit.Cell != null)
            {
                originCells.Add(unit.Cell);
            }

            foreach (Cell originCell in originCells.Where(cell => cell != null))
            {
                AddWeaponThreatCells(unit, originCell, allCells, threatenedCells);
                AddSkillThreatCells(unit, originCell, grid, allCells, threatenedCells);
            }

            return threatenedCells;
        }

        private readonly struct PreviewOccupancyScope : IDisposable
        {
            private readonly Dictionary<Cell, List<Unit>> originalUnitsByCell;
            private readonly Dictionary<Cell, bool> originalTakenByCell;
            private readonly CellGrid grid;

            private PreviewOccupancyScope(CellGrid grid, Dictionary<Cell, List<Unit>> originalUnitsByCell, Dictionary<Cell, bool> originalTakenByCell)
            {
                this.grid = grid;
                this.originalUnitsByCell = originalUnitsByCell;
                this.originalTakenByCell = originalTakenByCell;
            }

            public static PreviewOccupancyScope Create(CellGrid grid)
            {
                if (grid == null)
                {
                    return default;
                }

                List<Cell> allCells = grid.GetAllCells();
                Dictionary<Cell, List<Unit>> originalUnitsByCell = new Dictionary<Cell, List<Unit>>();
                Dictionary<Cell, bool> originalTakenByCell = new Dictionary<Cell, bool>();

                foreach (Cell cell in allCells)
                {
                    if (cell == null)
                    {
                        continue;
                    }

                    originalUnitsByCell[cell] = cell.CurrentUnits != null ? new List<Unit>(cell.CurrentUnits) : new List<Unit>();
                    originalTakenByCell[cell] = cell.IsTaken;
                    cell.ClearCurrentUnits();
                }

                foreach (Unit unit in grid.GetAllUnits())
                {
                    if (unit == null || unit.ExcludedFromBattle || unit.HitPoints <= 0)
                    {
                        continue;
                    }

                    Cell effectiveCell = unit.HasPendingMove ? unit.PreviewCell : unit.Cell;
                    Cell canonicalCell = grid.ResolveCanonicalCell(effectiveCell);
                    if (canonicalCell == null)
                    {
                        continue;
                    }

                    canonicalCell.CurrentUnits.Add(unit);
                }

                foreach (Cell cell in allCells)
                {
                    if (cell != null)
                    {
                        Unit.RefreshCellOccupancy(cell);
                    }
                }

                return new PreviewOccupancyScope(grid, originalUnitsByCell, originalTakenByCell);
            }

            public void Dispose()
            {
                if (grid == null || originalUnitsByCell == null || originalTakenByCell == null)
                {
                    return;
                }

                foreach ((Cell cell, List<Unit> units) in originalUnitsByCell)
                {
                    if (cell == null)
                    {
                        continue;
                    }

                    cell.ClearCurrentUnits();
                    if (units != null)
                    {
                        cell.CurrentUnits.AddRange(units.Where(unit => unit != null));
                    }

                    if (originalTakenByCell.TryGetValue(cell, out bool wasTaken))
                    {
                        cell.IsTaken = wasTaken;
                    }
                }
            }
        }

        private static void AddWeaponThreatCells(Unit unit, Cell originCell, IReadOnlyList<Cell> allCells, ISet<Cell> results)
        {
            foreach (Item weaponEntry in unit.GetWeaponInventoryEntries())
            {
                WeaponData weapon = weaponEntry?.Weapon;
                if (weapon == null)
                {
                    continue;
                }

                AddRangeCells(originCell, unit.GetMinAttackRangeForWeapon(weapon), unit.GetMaxAttackRangeForWeapon(weapon), allCells, results);
            }
        }

        private static void AddSkillThreatCells(Unit unit, Cell originCell, CellGrid grid, IReadOnlyList<Cell> allCells, ISet<Cell> results)
        {
            IReadOnlyList<Skill> skills = unit.SkillList?.Entries ?? Array.Empty<Skill>();
            foreach (Skill skill in skills)
            {
                if (!CanContributeThreat(unit, skill))
                {
                    continue;
                }

                SkillData data = skill.Data;
                if (IsAreaOffensiveSkill(data))
                {
                    foreach (Cell centerCell in GetAreaSkillCandidateCenters(unit, skill, originCell, grid, allCells))
                    {
                        foreach (Cell affectedCell in GetAreaSkillAffectedCells(unit, skill, originCell, centerCell, grid, allCells))
                        {
                            if (affectedCell != null)
                            {
                                results.Add(affectedCell);
                            }
                        }
                    }

                    continue;
                }

                if (data.Category == SkillCategory.CombatArt)
                {
                    foreach (Item weaponEntry in unit.GetWeaponInventoryEntries())
                    {
                        WeaponData weapon = weaponEntry?.Weapon;
                        if (weapon == null || !SkillMatchesWeapon(data, weapon))
                        {
                            continue;
                        }

                        SkillRangeUtility.ApplyCombatArtRangeModifiers(
                            unit.GetMinAttackRangeForWeapon(weapon),
                            unit.GetMaxAttackRangeForWeapon(weapon),
                            data.AttackProfile.MinRange,
                            data.AttackProfile.MaxRange,
                            out int combatArtMinRange,
                            out int combatArtMaxRange);
                        AddRangeCells(originCell, combatArtMinRange, combatArtMaxRange, allCells, results);
                    }

                    continue;
                }

                int minRange = Mathf.Max(0, data.AttackProfile.MinRange);
                int maxRange = Mathf.Max(minRange, data.AttackProfile.MaxRange);
                if (SkillRangeUtility.IsInfiniteRange(maxRange))
                {
                    maxRange = ResolveMaxDistance(originCell, allCells);
                }

                AddRangeCells(originCell, minRange, maxRange, allCells, results);
            }
        }

        private static bool CanContributeThreat(Unit unit, Skill skill)
        {
            if (unit == null || skill?.Data == null || !unit.CanUseSkill(skill))
            {
                return false;
            }

            SkillData data = skill.Data;
            return IsSingleTargetOffensiveSkill(data) || IsAreaOffensiveSkill(data);
        }

        private static bool IsSingleTargetOffensiveSkill(SkillData data)
        {
            return data != null
                && data.AttackProfile.Enabled
                && (data.TargetingType == SkillTargetingType.EnemyUnit || data.TargetingType == SkillTargetingType.AnyUnit);
        }

        private static bool IsAreaOffensiveSkill(SkillData data)
        {
            return data != null
                && data.AttackProfile.Enabled
                && data.AreaProfile.Enabled
                && data.AreaProfile.AffectsEnemies;
        }

        private static void AddRangeCells(Cell originCell, int minRange, int maxRange, IReadOnlyList<Cell> allCells, ISet<Cell> results)
        {
            if (originCell == null || allCells == null || results == null)
            {
                return;
            }

            int resolvedMin = Mathf.Max(0, minRange);
            int resolvedMax = Mathf.Max(resolvedMin, maxRange);
            foreach (Cell candidate in allCells)
            {
                if (candidate == null)
                {
                    continue;
                }

                int distance = originCell.GetDistance(candidate);
                if (distance < resolvedMin || distance > resolvedMax)
                {
                    continue;
                }

                results.Add(candidate);
            }
        }

        private static List<Cell> GetAreaSkillCandidateCenters(Unit unit, Skill skill, Cell originCell, CellGrid grid, IReadOnlyList<Cell> allCells)
        {
            List<Cell> results = new List<Cell>();
            if (unit == null || skill?.Data == null || originCell == null || grid == null || allCells == null)
            {
                return results;
            }

            SkillData data = skill.Data;
            int minRange = Mathf.Max(0, data.AreaProfile.MinRange);
            int maxRange = ResolveAreaSkillMaxRange(data, originCell, allCells);

            if (data.AreaProfile.Shape == SkillAreaShape.Line)
            {
                Vector2Int source = originCell.Coordinates;
                Vector2Int[] directions =
                {
                    Vector2Int.up,
                    Vector2Int.right,
                    Vector2Int.down,
                    Vector2Int.left
                };

                foreach (Vector2Int direction in directions)
                {
                    for (int distance = Mathf.Max(1, minRange); distance <= Mathf.Max(minRange, maxRange); distance++)
                    {
                        Vector2Int targetCoordinates = source + direction * distance;
                        Cell cell = allCells.FirstOrDefault(candidate => candidate != null && candidate.Coordinates == targetCoordinates);
                        if (cell == null)
                        {
                            break;
                        }

                        results.Add(cell);
                    }
                }

                return results;
            }

            foreach (Cell cell in allCells)
            {
                if (cell == null)
                {
                    continue;
                }

                int distance = originCell.GetDistance(cell);
                if (distance >= minRange && distance <= Mathf.Max(minRange, maxRange))
                {
                    results.Add(cell);
                }
            }

            return results;
        }

        private static HashSet<Cell> GetAreaSkillAffectedCells(Unit unit, Skill skill, Cell originCell, Cell centerCell, CellGrid grid, IReadOnlyList<Cell> allCells)
        {
            HashSet<Cell> results = new HashSet<Cell>();
            if (unit == null || skill?.Data == null || originCell == null || centerCell == null || grid == null || allCells == null)
            {
                return results;
            }

            if (skill.Data.AreaProfile.Shape == SkillAreaShape.Line)
            {
                Vector2Int direction = centerCell.Coordinates - originCell.Coordinates;
                direction = new Vector2Int(Math.Sign(direction.x), Math.Sign(direction.y));
                if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1)
                {
                    return results;
                }

                int minRange = Mathf.Max(1, skill.Data.AreaProfile.MinRange);
                int maxRange = ResolveAreaSkillMaxRange(skill.Data, originCell, allCells);
                int halfWidth = Mathf.Max(0, skill.Data.AreaProfile.Radius);
                Vector2Int perpendicular = new Vector2Int(-direction.y, direction.x);
                Dictionary<Vector2Int, Cell> lookup = allCells
                    .Where(cell => cell != null)
                    .ToDictionary(cell => cell.Coordinates, cell => cell);

                for (int distance = minRange; distance <= Mathf.Max(minRange, maxRange); distance++)
                {
                    Vector2Int centerCoordinates = originCell.Coordinates + direction * distance;
                    for (int offset = -halfWidth; offset <= halfWidth; offset++)
                    {
                        Vector2Int targetCoordinates = centerCoordinates + perpendicular * offset;
                        if (!lookup.TryGetValue(targetCoordinates, out Cell cell))
                        {
                            continue;
                        }

                        if (skill.Data.SelfImmune && cell == originCell)
                        {
                            continue;
                        }

                        results.Add(cell);
                    }
                }

                return results;
            }

            int radius = Mathf.Max(0, skill.Data.AreaProfile.Radius);
            foreach (Cell cell in allCells)
            {
                if (cell == null || centerCell.GetDistance(cell) > radius)
                {
                    continue;
                }

                if (skill.Data.SelfImmune && cell == originCell)
                {
                    continue;
                }

                results.Add(cell);
            }

            return results;
        }

        private static int ResolveAreaSkillMaxRange(SkillData data, Cell originCell, IReadOnlyList<Cell> allCells)
        {
            if (data == null || originCell == null || allCells == null)
            {
                return 0;
            }

            int minRange = Mathf.Max(0, data.AreaProfile.MinRange);
            int maxRange = Mathf.Max(minRange, data.AreaProfile.MaxRange);
            if (!SkillRangeUtility.IsInfiniteRange(maxRange))
            {
                return maxRange;
            }

            return ResolveMaxDistance(originCell, allCells);
        }

        private static int ResolveMaxDistance(Cell originCell, IReadOnlyList<Cell> allCells)
        {
            if (originCell == null || allCells == null)
            {
                return 0;
            }

            return allCells
                .Where(cell => cell != null)
                .Select(originCell.GetDistance)
                .DefaultIfEmpty(0)
                .Max();
        }

        private static bool SkillMatchesWeapon(SkillData data, WeaponData weapon)
        {
            if (data == null || weapon == null)
            {
                return false;
            }

            return data.RequiredWeaponType switch
            {
                CombatArtWeaponType.Any => true,
                CombatArtWeaponType.Sword => (weapon.WeaponType & WeaponType.Sword) != 0,
                CombatArtWeaponType.Lance => (weapon.WeaponType & WeaponType.Lance) != 0,
                CombatArtWeaponType.Blunt => (weapon.WeaponType & WeaponType.Blunt) != 0,
                CombatArtWeaponType.Ranged => (weapon.WeaponType & WeaponType.Ranged) != 0,
                CombatArtWeaponType.Magic => (weapon.WeaponType & WeaponType.Magic) != 0 || weapon.DamageType == DamageType.Magic,
                _ => false
            };
        }
    }
}
