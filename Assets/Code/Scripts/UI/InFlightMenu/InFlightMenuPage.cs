using UnityEngine;

public abstract class InFlightMenuPage : MonoBehaviour
{
    [SerializeField] private string pageId;
    [SerializeField] private GameObject contentRoot;
    [SerializeField] private UnityEngine.UI.Selectable initialSelectable;

    public string PageId => pageId;
    public GameObject ContentRoot => contentRoot;
    public UnityEngine.UI.Selectable InitialSelectable => initialSelectable;
    public virtual bool IsDirty => false;

    public virtual void OnPageSelected()
    {
    }

    public virtual void OnPageDeselected()
    {
    }

    public virtual bool TryApplyChanges()
    {
        return true;
    }

    public virtual void DiscardChanges()
    {
    }
}

[System.Serializable]
public sealed class PauseMenuPageRegistration
{
    [SerializeField] private InFlightMenuPage page;
    [SerializeField] private UnityEngine.UI.Button navigationButton;

    public InFlightMenuPage Page => page;
    public UnityEngine.UI.Button NavigationButton => navigationButton;
}
