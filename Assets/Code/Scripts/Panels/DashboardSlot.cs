using UnityEngine;

public class DashboardSlot : MonoBehaviour
{
    private DashboardPanel currentPanel;
    public bool isFullscreen = false;
    public DashboardPanelType? CurrentPanelType { get; private set; }

    public void SetPanel(DashboardPanel panelPrefab, DashboardPanelType panelType)
    {
        if (currentPanel != null)
        {
            currentPanel.OnPanelHidden();
            Destroy(currentPanel.gameObject);
        }

        currentPanel = Instantiate(panelPrefab, transform);
        currentPanel.OwnerSlot = this;
        CurrentPanelType = panelType;
        currentPanel.OnPanelShown();
    }

    public DashboardPanel GetCurrentPanel()
    {
        return currentPanel;
    }

    public void ClearPanel()
    {
        if (currentPanel == null)
            return;

        currentPanel.OnPanelHidden();
        Destroy(currentPanel.gameObject);
        currentPanel = null;
        CurrentPanelType = null;
    }
}
