using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using Windy.Srpg.Game.Grid;
using Windy.Srpg.Game.UI;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Runtime.Grid;

namespace Windy.Srpg.Game.CameraControl
{
    public class GameplayCameraController : MonoBehaviour
    {
        private static GameplayCameraController activeInstance;

        public static bool HasActiveInstance { get; private set; }

        [Header("References")]
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private CellGrid cellGrid;

        [Header("Map Bounds")]
        [SerializeField] private float borderPadding = 2f;
        [SerializeField] private bool deriveGroundPlaneFromGrid = true;
        [SerializeField] private float groundPlaneY;

        [Header("Manual Movement")]
        [SerializeField] private bool edgeScrollEnabled;
        [SerializeField] private float screenEdgeSize = 48f;
        [SerializeField] private float edgeScrollOutsideTolerance = 96f;
        [SerializeField] private float edgeScrollSpeed = 10f;
        [SerializeField] private float keyboardMoveSpeed = 10f;
        [SerializeField] private float fastMoveMultiplier = 2.5f;
        [SerializeField] private float dragMoveMultiplier = 3.5f;

        [Header("Focus")]
        [SerializeField] private float focusMoveSpeed = 10f;
        [SerializeField] private float minimumFocusDuration = 0.12f;
        [SerializeField] private float focusCenterTolerancePixels = 48f;
        [SerializeField] private float focusRetargetLeewayTiles = 1f;

        private Vector3 cameraOffset;
        private Vector3 currentFocusPosition;
        private Vector3 focusTargetPosition;
        private bool initialized;

        private bool boundsInitialized;
        private float minFocusX;
        private float maxFocusX;
        private float minFocusY;
        private float maxFocusY;
        private float estimatedTileWorldSize = 1f;
        private bool pendingFocusWithinLeeway;
        private Cell focusLeewayAnchorCell;
        private Vector3 focusLeewayAnchorPosition;

        private bool combatSequenceVisible;
        private bool experienceHudVisible;
        private bool levelUpUiVisible;
        private bool actionMenuVisible;
        private bool attackPreviewVisible;
        private bool combatFocusActive;

        private bool dragActive;
        private Vector3 lastDragScreenPosition;

        public static void SetPreviewUiContainment(Vector3 screenReferencePoint, Vector2 screenDelta)
        {
            // Preview-specific camera bias is disabled in the simplified focus-tile camera.
        }

        public static void ClearPreviewUiContainment()
        {
            // Preview-specific camera bias is disabled in the simplified focus-tile camera.
        }

        public static void SetPreviewUnitVisibility(object primaryUnit, object secondaryUnit, object safeScreenRect)
        {
            // Preview-specific camera bias is disabled in the simplified focus-tile camera.
        }

        public static void ClearPreviewUnitVisibility()
        {
            // Preview-specific camera bias is disabled in the simplified focus-tile camera.
        }

        public static void SetFocusedCell(Cell cell)
        {
            if (activeInstance == null || cell == null)
            {
                return;
            }

            activeInstance.SetFocusTarget(cell.transform.position);
        }

        public static void SetFocusedWorldPosition(Vector3 worldPosition)
        {
            if (activeInstance == null)
            {
                return;
            }

            activeInstance.SetFocusTarget(worldPosition);
        }

        public static Cell GetFocusedCell()
        {
            if (activeInstance == null || !activeInstance.TryInitialize())
            {
                return null;
            }

            return activeInstance.FindNearestCell(activeInstance.focusTargetPosition);
        }

        public static IEnumerator WaitForFocusSettled(float timeoutSeconds = 2f)
        {
            if (activeInstance == null)
            {
                yield break;
            }

            yield return activeInstance.WaitForFocusSettledRoutine(timeoutSeconds);
        }

        private void OnEnable()
        {
            TryInitialize();
            activeInstance = this;
            HasActiveInstance = true;

            UnitInspectPanelUI.SelectionTargetChanged += OnSelectionTargetChanged;
            UnitInspectPanelUI.InspectTargetChanged += OnInspectTargetChanged;
            ActionMenuUI.VisibilityChanged += OnActionMenuVisibilityChanged;
            AttackPreviewUI.VisibilityChanged += OnAttackPreviewVisibilityChanged;
            CombatSequenceUI.VisibilityChanged += OnCombatSequenceVisibilityChanged;
            ExperienceGainHUD.VisibilityChanged += OnExperienceHudVisibilityChanged;
            LevelUpUI.VisibilityChanged += OnLevelUpUiVisibilityChanged;
            Unit.CombatCameraFocusRequested += OnCombatCameraFocusRequested;
            Unit.CombatCameraFocusReleased += OnCombatCameraFocusReleased;

            if (cellGrid != null)
            {
                cellGrid.LevelInitialized += OnLevelLoadingDone;
            }
        }

        private void OnDisable()
        {
            if (activeInstance == this)
            {
                activeInstance = null;
            }

            HasActiveInstance = false;
            UnitInspectPanelUI.SelectionTargetChanged -= OnSelectionTargetChanged;
            UnitInspectPanelUI.InspectTargetChanged -= OnInspectTargetChanged;
            ActionMenuUI.VisibilityChanged -= OnActionMenuVisibilityChanged;
            AttackPreviewUI.VisibilityChanged -= OnAttackPreviewVisibilityChanged;
            CombatSequenceUI.VisibilityChanged -= OnCombatSequenceVisibilityChanged;
            ExperienceGainHUD.VisibilityChanged -= OnExperienceHudVisibilityChanged;
            LevelUpUI.VisibilityChanged -= OnLevelUpUiVisibilityChanged;
            Unit.CombatCameraFocusRequested -= OnCombatCameraFocusRequested;
            Unit.CombatCameraFocusReleased -= OnCombatCameraFocusReleased;

            if (cellGrid != null)
            {
                cellGrid.LevelInitialized -= OnLevelLoadingDone;
            }
        }

        private void LateUpdate()
        {
            if (!TryInitialize())
            {
                return;
            }

            if (IsCameraMovementLocked())
            {
                if (ShouldMoveFocusWhileLocked())
                {
                    MoveTowardsFocusedTile(Time.unscaledDeltaTime);
                    currentFocusPosition = ClampFocusToBounds(currentFocusPosition);
                    focusTargetPosition = ClampFocusToBounds(focusTargetPosition);
                }

                ApplyCameraPosition();
                return;
            }

            HandleManualMovement(Time.unscaledDeltaTime);
            MoveTowardsFocusedTile(Time.unscaledDeltaTime);
            currentFocusPosition = ClampFocusToBounds(currentFocusPosition);
            focusTargetPosition = ClampFocusToBounds(focusTargetPosition);
            ApplyCameraPosition();
        }

        private void OnLevelLoadingDone(object sender, EventArgs e)
        {
            RecalculateBounds();
            currentFocusPosition = ClampFocusToBounds(currentFocusPosition);
            focusTargetPosition = ClampFocusToBounds(focusTargetPosition);
            UpdateFocusLeewayAnchor(focusTargetPosition);
            ApplyCameraPosition();
        }

        private bool TryInitialize()
        {
            if (initialized && controlledCamera != null)
            {
                return true;
            }

            controlledCamera = controlledCamera != null ? controlledCamera : GetComponent<Camera>();
            if (controlledCamera == null)
            {
                controlledCamera = Camera.main;
            }

            if (controlledCamera == null)
            {
                return false;
            }

            if (cellGrid == null)
            {
                cellGrid = FindAnyObjectByType<CellGrid>();
            }

            if (cellGrid != null && deriveGroundPlaneFromGrid)
            {
                Cell firstCell = cellGrid.GetAllCells().FirstOrDefault(cell => cell != null);
                if (firstCell != null)
                {
                    groundPlaneY = firstCell.transform.position.z;
                }
            }

            Vector3 initialFocus = GetFallbackFocusPosition();
            currentFocusPosition = ToFocusPlane(initialFocus);
            focusTargetPosition = currentFocusPosition;
            cameraOffset = controlledCamera.transform.position - currentFocusPosition;
            RecalculateBounds();
            currentFocusPosition = ClampFocusToBounds(currentFocusPosition);
            focusTargetPosition = ClampFocusToBounds(focusTargetPosition);
            UpdateFocusLeewayAnchor(focusTargetPosition);
            ApplyCameraPosition();
            initialized = true;
            return true;
        }

        private void HandleManualMovement(float deltaTime)
        {
            HandleDragState();

            Vector3 directionalMove = GetKeyboardAndEdgeMoveDirection();
            if (directionalMove.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float speed = Mathf.Max(edgeScrollSpeed, keyboardMoveSpeed);
            if (Input.GetKey(KeyCode.Space))
            {
                speed *= Mathf.Max(1f, fastMoveMultiplier);
            }

            focusTargetPosition += directionalMove.normalized * speed * deltaTime;
            focusTargetPosition = ClampFocusToBounds(focusTargetPosition);
            pendingFocusWithinLeeway = false;
            UpdateFocusLeewayAnchor(focusTargetPosition);
        }

        private void HandleDragState()
        {
            if (!IsManualCameraInputAllowed())
            {
                dragActive = false;
                return;
            }

            if (Input.GetMouseButtonDown(0) && !IsPointerOverUi() && TryGetUnoccupiedCellUnderPointer(out _))
            {
                dragActive = true;
                lastDragScreenPosition = Input.mousePosition;
            }

            if (dragActive && Input.GetMouseButton(0))
            {
                Vector3 screenDelta = Input.mousePosition - lastDragScreenPosition;
                Vector3 delta = GetDragMoveDelta(screenDelta);
                if (delta.sqrMagnitude > 0.0001f)
                {
                    Vector3 previousFocusTarget = focusTargetPosition;
                    focusTargetPosition = ClampFocusToBounds(focusTargetPosition + delta);
                    Vector3 appliedDelta = focusTargetPosition - previousFocusTarget;
                    currentFocusPosition = ClampFocusToBounds(currentFocusPosition + appliedDelta);
                    pendingFocusWithinLeeway = false;
                    UpdateFocusLeewayAnchor(focusTargetPosition);
                }

                lastDragScreenPosition = Input.mousePosition;
            }

            if (Input.GetMouseButtonUp(0))
            {
                dragActive = false;
            }
        }

        private Vector3 GetDragMoveDelta(Vector3 screenDelta)
        {
            Vector3 planarRight = Vector3.ProjectOnPlane(controlledCamera.transform.right, Vector3.forward).normalized;
            Vector3 planarUp = Vector3.ProjectOnPlane(controlledCamera.transform.up, Vector3.forward).normalized;

            float visibleHeight = controlledCamera != null && controlledCamera.orthographic
                ? controlledCamera.orthographicSize * 2f
                : Mathf.Max(1f, keyboardMoveSpeed);
            float visibleWidth = visibleHeight * (controlledCamera != null ? controlledCamera.aspect : 1f);

            float normalizedX = Screen.width > 0 ? screenDelta.x / Screen.width : 0f;
            float normalizedY = Screen.height > 0 ? screenDelta.y / Screen.height : 0f;

            Vector3 delta = (-normalizedX * visibleWidth * planarRight) + (-normalizedY * visibleHeight * planarUp);
            delta.z = 0f;
            return delta * Mathf.Max(0f, dragMoveMultiplier);
        }

        private Vector3 GetKeyboardAndEdgeMoveDirection()
        {
            if (!IsManualCameraInputAllowed())
            {
                return Vector3.zero;
            }

            Vector3 planarRight = Vector3.ProjectOnPlane(controlledCamera.transform.right, Vector3.forward).normalized;
            Vector3 planarUp = Vector3.ProjectOnPlane(controlledCamera.transform.up, Vector3.forward).normalized;
            Vector3 direction = Vector3.zero;

            if (edgeScrollEnabled && !IsPointerOverUi() && TryGetEdgeScrollMousePosition(out Vector3 mousePosition))
            {
                if (mousePosition.x <= screenEdgeSize)
                {
                    direction -= planarRight;
                }
                else if (mousePosition.x >= Screen.width - screenEdgeSize)
                {
                    direction += planarRight;
                }

                if (mousePosition.y <= screenEdgeSize)
                {
                    direction -= planarUp;
                }
                else if (mousePosition.y >= Screen.height - screenEdgeSize)
                {
                    direction += planarUp;
                }
            }

            direction.z = 0f;
            return direction;
        }

        private void MoveTowardsFocusedTile(float deltaTime)
        {
            if (pendingFocusWithinLeeway)
            {
                return;
            }

            if (!TryBuildCenteredCameraTarget(focusTargetPosition, out Vector3 centeredTarget))
            {
                UpdateFocusLeewayAnchor(focusTargetPosition);
                return;
            }

            Vector3 clampedTarget = ClampFocusToBounds(centeredTarget);
            float distance = Vector3.Distance(currentFocusPosition, clampedTarget);
            if (distance <= 0.0001f)
            {
                UpdateFocusLeewayAnchor(focusTargetPosition);
                return;
            }

            currentFocusPosition = Vector3.MoveTowards(
                currentFocusPosition,
                clampedTarget,
                GetAdjustedFocusSpeed(distance) * deltaTime);

            if (Vector3.Distance(currentFocusPosition, clampedTarget) <= 0.0001f)
            {
                UpdateFocusLeewayAnchor(focusTargetPosition);
            }
        }

        private float GetAdjustedFocusSpeed(float distance)
        {
            float baseSpeed = Mathf.Max(0f, focusMoveSpeed);
            float minDuration = Mathf.Max(0f, minimumFocusDuration);
            if (baseSpeed <= 0f || minDuration <= 0f || distance <= 0f)
            {
                return baseSpeed;
            }

            float maxSpeedForMinimumDuration = distance / minDuration;
            return Mathf.Min(baseSpeed, maxSpeedForMinimumDuration);
        }

        private bool TryBuildCenteredCameraTarget(Vector3 focusTarget, out Vector3 centeredTarget)
        {
            centeredTarget = currentFocusPosition;
            if (controlledCamera == null)
            {
                return false;
            }

            Vector3 focusScreenPoint = controlledCamera.WorldToScreenPoint(ToFocusPlane(focusTarget));
            if (focusScreenPoint.z <= 0f)
            {
                return false;
            }

            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 screenShift = screenCenter - new Vector2(focusScreenPoint.x, focusScreenPoint.y);
            float tolerancePixels = Mathf.Max(0f, focusCenterTolerancePixels);
            if (screenShift.sqrMagnitude <= tolerancePixels * tolerancePixels)
            {
                return false;
            }

            if (!TryGetScreenGroundPoint(focusScreenPoint, out Vector3 currentGroundPoint)
                || !TryGetScreenGroundPoint(focusScreenPoint + (Vector3)screenShift, out Vector3 desiredGroundPoint))
            {
                return false;
            }

            Vector3 centeringOffset = currentGroundPoint - desiredGroundPoint;
            centeringOffset.z = 0f;
            centeredTarget = ToFocusPlane(currentFocusPosition + centeringOffset);
            return true;
        }

        private void ApplyCameraPosition()
        {
            if (controlledCamera == null)
            {
                return;
            }

            controlledCamera.transform.position = currentFocusPosition + cameraOffset;
        }

        private void SetFocusTarget(Vector3 worldPosition)
        {
            if (!TryInitialize())
            {
                return;
            }

            Vector3 nextFocusTarget = ClampFocusToBounds(ToFocusPlane(worldPosition));
            focusTargetPosition = nextFocusTarget;
            pendingFocusWithinLeeway = IsWithinRetargetLeeway(nextFocusTarget);
        }

        private Vector3 ResolveUnitFocusPosition(Unit unit)
        {
            if (unit == null)
            {
                return focusTargetPosition;
            }

            Cell focusCell = unit.HasPendingMove ? unit.PreviewCell : unit.Cell;
            if (focusCell != null)
            {
                return ToFocusPlane(focusCell.transform.position);
            }

            return ToFocusPlane(unit.transform.position);
        }

        private void RecalculateBounds()
        {
            List<Cell> cells = cellGrid?.GetAllCells();
            if (cells == null || cells.Count == 0)
            {
                boundsInitialized = false;
                estimatedTileWorldSize = 1f;
                return;
            }

            bool hasAnyCell = false;
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            List<float> xPositions = new List<float>();
            List<float> yPositions = new List<float>();

            foreach (Cell cell in cells)
            {
                if (cell == null)
                {
                    continue;
                }

                Vector3 position = cell.transform.position;
                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
                minY = Mathf.Min(minY, position.y);
                maxY = Mathf.Max(maxY, position.y);
                xPositions.Add(position.x);
                yPositions.Add(position.y);
                hasAnyCell = true;
            }

            if (!hasAnyCell)
            {
                boundsInitialized = false;
                estimatedTileWorldSize = 1f;
                return;
            }

            minFocusX = minX - borderPadding;
            maxFocusX = maxX + borderPadding;
            minFocusY = minY - borderPadding;
            maxFocusY = maxY + borderPadding;
            estimatedTileWorldSize = EstimateTileWorldSize(xPositions, yPositions);
            boundsInitialized = true;
        }

        private bool IsWithinRetargetLeeway(Vector3 candidateFocusTarget)
        {
            float leewayTiles = Mathf.Max(0f, focusRetargetLeewayTiles);
            if (leewayTiles <= 0f)
            {
                return false;
            }

            Cell candidateCell = FindNearestCell(candidateFocusTarget);
            if (focusLeewayAnchorCell != null && candidateCell != null)
            {
                int leewayCellDistance = Mathf.FloorToInt(leewayTiles);
                if (leewayCellDistance <= 0)
                {
                    return false;
                }

                return focusLeewayAnchorCell.GetDistance(candidateCell) <= leewayCellDistance;
            }

            float tileSize = Mathf.Max(0.0001f, estimatedTileWorldSize);
            float leewayDistance = tileSize * leewayTiles;
            Vector2 currentCenter = new Vector2(focusLeewayAnchorPosition.x, focusLeewayAnchorPosition.y);
            Vector2 candidateCenter = new Vector2(candidateFocusTarget.x, candidateFocusTarget.y);
            return Vector2.Distance(currentCenter, candidateCenter) <= leewayDistance;
        }

        private void UpdateFocusLeewayAnchor(Vector3 worldPosition)
        {
            focusLeewayAnchorPosition = worldPosition;
            focusLeewayAnchorCell = FindNearestCell(worldPosition);
        }

        private Cell FindNearestCell(Vector3 worldPosition)
        {
            List<Cell> cells = cellGrid?.GetAllCells();
            if (cells == null || cells.Count == 0)
            {
                return null;
            }

            Cell nearestCell = null;
            float nearestDistanceSqr = float.PositiveInfinity;
            foreach (Cell cell in cells)
            {
                if (cell == null)
                {
                    continue;
                }

                float distanceSqr = (ToFocusPlane(cell.transform.position) - ToFocusPlane(worldPosition)).sqrMagnitude;
                if (distanceSqr >= nearestDistanceSqr)
                {
                    continue;
                }

                nearestDistanceSqr = distanceSqr;
                nearestCell = cell;
            }

            return nearestCell;
        }

        private static float EstimateTileWorldSize(List<float> xPositions, List<float> yPositions)
        {
            float xSpacing = GetMinimumPositiveSpacing(xPositions);
            float ySpacing = GetMinimumPositiveSpacing(yPositions);

            if (xSpacing > 0f && ySpacing > 0f)
            {
                return Mathf.Min(xSpacing, ySpacing);
            }

            if (xSpacing > 0f)
            {
                return xSpacing;
            }

            if (ySpacing > 0f)
            {
                return ySpacing;
            }

            return 1f;
        }

        private static float GetMinimumPositiveSpacing(List<float> values)
        {
            if (values == null || values.Count < 2)
            {
                return 0f;
            }

            values.Sort();
            float minimumSpacing = float.PositiveInfinity;
            for (int i = 1; i < values.Count; i++)
            {
                float spacing = Mathf.Abs(values[i] - values[i - 1]);
                if (spacing <= 0.001f)
                {
                    continue;
                }

                minimumSpacing = Mathf.Min(minimumSpacing, spacing);
            }

            return float.IsInfinity(minimumSpacing) ? 0f : minimumSpacing;
        }

        private Vector3 ClampFocusToBounds(Vector3 focus)
        {
            focus.z = groundPlaneY;
            if (!boundsInitialized)
            {
                return focus;
            }

            focus.x = Mathf.Clamp(focus.x, minFocusX, maxFocusX);
            focus.y = Mathf.Clamp(focus.y, minFocusY, maxFocusY);
            return focus;
        }

        private Vector3 GetFallbackFocusPosition()
        {
            if (controlledCamera == null)
            {
                return Vector3.zero;
            }

            return new Vector3(controlledCamera.transform.position.x, controlledCamera.transform.position.y, groundPlaneY);
        }

        private Vector3 ToFocusPlane(Vector3 worldPosition)
        {
            worldPosition.z = groundPlaneY;
            return worldPosition;
        }

        private bool TryGetScreenGroundPoint(Vector3 screenPoint, out Vector3 worldPoint)
        {
            if (controlledCamera == null)
            {
                worldPoint = Vector3.zero;
                return false;
            }

            Ray ray = controlledCamera.ScreenPointToRay(screenPoint);
            return TryIntersectGroundPlane(ray, out worldPoint);
        }

        private bool TryIntersectGroundPlane(Ray ray, out Vector3 worldPoint)
        {
            Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, groundPlaneY));
            if (plane.Raycast(ray, out float distance))
            {
                worldPoint = ray.GetPoint(distance);
                worldPoint.z = groundPlaneY;
                return true;
            }

            worldPoint = Vector3.zero;
            return false;
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private bool TryGetEdgeScrollMousePosition(out Vector3 mousePosition)
        {
            mousePosition = Input.mousePosition;
            float outsideTolerance = Mathf.Max(0f, edgeScrollOutsideTolerance);

            return mousePosition.x >= -outsideTolerance
                && mousePosition.x <= Screen.width + outsideTolerance
                && mousePosition.y >= -outsideTolerance
                && mousePosition.y <= Screen.height + outsideTolerance;
        }

        private bool IsManualCameraInputAllowed()
        {
            return !IsCameraMovementLocked();
        }

        private bool ShouldMoveFocusWhileLocked()
        {
            return combatFocusActive;
        }

        private bool IsCameraMovementLocked()
        {
            return actionMenuVisible
                || attackPreviewVisible
                || combatSequenceVisible
                || experienceHudVisible
                || levelUpUiVisible;
        }

        private bool TryGetUnoccupiedCellUnderPointer(out Cell cell)
        {
            cell = null;
            if (controlledCamera == null)
            {
                return false;
            }

            Ray ray = controlledCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, float.MaxValue);
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

                Cell hitCell = hit.collider.GetComponentInParent<Cell>();
                if (hitCell == null)
                {
                    continue;
                }

                if (hitCell.CurrentUnits != null && hitCell.CurrentUnits.Count > 0)
                {
                    return false;
                }

                cell = hitCell;
                return true;
            }

            return false;
        }

        private void OnSelectionTargetChanged(Unit unit)
        {
            if (unit != null)
            {
                SetFocusTarget(ResolveUnitFocusPosition(unit));
            }
        }

        private void OnInspectTargetChanged(Unit unit)
        {
            if (unit != null)
            {
                SetFocusTarget(ResolveUnitFocusPosition(unit));
            }
        }

        private void OnCombatSequenceVisibilityChanged(bool isVisible)
        {
            combatSequenceVisible = isVisible;
        }

        private void OnActionMenuVisibilityChanged(bool isVisible)
        {
            actionMenuVisible = isVisible;
        }

        private void OnAttackPreviewVisibilityChanged(bool isVisible)
        {
            attackPreviewVisible = isVisible;
        }

        private void OnExperienceHudVisibilityChanged(bool isVisible)
        {
            experienceHudVisible = isVisible;
        }

        private void OnLevelUpUiVisibilityChanged(bool isVisible)
        {
            levelUpUiVisible = isVisible;
        }

        private void OnCombatCameraFocusRequested(Vector3 worldPosition)
        {
            combatFocusActive = true;
            SetFocusTarget(worldPosition);
        }

        private void OnCombatCameraFocusReleased()
        {
            combatFocusActive = false;
        }

        private IEnumerator WaitForFocusSettledRoutine(float timeoutSeconds)
        {
            if (!TryInitialize())
            {
                yield break;
            }

            float elapsedSeconds = 0f;
            float clampedTimeoutSeconds = Mathf.Max(0f, timeoutSeconds);
            while (!IsFocusSettled())
            {
                if (clampedTimeoutSeconds > 0f && elapsedSeconds >= clampedTimeoutSeconds)
                {
                    yield break;
                }

                elapsedSeconds += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private bool IsFocusSettled()
        {
            if (!TryInitialize())
            {
                return true;
            }

            if (pendingFocusWithinLeeway)
            {
                return true;
            }

            if (!TryBuildCenteredCameraTarget(focusTargetPosition, out Vector3 centeredTarget))
            {
                return true;
            }

            Vector3 clampedCurrent = ClampFocusToBounds(currentFocusPosition);
            Vector3 clampedTarget = ClampFocusToBounds(centeredTarget);
            return Vector3.Distance(clampedCurrent, clampedTarget) <= 0.01f;
        }
    }
}

