using UnityEngine;
using UnityEngine.UI;
using Windy.Srpg.Game.Abilities;
using Windy.Srpg.Game.UI;
using static Windy.Srpg.Game.Abilities.MoveAbility;

public class ActionMenuUI : MonoBehaviour, IActionMenuUI
{
    public static event System.Action<bool> VisibilityChanged;

    public GameObject panel;
    public Button attackButton;
    public Button healButton;
    public Button skillButton;
    public Button itemButton;
    public Button tradeButton;
    public Button waitButton;
    public Button cancelButton;
    [SerializeField] private RectTransform positionTarget;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera worldCamera;
    private const float ButtonStartY = 0f;
    private const float ButtonSpacingY = -70f;
    private Vector2 screenOffset = new Vector2(240f, 0f);
    private Vector2 screenPadding = new Vector2(24f, 24f);

    private System.Action _onAttack;
    private System.Action _onHeal;
    private System.Action _onSkill;
    private System.Action _onItem;
    private System.Action _onTrade;
    private System.Action _onWait;
    private System.Action _onCancel;
    private RectTransform _panelRectTransform;
    private RectTransform _canvasRectTransform;

    void Awake()
    {
        if (panel == null)
        {
            return;
        }

        _panelRectTransform = panel.GetComponent<RectTransform>();
        if (positionTarget == null)
        {
            RectTransform ownRectTransform = transform as RectTransform;
            if (ownRectTransform != null && panel.transform.IsChildOf(transform))
            {
                positionTarget = ownRectTransform;
            }
            else
            {
                positionTarget = _panelRectTransform;
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

        panel.SetActive(false);
        VisibilityChanged?.Invoke(false);
        if (attackButton != null)
        {
            attackButton.onClick.AddListener(() => _onAttack?.Invoke());
        }
        if (healButton != null)
        {
            healButton.onClick.AddListener(() => _onHeal?.Invoke());
        }
        if (skillButton != null)
        {
            skillButton.onClick.AddListener(() => _onSkill?.Invoke());
        }
        if (itemButton != null)
        {
            itemButton.onClick.AddListener(() => _onItem?.Invoke());
        }
        if (tradeButton != null)
        {
            tradeButton.onClick.AddListener(() => _onTrade?.Invoke());
        }
        if (waitButton != null)
        {
            waitButton.onClick.AddListener(() => _onWait?.Invoke());
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(() => _onCancel?.Invoke());
        }
    }

    public void Show(Vector3 worldPosition, bool showAttack, bool showHeal, bool showSkill, bool showItem, bool showTrade, System.Action onAttack, System.Action onHeal, System.Action onSkill, System.Action onItem, System.Action onTrade, System.Action onWait, System.Action onCancel)
    {
        _onAttack = onAttack;
        _onHeal = onHeal;
        _onSkill = onSkill;
        _onItem = onItem;
        _onTrade = onTrade;
        _onWait = onWait;
        _onCancel = onCancel;

        if (attackButton != null)
        {
            attackButton.gameObject.SetActive(showAttack);
        }
        if (healButton != null)
        {
            healButton.gameObject.SetActive(showHeal);
        }
        if (skillButton != null)
        {
            skillButton.gameObject.SetActive(showSkill);
        }
        if (itemButton != null)
        {
            itemButton.gameObject.SetActive(showItem);
        }
        if (tradeButton != null)
        {
            tradeButton.gameObject.SetActive(showTrade);
        }

        RepositionVisibleButtons();
        panel.SetActive(true);
        VisibilityChanged?.Invoke(true);
        PositionPanel(worldPosition);
    }

    public void Hide()
    {
        _onAttack = null;
        _onHeal = null;
        _onSkill = null;
        _onItem = null;
        _onTrade = null;
        _onWait = null;
        _onCancel = null;
        panel.SetActive(false);
        VisibilityChanged?.Invoke(false);
    }

    private void OnDisable()
    {
        VisibilityChanged?.Invoke(false);
    }

    private void RepositionVisibleButtons()
    {
        float nextY = ButtonStartY;
        RepositionButton(tradeButton, ref nextY);
        RepositionButton(attackButton, ref nextY);
        RepositionButton(healButton, ref nextY);
        RepositionButton(skillButton, ref nextY);
        RepositionButton(itemButton, ref nextY);
        RepositionButton(waitButton, ref nextY);
        RepositionButton(cancelButton, ref nextY);
    }

    private static void RepositionButton(Button button, ref float nextY)
    {
        if (button == null || !button.gameObject.activeSelf)
        {
            return;
        }

        RectTransform rectTransform = button.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            return;
        }

        Vector2 anchoredPosition = rectTransform.anchoredPosition;
        anchoredPosition.y = nextY;
        rectTransform.anchoredPosition = anchoredPosition;
        nextY += ButtonSpacingY;
    }

    private void PositionPanel(Vector3 worldPosition)
    {
        CanvasClampManager.PositionAtWorldPoint(
            canvas,
            worldCamera,
            _canvasRectTransform,
            _panelRectTransform,
            positionTarget,
            worldPosition,
            screenOffset,
            screenPadding);
    }
}

