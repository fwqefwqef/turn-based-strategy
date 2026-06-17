using System.Collections.Generic;
using System.Linq;
using System.Text;
using TbsFramework.Cells;
using UnityEngine;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Board;
using Windy.Srpg.Runtime.Board.States;
using Windy.Srpg.Runtime.Players;
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
        public static bool EnableRuntimeParityDiagnostics = false;

        public static bool LogReachableParity = true;
        public static bool LogSelectionParity = true;
        public static bool LogRightClickParity = true;
        public static bool LogSelectedMoveParity = true;
        public static bool LogPendingMoveParity = true;
        public static bool LogPendingAttackParity = true;
        public static bool LogRuntimeRoutingParity = true;
        public static bool LogMirroredBoardStateParity = true;
        public static bool LogTurnLoopParity = true;
        public static bool LogBattleOutcomeParity = true;

        private static bool IsLoggingEnabled(bool categoryEnabled)
        {
            return EnableRuntimeParityDiagnostics && categoryEnabled;
        }

        public static void CompareBattleOutcome(
            string eventLabel,
            BattleOutcome frameworkOutcome,
            BattleOutcome runtimeOutcome)
        {
            if (!IsLoggingEnabled(LogBattleOutcomeParity))
            {
                return;
            }

            bool match = frameworkOutcome.IsFinished == runtimeOutcome.IsFinished
                && SequenceEqual(frameworkOutcome.WinningPlayerIds, runtimeOutcome.WinningPlayerIds)
                && SequenceEqual(frameworkOutcome.DefeatedPlayerIds, runtimeOutcome.DefeatedPlayerIds);

            var sb = new StringBuilder();
            sb.AppendLine($"[RuntimeShadow] {eventLabel}");
            sb.AppendLine($"  finished: framework={frameworkOutcome.IsFinished} runtime={runtimeOutcome.IsFinished}");
            if (frameworkOutcome.IsFinished || runtimeOutcome.IsFinished)
            {
                sb.AppendLine($"  winners: framework={FormatPlayerIds(frameworkOutcome.WinningPlayerIds)} runtime={FormatPlayerIds(runtimeOutcome.WinningPlayerIds)}");
                sb.AppendLine($"  defeated: framework={FormatPlayerIds(frameworkOutcome.DefeatedPlayerIds)} runtime={FormatPlayerIds(runtimeOutcome.DefeatedPlayerIds)}");
            }

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

        public static void CompareTurnTransition(
            int currentPlayerId,
            RoundRobinTurnPlan frameworkPlan,
            RoundRobinTurnPlan runtimePlan,
            BattleOutcome frameworkOutcome,
            BattleOutcome runtimeOutcome)
        {
            if (!IsLoggingEnabled(LogTurnLoopParity))
            {
                return;
            }

            bool outcomeMatch = frameworkOutcome.IsFinished == runtimeOutcome.IsFinished
                && SequenceEqual(frameworkOutcome.WinningPlayerIds, runtimeOutcome.WinningPlayerIds)
                && SequenceEqual(frameworkOutcome.DefeatedPlayerIds, runtimeOutcome.DefeatedPlayerIds);

            int frameworkNextPlayerId = frameworkPlan.NextPlayer?.PlayerId ?? -1;
            int runtimeNextPlayerId = runtimePlan.NextPlayer?.PlayerId ?? -1;
            bool nextPlayerMatch = frameworkNextPlayerId == runtimeNextPlayerId;

            var frameworkUnits = new HashSet<string>(FormatBattleUnitNames(frameworkPlan.PlayableUnits));
            var runtimeUnits = new HashSet<string>(FormatBattleUnitNames(runtimePlan.PlayableUnits));
            List<string> onlyFrameworkUnits = frameworkUnits.Except(runtimeUnits).ToList();
            List<string> onlyRuntimeUnits = runtimeUnits.Except(frameworkUnits).ToList();
            bool playableUnitsMatch = onlyFrameworkUnits.Count == 0 && onlyRuntimeUnits.Count == 0;

            string frameworkStateLabel = DescribeExpectedPostTurnState(frameworkPlan.NextPlayer);
            string runtimeStateLabel = DescribeExpectedPostTurnState(runtimePlan.NextPlayer);
            bool stateLabelMatch = frameworkStateLabel == runtimeStateLabel;

            bool match = outcomeMatch && nextPlayerMatch && playableUnitsMatch && stateLabelMatch;

            var sb = new StringBuilder();
            sb.AppendLine($"[RuntimeShadow] End turn from player {currentPlayerId}");
            sb.AppendLine($"  outcome: framework finished={frameworkOutcome.IsFinished} runtime finished={runtimeOutcome.IsFinished}");
            if (frameworkOutcome.IsFinished || runtimeOutcome.IsFinished)
            {
                sb.AppendLine($"  winners: framework={FormatPlayerIds(frameworkOutcome.WinningPlayerIds)} runtime={FormatPlayerIds(runtimeOutcome.WinningPlayerIds)}");
                sb.AppendLine($"  defeated: framework={FormatPlayerIds(frameworkOutcome.DefeatedPlayerIds)} runtime={FormatPlayerIds(runtimeOutcome.DefeatedPlayerIds)}");
            }

            sb.AppendLine($"  nextPlayer: framework={frameworkNextPlayerId} runtime={runtimeNextPlayerId}");
            sb.AppendLine($"  postTurnState: framework={frameworkStateLabel} runtime={runtimeStateLabel}");
            sb.AppendLine($"  playableUnits: framework={frameworkUnits.Count} runtime={runtimeUnits.Count}");

            if (onlyFrameworkUnits.Count > 0)
            {
                sb.AppendLine($"  only-framework units ({onlyFrameworkUnits.Count}): {string.Join(", ", onlyFrameworkUnits)}");
            }

            if (onlyRuntimeUnits.Count > 0)
            {
                sb.AppendLine($"  only-runtime units ({onlyRuntimeUnits.Count}): {string.Join(", ", onlyRuntimeUnits)}");
            }

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

        public static void CompareCurrentPlayerSync(int frameworkPlayerId, int runtimePlayerId)
        {
            if (!IsLoggingEnabled(LogTurnLoopParity))
            {
                return;
            }

            bool match = frameworkPlayerId == runtimePlayerId;
            string message = match
                ? $"[RuntimeShadow] Current player sync framework={frameworkPlayerId} runtime={runtimePlayerId} RESULT: MATCH"
                : $"[RuntimeShadow] Current player sync framework={frameworkPlayerId} runtime={runtimePlayerId} RESULT: MISMATCH";

            if (match)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogWarning(message);
            }
        }

        public static void CompareAiTurnUnitOrder(
            IReadOnlyList<CustomUnit> frameworkOrder,
            IReadOnlyList<BattleUnit> runtimeOrder)
        {
            if (!IsLoggingEnabled(LogTurnLoopParity))
            {
                return;
            }

            List<string> frameworkNames = frameworkOrder?
                .Where(unit => unit != null)
                .Select(unit => unit.name)
                .ToList()
                ?? new List<string>();
            List<string> runtimeNames = runtimeOrder?
                .Where(unit => unit != null)
                .Select(unit => unit.name)
                .ToList()
                ?? new List<string>();

            bool match = frameworkNames.SequenceEqual(runtimeNames);
            var sb = new StringBuilder();
            sb.AppendLine("[RuntimeShadow] AI turn unit order");
            sb.AppendLine($"  framework ({frameworkNames.Count}): {string.Join(" -> ", frameworkNames)}");
            sb.AppendLine($"  runtime ({runtimeNames.Count}): {string.Join(" -> ", runtimeNames)}");
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

        private static bool SequenceEqual(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            left ??= System.Array.Empty<int>();
            right ??= System.Array.Empty<int>();
            return left.SequenceEqual(right);
        }

        private static string FormatPlayerIds(IReadOnlyList<int> playerIds)
        {
            return playerIds == null || playerIds.Count == 0
                ? "<none>"
                : string.Join(", ", playerIds);
        }

        private static IEnumerable<string> FormatBattleUnitNames(IEnumerable<IBattleUnit> units)
        {
            return units?
                .Where(unit => unit != null)
                .Select(DescribeBattleUnit)
                ?? Enumerable.Empty<string>();
        }

        private static string DescribeBattleUnit(IBattleUnit unit)
        {
            if (unit is CustomUnit customUnit)
            {
                return customUnit.name;
            }

            if (unit is BattleUnit battleUnit)
            {
                return battleUnit.name;
            }

            return unit?.ToString() ?? "<none>";
        }

        private static string DescribeExpectedPostTurnState(Windy.Srpg.Runtime.Players.IBattlePlayer player)
        {
            if (player == null)
            {
                return "<none>";
            }

            return player.IsHumanControlled ? "Waiting" : "AiTurn";
        }

        public static void ComparePendingAttackables(
            CustomUnit actor,
            Cell frameworkActingCell,
            Cell runtimeActingCell,
            IEnumerable<CustomUnit> frameworkAttackables,
            IEnumerable<CustomUnit> runtimeAttackables)
        {
            if (!IsLoggingEnabled(LogPendingAttackParity) || actor == null)
            {
                return;
            }

            var frameworkSet = new HashSet<CustomUnit>(frameworkAttackables?.Where(unit => unit != null) ?? Enumerable.Empty<CustomUnit>());
            var runtimeSet = new HashSet<CustomUnit>(runtimeAttackables?.Where(unit => unit != null) ?? Enumerable.Empty<CustomUnit>());
            List<CustomUnit> onlyFramework = frameworkSet.Except(runtimeSet).ToList();
            List<CustomUnit> onlyRuntime = runtimeSet.Except(frameworkSet).ToList();

            bool match = onlyFramework.Count == 0 && onlyRuntime.Count == 0;

            var sb = new StringBuilder();
            sb.AppendLine($"[RuntimeShadow] Pending attackables for {Describe(actor)}");
            sb.AppendLine($"  actingCell: framework={Describe(frameworkActingCell)} runtime={Describe(runtimeActingCell)}");
            sb.AppendLine($"  attackables: framework={frameworkSet.Count} runtime={runtimeSet.Count}");

            if (onlyFramework.Count > 0)
            {
                sb.AppendLine($"  only-framework ({onlyFramework.Count}): {FormatUnits(onlyFramework)}");
            }

            if (onlyRuntime.Count > 0)
            {
                sb.AppendLine($"  only-runtime ({onlyRuntime.Count}): {FormatUnits(onlyRuntime)}");
            }

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

        public static void CompareRuntimeBattleOutcomeRouting(
            string eventLabel,
            CustomCellGrid.RuntimeBattleOutcomeDecision shadowDecision,
            CustomCellGrid.RuntimeBattleOutcomeDecision processDecision)
        {
            if (!IsLoggingEnabled(LogRuntimeRoutingParity))
            {
                return;
            }

            bool match =
                shadowDecision.IsFinished == processDecision.IsFinished
                && SequenceEqual(shadowDecision.WinningPlayerIds, processDecision.WinningPlayerIds)
                && SequenceEqual(shadowDecision.DefeatedPlayerIds, processDecision.DefeatedPlayerIds);

            var sb = new StringBuilder();
            sb.AppendLine($"[RuntimeShadow] {eventLabel}");
            sb.AppendLine($"  shadow:  finished={shadowDecision.IsFinished}, winners={FormatPlayerIds(shadowDecision.WinningPlayerIds)}, defeated={FormatPlayerIds(shadowDecision.DefeatedPlayerIds)}");
            sb.AppendLine($"  process: finished={processDecision.IsFinished}, winners={FormatPlayerIds(processDecision.WinningPlayerIds)}, defeated={FormatPlayerIds(processDecision.DefeatedPlayerIds)}");
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

        public static void CompareRuntimeEndTurnRouting(
            string eventLabel,
            CustomCellGrid.RuntimeEndTurnTransitionDecision shadowDecision,
            CustomCellGrid.RuntimeEndTurnTransitionDecision processDecision)
        {
            if (!IsLoggingEnabled(LogRuntimeRoutingParity))
            {
                return;
            }

            bool match =
                shadowDecision.EndingPlayerId == processDecision.EndingPlayerId
                && shadowDecision.NextPlayerId == processDecision.NextPlayerId
                && string.Equals(
                    shadowDecision.PostTurnStateLabel,
                    processDecision.PostTurnStateLabel,
                    System.StringComparison.Ordinal);

            var sb = new StringBuilder();
            sb.AppendLine($"[RuntimeShadow] {eventLabel}");
            sb.AppendLine($"  shadow:  ending={shadowDecision.EndingPlayerId}, next={shadowDecision.NextPlayerId}, state={shadowDecision.PostTurnStateLabel}");
            sb.AppendLine($"  process: ending={processDecision.EndingPlayerId}, next={processDecision.NextPlayerId}, state={processDecision.PostTurnStateLabel}");
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

        public static void CompareRuntimeStateDecision(
            string eventLabel,
            CustomCellGrid.RuntimeStateTransitionDecision shadowDecision,
            CustomCellGrid.RuntimeStateTransitionDecision processDecision)
        {
            if (!IsLoggingEnabled(LogRuntimeRoutingParity))
            {
                return;
            }

            bool match =
                string.Equals(shadowDecision.StateLabel, processDecision.StateLabel, System.StringComparison.Ordinal)
                && shadowDecision.SelectedUnit == processDecision.SelectedUnit
                && shadowDecision.PendingDestination == processDecision.PendingDestination;

            var sb = new StringBuilder();
            sb.AppendLine($"[RuntimeShadow] {eventLabel}");
            sb.AppendLine($"  shadow:  state={shadowDecision.StateLabel}, selected={(shadowDecision.SelectedUnit != null ? shadowDecision.SelectedUnit.name : "<none>")}, pending={Describe(shadowDecision.PendingDestination)}");
            sb.AppendLine($"  process: state={processDecision.StateLabel}, selected={(processDecision.SelectedUnit != null ? processDecision.SelectedUnit.name : "<none>")}, pending={Describe(processDecision.PendingDestination)}");
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
            if (!IsLoggingEnabled(LogSelectionParity))
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
            if (!IsLoggingEnabled(LogRightClickParity))
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
            if (!IsLoggingEnabled(LogSelectedMoveParity))
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
            if (!IsLoggingEnabled(LogPendingMoveParity))
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
            if (!IsLoggingEnabled(LogReachableParity) || frameworkUnit == null)
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

        private static string FormatUnits(IEnumerable<CustomUnit> units)
        {
            return string.Join(", ", units.Where(unit => unit != null).Select(unit => unit.name));
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
