using TMPro;
using Windy.Srpg.Game.CameraControl;
using Windy.Srpg.Game.Localization;
using Windy.Srpg.Game.Units;
using Windy.Srpg.Game.Abilities;
using UnityEngine;
using UnityEngine.UI;

namespace Windy.Srpg.Game.UI
{
    public class AttackPreviewUI : MonoBehaviour, CustomMoveAbility.IAttackPreviewUI
    {
        public static event System.Action<bool> VisibilityChanged;

        [System.Serializable]
        public class PreviewPanelBindings
        {
            public GameObject Root;
            public TMP_Text NameText;
            public TMP_Text WeaponText;
            public Button NextWeaponButton;
            public TMP_Text HitPointsText;
            public TMP_Text MitigationText;
            public TMP_Text AttackText;
            public TMP_Text HitText;
            public TMP_Text CritText;
        }

        [Header("References")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform positionTarget;
        [SerializeField] private PreviewPanelBindings leftPanel;
        [SerializeField] private PreviewPanelBindings rightPanel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Canvas canvas;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Vector2 screenPadding = new Vector2(24f, 24f);

        private System.Action _onNextWeapon;
        private System.Action _onConfirm;
        private System.Action _onCancel;
        private RectTransform _rootRectTransform;
        private RectTransform _canvasRectTransform;
        private CustomUnit _attackerUnit;
        private CustomUnit _defenderUnit;

        private void Awake()
        {
            if (root != null)
            {
                _rootRectTransform = root.GetComponent<RectTransform>();
            }

            if (positionTarget == null)
            {
                RectTransform ownRectTransform = transform as RectTransform;
                if (ownRectTransform != null && root != null && root.transform.IsChildOf(transform))
                {
                    positionTarget = ownRectTransform;
                }
                else
                {
                    positionTarget = _rootRectTransform;
                }
            }

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            if (canvas != null)
            {
                _canvasRectTransform = canvas.GetComponent<RectTransform>();
            }

            HookPanelButton(leftPanel);
            HookPanelButton(rightPanel);

            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(() => _onConfirm?.Invoke());
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(() => _onCancel?.Invoke());
            }

            Hide();
        }

        private void LateUpdate()
        {
            if (root == null || !root.activeSelf)
            {
                return;
            }

            RefreshPresentation();
        }

        private void OnEnable()
        {
            CustomUnit.CombatSequenceStarted += OnCombatSequenceStarted;
        }

        private void OnDisable()
        {
            CustomUnit.CombatSequenceStarted -= OnCombatSequenceStarted;
            GameplayCameraController.ClearPreviewUiContainment();
            GameplayCameraController.ClearPreviewUnitVisibility();
        }

        public void Show(
            Vector3 worldPosition,
            CustomUnit attackerUnit,
            CustomUnit defenderUnit,
            CustomMoveAbility.AttackPreviewPanelData attacker,
            CustomMoveAbility.AttackPreviewPanelData defender,
            string attackerActionLabel,
            string attackerActionName,
            string defenderActionLabel,
            string defenderActionName,
            bool canCycleAction,
            System.Action onNextAction,
            System.Action onConfirm,
            System.Action onCancel)
        {
            _onNextWeapon = onNextAction;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _attackerUnit = attackerUnit;
            _defenderUnit = defenderUnit;

            GetOrderedPanels(
                attackerUnit,
                defenderUnit,
                attacker,
                defender,
                out CustomMoveAbility.AttackPreviewPanelData leftData,
                out CustomMoveAbility.AttackPreviewPanelData rightData,
                out bool attackerIsOnLeft);

            string resolvedAttackerActionLabel = string.IsNullOrWhiteSpace(attackerActionLabel) ? GameTextCatalog.Get("ui.common.weapon", "Weapon") : attackerActionLabel;
            string resolvedAttackerActionDisplay = string.IsNullOrWhiteSpace(attackerActionName) ? GameTextCatalog.Get("ui.common.none", "None") : attackerActionName;
            string resolvedDefenderActionLabel = string.IsNullOrWhiteSpace(defenderActionLabel) ? GameTextCatalog.Get("ui.common.weapon", "Weapon") : defenderActionLabel;
            string resolvedDefenderActionDisplay = string.IsNullOrWhiteSpace(defenderActionName) ? GetActionDisplayName(defenderUnit) : defenderActionName;

            ApplyPanel(
                leftPanel,
                leftData,
                attackerIsOnLeft,
                attackerIsOnLeft ? resolvedAttackerActionLabel : resolvedDefenderActionLabel,
                attackerIsOnLeft ? resolvedAttackerActionDisplay : resolvedDefenderActionDisplay,
                canCycleAction);
            ApplyPanel(
                rightPanel,
                rightData,
                !attackerIsOnLeft,
                attackerIsOnLeft ? resolvedDefenderActionLabel : resolvedAttackerActionLabel,
                attackerIsOnLeft ? resolvedDefenderActionDisplay : resolvedAttackerActionDisplay,
                canCycleAction);

            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(true);
                confirmButton.interactable = true;
            }

            if (cancelButton != null)
            {
                cancelButton.gameObject.SetActive(true);
                cancelButton.interactable = true;
            }

            if (root != null)
            {
                root.SetActive(true);
            }

            RefreshPresentation();

            VisibilityChanged?.Invoke(true);
        }

        public void ShowConfirmOnly(Vector3 worldPosition, System.Action onConfirm, System.Action onCancel)
        {
            _onNextWeapon = null;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _attackerUnit = null;
            _defenderUnit = null;

            if (leftPanel?.Root != null)
            {
                leftPanel.Root.SetActive(false);
            }

            if (rightPanel?.Root != null)
            {
                rightPanel.Root.SetActive(false);
            }

            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(true);
                confirmButton.interactable = true;
            }

            if (cancelButton != null)
            {
                cancelButton.gameObject.SetActive(true);
                cancelButton.interactable = true;
            }

            if (root != null)
            {
                root.SetActive(true);
            }

            RefreshPresentation();

            VisibilityChanged?.Invoke(true);
        }

        public void Hide()
        {
            _onNextWeapon = null;
            _onConfirm = null;
            _onCancel = null;
            _attackerUnit = null;
            _defenderUnit = null;

            if (root != null)
            {
                root.SetActive(false);
            }

            GameplayCameraController.ClearPreviewUiContainment();
            GameplayCameraController.ClearPreviewUnitVisibility();
            VisibilityChanged?.Invoke(false);
        }

        private void OnCombatSequenceStarted(object sender, CombatSequenceEventArgs e)
        {
            Hide();
        }

        private void RefreshPresentation()
        {
            UpdatePreviewCameraBias();
        }

        private void ClampRootToCanvas()
        {
            // Intentionally disabled. The attack preview root now stays at the
            // scene-authored canvas position instead of being auto-positioned.
        }

        private void UpdatePreviewCameraBias()
        {
            GameplayCameraController.ClearPreviewUiContainment();

            if (!GameplayCameraController.HasActiveInstance)
            {
                GameplayCameraController.ClearPreviewUnitVisibility();
                return;
            }

            if (!TryBuildPreviewSafeScreenRect(out Rect safeScreenRect))
            {
                GameplayCameraController.ClearPreviewUnitVisibility();
                return;
            }

            GameplayCameraController.SetPreviewUnitVisibility(_attackerUnit, _defenderUnit, safeScreenRect);
        }

        private bool TryBuildPreviewSafeScreenRect(out Rect safeScreenRect)
        {
            safeScreenRect = Rect.zero;
            if (_rootRectTransform == null)
            {
                return false;
            }

            Camera eventCamera = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : (canvas != null && canvas.worldCamera != null ? canvas.worldCamera : (worldCamera != null ? worldCamera : Camera.main));
            Vector3[] corners = new Vector3[4];
            _rootRectTransform.GetWorldCorners(corners);

            float panelTop = float.NegativeInfinity;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 screenCorner = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[i]);
                panelTop = Mathf.Max(panelTop, screenCorner.y);
            }

            if (float.IsNegativeInfinity(panelTop))
            {
                return false;
            }

            float minX = screenPadding.x;
            float maxX = Screen.width - screenPadding.x;
            float minY = Mathf.Clamp(panelTop + screenPadding.y, screenPadding.y, Screen.height - screenPadding.y);
            float maxY = Screen.height - screenPadding.y;
            if (maxX <= minX || maxY <= minY)
            {
                return false;
            }

            safeScreenRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private static void GetOrderedPanels(
            CustomUnit firstUnit,
            CustomUnit secondUnit,
            CustomMoveAbility.AttackPreviewPanelData firstData,
            CustomMoveAbility.AttackPreviewPanelData secondData,
            out CustomMoveAbility.AttackPreviewPanelData leftData,
            out CustomMoveAbility.AttackPreviewPanelData rightData,
            out bool firstUnitOnLeft)
        {
            if (ShouldDisplayFirstUnitOnLeft(firstUnit, secondUnit))
            {
                leftData = firstData;
                rightData = secondData;
                firstUnitOnLeft = true;
                return;
            }

            leftData = secondData;
            rightData = firstData;
            firstUnitOnLeft = false;
        }

        private static bool ShouldDisplayFirstUnitOnLeft(CustomUnit firstUnit, CustomUnit secondUnit)
        {
            if (firstUnit == null)
            {
                return false;
            }

            if (secondUnit == null)
            {
                return true;
            }

            float firstX = GetPreviewOrCurrentX(firstUnit);
            float secondX = GetPreviewOrCurrentX(secondUnit);
            if (!Mathf.Approximately(firstX, secondX))
            {
                return firstX < secondX;
            }

            float firstOriginalX = GetOriginalX(firstUnit);
            float secondOriginalX = GetOriginalX(secondUnit);
            if (!Mathf.Approximately(firstOriginalX, secondOriginalX))
            {
                return firstOriginalX < secondOriginalX;
            }

            bool firstIsAlly = firstUnit.PlayerNumber == 0;
            bool secondIsAlly = secondUnit.PlayerNumber == 0;
            if (firstIsAlly != secondIsAlly)
            {
                return firstIsAlly;
            }

            return true;
        }

        private static float GetPreviewOrCurrentX(CustomUnit unit)
        {
            if (unit == null)
            {
                return 0f;
            }

            if (unit.HasPendingMove && unit.PreviewCell != null)
            {
                return unit.PreviewCell.transform.position.x;
            }

            return unit.transform.position.x;
        }

        private static float GetOriginalX(CustomUnit unit)
        {
            if (unit?.Cell != null)
            {
                return unit.Cell.transform.position.x;
            }

            return unit != null ? unit.transform.position.x : 0f;
        }

        private void HookPanelButton(PreviewPanelBindings panel)
        {
            if (panel?.NextWeaponButton != null)
            {
                panel.NextWeaponButton.onClick.AddListener(() => _onNextWeapon?.Invoke());
            }
        }

        private static string GetActionDisplayName(CustomUnit unit)
        {
            return unit?.EquippedWeapon?.Name ?? GameTextCatalog.Get("ui.common.basic_attack", "Basic Attack");
        }

        private static void ApplyPanel(
            PreviewPanelBindings panel,
            CustomMoveAbility.AttackPreviewPanelData data,
            bool isAttackerPanel,
            string actionLabel,
            string actionName,
            bool canCycleWeapon)
        {
            if (panel == null || panel.Root == null)
            {
                return;
            }

            panel.Root.SetActive(true);

            if (panel.NameText != null)
            {
                panel.NameText.text = GameTextCatalog.Format("ui.common.name_label", "Name: {0}", data.Name);
            }

            if (panel.WeaponText != null)
            {
                panel.WeaponText.text = GameTextCatalog.Format("ui.attack_preview.action_format", "{0}: {1}", actionLabel, actionName);
            }

            if (panel.NextWeaponButton != null)
            {
                panel.NextWeaponButton.gameObject.SetActive(isAttackerPanel);
                panel.NextWeaponButton.interactable = isAttackerPanel && canCycleWeapon;
            }

            if (panel.HitPointsText != null)
            {
                panel.HitPointsText.text = GameTextCatalog.Format("ui.common.hp", "HP: {0}", data.HitPoints);
            }

            if (panel.MitigationText != null)
            {
                panel.MitigationText.gameObject.SetActive(false);
            }

            if (panel.AttackText != null)
            {
                panel.AttackText.text = $"{data.PrimaryStatLabel}: {data.Attack}";
            }

            if (panel.HitText != null)
            {
                panel.HitText.text = GameTextCatalog.Format("ui.common.hit_label", "Hit: {0}", data.Hit);
            }

            if (panel.CritText != null)
            {
                panel.CritText.text = GameTextCatalog.Format("ui.common.crit_label", "Crit: {0}", data.Crit);
            }
        }
    }
}


