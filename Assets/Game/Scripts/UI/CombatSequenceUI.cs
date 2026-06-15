using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Windy.Srpg.Game.Localization;
using Windy.Srpg.Game.Units;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Windy.Srpg.Game.UI
{
    public class CombatSequenceUI : MonoBehaviour
    {
        public static event Action<bool> VisibilityChanged;
        public static bool IsVisible { get; private set; }

        public enum PreviewFaction
        {
            Friendly,
            Enemy,
            Neutral
        }

        [System.Serializable]
        public class UnitPanelBindings
        {
            public GameObject Root;
            public TMP_Text NameText;
            public TMP_Text HitPointsText;
            public Image BackgroundImage;
            public Image FillImage;

            [System.NonSerialized] public Texture2D RuntimeGradientTexture;
            [System.NonSerialized] public Sprite RuntimeGradientSprite;
            [System.NonSerialized] public Color LastGradientStartColor = new Color(-1f, -1f, -1f, -1f);
            [System.NonSerialized] public Color LastGradientEndColor = new Color(-1f, -1f, -1f, -1f);
        }

        [Serializable]
        public class PreviewUnitData
        {
            public string Name = "Unit";
            public int HitPoints = 20;
            public int MaxHitPoints = 30;
            public PreviewFaction Faction = PreviewFaction.Friendly;
        }

        [Header("References")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform positionTarget;
        [SerializeField] private UnitPanelBindings leftPanel;
        [SerializeField] private UnitPanelBindings rightPanel;
        [SerializeField] private Canvas canvas;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Vector2 screenOffset = new Vector2(-200f, -300f);
        [SerializeField] private Vector2 screenPadding = new Vector2(24f, 24f);

        [Header("Behavior")]
        [SerializeField] private float hideDelaySeconds = 0.75f;
        [SerializeField] private int gradientMaxHitPoints = 100;
        [SerializeField] private Color missingHealthColor = Color.black;

        [Header("Bar Colors")]
        [SerializeField] private Color friendlyStartColor = new Color32(120, 185, 255, 255);
        [SerializeField] private Color friendlyEndColor = new Color32(125, 20, 210, 255);
        [SerializeField] private Color enemyStartColor = new Color32(245, 70, 70, 255);
        [SerializeField] private Color enemyEndColor = new Color32(125, 20, 210, 255);
        [SerializeField] private Color neutralStartColor = new Color32(150, 235, 150, 255);
        [SerializeField] private Color neutralEndColor = new Color32(200, 170, 30, 255);

        [Header("Editor Preview")]
        [SerializeField] private bool showEditorPreview = false;
        [SerializeField] private PreviewUnitData previewLeftUnit = new PreviewUnitData
        {
            Name = "Ally",
            HitPoints = 24,
            MaxHitPoints = 30,
            Faction = PreviewFaction.Friendly
        };
        [SerializeField] private PreviewUnitData previewRightUnit = new PreviewUnitData
        {
            Name = "Enemy",
            HitPoints = 18,
            MaxHitPoints = 28,
            Faction = PreviewFaction.Enemy
        };

        private CustomUnit _attacker;
        private CustomUnit _defender;
        private Coroutine _hideCoroutine;
        private RectTransform _rootRectTransform;
        private RectTransform _canvasRectTransform;

        private const int GradientTextureWidth = 32;

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

            HideImmediate();
        }

        private void OnValidate()
        {
            if (root != null)
            {
                _rootRectTransform = root.GetComponent<RectTransform>();
            }

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            if (canvas != null)
            {
                _canvasRectTransform = canvas.GetComponent<RectTransform>();
            }

            if (Application.isPlaying || !showEditorPreview)
            {
                return;
            }

            ApplyPreviewData();
        }

        private void OnEnable()
        {
            CustomUnit.CombatSequenceStarted += OnCombatSequenceStarted;
            CustomUnit.CombatSequenceEnded += OnCombatSequenceEnded;
        }

        private void OnDisable()
        {
            IsVisible = false;
            CustomUnit.CombatSequenceStarted -= OnCombatSequenceStarted;
            CustomUnit.CombatSequenceEnded -= OnCombatSequenceEnded;
            UnsubscribeFromUnitEvents(_attacker);
            UnsubscribeFromUnitEvents(_defender);
            DestroyPanelGradient(leftPanel);
            DestroyPanelGradient(rightPanel);
            VisibilityChanged?.Invoke(false);
        }

        private void OnCombatSequenceStarted(object sender, CombatSequenceEventArgs e)
        {
            if (e == null || e.Attacker == null || e.Defender == null)
            {
                return;
            }

            if (_hideCoroutine != null)
            {
                StopCoroutine(_hideCoroutine);
                _hideCoroutine = null;
            }

            SetCombatants(e.Attacker, e.Defender);
            RefreshPanels();
            PositionRoot(e.Defender.transform.position);

            if (root != null)
            {
                root.SetActive(true);
            }

            IsVisible = true;
            VisibilityChanged?.Invoke(true);
        }

        private void OnCombatSequenceEnded(object sender, CombatSequenceEventArgs e)
        {
            if (_hideCoroutine != null)
            {
                StopCoroutine(_hideCoroutine);
            }

            _hideCoroutine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            if (hideDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(hideDelaySeconds);
            }

            HideImmediate();
            _hideCoroutine = null;
        }

        private void SetCombatants(CustomUnit attacker, CustomUnit defender)
        {
            if (_attacker == attacker && _defender == defender)
            {
                return;
            }

            UnsubscribeFromUnitEvents(_attacker);
            UnsubscribeFromUnitEvents(_defender);

            _attacker = attacker;
            _defender = defender;

            SubscribeToUnitEvents(_attacker);
            SubscribeToUnitEvents(_defender);
        }

        private void SubscribeToUnitEvents(CustomUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            unit.UnitHealthChanged += OnUnitHealthChanged;
            unit.DestroyedInCombat += OnUnitDestroyed;
        }

        private void UnsubscribeFromUnitEvents(CustomUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            unit.UnitHealthChanged -= OnUnitHealthChanged;
            unit.DestroyedInCombat -= OnUnitDestroyed;
        }

        private void OnUnitHealthChanged(object sender, UnitHealthChangedEventArgs e)
        {
            RefreshPanels();
        }

        private void OnUnitDestroyed(object sender, CustomUnitDestroyedEventArgs e)
        {
            RefreshPanels();

            if (_hideCoroutine != null)
            {
                StopCoroutine(_hideCoroutine);
            }

            _hideCoroutine = StartCoroutine(HideAfterDelay());
        }

        private void RefreshPanels()
        {
            GetOrderedCombatants(out CustomUnit leftUnit, out CustomUnit rightUnit);
            RefreshPanel(leftPanel, leftUnit);
            RefreshPanel(rightPanel, rightUnit);
        }

        private void ApplyPreviewData()
        {
            RefreshPreviewPanel(leftPanel, previewLeftUnit);
            RefreshPreviewPanel(rightPanel, previewRightUnit);
        }

        private void RefreshPanel(UnitPanelBindings panel, CustomUnit unit)
        {
            if (panel == null || panel.Root == null)
            {
                return;
            }

            panel.Root.SetActive(unit != null);
            if (unit == null)
            {
                return;
            }

            if (panel.NameText != null)
            {
                panel.NameText.text = unit.unitName;
            }

            if (panel.HitPointsText != null)
            {
                panel.HitPointsText.text = unit.HitPoints.ToString();
            }

            if (panel.BackgroundImage != null)
            {
                panel.BackgroundImage.color = missingHealthColor;
                panel.BackgroundImage.enabled = true;
            }

            if (panel.FillImage != null)
            {
                int maxHitPoints = GetDisplayedMaxHitPoints(unit);
                UpdatePanelGradient(panel, unit, maxHitPoints);
                panel.FillImage.fillAmount = Mathf.Clamp01(unit.HitPoints / (float)maxHitPoints);
                panel.FillImage.color = Color.white;
            }
        }

        private void RefreshPreviewPanel(UnitPanelBindings panel, PreviewUnitData preview)
        {
            if (panel == null || panel.Root == null || preview == null)
            {
                return;
            }

            if (panel.NameText != null)
            {
                panel.NameText.text = string.IsNullOrWhiteSpace(preview.Name) ? GameTextCatalog.Get("ui.common.unit", "Unit") : preview.Name;
            }

            int maxHitPoints = Mathf.Max(1, preview.MaxHitPoints);
            int hitPoints = Mathf.Clamp(preview.HitPoints, 0, maxHitPoints);

            if (panel.HitPointsText != null)
            {
                panel.HitPointsText.text = hitPoints.ToString();
            }

            if (panel.BackgroundImage != null)
            {
                panel.BackgroundImage.color = missingHealthColor;
                panel.BackgroundImage.enabled = true;
            }

            if (panel.FillImage != null)
            {
                UpdatePanelGradient(panel, preview.Faction, maxHitPoints);
                panel.FillImage.fillAmount = hitPoints / (float)maxHitPoints;
                panel.FillImage.color = Color.white;
            }
        }

        private void UpdatePanelGradient(UnitPanelBindings panel, CustomUnit unit, int maxHitPoints)
        {
            if (panel == null || panel.FillImage == null)
            {
                return;
            }

            GetGradientColors(unit, maxHitPoints, out Color startColor, out Color endColor);
            ApplyGradient(panel, startColor, endColor);
        }

        private void UpdatePanelGradient(UnitPanelBindings panel, PreviewFaction faction, int maxHitPoints)
        {
            if (panel == null || panel.FillImage == null)
            {
                return;
            }

            GetGradientColors(faction, maxHitPoints, out Color startColor, out Color endColor);
            ApplyGradient(panel, startColor, endColor);
        }

        private void ApplyGradient(UnitPanelBindings panel, Color startColor, Color endColor)
        {
            if (panel.RuntimeGradientSprite != null &&
                ColorsApproximatelyEqual(panel.LastGradientStartColor, startColor) &&
                ColorsApproximatelyEqual(panel.LastGradientEndColor, endColor))
            {
                panel.FillImage.sprite = panel.RuntimeGradientSprite;
                return;
            }

            DestroyPanelGradient(panel);

            panel.RuntimeGradientTexture = new Texture2D(GradientTextureWidth, 1, TextureFormat.RGBA32, false)
            {
                name = "CombatSequenceUI_FillGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int i = 0; i < GradientTextureWidth; i++)
            {
                float t = i / (float)(GradientTextureWidth - 1);
                panel.RuntimeGradientTexture.SetPixel(i, 0, Color.Lerp(startColor, endColor, t));
            }

            panel.RuntimeGradientTexture.Apply();
            panel.RuntimeGradientSprite = Sprite.Create(
                panel.RuntimeGradientTexture,
                new Rect(0f, 0f, panel.RuntimeGradientTexture.width, panel.RuntimeGradientTexture.height),
                new Vector2(0.5f, 0.5f),
                panel.RuntimeGradientTexture.width);
            panel.RuntimeGradientSprite.name = "CombatSequenceUI_FillGradientSprite";

            panel.FillImage.sprite = panel.RuntimeGradientSprite;
            panel.LastGradientStartColor = startColor;
            panel.LastGradientEndColor = endColor;
        }

        private void GetGradientColors(CustomUnit unit, int maxHitPoints, out Color startColor, out Color endColor)
        {
            if (unit == null)
            {
                startColor = neutralStartColor;
                endColor = neutralEndColor;
                return;
            }

            if (unit.PlayerNumber == 0)
            {
                startColor = friendlyStartColor;
                endColor = friendlyEndColor;
            }
            else if (unit.PlayerNumber > 0)
            {
                startColor = enemyStartColor;
                endColor = enemyEndColor;
            }
            else
            {
                startColor = neutralStartColor;
                endColor = neutralEndColor;
            }

            float t = Mathf.Clamp01(maxHitPoints / (float)Mathf.Max(1, gradientMaxHitPoints));
            endColor = Color.Lerp(startColor, endColor, t);
        }

        private void GetGradientColors(PreviewFaction faction, int maxHitPoints, out Color startColor, out Color endColor)
        {
            switch (faction)
            {
                case PreviewFaction.Friendly:
                    startColor = friendlyStartColor;
                    endColor = friendlyEndColor;
                    break;
                case PreviewFaction.Enemy:
                    startColor = enemyStartColor;
                    endColor = enemyEndColor;
                    break;
                default:
                    startColor = neutralStartColor;
                    endColor = neutralEndColor;
                    break;
            }

            float t = Mathf.Clamp01(maxHitPoints / (float)Mathf.Max(1, gradientMaxHitPoints));
            endColor = Color.Lerp(startColor, endColor, t);
        }

        private static int GetDisplayedMaxHitPoints(CustomUnit unit)
        {
            if (unit == null)
            {
                return 1;
            }

            return Mathf.Max(1, unit.ComputedTotalHitPoints > 0 ? unit.ComputedTotalHitPoints : unit.HitPoints);
        }

        private void GetOrderedCombatants(out CustomUnit leftUnit, out CustomUnit rightUnit)
        {
            if (ShouldDisplayFirstUnitOnLeft(_attacker, _defender))
            {
                leftUnit = _attacker;
                rightUnit = _defender;
                return;
            }

            leftUnit = _defender;
            rightUnit = _attacker;
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

        public void Hide()
        {
            if (_hideCoroutine != null)
            {
                StopCoroutine(_hideCoroutine);
                _hideCoroutine = null;
            }

            HideImmediate();
        }

        private void HideImmediate()
        {
            if (root != null)
            {
                root.SetActive(false);
            }

            IsVisible = false;
            VisibilityChanged?.Invoke(false);

            UnsubscribeFromUnitEvents(_attacker);
            UnsubscribeFromUnitEvents(_defender);
            _attacker = null;
            _defender = null;
            DestroyPanelGradient(leftPanel);
            DestroyPanelGradient(rightPanel);

            if (leftPanel != null && leftPanel.BackgroundImage != null)
            {
                leftPanel.BackgroundImage.enabled = false;
            }

            if (rightPanel != null && rightPanel.BackgroundImage != null)
            {
                rightPanel.BackgroundImage.enabled = false;
            }
        }

        private void PositionRoot(Vector3 worldPosition)
        {
            CanvasClampManager.PositionAtWorldPoint(
                canvas,
                worldCamera,
                _canvasRectTransform,
                _rootRectTransform,
                positionTarget,
                worldPosition,
                screenOffset,
                screenPadding);
        }

        private void DestroyPanelGradient(UnitPanelBindings panel)
        {
            if (panel == null)
            {
                return;
            }

            if (panel.RuntimeGradientSprite != null)
            {
                Destroy(panel.RuntimeGradientSprite);
                panel.RuntimeGradientSprite = null;
            }

            if (panel.RuntimeGradientTexture != null)
            {
                Destroy(panel.RuntimeGradientTexture);
                panel.RuntimeGradientTexture = null;
            }

            panel.LastGradientStartColor = new Color(-1f, -1f, -1f, -1f);
            panel.LastGradientEndColor = new Color(-1f, -1f, -1f, -1f);
        }

        private static bool ColorsApproximatelyEqual(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) &&
                   Mathf.Approximately(a.g, b.g) &&
                   Mathf.Approximately(a.b, b.b) &&
                   Mathf.Approximately(a.a, b.a);
        }
    }

}


