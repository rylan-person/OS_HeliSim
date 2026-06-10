using System.Collections.Generic;
using UnityEngine;

public class DashboardManager : MonoBehaviour
{
    [Header("Slots")]
    public DashboardSlot topLeft;
    public DashboardSlot topRight;
    public DashboardSlot bottomLeft;
    public DashboardSlot bottomRight;

    public RectTransform fullscreenRoot;
    public RectTransform gridRoot;

    public DashboardSlot focusedSlot;
    private Transform originalParent;
    private int originalSiblingIndex;

    [Header("Available Panels")]
    [SerializeField] private List<DashboardPanelEntry> availablePanels = new();
    private Dictionary<DashboardPanelType, DashboardPanel> panelLookup;

    // Singleton instance for easy access
    public static DashboardManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple instances of DashboardManager detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BuildPanelLookup();
    }

    private void Start()
    {
        topLeft.SetPanel(panelLookup[DashboardPanelType.FollowCamera]);
    }

    private void BuildPanelLookup()
    {
        panelLookup = new Dictionary<DashboardPanelType, DashboardPanel>();

        foreach (DashboardPanelEntry entry in availablePanels)
        {
            if (entry.prefab == null)
            {
                Debug.LogWarning($"Panel entry for {entry.panelType} has no prefab assigned.");
                continue;
            }

            if (panelLookup.ContainsKey(entry.panelType))
            {
                Debug.LogWarning($"Duplicate panel type found: {entry.panelType}");
                continue;
            }

            panelLookup.Add(entry.panelType, entry.prefab);
        }
    }

    public void FocusSlot(DashboardSlot slot)
    {
        if (focusedSlot != null)
            Unfocus();

        var panel = slot.GetCurrentPanel();
        if (panel == null)
            return;

        focusedSlot = slot;
        originalParent = panel.transform.parent;
        originalSiblingIndex = panel.transform.GetSiblingIndex();

        panel.transform.SetParent(fullscreenRoot, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        fullscreenRoot.gameObject.SetActive(true);
        gridRoot.gameObject.SetActive(false);

        panel.SetFullscreen(true);
    }

    public void Unfocus()
    {
        if (focusedSlot == null)
            return;

        var panel = fullscreenRoot.GetComponentInChildren<DashboardPanel>();
        if (panel != null)
        {
            panel.transform.SetParent(originalParent, false);
            panel.transform.SetSiblingIndex(originalSiblingIndex);
            panel.SetFullscreen(false);
        }

        fullscreenRoot.gameObject.SetActive(false);
        gridRoot.gameObject.SetActive(true);

        focusedSlot = null;
    }
}