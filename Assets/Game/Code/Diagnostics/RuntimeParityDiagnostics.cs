using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Grid;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Diagnostics
{
    /// <summary>
    /// Read-only comparison of scene <see cref="Unit"/> pathfinding vs the mirrored <see cref="GridUnit"/>.
    /// Used during Phase 4 to prove parity before runtime path delegation is removed.
    /// </summary>
    public static class RuntimeParityDiagnostics
    {
        public static bool Enabled { get; set; } = true;

        private const int MaxSamplePaths = 4;

        public static void CompareAiTurnPrecalc(CellGrid cellGrid, IEnumerable<Unit> units)
        {
            if (!Enabled || !Application.isPlaying || cellGrid == null)
            {
                return;
            }

            List<Unit> unitList = units?
                .Where(unit => unit != null && !unit.ExcludedFromBattle)
                .Distinct()
                .ToList()
                ?? new List<Unit>();

            Debug.Log(
                $"[RuntimeParity] ai-precalc checking {unitList.Count} unit(s) for player {cellGrid.CurrentPlayerNumber}");

            cellGrid.RefreshSceneCellOccupancyNow();

            for (int i = 0; i < unitList.Count; i++)
            {
                Unit unit = unitList[i];
                string context = $"ai-precalc {i + 1}/{unitList.Count}";
                if (unit == null)
                {
                    Debug.LogWarning($"[RuntimeParity] SKIP ({context}) unit reference was lost before compare");
                    continue;
                }

                CompareMovementReach(unit, cellGrid, context, refreshOccupancy: false);
            }
        }

        public static void CompareMovementReach(Unit unit, CellGrid cellGrid, string context)
        {
            CompareMovementReach(unit, cellGrid, context, refreshOccupancy: true);
        }

        public static void CompareMovementReach(
            Unit unit,
            CellGrid cellGrid,
            string context,
            bool refreshOccupancy)
        {
            if (!Enabled || !Application.isPlaying || unit == null || cellGrid == null)
            {
                return;
            }

            GridUnit mirror = unit.GetComponent<GridUnit>();
            if (mirror == null)
            {
                LogResult(context, unit, "SKIP", "no GridUnit mirror");
                return;
            }

            if (refreshOccupancy)
            {
                cellGrid.RefreshSceneCellOccupancyNow();
            }

            unit.SyncMirroredRuntimeNow();

            if (unit.Cell == null || mirror.CurrentCell == null)
            {
                LogResult(
                    context,
                    unit,
                    "SKIP",
                    $"missing cell scene={FormatCell(unit.Cell)} mirror={FormatCell(mirror.CurrentCell)}");
                return;
            }

            List<Cell> allCells = cellGrid.GetAllCells()?.Where(cell => cell != null).ToList()
                ?? new List<Cell>();

            bool originMatch = CellsMatch(unit.Cell, mirror.CurrentCell);
            bool movementPointsMatch = Mathf.Approximately(unit.MovementPoints, mirror.MovementPointsRemaining);

            HashSet<Cell> sceneReach = unit.ComputeAvailableDestinationsSceneOnly(allCells);
            mirror.CachePaths(allCells);
            HashSet<Cell> mirrorReach = mirror.GetAvailableDestinations(allCells);

            SplitCellSetDifference(sceneReach, mirrorReach, out List<Cell> onlyScene, out List<Cell> onlyMirror);
            bool destinationsMatch = onlyScene.Count == 0 && onlyMirror.Count == 0;

            bool pathsMatch = CompareSamplePaths(
                unit,
                mirror,
                allCells,
                sceneReach,
                mirrorReach,
                out string pathDetail);

            bool match = originMatch && movementPointsMatch && destinationsMatch && pathsMatch;
            string status = match ? "MATCH" : "MISMATCH";

            var detail = new StringBuilder();
            detail.Append($"origin={(originMatch ? "ok" : DescribeCellPair(unit.Cell, mirror.CurrentCell))}");
            detail.Append($", mp={(movementPointsMatch ? "ok" : $"scene={unit.MovementPoints:0.##} mirror={mirror.MovementPointsRemaining:0.##}")}");
            detail.Append($", destinations={(destinationsMatch ? "ok" : DescribeDestinationDiff(onlyScene, onlyMirror))}");
            if (!string.IsNullOrEmpty(pathDetail))
            {
                detail.Append($", paths={pathDetail}");
            }

            LogResult(context, unit, status, detail.ToString());
        }

        private static bool CompareSamplePaths(
            Unit unit,
            GridUnit mirror,
            List<Cell> allCells,
            HashSet<Cell> sceneReach,
            HashSet<Cell> mirrorReach,
            out string detail)
        {
            detail = string.Empty;
            List<Cell> samples = sceneReach
                .Intersect(mirrorReach)
                .Where(cell => cell != null)
                .Take(MaxSamplePaths)
                .ToList();

            if (samples.Count == 0)
            {
                samples = sceneReach
                    .Concat(mirrorReach)
                    .Where(cell => cell != null)
                    .Distinct()
                    .Take(MaxSamplePaths)
                    .ToList();
            }

            if (samples.Count == 0)
            {
                detail = "ok";
                return true;
            }

            var mismatches = new List<string>();
            foreach (Cell destination in samples)
            {
                IList<Cell> scenePath = unit.ComputeFindPathSceneOnly(allCells, destination);
                IList<Cell> mirrorPath = mirror.FindPath(allCells, destination);

                if (PathsMatch(scenePath, mirrorPath))
                {
                    continue;
                }

                mismatches.Add(
                    $"{FormatCell(destination)} scene={FormatPath(scenePath)} mirror={FormatPath(mirrorPath)}");
            }

            if (mismatches.Count == 0)
            {
                detail = "ok";
                return true;
            }

            detail = string.Join("; ", mismatches);
            return false;
        }

        private static bool PathsMatch(IList<Cell> scenePath, IList<Cell> mirrorPath)
        {
            string sceneKey = FormatPathCoordinates(NormalizeSceneLegacyPath(scenePath));
            string mirrorKey = FormatPathCoordinates(NormalizeMirrorPath(mirrorPath));
            if (sceneKey == mirrorKey)
            {
                return true;
            }

            // Mirror may already be in legacy destination-first form in some call paths.
            return sceneKey == FormatPathCoordinates(NormalizeSceneLegacyPath(mirrorPath));
        }

        private static IList<Cell> NormalizeSceneLegacyPath(IList<Cell> path)
        {
            if (path == null || path.Count == 0)
            {
                return Array.Empty<Cell>();
            }

            return path.Reverse().ToList();
        }

        private static IList<Cell> NormalizeMirrorPath(IList<Cell> path)
        {
            if (path == null || path.Count == 0)
            {
                return Array.Empty<Cell>();
            }

            // Runtime paths include the origin cell; scene legacy paths omit it.
            return path.Count > 1 ? path.Skip(1).ToList() : path.ToList();
        }

        private static string FormatPathCoordinates(IList<Cell> path)
        {
            if (path == null || path.Count == 0)
            {
                return "[]";
            }

            return "[" + string.Join(">", path.Select(FormatCell)) + "]";
        }

        private static void SplitCellSetDifference(
            HashSet<Cell> sceneReach,
            HashSet<Cell> mirrorReach,
            out List<Cell> onlyScene,
            out List<Cell> onlyMirror)
        {
            onlyScene = sceneReach
                .Where(cell => cell != null && !mirrorReach.Any(other => CellsMatch(cell, other)))
                .ToList();
            onlyMirror = mirrorReach
                .Where(cell => cell != null && !sceneReach.Any(other => CellsMatch(cell, other)))
                .ToList();
        }

        private static bool CellsMatch(Cell left, Cell right)
        {
            if (left == right)
            {
                return true;
            }

            return left != null
                && right != null
                && left.Coordinates == right.Coordinates;
        }

        private static string DescribeCellPair(Cell sceneCell, Cell mirrorCell)
        {
            return $"scene={FormatCell(sceneCell)} mirror={FormatCell(mirrorCell)}";
        }

        private static string DescribeDestinationDiff(IReadOnlyList<Cell> onlyScene, IReadOnlyList<Cell> onlyMirror)
        {
            return $"scene-only=[{FormatCells(onlyScene)}] mirror-only=[{FormatCells(onlyMirror)}]";
        }

        private static string FormatCells(IEnumerable<Cell> cells)
        {
            if (cells == null)
            {
                return string.Empty;
            }

            return string.Join(", ", cells.Select(FormatCell));
        }

        private static string FormatCell(Cell cell)
        {
            return cell == null ? "null" : $"{cell.Coordinates.x},{cell.Coordinates.y}";
        }

        private static string FormatPath(IList<Cell> path)
        {
            if (path == null || path.Count == 0)
            {
                return "[]";
            }

            return "[" + string.Join(">", path.Select(FormatCell)) + "]";
        }

        private static string FormatUnitLabel(Unit unit)
        {
            if (unit == null)
            {
                return "null";
            }

            return $"{unit.name}@{FormatCell(unit.Cell)} unitId={unit.UnitId}";
        }

        private static void LogResult(string context, Unit unit, string status, string detail)
        {
            string message = $"[RuntimeParity] {status} ({context}) {FormatUnitLabel(unit)} {detail}";
            if (status == "MATCH")
            {
                Debug.Log(message);
            }
            else if (status == "SKIP")
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogWarning(message);
            }
        }
    }
}
