using System.Collections.Generic;
using System.Linq;
using System.Text;
using TbsFramework.Cells;
using UnityEngine;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Board.States;
using Windy.Srpg.Runtime.Units;

namespace Windy.Srpg.Game.Diagnostics
{
    /// <summary>
    /// Read-only parity checks that compare the dormant runtime board model against the
    /// authoritative framework state during the runtime-ownership migration. The goal is to
    /// prove the runtime produces identical results on the live board before any authority is
    /// handed over to it. These helpers never mutate scene or runtime state (only the runtime
    /// unit's internal path cache, which is recomputed on demand anyway).
    /// </summary>
    public static class RuntimeParityDiagnostics
    {
        public static bool LogReachableParity = true;
        public static bool LogSelectionParity = true;
        public static bool LogRightClickParity = true;
        public static bool LogSelectedMoveParity = true;
        public static bool LogPendingMoveParity = true;

        /// <summary>
        /// Shadow-harness check: did the runtime's waiting-for-input state decide to select the
        /// same unit the framework selected on this click? <paramref name="frameworkSelected"/> is
        /// the unit the framework will select (null if none); <paramref name="runtimeDecided"/> is
        /// the BattleUnit the runtime would select (null if none); <paramref name="expectedRuntime"/>
        /// is the runtime mirror of <paramref name="frameworkSelected"/>.
        /// </summary>
        public static void CompareSelection(
            CustomUnit clicked,
            CustomUnit frameworkSelected,
            BattleUnit runtimeDecided,
            BattleUnit expectedRuntime)
        {
            if (!LogSelectionParity)
            {
                return;
            }

            bool match = runtimeDecided == expectedRuntime;

            var sb = new StringBuilder();
            sb.AppendLine($"[RuntimeShadow] Selection on click of {Describe(clicked)}");
            sb.AppendLine($"  framework: selected={(frameworkSelected != null ? frameworkSelected.name : "<none>")}");
            sb.AppendLine($"  runtime:   selected={(runtimeDecided != null ? runtimeDecided.name : "<none>")}");
            sb.Append(match ? "  RESULT: MATCH" : "  RESULT: MISMATCH");

            if (match)
            {
                Debug.Log(sb.ToString());
            }
            else
            {
                Debug.LogWarning(sb.ToString());
            }
        }

        /// <summary>
        /// Shadow-harness check: did right-click from a selected-unit state leave the runtime with
        /// the same selected unit as the framework? For the current behavior both sides should
        /// typically end with no selected unit.
        /// </summary>
        public static void CompareRightClick(
            CustomUnit frameworkPreviouslySelected,
            CustomUnit frameworkSelectedAfterRightClick,
            BattleUnit runtimeSelectedAfterRightClick,
            BattleUnit expectedRuntimeAfterRightClick)
        {
            if (!LogRightClickParity)
            {
                return;
            }

            bool match = runtimeSelectedAfterRightClick == expectedRuntimeAfterRightClick;

            var sb = new StringBuilder();
            sb.AppendLine($"[RuntimeShadow] RightClick from selected {Describe(frameworkPreviouslySelected)}");
            sb.AppendLine($"  framework: selected={(frameworkSelectedAfterRightClick != null ? frameworkSelectedAfterRightClick.name : "<none>")}");
            sb.AppendLine($"  runtime:   selected={(runtimeSelectedAfterRightClick != null ? runtimeSelectedAfterRightClick.name : "<none>")}");
            sb.Append(match ? "  RESULT: MATCH" : "  RESULT: MISMATCH");

            if (match)
            {
                Debug.Log(sb.ToString());
            }
            else
            {
                Debug.LogWarning(sb.ToString());
            }
        }

        public static void CompareSelectedStateTransition(
            string eventLabel,
            CustomUnit frameworkPreviouslySelected,
            string frameworkStateLabel,
            CustomUnit frameworkSelectedAfterTransition,
            Cell frameworkPendingDestination,
            BoardState runtimeState,
            BattleUnit expectedRuntimeSelected,
            BoardCell expectedRuntimePendingDestination)
        {
            if (!LogSelectedMoveParity)
            {
                return;
            }

            string runtimeStateLabel = runtimeState?.DiagnosticStateLabel ?? "<null>";
            BattleUnit runtimeSelected = runtimeState?.SelectedUnit;
            BoardCell runtimePendingDestination = runtimeState?.PendingDestination;

            bool match =
                string.Equals(runtimeStateLabel, frameworkStateLabel, System.StringComparison.Ordinal)
                && runtimeSelected == expectedRuntimeSelected
                && runtimePendingDestination == expectedRuntimePendingDestination;

            var sb = new StringBuilder();
            sb.AppendLine($"[RuntimeShadow] {eventLabel} from selected {Describe(frameworkPreviouslySelected)}");
            sb.AppendLine($"  framework: state={frameworkStateLabel}, selected={(frameworkSelectedAfterTransition != null ? frameworkSelectedAfterTransition.name : "<none>")}, pending={Describe(frameworkPendingDestination)}");
            sb.AppendLine($"  runtime:   state={runtimeStateLabel}, selected={(runtimeSelected != null ? runtimeSelected.name : "<none>")}, pending={Describe(runtimePendingDestination)}");
            sb.Append(match ? "  RESULT: MATCH" : "  RESULT: MISMATCH");

            if (match)
            {
                Debug.Log(sb.ToString());
            }
            else
            {
                Debug.LogWarning(sb.ToString());
            }
        }

        public static void ComparePendingMoveTransition(
            string eventLabel,
            CustomUnit frameworkUnit,
            string frameworkStateLabel,
            CustomUnit frameworkSelectedAfterTransition,
            Cell frameworkPendingDestination,
            Cell frameworkUnitCellAfterTransition,
            float frameworkMovementPointsAfterTransition,
            bool frameworkFinishedAfterTransition,
            BattleBoard.ShadowTransitionSnapshot runtimeSnapshot)
        {
            if (!LogPendingMoveParity)
            {
                return;
            }

            BoardCell expectedRuntimePendingDestination = frameworkPendingDestination?.GetComponent<BoardCell>();
            BoardCell expectedRuntimeUnitCell = frameworkUnitCellAfterTransition?.GetComponent<BoardCell>();
            BattleUnit expectedRuntimeSelected = frameworkSelectedAfterTransition?.GetComponent<BattleUnit>();

            bool match =
                string.Equals(runtimeSnapshot.StateLabel, frameworkStateLabel, System.StringComparison.Ordinal)
                && runtimeSnapshot.SelectedUnit == expectedRuntimeSelected
                && runtimeSnapshot.PendingDestination == expectedRuntimePendingDestination
                && runtimeSnapshot.ObservedUnitCell == expectedRuntimeUnitCell
                && Mathf.Approximately(runtimeSnapshot.ObservedUnitMovementPoints, frameworkMovementPointsAfterTransition)
                && runtimeSnapshot.ObservedUnitFinished == frameworkFinishedAfterTransition;

            var sb = new StringBuilder();
            sb.AppendLine($"[RuntimeShadow] {eventLabel} for {Describe(frameworkUnit)}");
            sb.AppendLine($"  framework: state={frameworkStateLabel}, selected={(frameworkSelectedAfterTransition != null ? frameworkSelectedAfterTransition.name : "<none>")}, pending={Describe(frameworkPendingDestination)}, cell={Describe(frameworkUnitCellAfterTransition)}, mp={frameworkMovementPointsAfterTransition}, finished={frameworkFinishedAfterTransition}");
            sb.AppendLine($"  runtime:   state={runtimeSnapshot.StateLabel}, selected={(runtimeSnapshot.SelectedUnit != null ? runtimeSnapshot.SelectedUnit.name : "<none>")}, pending={Describe(runtimeSnapshot.PendingDestination)}, cell={Describe(runtimeSnapshot.ObservedUnitCell)}, mp={runtimeSnapshot.ObservedUnitMovementPoints}, finished={runtimeSnapshot.ObservedUnitFinished}");
            sb.Append(match ? "  RESULT: MATCH" : "  RESULT: MISMATCH");

            if (match)
            {
                Debug.Log(sb.ToString());
            }
            else
            {
                Debug.LogWarning(sb.ToString());
            }
        }

        public static void CompareReachable(CustomUnit frameworkUnit, IReadOnlyList<Cell> frameworkCells, HashSet<Cell> frameworkReachable)
        {
            if (!LogReachableParity || frameworkUnit == null)
            {
                return;
            }

            BattleUnit runtimeUnit = frameworkUnit.GetComponent<BattleUnit>();
            if (runtimeUnit == null)
            {
                Debug.LogWarning($"[RuntimeParity] {Describe(frameworkUnit)} has no BattleUnit mirror; cannot compare reachable sets.");
                return;
            }

            List<BoardCell> runtimeCells = frameworkCells?
                .Select(cell => cell != null ? cell.GetComponent<BoardCell>() : null)
                .Where(cell => cell != null)
                .ToList() ?? new List<BoardCell>();

            // Mirror the framework's GetAvailableDestinations, which recomputes paths fresh on
            // every call. The runtime unit otherwise reuses a stale path cache across selections.
            runtimeUnit.CachePaths(runtimeCells);
            HashSet<BoardCell> runtimeReachable = runtimeUnit.GetAvailableDestinations(runtimeCells);

            var frameworkReachableRuntime = new HashSet<BoardCell>();
            int unmappedFramework = 0;
            if (frameworkReachable != null)
            {
                foreach (Cell cell in frameworkReachable)
                {
                    BoardCell runtimeCell = cell != null ? cell.GetComponent<BoardCell>() : null;
                    if (runtimeCell != null)
                    {
                        frameworkReachableRuntime.Add(runtimeCell);
                    }
                    else
                    {
                        unmappedFramework++;
                    }
                }
            }

            List<BoardCell> onlyFramework = frameworkReachableRuntime.Except(runtimeReachable).ToList();
            List<BoardCell> onlyRuntime = runtimeReachable.Except(frameworkReachableRuntime).ToList();

            string fwOrigin = frameworkUnit.Cell != null ? frameworkUnit.Cell.OffsetCoord.ToString() : "<null>";
            string rtOrigin = runtimeUnit.CurrentCell != null ? runtimeUnit.CurrentCell.Coordinates.ToString() : "<null>";

            var sb = new StringBuilder();
            sb.AppendLine($"[RuntimeParity] Reachable comparison for {Describe(frameworkUnit)}");
            sb.AppendLine($"  origin: framework={fwOrigin} runtime={rtOrigin}");
            sb.AppendLine($"  movePoints: framework={frameworkUnit.MovementPoints} runtime={runtimeUnit.MovementPointsRemaining}");
            sb.AppendLine($"  cells: framework={frameworkCells?.Count ?? 0} runtimeMapped={runtimeCells.Count}");
            sb.AppendLine($"  reachable: framework={frameworkReachable?.Count ?? 0} runtime={runtimeReachable.Count} (unmappedFramework={unmappedFramework})");

            if (onlyFramework.Count == 0 && onlyRuntime.Count == 0 && unmappedFramework == 0)
            {
                sb.Append("  RESULT: MATCH");
                Debug.Log(sb.ToString());
                return;
            }

            if (onlyFramework.Count > 0)
            {
                sb.AppendLine($"  only-framework ({onlyFramework.Count}): {FormatCoords(onlyFramework)}");
            }

            if (onlyRuntime.Count > 0)
            {
                sb.AppendLine($"  only-runtime ({onlyRuntime.Count}): {FormatCoords(onlyRuntime)}");
            }

            sb.Append("  RESULT: MISMATCH");
            Debug.LogWarning(sb.ToString());
        }

        private static string FormatCoords(IEnumerable<BoardCell> cells)
        {
            return string.Join(", ", cells.Where(cell => cell != null).Select(cell => cell.Coordinates.ToString()));
        }

        private static string Describe(CustomUnit unit)
        {
            return unit == null ? "<null unit>" : $"{unit.name} (player {unit.PlayerNumber})";
        }

        private static string Describe(Cell cell)
        {
            return cell == null ? "<none>" : cell.OffsetCoord.ToString();
        }

        private static string Describe(BoardCell cell)
        {
            return cell == null ? "<none>" : cell.Coordinates.ToString();
        }
    }
}
