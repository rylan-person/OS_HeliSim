using UnityEngine;
using TMPro;

public abstract class DashboardPanel : MonoBehaviour
{
    public abstract string PanelName { get; }
    public TMP_Dropdown panelDropdown;

    protected virtual void Awake()
    {
        if (panelDropdown != null)
        {
            panelDropdown.onValueChanged.AddListener(value =>
            {
                Debug.Log($"Dropdown value changed to {value} in panel {PanelName}");
                int slotIndex = GetOwnerSlotIndex();
                if (slotIndex >= 0)
                    DashboardManager.Instance.SetPanelBySlotIndex(slotIndex, value);
            });
        }
    }

    private int GetOwnerSlotIndex()
    {
        var slot = GetComponentInParent<DashboardSlot>();
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
