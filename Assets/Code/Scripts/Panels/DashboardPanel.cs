using UnityEngine;

public abstract class DashboardPanel : MonoBehaviour
{
    public abstract string PanelName { get; }

    public virtual void OnPanelShown() { }
    public virtual void OnPanelHidden() { }
    public virtual void SetFullscreen(bool fullscreen) { }
}
