using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.CameraControl;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.UI;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Localization;
using Windy.Srpg.Game.Skills;
using Windy.Srpg.Game.Units;
using UnityEngine;
using Windy.Srpg.Runtime.Grid;
using Windy.Srpg.Runtime.Rendering;

namespace Windy.Srpg.Game.Abilities
{
    public partial class MoveAbility : Ability
    {
        // --- Movement, pending-move shell, action menu ---
        public static bool SuppressInspectClicks { get; private set; }
        public static bool AllowManualCameraInputInPendingState { get; private set; }

        public struct AttackPreviewPanelData
        {
            public string Name;
            public string HitPoints;
            public string Mitigation;
            public string Attack;
            public string Hit;
            public string Crit;
            public string PrimaryStatLabel;

            public AttackPreviewPanelData(string name, string hitPoints, string mitigation, string attack, string hit, string crit, string primaryStatLabel = null)
            {
                Name = name;
                HitPoints = hitPoints;
                Mitigation = mitigation;
                Attack = attack;
                Hit = hit;
                Crit = crit;
                PrimaryStatLabel = string.IsNullOrWhiteSpace(primaryStatLabel) ? GameTextCatalog.Get("ui.attack_preview.default_stat", "DMG") : primaryStatLabel;
            }
        }

        public readonly struct AreaConfirmTargetPreviewData
        {
            public readonly string Name;
            public readonly int CurrentHitPoints;
            public readonly int ProjectedHitPoints;

            public AreaConfirmTargetPreviewData(string name, int currentHitPoints, int projectedHitPoints)
            {
                Name = string.IsNullOrWhiteSpace(name) ? GameTextCatalog.Get("ui.common.unit", "Unit") : name;
                CurrentHitPoints = Mathf.Max(0, currentHitPoints);
                ProjectedHitPoints = Mathf.Max(0, projectedHitPoints);
            }
        }

        private static string FormatHitPointsDisplay(Unit unit)
        {
            if (unit == null)
            {
                return "-";
            }

            return $"{unit.HitPoints}/{unit.ComputedTotalHitPoints}";
        }

        public Cell Destination { get; set; }
        private IList<Cell> currentPath;
        public HashSet<Cell> availableDestinations;
        private bool awaitingAttackTargetSelection;
        private bool awaitingSkillTargetSelection;
        private bool awaitingTradeTargetSelection;
        private readonly List<Unit> pendingAttackableEnemies = new List<Unit>();
        private readonly List<Cell> pendingAttackPreviewCells = new List<Cell>();
        private readonly List<Unit> pendingSkillTargets = new List<Unit>();
        private readonly List<Cell> pendingSkillPreviewCells = new List<Cell>();
        private readonly List<Cell> pendingAreaSkillCenterCells = new List<Cell>();
        private readonly List<Cell> pendingAreaSkillRadiusCells = new List<Cell>();
        private readonly List<Unit> pendingAreaSkillTargets = new List<Unit>();
        private readonly List<Cell> pendingTradePreviewCells = new List<Cell>();
        private readonly List<Item> attackPreviewWeaponOptions = new List<Item>();
        private Unit selectedAttackPreviewTarget;
        private readonly List<Skill> skillPreviewOptions = new List<Skill>();
        private Unit selectedSkillPreviewTarget;
        private Skill selectedTargetingSkill;
        private Cell selectedAreaSkillCenterCell;
        private Item selectedSkillPreviewWeaponEntry;
        private bool awaitingAreaSkillConfirmation;
        private int attackPreviewWeaponIndex = -1;
        private int skillPreviewIndex = -1;
        private bool resolvingPendingAttack;
        private int cachedOccupancyRevision = -1;

        private readonly struct AreaLineProjection
        {
            public readonly Vector2Int Direction;
            public readonly Cell Endpoint;
            public readonly List<Cell> Cells;

            public AreaLineProjection(Vector2Int direction, Cell endpoint, List<Cell> cells)
            {
                Direction = direction;
                Endpoint = endpoint;
                Cells = cells;
            }
        }

        private readonly struct AreaBorderOutline
        {
            public readonly Cell Cell;
            public readonly bool Top;
            public readonly bool Right;
            public readonly bool Bottom;
            public readonly bool Left;

            public AreaBorderOutline(Cell cell, bool top, bool right, bool bottom, bool left)
            {
                Cell = cell;
                Top = top;
                Right = right;
                Bottom = bottom;
                Left = left;
            }
        }

        public interface IActionMenuUI
        {
            void Show(Vector3 worldPosition, bool showAttack, bool showHeal, bool showSkill, bool showItem, bool showTrade, System.Action onAttack, System.Action onHeal, System.Action onSkill, System.Action onItem, System.Action onTrade, System.Action onWait, System.Action onCancel);
            void Hide();
        }

        public interface IAttackPreviewUI
        {
            void Show(
                Vector3 worldPosition,
                Unit attackerUnit,
                Unit defenderUnit,
                AttackPreviewPanelData attacker,
                AttackPreviewPanelData defender,
                string attackerActionLabel,
                string attackerActionName,
                string defenderActionLabel,
                string defenderActionName,
                bool canCycleAction,
                System.Action onNextAction,
                System.Action onConfirm,
                System.Action onCancel);
            void ShowConfirmOnly(Vector3 worldPosition, System.Action onConfirm, System.Action onCancel);
            void Hide();
        }

        public interface ISkillMenuUI
        {
            void Show(
                Vector3 worldPosition,
                Unit unit,
                IReadOnlyList<Skill> skills,
                System.Func<Skill, bool> canUse,
                System.Action<Skill> onSelect,
                System.Action onCancel);
            void Hide();
        }

        public interface IAreaConfirmUI
        {
            void Show(Vector3 worldPosition, string title, string description, IReadOnlyList<AreaConfirmTargetPreviewData> targetPreviews, System.Action onConfirm, System.Action onCancel);
            void Hide();
        }

        public interface IInventoryMenuUI
        {
            void Show(Vector3 worldPosition, Unit unit, System.Action onClose, System.Action onConsumableUsed);
            void Hide();
        }

        public interface ITradeMenuUI
        {
            void Show(Vector3 worldPosition, Unit selfUnit, Unit friendlyUnit, System.Action<bool> onClose, System.Action onFirstTradeCommitted);
            void Hide();
        }

        private IActionMenuUI _actionMenuUi;
        private IAttackPreviewUI _attackPreviewUi;
        private IInventoryMenuUI _inventoryMenuUi;
        private ISkillMenuUI _skillMenuUi;
        private IAreaConfirmUI _areaConfirmUi;
        private ITradeMenuUI _tradeMenuUi;
        private bool showingInventoryMenu;
        private bool showingTradeMenu;

        private static T FindSceneUi<T>(ref T cachedUi) where T : class
        {
            if (cachedUi is MonoBehaviour cachedBehaviour && cachedBehaviour != null)
            {
                return cachedUi;
            }

            foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>())
            {
                if (mb is T ui)
                {
                    cachedUi = ui;
                    return cachedUi;
                }
            }

            foreach (var mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (mb == null || mb.hideFlags != HideFlags.None)
                {
                    continue;
                }

                GameObject gameObject = mb.gameObject;
                if (gameObject == null || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
                {
                    continue;
                }

                if (mb is T ui)
                {
                    cachedUi = ui;
                    return cachedUi;
                }
            }

            cachedUi = null;
            return null;
        }

        private IActionMenuUI FindActionMenuUI()
        {
            return FindSceneUi(ref _actionMenuUi);
        }

        private IAttackPreviewUI FindAttackPreviewUI()
        {
            return FindSceneUi(ref _attackPreviewUi);
        }

        private IInventoryMenuUI FindInventoryMenuUI()
        {
            return FindSceneUi(ref _inventoryMenuUi);
        }

        private IAreaConfirmUI FindAreaConfirmUI()
        {
            return FindSceneUi(ref _areaConfirmUi);
        }

        private ISkillMenuUI FindSkillMenuUI()
        {
            return FindSceneUi(ref _skillMenuUi);
        }

        private ITradeMenuUI FindTradeMenuUI()
        {
            return FindSceneUi(ref _tradeMenuUi);
        }

        private void HideTradeMenu()
        {
            showingTradeMenu = false;
            if (_tradeMenuUi != null)
            {
                _tradeMenuUi.Hide();
                return;
            }

            FindTradeMenuUI()?.Hide();
        }

        private static List<Cell> ResolveCells(CellGrid cellGrid)
        {
            return cellGrid?.GetAllCells() ?? new List<Cell>();
        }

        private static List<Unit> ResolveGridUnits(CellGrid cellGrid)
        {
            return cellGrid?.GetAllUnits() ?? new List<Unit>();
        }

        private static Cell FindCellByOffset(CellGrid cellGrid, Vector2Int offset)
        {
            return cellGrid?.FindCellByOffset(offset)
                ?? ResolveCells(cellGrid).FirstOrDefault(cell =>
                    cell != null
                    && Mathf.RoundToInt(cell.OffsetCoord.x) == offset.x
                    && Mathf.RoundToInt(cell.OffsetCoord.y) == offset.y);
        }

        protected override IEnumerator Act(CellGrid cellGrid, bool isNetworkInvoked = false)
        {
            RefreshAvailableDestinationsIfNeeded(cellGrid);
            if (UnitReference.CanStartActionThisTurn
                && Destination != null
                && availableDestinations != null
                && availableDestinations.Contains(Destination))
            {
                // If the unit already preview-moved, just commit it.
                if (UnitReference.HasPendingMove)
                {
                    if (!UnitReference.ConfirmPendingMove())
                    {
                        yield break;
                    }
                }
                else
                {
                    // AI / remote (or old path): immediate move
                    if (Destination == null || !UnitReference.IsCellMovableTo(Destination))
                    {
                        yield break;
                    }

                    var path = UnitReference.FindPath(ResolveCells(cellGrid), Destination);
                    yield return UnitReference.Move(Destination, path);
                }

                yield return base.Act(cellGrid, isNetworkInvoked);
            }
        }

        protected override void Display(CellGrid cellGrid)
        {
            if (!UnitReference.CanStartActionThisTurn)
            {
                return;
            }

            foreach (var cell in availableDestinations)
            {
                MarkReachableCell(cell, cellGrid);
            }
        }

        protected override void HandleUnitClicked(Unit unit, CellGrid cellGrid)
        {
            if (cellGrid != null
                && cellGrid.GetCurrentPlayerUnits().Contains(unit)
                && unit != null
                && !unit.IsFinishedForTurn)
            {
                cellGrid.EnterSelectedState(unit);
                return;
            }

            if (unit != null && cellGrid != null && !cellGrid.GetCurrentPlayerUnits().Contains(unit))
            {
                cellGrid.EnterWaitingState();
            }
        }

        public void OnSelectedUnitClicked(CellGrid cellGrid)
        {
            if (UnitReference == null || UnitReference.IsFinishedForTurn)
            {
                return;
            }

            if (!UnitReference.BeginPendingMoveInPlace())
            {
                return;
            }

            EnterPendingMoveConfirmState(cellGrid);
            GameplayCameraController.SetFocusedCell(UnitReference.PreviewCell);
            StartCoroutine(CompletePendingMovePreview(cellGrid, UnitReference.PreviewCell, waitForPreviewCamera: false));
        }

        protected override void HandleCellClicked(Cell cell, CellGrid cellGrid)
        {
            RefreshAvailableDestinationsIfNeeded(cellGrid);

            if (!availableDestinations.Contains(cell))
            {
                return;
            }

            Destination = cell;
            currentPath = null;

            // Compute path
            var path = UnitReference.FindPath(ResolveCells(cellGrid), cell);

            EnterPendingMoveConfirmState(cellGrid);
            GameplayCameraController.SetFocusedCell(cell);
            StartCoroutine(BeginPendingMovePreview(cellGrid, cell, path));
        }

        private void EnterPendingMoveConfirmState(CellGrid cellGrid)
        {
            // Hide reachable tile highlights while waiting for confirm.
            CleanUp(cellGrid);

            // Enter pending state that still allows attacking enemies in range.
            cellGrid?.EnterPendingMoveConfirmState(this);

            // Keep the unit selected while preview/pending confirm is active.
            UnitReference.OnUnitSelected();
        }

        private IEnumerator BeginPendingMovePreview(CellGrid cellGrid, Cell destinationCell, IList<Cell> path)
        {
            yield return StartCoroutine(UnitReference.PreviewMove(destinationCell, path));
            yield return StartCoroutine(CompletePendingMovePreview(cellGrid, destinationCell, waitForPreviewCamera: true));
        }

        private IEnumerator CompletePendingMovePreview(CellGrid cellGrid, Cell destinationCell, bool waitForPreviewCamera)
        {
            if (!UnitReference.HasPendingMove)
            {
                yield break;
            }

            if (UnitReference.PreviewCell != destinationCell)
            {
                yield break;
            }

            if (cellGrid == null || cellGrid.CurrentState is not CellGridStateMovePendingConfirm)
            {
                yield break;
            }

            // Preview-center camera focus is intentionally disabled. Once the
            // preview move finishes, we immediately open the pending action menu
            // instead of recentering the camera on the destination tile first.

            ShowActionMenu(cellGrid);
        }

        public void OnPendingMoveStateEnter(CellGrid cellGrid)
        {
        }

        public void OnPendingMoveStateExit(CellGrid cellGrid)
        {
            SuppressInspectClicks = false;
            AllowManualCameraInputInPendingState = false;

            awaitingAttackTargetSelection = false;
            awaitingSkillTargetSelection = false;
            awaitingTradeTargetSelection = false;
            awaitingAreaSkillConfirmation = false;
            selectedAttackPreviewTarget = null;
            selectedSkillPreviewTarget = null;
            selectedTargetingSkill = null;
            selectedAreaSkillCenterCell = null;
            selectedSkillPreviewWeaponEntry = null;
            attackPreviewWeaponOptions.Clear();
            skillPreviewOptions.Clear();
            attackPreviewWeaponIndex = -1;
            skillPreviewIndex = -1;
            showingInventoryMenu = false;
            showingTradeMenu = false;

            ClearAttackTargetingPreview();
            ClearSkillTargetingPreview();
            ClearTradeTargetingPreview();

            var actionMenuUi = FindActionMenuUI();
            if (actionMenuUi != null)
            {
                actionMenuUi.Hide();
            }

            FindAttackPreviewUI()?.Hide();
            FindInventoryMenuUI()?.Hide();
            FindSkillMenuUI()?.Hide();
            FindAreaConfirmUI()?.Hide();
            HideTradeMenu();
        }

        public void OnPendingMoveUnitClicked(Unit unit, CellGrid cellGrid)
        {
            if (resolvingPendingAttack || showingInventoryMenu || showingTradeMenu || unit == null || !UnitReference.HasPendingMove)
            {
                return;
            }

            if (awaitingSkillTargetSelection)
            {
                if (IsAreaSkill(selectedTargetingSkill))
                {
                    if (awaitingAreaSkillConfirmation)
                    {
                        return;
                    }

                    HandlePendingAreaSkillCellInteraction(unit.HasPendingMove ? unit.PreviewCell : unit.Cell, cellGrid);
                    return;
                }

                HandlePendingSkillUnitClicked(unit, cellGrid);
                return;
            }

            if (awaitingTradeTargetSelection)
            {
                if (unit == UnitReference)
                {
                    CancelTradeTargeting(cellGrid);
                    return;
                }

                if (!IsUnitTradeableFromPreview(unit, cellGrid))
                {
                    return;
                }

                OpenTradeMenuForUnit(unit, cellGrid);
                return;
            }

            if (!awaitingAttackTargetSelection)
            {
                return;
            }

            if (IsAttackPreviewOpen())
            {
                if (unit == UnitReference)
                {
                    CancelAttackPreview(cellGrid);
                    return;
                }

                if (unit == selectedAttackPreviewTarget)
                {
                    ConfirmAttackPreview(cellGrid);
                    return;
                }

                if (!CanAttackTargetWithAnyWeaponFromPreview(unit))
                {
                    return;
                }

                OpenAttackPreview(unit, cellGrid);
                return;
            }

            if (unit == UnitReference)
            {
                CancelAttackTargeting(cellGrid);
                return;
            }

            if (!CanAttackTargetWithAnyWeaponFromPreview(unit))
            {
                return;
            }

            OpenAttackPreview(unit, cellGrid);
        }

        public void OnPendingMoveUnitHighlighted(Unit unit, CellGrid cellGrid)
        {
            if (awaitingSkillTargetSelection && !awaitingAreaSkillConfirmation && IsAreaSkill(selectedTargetingSkill))
            {
                UpdateAreaSkillProjection(unit.HasPendingMove ? unit.PreviewCell : unit.Cell, cellGrid);
            }
        }

        public void OnPendingMoveUnitDehighlighted(Unit unit, CellGrid cellGrid)
        {
        }

        public void OnPendingMoveCellSelected(Cell cell, CellGrid cellGrid)
        {
            if (!awaitingSkillTargetSelection || awaitingAreaSkillConfirmation || !IsAreaSkill(selectedTargetingSkill))
            {
                return;
            }

            UpdateAreaSkillProjection(cell, cellGrid);
        }

        public void OnPendingMoveCellDeselected(Cell cell, CellGrid cellGrid)
        {
        }

        public void OnPendingMoveCellClicked(Cell cell, CellGrid cellGrid)
        {
            if (showingInventoryMenu || showingTradeMenu || cell == null || !UnitReference.HasPendingMove)
            {
                return;
            }

            if (awaitingSkillTargetSelection)
            {
                if (IsAreaSkill(selectedTargetingSkill))
                {
                    if (awaitingAreaSkillConfirmation)
                    {
                        return;
                    }

                    if (IsPendingActingCell(cell, cellGrid))
                    {
                        CancelSkillTargeting(cellGrid);
                        return;
                    }

                    HandlePendingAreaSkillCellInteraction(cell, cellGrid);
                    return;
                }

                if (IsPendingActingCell(cell, cellGrid))
                {
                    CancelSkillTargeting(cellGrid);
                }

                return;
            }

            if (awaitingTradeTargetSelection)
            {
                if (IsPendingActingCell(cell, cellGrid))
                {
                    CancelTradeTargeting(cellGrid);
                }

                return;
            }

            if (!awaitingAttackTargetSelection)
            {
                return;
            }

            if (IsAttackPreviewOpen())
            {
                if (IsPendingActingCell(cell, cellGrid))
                {
                    CancelAttackPreview(cellGrid);
                }

                return;
            }

            if (IsPendingActingCell(cell, cellGrid))
            {
                CancelAttackTargeting(cellGrid);
            }
        }

        private bool IsUnitAttackableFromPreview(Unit target, CellGrid cellGrid)
        {
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            return target != null && UnitReference.IsUnitAttackable(target, target.Cell, actingCell);
        }

        private bool IsUnitAttackableFromPreview(Unit target)
        {
            return IsUnitAttackableFromPreview(target, FindSceneCellGrid());
        }

        private bool IsPendingActingCell(Cell cell, CellGrid cellGrid)
        {
            if (cell == null)
            {
                return false;
            }

            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            return actingCell != null && cell == actingCell;
        }

        private void BeginPendingCombatPresentation(CellGrid cellGrid)
        {
            cellGrid?.PrepareRuntimeRoutedPendingAttackCommit();
        }

        private void CommitPendingMoveAfterCombatPresentation(CellGrid cellGrid)
        {
            cellGrid?.TryCommitPendingMoveAfterCombatPresentation(UnitReference);
        }

        private void CommitPendingMoveFromPendingAction(CellGrid cellGrid, bool consumeAllRemainingMovement = false)
        {
            cellGrid?.TryCommitPendingMoveFromPendingAction(UnitReference, consumeAllRemainingMovement);
        }

        private void EnterPendingMenuBlockedInput(CellGrid cellGrid)
        {
            if (cellGrid != null && cellGrid.ShouldRouteHumanMovementThroughRuntime)
            {
                cellGrid.EnterSceneOnlyBlockedInputState();
                return;
            }

            cellGrid?.EnterBlockedInputState();
        }

        private void EndTurnAndCommitPendingMove(CellGrid cellGrid)
        {
            if (cellGrid != null && cellGrid.ShouldRouteHumanMovementThroughRuntime)
            {
                CellGrid.RuntimeStateTransitionDecision runtimeDecision = cellGrid.ProcessRuntimePendingMoveWait();
                CommitPendingMoveFromPendingAction(cellGrid, consumeAllRemainingMovement: true);
                UnitReference.OnUnitDeselected();
                UnitReference.EndTurnForUnit();

                if (runtimeDecision.StateLabel == "Waiting")
                {
                    cellGrid.ApplySceneStateFromRuntime(cellGrid.EnterWaitingState);
                }

                return;
            }

            if (UnitReference.HasPendingMove)
            {
                UnitReference.ConfirmPendingMove();
            }

            UnitReference.OnUnitDeselected();
            UnitReference.EndTurnForUnit();
            cellGrid?.EnterWaitingState();
        }

        private IEnumerator AttackThenConfirmPendingMove(Unit unitToAttack, CellGrid cellGrid)
        {
            resolvingPendingAttack = true;
            try
            {
                FindActionMenuUI()?.Hide();
                FindAttackPreviewUI()?.Hide();
                FindInventoryMenuUI()?.Hide();
                FindSkillMenuUI()?.Hide();
                FindAreaConfirmUI()?.Hide();
                HideTradeMenu();
                awaitingAttackTargetSelection = false;
                awaitingSkillTargetSelection = false;
                awaitingTradeTargetSelection = false;
                awaitingAreaSkillConfirmation = false;
                selectedAttackPreviewTarget = null;
                selectedSkillPreviewTarget = null;
                selectedTargetingSkill = null;
                selectedAreaSkillCenterCell = null;
                selectedSkillPreviewWeaponEntry = null;
                attackPreviewWeaponOptions.Clear();
                skillPreviewOptions.Clear();
                attackPreviewWeaponIndex = -1;
                skillPreviewIndex = -1;
                showingInventoryMenu = false;

                ClearAttackTargetingPreview();
                ClearSkillTargetingPreview();
                ClearTradeTargetingPreview();
                BeginPendingCombatPresentation(cellGrid);

                if (UnitReference == null)
                {
                    yield break;
                }

                UnitReference.AttackHandler(unitToAttack);
                UnitReference.OnUnitDeselected();
                yield return new WaitUntil(() => UnitReference == null || !UnitReference.IsAttackSequenceRunning);

                CommitPendingMoveAfterCombatPresentation(cellGrid);
            }
            finally
            {
                CompletePendingActionResolution(cellGrid);
            }
        }

        private void CompletePendingActionResolution(CellGrid cellGrid)
        {
            resolvingPendingAttack = false;
            cellGrid?.EnterPostCombatGridState();
        }

        private void ShowActionMenu(CellGrid cellGrid)
        {
            SuppressInspectClicks = false;
            AllowManualCameraInputInPendingState = false;
            var actionMenuUi = FindActionMenuUI();
            if (actionMenuUi == null)
            {
                if (UnitReference.HasPendingMove)
                {
                    UnitReference.ConfirmPendingMove();
                }

                UnitReference.OnUnitDeselected();
                cellGrid?.EnterWaitingState();
                return;
            }

            awaitingAttackTargetSelection = false;
            awaitingSkillTargetSelection = false;
            awaitingTradeTargetSelection = false;
            awaitingAreaSkillConfirmation = false;
            selectedAttackPreviewTarget = null;
            selectedSkillPreviewTarget = null;
            selectedTargetingSkill = null;
            selectedAreaSkillCenterCell = null;
            selectedSkillPreviewWeaponEntry = null;
            attackPreviewWeaponOptions.Clear();
            skillPreviewOptions.Clear();
            attackPreviewWeaponIndex = -1;
            skillPreviewIndex = -1;
            ClearAttackPreviewCells();
            ClearSkillPreviewCells();
            ClearTradePreviewCells();
            FindAttackPreviewUI()?.Hide();
            FindInventoryMenuUI()?.Hide();
            FindSkillMenuUI()?.Hide();
            FindAreaConfirmUI()?.Hide();
            HideTradeMenu();
            showingInventoryMenu = false;
            ClearPendingAttackableEnemies();
            ClearPendingSkillTargets();

            var enemyUnits = GetEnemySkillTargetCandidates(cellGrid);
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            bool canAttackFromPreview = UnitReference.HasAnyWeaponThatCanAttack(enemyUnits, actingCell);
            bool canUseAnyHealingSkillFromPreview = HasAnyUsableHealingSkillFromPreview(cellGrid);
            bool canUseAnySkillFromPreview = HasAnyUsableSkillFromPreview(cellGrid);
            bool showItem = (UnitReference.Inventory?.Entries.Count ?? 0) > 0;
            bool showTrade = GetTradePartnerFromPreview(cellGrid) != null;
            Vector3 actionMenuWorldPosition =
                actingCell != null
                    ? actingCell.transform.position
                    : UnitReference.transform.position;

            actionMenuUi.Show(
                worldPosition: actionMenuWorldPosition,
                showAttack: canAttackFromPreview,
                showHeal: canUseAnyHealingSkillFromPreview,
                showSkill: canUseAnySkillFromPreview,
                showItem: showItem,
                showTrade: showTrade,
                onAttack: () => BeginAttackTargeting(cellGrid),
                onHeal: () => BeginHealSelection(cellGrid),
                onSkill: () => BeginSkillSelection(cellGrid),
                onItem: () => ShowInventoryMenu(cellGrid),
                onTrade: () => BeginTradeTargeting(cellGrid),
                onWait: () =>
                {
                    EndTurnAndCommitPendingMove(cellGrid);
                    actionMenuUi.Hide();
                },
                onCancel: () =>
                {
                    CancelPendingMoveAndRestoreSelection(cellGrid);
                });
        }

        public void OnPendingMoveRightClicked(CellGrid cellGrid)
        {
            if (TryHandlePendingMoveRightClickUiModes(cellGrid))
            {
                return;
            }

            CancelPendingMoveAndRestoreSelection(cellGrid);
        }

        internal bool TryHandlePendingMoveRightClickUiModes(CellGrid cellGrid)
        {
            if (!UnitReference.HasPendingMove)
            {
                return false;
            }

            if (showingInventoryMenu)
            {
                return true;
            }

            if (showingTradeMenu)
            {
                return true;
            }

            if (awaitingAttackTargetSelection)
            {
                if (IsAttackPreviewOpen())
                {
                    CancelAttackPreview(cellGrid);
                }
                else
                {
                    CancelAttackTargeting(cellGrid);
                }

                return true;
            }

            if (awaitingSkillTargetSelection)
            {
                if (IsSkillPreviewOpen())
                {
                    CancelSkillPreview(cellGrid);
                }
                else if (awaitingAreaSkillConfirmation)
                {
                    CancelAreaSkillConfirmation(cellGrid);
                }
                else
                {
                    CancelSkillTargeting(cellGrid);
                }

                return true;
            }

            if (awaitingTradeTargetSelection)
            {
                CancelTradeTargeting(cellGrid);
                return true;
            }

            return false;
        }

        internal void ApplySceneEffectsAfterRuntimePendingMoveRightClick(
            CellGrid cellGrid,
            CellGrid.RuntimeStateTransitionDecision runtimeDecision)
        {
            if (runtimeDecision.StateLabel == "Selected"
                && runtimeDecision.SelectedUnit == UnitReference)
            {
                CancelPendingMoveAndRestoreSelection(cellGrid);
                return;
            }

            if (runtimeDecision.StateLabel == "Waiting")
            {
                UnitReference.CancelPendingMove();
                FindActionMenuUI()?.Hide();
                cellGrid.ApplySceneStateFromRuntime(cellGrid.EnterWaitingState);
            }
        }

    }
}
