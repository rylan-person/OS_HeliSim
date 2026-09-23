using UnityEngine;

public class TimingDashboardPanel : DashboardPanel
{
    public override string PanelName => "Timing";

    [SerializeField] private UnityEngine.UI.Button fullscreenButton;
    [SerializeField] private Timing timingView;

    private PlayerTimeTrialState activeState;
    private bool isShown;
    private RectTransform timingRect;
    private float nextRefreshTime;

    protected override void Awake()
    {
        base.Awake();

        if (timingView == null)
        {
            timingView = GetComponentInChildren<Timing>(true);
        }

        if (timingView != null)
        {
            timingView.IsExternallyDriven = true;
            timingRect = timingView.GetComponent<RectTransform>();
        }

        if (fullscreenButton != null)
        {
            fullscreenButton.onClick.AddListener(ToggleFullscreen);
        }
    }

    private void OnEnable()
    {
        PlayerTimeTrialState.ActiveStateChanged += OnActiveStateChanged;
        OnActiveStateChanged(PlayerTimeTrialState.ActiveState);
    }

    private void OnDisable()
    {
        PlayerTimeTrialState.ActiveStateChanged -= OnActiveStateChanged;
    }

    private void Update()
    {
        if (isShown && timingView != null && activeState != null && Time.unscaledTime >= nextRefreshTime)
        {
            timingView.Render(activeState);
            nextRefreshTime = Time.unscaledTime + 0.05f;
        }
    }

    public override void OnPanelShown()
    {
        isShown = true;
        RefreshTimingLayout();
        OnActiveStateChanged(PlayerTimeTrialState.ActiveState);
    }

    public override void OnPanelHidden()
    {
        isShown = false;
    }

    public override void SetFullscreen(bool fullscreen)
    {
        RefreshTimingLayout();
    }

    private void OnRectTransformDimensionsChange()
    {
        RefreshTimingLayout();
    }

    private void RefreshTimingLayout()
    {
        if (timingRect == null)
        {
            return;
        }

        RectTransform panelRect = GetComponent<RectTransform>();
        if (panelRect == null || panelRect.rect.width <= 0f || panelRect.rect.height <= 0f)
        {
            return;
        }

        const float boardWidth = 200f;
        const float boardHeight = 250f;
        const float headerHeight = 75f;
        const float padding = 24f;
        float widthScale = (panelRect.rect.width - padding) / boardWidth;
        float heightScale = (panelRect.rect.height - headerHeight - padding) / boardHeight;
        float scale = Mathf.Max(0.1f, Mathf.Min(widthScale, heightScale));
        timingRect.localScale = new Vector3(scale, scale, 1f);
        timingRect.anchoredPosition = new Vector2(0f, -headerHeight * 0.5f);
    }

    private void OnActiveStateChanged(PlayerTimeTrialState state)
    {
        activeState = state;
        if (timingView == null)
        {
            return;
        }

        if (activeState == null)
        {
            timingView.ResetDisplay();
        }
        else if (isShown)
        {
            timingView.Render(activeState);
            nextRefreshTime = Time.unscaledTime + 0.05f;
        }
    }

    private void ToggleFullscreen()
    {
        DashboardManager manager = DashboardManager.Instance;
        if (manager == null || OwnerSlot == null)
        {
            return;
        }

        if (manager.focusedSlot == OwnerSlot)
        {
            manager.Unfocus();
        }
        else
        {
            manager.FocusSlot(OwnerSlot);
        }
    }
}
