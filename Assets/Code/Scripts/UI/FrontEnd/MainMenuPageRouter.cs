using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HeliSim.FrontEnd
{
    [Serializable]
    public sealed class MainMenuPageEntry
    {
        public string id;
        public GameObject root;
        public Button navigationButton;
        public InFlightMenuPage settingsPage;
        public Selectable initialSelectable;
    }

    public sealed class MainMenuPageRouter : MonoBehaviour
    {
        [SerializeField] private MainMenuPageEntry[] pages = Array.Empty<MainMenuPageEntry>();
        [SerializeField] private GameObject confirmationRoot;
        [SerializeField] private Button applyChangesButton;
        [SerializeField] private Button discardChangesButton;
        [SerializeField] private Button cancelChangesButton;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private ScrollRect pageScrollRect;

        private readonly Dictionary<string, MainMenuPageEntry> pagesById = new Dictionary<string, MainMenuPageEntry>(StringComparer.Ordinal);
        private readonly List<MainMenuPageEntry> validPages = new List<MainMenuPageEntry>();
        private readonly Dictionary<Button, UnityAction> navigationListeners = new Dictionary<Button, UnityAction>();
        private readonly Dictionary<Selectable, bool> suspendedSelectables = new Dictionary<Selectable, bool>();
        private MainMenuPageEntry activePage;
        private Action pendingAction;
        private GameObject selectionBeforeConfirmation;
        private bool started;
        private GameObject lastObservedSelection;
        private readonly Vector3[] scrollCorners = new Vector3[4];
        private readonly List<TMP_Dropdown> menuDropdowns = new List<TMP_Dropdown>();
        private bool dropdownWasExpanded;

        public string ActivePageId => activePage != null ? activePage.id : null;
        public bool HasPendingOperation => pendingAction != null;
        public bool NavigationSuspended { get; set; }
        public event Action<string> PageChanged;

        private void Awake()
        {
            Initialize();
        }

        private void Start()
        {
            started = true;
            SelectInitialPage();
        }

        private void OnEnable()
        {
            if (started) SelectInitialPage();
        }

        private void OnDisable()
        {
            dropdownWasExpanded = false;
            lastObservedSelection = null;
            ClearPending();
            DeselectActivePage();
            SetAllRootsInactive();
        }

        private void OnDestroy()
        {
            UnwireListeners();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                HandleEscape();
            }
        }

        private void LateUpdate()
        {
            RevealChangedSelection();
            dropdownWasExpanded = FindExpandedDropdown() != null;
        }

        public void SetScrollRect(ScrollRect scrollRect)
        {
            pageScrollRect = scrollRect;
            lastObservedSelection = null;
        }

        public void Configure(MainMenuPageEntry[] entries, GameObject confirmationRoot, Button apply,
            Button discard, Button cancel, EventSystem eventSystem)
        {
            ClearPending();
            DeselectActivePage();
            SetAllRootsInactive();
            UnwireListeners();
            pages = entries ?? Array.Empty<MainMenuPageEntry>();
            this.confirmationRoot = confirmationRoot;
            applyChangesButton = apply;
            discardChangesButton = discard;
            cancelChangesButton = cancel;
            this.eventSystem = eventSystem;
            Initialize();
            if (started) SelectInitialPage();
        }

        // False means no transition occurred; a dirty page can have queued the request.
        public bool SelectPage(string id)
        {
            if (NavigationSuspended || HasPendingOperation || string.IsNullOrWhiteSpace(id) || !pagesById.TryGetValue(id, out MainMenuPageEntry next))
            {
                return false;
            }

            if (activePage == next) return true;
            if (IsActivePageDirty())
            {
                ShowConfirmation(() => SelectPageNow(next));
                return false;
            }

            SelectPageNow(next);
            return true;
        }

        public void SelectPageFromButton(string id) => SelectPage(id);

        public void RequestAction(Action action)
        {
            if (NavigationSuspended || action == null || HasPendingOperation) return;
            if (IsActivePageDirty()) ShowConfirmation(action);
            else action();
        }

        public void ResolvePending(DirtyPageChoice choice)
        {
            if (!HasPendingOperation) return;
            if (choice == DirtyPageChoice.Cancel)
            {
                GameObject previousSelection = selectionBeforeConfirmation;
                ClearPending();
                RestoreSelection(previousSelection);
                return;
            }

            if (choice != DirtyPageChoice.Apply && choice != DirtyPageChoice.Discard) return;
            InFlightMenuPage settings = activePage != null ? activePage.settingsPage : null;
            if (settings != null)
            {
                if (choice == DirtyPageChoice.Apply && !settings.TryApplyChanges()) return;
                if (choice == DirtyPageChoice.Discard) settings.DiscardChanges();
            }

            Action action = pendingAction;
            GameObject savedSelection = selectionBeforeConfirmation;
            ClearPending();
            RestoreSelection(savedSelection);
            action();
        }

        private void HandleEscape()
        {
            if (NavigationSuspended) return;
            TMP_Dropdown expandedDropdown = FindExpandedDropdown();
            if (expandedDropdown != null)
            {
                expandedDropdown.Hide();
                // TMP restores selection through EventSystem.current; honor the menu's configured system.
                Select(expandedDropdown);
                dropdownWasExpanded = false;
                return;
            }
            // The UI input module may have handled Cancel earlier in this frame.
            if (dropdownWasExpanded)
            {
                dropdownWasExpanded = false;
                return;
            }
            if (activePage != null && activePage.settingsPage is ControlsRebindingPage controls && controls.TryHandleEscape()) return;
            if (HasPendingOperation) ResolvePending(DirtyPageChoice.Cancel);
            else SelectPage("fly");
        }

        private TMP_Dropdown FindExpandedDropdown()
        {
            GetComponentsInChildren(false, menuDropdowns);
            foreach (TMP_Dropdown dropdown in menuDropdowns)
            {
                if (dropdown.IsExpanded) return dropdown;
            }
            if (activePage != null && !activePage.root.transform.IsChildOf(transform))
            {
                activePage.root.GetComponentsInChildren(false, menuDropdowns);
                foreach (TMP_Dropdown dropdown in menuDropdowns)
                {
                    if (dropdown.IsExpanded) return dropdown;
                }
            }
            return null;
        }

        private void Initialize()
        {
            UnwireListeners();
            pagesById.Clear();
            validPages.Clear();
            HashSet<GameObject> roots = new HashSet<GameObject>();
            foreach (MainMenuPageEntry entry in pages ?? Array.Empty<MainMenuPageEntry>())
            {
                if (entry == null) continue;
                if (entry.root != null && !transform.IsChildOf(entry.root.transform)) entry.root.SetActive(false);
                if (string.IsNullOrWhiteSpace(entry.id) || entry.root == null || transform.IsChildOf(entry.root.transform))
                {
                    Debug.LogError("Main menu pages require a non-empty id and a page root separate from the router.", this);
                    continue;
                }
                if (pagesById.ContainsKey(entry.id) || !roots.Add(entry.root))
                {
                    Debug.LogError("Duplicate main menu page id or root: " + entry.id, this);
                    continue;
                }
                pagesById.Add(entry.id, entry);
                validPages.Add(entry);
            }
            if (confirmationRoot != null) confirmationRoot.SetActive(false);
            foreach (MainMenuPageEntry entry in validPages)
            {
                if (entry.navigationButton == null || navigationListeners.ContainsKey(entry.navigationButton)) continue;
                string id = entry.id;
                UnityAction listener = () => SelectPage(id);
                navigationListeners.Add(entry.navigationButton, listener);
                entry.navigationButton.onClick.AddListener(listener);
            }
            if (applyChangesButton != null) applyChangesButton.onClick.AddListener(ApplyPending);
            if (discardChangesButton != null) discardChangesButton.onClick.AddListener(DiscardPending);
            if (cancelChangesButton != null) cancelChangesButton.onClick.AddListener(CancelPending);
            foreach (Selectable selectable in GetMenuSelectables())
            {
                if (selectable.navigation.mode != Navigation.Mode.None) continue;
                Navigation navigation = selectable.navigation;
                navigation.mode = Navigation.Mode.Automatic;
                selectable.navigation = navigation;
            }
        }

        private void UnwireListeners()
        {
            foreach (KeyValuePair<Button, UnityAction> pair in navigationListeners)
            {
                if (pair.Key != null) pair.Key.onClick.RemoveListener(pair.Value);
            }
            navigationListeners.Clear();
            if (applyChangesButton != null) applyChangesButton.onClick.RemoveListener(ApplyPending);
            if (discardChangesButton != null) discardChangesButton.onClick.RemoveListener(DiscardPending);
            if (cancelChangesButton != null) cancelChangesButton.onClick.RemoveListener(CancelPending);
        }

        private void ApplyPending() => ResolvePending(DirtyPageChoice.Apply);
        private void DiscardPending() => ResolvePending(DirtyPageChoice.Discard);
        private void CancelPending() => ResolvePending(DirtyPageChoice.Cancel);
        private bool IsActivePageDirty() => activePage != null && activePage.settingsPage != null && activePage.settingsPage.IsDirty;

        private void SelectInitialPage()
        {
            if (activePage != null || validPages.Count == 0) return;
            SelectPage(pagesById.ContainsKey("fly") ? "fly" : validPages[0].id);
        }

        private void SelectPageNow(MainMenuPageEntry next)
        {
            DeselectActivePage();
            SetAllRootsInactive();
            activePage = next;
            next.root.SetActive(true);
            if (next.settingsPage != null) next.settingsPage.OnPageSelected();
            ResetPageScroll();
            Select(GetInitialSelectable(next));
            lastObservedSelection = null;
            RevealChangedSelection();
            PageChanged?.Invoke(next.id);
        }

        private void ResetPageScroll()
        {
            if (pageScrollRect == null || pageScrollRect.content == null) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(pageScrollRect.content);
            Canvas.ForceUpdateCanvases();
            pageScrollRect.StopMovement();
            pageScrollRect.verticalNormalizedPosition = 1f;
        }

        private void RevealChangedSelection()
        {
            EventSystem system = GetEventSystem();
            GameObject selected = system != null ? system.currentSelectedGameObject : null;
            if (selected == lastObservedSelection) return;
            lastObservedSelection = selected;
            if (NavigationSuspended || HasPendingOperation || pageScrollRect == null || !pageScrollRect.isActiveAndEnabled ||
                pageScrollRect.content == null || selected == null || !selected.activeInHierarchy ||
                !selected.transform.IsChildOf(pageScrollRect.content)) return;

            TMP_Dropdown dropdown = selected.GetComponentInParent<TMP_Dropdown>();
            if (dropdown != null && dropdown.IsExpanded) return;

            RectTransform selectedRect = selected.transform as RectTransform;
            RectTransform viewport = pageScrollRect.viewport != null ? pageScrollRect.viewport : pageScrollRect.transform as RectTransform;
            if (selectedRect == null || viewport == null || !pageScrollRect.vertical) return;
            Canvas.ForceUpdateCanvases();
            Bounds selectedBounds = GetBoundsInViewport(selectedRect, viewport);
            Bounds contentBounds = GetBoundsInViewport(pageScrollRect.content, viewport);
            float hiddenHeight = contentBounds.size.y - viewport.rect.height;
            if (hiddenHeight <= 0f) return;

            float offset = 0f;
            if (selectedBounds.max.y > viewport.rect.yMax) offset = viewport.rect.yMax - selectedBounds.max.y;
            else if (selectedBounds.min.y < viewport.rect.yMin) offset = viewport.rect.yMin - selectedBounds.min.y;
            if (Mathf.Approximately(offset, 0f)) return;

            pageScrollRect.StopMovement();
            pageScrollRect.verticalNormalizedPosition = Mathf.Clamp01(pageScrollRect.verticalNormalizedPosition - offset / hiddenHeight);
        }

        private Bounds GetBoundsInViewport(RectTransform rect, RectTransform viewport)
        {
            rect.GetWorldCorners(scrollCorners);
            Bounds bounds = new Bounds(viewport.InverseTransformPoint(scrollCorners[0]), Vector3.zero);
            for (int index = 1; index < scrollCorners.Length; index++) bounds.Encapsulate(viewport.InverseTransformPoint(scrollCorners[index]));
            return bounds;
        }

        private void DeselectActivePage()
        {
            if (activePage != null && activePage.settingsPage != null) activePage.settingsPage.OnPageDeselected();
            activePage = null;
        }

        private void SetAllRootsInactive()
        {
            foreach (MainMenuPageEntry entry in validPages)
            {
                if (entry.root != null) entry.root.SetActive(false);
            }
        }

        private void ShowConfirmation(Action action)
        {
            if (confirmationRoot == null || cancelChangesButton == null)
            {
                Debug.LogWarning("Unsaved settings require a configured confirmation dialog before leaving this page.", this);
                return;
            }
            pendingAction = action;
            EventSystem system = GetEventSystem();
            selectionBeforeConfirmation = system != null ? system.currentSelectedGameObject : null;
            foreach (Selectable selectable in GetMenuSelectables())
            {
                if (selectable.transform.IsChildOf(confirmationRoot.transform)) continue;
                suspendedSelectables.Add(selectable, selectable.interactable);
                selectable.interactable = false;
            }
            confirmationRoot.SetActive(true);
            List<Selectable> actions = new List<Selectable>();
            if (applyChangesButton != null) actions.Add(applyChangesButton);
            if (discardChangesButton != null) actions.Add(discardChangesButton);
            actions.Add(cancelChangesButton);
            for (int index = 0; index < actions.Count; index++)
            {
                Selectable previous = actions[(index + actions.Count - 1) % actions.Count];
                Selectable next = actions[(index + 1) % actions.Count];
                actions[index].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = previous, selectOnUp = previous,
                    selectOnRight = next, selectOnDown = next
                };
            }
            Select(cancelChangesButton);
        }

        private void ClearPending()
        {
            pendingAction = null;
            selectionBeforeConfirmation = null;
            if (confirmationRoot != null) confirmationRoot.SetActive(false);
            foreach (KeyValuePair<Selectable, bool> pair in suspendedSelectables)
            {
                if (pair.Key != null) pair.Key.interactable = pair.Value;
            }
            suspendedSelectables.Clear();
        }

        private HashSet<Selectable> GetMenuSelectables()
        {
            HashSet<Selectable> result = new HashSet<Selectable>(GetComponentsInChildren<Selectable>(true));
            foreach (MainMenuPageEntry entry in validPages)
            {
                foreach (Selectable selectable in entry.root.GetComponentsInChildren<Selectable>(true)) result.Add(selectable);
                if (entry.navigationButton != null) result.Add(entry.navigationButton);
            }
            return result;
        }

        private static Selectable GetInitialSelectable(MainMenuPageEntry page)
        {
            if (page == null) return null;
            if (page.initialSelectable != null) return page.initialSelectable;
            if (page.settingsPage != null)
            {
                // Shared in-flight pages may focus a footer action. Main-menu entry
                // starts at the first usable control after dynamic rows are built.
                foreach (Selectable control in page.root.GetComponentsInChildren<Selectable>())
                {
                    if (control.IsActive() && control.IsInteractable()) return control;
                }
                if (page.settingsPage.InitialSelectable != null) return page.settingsPage.InitialSelectable;
            }
            return page.navigationButton;
        }

        private void RestoreSelection(GameObject previous)
        {
            Selectable selectable = previous != null ? previous.GetComponent<Selectable>() : null;
            if (selectable != null && selectable.IsActive() && selectable.IsInteractable()) Select(selectable);
            else Select(GetInitialSelectable(activePage));
        }

        private void Select(Selectable selectable)
        {
            EventSystem system = GetEventSystem();
            if (system != null) system.SetSelectedGameObject(selectable != null && selectable.IsActive() && selectable.IsInteractable() ? selectable.gameObject : null);
        }

        private EventSystem GetEventSystem() => eventSystem != null ? eventSystem : EventSystem.current;
    }
}
