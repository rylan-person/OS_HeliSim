using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
    [SerializeField] private GameObject confirmationRoot;
    [SerializeField] private UnityEngine.UI.Button resumeButton;
    [SerializeField] private UnityEngine.UI.Button applyChangesButton;
    [SerializeField] private UnityEngine.UI.Button discardChangesButton;
    [SerializeField] private UnityEngine.UI.Button cancelCloseButton;
    [SerializeField] private EventSystem eventSystem;

    private readonly Dictionary<string, InFlightMenuPage> pagesById = new Dictionary<string, InFlightMenuPage>();
    private readonly List<InFlightMenuPage> validPages = new List<InFlightMenuPage>();
    private InFlightMenuPage activePage;
    private PendingOperation pendingOperation;
    private string pendingPageId;
    private CursorLockMode cursorLockState;
    private bool cursorVisible;
    private bool isOpen;
    private bool listenersWired;

    private void Awake()
    {
        BuildPageLookup();
        WireListeners();
        SetAllPageRootsActive(false);

        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }

        if (confirmationRoot != null)
        {
            confirmationRoot.SetActive(false);
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
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

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SelectPageNow(validPages.Count > 0 ? validPages[0].PageId : null);
    }

    public void Close()
    {
        if (!isOpen)
        {
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
        if (!isOpen || string.IsNullOrWhiteSpace(pageId) || !pagesById.ContainsKey(pageId) || activePage != null && activePage.PageId == pageId)
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
            ClearPendingOperation();
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
        if (confirmationRoot != null)
        {
            confirmationRoot.SetActive(true);
        }
    }

    private void ClearPendingOperation()
    {
        pendingOperation = PendingOperation.None;
        pendingPageId = null;
        if (confirmationRoot != null)
        {
            confirmationRoot.SetActive(false);
        }
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
        Select(activePage.InitialSelectable != null ? activePage.InitialSelectable : resumeButton);
    }

    private void CloseNow()
    {
        ClearPendingOperation();
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

    private void Select(UnityEngine.UI.Selectable selectable)
    {
        EventSystem selectionEventSystem = eventSystem != null ? eventSystem : EventSystem.current;
        if (selectionEventSystem == null)
        {
            Debug.LogWarning("In-flight menu could not select a control because no EventSystem is available.", this);
            return;
        }

        selectionEventSystem.SetSelectedGameObject(selectable != null ? selectable.gameObject : null);
    }
}
