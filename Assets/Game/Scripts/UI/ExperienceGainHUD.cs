using System.Collections;
using TMPro;
using Windy.Srpg.Game.Localization;
using Windy.Srpg.Game.Units;
using UnityEngine;
using UnityEngine.UI;

namespace Windy.Srpg.Game.UI
{
    [AddComponentMenu("UI/Experience Gain HUD")]
    public class ExperienceGainHUD : MonoBehaviour
    {
        public static event System.Action<bool> VisibilityChanged;

        [Header("References")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text experienceText;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Canvas canvas;
        [SerializeField] private Camera worldCamera;

        [Header("Positioning")]
        [SerializeField] private Vector2 screenOffset = new Vector2(0f, -180f);
        [SerializeField] private Vector2 screenPadding = new Vector2(24f, 24f);

        [Header("Animation")]
        [SerializeField] private Color fillColor = new Color32(90, 220, 120, 255);
        [SerializeField] private float expFillSpeedPerSecond = 120f;
        [SerializeField] private float startDelaySeconds = 0.15f;
        [SerializeField] private float holdDelaySeconds = 0.4f;
        [SerializeField] private float levelUpDelaySeconds = 0.15f;

        [Header("Editor Preview")]
        [SerializeField] private bool showEditorPreview = false;
        [SerializeField] private string previewUnitName = string.Empty;
        [SerializeField] [Range(0, 99)] private int previewExperience = 37;

        private RectTransform rootRectTransform;
        private RectTransform canvasRectTransform;

        public float LevelUpDelaySeconds => Mathf.Max(0f, levelUpDelaySeconds);

        private void Awake()
        {
            if (root != null)
            {
                rootRectTransform = root.GetComponent<RectTransform>();
            }

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            if (canvas != null)
            {
                canvasRectTransform = canvas.GetComponent<RectTransform>();
            }

            HideImmediate();
        }

        private void OnValidate()
        {
            if (root != null)
            {
                rootRectTransform = root.GetComponent<RectTransform>();
            }

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            if (canvas != null)
            {
                canvasRectTransform = canvas.GetComponent<RectTransform>();
            }

            if (fillImage != null)
            {
                fillImage.color = fillColor;
            }

            if (backgroundImage != null)
            {
                backgroundImage.enabled = true;
            }

            if (Application.isPlaying || root == null)
            {
                return;
            }

            if (!showEditorPreview)
            {
                return;
            }

            if (nameText != null)
            {
                nameText.text = string.IsNullOrWhiteSpace(previewUnitName)
                    ? GameTextCatalog.Get("ui.common.preview_unit", "Preview Unit")
                    : previewUnitName;
            }

            SetDisplayedExperience(previewExperience);
        }

        private void OnDisable()
        {
            VisibilityChanged?.Invoke(false);
        }

        public IEnumerator ShowAndWait(CustomUnit unit, ExperienceAwardResult award)
        {
            if (unit == null || award == null || root == null)
            {
                yield break;
            }

            root.SetActive(true);
            VisibilityChanged?.Invoke(true);
            if (fillImage != null)
            {
                fillImage.color = fillColor;
            }

            if (backgroundImage != null)
            {
                backgroundImage.enabled = true;
            }

            if (nameText != null)
            {
                nameText.text = unit.unitName;
            }

            PositionRoot(unit.transform.position);
            SetDisplayedExperience(award.OldExperience);

            if (startDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(startDelaySeconds);
            }

            foreach (ExperienceBarSegment segment in award.BarSegments)
            {
                yield return AnimateSegment(segment);
            }

            if (holdDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(holdDelaySeconds);
            }

            HideImmediate();
        }

        private IEnumerator AnimateSegment(ExperienceBarSegment segment)
        {
            float currentValue = segment.StartExperience;
            int targetValue = segment.EndExperience;

            if (segment.StartExperience == ExperienceCalculator.MaxGain && segment.EndExperience == 0)
            {
                SetDisplayedExperience(0);
                yield break;
            }

            if (Mathf.Approximately(currentValue, targetValue))
            {
                SetDisplayedExperience(targetValue == ExperienceCalculator.MaxGain ? 0 : targetValue);
                yield break;
            }

            float speed = Mathf.Max(0.01f, expFillSpeedPerSecond);

            while (currentValue < targetValue)
            {
                currentValue = Mathf.MoveTowards(currentValue, targetValue, speed * Time.deltaTime);

                SetDisplayedExperience(currentValue);
                yield return null;
            }

            if (targetValue >= ExperienceCalculator.MaxGain)
            {
                SetDisplayedExperience(0);
            }
        }

        private void SetDisplayedExperience(float experienceValue)
        {
            float clampedValue = Mathf.Clamp(experienceValue, 0f, ExperienceCalculator.MaxGain);
            if (fillImage != null)
            {
                fillImage.fillAmount = clampedValue / (float)ExperienceCalculator.MaxGain;
            }

            if (experienceText != null)
            {
                int displayedValue = Mathf.Clamp(Mathf.FloorToInt(clampedValue), 0, ExperienceCalculator.MaxGain - 1);
                experienceText.text = displayedValue.ToString();
            }
        }

        private void HideImmediate()
        {
            if (root != null)
            {
                root.SetActive(false);
            }

            VisibilityChanged?.Invoke(false);
        }

        private void PositionRoot(Vector3 worldPosition)
        {
            CanvasClampManager.PositionAtWorldPoint(
                canvas,
                worldCamera,
                canvasRectTransform,
                rootRectTransform,
                rootRectTransform,
                worldPosition,
                screenOffset,
                screenPadding);
        }
    }
}
