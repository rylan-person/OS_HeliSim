using UnityEngine;
using UnityEngine.UI;

public abstract class InFlightMenuPage : MonoBehaviour
{
    [SerializeField] private string pageId;
    [SerializeField] private GameObject contentRoot;
    [SerializeField] private Selectable initialSelectable;

    public string PageId => pageId;
    public GameObject ContentRoot => contentRoot;
    public Selectable InitialSelectable => initialSelectable;
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
    [SerializeField] private Button navigationButton;

    public InFlightMenuPage Page => page;
    public Button NavigationButton => navigationButton;
}
