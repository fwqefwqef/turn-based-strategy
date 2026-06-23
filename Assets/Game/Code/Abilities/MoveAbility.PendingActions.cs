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
    public partial class MoveAbility
    {
        // --- Pending attack / skill / trade / inventory actions ---
        private void BeginAttackTargeting(CellGrid cellGrid)
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

        private void BeginHealSelection(CellGrid cellGrid)
        {
            BeginSkillSelection(cellGrid, IsHealingSkill);
        }

        private void BeginSkillSelection(CellGrid cellGrid)
        {
            BeginSkillSelection(cellGrid, null);
        }

        private void BeginSkillSelection(CellGrid cellGrid, System.Func<Skill, bool> skillFilter)
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

        private void SelectSkillFromMenu(Skill skill, CellGrid cellGrid)
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

        private void BeginAreaSkillTargeting(Skill skill, CellGrid cellGrid)
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

        private void HandlePendingAreaSkillCellInteraction(Cell hoveredCell, CellGrid cellGrid)
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

        private void HandlePendingSkillUnitClicked(Unit unit, CellGrid cellGrid)
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

        private void ShowInventoryMenu(CellGrid cellGrid)
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

        private void BeginTradeTargeting(CellGrid cellGrid)
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

        private void CancelAttackTargeting(CellGrid cellGrid)
        {
            awaitingAttackTargetSelection = false;
            ClearAttackTargetingPreview();
            ShowActionMenu(cellGrid);
        }

        private void CancelAttackPreview(CellGrid cellGrid)
        {
            ClearAttackPreviewSelection();
            awaitingAttackTargetSelection = false;
            ClearAttackTargetingPreview();
            ShowActionMenu(cellGrid);
        }

        private void CancelTradeTargeting(CellGrid cellGrid)
        {
            awaitingTradeTargetSelection = false;
            ClearTradeTargetingPreview();
            ShowActionMenu(cellGrid);
        }

        private void ShowAreaSkillConfirmPopup(CellGrid cellGrid, Cell centerCell, IReadOnlyList<Unit> affectedTargets)
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

        private void ConfirmAreaSkill(CellGrid cellGrid)
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

        private void CancelAreaSkillConfirmation(CellGrid cellGrid)
        {
            awaitingSkillTargetSelection = false;
            awaitingAreaSkillConfirmation = false;
            selectedTargetingSkill = null;
            selectedAreaSkillCenterCell = null;
            FindAreaConfirmUI()?.Hide();
            ClearSkillTargetingPreview();
            ShowActionMenu(cellGrid);
        }

        private void CancelPendingMoveAndRestoreSelection(CellGrid cellGrid)
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

        private void ShowAttackPreviewCells(CellGrid cellGrid)
        {
            ClearAttackPreviewCells();

            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (actingCell == null)
            {
                return;
            }

            foreach (var cell in ResolveCells(cellGrid))
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
            Windy.Srpg.Game.Grid.CellGrid cellGrid = FindSceneCellGrid();
            foreach (var cell in pendingAttackPreviewCells)
            {
                ClearAttackPreviewCellMark(cell, cellGrid);
            }

            pendingAttackPreviewCells.Clear();
        }

        private static void MarkAttackPreviewCell(Cell cell, CellGrid cellGrid)
        {
            if (cell == null)
            {
                return;
            }

            cell?.ApplyHighlight(CellHighlightKind.Attack);
        }

        private static void ClearAttackPreviewCellMark(Cell cell, CellGrid cellGrid)
        {
            if (cell == null)
            {
                return;
            }

            cell.UnMark();
        }

        private Cell GetActingCellForPendingActions(CellGrid cellGrid)
        {
            if (UnitReference == null)
            {
                return null;
            }

            return UnitReference.HasPendingMove
                ? UnitReference.PreviewCell
                : UnitReference.Cell;
        }

        private static CellGrid FindSceneCellGrid()
        {
            return UnityEngine.Object.FindAnyObjectByType<CellGrid>();
        }

        private void RefreshPendingAttackableEnemies(CellGrid cellGrid)
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

        private void ShowSkillPreviewCells(Skill skill, CellGrid cellGrid)
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
            foreach (var cell in ResolveCells(cellGrid))
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
            CellGrid cellGrid = FindSceneCellGrid();
            foreach (var cell in pendingSkillPreviewCells)
            {
                ClearSkillPreviewCellMark(cell, cellGrid);
            }

            pendingSkillPreviewCells.Clear();
        }

        private static void MarkSkillPreviewCell(Cell cell, SkillHighlightMode highlightMode, CellGrid cellGrid, bool faint = false)
        {
            if (cell == null)
            {
                return;
            }

            ApplySkillPreviewHighlight(cell, highlightMode, faint);
        }

        private static void ClearSkillPreviewCellMark(Cell cell, CellGrid cellGrid)
        {
            if (cell == null)
            {
                return;
            }

            cell.UnMark();
        }

        private void RefreshPendingSkillTargets(Skill skill, CellGrid cellGrid)
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
                CellTilePreviewUtility.ClearPreviewBorder(cell);
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

        private bool IsAreaSkillUsableFromPreview(Skill skill, CellGrid cellGrid)
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

        private bool HasAnyUsableLineAreaSkillProjection(Skill skill, CellGrid cellGrid)
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

        private List<Cell> GetLegalAreaSkillCenterCells(Skill skill, CellGrid cellGrid)
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

            foreach (var cell in ResolveCells(cellGrid))
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

        private Cell GetClosestLegalAreaSkillCenterCell(Skill skill, Cell hoveredCell, CellGrid cellGrid)
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

        private Vector2Int ResolveLineAreaDirection(Cell hoveredCell, CellGrid cellGrid)
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

        private List<AreaBorderOutline> BuildAreaBorderOutlines(IEnumerable<Cell> cells, CellGrid cellGrid)
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

        private List<AreaBorderOutline> GetAreaSkillCenterBorderOutlines(Skill skill, CellGrid cellGrid)
        {
            return BuildAreaBorderOutlines(GetLegalAreaSkillCenterCells(skill, cellGrid), cellGrid);
        }

        private Dictionary<Vector2Int, Cell> BuildCellLookup(CellGrid cellGrid)
        {
            return ResolveCells(cellGrid)
                .Where(cell => cell != null)
                .GroupBy(cell => new Vector2Int(Mathf.RoundToInt(cell.OffsetCoord.x), Mathf.RoundToInt(cell.OffsetCoord.y)))
                .ToDictionary(group => group.Key, group => group.First());
        }

        private AreaLineProjection BuildLineAreaProjection(Skill skill, Vector2Int direction, CellGrid cellGrid)
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

        private int ResolveInfiniteLineRangeToGridEdge(Vector2Int direction, CellGrid cellGrid)
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

        private List<Unit> GetAreaSkillTargets(Skill skill, Cell centerCell, CellGrid cellGrid)
        {
            var results = new List<Unit>();
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

            foreach (var unit in GetAllBattleUnits(cellGrid))
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

            var filteredResults = new List<Unit>(results.Count);
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

        private HashSet<Cell> GetAreaSkillAffectedCells(Skill skill, Cell centerCell, CellGrid cellGrid)
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
            foreach (var cell in ResolveCells(cellGrid))
            {
                if (cell == null || centerCell.GetDistance(cell) > radius || ShouldSkipAreaSkillOverlayCell(skill, cell, cellGrid))
                {
                    continue;
                }

                results.Add(cell);
            }

            return results;
        }

        private SkillContext BuildAreaSkillContext(Skill skill, Cell centerCell, Unit primaryTarget, CellGrid cellGrid, IReadOnlyList<Unit> areaTargets = null)
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

        private bool ShouldSkipAreaSkillOverlayCell(Skill skill, Cell cell, CellGrid cellGrid)
        {
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            return skill?.Data != null
                && skill.Data.SelfImmune
                && actingCell != null
                && cell == actingCell;
        }

        private void UpdateAreaSkillProjection(Cell hoveredCell, CellGrid cellGrid)
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

        private void ShowTradePreviewCells(CellGrid cellGrid)
        {
            ClearTradePreviewCells();

            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (actingCell == null || cellGrid == null)
            {
                return;
            }

            foreach (var cell in ResolveCells(cellGrid))
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
            CellGrid cellGrid = FindSceneCellGrid();
            foreach (var cell in pendingTradePreviewCells)
            {
                ClearTradePreviewCellMark(cell, cellGrid);
            }

            pendingTradePreviewCells.Clear();
        }

        private static void MarkTradePreviewCell(Cell cell, CellGrid cellGrid)
        {
            if (cell == null)
            {
                return;
            }

            cell?.ApplyHighlight(CellHighlightKind.Deployment);
        }

        private static void ClearTradePreviewCellMark(Cell cell, CellGrid cellGrid)
        {
            if (cell == null)
            {
                return;
            }

            cell.UnMark();
        }

        private void ClearTradeTargetingPreview()
        {
            ClearTradePreviewCells();
            HideTradeMenu();
        }

        private List<Unit> GetAttackableEnemiesFromPreview(CellGrid cellGrid)
        {
            if (cellGrid == null)
            {
                return new List<Unit>();
            }

            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            return cellGrid.GetAttackableEnemiesFromActingCell(UnitReference, actingCell);
        }

        private bool IsUnitTradeableFromPreview(Unit target, CellGrid cellGrid)
        {
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (target == null || target == UnitReference || target.Cell == null || actingCell == null)
            {
                return false;
            }

            return target.PlayerNumber == UnitReference.PlayerNumber
                && actingCell.GetDistance(target.Cell) == 1;
        }

        private bool IsUnitTradeableFromPreview(Unit target)
        {
            return IsUnitTradeableFromPreview(target, FindSceneCellGrid());
        }

        private List<Unit> GetTradeableFriendliesFromPreview(CellGrid cellGrid)
        {
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            if (cellGrid == null || actingCell == null)
            {
                return new List<Unit>();
            }

            return cellGrid.GetCurrentPlayerUnits()
                .Where(unit => unit != null
                    && unit != UnitReference
                    && unit.Cell != null
                    && actingCell.GetDistance(unit.Cell) == 1)
                .OrderBy(unit => unit.UnitID)
                .ToList();
        }

        private Unit GetTradePartnerFromPreview(CellGrid cellGrid)
        {
            return GetTradeableFriendliesFromPreview(cellGrid).FirstOrDefault();
        }

        private void OpenTradeMenuForUnit(Unit tradePartner, CellGrid cellGrid)
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

        private bool CanAttackTargetWithAnyWeaponFromPreview(Unit target, CellGrid cellGrid)
        {
            Cell actingCell = GetActingCellForPendingActions(cellGrid);
            return target != null
                && actingCell != null
                && UnitReference.CanAttackTargetWithAnyWeapon(target, actingCell);
        }

        private bool CanAttackTargetWithAnyWeaponFromPreview(Unit target)
        {
            return CanAttackTargetWithAnyWeaponFromPreview(target, FindSceneCellGrid());
        }

        private void OpenAttackPreview(Unit target, CellGrid cellGrid)
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

        private void ConfirmAttackPreview(CellGrid cellGrid)
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

        private void CycleAttackPreviewWeapon(CellGrid cellGrid)
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

        private void OpenSkillPreview(Unit target, CellGrid cellGrid, Skill preferredSkill)
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

        private void ShowSkillPreview(Unit target, CellGrid cellGrid)
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

        private void ConfirmSkillPreview(CellGrid cellGrid)
        {
            var skill = GetSelectedSkillPreviewEntry();
            if (!IsSkillPreviewOpen() || skill?.Data == null || selectedSkillPreviewTarget == null)
            {
                return;
            }

            StartCoroutine(ExecuteSkillThenConfirmPendingMove(skill, selectedSkillPreviewTarget, cellGrid));
        }

        private void CycleSkillPreviewWeapon(CellGrid cellGrid)
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

        private void CancelSkillPreview(CellGrid cellGrid)
        {
            ClearSkillPreviewSelection();
            awaitingSkillTargetSelection = false;
            selectedTargetingSkill = null;
            selectedSkillPreviewWeaponEntry = null;
            ClearSkillTargetingPreview();
            ShowActionMenu(cellGrid);
        }

        private void CancelSkillTargeting(CellGrid cellGrid)
        {
            awaitingSkillTargetSelection = false;
            awaitingAreaSkillConfirmation = false;
            selectedTargetingSkill = null;
            selectedAreaSkillCenterCell = null;
            selectedSkillPreviewWeaponEntry = null;
            ClearSkillTargetingPreview();
            ShowActionMenu(cellGrid);
        }

        private IEnumerator ExecuteSkillThenConfirmPendingMove(Skill skill, Unit target, CellGrid cellGrid)
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

        private IEnumerator ExecuteAreaSkillThenConfirmPendingMove(Skill skill, Cell centerCell, IReadOnlyList<Unit> affectedTargets, CellGrid cellGrid)
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

                IReadOnlyList<Unit> orderedTargets = affectedTargets
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

        private void ShowAttackPreview(Unit target, CellGrid cellGrid)
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

        private AttackPreviewPanelData BuildAttackerPreview(Unit defender, WeaponData weapon)
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

        private AttackPreviewPanelData BuildDefenderPreview(Unit defender, WeaponData incomingWeapon)
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

        private static string BuildMitigationValue(Unit unit, bool hasIncomingAttack, bool incomingIsMagic)
        {
            if (unit == null || !hasIncomingAttack)
            {
                return "Def/Res: -";
            }

            return incomingIsMagic
                ? $"Res: {unit.Resistance}"
                : $"Def: {unit.Defense}";
        }

        private static int CalculateAttackValue(Unit attacker, WeaponData weapon = null)
        {
            if (attacker == null)
            {
                return 0;
            }

            return weapon != null ? attacker.GetAttackForWeapon(weapon) : attacker.Attack;
        }

        private static int CalculatePerHitDamage(Unit attacker, Unit defender, WeaponData weapon = null)
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

        private static int CalculatePerHitCritDamage(Unit attacker, Unit defender, WeaponData weapon = null)
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

        private static int CalculateProjectedDamage(Unit attacker, Unit defender, int multiplier, WeaponData weapon = null)
        {
            return CalculatePerHitDamage(attacker, defender, weapon) * Mathf.Max(1, multiplier);
        }

        private static int CalculateProjectedCritDamage(Unit attacker, Unit defender, int multiplier, WeaponData weapon = null)
        {
            return CalculatePerHitCritDamage(attacker, defender, weapon) * Mathf.Max(1, multiplier);
        }

        private static int CalculateHitChance(Unit attacker, Unit defender, WeaponData weapon = null)
        {
            if (attacker == null || defender == null)
            {
                return 0;
            }

            int accuracy = weapon != null ? attacker.GetAccuracyForWeapon(weapon) : attacker.Accuracy;
            return Mathf.Clamp(accuracy - defender.Evade, 0, 100);
        }

        private static int CalculateCritChance(Unit attacker, Unit defender, WeaponData weapon = null)
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

        private static string FormatCritValue(Unit attacker, Unit defender, int normalDamage, WeaponData weapon = null)
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

        private bool HasAnyUsableHealingSkillFromPreview(CellGrid cellGrid)
        {
            return GetAllKnownSkills()
                .Where(IsHealingSkill)
                .Any(skill => CanUseSkillFromPreview(skill, cellGrid));
        }

        private bool HasAnyUsableSkillFromPreview(CellGrid cellGrid)
        {
            return GetAllKnownSkills().Any(skill => CanUseSkillFromPreview(skill, cellGrid));
        }

        private bool CanUseSkillFromPreview(Skill skill, CellGrid cellGrid)
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

        private bool CanUseAnySkillAgainstTarget(Unit target, CellGrid cellGrid)
        {
            return target != null && GetAllKnownSkills().Any(skill => CanUseSkillAgainstTarget(skill, target, cellGrid));
        }

        private bool CanUseSkillAgainstTarget(Skill skill, Unit target, CellGrid cellGrid)
        {
            return skill != null
                && target != null
                && UnitReference != null
                && UnitReference.CanUseSkill(skill)
                && IsSkillSupported(skill.Data)
                && IsSkillEffectImplemented(skill.Data)
                && CanSkillTargetUnit(skill, target, cellGrid);
        }

        private List<Unit> GetValidSkillTargets(Skill skill, CellGrid cellGrid)
        {
            var results = new List<Unit>();
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

        private IEnumerable<Unit> GetEnemySkillTargetCandidates(CellGrid cellGrid)
        {
            if (UnitReference == null)
            {
                return Enumerable.Empty<Unit>();
            }

            return GetAllBattleUnits(cellGrid)
                .Where(unit => unit != null && unit.HitPoints > 0 && unit.PlayerNumber != UnitReference.PlayerNumber);
        }

        private IEnumerable<Unit> GetAllySkillTargetCandidates(CellGrid cellGrid)
        {
            if (UnitReference == null)
            {
                return Enumerable.Empty<Unit>();
            }

            return GetAllBattleUnits(cellGrid)
                .Where(unit => unit != null && unit != UnitReference && unit.HitPoints > 0 && unit.PlayerNumber == UnitReference.PlayerNumber);
        }

        private IEnumerable<Unit> GetAnySkillTargetCandidates(CellGrid cellGrid)
        {
            if (UnitReference == null)
            {
                return Enumerable.Empty<Unit>();
            }

            return GetAllBattleUnits(cellGrid)
                .Where(unit => unit != null && unit != UnitReference && unit.HitPoints > 0);
        }

        private bool CanSkillTargetUnit(Skill skill, Unit target, CellGrid cellGrid)
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

        private SkillContext BuildSkillContext(Skill skill, Unit target, CellGrid cellGrid)
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

        private bool MatchesSkillTargeting(SkillTargetingType targetingType, Unit target)
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

        private Item GetPreferredWeaponForSkill(Skill skill, Unit target, Cell sourceCell, Item preferredWeaponEntry = null)
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
            CellHighlightKind kind = highlightMode switch
            {
                SkillHighlightMode.Enemy => CellHighlightKind.Attack,
                SkillHighlightMode.Any => CellHighlightKind.Support,
                _ => CellHighlightKind.Support
            };

            if (faint)
            {
                CellTilePreviewUtility.ApplySkillPreviewHighlight(cell, kind, faint: true);
                return;
            }

            CellTilePreviewUtility.ApplySkillPreviewHighlight(cell, kind);
        }

        private static void ApplyAreaCenterBorderPreview(AreaBorderOutline outline, SkillHighlightMode highlightMode)
        {
            if (outline.Cell == null)
            {
                return;
            }

            CellTilePreviewUtility.ShowPreviewBorder(
                outline.Cell,
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

        private void ApplySkillTargetHighlight(Unit unit, SkillData data)
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

        private string GetSkillActionDisplayName(Skill skill, Unit target, CellGrid cellGrid)
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

        private static string GetDefenderPreviewWeaponDisplayName(Unit defender)
        {
            return defender?.EquippedWeapon?.Name ?? "None";
        }

        private bool TryGetHealingAmount(Skill skill, Unit target, CellGrid cellGrid, out int healingAmount)
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
            IReadOnlyList<Unit> targets,
            CellGrid cellGrid)
        {
            var results = new List<AreaConfirmTargetPreviewData>();
            if (skill?.Data == null || centerCell == null || targets == null || targets.Count == 0 || cellGrid == null)
            {
                return results;
            }

            IReadOnlyList<Unit> orderedTargets = targets
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
                foreach (Unit target in orderedTargets)
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

            foreach (Unit target in orderedTargets)
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

        private AttackPreviewPanelData BuildSkillAttackerPreview(Skill skill, Unit target, CellGrid cellGrid)
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

        private AttackPreviewPanelData BuildSkillDefenderPreview(Skill skill, Unit defender, CellGrid cellGrid)
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

        private bool TryBuildSkillAttackProfile(Skill skill, Unit target, Item backingWeaponEntry, out ResolvedAttackProfile profile)
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

        private List<Item> GetSkillPreviewWeaponOptions(Skill skill, Unit target, CellGrid cellGrid)
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

        private bool CanCycleSkillPreviewWeapon(Unit target, CellGrid cellGrid)
        {
            return GetSkillPreviewWeaponOptions(GetSelectedSkillPreviewEntry(), target, cellGrid).Count > 1;
        }

        private bool CanSkillReachCellFromPreview(Skill skill, Cell targetCell, CellGrid cellGrid)
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

        private Item ResolveSkillPreviewWeaponForCurrentSelection(Unit target, CellGrid cellGrid)
        {
            Skill selectedSkill = GetSelectedSkillPreviewEntry();
            if (selectedSkill?.Data == null || selectedSkill.Data.Category != SkillCategory.CombatArt)
            {
                return null;
            }

            return GetPreferredWeaponForSkill(selectedSkill, target, GetActingCellForPendingActions(cellGrid), selectedSkillPreviewWeaponEntry);
        }

        private static int CalculateProjectedDamage(Unit attacker, Unit defender, int multiplier, ResolvedAttackProfile profile)
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

        private static int CalculatePerHitDamage(ResolvedAttackProfile profile, Unit defender)
        {
            if (defender == null)
            {
                return 0;
            }

            int defenseStat = profile.IsMagic ? defender.Resistance : defender.Defense;
            return Mathf.Max(1, profile.Damage - defenseStat);
        }

        private static int CalculatePerHitCritDamage(ResolvedAttackProfile profile, Unit defender)
        {
            if (defender == null)
            {
                return 0;
            }

            int defenseStat = profile.IsMagic ? defender.Resistance : defender.Defense;
            return Mathf.Max(1, profile.Damage * 2 - defenseStat);
        }

        private static int CalculateHitChance(ResolvedAttackProfile profile, Unit defender)
        {
            if (defender == null)
            {
                return 0;
            }

            return Mathf.Clamp(profile.Accuracy - defender.Evade, 0, 100);
        }

        private static int CalculateCritChance(ResolvedAttackProfile profile, Unit defender)
        {
            if (defender == null)
            {
                return 0;
            }

            return Mathf.Clamp(profile.Crit - defender.CritAvoid, 0, 100);
        }

        private static string FormatCritValue(ResolvedAttackProfile profile, Unit defender, int normalDamage)
        {
            if (defender == null)
            {
                return "-";
            }

            int critChance = CalculateCritChance(profile, defender);
            int critDamage = CalculatePerHitCritDamage(profile, defender);
            return $"{FormatPercentValue(critChance)} ({normalDamage} -> {critDamage})";
        }

        protected override void HandleCellSelected(Cell cell, CellGrid cellGrid)
        {
            RefreshAvailableDestinationsIfNeeded(cellGrid);

            if (UnitReference.CanStartActionThisTurn && availableDestinations.Contains(cell))
            {
                RestoreReachableDisplay(cellGrid);
                currentPath = UnitReference.FindPath(ResolveCells(cellGrid), cell);
                MarkCurrentPath(cellGrid);
            }
        }

        protected override void HandleCellDeselected(Cell cell, CellGrid cellGrid)
        {
            RefreshAvailableDestinationsIfNeeded(cellGrid);

            ClearCurrentPathHighlights(cellGrid);
            currentPath = null;

            if (UnitReference.CanStartActionThisTurn && availableDestinations.Contains(cell))
            {
                RestoreReachableDisplay(cellGrid);
            }
        }

        private void MarkCurrentPath(CellGrid cellGrid)
        {
            Cell originCell = UnitReference?.Cell;
            if (originCell != null)
            {
                ClearCellMark(originCell, cellGrid);
            }

            if (currentPath == null)
            {
                return;
            }

            foreach (var pathCell in currentPath)
            {
                if (pathCell == null || pathCell == originCell)
                {
                    continue;
                }

                MarkPathCell(pathCell, cellGrid);
            }
        }

        private void ClearCurrentPathHighlights(CellGrid cellGrid)
        {
            if (currentPath == null)
            {
                return;
            }

            Cell originCell = UnitReference?.Cell;
            foreach (var pathCell in currentPath)
            {
                if (pathCell == null || pathCell == originCell)
                {
                    continue;
                }

                ClearCellMark(pathCell, cellGrid);
            }
        }

        protected override void OnAbilitySelected(CellGrid cellGrid)
        {
            RefreshAvailableDestinations(cellGrid);
        }

        protected override void CleanUp(CellGrid cellGrid)
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

        private void RestoreReachableDisplay(CellGrid cellGrid)
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

        private static void MarkReachableCell(Cell cell, CellGrid cellGrid)
        {
            if (cell == null)
            {
                return;
            }

            cell.MarkAsReachable();
        }

        private static void MarkPathCell(Cell cell, CellGrid cellGrid)
        {
            if (cell == null)
            {
                return;
            }

            cell.MarkAsPath();
        }

        private static void ClearCellMark(Cell cell, CellGrid cellGrid)
        {
            if (cell == null)
            {
                return;
            }

            cell.UnMark();
        }


        protected override bool CanPerformAbility(CellGrid cellGrid)
        {
            RefreshAvailableDestinationsIfNeeded(cellGrid);
            return UnitReference.CanStartActionThisTurn && UnitReference.GetAvailableDestinations(ResolveCells(cellGrid)).Count > 0;
        }

        private void RefreshAvailableDestinationsIfNeeded(CellGrid cellGrid)
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

        private void RefreshAvailableDestinations(CellGrid cellGrid)
        {
            List<Cell> allCells = ResolveCells(cellGrid);
            UnitReference.CachePaths(allCells);
            availableDestinations = UnitReference.GetAvailableDestinations(allCells);
            cachedOccupancyRevision = cellGrid != null ? cellGrid.OccupancyRevision : cachedOccupancyRevision;
        }

        public override IDictionary<string, string> Encapsulate()
        {
            return new Dictionary<string, string>
            {
                ["destination_x"] = Destination.OffsetCoord.x.ToString(),
                ["destination_y"] = Destination.OffsetCoord.y.ToString(),
            };
        }

        protected override IEnumerator Apply(CellGrid cellGrid, IDictionary<string, string> actionParams, bool isNetworkInvoked = false)
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
