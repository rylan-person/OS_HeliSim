using UnityEngine;

public class DashboardSlot : MonoBehaviour
{
    private DashboardPanel currentPanel;
    public bool isFullscreen = false;

    public void SetPanel(DashboardPanel panelPrefab)
    {
        if (currentPanel != null)
        {
            currentPanel.OnPanelHidden();
            Destroy(currentPanel.gameObject);
        }

        currentPanel = Instantiate(panelPrefab, transform);
        currentPanel.OnPanelShown();
    }

    public DashboardPanel GetCurrentPanel()
    {
        return currentPanel;
    }
}