using System.Collections.Generic;
using UnityEngine;
using TMPro;

public abstract class DashboardPanel : MonoBehaviour
{
    public abstract string PanelName { get; }
    public TMP_Dropdown panelDropdown;
    public DashboardSlot OwnerSlot { get; set; }
    private List<TMP_Dropdown.OptionData> allPanelOptions;
    private readonly List<DashboardPanelType> displayedPanelTypes = new();

    protected virtual void Awake()
    {
        if (panelDropdown != null)
        {
            allPanelOptions = new List<TMP_Dropdown.OptionData>();
            foreach (TMP_Dropdown.OptionData option in panelDropdown.options)
                allPanelOptions.Add(new TMP_Dropdown.OptionData(option.text));

            panelDropdown.onValueChanged.AddListener(value =>
            {
                if (value < 0 || value >= displayedPanelTypes.Count)
                    return;

                int slotIndex = GetOwnerSlotIndex();
                if (slotIndex >= 0 && DashboardManager.Instance != null)
                    DashboardManager.Instance.SetPanelBySlotIndex(slotIndex, (int)displayedPanelTypes[value]);
            });
        }
    }

    public void SetAvailablePanelTypes(IReadOnlyList<DashboardPanelType> availablePanelTypes)
    {
        if (panelDropdown == null || allPanelOptions == null)
            return;

        displayedPanelTypes.Clear();
        panelDropdown.ClearOptions();

        foreach (DashboardPanelType panelType in availablePanelTypes)
        {
            displayedPanelTypes.Add(panelType);
            int optionIndex = (int)panelType;
            string optionText = optionIndex >= 0 && optionIndex < allPanelOptions.Count
                ? allPanelOptions[optionIndex].text
                : panelType.ToString();
            panelDropdown.options.Add(new TMP_Dropdown.OptionData(optionText));
        }

        DashboardPanelType? currentPanelType = OwnerSlot?.CurrentPanelType;
        if (currentPanelType.HasValue)
        {
            int selectedIndex = displayedPanelTypes.IndexOf(currentPanelType.Value);
            if (selectedIndex >= 0)
                panelDropdown.SetValueWithoutNotify(selectedIndex);
        }

        panelDropdown.RefreshShownValue();
    }

    private int GetOwnerSlotIndex()
    {
        var slot = OwnerSlot;
        var mgr = DashboardManager.Instance;
        if (slot == null || mgr == null) return -1;
        if (slot == mgr.topLeft) return 0;
        if (slot == mgr.topRight) return 1;
        if (slot == mgr.bottomLeft) return 2;
        if (slot == mgr.bottomRight) return 3;
        return -1;
    }

    public virtual void OnPanelShown() { }
    public virtual void OnPanelHidden() { }
    public virtual void SetFullscreen(bool fullscreen) { }
}
