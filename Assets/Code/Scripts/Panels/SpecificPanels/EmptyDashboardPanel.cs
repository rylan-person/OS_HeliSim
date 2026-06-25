using UnityEngine;
using UnityEngine.UI;

public class EmptyDashboardPanel : DashboardPanel
{
    public override string PanelName => "Empty";

    [SerializeField] private Button fullscreenButton;

    protected override void Awake()
    {
        base.Awake();

        if (fullscreenButton != null)
        {
            fullscreenButton.onClick.AddListener(() =>
            {
                var mgr = DashboardManager.Instance;
                if (mgr == null) return;

                if (mgr.focusedSlot == OwnerSlot)
                    mgr.Unfocus();
                else
                    mgr.FocusSlot(OwnerSlot);
            });
        }
    }
}
