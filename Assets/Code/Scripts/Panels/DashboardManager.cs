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
        topRight.SetPanel(panelLookup[DashboardPanelType.OrbitCamera]);
        bottomLeft.SetPanel(panelLookup[DashboardPanelType.DynamicCamera]);
        bottomRight.SetPanel(panelLookup[DashboardPanelType.FPVCamera]);
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

    private IEnumerable<DashboardSlot> AllSlots()
    {
        yield return topLeft;
        yield return topRight;
        yield return bottomLeft;
        yield return bottomRight;
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

        // Hide all other slots' panels to free resources
        foreach (DashboardSlot s in AllSlots())
        {
            if (s != slot)
                s.GetCurrentPanel()?.OnPanelHidden();
        }

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

        DashboardSlot previousFocus = focusedSlot;
        focusedSlot = null;

        // Restore all other slots' panels
        foreach (DashboardSlot s in AllSlots())
        {
            if (s != previousFocus)
                s.GetCurrentPanel()?.OnPanelShown();
        }
    }

    private void SetSlotPanel(DashboardSlot slot, DashboardPanelType panelType)
    {
        if (slot == null)
        {
            Debug.LogWarning("Tried to set a panel on a missing dashboard slot.");
            return;
        }

        bool wasFocused = focusedSlot == slot;
        if (wasFocused)
            Unfocus();

        if (panelType == DashboardPanelType.Empty)
        {
            slot.ClearPanel();
            return;
        }

        if (!panelLookup.TryGetValue(panelType, out DashboardPanel prefab))
        {
            Debug.LogWarning($"No panel prefab registered for panel type: {panelType}");
            return;
        }

        slot.SetPanel(prefab);

        if (wasFocused)
            FocusSlot(slot);
    }

    public void SetPanelBySlotIndex(int slotIndex, int panelTypeIndex)
    {
        DashboardPanelType panelType = (DashboardPanelType)panelTypeIndex;

        Debug.Log($"Setting panel for slot index {slotIndex} to panel type {panelType}");

        switch (slotIndex)
        {
            case 0:
                SetSlotPanel(topLeft, panelType);
                break;

            case 1:
                SetSlotPanel(topRight, panelType);
                break;

            case 2:
                SetSlotPanel(bottomLeft, panelType);
                break;

            case 3:
                SetSlotPanel(bottomRight, panelType);
                break;

            default:
                Debug.LogWarning($"Invalid slot index: {slotIndex}");
                break;
        }
    }
}