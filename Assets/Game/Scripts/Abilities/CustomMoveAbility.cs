using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Windy.Srpg.Game.CameraControl;
using TbsFramework.Cells;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Grid.States;
using Windy.Srpg.Game.UI;
using Windy.Srpg.Game.Inventory;
using Windy.Srpg.Game.Localization;
using Windy.Srpg.Game.Skills;
using Windy.Srpg.Game.Units;
using UnityEngine;
using Windy.Srpg.Runtime.Board;

namespace Windy.Srpg.Game.Abilities
{
    public class CustomMoveAbility : CustomAbility
    {
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

        private static string FormatHitPointsDisplay(CustomUnit unit)
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
        private readonly List<CustomUnit> pendingAttackableEnemies = new List<CustomUnit>();
        private readonly List<Cell> pendingAttackPreviewCells = new List<Cell>();
        private readonly List<CustomUnit> pendingSkillTargets = new List<CustomUnit>();
        private readonly List<Cell> pendingSkillPreviewCells = new List<Cell>();
        private readonly List<Cell> pendingAreaSkillCenterCells = new List<Cell>();
        private readonly List<Cell> pendingAreaSkillRadiusCells = new List<Cell>();
        private readonly List<CustomUnit> pendingAreaSkillTargets = new List<CustomUnit>();
        private readonly List<Cell> pendingTradePreviewCells = new List<Cell>();
        private readonly List<Item> attackPreviewWeaponOptions = new List<Item>();
        private CustomUnit selectedAttackPreviewTarget;
        private readonly List<Skill> skillPreviewOptions = new List<Skill>();
        private CustomUnit selectedSkillPreviewTarget;
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
                CustomUnit attackerUnit,
                CustomUnit defenderUnit,
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
                CustomUnit unit,
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
            void Show(Vector3 worldPosition, CustomUnit unit, System.Action onClose, System.Action onConsumableUsed);
            void Hide();
        }

        public interface ITradeMenuUI
        {
            void Show(Vector3 worldPosition, CustomUnit selfUnit, CustomUnit friendlyUnit, System.Action<bool> onClose, System.Action onFirstTradeCommitted);
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

        private static List<Cell> ResolveGridCells(CustomCellGrid cellGrid)
        {
            return cellGrid?.GetAllCells() ?? new List<Cell>();
        }

        private static List<CustomUnit> ResolveGridUnits(CustomCellGrid cellGrid)
        {
            return cellGrid?.GetAllCustomUnits() ?? new List<CustomUnit>();
        }

        private static Cell FindCellByOffset(CustomCellGrid cellGrid, Vector2Int offset)
        {
            return cellGrid?.FindCellByOffset(offset)
                ?? ResolveGridCells(cellGrid).FirstOrDefault(cell =>
                    cell != null
                    && Mathf.RoundToInt(cell.OffsetCoord.x) == offset.x
                    && Mathf.RoundToInt(cell.OffsetCoord.y) == offset.y);
        }

        protected override IEnumerator Act(CustomCellGrid cellGrid, bool isNetworkInvoked = false)
        {
            if (UnitReference.CanStartActionThisTurn && availableDestinations.Contains(Destination))
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

                    var path = UnitReference.FindPath(ResolveGridCells(cellGrid), Destination);
                    yield return UnitReference.Move(Destination, path);
                }
            }

            yield return base.Act(cellGrid, isNetworkInvoked);
        }

        protected override void Display(CustomCellGrid cellGrid)
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

        public override void OnUnitClicked(CustomUnit unit, CustomCellGrid cellGrid)
        {
            if (cellGrid != null
                && cellGrid.GetCurrentPlayerCustomUnits().Contains(unit)
                && unit != null
                && !unit.IsFinishedForTurn)
            {
                cellGrid.EnterSelectedState(unit);
                return;
            }

            if (unit != null && cellGrid != null && !cellGrid.GetCurrentPlayerCustomUnits().Contains(unit))
            {
                cellGrid.EnterWaitingState();
            }
        }

        public void OnSelectedUnitClicked(CustomCellGrid cellGrid)
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

        protected override void OnCellClicked(Cell cell, CustomCellGrid cellGrid)
        {
            RefreshAvailableDestinationsIfNeeded(cellGrid);

            if (!availableDestinations.Contains(cell))
            {
                return;
            }

            Destination = cell;
            currentPath = null;

            // Compute path
            var path = UnitReference.FindPath(ResolveGridCells(cellGrid), cell);

            EnterPendingMoveConfirmState(cellGrid);
            GameplayCameraController.SetFocusedCell(cell);
            StartCoroutine(BeginPendingMovePreview(cellGrid, cell, path));
        }

        private void EnterPendingMoveConfirmState(CustomCellGrid cellGrid)
        {
            // Hide reachable tile highlights while waiting for confirm.
            CleanUp(cellGrid);

            // Enter pending state that still allows attacking enemies in range.
            cellGrid?.EnterPendingMoveConfirmState(this);

            // Keep the unit selected while preview/pending confirm is active.
            UnitReference.OnUnitSelected();
        }

        private IEnumerator BeginPendingMovePreview(CustomCellGrid cellGrid, Cell destinationCell, IList<Cell> path)
        {
            yield return StartCoroutine(UnitReference.PreviewMove(destinationCell, path));
            yield return StartCoroutine(CompletePendingMovePreview(cellGrid, destinationCell, waitForPreviewCamera: true));
        }

        private IEnumerator CompletePendingMovePreview(CustomCellGrid cellGrid, Cell destinationCell, bool waitForPreviewCamera)
        {
            if (!UnitReference.HasPendingMove)
            {
                yield break;
            }

            if (UnitReference.PreviewCell != destinationCell)
            {
                yield break;
            }

            if (cellGrid == null || cellGrid.CurrentCustomState is not CustomCellGridStateMovePendingConfirm)
            {
                yield break;
            }

            // Preview-center camera focus is intentionally disabled. Once the
            // preview move finishes, we immediately open the pending action menu
            // instead of recentering the camera on the destination tile first.

            ShowActionMenu(cellGrid);
        }

        public void OnPendingMoveStateEnter(CustomCellGrid cellGrid)
        {
        }

        public void OnPendingMoveStateExit(CustomCellGrid cellGrid)
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

        public void OnPendingMoveUnitClicked(CustomUnit unit, CustomCellGrid cellGrid)
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

        public void OnPendingMoveUnitHighlighted(CustomUnit unit, CustomCellGrid cellGrid)
        {
            if (awaitingSkillTargetSelection && !awaitingAreaSkillConfirmation && IsAreaSkill(selectedTargetingSkill))
            {
                UpdateAreaSkillProjection(unit.HasPendingMove ? unit.PreviewCell : unit.Cell, cellGrid);
            }
        }

        public void OnPendingMoveUnitDehighlighted(CustomUnit unit, CustomCellGrid cellGrid)
        {
        }

        public void OnPendingMoveCellSelected(Cell cell, CustomCellGrid cellGrid)
        {
            if (!awaitingSkillTargetSelection || awaitingAreaSkillConfirmation || !IsAreaSkill(selectedTargetingSkill))
            {
                return;
            }

            UpdateAreaSkillProjection(cell, cellGrid);
        }

        public void OnPendingMoveCellDeselected(Cell cell, CustomCellGrid cellGrid)
        {
        }

        public void OnPendingMoveCellClicked(Cell cell, CustomCellGrid cellGrid)
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

        private bool IsUnitAttackableFromPreview(CustomUnit target, CustomCellGrid cellGrid)
        {
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            return target != null && UnitReference.IsUnitAttackable(target, target.Cell, actingCell);
        }

        private bool IsUnitAttackableFromPreview(CustomUnit target)
        {
            return IsUnitAttackableFromPreview(target, FindSceneCellGrid());
        }

        private bool IsPendingActingCell(Cell cell, CustomCellGrid cellGrid)
        {
            if (cell == null)
            {
                return false;
            }

            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            return actingCell != null && cell == actingCell;
        }

        private void BeginPendingCombatPresentation(CustomCellGrid cellGrid)
        {
            cellGrid?.PrepareRuntimeRoutedPendingAttackCommit();
        }

        private void CommitPendingMoveAfterCombatPresentation(CustomCellGrid cellGrid)
        {
            cellGrid?.TryCommitPendingMoveAfterCombatPresentation(UnitReference);
        }

        private void CommitPendingMoveFromPendingAction(CustomCellGrid cellGrid, bool consumeAllRemainingMovement = false)
        {
            cellGrid?.TryCommitPendingMoveFromPendingAction(UnitReference, consumeAllRemainingMovement);
        }

        private void EnterPendingMenuBlockedInput(CustomCellGrid cellGrid)
        {
            if (cellGrid != null && cellGrid.ShouldRouteHumanMovementThroughRuntime)
            {
                cellGrid.EnterLegacyBlockedInputState();
                return;
            }

            cellGrid?.EnterBlockedInputState();
        }

        private void EndTurnAndCommitPendingMove(CustomCellGrid cellGrid)
        {
            if (cellGrid != null && cellGrid.ShouldRouteHumanMovementThroughRuntime)
            {
                CustomCellGrid.RuntimeStateTransitionDecision runtimeDecision = cellGrid.ProcessRuntimePendingMoveWait();
                CommitPendingMoveFromPendingAction(cellGrid, consumeAllRemainingMovement: true);
                UnitReference.OnUnitDeselected();
                UnitReference.EndTurnForUnit();

                if (runtimeDecision.StateLabel == "Waiting")
                {
                    cellGrid.ApplyLegacyStateFromRuntime(cellGrid.EnterWaitingState);
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

        private IEnumerator AttackThenConfirmPendingMove(CustomUnit unitToAttack, CustomCellGrid cellGrid)
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

        private void CompletePendingActionResolution(CustomCellGrid cellGrid)
        {
            resolvingPendingAttack = false;
            cellGrid?.EnterPostCombatGridState();
        }

        private void ShowActionMenu(CustomCellGrid cellGrid)
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

        public void OnPendingMoveRightClicked(CustomCellGrid cellGrid)
        {
            if (TryHandlePendingMoveRightClickUiModes(cellGrid))
            {
                return;
            }

            CancelPendingMoveAndRestoreSelection(cellGrid);
        }

        internal bool TryHandlePendingMoveRightClickUiModes(CustomCellGrid cellGrid)
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

        internal void ApplyLegacyEffectsAfterRuntimePendingMoveRightClick(
            CustomCellGrid cellGrid,
            CustomCellGrid.RuntimeStateTransitionDecision runtimeDecision)
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
                cellGrid.ApplyLegacyStateFromRuntime(cellGrid.EnterWaitingState);
            }
        }

        private void BeginAttackTargeting(CustomCellGrid cellGrid)
        {
            if (!UnitReference.HasPendingMove)
            {
                return;
            }

            var attackableEnemies = GetAttackableEnemiesFromPreview(cellGrid);
            if (attackableEnemies.Count == 0)
            {
                return;
            }

            SuppressInspectClicks = true;
            AllowManualCameraInputInPendingState = true;
            FindActionMenuUI()?.Hide();
            FindSkillMenuUI()?.Hide();
            HideTradeMenu();
            awaitingTradeTargetSelection = false;
            awaitingAttackTargetSelection = true;
            awaitingSkillTargetSelection = false;
            selectedAttackPreviewTarget = null;
            selectedSkillPreviewTarget = null;
            selectedTargetingSkill = null;
            selectedAreaSkillCenterCell = null;
            awaitingAreaSkillConfirmation = false;
            attackPreviewWeaponOptions.Clear();
            skillPreviewOptions.Clear();
            attackPreviewWeaponIndex = -1;
            skillPreviewIndex = -1;

            ShowAttackPreviewCells(cellGrid);
            RefreshPendingAttackableEnemies(cellGrid);
            FindAttackPreviewUI()?.Hide();
        }

        private void BeginHealSelection(CustomCellGrid cellGrid)
        {
            BeginSkillSelection(cellGrid, IsHealingSkill);
        }

        private void BeginSkillSelection(CustomCellGrid cellGrid)
        {
            BeginSkillSelection(cellGrid, null);
        }

        private void BeginSkillSelection(CustomCellGrid cellGrid, System.Func<Skill, bool> skillFilter)
        {
            var skillMenuUi = FindSkillMenuUI();
            if (!UnitReference.HasPendingMove || skillMenuUi == null)
            {
                return;
            }

            var skills = GetAllKnownSkills();
            if (skillFilter != null)
            {
                skills = skills.Where(skill => skill != null && skillFilter(skill)).ToList();
            }

            if (skills.Count == 0)
            {
                return;
            }

            awaitingAttackTargetSelection = false;
            awaitingSkillTargetSelection = false;
            awaitingTradeTargetSelection = false;
            selectedAttackPreviewTarget = null;
            selectedSkillPreviewTarget = null;
            selectedTargetingSkill = null;
            selectedAreaSkillCenterCell = null;
            awaitingAreaSkillConfirmation = false;
            attackPreviewWeaponOptions.Clear();
            skillPreviewOptions.Clear();
            attackPreviewWeaponIndex = -1;
            skillPreviewIndex = -1;
            ClearAttackTargetingPreview();
            ClearSkillTargetingPreview();
            ClearTradeTargetingPreview();
            AllowManualCameraInputInPendingState = false;
            FindActionMenuUI()?.Hide();
            FindAttackPreviewUI()?.Hide();
            FindInventoryMenuUI()?.Hide();
            FindAreaConfirmUI()?.Hide();
            HideTradeMenu();

            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            Vector3 worldPosition =
                actingCell != null
                    ? actingCell.transform.position
                    : UnitReference.transform.position;

            skillMenuUi.Show(
                worldPosition,
                UnitReference,
                skills,
                skill => CanUseSkillFromPreview(skill, cellGrid),
                skill => SelectSkillFromMenu(skill, cellGrid),
                () =>
                {
                    FindSkillMenuUI()?.Hide();
                    ShowActionMenu(cellGrid);
                });
        }

        private void SelectSkillFromMenu(Skill skill, CustomCellGrid cellGrid)
        {
            if (skill?.Data == null || !CanUseSkillFromPreview(skill, cellGrid))
            {
                return;
            }

            SuppressInspectClicks = true;
            AllowManualCameraInputInPendingState = true;
            FindSkillMenuUI()?.Hide();
            selectedTargetingSkill = skill;
            awaitingSkillTargetSelection = true;
            if (IsAreaSkill(skill))
            {
                BeginAreaSkillTargeting(skill, cellGrid);
                return;
            }

            ShowSkillPreviewCells(skill, cellGrid);
            RefreshPendingSkillTargets(skill, cellGrid);
            FindAttackPreviewUI()?.Hide();

            if (skill.Data.TargetingType == SkillTargetingType.Self)
            {
                OpenSkillPreview(UnitReference, cellGrid, skill);
            }
        }

        private void BeginAreaSkillTargeting(Skill skill, CustomCellGrid cellGrid)
        {
            AllowManualCameraInputInPendingState = true;
            awaitingAreaSkillConfirmation = false;
            selectedAreaSkillCenterCell = null;
            ClearAreaSkillTargetingPreview();
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (actingCell != null)
            {
                UpdateAreaSkillProjection(actingCell, cellGrid);
            }
            FindAttackPreviewUI()?.Hide();
        }

        private void HandlePendingAreaSkillCellInteraction(Cell hoveredCell, CustomCellGrid cellGrid)
        {
            if (selectedTargetingSkill?.Data == null)
            {
                CancelSkillTargeting(cellGrid);
                return;
            }

            if (hoveredCell == null)
            {
                return;
            }

            UpdateAreaSkillProjection(hoveredCell, cellGrid);
            if (selectedAreaSkillCenterCell == null)
            {
                return;
            }

            var affectedTargets = GetAreaSkillTargets(selectedTargetingSkill, selectedAreaSkillCenterCell, cellGrid);
            if (affectedTargets.Count == 0)
            {
                return;
            }

            ShowAreaSkillConfirmPopup(cellGrid, selectedAreaSkillCenterCell, affectedTargets);
        }

        private void HandlePendingSkillUnitClicked(CustomUnit unit, CustomCellGrid cellGrid)
        {
            if (selectedTargetingSkill?.Data == null)
            {
                CancelSkillTargeting(cellGrid);
                return;
            }

            if (unit == UnitReference)
            {
                if (IsSkillPreviewOpen())
                {
                    CancelSkillPreview(cellGrid);
                }
                else
                {
                    CancelSkillTargeting(cellGrid);
                }

                return;
            }

            if (IsSkillPreviewOpen())
            {
                var preferredSkill = GetSelectedSkillPreviewEntry() ?? selectedTargetingSkill;

                if (unit == selectedSkillPreviewTarget)
                {
                    ConfirmSkillPreview(cellGrid);
                    return;
                }

                if (!CanUseSkillAgainstTarget(preferredSkill, unit, cellGrid))
                {
                    return;
                }

                OpenSkillPreview(unit, cellGrid, preferredSkill);
                return;
            }

            if (!CanSkillTargetUnit(selectedTargetingSkill, unit, cellGrid))
            {
                return;
            }

            OpenSkillPreview(unit, cellGrid, selectedTargetingSkill);
        }

        private void ShowInventoryMenu(CustomCellGrid cellGrid)
        {
            var inventoryMenuUi = FindInventoryMenuUI();
            if (inventoryMenuUi == null)
            {
                return;
            }

            showingInventoryMenu = true;
            awaitingAttackTargetSelection = false;
            awaitingSkillTargetSelection = false;
            awaitingTradeTargetSelection = false;
            awaitingAreaSkillConfirmation = false;
            selectedAttackPreviewTarget = null;
            selectedSkillPreviewTarget = null;
            selectedTargetingSkill = null;
            selectedAreaSkillCenterCell = null;
            attackPreviewWeaponOptions.Clear();
            skillPreviewOptions.Clear();
            attackPreviewWeaponIndex = -1;
            skillPreviewIndex = -1;
            ClearAttackTargetingPreview();
            ClearSkillTargetingPreview();
            ClearTradeTargetingPreview();
            FindActionMenuUI()?.Hide();
            FindAttackPreviewUI()?.Hide();
            FindSkillMenuUI()?.Hide();
            FindAreaConfirmUI()?.Hide();
            HideTradeMenu();

            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            Vector3 actionMenuWorldPosition =
                actingCell != null
                    ? actingCell.transform.position
                    : UnitReference.transform.position;

            inventoryMenuUi.Show(
                actionMenuWorldPosition,
                UnitReference,
                () =>
                {
                    ShowActionMenu(cellGrid);
                },
                () =>
                {
                    EndTurnAndCommitPendingMove(cellGrid);
                });
        }

        private void BeginTradeTargeting(CustomCellGrid cellGrid)
        {
            if (!UnitReference.HasPendingMove)
            {
                return;
            }

            if (GetTradeableFriendliesFromPreview(cellGrid).Count == 0)
            {
                return;
            }

            FindActionMenuUI()?.Hide();
            FindAttackPreviewUI()?.Hide();
            FindInventoryMenuUI()?.Hide();
            FindAreaConfirmUI()?.Hide();
            awaitingAttackTargetSelection = false;
            awaitingSkillTargetSelection = false;
            awaitingTradeTargetSelection = true;
            awaitingAreaSkillConfirmation = false;
            selectedAttackPreviewTarget = null;
            selectedSkillPreviewTarget = null;
            selectedTargetingSkill = null;
            selectedAreaSkillCenterCell = null;
            attackPreviewWeaponOptions.Clear();
            skillPreviewOptions.Clear();
            attackPreviewWeaponIndex = -1;
            skillPreviewIndex = -1;
            showingInventoryMenu = false;
            AllowManualCameraInputInPendingState = true;
            ClearAttackTargetingPreview();
            ClearSkillTargetingPreview();
            ShowTradePreviewCells(cellGrid);
        }

        private void CancelAttackTargeting(CustomCellGrid cellGrid)
        {
            awaitingAttackTargetSelection = false;
            ClearAttackTargetingPreview();
            ShowActionMenu(cellGrid);
        }

        private void CancelAttackPreview(CustomCellGrid cellGrid)
        {
            ClearAttackPreviewSelection();
            awaitingAttackTargetSelection = false;
            ClearAttackTargetingPreview();
            ShowActionMenu(cellGrid);
        }

        private void CancelTradeTargeting(CustomCellGrid cellGrid)
        {
            awaitingTradeTargetSelection = false;
            ClearTradeTargetingPreview();
            ShowActionMenu(cellGrid);
        }

        private void ShowAreaSkillConfirmPopup(CustomCellGrid cellGrid, Cell centerCell, IReadOnlyList<CustomUnit> affectedTargets)
        {
            if (selectedTargetingSkill?.Data == null || centerCell == null || affectedTargets == null || affectedTargets.Count == 0)
            {
                return;
            }

            awaitingAreaSkillConfirmation = true;
            AllowManualCameraInputInPendingState = false;
            selectedAreaSkillCenterCell = centerCell;
            pendingAreaSkillTargets.Clear();
            pendingAreaSkillTargets.AddRange(affectedTargets.Where(target => target != null));

            FindActionMenuUI()?.Hide();
            FindSkillMenuUI()?.Hide();
            FindAttackPreviewUI()?.Hide();

            var confirmUi = FindAreaConfirmUI();
            if (confirmUi == null)
            {
                StartCoroutine(ExecuteAreaSkillThenConfirmPendingMove(selectedTargetingSkill, selectedAreaSkillCenterCell, pendingAreaSkillTargets.ToList(), cellGrid));
                return;
            }

            confirmUi.Show(
                centerCell.transform.position,
                selectedTargetingSkill.Data.Name,
                selectedTargetingSkill.Data.Description,
                BuildAreaConfirmTargetPreviews(selectedTargetingSkill, selectedAreaSkillCenterCell, pendingAreaSkillTargets, cellGrid),
                () => ConfirmAreaSkill(cellGrid),
                () => CancelAreaSkillConfirmation(cellGrid));
        }

        private void ConfirmAreaSkill(CustomCellGrid cellGrid)
        {
            if (!awaitingAreaSkillConfirmation || selectedTargetingSkill?.Data == null || selectedAreaSkillCenterCell == null || pendingAreaSkillTargets.Count == 0)
            {
                return;
            }

            if (!CanUseSkillFromPreview(selectedTargetingSkill, cellGrid))
            {
                CancelAreaSkillConfirmation(cellGrid);
                return;
            }

            awaitingAreaSkillConfirmation = false;
            StartCoroutine(ExecuteAreaSkillThenConfirmPendingMove(selectedTargetingSkill, selectedAreaSkillCenterCell, pendingAreaSkillTargets.ToList(), cellGrid));
        }

        private void CancelAreaSkillConfirmation(CustomCellGrid cellGrid)
        {
            awaitingSkillTargetSelection = false;
            awaitingAreaSkillConfirmation = false;
            selectedTargetingSkill = null;
            selectedAreaSkillCenterCell = null;
            FindAreaConfirmUI()?.Hide();
            ClearSkillTargetingPreview();
            ShowActionMenu(cellGrid);
        }

        private void CancelPendingMoveAndRestoreSelection(CustomCellGrid cellGrid)
        {
            UnitReference.CancelPendingMove();
            GameplayCameraController.SetFocusedCell(UnitReference.Cell);
            FindActionMenuUI()?.Hide();
            FindAttackPreviewUI()?.Hide();
            FindInventoryMenuUI()?.Hide();
            FindSkillMenuUI()?.Hide();
            FindAreaConfirmUI()?.Hide();
            HideTradeMenu();
            awaitingAttackTargetSelection = false;
            awaitingSkillTargetSelection = false;
            awaitingTradeTargetSelection = false;
            selectedAttackPreviewTarget = null;
            selectedSkillPreviewTarget = null;
            selectedTargetingSkill = null;
            selectedAreaSkillCenterCell = null;
            awaitingAreaSkillConfirmation = false;
            attackPreviewWeaponOptions.Clear();
            skillPreviewOptions.Clear();
            attackPreviewWeaponIndex = -1;
            skillPreviewIndex = -1;
            showingInventoryMenu = false;
            ClearSkillTargetingPreview();
            ClearTradeTargetingPreview();

            cellGrid?.EnterSelectedState(UnitReference);

            OnAbilitySelected(cellGrid);
            Display(cellGrid);
        }

        private void ShowAttackPreviewCells(CustomCellGrid cellGrid)
        {
            ClearAttackPreviewCells();

            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (actingCell == null)
            {
                return;
            }

            foreach (var cell in ResolveGridCells(cellGrid))
            {
                if (cell == null)
                {
                    continue;
                }

                int distance = actingCell.GetDistance(cell);
                if (!GetAvailableAttackPreviewWeapons()
                    .Any(weapon => distance >= UnitReference.GetMinAttackRangeForWeapon(weapon)
                        && distance <= UnitReference.GetMaxAttackRangeForWeapon(weapon)))
                {
                    continue;
                }

                pendingAttackPreviewCells.Add(cell);
                MarkAttackPreviewCell(cell, cellGrid);
            }
        }

        private void ClearAttackPreviewCells()
        {
            Windy.Srpg.Game.Grid.CustomCellGrid cellGrid = FindSceneCellGrid();
            foreach (var cell in pendingAttackPreviewCells)
            {
                ClearAttackPreviewCellMark(cell, cellGrid);
            }

            pendingAttackPreviewCells.Clear();
        }

        private static void MarkAttackPreviewCell(Cell cell, CustomCellGrid cellGrid)
        {
            if (cell == null)
            {
                return;
            }

            if (ShouldUseRuntimeCellHighlighting(cellGrid))
            {
                GetRuntimeBoardCell(cell)?.ApplyHighlight(CellHighlightKind.Attack);
            }
            else if (cell is CustomSquare customSquare)
            {
                customSquare.MarkAsAttackPreview();
            }
        }

        private static void ClearAttackPreviewCellMark(Cell cell, CustomCellGrid cellGrid)
        {
            if (cell == null)
            {
                return;
            }

            if (ShouldUseRuntimeCellHighlighting(cellGrid))
            {
                GetRuntimeBoardCell(cell)?.ClearHighlight();
            }
            else
            {
                cell.UnMark();
            }
        }

        private Cell GetActingCellForPendingActions(CustomCellGrid cellGrid)
        {
            if (UnitReference == null)
            {
                return null;
            }

            // Framework preview is authoritative for pending-action menu queries. Runtime
            // PreviewCell can lag because legacy→runtime state mirroring may run before PreviewMove.
            Cell frameworkActingCell = UnitReference.PreviewCell ?? UnitReference.Cell;

            if (cellGrid == null || !cellGrid.ShouldRouteHumanMovementThroughRuntime)
            {
                return frameworkActingCell;
            }

            Cell runtimeActingCell = cellGrid.ResolveRuntimeActingCell(UnitReference);
            return runtimeActingCell == frameworkActingCell
                ? runtimeActingCell
                : frameworkActingCell;
        }

        private static CustomCellGrid FindSceneCellGrid()
        {
            return UnityEngine.Object.FindObjectOfType<CustomCellGrid>();
        }

        private void RefreshPendingAttackableEnemies(CustomCellGrid cellGrid)
        {
            ClearPendingAttackableEnemies();

            foreach (var enemyUnit in GetAttackableEnemiesFromPreview(cellGrid))
            {
                pendingAttackableEnemies.Add(enemyUnit);
            }

            if (selectedAttackPreviewTarget != null && pendingAttackableEnemies.Contains(selectedAttackPreviewTarget))
            {
                selectedAttackPreviewTarget.MarkAsDefending(UnitReference);
            }
        }

        private void ClearPendingAttackableEnemies()
        {
            foreach (var unit in pendingAttackableEnemies)
            {
                if (unit != null)
                {
                    unit.UnMark();
                }
            }
            pendingAttackableEnemies.Clear();
        }

        private void ClearAttackTargetingPreview()
        {
            ClearAttackPreviewSelection();
            ClearAttackPreviewCells();
            ClearPendingAttackableEnemies();
        }

        private void ShowSkillPreviewCells(Skill skill, CustomCellGrid cellGrid)
        {
            ClearSkillPreviewCells();

            if (skill?.Data == null || cellGrid == null)
            {
                return;
            }

            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (actingCell == null)
            {
                return;
            }

            SkillHighlightMode highlightMode = GetSkillHighlightMode(skill.Data);
            bool isInfiniteRange = TryResolveSkillRange(skill, null, actingCell, out _, out int maxRange)
                && IsInfiniteResolvedSkillRange(maxRange);
            foreach (var cell in ResolveGridCells(cellGrid))
            {
                if (cell == null)
                {
                    continue;
                }

                if (!isInfiniteRange && !CanSkillReachCellFromPreview(skill, cell, cellGrid))
                {
                    continue;
                }

                pendingSkillPreviewCells.Add(cell);
                MarkSkillPreviewCell(cell, highlightMode, cellGrid, isInfiniteRange);
            }
        }

        private void ClearSkillPreviewCells()
        {
            CustomCellGrid cellGrid = FindSceneCellGrid();
            foreach (var cell in pendingSkillPreviewCells)
            {
                ClearSkillPreviewCellMark(cell, cellGrid);
            }

            pendingSkillPreviewCells.Clear();
        }

        private static void MarkSkillPreviewCell(Cell cell, SkillHighlightMode highlightMode, CustomCellGrid cellGrid, bool faint = false)
        {
            if (cell == null)
            {
                return;
            }

            if (ShouldUseRuntimeCellHighlighting(cellGrid))
            {
                CellHighlightKind kind = highlightMode switch
                {
                    SkillHighlightMode.Enemy => CellHighlightKind.Attack,
                    SkillHighlightMode.Any => CellHighlightKind.Support,
                    _ => CellHighlightKind.Deployment
                };
                GetRuntimeBoardCell(cell)?.ApplyHighlight(kind);
            }
            else
            {
                ApplySkillPreviewHighlight(cell, highlightMode, faint);
            }
        }

        private static void ClearSkillPreviewCellMark(Cell cell, CustomCellGrid cellGrid)
        {
            if (cell == null)
            {
                return;
            }

            if (ShouldUseRuntimeCellHighlighting(cellGrid))
            {
                GetRuntimeBoardCell(cell)?.ClearHighlight();
            }
            else
            {
                cell.UnMark();
            }
        }

        private void RefreshPendingSkillTargets(Skill skill, CustomCellGrid cellGrid)
        {
            ClearPendingSkillTargets();

            foreach (var unit in GetValidSkillTargets(skill, cellGrid))
            {
                pendingSkillTargets.Add(unit);
                ApplySkillTargetHighlight(unit, skill.Data);
            }

            if (selectedSkillPreviewTarget != null && pendingSkillTargets.Contains(selectedSkillPreviewTarget))
            {
                selectedSkillPreviewTarget.MarkAsDefending(UnitReference);
            }
        }

        private void ClearPendingSkillTargets()
        {
            foreach (var unit in pendingSkillTargets)
            {
                if (unit != null)
                {
                    unit.UnMark();
                }
            }

            pendingSkillTargets.Clear();
        }

        private void ClearSkillTargetingPreview()
        {
            ClearSkillPreviewSelection();
            ClearSkillPreviewCells();
            ClearPendingSkillTargets();
            ClearAreaSkillTargetingPreview();
            FindSkillMenuUI()?.Hide();
        }

        private void ClearAreaSkillCenterCells()
        {
            foreach (var cell in pendingAreaSkillCenterCells)
            {
                if (cell is CustomSquare customSquare)
                {
                    customSquare.ClearPreviewBorder();
                }

                if (cell != null)
                {
                    cell.UnMark();
                }
            }

            pendingAreaSkillCenterCells.Clear();
        }

        private void ClearAreaSkillRadiusCells()
        {
            foreach (var cell in pendingAreaSkillRadiusCells)
            {
                if (cell != null)
                {
                    cell.UnMark();
                }
            }

            pendingAreaSkillRadiusCells.Clear();
        }

        private void ClearAreaSkillTargets()
        {
            foreach (var unit in pendingAreaSkillTargets)
            {
                if (unit != null)
                {
                    unit.UnMark();
                }
            }

            pendingAreaSkillTargets.Clear();
        }

        private void ClearAreaSkillTargetingPreview()
        {
            selectedAreaSkillCenterCell = null;
            ClearAreaSkillCenterCells();
            ClearAreaSkillRadiusCells();
            ClearAreaSkillTargets();
        }

        private static bool IsAreaSkill(Skill skill)
        {
            return skill?.Data != null && IsAreaSkill(skill.Data);
        }

        private static bool IsAreaSkill(SkillData data)
        {
            return data != null && (data.AreaProfile.Enabled || data.TargetingType == SkillTargetingType.AreaCell);
        }

        private static bool IsLineAreaSkill(Skill skill)
        {
            return skill?.Data != null && skill.Data.AreaProfile.Enabled && skill.Data.AreaProfile.Shape == SkillAreaShape.Line;
        }

        private static bool IsCenteredAreaSkill(Skill skill)
        {
            return skill?.Data != null && skill.Data.AreaProfile.Enabled && skill.Data.AreaProfile.Shape != SkillAreaShape.Line;
        }

        private bool IsAreaSkillUsableFromPreview(Skill skill, CustomCellGrid cellGrid)
        {
            if (skill == null
                || UnitReference == null
                || !UnitReference.CanUseSkill(skill)
                || !IsSkillSupported(skill.Data)
                || !IsSkillEffectImplemented(skill.Data))
            {
                return false;
            }

            if (IsLineAreaSkill(skill))
            {
                return HasAnyUsableLineAreaSkillProjection(skill, cellGrid);
            }

            return GetLegalAreaSkillCenterCells(skill, cellGrid).Any(center => GetAreaSkillTargets(skill, center, cellGrid).Count > 0);
        }

        private bool HasAnyUsableLineAreaSkillProjection(Skill skill, CustomCellGrid cellGrid)
        {
            if (skill?.Data == null || cellGrid == null || GetActingCellForPendingActions(cellGrid) == null)
            {
                return false;
            }

            foreach (Vector2Int direction in GetLineAreaDirections())
            {
                AreaLineProjection projection = BuildLineAreaProjection(skill, direction, cellGrid);
                if (projection.Cells.Count == 0)
                {
                    continue;
                }

                if (GetAreaSkillTargets(skill, projection.Endpoint, cellGrid).Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private List<Cell> GetLegalAreaSkillCenterCells(Skill skill, CustomCellGrid cellGrid)
        {
            var results = new List<Cell>();
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (skill?.Data == null || cellGrid == null || actingCell == null)
            {
                return results;
            }

            if (!TryResolveSkillRange(skill, null, actingCell, out int minRange, out int maxRange))
            {
                return results;
            }

            if (maxRange == 0)
            {
                results.Add(actingCell);
                return results;
            }

            bool isInfiniteRange = IsInfiniteResolvedSkillRange(maxRange);

            foreach (var cell in ResolveGridCells(cellGrid))
            {
                if (cell == null)
                {
                    continue;
                }

                int distance = actingCell.GetDistance(cell);
                if (distance < minRange || (!isInfiniteRange && distance > maxRange))
                {
                    continue;
                }

                results.Add(cell);
            }

            return results;
        }

        private Cell GetClosestLegalAreaSkillCenterCell(Skill skill, Cell hoveredCell, CustomCellGrid cellGrid)
        {
            if (hoveredCell == null)
            {
                return null;
            }

            var legalCenters = GetLegalAreaSkillCenterCells(skill, cellGrid);
            if (legalCenters.Count == 0)
            {
                return null;
            }

            if (legalCenters.Contains(hoveredCell))
            {
                return hoveredCell;
            }

            Vector3 hoveredPosition = hoveredCell.transform.position;
            return legalCenters
                .OrderBy(cell => (cell.transform.position - hoveredPosition).sqrMagnitude)
                .ThenBy(cell => cell.OffsetCoord.x)
                .ThenBy(cell => cell.OffsetCoord.y)
                .FirstOrDefault();
        }

        private static IEnumerable<Vector2Int> GetLineAreaDirections()
        {
            yield return new Vector2Int(1, 0);
            yield return new Vector2Int(0, 1);
            yield return new Vector2Int(-1, 0);
            yield return new Vector2Int(0, -1);
        }

        private Vector2Int ResolveLineAreaDirection(Cell hoveredCell, CustomCellGrid cellGrid)
        {
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (hoveredCell == null || actingCell == null)
            {
                return Vector2Int.zero;
            }

            Vector2 source = actingCell.OffsetCoord;
            Vector2 target = hoveredCell.OffsetCoord;
            int dx = Mathf.RoundToInt(target.x - source.x);
            int dy = Mathf.RoundToInt(target.y - source.y);

            if (dx == 0 && dy == 0)
            {
                return Vector2Int.zero;
            }

            if (Mathf.Abs(dx) >= Mathf.Abs(dy))
            {
                return new Vector2Int(dx > 0 ? 1 : -1, 0);
            }

            return new Vector2Int(0, dy > 0 ? 1 : -1);
        }

        private List<AreaBorderOutline> BuildAreaBorderOutlines(IEnumerable<Cell> cells, CustomCellGrid cellGrid)
        {
            var legalCenters = cells?
                .Where(cell => cell != null)
                .Distinct()
                .ToList() ?? new List<Cell>();
            var results = new List<AreaBorderOutline>();
            if (legalCenters.Count == 0)
            {
                return results;
            }

            var legalSet = new HashSet<Cell>(legalCenters);

            foreach (var cell in legalCenters)
            {
                if (cell == null)
                {
                    continue;
                }

                Vector2Int offset = new Vector2Int(
                    Mathf.RoundToInt(cell.OffsetCoord.x),
                    Mathf.RoundToInt(cell.OffsetCoord.y));

                bool top = !legalSet.Contains(FindCellByOffset(cellGrid, offset + Vector2Int.up));
                bool right = !legalSet.Contains(FindCellByOffset(cellGrid, offset + Vector2Int.right));
                bool bottom = !legalSet.Contains(FindCellByOffset(cellGrid, offset + Vector2Int.down));
                bool left = !legalSet.Contains(FindCellByOffset(cellGrid, offset + Vector2Int.left));

                if (top || right || bottom || left)
                {
                    results.Add(new AreaBorderOutline(cell, top, right, bottom, left));
                }
            }

            return results;
        }

        private List<AreaBorderOutline> GetAreaSkillCenterBorderOutlines(Skill skill, CustomCellGrid cellGrid)
        {
            return BuildAreaBorderOutlines(GetLegalAreaSkillCenterCells(skill, cellGrid), cellGrid);
        }

        private Dictionary<Vector2Int, Cell> BuildCellLookup(CustomCellGrid cellGrid)
        {
            return ResolveGridCells(cellGrid)
                .Where(cell => cell != null)
                .GroupBy(cell => new Vector2Int(Mathf.RoundToInt(cell.OffsetCoord.x), Mathf.RoundToInt(cell.OffsetCoord.y)))
                .ToDictionary(group => group.Key, group => group.First());
        }

        private AreaLineProjection BuildLineAreaProjection(Skill skill, Vector2Int direction, CustomCellGrid cellGrid)
        {
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (skill?.Data == null || cellGrid == null || actingCell == null || direction == Vector2Int.zero)
            {
                return new AreaLineProjection(direction, null, new List<Cell>());
            }

            if (!TryResolveSkillRange(skill, null, actingCell, out int minRange, out int maxRange))
            {
                return new AreaLineProjection(direction, null, new List<Cell>());
            }

            if (IsInfiniteResolvedSkillRange(maxRange))
            {
                maxRange = ResolveInfiniteLineRangeToGridEdge(direction, cellGrid);
            }

            if (maxRange <= 0)
            {
                return new AreaLineProjection(direction, null, new List<Cell>());
            }

            int startDistance = Mathf.Max(1, minRange);
            int endDistance = Mathf.Max(startDistance, maxRange);
            int halfWidth = Mathf.Max(0, skill.Data.AreaProfile.Radius);

            Vector2Int perpendicular = new Vector2Int(-direction.y, direction.x);
            Vector2Int sourceCoord = new Vector2Int(
                Mathf.RoundToInt(actingCell.OffsetCoord.x),
                Mathf.RoundToInt(actingCell.OffsetCoord.y));
            Dictionary<Vector2Int, Cell> lookup = BuildCellLookup(cellGrid);
            var cells = new List<Cell>();
            var added = new HashSet<Cell>();
            Cell endpoint = null;

            for (int distance = startDistance; distance <= endDistance; distance++)
            {
                Vector2Int centerCoord = sourceCoord + direction * distance;
                if (lookup.TryGetValue(centerCoord, out Cell centerCell))
                {
                    endpoint = centerCell;
                }

                for (int offset = -halfWidth; offset <= halfWidth; offset++)
                {
                    Vector2Int targetCoord = centerCoord + perpendicular * offset;
                    if (!lookup.TryGetValue(targetCoord, out Cell cell))
                    {
                        continue;
                    }

                    if (ShouldSkipAreaSkillOverlayCell(skill, cell, cellGrid))
                    {
                        continue;
                    }

                    if (added.Add(cell))
                    {
                        cells.Add(cell);
                    }
                }
            }

            return new AreaLineProjection(direction, endpoint, cells);
        }

        private int ResolveInfiniteLineRangeToGridEdge(Vector2Int direction, CustomCellGrid cellGrid)
        {
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (direction == Vector2Int.zero || cellGrid == null || actingCell == null)
            {
                return 0;
            }

            Vector2Int currentCoord = new Vector2Int(
                Mathf.RoundToInt(actingCell.OffsetCoord.x),
                Mathf.RoundToInt(actingCell.OffsetCoord.y));

            int steps = 0;
            while (true)
            {
                currentCoord += direction;
                if (FindCellByOffset(cellGrid, currentCoord) == null)
                {
                    return steps;
                }

                steps++;
            }
        }

        private List<CustomUnit> GetAreaSkillTargets(Skill skill, Cell centerCell, CustomCellGrid cellGrid)
        {
            var results = new List<CustomUnit>();
            if (skill?.Data == null || centerCell == null)
            {
                return results;
            }

            HashSet<Cell> affectedCells = GetAreaSkillAffectedCells(skill, centerCell, cellGrid);
            if (affectedCells.Count == 0)
            {
                return results;
            }

            bool affectsAllies = skill.Data.AreaProfile.AffectsAllies;
            bool affectsEnemies = skill.Data.AreaProfile.AffectsEnemies;
            bool isAnyTargetArea = affectsAllies && affectsEnemies;

            foreach (var unit in ResolveGridUnits(cellGrid))
            {
                if (unit == null || unit.HitPoints <= 0)
                {
                    continue;
                }

                Cell unitCell = unit.HasPendingMove ? unit.PreviewCell : unit.Cell;
                if (unitCell == null || !affectedCells.Contains(unitCell))
                {
                    continue;
                }

                if (skill.Data.SelfImmune && unit == UnitReference)
                {
                    continue;
                }

                bool isAlly = unit.PlayerNumber == UnitReference.PlayerNumber;
                if (!isAnyTargetArea)
                {
                    if (isAlly && !affectsAllies)
                    {
                        continue;
                    }

                    if (!isAlly && !affectsEnemies)
                    {
                        continue;
                    }
                }

                results.Add(unit);
            }

            if (results.Count == 0 || string.IsNullOrWhiteSpace(skill.Data.EffectId))
            {
                return results;
            }

            var filteredResults = new List<CustomUnit>(results.Count);
            foreach (var unit in results)
            {
                SkillContext context = BuildAreaSkillContext(skill, centerCell, unit, cellGrid, results);
                if (SkillEffectRegistry.TryCreate(skill.Data.EffectId, out ISkillEffect effect) && effect.CanUse(UnitReference, context))
                {
                    filteredResults.Add(unit);
                }
            }

            return filteredResults;
        }

        private HashSet<Cell> GetAreaSkillAffectedCells(Skill skill, Cell centerCell, CustomCellGrid cellGrid)
        {
            var results = new HashSet<Cell>();
            if (skill?.Data == null || centerCell == null || cellGrid == null)
            {
                return results;
            }

            if (IsLineAreaSkill(skill))
            {
                Vector2Int direction = ResolveLineAreaDirection(centerCell, cellGrid);
                AreaLineProjection projection = BuildLineAreaProjection(skill, direction, cellGrid);
                foreach (var cell in projection.Cells)
                {
                    if (cell != null)
                    {
                        results.Add(cell);
                    }
                }

                return results;
            }

            int radius = Mathf.Max(0, skill.Data.AreaProfile.Radius);
            foreach (var cell in ResolveGridCells(cellGrid))
            {
                if (cell == null || centerCell.GetDistance(cell) > radius || ShouldSkipAreaSkillOverlayCell(skill, cell, cellGrid))
                {
                    continue;
                }

                results.Add(cell);
            }

            return results;
        }

        private SkillContext BuildAreaSkillContext(Skill skill, Cell centerCell, CustomUnit primaryTarget, CustomCellGrid cellGrid, IReadOnlyList<CustomUnit> areaTargets = null)
        {
            return new SkillContext
            {
                User = UnitReference,
                PrimaryTargetUnit = primaryTarget,
                TargetCell = centerCell,
                CellGrid = cellGrid,
                AreaTargets = areaTargets,
                Skill = skill?.Data
            };
        }

        private bool ShouldSkipAreaSkillOverlayCell(Skill skill, Cell cell, CustomCellGrid cellGrid)
        {
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            return skill?.Data != null
                && skill.Data.SelfImmune
                && actingCell != null
                && cell == actingCell;
        }

        private void UpdateAreaSkillProjection(Cell hoveredCell, CustomCellGrid cellGrid)
        {
            if (!IsAreaSkill(selectedTargetingSkill) || hoveredCell == null || cellGrid == null)
            {
                return;
            }

            Cell snappedCenter = IsLineAreaSkill(selectedTargetingSkill)
                ? hoveredCell
                : GetClosestLegalAreaSkillCenterCell(selectedTargetingSkill, hoveredCell, cellGrid);
            if (snappedCenter == null)
            {
                return;
            }

            if (IsLineAreaSkill(selectedTargetingSkill) && ResolveLineAreaDirection(snappedCenter, cellGrid) == Vector2Int.zero)
            {
                ClearAreaSkillTargetingPreview();
                return;
            }

            selectedAreaSkillCenterCell = snappedCenter;
            ClearAreaSkillTargetingPreview();
            selectedAreaSkillCenterCell = snappedCenter;

            SkillData data = selectedTargetingSkill.Data;
            SkillHighlightMode highlightMode = GetSkillHighlightMode(data);
            bool showCenterRangeCells = false;
            bool showSelfCenteredRadiusBorder = false;
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (!IsLineAreaSkill(selectedTargetingSkill)
                && actingCell != null
                && TryResolveSkillRange(selectedTargetingSkill, null, actingCell, out _, out int maxRange)
                && !IsInfiniteResolvedSkillRange(maxRange))
            {
                showCenterRangeCells = maxRange > 0;
                showSelfCenteredRadiusBorder = maxRange == 0;
            }

            if (showCenterRangeCells)
            {
                foreach (var outline in GetAreaSkillCenterBorderOutlines(selectedTargetingSkill, cellGrid))
                {
                    if (outline.Cell == null)
                    {
                        continue;
                    }

                    pendingAreaSkillCenterCells.Add(outline.Cell);
                    ApplyAreaCenterBorderPreview(outline, highlightMode);
                }
            }

            HashSet<Cell> affectedCells = GetAreaSkillAffectedCells(selectedTargetingSkill, snappedCenter, cellGrid);
            if (showSelfCenteredRadiusBorder)
            {
                foreach (var outline in BuildAreaBorderOutlines(affectedCells, cellGrid))
                {
                    if (outline.Cell == null)
                    {
                        continue;
                    }

                    pendingAreaSkillCenterCells.Add(outline.Cell);
                    ApplyAreaCenterBorderPreview(outline, highlightMode);
                }
            }

            foreach (var cell in affectedCells)
            {
                if (cell == null)
                {
                    continue;
                }

                pendingAreaSkillRadiusCells.Add(cell);
                ApplySkillPreviewHighlight(cell, highlightMode);
            }

            foreach (var unit in GetAreaSkillTargets(selectedTargetingSkill, snappedCenter, cellGrid))
            {
                pendingAreaSkillTargets.Add(unit);
                ApplySkillTargetHighlight(unit, data);
            }
        }

        private void ShowTradePreviewCells(CustomCellGrid cellGrid)
        {
            ClearTradePreviewCells();

            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (actingCell == null || cellGrid == null)
            {
                return;
            }

            foreach (var cell in ResolveGridCells(cellGrid))
            {
                if (cell == null || actingCell.GetDistance(cell) != 1)
                {
                    continue;
                }

                pendingTradePreviewCells.Add(cell);
                MarkTradePreviewCell(cell, cellGrid);
            }
        }

        private void ClearTradePreviewCells()
        {
            CustomCellGrid cellGrid = FindSceneCellGrid();
            foreach (var cell in pendingTradePreviewCells)
            {
                ClearTradePreviewCellMark(cell, cellGrid);
            }

            pendingTradePreviewCells.Clear();
        }

        private static void MarkTradePreviewCell(Cell cell, CustomCellGrid cellGrid)
        {
            if (cell == null)
            {
                return;
            }

            if (ShouldUseRuntimeCellHighlighting(cellGrid))
            {
                GetRuntimeBoardCell(cell)?.ApplyHighlight(CellHighlightKind.Deployment);
            }
            else if (cell is CustomSquare customSquare)
            {
                customSquare.MarkAsTradePreview();
            }
        }

        private static void ClearTradePreviewCellMark(Cell cell, CustomCellGrid cellGrid)
        {
            if (cell == null)
            {
                return;
            }

            if (ShouldUseRuntimeCellHighlighting(cellGrid))
            {
                GetRuntimeBoardCell(cell)?.ClearHighlight();
            }
            else
            {
                cell.UnMark();
            }
        }

        private void ClearTradeTargetingPreview()
        {
            ClearTradePreviewCells();
            HideTradeMenu();
        }

        private List<CustomUnit> GetAttackableEnemiesFromPreview(CustomCellGrid cellGrid)
        {
            if (cellGrid == null)
            {
                return new List<CustomUnit>();
            }

            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            return cellGrid.GetAttackableEnemiesFromActingCell(UnitReference, actingCell);
        }

        private bool IsUnitTradeableFromPreview(CustomUnit target, CustomCellGrid cellGrid)
        {
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (target == null || target == UnitReference || target.Cell == null || actingCell == null)
            {
                return false;
            }

            return target.PlayerNumber == UnitReference.PlayerNumber
                && actingCell.GetDistance(target.Cell) == 1;
        }

        private bool IsUnitTradeableFromPreview(CustomUnit target)
        {
            return IsUnitTradeableFromPreview(target, FindSceneCellGrid());
        }

        private List<CustomUnit> GetTradeableFriendliesFromPreview(CustomCellGrid cellGrid)
        {
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (cellGrid == null || actingCell == null)
            {
                return new List<CustomUnit>();
            }

            return cellGrid.GetCurrentPlayerCustomUnits()
                .Where(unit => unit != null
                    && unit != UnitReference
                    && unit.Cell != null
                    && actingCell.GetDistance(unit.Cell) == 1)
                .OrderBy(unit => unit.UnitID)
                .ToList();
        }

        private CustomUnit GetTradePartnerFromPreview(CustomCellGrid cellGrid)
        {
            return GetTradeableFriendliesFromPreview(cellGrid).FirstOrDefault();
        }

        private void OpenTradeMenuForUnit(CustomUnit tradePartner, CustomCellGrid cellGrid)
        {
            var tradeMenuUi = FindTradeMenuUI();
            if (tradeMenuUi == null)
            {
                awaitingTradeTargetSelection = false;
                ShowActionMenu(cellGrid);
                return;
            }

            awaitingTradeTargetSelection = false;
            ClearTradePreviewCells();
            FindActionMenuUI()?.Hide();
            FindAttackPreviewUI()?.Hide();
            FindInventoryMenuUI()?.Hide();
            HideTradeMenu();
            EnterPendingMenuBlockedInput(cellGrid);

            tradeMenuUi.Show(
                tradePartner.transform.position,
                UnitReference,
                tradePartner,
                didTrade =>
                {
                    showingTradeMenu = false;
                    if (didTrade)
                    {
                        cellGrid?.EnterSelectedState(UnitReference);
                        return;
                    }

                    cellGrid?.EnterPendingMoveConfirmState(this);
                    ShowActionMenu(cellGrid);
                },
                () =>
                {
                    CommitPendingMoveFromPendingAction(cellGrid, consumeAllRemainingMovement: false);
                });
            showingTradeMenu = true;
        }

        private bool IsAttackPreviewOpen()
        {
            return selectedAttackPreviewTarget != null
                && attackPreviewWeaponIndex >= 0
                && attackPreviewWeaponIndex < attackPreviewWeaponOptions.Count;
        }

        private IEnumerable<WeaponData> GetAvailableAttackPreviewWeapons()
        {
            return UnitReference.GetWeaponInventoryEntries()
                .Select(entry => entry?.Weapon)
                .Where(weapon => weapon != null);
        }

        private bool CanAttackTargetWithAnyWeaponFromPreview(CustomUnit target, CustomCellGrid cellGrid)
        {
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            return target != null
                && actingCell != null
                && UnitReference.CanAttackTargetWithAnyWeapon(target, actingCell);
        }

        private bool CanAttackTargetWithAnyWeaponFromPreview(CustomUnit target)
        {
            return CanAttackTargetWithAnyWeaponFromPreview(target, FindSceneCellGrid());
        }

        private void OpenAttackPreview(CustomUnit target, CustomCellGrid cellGrid)
        {
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (target == null || actingCell == null)
            {
                return;
            }

            var legalWeapons = UnitReference.GetWeaponsThatCanAttack(target, actingCell).ToList();
            if (legalWeapons.Count == 0)
            {
                return;
            }

            selectedAttackPreviewTarget = target;
            attackPreviewWeaponOptions.Clear();
            attackPreviewWeaponOptions.AddRange(legalWeapons);

            var equippedWeaponEntry = UnitReference.Inventory?.EquippedWeaponEntry;
            attackPreviewWeaponIndex = equippedWeaponEntry != null
                ? attackPreviewWeaponOptions.FindIndex(entry => entry == equippedWeaponEntry)
                : -1;

            if (attackPreviewWeaponIndex < 0)
            {
                attackPreviewWeaponIndex = 0;
            }

            ShowAttackPreview(target, cellGrid);
            RefreshPendingAttackableEnemies(cellGrid);
        }

        private void ConfirmAttackPreview(CustomCellGrid cellGrid)
        {
            if (!IsAttackPreviewOpen() || selectedAttackPreviewTarget == null)
            {
                return;
            }

            var previewWeaponEntry = GetSelectedAttackPreviewWeaponEntry();
            if (previewWeaponEntry == null)
            {
                return;
            }

            UnitReference.EquipWeapon(previewWeaponEntry);
            StartCoroutine(AttackThenConfirmPendingMove(selectedAttackPreviewTarget, cellGrid));
        }

        private void CycleAttackPreviewWeapon(CustomCellGrid cellGrid)
        {
            if (!IsAttackPreviewOpen() || attackPreviewWeaponOptions.Count <= 1)
            {
                return;
            }

            attackPreviewWeaponIndex = (attackPreviewWeaponIndex + 1) % attackPreviewWeaponOptions.Count;
            ShowAttackPreview(selectedAttackPreviewTarget, cellGrid);
            RefreshPendingAttackableEnemies(cellGrid);
        }

        private Item GetSelectedAttackPreviewWeaponEntry()
        {
            if (attackPreviewWeaponIndex < 0 || attackPreviewWeaponIndex >= attackPreviewWeaponOptions.Count)
            {
                return null;
            }

            return attackPreviewWeaponOptions[attackPreviewWeaponIndex];
        }

        private void ClearAttackPreviewSelection()
        {
            selectedAttackPreviewTarget = null;
            attackPreviewWeaponOptions.Clear();
            attackPreviewWeaponIndex = -1;
            FindAttackPreviewUI()?.Hide();
        }

        private bool IsSkillPreviewOpen()
        {
            return selectedSkillPreviewTarget != null
                && GetSelectedSkillPreviewEntry()?.Data != null;
        }

        private Skill GetSelectedSkillPreviewEntry()
        {
            if (selectedTargetingSkill?.Data != null)
            {
                return selectedTargetingSkill;
            }

            if (skillPreviewIndex < 0 || skillPreviewIndex >= skillPreviewOptions.Count)
            {
                return null;
            }

            return skillPreviewOptions[skillPreviewIndex];
        }

        private void ClearSkillPreviewSelection()
        {
            selectedSkillPreviewTarget = null;
            skillPreviewOptions.Clear();
            skillPreviewIndex = -1;
            selectedSkillPreviewWeaponEntry = null;
            FindAttackPreviewUI()?.Hide();
        }

        private void OpenSkillPreview(CustomUnit target, CustomCellGrid cellGrid, Skill preferredSkill)
        {
            if (target == null)
            {
                return;
            }

            Skill skillToPreview = preferredSkill ?? selectedTargetingSkill;
            if (!CanUseSkillAgainstTarget(skillToPreview, target, cellGrid))
            {
                return;
            }

            selectedSkillPreviewTarget = target;
            skillPreviewOptions.Clear();
            skillPreviewOptions.Add(skillToPreview);
            skillPreviewIndex = 0;
            selectedTargetingSkill = skillToPreview;
            selectedSkillPreviewWeaponEntry = ResolveSkillPreviewWeaponForCurrentSelection(target, cellGrid);
            ShowSkillPreview(target, cellGrid);
            RefreshPendingSkillTargets(selectedTargetingSkill, cellGrid);
        }

        private void ShowSkillPreview(CustomUnit target, CustomCellGrid cellGrid)
        {
            AllowManualCameraInputInPendingState = false;
            var previewUi = FindAttackPreviewUI();
            var skill = GetSelectedSkillPreviewEntry();
            if (previewUi == null || target == null || skill?.Data == null)
            {
                return;
            }

            selectedSkillPreviewWeaponEntry = ResolveSkillPreviewWeaponForCurrentSelection(target, cellGrid);
            UnitInspectPanelUI.RequestGameplayHide();

            previewUi.Show(
                target.transform.position,
                UnitReference,
                target,
                BuildSkillAttackerPreview(skill, target, cellGrid),
                BuildSkillDefenderPreview(skill, target, cellGrid),
                "Skill",
                GetSkillActionDisplayName(skill, target, cellGrid),
                "Weapon",
                GetDefenderPreviewWeaponDisplayName(target),
                CanCycleSkillPreviewWeapon(target, cellGrid),
                () => CycleSkillPreviewWeapon(cellGrid),
                () => ConfirmSkillPreview(cellGrid),
                () => CancelSkillPreview(cellGrid));
        }

        private void ConfirmSkillPreview(CustomCellGrid cellGrid)
        {
            var skill = GetSelectedSkillPreviewEntry();
            if (!IsSkillPreviewOpen() || skill?.Data == null || selectedSkillPreviewTarget == null)
            {
                return;
            }

            StartCoroutine(ExecuteSkillThenConfirmPendingMove(skill, selectedSkillPreviewTarget, cellGrid));
        }

        private void CycleSkillPreviewWeapon(CustomCellGrid cellGrid)
        {
            if (!IsSkillPreviewOpen() || selectedSkillPreviewTarget == null)
            {
                return;
            }

            Skill skill = GetSelectedSkillPreviewEntry();
            var legalWeapons = GetSkillPreviewWeaponOptions(skill, selectedSkillPreviewTarget, cellGrid);
            if (legalWeapons.Count <= 1)
            {
                return;
            }

            int currentIndex = selectedSkillPreviewWeaponEntry != null
                ? legalWeapons.FindIndex(entry => entry == selectedSkillPreviewWeaponEntry)
                : -1;
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            selectedSkillPreviewWeaponEntry = legalWeapons[(currentIndex + 1) % legalWeapons.Count];
            ShowSkillPreview(selectedSkillPreviewTarget, cellGrid);
            RefreshPendingSkillTargets(skill, cellGrid);
        }

        private void CancelSkillPreview(CustomCellGrid cellGrid)
        {
            ClearSkillPreviewSelection();
            awaitingSkillTargetSelection = false;
            selectedTargetingSkill = null;
            selectedSkillPreviewWeaponEntry = null;
            ClearSkillTargetingPreview();
            ShowActionMenu(cellGrid);
        }

        private void CancelSkillTargeting(CustomCellGrid cellGrid)
        {
            awaitingSkillTargetSelection = false;
            awaitingAreaSkillConfirmation = false;
            selectedTargetingSkill = null;
            selectedAreaSkillCenterCell = null;
            selectedSkillPreviewWeaponEntry = null;
            ClearSkillTargetingPreview();
            ShowActionMenu(cellGrid);
        }

        private IEnumerator ExecuteSkillThenConfirmPendingMove(Skill skill, CustomUnit target, CustomCellGrid cellGrid)
        {
            if (skill?.Data == null || target == null)
            {
                yield break;
            }

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
                showingInventoryMenu = false;
                ClearAttackTargetingPreview();
                ClearSkillTargetingPreview();
                ClearTradeTargetingPreview();
                BeginPendingCombatPresentation(cellGrid);

                if (UnitReference == null)
                {
                    yield break;
                }

                Item backingWeaponEntry = GetPreferredWeaponForSkill(skill, target, GetActingCellForPendingActions(cellGrid), selectedSkillPreviewWeaponEntry);
                if (backingWeaponEntry != null)
                {
                    UnitReference.EquipWeapon(backingWeaponEntry);
                }

                bool executed = false;
                SkillContext context = BuildSkillContext(skill, target, cellGrid);
                if (TryBuildSkillAttackProfile(skill, target, backingWeaponEntry, out ResolvedAttackProfile attackProfile))
                {
                    if (TryPrepareAttackSkillEffect(skill, context, ref attackProfile, out ISkillEffect effect)
                        && UnitReference.MarkSkillUsed(skill))
                    {
                        effect?.Use(UnitReference, context);
                        UnitReference.AttackHandler(target, attackProfile);
                        executed = true;
                        yield return new WaitUntil(() => UnitReference == null || !UnitReference.IsAttackSequenceRunning);
                    }
                }
                else
                {
                    bool unexpectedlyUsingSupportPath =
                        skill?.Data != null
                        && skill.Data.AttackProfile.Enabled
                        && target != null
                        && (skill.Data.TargetingType == SkillTargetingType.EnemyUnit
                            || (skill.Data.TargetingType == SkillTargetingType.AnyUnit && target.PlayerNumber != UnitReference.PlayerNumber));

                    if (unexpectedlyUsingSupportPath)
                    {
                        Debug.LogWarning(
                            $"[Skill] Offensive single-target skill '{skill.Data.Name}' fell back to support-skill execution. " +
                            $"(skillId={skill.Data.Id}, user={UnitReference?.unitName}, target={target.unitName})");
                    }

                    if (SkillEffectRegistry.TryCreate(skill.Data.EffectId, out ISkillEffect effect)
                        && effect.CanUse(UnitReference, context)
                        && UnitReference.MarkSkillUsed(skill))
                    {
                        if (UnitReference.HasPendingMove)
                        {
                            CommitPendingMoveFromPendingAction(cellGrid, consumeAllRemainingMovement: false);
                        }

                        UnitReference.UseSupportSkill(
                            target,
                            skill.Data.EndsTurn,
                            () => effect.Use(UnitReference, context),
                            skill.Data,
                            cellGrid);
                        executed = true;
                        yield return new WaitUntil(() => UnitReference == null || !UnitReference.IsAttackSequenceRunning);
                    }
                }

                if (executed)
                {
                    CommitPendingMoveAfterCombatPresentation(cellGrid);
                }
            }
            finally
            {
                CompletePendingActionResolution(cellGrid);
            }
        }

        private IEnumerator ExecuteAreaSkillThenConfirmPendingMove(Skill skill, Cell centerCell, IReadOnlyList<CustomUnit> affectedTargets, CustomCellGrid cellGrid)
        {
            if (skill?.Data == null || centerCell == null || affectedTargets == null || affectedTargets.Count == 0)
            {
                yield break;
            }

            resolvingPendingAttack = true;
            bool executed = false;
            try
            {
                FindActionMenuUI()?.Hide();
                FindAttackPreviewUI()?.Hide();
                FindInventoryMenuUI()?.Hide();
                FindSkillMenuUI()?.Hide();
                HideTradeMenu();
                awaitingAttackTargetSelection = false;
                awaitingSkillTargetSelection = false;
                awaitingTradeTargetSelection = false;
                showingInventoryMenu = false;
                ClearAttackTargetingPreview();
                ClearSkillTargetingPreview();
                ClearTradeTargetingPreview();
                BeginPendingCombatPresentation(cellGrid);

                if (UnitReference == null)
                {
                    yield break;
                }

                IReadOnlyList<CustomUnit> orderedTargets = affectedTargets
                    .Where(target => target != null)
                    .Distinct()
                    .OrderBy(target => target.PlayerNumber == UnitReference.PlayerNumber ? 0 : 1)
                    .ThenBy(target => target.UnitID)
                    .ToList();

                if (skill.Data.AttackProfile.Enabled)
                {
                    if (TryBuildSkillAttackProfile(skill, orderedTargets.FirstOrDefault(), null, out ResolvedAttackProfile profile))
                    {
                        SkillContext castContext = BuildAreaSkillContext(skill, centerCell, orderedTargets.FirstOrDefault(), cellGrid, orderedTargets);
                        if (TryPrepareAttackSkillEffect(skill, castContext, ref profile, out ISkillEffect effect)
                            && UnitReference.MarkSkillUsed(skill))
                        {
                            if (UnitReference.HasPendingMove)
                            {
                                CommitPendingMoveFromPendingAction(cellGrid, consumeAllRemainingMovement: false);
                            }

                            effect?.Use(UnitReference, castContext);

                            UnitReference.UseAreaSkill(
                                orderedTargets,
                                skill.Data.EndsTurn,
                                target =>
                                {
                                    if (target == null || target.HitPoints <= 0)
                                    {
                                        return;
                                    }

                                    target.DefendHandler(
                                        UnitReference,
                                        profile.Damage,
                                        GetGuaranteedAreaSkillHitChance(),
                                        0,
                                        isMagicAttack: profile.IsMagic,
                                        isCounterAttack: false,
                                        simulateOnly: false);
                                },
                                skill.Data,
                                cellGrid);
                            executed = true;
                            yield return new WaitUntil(() => UnitReference == null || !UnitReference.IsAttackSequenceRunning);
                        }
                    }
                }
                else if (SkillEffectRegistry.TryCreate(skill.Data.EffectId, out ISkillEffect effect))
                {
                    bool canExecute = orderedTargets.Any(target => effect.CanUse(UnitReference, BuildAreaSkillContext(skill, centerCell, target, cellGrid, orderedTargets)));
                    if (canExecute && UnitReference.MarkSkillUsed(skill))
                    {
                        if (UnitReference.HasPendingMove)
                        {
                            CommitPendingMoveFromPendingAction(cellGrid, consumeAllRemainingMovement: false);
                        }

                        UnitReference.UseAreaSkill(
                            orderedTargets,
                            skill.Data.EndsTurn,
                            target =>
                            {
                                SkillContext targetContext = BuildAreaSkillContext(skill, centerCell, target, cellGrid, orderedTargets);
                                if (effect.CanUse(UnitReference, targetContext))
                                {
                                    effect.Use(UnitReference, targetContext);
                                }
                            },
                            skill.Data,
                            cellGrid);
                        executed = true;
                        yield return new WaitUntil(() => UnitReference == null || !UnitReference.IsAttackSequenceRunning);
                    }
                }
            }
            finally
            {
                resolvingPendingAttack = false;
                if (executed)
                {
                    cellGrid?.EnterPostCombatGridState();
                }
                else if (cellGrid != null && !cellGrid.GameFinished)
                {
                    cellGrid.EnterPendingMoveConfirmState(this);
                    ShowActionMenu(cellGrid);
                }
            }
        }

        private void ShowAttackPreview(CustomUnit target, CustomCellGrid cellGrid)
        {
            AllowManualCameraInputInPendingState = false;
            var previewUi = FindAttackPreviewUI();
            var previewWeaponEntry = GetSelectedAttackPreviewWeaponEntry();
            if (previewUi == null || target == null || previewWeaponEntry?.Weapon == null)
            {
                return;
            }

            UnitInspectPanelUI.RequestGameplayHide();
            previewUi.Show(
                target.transform.position,
                UnitReference,
                target,
                BuildAttackerPreview(target, previewWeaponEntry.Weapon),
                BuildDefenderPreview(target, previewWeaponEntry.Weapon),
                "Weapon",
                previewWeaponEntry.Weapon.Name,
                "Weapon",
                GetDefenderPreviewWeaponDisplayName(target),
                attackPreviewWeaponOptions.Count > 1,
                () => CycleAttackPreviewWeapon(cellGrid),
                () => ConfirmAttackPreview(cellGrid),
                () => CancelAttackPreview(cellGrid));
        }

        private AttackPreviewPanelData BuildAttackerPreview(CustomUnit defender, WeaponData weapon)
        {
            int attackMultiplier = Mathf.Max(1, UnitReference.GetNumHitsForWeapon(weapon));
            if (UnitReference.CanPursuitAttackAgainst(defender, weapon))
            {
                attackMultiplier *= 2;
            }

            int perHitDamage = CalculatePerHitDamage(UnitReference, defender, weapon);
            bool isFatal = CalculateProjectedDamage(UnitReference, defender, attackMultiplier, weapon) >= defender.HitPoints;

            return new AttackPreviewPanelData(
                UnitReference.unitName,
                FormatHitPointsDisplay(UnitReference),
                BuildMitigationValue(UnitReference, defender != null && defender.HasUsableWeapon, defender != null && defender.IsMagic),
                FormatDamageValue(perHitDamage, attackMultiplier, isFatal),
                FormatPercentValue(CalculateHitChance(UnitReference, defender, weapon)),
                FormatCritValue(UnitReference, defender, perHitDamage, weapon));
        }

        private AttackPreviewPanelData BuildDefenderPreview(CustomUnit defender, WeaponData incomingWeapon)
        {
            bool hasIncomingAttack = incomingWeapon != null;
            bool incomingIsMagic = incomingWeapon != null
                ? UnitReference.GetIsMagicForWeapon(incomingWeapon)
                : UnitReference.IsMagic;
            bool defenderCanCounter = defender.CanCounterAttackAgainst(UnitReference, incomingWeapon != null && incomingWeapon.PreventsCounterattack);
            if (!defenderCanCounter)
            {
                return new AttackPreviewPanelData(
                    defender.unitName,
                    FormatHitPointsDisplay(defender),
                    BuildMitigationValue(defender, hasIncomingAttack, incomingIsMagic),
                    "-",
                    "-",
                    "-");
            }

            int attackMultiplier = Mathf.Max(1, defender.NumHits);
            int perHitDamage = CalculatePerHitDamage(defender, UnitReference);
            bool isFatal = CalculateProjectedDamage(defender, UnitReference, attackMultiplier) >= UnitReference.HitPoints;

            return new AttackPreviewPanelData(
                defender.unitName,
                FormatHitPointsDisplay(defender),
                BuildMitigationValue(defender, hasIncomingAttack, incomingIsMagic),
                FormatDamageValue(perHitDamage, attackMultiplier, isFatal),
                FormatPercentValue(CalculateHitChance(defender, UnitReference)),
                FormatCritValue(defender, UnitReference, perHitDamage));
        }

        private static string BuildMitigationValue(CustomUnit unit, bool hasIncomingAttack, bool incomingIsMagic)
        {
            if (unit == null || !hasIncomingAttack)
            {
                return "Def/Res: -";
            }

            return incomingIsMagic
                ? $"Res: {unit.Resistance}"
                : $"Def: {unit.Defense}";
        }

        private static int CalculateAttackValue(CustomUnit attacker, WeaponData weapon = null)
        {
            if (attacker == null)
            {
                return 0;
            }

            return weapon != null ? attacker.GetAttackForWeapon(weapon) : attacker.Attack;
        }

        private static int CalculatePerHitDamage(CustomUnit attacker, CustomUnit defender, WeaponData weapon = null)
        {
            if (attacker == null || defender == null)
            {
                return 0;
            }

            bool isMagicAttack = weapon != null ? attacker.GetIsMagicForWeapon(weapon) : attacker.IsMagic;
            int attackValue = weapon != null ? attacker.GetAttackForWeapon(weapon) : attacker.Attack;
            int defenseStat = isMagicAttack ? defender.Resistance : defender.Defense;
            return Mathf.Max(1, attackValue - defenseStat);
        }

        private static int CalculatePerHitCritDamage(CustomUnit attacker, CustomUnit defender, WeaponData weapon = null)
        {
            if (attacker == null || defender == null)
            {
                return 0;
            }

            bool isMagicAttack = weapon != null ? attacker.GetIsMagicForWeapon(weapon) : attacker.IsMagic;
            int attackValue = weapon != null ? attacker.GetAttackForWeapon(weapon) : attacker.Attack;
            int defenseStat = isMagicAttack ? defender.Resistance : defender.Defense;
            return Mathf.Max(1, attackValue * 2 - defenseStat);
        }

        private static int CalculateProjectedDamage(CustomUnit attacker, CustomUnit defender, int multiplier, WeaponData weapon = null)
        {
            return CalculatePerHitDamage(attacker, defender, weapon) * Mathf.Max(1, multiplier);
        }

        private static int CalculateProjectedCritDamage(CustomUnit attacker, CustomUnit defender, int multiplier, WeaponData weapon = null)
        {
            return CalculatePerHitCritDamage(attacker, defender, weapon) * Mathf.Max(1, multiplier);
        }

        private static int CalculateHitChance(CustomUnit attacker, CustomUnit defender, WeaponData weapon = null)
        {
            if (attacker == null || defender == null)
            {
                return 0;
            }

            int accuracy = weapon != null ? attacker.GetAccuracyForWeapon(weapon) : attacker.Accuracy;
            return Mathf.Clamp(accuracy - defender.Evade, 0, 100);
        }

        private static int CalculateCritChance(CustomUnit attacker, CustomUnit defender, WeaponData weapon = null)
        {
            if (attacker == null || defender == null)
            {
                return 0;
            }

            int crit = weapon != null ? attacker.GetCritForWeapon(weapon) : attacker.Crit;
            return Mathf.Clamp(crit - defender.CritAvoid, 0, 100);
        }

        private static string FormatDamageValue(int damageValue, int multiplier, bool isFatal)
        {
            string result;
            if (multiplier > 1)
            {
                result = $"{damageValue} x{multiplier}";
            }
            else
            {
                result = damageValue.ToString();
            }

            if (isFatal)
            {
                result += " <color=#ff4040>(Fatal!)</color>";
            }

            return result;
        }

        private static string FormatPercentValue(int value)
        {
            return $"{value}%";
        }

        private static string FormatCritValue(CustomUnit attacker, CustomUnit defender, int normalDamage, WeaponData weapon = null)
        {
            if (attacker == null || defender == null)
            {
                return "-";
            }

            int critChance = CalculateCritChance(attacker, defender, weapon);
            int critDamage = CalculatePerHitCritDamage(attacker, defender, weapon);
            return $"{FormatPercentValue(critChance)} ({normalDamage} -> {critDamage})";
        }

        private List<Skill> GetAllKnownSkills()
        {
            return UnitReference?.SkillList?.Entries?
                .Where(skill => skill?.Data != null)
                .ToList() ?? new List<Skill>();
        }

        private static bool IsHealingSkill(Skill skill)
        {
            if (skill?.Data == null || string.IsNullOrWhiteSpace(skill.Data.EffectId))
            {
                return false;
            }

            return SkillEffectRegistry.TryCreate(skill.Data.EffectId, out ISkillEffect effect)
                && effect is IHealingSkillEffect;
        }

        private bool HasAnyUsableHealingSkillFromPreview(CustomCellGrid cellGrid)
        {
            return GetAllKnownSkills()
                .Where(IsHealingSkill)
                .Any(skill => CanUseSkillFromPreview(skill, cellGrid));
        }

        private bool HasAnyUsableSkillFromPreview(CustomCellGrid cellGrid)
        {
            return GetAllKnownSkills().Any(skill => CanUseSkillFromPreview(skill, cellGrid));
        }

        private bool CanUseSkillFromPreview(Skill skill, CustomCellGrid cellGrid)
        {
            if (IsAreaSkill(skill))
            {
                return IsAreaSkillUsableFromPreview(skill, cellGrid);
            }

            return skill != null
                && UnitReference != null
                && UnitReference.CanUseSkill(skill)
                && IsSkillSupported(skill.Data)
                && IsSkillEffectImplemented(skill.Data)
                && GetValidSkillTargets(skill, cellGrid).Count > 0;
        }

        private bool CanUseAnySkillAgainstTarget(CustomUnit target, CustomCellGrid cellGrid)
        {
            return target != null && GetAllKnownSkills().Any(skill => CanUseSkillAgainstTarget(skill, target, cellGrid));
        }

        private bool CanUseSkillAgainstTarget(Skill skill, CustomUnit target, CustomCellGrid cellGrid)
        {
            return skill != null
                && target != null
                && UnitReference != null
                && UnitReference.CanUseSkill(skill)
                && IsSkillSupported(skill.Data)
                && IsSkillEffectImplemented(skill.Data)
                && CanSkillTargetUnit(skill, target, cellGrid);
        }

        private List<CustomUnit> GetValidSkillTargets(Skill skill, CustomCellGrid cellGrid)
        {
            var results = new List<CustomUnit>();
            if (skill?.Data == null || cellGrid == null || GetActingCellForPendingActions(cellGrid) == null)
            {
                return results;
            }

            switch (skill.Data.TargetingType)
            {
                case SkillTargetingType.Self:
                    if (CanSkillTargetUnit(skill, UnitReference, cellGrid))
                    {
                        results.Add(UnitReference);
                    }
                    break;

                case SkillTargetingType.EnemyUnit:
                    results.AddRange(GetEnemySkillTargetCandidates(cellGrid).Where(unit => CanSkillTargetUnit(skill, unit, cellGrid)));
                    break;

                case SkillTargetingType.AllyUnit:
                    results.AddRange(GetAllySkillTargetCandidates(cellGrid).Where(unit => CanSkillTargetUnit(skill, unit, cellGrid)));
                    break;

                case SkillTargetingType.AnyUnit:
                    results.AddRange(GetAnySkillTargetCandidates(cellGrid).Where(unit => CanSkillTargetUnit(skill, unit, cellGrid)));
                    break;
            }

            return results;
        }

        private IEnumerable<CustomUnit> GetEnemySkillTargetCandidates(CustomCellGrid cellGrid)
        {
            if (UnitReference == null)
            {
                return Enumerable.Empty<CustomUnit>();
            }

            return ResolveGridUnits(cellGrid)
                .Where(unit => unit != null && unit.HitPoints > 0 && unit.PlayerNumber != UnitReference.PlayerNumber);
        }

        private IEnumerable<CustomUnit> GetAllySkillTargetCandidates(CustomCellGrid cellGrid)
        {
            if (UnitReference == null)
            {
                return Enumerable.Empty<CustomUnit>();
            }

            return ResolveGridUnits(cellGrid)
                .Where(unit => unit != null && unit != UnitReference && unit.HitPoints > 0 && unit.PlayerNumber == UnitReference.PlayerNumber);
        }

        private IEnumerable<CustomUnit> GetAnySkillTargetCandidates(CustomCellGrid cellGrid)
        {
            if (UnitReference == null)
            {
                return Enumerable.Empty<CustomUnit>();
            }

            return ResolveGridUnits(cellGrid)
                .Where(unit => unit != null && unit != UnitReference && unit.HitPoints > 0);
        }

        private bool CanSkillTargetUnit(Skill skill, CustomUnit target, CustomCellGrid cellGrid)
        {
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (skill?.Data == null || target == null || actingCell == null)
            {
                return false;
            }

            if (!MatchesSkillTargeting(skill.Data.TargetingType, target))
            {
                return false;
            }

            Item backingWeaponEntry = GetPreferredWeaponForSkill(skill, target, actingCell, skill == GetSelectedSkillPreviewEntry() ? selectedSkillPreviewWeaponEntry : null);
            if (!TryResolveSkillRange(skill, backingWeaponEntry?.Weapon, actingCell, out int minRange, out int maxRange))
            {
                return false;
            }

            Cell targetCell = target.HasPendingMove ? target.PreviewCell : target.Cell;
            if (targetCell == null)
            {
                return false;
            }

            int distance = actingCell.GetDistance(targetCell);
            if (distance < minRange || distance > maxRange)
            {
                return false;
            }

            SkillContext context = BuildSkillContext(skill, target, cellGrid);
            if (skill.Data.AttackProfile.Enabled)
            {
                return string.IsNullOrWhiteSpace(skill.Data.EffectId)
                    || (SkillEffectRegistry.TryCreate(skill.Data.EffectId, out ISkillEffect attackEffect)
                        && attackEffect.CanUse(UnitReference, context));
            }

            return SkillEffectRegistry.TryCreate(skill.Data.EffectId, out ISkillEffect effect)
                && effect.CanUse(UnitReference, context);
        }

        private SkillContext BuildSkillContext(Skill skill, CustomUnit target, CustomCellGrid cellGrid)
        {
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            return new SkillContext
            {
                User = UnitReference,
                PrimaryTargetUnit = target,
                TargetCell = target != null ? (target.HasPendingMove ? target.PreviewCell : target.Cell) : actingCell,
                CellGrid = cellGrid,
                Skill = skill?.Data
            };
        }

        private bool TryPrepareAttackSkillEffect(Skill skill, SkillContext context, ref ResolvedAttackProfile profile, out ISkillEffect effect)
        {
            effect = null;
            if (skill?.Data == null || string.IsNullOrWhiteSpace(skill.Data.EffectId))
            {
                return true;
            }

            if (!SkillEffectRegistry.TryCreate(skill.Data.EffectId, out effect) || !effect.CanUse(UnitReference, context))
            {
                effect = null;
                return false;
            }

            if (effect is IAttackSkillEffect attackSkillEffect)
            {
                attackSkillEffect.ModifyAttackProfile(UnitReference, context, ref profile);
            }

            return true;
        }

        private bool IsSkillSupported(SkillData data)
        {
            if (data == null)
            {
                return false;
            }

            return data.TargetingType == SkillTargetingType.Self
                || data.TargetingType == SkillTargetingType.EnemyUnit
                || data.TargetingType == SkillTargetingType.AllyUnit
                || data.TargetingType == SkillTargetingType.AnyUnit
                || data.TargetingType == SkillTargetingType.AreaCell;
        }

        private bool IsSkillEffectImplemented(SkillData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.EffectId))
            {
                return true;
            }

            return SkillEffectRegistry.TryCreate(data.EffectId, out _);
        }

        private bool MatchesSkillTargeting(SkillTargetingType targetingType, CustomUnit target)
        {
            if (target == null || UnitReference == null)
            {
                return false;
            }

            switch (targetingType)
            {
                case SkillTargetingType.Self:
                    return target == UnitReference;
                case SkillTargetingType.EnemyUnit:
                    return target.PlayerNumber != UnitReference.PlayerNumber;
                case SkillTargetingType.AllyUnit:
                    return target != UnitReference && target.PlayerNumber == UnitReference.PlayerNumber;
                case SkillTargetingType.AnyUnit:
                    return target != UnitReference;
                default:
                    return false;
            }
        }

        private bool TryResolveSkillRange(Skill skill, WeaponData backingWeapon, Cell sourceCell, out int minRange, out int maxRange)
        {
            minRange = 0;
            maxRange = 0;
            if (skill?.Data == null || sourceCell == null)
            {
                return false;
            }

            SkillData data = skill.Data;
            if (data.TargetingType == SkillTargetingType.Self)
            {
                return true;
            }

            if (data.Category == SkillCategory.CombatArt)
            {
                if (backingWeapon == null)
                {
                    return false;
                }

                SkillRangeUtility.ApplyCombatArtRangeModifiers(
                    UnitReference.GetMinAttackRangeForWeapon(backingWeapon),
                    UnitReference.GetMaxAttackRangeForWeapon(backingWeapon),
                    data.AttackProfile.MinRange,
                    data.AttackProfile.MaxRange,
                    out minRange,
                    out maxRange);
                NormalizeResolvedSkillRange(ref minRange, ref maxRange);
                return true;
            }

            if (data.AreaProfile.Enabled)
            {
                minRange = Mathf.Max(0, data.AreaProfile.MinRange);
                maxRange = Mathf.Max(minRange, data.AreaProfile.MaxRange);
                NormalizeResolvedSkillRange(ref minRange, ref maxRange);
                return true;
            }

            minRange = Mathf.Max(0, data.AttackProfile.MinRange);
            maxRange = Mathf.Max(minRange, data.AttackProfile.MaxRange);
            NormalizeResolvedSkillRange(ref minRange, ref maxRange);
            return true;
        }

        private static void NormalizeResolvedSkillRange(ref int minRange, ref int maxRange)
        {
            minRange = Mathf.Max(0, minRange);
            maxRange = Mathf.Max(minRange, maxRange);

            if (SkillRangeUtility.IsInfiniteRange(maxRange))
            {
                maxRange = int.MaxValue;
            }
        }

        private static bool IsInfiniteResolvedSkillRange(int maxRange)
        {
            return maxRange == int.MaxValue;
        }

        private Item GetPreferredWeaponForSkill(Skill skill, CustomUnit target, Cell sourceCell, Item preferredWeaponEntry = null)
        {
            if (skill?.Data == null || skill.Data.Category != SkillCategory.CombatArt)
            {
                return null;
            }

            var candidates = UnitReference.GetWeaponInventoryEntries()
                .Where(entry => entry?.Weapon != null && SkillMatchesWeapon(skill.Data, entry.Weapon))
                .ToList();

            if (target != null && sourceCell != null)
            {
                candidates = candidates
                    .Where(entry =>
                    {
                        SkillRangeUtility.ApplyCombatArtRangeModifiers(
                            UnitReference.GetMinAttackRangeForWeapon(entry.Weapon),
                            UnitReference.GetMaxAttackRangeForWeapon(entry.Weapon),
                            skill.Data.AttackProfile.MinRange,
                            skill.Data.AttackProfile.MaxRange,
                            out int minRange,
                            out int maxRange);

                        Cell targetCell = target.HasPendingMove ? target.PreviewCell : target.Cell;
                        if (targetCell == null)
                        {
                            return false;
                        }

                        int distance = sourceCell.GetDistance(targetCell);
                        return distance >= minRange && distance <= maxRange;
                    })
                    .ToList();
            }

            if (preferredWeaponEntry != null && candidates.Contains(preferredWeaponEntry))
            {
                return preferredWeaponEntry;
            }

            var equippedWeaponEntry = UnitReference.Inventory?.EquippedWeaponEntry;
            if (equippedWeaponEntry != null && candidates.Contains(equippedWeaponEntry))
            {
                return equippedWeaponEntry;
            }

            return candidates.FirstOrDefault();
        }

        private static bool SkillMatchesWeapon(SkillData data, WeaponData weapon)
        {
            if (data == null || weapon == null)
            {
                return false;
            }

            switch (data.RequiredWeaponType)
            {
                case CombatArtWeaponType.Any:
                    return true;
                case CombatArtWeaponType.Sword:
                    return (weapon.WeaponType & WeaponType.Sword) != 0;
                case CombatArtWeaponType.Lance:
                    return (weapon.WeaponType & WeaponType.Lance) != 0;
                case CombatArtWeaponType.Blunt:
                    return (weapon.WeaponType & WeaponType.Blunt) != 0;
                case CombatArtWeaponType.Ranged:
                    return (weapon.WeaponType & WeaponType.Ranged) != 0;
                case CombatArtWeaponType.Magic:
                    return (weapon.WeaponType & WeaponType.Magic) != 0 || weapon.DamageType == DamageType.Magic;
                default:
                    return false;
            }
        }

        private enum SkillHighlightMode
        {
            Enemy,
            Ally,
            Any
        }

        private static SkillHighlightMode GetSkillHighlightMode(SkillData data)
        {
            if (data == null)
            {
                return SkillHighlightMode.Ally;
            }

            if (data.AreaProfile.Enabled)
            {
                if (data.AreaProfile.AffectsAllies && data.AreaProfile.AffectsEnemies)
                {
                    return SkillHighlightMode.Any;
                }

                if (data.AreaProfile.AffectsEnemies)
                {
                    return SkillHighlightMode.Enemy;
                }

                return SkillHighlightMode.Ally;
            }

            return data.TargetingType switch
            {
                SkillTargetingType.EnemyUnit => SkillHighlightMode.Enemy,
                SkillTargetingType.AnyUnit => SkillHighlightMode.Any,
                _ => SkillHighlightMode.Ally
            };
        }

        private static void ApplySkillPreviewHighlight(Cell cell, SkillHighlightMode highlightMode, bool faint = false)
        {
            if (!(cell is CustomSquare customSquare))
            {
                return;
            }

            switch (highlightMode)
            {
                case SkillHighlightMode.Enemy:
                    if (faint)
                    {
                        customSquare.MarkAsAttackPreviewFaint();
                    }
                    else
                    {
                        customSquare.MarkAsAttackPreview();
                    }
                    break;
                case SkillHighlightMode.Any:
                    if (faint)
                    {
                        customSquare.MarkAsAnyPreviewFaint();
                    }
                    else
                    {
                        customSquare.MarkAsAnyPreview();
                    }
                    break;
                default:
                    if (faint)
                    {
                        customSquare.MarkAsTradePreviewFaint();
                    }
                    else
                    {
                        customSquare.MarkAsTradePreview();
                    }
                    break;
            }
        }

        private static void ApplyAreaCenterBorderPreview(AreaBorderOutline outline, SkillHighlightMode highlightMode)
        {
            if (outline.Cell is not CustomSquare customSquare)
            {
                return;
            }

            customSquare.ShowPreviewBorder(
                outline.Top,
                outline.Right,
                outline.Bottom,
                outline.Left,
                GetAreaCenterBorderColor(highlightMode));
        }

        private static Color GetAreaCenterBorderColor(SkillHighlightMode highlightMode)
        {
            return highlightMode switch
            {
                SkillHighlightMode.Enemy => new Color(0.65f, 0.12f, 0.12f, 1f),
                SkillHighlightMode.Any => new Color(0.16f, 0.58f, 0.16f, 1f),
                _ => new Color(0.16f, 0.4f, 0.78f, 1f)
            };
        }

        private void ApplySkillTargetHighlight(CustomUnit unit, SkillData data)
        {
            if (unit == null)
            {
                return;
            }

            switch (GetSkillHighlightMode(data))
            {
                case SkillHighlightMode.Enemy:
                    unit.MarkAsReachableEnemy();
                    break;
                case SkillHighlightMode.Any:
                    unit.SetColor(new Color32(150, 255, 150, 255));
                    break;
                default:
                    unit.SetColor(new Color32(150, 220, 255, 255));
                    break;
            }
        }

        private string GetSkillActionDisplayName(Skill skill, CustomUnit target, CustomCellGrid cellGrid)
        {
            if (skill?.Data == null)
            {
                return "Skill";
            }

            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            Item backingWeaponEntry = GetPreferredWeaponForSkill(skill, target, actingCell, selectedSkillPreviewWeaponEntry);
            if (backingWeaponEntry?.Weapon != null)
            {
                return $"{skill.Data.Name} ({backingWeaponEntry.Weapon.Name})";
            }

            return skill.Data.Name;
        }

        private static string GetDefenderPreviewWeaponDisplayName(CustomUnit defender)
        {
            return defender?.EquippedWeapon?.Name ?? "None";
        }

        private bool TryGetHealingAmount(Skill skill, CustomUnit target, CustomCellGrid cellGrid, out int healingAmount)
        {
            healingAmount = 0;
            if (skill?.Data == null || target == null || cellGrid == null || string.IsNullOrWhiteSpace(skill.Data.EffectId))
            {
                return false;
            }

            if (!SkillEffectRegistry.TryCreate(skill.Data.EffectId, out ISkillEffect effect) || effect is not IHealingSkillEffect healingEffect)
            {
                return false;
            }

            SkillContext context = BuildSkillContext(skill, target, cellGrid);
            if (!healingEffect.CanUse(UnitReference, context))
            {
                return false;
            }

            healingAmount = Mathf.Max(0, healingEffect.GetHealingAmount(UnitReference, context));
            return healingAmount > 0;
        }

        private List<AreaConfirmTargetPreviewData> BuildAreaConfirmTargetPreviews(
            Skill skill,
            Cell centerCell,
            IReadOnlyList<CustomUnit> targets,
            CustomCellGrid cellGrid)
        {
            var results = new List<AreaConfirmTargetPreviewData>();
            if (skill?.Data == null || centerCell == null || targets == null || targets.Count == 0 || cellGrid == null)
            {
                return results;
            }

            IReadOnlyList<CustomUnit> orderedTargets = targets
                .Where(target => target != null)
                .Distinct()
                .OrderBy(target => target.PlayerNumber == UnitReference.PlayerNumber ? 0 : 1)
                .ThenBy(target => target.UnitID)
                .ToList();

            if (orderedTargets.Count == 0)
            {
                return results;
            }

            if (skill.Data.AttackProfile.Enabled)
            {
                if (!TryBuildSkillAttackProfile(skill, orderedTargets.FirstOrDefault(), null, out ResolvedAttackProfile profile))
                {
                    return results;
                }

                SkillContext castContext = BuildAreaSkillContext(skill, centerCell, orderedTargets.FirstOrDefault(), cellGrid, orderedTargets);
                if (!TryPrepareAttackSkillEffect(skill, castContext, ref profile, out _))
                {
                    return results;
                }

                int hitMultiplier = Mathf.Max(1, profile.NumHits);
                foreach (CustomUnit target in orderedTargets)
                {
                    int damage = CalculateProjectedDamage(UnitReference, target, hitMultiplier, profile);
                    int projectedHp = Mathf.Max(0, target.HitPoints - damage);
                    results.Add(new AreaConfirmTargetPreviewData(target.unitName, target.HitPoints, projectedHp));
                }

                return results;
            }

            if (!SkillEffectRegistry.TryCreate(skill.Data.EffectId, out ISkillEffect effect))
            {
                return results;
            }

            foreach (CustomUnit target in orderedTargets)
            {
                SkillContext context = BuildAreaSkillContext(skill, centerCell, target, cellGrid, orderedTargets);
                if (!effect.CanUse(UnitReference, context))
                {
                    continue;
                }

                int projectedHp = target.HitPoints;
                if (effect is IHealingSkillEffect healingEffect)
                {
                    int healingAmount = Mathf.Max(0, healingEffect.GetHealingAmount(UnitReference, context));
                    projectedHp = Mathf.Min(target.ComputedTotalHitPoints, target.HitPoints + healingAmount);
                }

                results.Add(new AreaConfirmTargetPreviewData(target.unitName, target.HitPoints, projectedHp));
            }

            return results;
        }

        private AttackPreviewPanelData BuildSkillAttackerPreview(Skill skill, CustomUnit target, CustomCellGrid cellGrid)
        {
            if (skill?.Data == null)
            {
                return new AttackPreviewPanelData(UnitReference.unitName, FormatHitPointsDisplay(UnitReference), "Def/Res: -", "-", "-", "-");
            }

            if (TryGetHealingAmount(skill, target, cellGrid, out int healingAmount))
            {
                return new AttackPreviewPanelData(
                    UnitReference.unitName,
                    FormatHitPointsDisplay(UnitReference),
                    "Def: -",
                    healingAmount.ToString(),
                    FormatPercentValue(100),
                    FormatPercentValue(0),
                    "Heal");
            }

            if (!TryBuildSkillAttackProfile(skill, target, GetPreferredWeaponForSkill(skill, target, GetActingCellForPendingActions(cellGrid), selectedSkillPreviewWeaponEntry), out ResolvedAttackProfile profile))
            {
                return new AttackPreviewPanelData(UnitReference.unitName, FormatHitPointsDisplay(UnitReference), "Def/Res: -", "-", "-", "-");
            }

            SkillContext context = BuildSkillContext(skill, target, cellGrid);
            if (!TryPrepareAttackSkillEffect(skill, context, ref profile, out _))
            {
                return new AttackPreviewPanelData(UnitReference.unitName, FormatHitPointsDisplay(UnitReference), "Def/Res: -", "-", "-", "-");
            }

            int attackMultiplier = Mathf.Max(1, profile.NumHits);
            if (profile.CanPursuitAttack && target != null && UnitReference.Speed >= target.Speed + 5)
            {
                attackMultiplier *= 2;
            }

            int perHitDamage = CalculatePerHitDamage(profile, target);
            bool isFatal = CalculateProjectedDamage(UnitReference, target, attackMultiplier, profile) >= target.HitPoints;
            return new AttackPreviewPanelData(
                UnitReference.unitName,
                FormatHitPointsDisplay(UnitReference),
                BuildMitigationValue(UnitReference, target != null && target.HasUsableWeapon, target != null && target.IsMagic),
                FormatDamageValue(perHitDamage, attackMultiplier, isFatal),
                FormatPercentValue(CalculateHitChance(profile, target)),
                FormatCritValue(profile, target, perHitDamage));
        }

        private AttackPreviewPanelData BuildSkillDefenderPreview(Skill skill, CustomUnit defender, CustomCellGrid cellGrid)
        {
            if (skill?.Data == null || defender == null)
            {
                return new AttackPreviewPanelData("-", "-", "Def/Res: -", "-", "-", "-");
            }

            if (TryGetHealingAmount(skill, defender, cellGrid, out int healingAmount))
            {
                return new AttackPreviewPanelData(
                    defender.unitName,
                    FormatHitPointsDisplay(defender),
                    "Def: -",
                    "-",
                    "-",
                    "-");
            }

            if (!TryBuildSkillAttackProfile(skill, defender, GetPreferredWeaponForSkill(skill, defender, GetActingCellForPendingActions(cellGrid), selectedSkillPreviewWeaponEntry), out ResolvedAttackProfile attackProfile))
            {
                return new AttackPreviewPanelData(defender.unitName, FormatHitPointsDisplay(defender), "Def/Res: -", "-", "-", "-");
            }

            SkillContext context = BuildSkillContext(skill, defender, cellGrid);
            if (!TryPrepareAttackSkillEffect(skill, context, ref attackProfile, out _))
            {
                return new AttackPreviewPanelData(defender.unitName, FormatHitPointsDisplay(defender), "Def/Res: -", "-", "-", "-");
            }

            bool defenderCanCounter = skill.Data.TargetingType == SkillTargetingType.EnemyUnit
                && defender.CanCounterAttackAgainst(UnitReference, attackProfile.PreventsCounterattack);
            if (!defenderCanCounter)
            {
                return new AttackPreviewPanelData(
                    defender.unitName,
                    FormatHitPointsDisplay(defender),
                    BuildMitigationValue(defender, true, attackProfile.IsMagic),
                    "-",
                    "-",
                    "-");
            }

            int attackMultiplier = Mathf.Max(1, defender.NumHits);
            int perHitDamage = CalculatePerHitDamage(defender, UnitReference);
            bool isFatal = CalculateProjectedDamage(defender, UnitReference, attackMultiplier) >= UnitReference.HitPoints;
            return new AttackPreviewPanelData(
                defender.unitName,
                FormatHitPointsDisplay(defender),
                BuildMitigationValue(defender, true, attackProfile.IsMagic),
                FormatDamageValue(perHitDamage, attackMultiplier, isFatal),
                FormatPercentValue(CalculateHitChance(defender, UnitReference)),
                FormatCritValue(defender, UnitReference, perHitDamage));
        }

        private bool TryBuildSkillAttackProfile(Skill skill, CustomUnit target, Item backingWeaponEntry, out ResolvedAttackProfile profile)
        {
            profile = default;
            if (skill?.Data == null || !skill.Data.AttackProfile.Enabled)
            {
                return false;
            }

            SkillData data = skill.Data;
            if (data.Category == SkillCategory.CombatArt)
            {
                WeaponData weapon = backingWeaponEntry?.Weapon;
                if (weapon == null)
                {
                    return false;
                }

                profile = new ResolvedAttackProfile
                {
                    Damage = UnitReference.GetAttackForWeapon(weapon) + data.AttackProfile.Might,
                    Accuracy = UnitReference.GetAccuracyForWeapon(weapon) + data.AttackProfile.Accuracy,
                    Crit = UnitReference.GetCritForWeapon(weapon) + data.AttackProfile.Crit,
                    NumHits = Mathf.Max(1, data.AttackProfile.NumHits),
                    IsMagic = data.AttackProfile.IsMagic || UnitReference.GetIsMagicForWeapon(weapon),
                    // Combat arts use the skill's hit count as the final hit count.
                    CanPursuitAttack = false,
                    PreventsCounterattack = data.AttackProfile.PreventsCounterattack,
                    EndsTurn = data.EndsTurn
                };
                return true;
            }

            bool isMagic = data.AttackProfile.IsMagic;
            int offensiveStat = isMagic ? UnitReference.Magic : UnitReference.Strength;
            profile = new ResolvedAttackProfile
            {
                Damage = offensiveStat + data.AttackProfile.Might,
                Accuracy = UnitReference.Speed * 5 + data.AttackProfile.Accuracy,
                Crit = UnitReference.Luck * 5 + data.AttackProfile.Crit,
                NumHits = Mathf.Max(1, data.AttackProfile.NumHits),
                IsMagic = isMagic,
                CanPursuitAttack = false,
                PreventsCounterattack = data.AttackProfile.PreventsCounterattack,
                EndsTurn = data.EndsTurn
            };
            return true;
        }

        private List<Item> GetSkillPreviewWeaponOptions(Skill skill, CustomUnit target, CustomCellGrid cellGrid)
        {
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (skill?.Data == null || skill.Data.Category != SkillCategory.CombatArt || actingCell == null)
            {
                return new List<Item>();
            }

            return UnitReference.GetWeaponInventoryEntries()
                .Where(entry => entry?.Weapon != null && SkillMatchesWeapon(skill.Data, entry.Weapon))
                .Where(entry =>
                {
                    SkillRangeUtility.ApplyCombatArtRangeModifiers(
                        UnitReference.GetMinAttackRangeForWeapon(entry.Weapon),
                        UnitReference.GetMaxAttackRangeForWeapon(entry.Weapon),
                        skill.Data.AttackProfile.MinRange,
                        skill.Data.AttackProfile.MaxRange,
                        out int minRange,
                        out int maxRange);

                    Cell targetCell = target?.HasPendingMove == true ? target.PreviewCell : target?.Cell;
                    if (targetCell == null)
                    {
                        return false;
                    }

                    int distance = actingCell.GetDistance(targetCell);
                    return distance >= minRange && distance <= maxRange;
                })
                .ToList();
        }

        private bool CanCycleSkillPreviewWeapon(CustomUnit target, CustomCellGrid cellGrid)
        {
            return GetSkillPreviewWeaponOptions(GetSelectedSkillPreviewEntry(), target, cellGrid).Count > 1;
        }

        private bool CanSkillReachCellFromPreview(Skill skill, Cell targetCell, CustomCellGrid cellGrid)
        {
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (skill?.Data == null || targetCell == null || actingCell == null)
            {
                return false;
            }

            if (skill.Data.TargetingType == SkillTargetingType.Self)
            {
                return targetCell == actingCell;
            }

            if (skill.Data.Category == SkillCategory.CombatArt)
            {
                foreach (var weaponEntry in UnitReference.GetWeaponInventoryEntries().Where(entry => entry?.Weapon != null && SkillMatchesWeapon(skill.Data, entry.Weapon)))
                {
                    SkillRangeUtility.ApplyCombatArtRangeModifiers(
                        UnitReference.GetMinAttackRangeForWeapon(weaponEntry.Weapon),
                        UnitReference.GetMaxAttackRangeForWeapon(weaponEntry.Weapon),
                        skill.Data.AttackProfile.MinRange,
                        skill.Data.AttackProfile.MaxRange,
                        out int minRange,
                        out int maxRange);

                    int distance = actingCell.GetDistance(targetCell);
                    if (distance >= minRange && distance <= maxRange)
                    {
                        return true;
                    }
                }

                return false;
            }

            if (!TryResolveSkillRange(skill, null, actingCell, out int minRangeResolved, out int maxRangeResolved))
            {
                return false;
            }

            int resolvedDistance = actingCell.GetDistance(targetCell);
            return resolvedDistance >= minRangeResolved && resolvedDistance <= maxRangeResolved;
        }

        private Item ResolveSkillPreviewWeaponForCurrentSelection(CustomUnit target, CustomCellGrid cellGrid)
        {
            Skill selectedSkill = GetSelectedSkillPreviewEntry();
            if (selectedSkill?.Data == null || selectedSkill.Data.Category != SkillCategory.CombatArt)
            {
                return null;
            }

            return GetPreferredWeaponForSkill(selectedSkill, target, GetActingCellForPendingActions(cellGrid), selectedSkillPreviewWeaponEntry);
        }

        private static int CalculateProjectedDamage(CustomUnit attacker, CustomUnit defender, int multiplier, ResolvedAttackProfile profile)
        {
            if (attacker == null || defender == null)
            {
                return 0;
            }

            int defenseStat = profile.IsMagic ? defender.Resistance : defender.Defense;
            return Mathf.Max(1, profile.Damage - defenseStat) * Mathf.Max(1, multiplier);
        }

        private static int GetGuaranteedAreaSkillHitChance()
        {
            // DefendHandler subtracts Evade from the incoming hit stat.
            // Use an intentionally oversized value so offensive area spells always hit.
            return 10000;
        }

        private static int CalculatePerHitDamage(ResolvedAttackProfile profile, CustomUnit defender)
        {
            if (defender == null)
            {
                return 0;
            }

            int defenseStat = profile.IsMagic ? defender.Resistance : defender.Defense;
            return Mathf.Max(1, profile.Damage - defenseStat);
        }

        private static int CalculatePerHitCritDamage(ResolvedAttackProfile profile, CustomUnit defender)
        {
            if (defender == null)
            {
                return 0;
            }

            int defenseStat = profile.IsMagic ? defender.Resistance : defender.Defense;
            return Mathf.Max(1, profile.Damage * 2 - defenseStat);
        }

        private static int CalculateHitChance(ResolvedAttackProfile profile, CustomUnit defender)
        {
            if (defender == null)
            {
                return 0;
            }

            return Mathf.Clamp(profile.Accuracy - defender.Evade, 0, 100);
        }

        private static int CalculateCritChance(ResolvedAttackProfile profile, CustomUnit defender)
        {
            if (defender == null)
            {
                return 0;
            }

            return Mathf.Clamp(profile.Crit - defender.CritAvoid, 0, 100);
        }

        private static string FormatCritValue(ResolvedAttackProfile profile, CustomUnit defender, int normalDamage)
        {
            if (defender == null)
            {
                return "-";
            }

            int critChance = CalculateCritChance(profile, defender);
            int critDamage = CalculatePerHitCritDamage(profile, defender);
            return $"{FormatPercentValue(critChance)} ({normalDamage} -> {critDamage})";
        }

        protected override void OnCellSelected(Cell cell, CustomCellGrid cellGrid)
        {
            RefreshAvailableDestinationsIfNeeded(cellGrid);

            if (UnitReference.CanStartActionThisTurn && availableDestinations.Contains(cell))
            {
                RestoreReachableDisplay(cellGrid);
                currentPath = UnitReference.FindPath(ResolveGridCells(cellGrid), cell);
                foreach (var c in currentPath)
                {
                    MarkPathCell(c, cellGrid);
                }
            }
        }
        protected override void OnCellDeselected(Cell cell, CustomCellGrid cellGrid)
        {
            RefreshAvailableDestinationsIfNeeded(cellGrid);

            if (UnitReference.CanStartActionThisTurn && availableDestinations.Contains(cell))
            {
                RestoreReachableDisplay(cellGrid);
                currentPath = null;
            }
        }

        protected override void OnAbilitySelected(CustomCellGrid cellGrid)
        {
            RefreshAvailableDestinations(cellGrid);
        }

        protected override void CleanUp(CustomCellGrid cellGrid)
        {
            if (availableDestinations == null)
            {
                return;
            }

            foreach (var cell in availableDestinations)
            {
                ClearCellMark(cell, cellGrid);
            }
        }

        private void RestoreReachableDisplay(CustomCellGrid cellGrid)
        {
            if (availableDestinations == null)
            {
                return;
            }

            foreach (var reachableCell in availableDestinations)
            {
                MarkReachableCell(reachableCell, cellGrid);
            }
        }

        private static bool ShouldUseRuntimeCellHighlighting(CustomCellGrid cellGrid)
        {
            return cellGrid != null && cellGrid.ShouldRouteHumanMovementThroughRuntime;
        }

        private static void MarkReachableCell(Cell cell, CustomCellGrid cellGrid)
        {
            if (cell == null)
            {
                return;
            }

            if (ShouldUseRuntimeCellHighlighting(cellGrid))
            {
                GetRuntimeBoardCell(cell)?.ApplyHighlight(CellHighlightKind.Reachable);
            }
            else
            {
                cell.MarkAsReachable();
            }
        }

        private static void MarkPathCell(Cell cell, CustomCellGrid cellGrid)
        {
            if (cell == null)
            {
                return;
            }

            if (ShouldUseRuntimeCellHighlighting(cellGrid))
            {
                GetRuntimeBoardCell(cell)?.ApplyHighlight(CellHighlightKind.Path);
            }
            else
            {
                cell.MarkAsPath();
            }
        }

        private static void ClearCellMark(Cell cell, CustomCellGrid cellGrid)
        {
            if (cell == null)
            {
                return;
            }

            if (ShouldUseRuntimeCellHighlighting(cellGrid))
            {
                GetRuntimeBoardCell(cell)?.ClearHighlight();
            }
            else
            {
                cell.UnMark();
            }
        }

        private static BoardCell GetRuntimeBoardCell(Cell cell)
        {
            return cell != null ? cell.GetComponent<BoardCell>() : null;
        }

        protected override bool CanPerform(CustomCellGrid cellGrid)
        {
            RefreshAvailableDestinationsIfNeeded(cellGrid);
            return UnitReference.CanStartActionThisTurn && UnitReference.GetAvailableDestinations(ResolveGridCells(cellGrid)).Count > 0;
        }

        private void RefreshAvailableDestinationsIfNeeded(CustomCellGrid cellGrid)
        {
            if (cellGrid == null)
            {
                return;
            }

            if (availableDestinations == null || cachedOccupancyRevision != cellGrid.OccupancyRevision)
            {
                RefreshAvailableDestinations(cellGrid);
            }
        }

        private void RefreshAvailableDestinations(CustomCellGrid cellGrid)
        {
            List<Cell> allCells = ResolveGridCells(cellGrid);
            UnitReference.CachePaths(allCells);
            availableDestinations = UnitReference.GetAvailableDestinations(allCells);
            cachedOccupancyRevision = cellGrid != null ? cellGrid.OccupancyRevision : cachedOccupancyRevision;
        }

        public override IDictionary<string, string> Encapsulate()
        {
            var actionParams = new Dictionary<string, string>();

            actionParams.Add("destination_x", Destination.OffsetCoord.x.ToString());
            actionParams.Add("destination_y", Destination.OffsetCoord.y.ToString());

            return actionParams;
        }

        protected override IEnumerator Apply(CustomCellGrid cellGrid, IDictionary<string, string> actionParams, bool isNetworkInvoked = false)
        {
            var actionDestination = FindCellByOffset(
                cellGrid,
                new Vector2Int(
                    Mathf.RoundToInt(float.Parse(actionParams["destination_x"])),
                    Mathf.RoundToInt(float.Parse(actionParams["destination_y"]))));
            Destination = actionDestination;
            yield return StartCoroutine(RemoteExecute(cellGrid));
        }
    }
}
