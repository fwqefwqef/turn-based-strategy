using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Windy.Srpg.Game.CameraControl;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Grid;

namespace Windy.Srpg.Game.UI
{
    /// <summary>
    /// Central gameplay input coordinator.
    /// Owns the active control scheme, hover/focus tile, keyboard UI navigation, and the mapping
    /// from raw input into scene-grid actions. Modal gameplay UI blocks hover-tile input here
    /// instead of scattering the checks across each individual panel.
    /// </summary>
    public sealed class GameplayInputController : MonoBehaviour
    {
        private enum ControlScheme
        {
            Mouse,
            Keyboard,
            Controller
        }

        private sealed class ControlProfile
        {
            public readonly KeyCode KeyboardSelect = KeyCode.X;
            public readonly KeyCode KeyboardCancel = KeyCode.Z;
            public readonly KeyCode KeyboardInspect = KeyCode.S;
            public readonly KeyCode KeyboardShowRanges = KeyCode.A;
            public readonly KeyCode KeyboardUp = KeyCode.UpArrow;
            public readonly KeyCode KeyboardDown = KeyCode.DownArrow;
            public readonly KeyCode KeyboardLeft = KeyCode.LeftArrow;
            public readonly KeyCode KeyboardRight = KeyCode.RightArrow;
        }

        private static readonly Color CursorBorderColor = Color.black;
        private static readonly Color FocusedButtonColor = new Color(0.63f, 0.84f, 1f, 1f);
        private static GameplayInputController activeInstance;

        [Header("References")]
        [SerializeField] private CellGrid cellGrid;
        [SerializeField] private Camera worldCamera;

        [Header("Keyboard Hover")]
        [SerializeField] private float holdRepeatDelay = 0.3f;
        [SerializeField] private float holdRepeatInterval = 0.08f;
        [SerializeField] private float mouseSchemeSwitchThresholdPixels = 2f;

        private readonly ControlProfile controls = new ControlProfile();
        private readonly Dictionary<Vector2Int, Cell> cellByCoordinates = new Dictionary<Vector2Int, Cell>();
        private readonly HashSet<string> enemyRangeToggles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Button, ColorBlock> originalButtonColors = new Dictionary<Button, ColorBlock>();

        private ControlScheme currentScheme = ControlScheme.Mouse;
        private Cell hoveredCell;
        private Unit hoveredUnit;
        private Cell keyboardHoveredCell;
        private KeyCode repeatingDirectionKey = KeyCode.None;
        private float nextRepeatTime;
        private bool combatSequenceVisible;
        private bool experienceHudVisible;
        private bool levelUpUiVisible;
        private bool collectiveEnemyRangeVisible;
        private bool initialized;
        private Vector3 lastMousePosition;
        private bool? originalSendNavigationEvents;

        public static bool IsCentralizedSceneInputActive => activeInstance != null && activeInstance.enabled && activeInstance.initialized;

        public void Initialize(CellGrid grid)
        {
            if (initialized)
            {
                return;
            }

            if (grid != null)
            {
                cellGrid = grid;
            }

            if (cellGrid == null)
            {
                cellGrid = FindAnyObjectByType<CellGrid>();
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (cellGrid == null || worldCamera == null)
            {
                enabled = false;
                return;
            }

            RebuildCellLookup();
            lastMousePosition = Input.mousePosition;
            initialized = true;
        }

        private void OnEnable()
        {
            activeInstance = this;
            SubscribeUiEvents();
        }

        private void OnDisable()
        {
            if (activeInstance == this)
            {
                activeInstance = null;
            }

            UnsubscribeUiEvents();
            ClearHoverState();
        }

        private void Update()
        {
            if (!initialized)
            {
                Initialize(cellGrid);
                if (!initialized)
                {
                    return;
                }
            }

            DetectControlSchemeChanges();
            ApplyFocusColorsToRelevantButtons();

            UpdateEventSystemNavigationMode();

            if (currentScheme == ControlScheme.Keyboard && IsKeyboardUiNavigationActive())
            {
                ClearHoverState();
                if (keyboardHoveredCell != null)
                {
                    keyboardHoveredCell.ClearCursorBorder();
                }

                if (TryHandlePreBattleCancelCommand())
                {
                    return;
                }

                if (TryHandleBlockedCancelCommand())
                {
                    return;
                }

                if (UnitInspectPanelUI.HasOpenInspect && (IsCancelPressed() || IsInspectPressed()))
                {
                    UnitInspectPanelUI.TryClearInspectFromInput();
                    return;
                }

                TryHandleKeyboardUiNavigation();
                return;
            }

            if (TryHandlePreBattleCancelCommand())
            {
                return;
            }

            if (TryHandleBlockedCancelCommand())
            {
                return;
            }

            if (TryHandleKeyboardUiNavigation())
            {
                EnsureCursorBorderVisible();
                return;
            }

            if (currentScheme == ControlScheme.Keyboard)
            {
                EnsureKeyboardHoverCell();
                if (!IsGameplayInputBlocked())
                {
                    HandleKeyboardHoverMovement();
                }
            }
            else
            {
                UpdateMouseHover(ignoreUiOcclusion: IsGameplayInputBlocked(), dispatchGameplayHover: !IsGameplayInputBlocked());
            }

            EnsureCursorBorderVisible();

            if (IsGameplayInputBlocked())
            {
                if (UnitInspectPanelUI.HasOpenInspect && ShouldClearInspectFromMouseClick())
                {
                    UnitInspectPanelUI.TryClearInspectFromInput();
                }

                return;
            }

            if (TryHandleRangeToggleCommand())
            {
                return;
            }

            if (TryHandleInspectClearOnPointerClick())
            {
                return;
            }

            if (TryHandleInspectCommand())
            {
                return;
            }

            if (TryHandleCancelCommand())
            {
                return;
            }

            TryHandleSelectCommand();
        }

        private void SubscribeUiEvents()
        {
            CombatSequenceUI.VisibilityChanged += OnCombatSequenceVisibilityChanged;
            ExperienceGainHUD.VisibilityChanged += OnExperienceHudVisibilityChanged;
            LevelUpUI.VisibilityChanged += OnLevelUpUiVisibilityChanged;
        }

        private void UnsubscribeUiEvents()
        {
            CombatSequenceUI.VisibilityChanged -= OnCombatSequenceVisibilityChanged;
            ExperienceGainHUD.VisibilityChanged -= OnExperienceHudVisibilityChanged;
            LevelUpUI.VisibilityChanged -= OnLevelUpUiVisibilityChanged;
        }

        private void DetectControlSchemeChanges()
        {
            if (WasMouseInputDetected())
            {
                SwitchToMouseScheme();
                return;
            }

            if (WasKeyboardInputDetected())
            {
                SwitchToKeyboardScheme();
            }
        }

        private bool WasMouseInputDetected()
        {
            Vector3 currentMousePosition = Input.mousePosition;
            bool mouseMoved = (currentMousePosition - lastMousePosition).sqrMagnitude >= mouseSchemeSwitchThresholdPixels * mouseSchemeSwitchThresholdPixels;
            lastMousePosition = currentMousePosition;

            return Input.mouseScrollDelta.sqrMagnitude > 0.0001f
                || Input.GetMouseButtonDown(0)
                || Input.GetMouseButtonDown(1)
                || Input.GetMouseButtonDown(2)
                || mouseMoved;
        }

        private bool WasKeyboardInputDetected()
        {
            return Input.GetKeyDown(controls.KeyboardSelect)
                || Input.GetKeyDown(controls.KeyboardCancel)
                || Input.GetKeyDown(controls.KeyboardInspect)
                || Input.GetKeyDown(controls.KeyboardShowRanges)
                || Input.GetKeyDown(controls.KeyboardUp)
                || Input.GetKeyDown(controls.KeyboardDown)
                || Input.GetKeyDown(controls.KeyboardLeft)
                || Input.GetKeyDown(controls.KeyboardRight);
        }

        private void SwitchToMouseScheme()
        {
            if (currentScheme == ControlScheme.Mouse)
            {
                return;
            }

            currentScheme = ControlScheme.Mouse;
            keyboardHoveredCell = null;
            repeatingDirectionKey = KeyCode.None;
            nextRepeatTime = 0f;
            ClearHoverDispatchState();
            ClearKeyboardUiSelection();
        }

        private void SwitchToKeyboardScheme()
        {
            if (currentScheme == ControlScheme.Keyboard)
            {
                return;
            }

            currentScheme = ControlScheme.Keyboard;
            if (PreBattleUIController.TryGetPreferredSwitchDeploymentCell(out Cell preferredDeploymentCell))
            {
                keyboardHoveredCell = preferredDeploymentCell;
            }
            else
            {
                keyboardHoveredCell = hoveredCell ?? GameplayCameraController.GetFocusedCell();
            }

            if (keyboardHoveredCell == null)
            {
                keyboardHoveredCell = GetFallbackCell();
            }

            if (keyboardHoveredCell != null)
            {
                GameplayCameraController.SetFocusedCell(keyboardHoveredCell);
            }

            ApplyHoverTarget(keyboardHoveredCell, GetPrimaryUnitOnCell(keyboardHoveredCell));
        }

        private bool TryHandleKeyboardUiNavigation()
        {
            if (currentScheme != ControlScheme.Keyboard || !IsKeyboardUiNavigationActive())
            {
                return false;
            }

            List<Button> activeButtons = GetActiveKeyboardNavigationButtons();
            if (activeButtons.Count == 0)
            {
                ClearKeyboardUiSelection();
                return false;
            }

            Button selectedButton = GetCurrentSelectedButton(activeButtons);
            if (selectedButton == null)
            {
                selectedButton = ResolvePreferredKeyboardUiButton(activeButtons);
                SelectButton(selectedButton);
            }

            if (TryMoveKeyboardUiSelection(activeButtons, selectedButton))
            {
                return true;
            }

            if (Input.GetKeyDown(controls.KeyboardSelect))
            {
                Button focusedButton = GetCurrentSelectedButton(activeButtons) ?? selectedButton;
                focusedButton?.onClick.Invoke();
                return true;
            }

            return true;
        }

        private Button ResolvePreferredKeyboardUiButton(IReadOnlyList<Button> activeButtons)
        {
            if (activeButtons == null || activeButtons.Count == 0)
            {
                return null;
            }

            GameplayModalUI activeModal = GameplayModalUI.GetTopmostActiveModal(requireKeyboardNavigation: true);
            Button preferredButton = activeModal?.GetPreferredFocusButton();
            if (preferredButton != null && activeButtons.Contains(preferredButton))
            {
                return preferredButton;
            }

            preferredButton = PreBattleUIController.GetPreferredFocusButton(activeButtons);
            if (preferredButton != null && activeButtons.Contains(preferredButton))
            {
                return preferredButton;
            }

            preferredButton = LevelUpUI.GetPreferredFocusButton(activeButtons);
            if (preferredButton != null && activeButtons.Contains(preferredButton))
            {
                return preferredButton;
            }

            return activeButtons[0];
        }

        private bool IsKeyboardUiNavigationActive()
        {
            if (UnitInspectPanelUI.HasOpenInspect)
            {
                return true;
            }

            if (GameplayModalUI.HasAnyKeyboardNavigationModal())
            {
                return true;
            }

            if (PreBattleUIController.IsSwitchDeploymentBoardInteractionActive)
            {
                return false;
            }

            if (cellGrid != null && cellGrid.IsPreBattlePhase)
            {
                return true;
            }

            return levelUpUiVisible;
        }

        private List<Button> GetActiveKeyboardNavigationButtons()
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude);
            List<Button> activeButtons = new List<Button>();
            Camera eventCamera = ResolveUiEventCamera();

            foreach (Button button in buttons)
            {
                if (button == null
                    || !button.isActiveAndEnabled
                    || !button.interactable
                    || !button.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (button.GetComponentInParent<Canvas>() == null)
                {
                    continue;
                }

                if (!IsButtonRelevantForCurrentUi(button))
                {
                    continue;
                }

                EnsureButtonFocusColors(button);
                activeButtons.Add(button);
            }

            return activeButtons
                .OrderByDescending(button => GetButtonScreenCenter(button, eventCamera).y)
                .ThenBy(button => GetButtonScreenCenter(button, eventCamera).x)
                .ToList();
        }

        private bool IsButtonRelevantForCurrentUi(Button button)
        {
            if (button == null)
            {
                return false;
            }

            if (UnitInspectPanelUI.HasOpenInspect)
            {
                return button.GetComponentInParent<UnitInspectPanelUI>() != null;
            }

            GameplayModalUI activeModal = GameplayModalUI.GetTopmostActiveModal(requireKeyboardNavigation: true);
            if (activeModal != null)
            {
                return activeModal.ContainsButton(button);
            }

            if (cellGrid != null && cellGrid.IsPreBattlePhase)
            {
                return true;
            }

            if (levelUpUiVisible)
            {
                return true;
            }

            return false;
        }

        private void EnsureButtonFocusColors(Button button)
        {
            if (button == null || originalButtonColors.ContainsKey(button))
            {
                return;
            }

            ColorBlock originalColors = button.colors;
            originalButtonColors[button] = originalColors;
            ColorBlock patchedColors = originalColors;
            patchedColors.normalColor = originalColors.normalColor;
            patchedColors.highlightedColor = FocusedButtonColor;
            patchedColors.selectedColor = FocusedButtonColor;
            patchedColors.pressedColor = FocusedButtonColor;
            if (patchedColors.colorMultiplier < 1f)
            {
                patchedColors.colorMultiplier = 1f;
            }

            button.colors = patchedColors;
        }

        private void ApplyFocusColorsToRelevantButtons()
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude);
            foreach (Button button in buttons)
            {
                if (button == null
                    || !button.isActiveAndEnabled
                    || !button.gameObject.activeInHierarchy
                    || button.GetComponentInParent<Canvas>() == null)
                {
                    continue;
                }

                if (!IsButtonRelevantForCurrentUi(button))
                {
                    continue;
                }

                EnsureButtonFocusColors(button);
            }
        }

        private Button GetCurrentSelectedButton(IReadOnlyList<Button> candidates)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return null;
            }

            Button currentButton = eventSystem.currentSelectedGameObject != null
                ? eventSystem.currentSelectedGameObject.GetComponent<Button>()
                : null;
            if (currentButton != null && candidates.Contains(currentButton) && currentButton.isActiveAndEnabled && currentButton.interactable)
            {
                return currentButton;
            }

            return null;
        }

        private bool TryMoveKeyboardUiSelection(IReadOnlyList<Button> activeButtons, Button currentButton)
        {
            KeyCode pressedDirectionKey = GetUiDirectionKeyPressed();
            if (pressedDirectionKey == KeyCode.None || currentButton == null)
            {
                return false;
            }

            if (TryMoveInspectListSelection(currentButton, pressedDirectionKey, out Button inspectListButton))
            {
                if (inspectListButton != null && inspectListButton != currentButton)
                {
                    SelectButton(inspectListButton);
                    inspectListButton.GetComponentInParent<UnitInspectEntryListUI>()?.ScrollButtonIntoView(inspectListButton);
                }

                return true;
            }

            Vector2 direction = DirectionDelta(pressedDirectionKey);
            Button nextButton = FindNextButtonInDirection(
                currentButton,
                activeButtons,
                direction,
                useStrictInspectAlignment: UnitInspectPanelUI.HasOpenInspect && currentButton.GetComponentInParent<UnitInspectPanelUI>() != null);
            if (nextButton == null || nextButton == currentButton)
            {
                return true;
            }

            SelectButton(nextButton);
            return true;
        }

        private bool TryMoveInspectListSelection(Button currentButton, KeyCode pressedDirectionKey, out Button nextButton)
        {
            nextButton = null;
            if (!UnitInspectPanelUI.HasOpenInspect
                || currentButton == null
                || (pressedDirectionKey != KeyCode.UpArrow && pressedDirectionKey != KeyCode.DownArrow))
            {
                return false;
            }

            UnitInspectEntryListUI inspectList = currentButton.GetComponentInParent<UnitInspectEntryListUI>();
            if (inspectList == null || !inspectList.ContainsButton(currentButton))
            {
                return false;
            }

            int direction = pressedDirectionKey == KeyCode.DownArrow ? 1 : -1;
            if (!inspectList.TryGetAdjacentButton(currentButton, direction, out nextButton))
            {
                nextButton = null;
                return false;
            }

            inspectList.ScrollButtonIntoView(nextButton);
            return true;
        }

        private static KeyCode GetUiDirectionKeyPressed()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                return KeyCode.UpArrow;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                return KeyCode.DownArrow;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                return KeyCode.LeftArrow;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                return KeyCode.RightArrow;
            }

            return KeyCode.None;
        }

        private Button FindNextButtonInDirection(Button currentButton, IReadOnlyList<Button> activeButtons, Vector2 direction, bool useStrictInspectAlignment = false)
        {
            Camera eventCamera = ResolveUiEventCamera();
            Vector2 currentPosition = GetButtonScreenCenter(currentButton, eventCamera);
            Vector2 currentSize = GetButtonScreenSize(currentButton, eventCamera);
            bool horizontalMove = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y);
            float directionSign = horizontalMove
                ? Mathf.Sign(direction.x == 0f ? 1f : direction.x)
                : Mathf.Sign(direction.y == 0f ? 1f : direction.y);
            Button bestAlignedButton = null;
            float bestAlignedPrimary = float.PositiveInfinity;
            float bestAlignedSecondary = float.PositiveInfinity;
            Button bestFallbackButton = null;
            float bestFallbackScore = float.PositiveInfinity;

            foreach (Button candidate in activeButtons)
            {
                if (candidate == null || candidate == currentButton)
                {
                    continue;
                }

                Vector2 candidatePosition = GetButtonScreenCenter(candidate, eventCamera);
                Vector2 candidateSize = GetButtonScreenSize(candidate, eventCamera);
                Vector2 delta = candidatePosition - currentPosition;
                if (delta.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                float primaryDelta = horizontalMove ? delta.x : delta.y;
                float secondaryDelta = horizontalMove ? delta.y : delta.x;
                if (Mathf.Sign(primaryDelta) != directionSign || Mathf.Abs(primaryDelta) <= 0.01f)
                {
                    continue;
                }

                float primaryDistance = Mathf.Abs(primaryDelta);
                float secondaryDistance = Mathf.Abs(secondaryDelta);
                float alignmentTolerance = horizontalMove
                    ? Mathf.Max(12f, (currentSize.y + candidateSize.y) * 0.5f)
                    : Mathf.Clamp(Mathf.Min(currentSize.x, candidateSize.x) * 0.2f, 12f, 48f);

                if (useStrictInspectAlignment)
                {
                    alignmentTolerance = horizontalMove
                        ? Mathf.Min(alignmentTolerance, 28f)
                        : Mathf.Min(alignmentTolerance, 20f);
                }

                if (secondaryDistance <= alignmentTolerance)
                {
                    bool betterAligned = primaryDistance < bestAlignedPrimary - 0.01f
                        || (Mathf.Abs(primaryDistance - bestAlignedPrimary) <= 0.01f && secondaryDistance < bestAlignedSecondary);
                    if (betterAligned)
                    {
                        bestAlignedPrimary = primaryDistance;
                        bestAlignedSecondary = secondaryDistance;
                        bestAlignedButton = candidate;
                    }
                }

                float fallbackScore = secondaryDistance * (useStrictInspectAlignment ? 8f : 3f) + primaryDistance;
                if (fallbackScore < bestFallbackScore)
                {
                    bestFallbackScore = fallbackScore;
                    bestFallbackButton = candidate;
                }
            }

            return bestAlignedButton != null ? bestAlignedButton : bestFallbackButton;
        }

        private void SelectButton(Button button)
        {
            if (button == null || EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private void ClearKeyboardUiSelection()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(null);
        }

        private void UpdateEventSystemNavigationMode()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            bool shouldDisableBuiltInNavigation = currentScheme == ControlScheme.Keyboard && IsKeyboardUiNavigationActive();
            if (!originalSendNavigationEvents.HasValue)
            {
                originalSendNavigationEvents = eventSystem.sendNavigationEvents;
            }

            if (shouldDisableBuiltInNavigation)
            {
                eventSystem.sendNavigationEvents = false;
                return;
            }

            eventSystem.sendNavigationEvents = originalSendNavigationEvents ?? true;
        }

        private Camera ResolveUiEventCamera()
        {
            if (worldCamera != null)
            {
                return worldCamera;
            }

            return Camera.main;
        }

        private static Vector2 GetButtonScreenCenter(Button button, Camera eventCamera)
        {
            RectTransform rectTransform = button != null ? button.transform as RectTransform : null;
            if (rectTransform == null)
            {
                return Vector2.zero;
            }

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < corners.Length; i++)
            {
                sum += RectTransformUtility.WorldToScreenPoint(eventCamera, corners[i]);
            }

            return sum / 4f;
        }

        private static Vector2 GetButtonScreenSize(Button button, Camera eventCamera)
        {
            RectTransform rectTransform = button != null ? button.transform as RectTransform : null;
            if (rectTransform == null)
            {
                return Vector2.zero;
            }

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
            Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[1]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);
            return new Vector2(Mathf.Abs(topRight.x - topLeft.x), Mathf.Abs(topLeft.y - bottomLeft.y));
        }

        private void UpdateMouseHover(bool ignoreUiOcclusion, bool dispatchGameplayHover)
        {
            if (!ignoreUiOcclusion && IsPointerOverUi())
            {
                ClearHoverState();
                return;
            }

            if (!TryGetHoveredBoardTarget(out Cell cell, out Unit unit))
            {
                ClearHoverState();
                return;
            }

            ApplyHoverTarget(cell, unit, dispatchGameplayHover);
        }

        private void HandleKeyboardHoverMovement()
        {
            KeyCode directionKey = GetDirectionKeyDownOrRepeat();
            if (directionKey == KeyCode.None)
            {
                return;
            }

            EnsureKeyboardHoverCell();
            Vector2Int delta = DirectionDelta(directionKey);
            if (delta == Vector2Int.zero || keyboardHoveredCell == null)
            {
                return;
            }

            Cell nextCell = PreBattleUIController.IsSwitchDeploymentBoardInteractionActive
                ? FindNextDeploymentSlotCellInDirection(keyboardHoveredCell, delta)
                : FindNeighbourInDirection(keyboardHoveredCell, delta);
            if (nextCell == null)
            {
                return;
            }

            keyboardHoveredCell = nextCell;
            GameplayCameraController.SetFocusedCell(nextCell);
            UnitInspectPanelUI.TryClearInspectFromInput();
            ApplyHoverTarget(nextCell, GetPrimaryUnitOnCell(nextCell));
        }

        private void EnsureKeyboardHoverCell()
        {
            if (keyboardHoveredCell == null)
            {
                if (PreBattleUIController.TryGetPreferredSwitchDeploymentCell(out Cell preferredDeploymentCell))
                {
                    keyboardHoveredCell = preferredDeploymentCell;
                }
                else
                {
                    keyboardHoveredCell = GameplayCameraController.GetFocusedCell() ?? GetFallbackCell();
                }

                ApplyHoverTarget(keyboardHoveredCell, GetPrimaryUnitOnCell(keyboardHoveredCell));
            }
        }

        private KeyCode GetDirectionKeyDownOrRepeat()
        {
            KeyCode[] keys =
            {
                controls.KeyboardUp,
                controls.KeyboardDown,
                controls.KeyboardLeft,
                controls.KeyboardRight
            };

            foreach (KeyCode key in keys)
            {
                if (Input.GetKeyDown(key))
                {
                    repeatingDirectionKey = key;
                    nextRepeatTime = Time.unscaledTime + Mathf.Max(0.01f, holdRepeatDelay);
                    return key;
                }
            }

            if (repeatingDirectionKey != KeyCode.None && !Input.GetKey(repeatingDirectionKey))
            {
                repeatingDirectionKey = KeyCode.None;
            }

            if (repeatingDirectionKey != KeyCode.None && Time.unscaledTime >= nextRepeatTime)
            {
                nextRepeatTime = Time.unscaledTime + Mathf.Max(0.01f, holdRepeatInterval);
                return repeatingDirectionKey;
            }

            return KeyCode.None;
        }

        private bool TryHandleRangeToggleCommand()
        {
            if (!IsRangeTogglePressed())
            {
                return false;
            }

            if (hoveredUnit != null && hoveredUnit.PlayerNumber != 0)
            {
                string unitId = !string.IsNullOrWhiteSpace(hoveredUnit.UnitId) ? hoveredUnit.UnitId : hoveredUnit.name;
                if (!string.IsNullOrWhiteSpace(unitId))
                {
                    if (!enemyRangeToggles.Add(unitId))
                    {
                        enemyRangeToggles.Remove(unitId);
                    }
                }

                return true;
            }

            if (hoveredCell != null)
            {
                collectiveEnemyRangeVisible = !collectiveEnemyRangeVisible;
                return true;
            }

            return false;
        }

        private bool TryHandleInspectClearOnPointerClick()
        {
            if (!UnitInspectPanelUI.HasOpenInspect || currentScheme != ControlScheme.Mouse)
            {
                return false;
            }

            if (!ShouldClearInspectFromMouseClick())
            {
                return false;
            }

            return UnitInspectPanelUI.TryClearInspectFromInput();
        }

        private bool TryHandleInspectCommand()
        {
            if (!IsInspectPressed())
            {
                return false;
            }

            if (hoveredUnit == null)
            {
                return false;
            }

            return UnitInspectPanelUI.TryOpenInspectForUnit(hoveredUnit);
        }

        private bool TryHandleBlockedCancelCommand()
        {
            if (!IsGameplayInputBlocked() || !IsCancelPressed())
            {
                return false;
            }

            if (UnitInspectPanelUI.HasOpenInspect && UnitInspectPanelUI.TryClearInspectFromInput())
            {
                return true;
            }

            if (GameplayModalUI.TryCancelTopmostActiveModal())
            {
                return true;
            }

            return false;
        }

        private bool TryHandlePreBattleCancelCommand()
        {
            if (!IsCancelPressed() || cellGrid == null || !cellGrid.IsPreBattlePhase)
            {
                return false;
            }

            return PreBattleUIController.RequestBackFromInput();
        }

        private bool TryHandleCancelCommand()
        {
            if (!IsCancelPressed())
            {
                return false;
            }

            if (UnitInspectPanelUI.HasOpenInspect)
            {
                return UnitInspectPanelUI.TryClearInspectFromInput();
            }

            cellGrid?.ProcessSceneRightClick();
            return true;
        }

        private bool TryHandleSelectCommand()
        {
            if (!IsSelectPressed())
            {
                return false;
            }

            if (hoveredUnit != null)
            {
                if (ShouldOpenTurnInfoForHoveredUnit(hoveredUnit))
                {
                    TurnCounterUI.RequestShow();
                    return true;
                }

                TurnCounterUI.RequestHide();

                if (cellGrid != null)
                {
                    cellGrid.HandleSceneUnitClicked(hoveredUnit);
                }

                return true;
            }

            if (hoveredCell != null)
            {
                if (ShouldOpenTurnInfoForHoveredCell(hoveredCell))
                {
                    TurnCounterUI.RequestShow();
                    return true;
                }

                TurnCounterUI.RequestHide();

                if (cellGrid != null)
                {
                    cellGrid.HandleSceneCellClicked(hoveredCell);
                }

                return true;
            }

            return false;
        }

        private bool IsSelectPressed()
        {
            if (currentScheme == ControlScheme.Keyboard)
            {
                return Input.GetKeyDown(controls.KeyboardSelect);
            }

            return Input.GetMouseButtonDown(0) && !IsMouseComboPressedThisFrame();
        }

        private bool IsCancelPressed()
        {
            if (currentScheme == ControlScheme.Keyboard)
            {
                return Input.GetKeyDown(controls.KeyboardCancel);
            }

            return Input.GetMouseButtonDown(1) && !IsMouseComboPressedThisFrame();
        }

        private bool IsInspectPressed()
        {
            if (currentScheme == ControlScheme.Keyboard)
            {
                return Input.GetKeyDown(controls.KeyboardInspect);
            }

            return Input.GetMouseButtonDown(2);
        }

        private bool IsRangeTogglePressed()
        {
            if (currentScheme == ControlScheme.Keyboard)
            {
                return Input.GetKeyDown(controls.KeyboardShowRanges);
            }

            return IsMouseComboPressedThisFrame();
        }

        private bool IsMouseComboPressedThisFrame()
        {
            return Input.GetMouseButton(0)
                && Input.GetMouseButton(1)
                && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1));
        }

        private bool ShouldClearInspectFromMouseClick()
        {
            bool leftOrRightPressed = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            return leftOrRightPressed && !UnitInspectPanelUI.IsPointerInsideActiveInspectUi();
        }

        private bool IsGameplayInputBlocked()
        {
            if (GameplayModalUI.HasAnyBlockingModal())
            {
                return true;
            }

            return combatSequenceVisible
                || experienceHudVisible
                || levelUpUiVisible;
        }

        private bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private bool TryGetHoveredBoardTarget(out Cell cell, out Unit unit)
        {
            cell = null;
            unit = null;

            if (worldCamera == null)
            {
                return false;
            }

            Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                Unit hitUnit = hit.collider.GetComponentInParent<Unit>();
                if (hitUnit != null)
                {
                    cell = hitUnit.Cell;
                    unit = hitUnit;
                    return cell != null;
                }
            }

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                Cell hitCell = hit.collider.GetComponentInParent<Cell>();
                if (hitCell != null)
                {
                    cell = hitCell;
                    unit = GetPrimaryUnitOnCell(hitCell);
                    return true;
                }
            }

            return false;
        }

        private void EnsureCursorBorderVisible()
        {
            hoveredCell?.ShowCursorBorder(CursorBorderColor);
        }

        private void ApplyHoverTarget(Cell cell, Unit unit, bool dispatchGameplayHover = true)
        {
            if (hoveredCell == cell && hoveredUnit == unit)
            {
                if (cell != null)
                {
                    cell.ShowCursorBorder(CursorBorderColor);
                }

                return;
            }

            ClearHoverDispatchState();

            hoveredCell = cell;
            hoveredUnit = unit;

            if (hoveredCell != null)
            {
                hoveredCell.ShowCursorBorder(CursorBorderColor);
            }

            if (!dispatchGameplayHover)
            {
                return;
            }

            if (hoveredUnit != null)
            {
                cellGrid?.HandleSceneUnitHighlighted(hoveredUnit);
            }
            else if (hoveredCell != null)
            {
                cellGrid?.HandleSceneCellSelected(hoveredCell);
            }
        }

        private void ClearHoverState()
        {
            ClearHoverDispatchState();
            hoveredCell = null;
            hoveredUnit = null;
        }

        private void ClearHoverDispatchState()
        {
            if (hoveredUnit != null)
            {
                cellGrid?.HandleSceneUnitDehighlighted(hoveredUnit);
            }
            else if (hoveredCell != null)
            {
                cellGrid?.HandleSceneCellDeselected(hoveredCell);
            }

            hoveredCell?.ClearCursorBorder();
        }

        private void RebuildCellLookup()
        {
            cellByCoordinates.Clear();
            if (cellGrid == null)
            {
                return;
            }

            foreach (Cell cell in cellGrid.GetAllCells())
            {
                if (cell != null)
                {
                    cellByCoordinates[cell.Coordinates] = cell;
                }
            }
        }

        private Cell GetFallbackCell()
        {
            if (cellGrid == null)
            {
                return null;
            }

            RebuildCellLookup();
            return cellGrid.GetAllCells().FirstOrDefault(cell => cell != null);
        }

        private Cell FindNeighbourInDirection(Cell originCell, Vector2Int delta)
        {
            if (originCell == null)
            {
                return null;
            }

            Vector2Int targetCoordinates = originCell.Coordinates + delta;
            if (cellByCoordinates.TryGetValue(targetCoordinates, out Cell directCell) && directCell != null)
            {
                return directCell;
            }

            List<Cell> allCells = cellGrid != null ? cellGrid.GetAllCells() : null;
            IEnumerable<Cell> neighbours = originCell.GetNeighbours(allCells);
            if (neighbours == null)
            {
                return null;
            }

            return neighbours
                .Where(cell => cell != null)
                .OrderByDescending(cell => Vector2.Dot((cell.Coordinates - originCell.Coordinates), delta))
                .ThenBy(cell => Vector2Int.Distance(cell.Coordinates, targetCoordinates))
                .FirstOrDefault(cell => Vector2.Dot((cell.Coordinates - originCell.Coordinates), delta) > 0f);
        }

        private Cell FindNextDeploymentSlotCellInDirection(Cell originCell, Vector2Int delta)
        {
            if (cellGrid == null || originCell == null)
            {
                return null;
            }

            IReadOnlyList<Cell> slotCells = cellGrid.GetPreBattleDeploymentSlotCells();
            if (slotCells == null || slotCells.Count == 0)
            {
                return null;
            }

            Vector2 direction = new Vector2(delta.x, delta.y);
            Cell bestCell = null;
            float bestAlignedPrimary = float.PositiveInfinity;
            float bestAlignedSecondary = float.PositiveInfinity;
            Cell bestFallbackCell = null;
            float bestFallbackScore = float.PositiveInfinity;
            Vector2 origin = new Vector2(originCell.transform.position.x, originCell.transform.position.y);
            bool horizontalMove = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y);
            float directionSign = horizontalMove
                ? Mathf.Sign(direction.x == 0f ? 1f : direction.x)
                : Mathf.Sign(direction.y == 0f ? 1f : direction.y);

            foreach (Cell candidate in slotCells)
            {
                if (candidate == null || candidate == originCell)
                {
                    continue;
                }

                Vector2 candidatePosition = new Vector2(candidate.transform.position.x, candidate.transform.position.y);
                Vector2 candidateDelta = candidatePosition - origin;
                float primaryDelta = horizontalMove ? candidateDelta.x : candidateDelta.y;
                float secondaryDelta = horizontalMove ? candidateDelta.y : candidateDelta.x;
                if (Mathf.Sign(primaryDelta) != directionSign || Mathf.Abs(primaryDelta) <= 0.01f)
                {
                    continue;
                }

                float primaryDistance = Mathf.Abs(primaryDelta);
                float secondaryDistance = Mathf.Abs(secondaryDelta);
                const float alignmentTolerance = 0.6f;
                if (secondaryDistance <= alignmentTolerance)
                {
                    bool betterAligned = primaryDistance < bestAlignedPrimary - 0.001f
                        || (Mathf.Abs(primaryDistance - bestAlignedPrimary) <= 0.001f && secondaryDistance < bestAlignedSecondary);
                    if (betterAligned)
                    {
                        bestAlignedPrimary = primaryDistance;
                        bestAlignedSecondary = secondaryDistance;
                        bestCell = candidate;
                    }
                }

                float fallbackScore = secondaryDistance * 3f + primaryDistance;
                if (fallbackScore < bestFallbackScore)
                {
                    bestFallbackScore = fallbackScore;
                    bestFallbackCell = candidate;
                }
            }

            return bestCell ?? bestFallbackCell;
        }

        private static Unit GetPrimaryUnitOnCell(Cell cell)
        {
            if (cell == null || cell.CurrentUnits == null || cell.CurrentUnits.Count == 0)
            {
                return null;
            }

            return cell.CurrentUnits.FirstOrDefault(unit => unit != null);
        }

        private bool ShouldOpenTurnInfoForHoveredCell(Cell cell)
        {
            if (cell == null || cellGrid == null || currentScheme == ControlScheme.Mouse && IsPointerOverUi())
            {
                return false;
            }

            if (cellGrid.IsPreBattlePhase || !cellGrid.IsHumanTurn)
            {
                return false;
            }

            if (hoveredUnit != null)
            {
                return false;
            }

            return cellGrid.CurrentState is Windy.Srpg.Game.Grid.States.CellGridStateWaitingForInput;
        }

        private bool ShouldOpenTurnInfoForHoveredUnit(Unit unit)
        {
            if (unit == null || cellGrid == null || currentScheme == ControlScheme.Mouse && IsPointerOverUi())
            {
                return false;
            }

            if (cellGrid.IsPreBattlePhase || !cellGrid.IsHumanTurn)
            {
                return false;
            }

            if (cellGrid.CurrentState is not Windy.Srpg.Game.Grid.States.CellGridStateWaitingForInput)
            {
                return false;
            }

            return unit.PlayerNumber == cellGrid.CurrentPlayerNumber
                && unit.IsFinishedForTurn;
        }

        private static Vector2Int DirectionDelta(KeyCode key)
        {
            return key switch
            {
                KeyCode.UpArrow => Vector2Int.up,
                KeyCode.DownArrow => Vector2Int.down,
                KeyCode.LeftArrow => Vector2Int.left,
                KeyCode.RightArrow => Vector2Int.right,
                _ => Vector2Int.zero
            };
        }

        private void OnCombatSequenceVisibilityChanged(bool isVisible) => combatSequenceVisible = isVisible;
        private void OnExperienceHudVisibilityChanged(bool isVisible) => experienceHudVisible = isVisible;
        private void OnLevelUpUiVisibilityChanged(bool isVisible) => levelUpUiVisible = isVisible;
    }
}
