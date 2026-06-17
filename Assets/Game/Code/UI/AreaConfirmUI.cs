using TMPro;
using Windy.Srpg.Game.CameraControl;
using Windy.Srpg.Game.Localization;
using Windy.Srpg.Game.Abilities;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Windy.Srpg.Game.UI
{
    public class AreaConfirmUI : MonoBehaviour, MoveAbility.IAreaConfirmUI
    {
        public static event System.Action<bool> VisibilityChanged;

        [SerializeField] private GameObject rootPanel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private GameObject targetPreviewPanel;
        [SerializeField] private RectTransform targetPreviewContent;
        [SerializeField] private TMP_Text targetPreviewRowTemplate;
        [SerializeField] private RectTransform positionTarget;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Canvas canvas;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Vector2 screenOffset = new Vector2(0f, -220f);
        [SerializeField] private Vector2 screenPadding = new Vector2(24f, 24f);
        private System.Action onConfirm;
        private System.Action onCancel;
        private RectTransform panelRectTransform;
        private RectTransform canvasRectTransform;
        private readonly List<TMP_Text> spawnedPreviewRows = new List<TMP_Text>();
        private Vector3 currentWorldPosition;

        private void Awake()
        {
            if (rootPanel != null)
            {
                panelRectTransform = rootPanel.GetComponent<RectTransform>();
                rootPanel.SetActive(false);
            }

            if (positionTarget == null)
            {
                RectTransform ownRectTransform = transform as RectTransform;
                if (ownRectTransform != null && rootPanel != null && rootPanel.transform.IsChildOf(transform))
                {
                    positionTarget = ownRectTransform;
                }
                else
                {
                    positionTarget = panelRectTransform;
                }
            }

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            if (canvas != null)
            {
                canvasRectTransform = canvas.GetComponent<RectTransform>();
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(() => onConfirm?.Invoke());
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(() => onCancel?.Invoke());
            }
        }

        private void LateUpdate()
        {
            if (rootPanel == null || !rootPanel.activeSelf)
            {
                return;
            }

            PositionPanel(currentWorldPosition);
        }

        public void Show(
            Vector3 worldPosition,
            string title,
            string description,
            IReadOnlyList<MoveAbility.AreaConfirmTargetPreviewData> targetPreviews,
            System.Action onConfirm,
            System.Action onCancel)
        {
            this.onConfirm = onConfirm;
            this.onCancel = onCancel;
            currentWorldPosition = worldPosition;

            if (titleText != null)
            {
                titleText.text = string.IsNullOrWhiteSpace(title)
                    ? GameTextCatalog.ResolveSceneText(titleText, "ui.area_confirm.title", "Cast Area Spell?")
                    : title;
            }

            if (descriptionText != null)
            {
                descriptionText.text = string.IsNullOrWhiteSpace(description)
                    ? GameTextCatalog.ResolveSceneText(descriptionText, "ui.area_confirm.description", "Confirm the selected area spell.")
                    : description;
            }

            RebuildTargetPreviewRows(targetPreviews);

            if (rootPanel != null)
            {
                rootPanel.SetActive(true);
            }

            Canvas.ForceUpdateCanvases();
            PositionPanel(worldPosition);

            VisibilityChanged?.Invoke(true);
        }

        public void Hide()
        {
            onConfirm = null;
            onCancel = null;

            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }

            ClearTargetPreviewRows();
            GameplayCameraController.ClearPreviewUiContainment();
            VisibilityChanged?.Invoke(false);
        }

        private void OnDisable()
        {
            GameplayCameraController.ClearPreviewUiContainment();
            VisibilityChanged?.Invoke(false);
        }

        private void PositionPanel(Vector3 worldPosition)
        {
            if (!CanvasClampManager.PositionAtWorldPointUnclamped(
                canvas,
                worldCamera,
                canvasRectTransform,
                positionTarget,
                worldPosition,
                screenOffset,
                out Vector3 screenPoint))
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            Vector2 screenDelta = CanvasClampManager.GetBoundsClampScreenDelta(
                canvas,
                canvasRectTransform,
                panelRectTransform,
                screenPadding);

            if (GameplayCameraController.HasActiveInstance)
            {
                if (screenDelta.sqrMagnitude > 0.25f)
                {
                    GameplayCameraController.SetPreviewUiContainment(screenPoint, screenDelta);
                }
                else
                {
                    GameplayCameraController.ClearPreviewUiContainment();
                }

                return;
            }

            CanvasClampManager.PositionAtWorldPoint(
                canvas,
                worldCamera,
                canvasRectTransform,
                panelRectTransform,
                positionTarget,
                worldPosition,
                screenOffset,
                screenPadding);
        }

        private void RebuildTargetPreviewRows(IReadOnlyList<MoveAbility.AreaConfirmTargetPreviewData> targetPreviews)
        {
            ClearTargetPreviewRows();

            if (targetPreviewPanel == null)
            {
                return;
            }

            if (targetPreviewContent == null || targetPreviewRowTemplate == null || targetPreviews == null || targetPreviews.Count == 0)
            {
                targetPreviewPanel.SetActive(false);
                return;
            }

            targetPreviewPanel.SetActive(true);

            foreach (MoveAbility.AreaConfirmTargetPreviewData preview in targetPreviews)
            {
                TMP_Text row = Instantiate(targetPreviewRowTemplate, targetPreviewContent);
                row.gameObject.SetActive(true);
                row.text = GameTextCatalog.Format("ui.area_confirm.target_row", "{0}: {1} -> {2}", preview.Name, preview.CurrentHitPoints, preview.ProjectedHitPoints);
                row.color = Color.black;
                spawnedPreviewRows.Add(row);
            }

            if (targetPreviewRowTemplate != null)
            {
                targetPreviewRowTemplate.gameObject.SetActive(false);
            }
        }

        private void ClearTargetPreviewRows()
        {
            foreach (TMP_Text row in spawnedPreviewRows)
            {
                if (row != null)
                {
                    Destroy(row.gameObject);
                }
            }

            spawnedPreviewRows.Clear();

            if (targetPreviewPanel != null && targetPreviewContent != null && targetPreviewRowTemplate != null)
            {
                targetPreviewPanel.SetActive(false);
                targetPreviewRowTemplate.gameObject.SetActive(false);
            }
        }

    }
}

