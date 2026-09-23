using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum DirtyPageChoice
{
    Apply,
    Discard,
    Cancel,
}

public sealed class PauseMenuController : MonoBehaviour
{
    private enum PendingOperation
    {
        None,
        Close,
        SelectPage,
    }

    [SerializeField] private List<PauseMenuPageRegistration> pages = new List<PauseMenuPageRegistration>();
    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private GameObject scrimRoot;
    [SerializeField] private GameObject confirmationRoot;
    [SerializeField] private UnityEngine.UI.Button resumeButton;
    [SerializeField] private UnityEngine.UI.Button applyChangesButton;
    [SerializeField] private UnityEngine.UI.Button discardChangesButton;
    [SerializeField] private UnityEngine.UI.Button cancelCloseButton;
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private ScrollRect pageScrollRect;

    private readonly Dictionary<string, InFlightMenuPage> pagesById = new Dictionary<string, InFlightMenuPage>();
    private readonly List<InFlightMenuPage> validPages = new List<InFlightMenuPage>();
    private InFlightMenuPage activePage;
    private PendingOperation pendingOperation;
    private string pendingPageId;
    private CursorLockMode cursorLockState;
    private bool cursorVisible;
    private bool isOpen;
    private bool listenersWired;
    private readonly List<Selectable> suspendedSelectables = new List<Selectable>();
    private readonly List<bool> suspendedInteractableStates = new List<bool>();
    private GameObject selectionBeforeConfirmation;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        BuildPageLookup();
        WireListeners();
        ConfigureAutomaticNavigation();
        SetAllPageRootsActive(false);

        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }
        if (scrimRoot != null) scrimRoot.SetActive(false);

        if (confirmationRoot != null)
        {
            confirmationRoot.SetActive(false);
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (activePage is ControlsRebindingPage controlsPage && controlsPage.TryHandleEscape())
            {
                return;
            }
            Toggle();
        }
    }

    public void Open()
    {
        if (isOpen)
        {
            return;
        }

        cursorLockState = Cursor.lockState;
        cursorVisible = Cursor.visible;
        isOpen = true;

        if (overlayRoot != null)
        {
            overlayRoot.SetActive(true);
        }
        if (scrimRoot != null) scrimRoot.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ConfigureAutomaticNavigation();
        SelectPageNow(validPages.Count > 0 ? validPages[0].PageId : null);
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        if (pendingOperation != PendingOperation.None)
        {
            ResolvePendingClose(DirtyPageChoice.Cancel);
            return;
        }

        if (activePage != null && activePage.IsDirty)
        {
            RequestDeferredOperation(PendingOperation.Close, null);
            return;
        }

        CloseNow();
    }

    public void Toggle()
    {
        if (isOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void SelectPage(string pageId)
    {
        if (!isOpen || pendingOperation != PendingOperation.None || string.IsNullOrWhiteSpace(pageId) || !pagesById.ContainsKey(pageId) || activePage != null && activePage.PageId == pageId)
        {
            return;
        }

        if (activePage != null && activePage.IsDirty)
        {
            RequestDeferredOperation(PendingOperation.SelectPage, pageId);
            return;
        }

        SelectPageNow(pageId);
    }

    public void ResolvePendingClose(DirtyPageChoice choice)
    {
        if (pendingOperation == PendingOperation.None)
        {
            return;
        }

        if (choice == DirtyPageChoice.Cancel)
        {
            GameObject previousSelection = selectionBeforeConfirmation;
            ClearPendingOperation();
            if (previousSelection != null && previousSelection.activeInHierarchy)
            {
                EventSystem selectionEventSystem = GetSelectionEventSystem();
                if (selectionEventSystem != null)
                {
                    selectionEventSystem.SetSelectedGameObject(previousSelection);
                }
            }
            else
            {
                Select(activePage != null && activePage.InitialSelectable != null ? activePage.InitialSelectable : resumeButton);
            }

            selectionBeforeConfirmation = null;
            return;
        }

        if (activePage != null)
        {
            if (choice == DirtyPageChoice.Apply && !activePage.TryApplyChanges())
            {
                return;
            }

            if (choice == DirtyPageChoice.Discard)
            {
                activePage.DiscardChanges();
            }
        }

        PendingOperation operation = pendingOperation;
        string pageId = pendingPageId;
        ClearPendingOperation();
        selectionBeforeConfirmation = null;

        if (operation == PendingOperation.Close)
        {
            CloseNow();
        }
        else if (operation == PendingOperation.SelectPage)
        {
            SelectPageNow(pageId);
        }
    }

    private void BuildPageLookup()
    {
        pagesById.Clear();
        validPages.Clear();

        foreach (PauseMenuPageRegistration registration in pages)
        {
            InFlightMenuPage page = registration != null ? registration.Page : null;
            if (page != null && page.ContentRoot != null)
            {
                page.ContentRoot.SetActive(false);
            }

            if (page == null || string.IsNullOrWhiteSpace(page.PageId))
            {
                Debug.LogError("In-flight menu page registrations require a page with a non-empty id.", this);
                continue;
            }

            if (pagesById.ContainsKey(page.PageId))
            {
                Debug.LogError("Duplicate in-flight menu page id: " + page.PageId, this);
                continue;
            }

            pagesById.Add(page.PageId, page);
            validPages.Add(page);
        }
    }

    private void WireListeners()
    {
        if (listenersWired)
        {
            return;
        }

        listenersWired = true;
        foreach (PauseMenuPageRegistration registration in pages)
        {
            if (registration == null || registration.Page == null || registration.NavigationButton == null || !pagesById.ContainsKey(registration.Page.PageId))
            {
                continue;
            }

            string pageId = registration.Page.PageId;
            registration.NavigationButton.onClick.AddListener(() => SelectPage(pageId));
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(Close);
        }

        if (applyChangesButton != null)
        {
            applyChangesButton.onClick.AddListener(() => ResolvePendingClose(DirtyPageChoice.Apply));
        }

        if (discardChangesButton != null)
        {
            discardChangesButton.onClick.AddListener(() => ResolvePendingClose(DirtyPageChoice.Discard));
        }

        if (cancelCloseButton != null)
        {
            cancelCloseButton.onClick.AddListener(() => ResolvePendingClose(DirtyPageChoice.Cancel));
        }
    }

    private void RequestDeferredOperation(PendingOperation operation, string pageId)
    {
        pendingOperation = operation;
        pendingPageId = pageId;
        EventSystem selectionEventSystem = GetSelectionEventSystem();
        selectionBeforeConfirmation = selectionEventSystem != null ? selectionEventSystem.currentSelectedGameObject : null;
        SuspendUnderlyingSelectables();
        if (confirmationRoot != null)
        {
            confirmationRoot.SetActive(true);
        }

        ConfigureAutomaticNavigation();
        ConfigureConfirmationNavigation();
        Select(cancelCloseButton != null ? cancelCloseButton : applyChangesButton != null ? applyChangesButton : discardChangesButton);
    }

    private void ClearPendingOperation()
    {
        pendingOperation = PendingOperation.None;
        pendingPageId = null;
        if (confirmationRoot != null)
        {
            confirmationRoot.SetActive(false);
        }

        RestoreUnderlyingSelectables();
    }

    private void SelectPageNow(string pageId)
    {
        InFlightMenuPage nextPage = null;
        if (!string.IsNullOrWhiteSpace(pageId))
        {
            pagesById.TryGetValue(pageId, out nextPage);
        }

        if (activePage != null)
        {
            activePage.OnPageDeselected();
        }

        SetAllPageRootsActive(false);
        activePage = nextPage;
        if (activePage == null)
        {
            Select(resumeButton);
            return;
        }

        if (activePage.ContentRoot != null)
        {
            activePage.ContentRoot.SetActive(true);
        }

        activePage.OnPageSelected();
        ResetPageScroll();
        Select(activePage.InitialSelectable != null ? activePage.InitialSelectable : resumeButton);
    }

    private void CloseNow()
    {
        ClearPendingOperation();
        selectionBeforeConfirmation = null;
        if (activePage != null)
        {
            activePage.OnPageDeselected();
            activePage = null;
        }

        SetAllPageRootsActive(false);
        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }
        if (scrimRoot != null) scrimRoot.SetActive(false);

        isOpen = false;
        Cursor.lockState = cursorLockState;
        Cursor.visible = cursorVisible;
    }

    private void SetAllPageRootsActive(bool active)
    {
        foreach (InFlightMenuPage page in validPages)
        {
            if (page.ContentRoot != null)
            {
                page.ContentRoot.SetActive(active);
            }
        }
    }

    private void ConfigureAutomaticNavigation()
    {
        foreach (Selectable selectable in GetMenuSelectables())
        {
            Navigation navigation = selectable.navigation;
            if (navigation.mode != Navigation.Mode.None)
            {
                continue;
            }

            navigation.mode = Navigation.Mode.Automatic;
            selectable.navigation = navigation;
        }
    }

    private void ResetPageScroll()
    {
        ScrollRect scrollRect = pageScrollRect;
        if (scrollRect == null && overlayRoot != null)
        {
            scrollRect = overlayRoot.GetComponentInChildren<ScrollRect>(true);
        }

        if (scrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        scrollRect.StopMovement();
        scrollRect.horizontalNormalizedPosition = 0f;
        scrollRect.verticalNormalizedPosition = 1f;
    }

    private void ConfigureConfirmationNavigation()
    {
        List<Selectable> confirmationActions = new List<Selectable>();
        AddConfirmationAction(applyChangesButton, confirmationActions);
        AddConfirmationAction(discardChangesButton, confirmationActions);
        AddConfirmationAction(cancelCloseButton, confirmationActions);

        for (int index = 0; index < confirmationActions.Count; index++)
        {
            Selectable action = confirmationActions[index];
            Selectable previous = confirmationActions[(index + confirmationActions.Count - 1) % confirmationActions.Count];
            Selectable next = confirmationActions[(index + 1) % confirmationActions.Count];
            Navigation navigation = action.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnLeft = previous;
            navigation.selectOnRight = next;
            navigation.selectOnUp = previous;
            navigation.selectOnDown = next;
            action.navigation = navigation;
        }
    }

    private static void AddConfirmationAction(Selectable action, ICollection<Selectable> confirmationActions)
    {
        if (action != null)
        {
            confirmationActions.Add(action);
        }
    }

    private void SuspendUnderlyingSelectables()
    {
        suspendedSelectables.Clear();
        suspendedInteractableStates.Clear();
        foreach (Selectable selectable in GetMenuSelectables())
        {
            if (IsConfirmationSelectable(selectable))
            {
                continue;
            }

            suspendedSelectables.Add(selectable);
            suspendedInteractableStates.Add(selectable.interactable);
            selectable.interactable = false;
        }
    }

    private void RestoreUnderlyingSelectables()
    {
        for (int index = 0; index < suspendedSelectables.Count; index++)
        {
            Selectable selectable = suspendedSelectables[index];
            if (selectable != null)
            {
                selectable.interactable = suspendedInteractableStates[index];
            }
        }

        suspendedSelectables.Clear();
        suspendedInteractableStates.Clear();
    }

    private IEnumerable<Selectable> GetMenuSelectables()
    {
        HashSet<Selectable> selectables = new HashSet<Selectable>();
        CollectSelectables(overlayRoot, selectables);
        CollectSelectables(resumeButton != null ? resumeButton.gameObject : null, selectables);

        foreach (PauseMenuPageRegistration registration in pages)
        {
            if (registration == null)
            {
                continue;
            }

            CollectSelectables(registration.NavigationButton != null ? registration.NavigationButton.gameObject : null, selectables);
            CollectSelectables(registration.Page != null ? registration.Page.ContentRoot : null, selectables);
        }

        return selectables;
    }

    private static void CollectSelectables(GameObject root, ISet<Selectable> selectables)
    {
        if (root == null)
        {
            return;
        }

        foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
        {
            selectables.Add(selectable);
        }
    }

    private bool IsConfirmationSelectable(Selectable selectable)
    {
        return confirmationRoot != null && selectable != null && selectable.transform.IsChildOf(confirmationRoot.transform);
    }

    private void Select(UnityEngine.UI.Selectable selectable)
    {
        EventSystem selectionEventSystem = GetSelectionEventSystem();
        if (selectionEventSystem == null)
        {
            Debug.LogWarning("In-flight menu could not select a control because no EventSystem is available.", this);
            return;
        }

        selectionEventSystem.SetSelectedGameObject(selectable != null ? selectable.gameObject : null);
    }

    private EventSystem GetSelectionEventSystem()
    {
        return eventSystem != null ? eventSystem : EventSystem.current;
    }
}
