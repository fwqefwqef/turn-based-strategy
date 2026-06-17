using System;
using System.Collections.Generic;
using Windy.Srpg.Game.Units;
using UnityEngine;

namespace Windy.Srpg.Game.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Unit))]
    public class UnitWorldHealthBar : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, -0.55f, 0f);
        [SerializeField] private Vector2 barSize = new Vector2(0.8f, 0.12f);
        [SerializeField] private float manaBarHeightScale = 0.5f;
        [SerializeField] private float secondaryBarVerticalSpacing = 0.05f;
        [SerializeField] private float autoPadding = 0.08f;

        [Header("HP Colors")]
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        [SerializeField] private Color borderColor = new Color(0f, 0f, 0f, 1f);
        [SerializeField] private Color friendlyStartColor = new Color32(60, 180, 75, 255);
        [SerializeField] private Color friendlyEndColor = new Color32(125, 20, 210, 255);
        [SerializeField] private Color enemyStartColor = new Color32(220, 50, 50, 255);
        [SerializeField] private Color enemyEndColor = new Color32(125, 20, 210, 255);
        [SerializeField] private Color neutralStartColor = new Color32(60, 180, 75, 255);
        [SerializeField] private Color neutralEndColor = new Color32(125, 20, 210, 255);
        [SerializeField] private int gradientMaxHitPoints = 100;

        [Header("MP Colors")]
        [SerializeField] private Color friendlyManaStartColor = new Color32(135, 205, 255, 255);
        [SerializeField] private Color friendlyManaEndColor = new Color32(125, 20, 210, 255);
        [SerializeField] private Color enemyManaStartColor = new Color32(135, 205, 255, 255);
        [SerializeField] private Color enemyManaEndColor = new Color32(125, 20, 210, 255);
        [SerializeField] private Color neutralManaStartColor = new Color32(135, 205, 255, 255);
        [SerializeField] private Color neutralManaEndColor = new Color32(125, 20, 210, 255);
        [SerializeField] private int gradientMaxManaPoints = 100;

        [Header("Behavior")]
        [SerializeField] private bool autoOffsetFromSprite = true;
        [SerializeField] private bool hideWhenDead = true;
        [SerializeField] private int sortingOrderOffset = 10;

        private static Sprite _whiteSprite;

        private Unit _unit;
        private Transform _root;
        private SpriteRenderer _borderRenderer;
        private SpriteRenderer _backgroundRenderer;
        private SpriteRenderer _fillRenderer;
        private SpriteRenderer _manaBorderRenderer;
        private SpriteRenderer _manaBackgroundRenderer;
        private SpriteRenderer _manaFillRenderer;
        private SpriteRenderer[] _unitSpriteRenderers;
        private readonly Dictionary<SpriteRenderer, int> _baseUnitSortingOrders = new Dictionary<SpriteRenderer, int>();
        private int _lastRenderedHitPoints = int.MinValue;
        private int _lastRenderedMaxHitPoints = int.MinValue;
        private int _lastRenderedManaPoints = int.MinValue;
        private int _lastRenderedMaxManaPoints = int.MinValue;
        private Color _lastHealthGradientStartColor = new Color(-1f, -1f, -1f, -1f);
        private Color _lastHealthGradientEndColor = new Color(-1f, -1f, -1f, -1f);
        private Color _lastManaGradientStartColor = new Color(-1f, -1f, -1f, -1f);
        private Color _lastManaGradientEndColor = new Color(-1f, -1f, -1f, -1f);
        private Texture2D _healthFillTexture;
        private Sprite _healthFillSprite;
        private Texture2D _manaFillTexture;
        private Sprite _manaFillSprite;
        private const int GradientTextureWidth = 32;

        private void Awake()
        {
            _unit = GetComponent<Unit>();
            CacheUnitSpriteRenderers();
            EnsureVisuals();
            Refresh();
        }

        private void OnEnable()
        {
            if (_unit == null)
            {
                _unit = GetComponent<Unit>();
            }

            if (_unit != null)
            {
                _unit.UnitHealthChanged += OnUnitHealthChanged;
                _unit.UnitStatsChanged += OnUnitStatsChanged;
                _unit.DestroyedInCombat += OnUnitDestroyed;
            }

            EnsureVisuals();
            Refresh();
        }

        private void OnDisable()
        {
            if (_unit != null)
            {
                _unit.UnitHealthChanged -= OnUnitHealthChanged;
                _unit.UnitStatsChanged -= OnUnitStatsChanged;
                _unit.DestroyedInCombat -= OnUnitDestroyed;
            }
        }

        private void OnDestroy()
        {
            DestroyGeneratedFillSprite();
        }

        private void LateUpdate()
        {
            if (_root == null)
            {
                return;
            }

            UpdateAnchor();

            int currentHitPoints = _unit != null ? _unit.HitPoints : 0;
            int currentMaxHitPoints = GetDisplayedMaxHitPoints();
            int currentManaPoints = _unit != null ? _unit.CurrentManaPoints : 0;
            int currentMaxManaPoints = GetDisplayedMaxManaPoints();
            if (currentHitPoints != _lastRenderedHitPoints
                || currentMaxHitPoints != _lastRenderedMaxHitPoints
                || currentManaPoints != _lastRenderedManaPoints
                || currentMaxManaPoints != _lastRenderedMaxManaPoints)
            {
                Refresh();
            }
        }

        private void OnUnitHealthChanged(object sender, UnitHealthChangedEventArgs e)
        {
            Refresh();
        }

        private void OnUnitStatsChanged(object sender, EventArgs e)
        {
            Refresh();
        }

        private void OnUnitDestroyed(object sender, UnitDestroyedEventArgs e)
        {
            if (hideWhenDead && _root != null)
            {
                _root.gameObject.SetActive(false);
            }
        }

        private void Refresh()
        {
            if (_unit == null)
            {
                return;
            }

            EnsureVisuals();
            CacheUnitSpriteRenderers();
            UpdateAnchor();
            UpdateSorting();

            int maxHitPoints = GetDisplayedMaxHitPoints();
            float healthRatio = Mathf.Clamp01(_unit.HitPoints / (float)maxHitPoints);
            int maxManaPoints = GetDisplayedMaxManaPoints();
            float manaRatio = maxManaPoints <= 0 ? 0f : Mathf.Clamp01(_unit.CurrentManaPoints / (float)maxManaPoints);
            UpdateHealthFillGradient();
            UpdateManaFillGradient();

            if (_fillRenderer != null)
            {
                float fillWidth = barSize.x * healthRatio;
                _fillRenderer.size = new Vector2(fillWidth, barSize.y);
                _fillRenderer.transform.localPosition = new Vector3((fillWidth - barSize.x) * 0.5f, 0f, -0.01f);
                _fillRenderer.color = Color.white;
                _fillRenderer.enabled = healthRatio > 0f;
            }

            if (_backgroundRenderer != null)
            {
                _backgroundRenderer.size = barSize;
            }

            if (_borderRenderer != null)
            {
                _borderRenderer.size = barSize + new Vector2(0.04f, 0.04f);
            }

            float manaBarLocalY = GetManaBarLocalY();
            Vector2 manaBarSize = GetManaBarSize();
            if (_manaFillRenderer != null)
            {
                float manaFillWidth = manaBarSize.x * manaRatio;
                _manaFillRenderer.size = new Vector2(manaFillWidth, manaBarSize.y);
                _manaFillRenderer.transform.localPosition = new Vector3((manaFillWidth - barSize.x) * 0.5f, manaBarLocalY, -0.01f);
                _manaFillRenderer.color = Color.white;
                _manaFillRenderer.enabled = manaRatio > 0f;
            }

            if (_manaBackgroundRenderer != null)
            {
                _manaBackgroundRenderer.size = manaBarSize;
                _manaBackgroundRenderer.transform.localPosition = new Vector3(0f, manaBarLocalY, 0f);
            }

            if (_manaBorderRenderer != null)
            {
                _manaBorderRenderer.size = manaBarSize + new Vector2(0.04f, 0.04f);
                _manaBorderRenderer.transform.localPosition = new Vector3(0f, manaBarLocalY, 0.01f);
            }

            if (_root != null)
            {
                _root.gameObject.SetActive(!hideWhenDead || _unit.HitPoints > 0);
            }

            _lastRenderedHitPoints = _unit.HitPoints;
            _lastRenderedMaxHitPoints = maxHitPoints;
            _lastRenderedManaPoints = _unit.CurrentManaPoints;
            _lastRenderedMaxManaPoints = maxManaPoints;
        }

        private void EnsureVisuals()
        {
            if (_root != null
                && _borderRenderer != null
                && _backgroundRenderer != null
                && _fillRenderer != null
                && _manaBorderRenderer != null
                && _manaBackgroundRenderer != null
                && _manaFillRenderer != null)
            {
                return;
            }

            Sprite sprite = GetWhiteSprite();

            if (_root == null)
            {
                Transform existing = transform.Find("WorldHealthBar");
                if (existing != null)
                {
                    _root = existing;
                }
                else
                {
                    GameObject root = new GameObject("WorldHealthBar");
                    root.transform.SetParent(transform, false);
                    _root = root.transform;
                }
            }

            _borderRenderer = GetOrCreateRenderer("Border", ref _borderRenderer, sprite, borderColor);
            _backgroundRenderer = GetOrCreateRenderer("Background", ref _backgroundRenderer, sprite, backgroundColor);
            _fillRenderer = GetOrCreateRenderer("Fill", ref _fillRenderer, sprite, Color.white);
            _manaBorderRenderer = GetOrCreateRenderer("ManaBorder", ref _manaBorderRenderer, sprite, borderColor);
            _manaBackgroundRenderer = GetOrCreateRenderer("ManaBackground", ref _manaBackgroundRenderer, sprite, backgroundColor);
            _manaFillRenderer = GetOrCreateRenderer("ManaFill", ref _manaFillRenderer, sprite, Color.white);

            _borderRenderer.drawMode = SpriteDrawMode.Sliced;
            _backgroundRenderer.drawMode = SpriteDrawMode.Sliced;
            _fillRenderer.drawMode = SpriteDrawMode.Sliced;
            _manaBorderRenderer.drawMode = SpriteDrawMode.Sliced;
            _manaBackgroundRenderer.drawMode = SpriteDrawMode.Sliced;
            _manaFillRenderer.drawMode = SpriteDrawMode.Sliced;

            _backgroundRenderer.transform.localPosition = Vector3.zero;
            _borderRenderer.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            _fillRenderer.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            _fillRenderer.transform.localScale = Vector3.one;
            float manaBarLocalY = GetManaBarLocalY();
            _manaBackgroundRenderer.transform.localPosition = new Vector3(0f, manaBarLocalY, 0f);
            _manaBorderRenderer.transform.localPosition = new Vector3(0f, manaBarLocalY, 0.01f);
            _manaFillRenderer.transform.localPosition = new Vector3(0f, manaBarLocalY, -0.01f);
            _manaFillRenderer.transform.localScale = Vector3.one;
        }

        private SpriteRenderer GetOrCreateRenderer(string childName, ref SpriteRenderer renderer, Sprite sprite, Color color)
        {
            if (renderer != null)
            {
                return renderer;
            }

            Transform child = _root.Find(childName);
            if (child == null)
            {
                GameObject childObject = new GameObject(childName);
                childObject.transform.SetParent(_root, false);
                child = childObject.transform;
            }

            renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = child.gameObject.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = sprite;
            renderer.color = color;
            renderer.maskInteraction = SpriteMaskInteraction.None;
            return renderer;
        }

        private void CacheUnitSpriteRenderers()
        {
            _unitSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < _unitSpriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = _unitSpriteRenderers[i];
                if (renderer == null || (_root != null && renderer.transform.IsChildOf(_root)))
                {
                    continue;
                }

                if (!_baseUnitSortingOrders.ContainsKey(renderer))
                {
                    _baseUnitSortingOrders[renderer] = renderer.sortingOrder;
                }
            }
        }

        private void UpdateAnchor()
        {
            if (_root == null)
            {
                return;
            }

            Vector3 resolvedOffset = localOffset;
            if (autoOffsetFromSprite)
            {
                float bottomY = float.PositiveInfinity;
                bool foundRenderer = false;

                for (int i = 0; i < _unitSpriteRenderers.Length; i++)
                {
                    SpriteRenderer renderer = _unitSpriteRenderers[i];
                    if (renderer == null || renderer.transform.IsChildOf(_root))
                    {
                        continue;
                    }

                    foundRenderer = true;
                    Vector3 localBottom = transform.InverseTransformPoint(renderer.bounds.min);
                    bottomY = Mathf.Min(bottomY, localBottom.y);
                }

                if (foundRenderer)
                {
                    resolvedOffset = new Vector3(0f, bottomY - autoPadding - (barSize.y * 0.5f), 0f);
                }
            }

            _root.localPosition = resolvedOffset;
        }

        private void UpdateSorting()
        {
            int baseOrder = int.MinValue;
            string sortingLayerName = "Default";

            for (int i = 0; i < _unitSpriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = _unitSpriteRenderers[i];
                if (renderer == null || renderer.transform.IsChildOf(_root))
                {
                    continue;
                }

                if (!_baseUnitSortingOrders.TryGetValue(renderer, out int rendererBaseOrder))
                {
                    rendererBaseOrder = renderer.sortingOrder;
                    _baseUnitSortingOrders[renderer] = rendererBaseOrder;
                }

                if (rendererBaseOrder >= baseOrder)
                {
                    baseOrder = rendererBaseOrder;
                    sortingLayerName = renderer.sortingLayerName;
                }
            }

            if (baseOrder == int.MinValue)
            {
                baseOrder = sortingOrderOffset + 6;
            }

            _borderRenderer.sortingLayerName = sortingLayerName;
            _backgroundRenderer.sortingLayerName = sortingLayerName;
            _fillRenderer.sortingLayerName = sortingLayerName;
            _manaBorderRenderer.sortingLayerName = sortingLayerName;
            _manaBackgroundRenderer.sortingLayerName = sortingLayerName;
            _manaFillRenderer.sortingLayerName = sortingLayerName;

            int preferredBarBaseOrder = Mathf.Max(3, baseOrder - sortingOrderOffset + 3);
            int maxBarBaseOrder = baseOrder - 5;
            int barBaseOrder = Mathf.Min(preferredBarBaseOrder, maxBarBaseOrder);
            _borderRenderer.sortingOrder = barBaseOrder;
            _backgroundRenderer.sortingOrder = barBaseOrder + 1;
            _fillRenderer.sortingOrder = barBaseOrder + 2;
            _manaBorderRenderer.sortingOrder = barBaseOrder + 3;
            _manaBackgroundRenderer.sortingOrder = barBaseOrder + 4;
            _manaFillRenderer.sortingOrder = barBaseOrder + 5;
        }

        private void UpdateHealthFillGradient()
        {
            GetFillGradientColors(out Color gradientStart, out Color gradientEnd);
            UpdateGradientRenderer(
                _fillRenderer,
                "UnitWorldHealthBar_HealthGradient",
                ref _healthFillTexture,
                ref _healthFillSprite,
                ref _lastHealthGradientStartColor,
                ref _lastHealthGradientEndColor,
                gradientStart,
                gradientEnd);
        }

        private void UpdateManaFillGradient()
        {
            GetManaGradientColors(out Color gradientStart, out Color gradientEnd);
            UpdateGradientRenderer(
                _manaFillRenderer,
                "UnitWorldHealthBar_ManaGradient",
                ref _manaFillTexture,
                ref _manaFillSprite,
                ref _lastManaGradientStartColor,
                ref _lastManaGradientEndColor,
                gradientStart,
                gradientEnd);
        }

        private void UpdateGradientRenderer(
            SpriteRenderer renderer,
            string textureName,
            ref Texture2D texture,
            ref Sprite sprite,
            ref Color lastStartColor,
            ref Color lastEndColor,
            Color gradientStart,
            Color gradientEnd)
        {
            if (renderer == null)
            {
                return;
            }

            if (sprite != null &&
                ColorsApproximatelyEqual(lastStartColor, gradientStart) &&
                ColorsApproximatelyEqual(lastEndColor, gradientEnd))
            {
                return;
            }

            DestroyGeneratedSprite(ref sprite, ref texture);

            texture = new Texture2D(GradientTextureWidth, 1, TextureFormat.RGBA32, false)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int i = 0; i < GradientTextureWidth; i++)
            {
                float t = i / (float)(GradientTextureWidth - 1);
                texture.SetPixel(i, 0, Color.Lerp(gradientStart, gradientEnd, t));
            }

            texture.Apply();

            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                texture.width);
            sprite.name = textureName + "_Sprite";

            renderer.sprite = sprite;
            renderer.color = Color.white;
            lastStartColor = gradientStart;
            lastEndColor = gradientEnd;
        }

        private void GetFillGradientColors(out Color gradientStart, out Color gradientEnd)
        {
            if (_unit == null)
            {
                gradientStart = neutralStartColor;
                gradientEnd = neutralEndColor;
                return;
            }

            Color baseStartColor;
            Color targetEndColor;

            if (_unit.PlayerNumber == 0)
            {
                baseStartColor = friendlyStartColor;
                targetEndColor = friendlyEndColor;
            }
            else if (_unit.PlayerNumber > 0)
            {
                baseStartColor = enemyStartColor;
                targetEndColor = enemyEndColor;
            }
            else
            {
                baseStartColor = neutralStartColor;
                targetEndColor = neutralEndColor;
            }

            float maxHpGradientT = Mathf.Clamp01(GetDisplayedMaxHitPoints() / (float)Mathf.Max(1, gradientMaxHitPoints));
            gradientStart = baseStartColor;
            gradientEnd = Color.Lerp(baseStartColor, targetEndColor, maxHpGradientT);
        }

        private void GetManaGradientColors(out Color gradientStart, out Color gradientEnd)
        {
            if (_unit == null)
            {
                gradientStart = neutralManaStartColor;
                gradientEnd = neutralManaEndColor;
                return;
            }

            Color baseStartColor;
            Color targetEndColor;

            if (_unit.PlayerNumber == 0)
            {
                baseStartColor = friendlyManaStartColor;
                targetEndColor = friendlyManaEndColor;
            }
            else if (_unit.PlayerNumber > 0)
            {
                baseStartColor = enemyManaStartColor;
                targetEndColor = enemyManaEndColor;
            }
            else
            {
                baseStartColor = neutralManaStartColor;
                targetEndColor = neutralManaEndColor;
            }

            float maxManaGradientT = Mathf.Clamp01(GetDisplayedMaxManaPoints() / (float)Mathf.Max(1, gradientMaxManaPoints));
            gradientStart = baseStartColor;
            gradientEnd = Color.Lerp(baseStartColor, targetEndColor, maxManaGradientT);
        }

        private void DestroyGeneratedFillSprite()
        {
            DestroyGeneratedSprite(ref _healthFillSprite, ref _healthFillTexture);
            DestroyGeneratedSprite(ref _manaFillSprite, ref _manaFillTexture);
        }

        private void DestroyGeneratedSprite(ref Sprite sprite, ref Texture2D texture)
        {
            if (sprite != null)
            {
                Destroy(sprite);
                sprite = null;
            }

            if (texture != null)
            {
                Destroy(texture);
                texture = null;
            }
        }

        private static bool ColorsApproximatelyEqual(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) &&
                   Mathf.Approximately(a.g, b.g) &&
                   Mathf.Approximately(a.b, b.b) &&
                   Mathf.Approximately(a.a, b.a);
        }

        private int GetDisplayedMaxHitPoints()
        {
            if (_unit == null)
            {
                return 1;
            }

            return Mathf.Max(1, _unit.ComputedTotalHitPoints > 0 ? _unit.ComputedTotalHitPoints : _unit.HitPoints);
        }

        private int GetDisplayedMaxManaPoints()
        {
            if (_unit == null)
            {
                return 0;
            }

            return Mathf.Max(0, _unit.ComputedTotalManaPoints);
        }

        private float GetManaBarLocalY()
        {
            Vector2 manaBarSize = GetManaBarSize();
            return -((barSize.y * 0.5f) + secondaryBarVerticalSpacing + (manaBarSize.y * 0.5f));
        }

        private Vector2 GetManaBarSize()
        {
            return new Vector2(barSize.x, Mathf.Max(0.01f, barSize.y * manaBarHeightScale));
        }

        private static Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null)
            {
                return _whiteSprite;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "UnitWorldHealthBar_White";
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;

            _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _whiteSprite.name = "UnitWorldHealthBar_WhiteSprite";
            return _whiteSprite;
        }
    }
}



