using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Windy.Srpg.Game.UI
{
    [DisallowMultipleComponent]
    public sealed class SceneButtonTextOverflowFitter : MonoBehaviour
    {
        [SerializeField] private bool includeInactiveButtons = false;
        [SerializeField] private bool checkContinuously = true;
        [SerializeField] private float checkInterval = 0.1f;
        [SerializeField] private float minimumFontSize = 8f;
        [SerializeField] private Vector2 textPadding = new Vector2(4f, 2f);
        [SerializeField] private int maxFitIterations = 12;

        private readonly Dictionary<TMP_Text, float> originalFontSizes = new();
        private float nextCheckTime;

        private void OnEnable()
        {
            FitAllButtons();
        }

        private void LateUpdate()
        {
            if (!checkContinuously || Time.unscaledTime < nextCheckTime)
            {
                return;
            }

            nextCheckTime = Time.unscaledTime + Mathf.Max(0.02f, checkInterval);
            FitAllButtons();
        }

        [ContextMenu("Fit Button Text Now")]
        public void FitAllButtons()
        {
            Canvas.ForceUpdateCanvases();

            var inactiveMode = includeInactiveButtons
                ? FindObjectsInactive.Include
                : FindObjectsInactive.Exclude;

            foreach (Button button in FindObjectsByType<Button>(inactiveMode))
            {
                if (!IsLoadedSceneButton(button))
                {
                    continue;
                }

                TMP_Text label = GetButtonLabel(button);
                if (label != null)
                {
                    FitText(label, button.transform as RectTransform);
                }
            }
        }

        private static bool IsLoadedSceneButton(Button button)
        {
            return button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.scene.isLoaded;
        }

        private static TMP_Text GetButtonLabel(Button button)
        {
            if (button == null)
            {
                return null;
            }

            TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(includeInactive: false);
            foreach (TMP_Text label in labels)
            {
                if (label != null && label.transform.parent == button.transform)
                {
                    return label;
                }
            }

            return labels.Length > 0 ? labels[0] : null;
        }

        private void FitText(TMP_Text text, RectTransform buttonRectTransform)
        {
            if (text == null || string.IsNullOrEmpty(text.text))
            {
                return;
            }

            RectTransform rectTransform = text.rectTransform;
            if (rectTransform == null)
            {
                return;
            }

            Rect rect = rectTransform.rect;
            if (buttonRectTransform != null)
            {
                Rect buttonRect = buttonRectTransform.rect;
                if (buttonRect.width > rect.width || buttonRect.height > rect.height)
                {
                    rect = new Rect(rect.position, new Vector2(
                        Mathf.Max(rect.width, buttonRect.width),
                        Mathf.Max(rect.height, buttonRect.height)));
                }
            }

            float availableWidth = Mathf.Max(1f, rect.width - textPadding.x * 2f);
            float availableHeight = Mathf.Max(1f, rect.height - textPadding.y * 2f);

            if (availableWidth <= 1f || availableHeight <= 1f)
            {
                return;
            }

            if (!originalFontSizes.TryGetValue(text, out float originalFontSize) || text.fontSize > originalFontSize)
            {
                originalFontSize = text.fontSize;
                originalFontSizes[text] = originalFontSize;
            }

            text.fontSize = originalFontSize;
            if (TextFits(text, availableWidth, availableHeight))
            {
                return;
            }

            float low = Mathf.Clamp(minimumFontSize, 1f, originalFontSize);
            float high = originalFontSize;
            int iterations = Mathf.Max(1, maxFitIterations);

            for (int i = 0; i < iterations; i++)
            {
                float mid = (low + high) * 0.5f;
                text.fontSize = mid;

                if (TextFits(text, availableWidth, availableHeight))
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            text.fontSize = low;
            text.ForceMeshUpdate(true, true);
        }

        private static bool TextFits(TMP_Text text, float availableWidth, float availableHeight)
        {
            text.ForceMeshUpdate(true, true);

            if (text.isTextOverflowing)
            {
                return false;
            }

            Vector2 preferredSize = text.GetPreferredValues(text.text, availableWidth, Mathf.Infinity);
            return preferredSize.x <= availableWidth + 0.5f
                && preferredSize.y <= availableHeight + 0.5f;
        }
    }
}
