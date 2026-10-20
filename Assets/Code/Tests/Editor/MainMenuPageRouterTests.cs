using System.Collections.Generic;
using System.Reflection;
using HeliSim.FrontEnd;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class MainMenuPageRouterTests
{
    private readonly List<GameObject> roots = new List<GameObject>();
    private MainMenuPageRouter router;
    private MainMenuPageEntry fly;
    private MainMenuPageEntry settings;
    private RouterSettingsPage draft;
    private GameObject modal;
    private Button apply;
    private Button discard;
    private Button cancel;
    private EventSystem eventSystem;

    [SetUp]
    public void SetUp()
    {
        GameObject root = Create("Menu");
        router = root.AddComponent<MainMenuPageRouter>();
        eventSystem = Create("EventSystem").AddComponent<EventSystem>();
        fly = Page("fly", root.transform);
        settings = Page("settings", root.transform);
        draft = settings.root.AddComponent<RouterSettingsPage>();
        settings.settingsPage = draft;
        modal = Create("Confirmation", root.transform);
        apply = Button("Apply", modal.transform);
        discard = Button("Discard", modal.transform);
        cancel = Button("Cancel", modal.transform);
        router.Configure(new[] { fly, settings }, modal, apply, discard, cancel, eventSystem);
        router.SelectPage("fly");
    }

    [TearDown]
    public void TearDown()
    {
        for (int index = roots.Count - 1; index >= 0; index--)
        {
            if (roots[index] != null) Object.DestroyImmediate(roots[index]);
        }
        roots.Clear();
    }

    [Test]
    public void SelectPage_ChangesOnlyRegisteredRootAndMovesKeyboardFocus()
    {
        string changed = null;
        router.PageChanged += id => changed = id;

        Assert.That(router.SelectPage("settings"), Is.True);

        Assert.That(router.ActivePageId, Is.EqualTo("settings"));
        Assert.That(fly.root.activeSelf, Is.False);
        Assert.That(settings.root.activeSelf, Is.True);
        Assert.That(draft.Selected, Is.EqualTo(1));
        Assert.That(changed, Is.EqualTo("settings"));
        Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(settings.initialSelectable.gameObject));
        Assert.That(router.SelectPage("missing"), Is.False);
        Assert.That(settings.root.activeSelf, Is.True);
    }

    [Test]
    public void DirtyNavigation_CancelKeepsDraftAndRestoresFocusAndDisabledState()
    {
        router.SelectPage("settings");
        draft.Dirty = true;
        fly.navigationButton.interactable = false;

        Assert.That(router.SelectPage("fly"), Is.False);

        Assert.That(router.HasPendingOperation, Is.True);
        Assert.That(modal.activeSelf, Is.True);
        Assert.That(settings.initialSelectable.interactable, Is.False);
        Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(cancel.gameObject));
        router.ResolvePending(DirtyPageChoice.Cancel);
        Assert.That(router.ActivePageId, Is.EqualTo("settings"));
        Assert.That(draft.Dirty, Is.True);
        Assert.That(draft.Deselected, Is.Zero);
        Assert.That(modal.activeSelf, Is.False);
        Assert.That(settings.initialSelectable.interactable, Is.True);
        Assert.That(fly.navigationButton.interactable, Is.False);
        Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(settings.initialSelectable.gameObject));
    }

    [Test]
    public void ApplyFailure_KeepsModalDraftAndDeferredOperation()
    {
        router.SelectPage("settings");
        draft.Dirty = true;
        draft.ApplySucceeds = false;
        router.SelectPage("fly");

        apply.onClick.Invoke();

        Assert.That(draft.ApplyCalls, Is.EqualTo(1));
        Assert.That(draft.Dirty, Is.True);
        Assert.That(router.HasPendingOperation, Is.True);
        Assert.That(modal.activeSelf, Is.True);
        Assert.That(router.ActivePageId, Is.EqualTo("settings"));
        Assert.That(settings.initialSelectable.interactable, Is.False);
    }

    [Test]
    public void DiscardNavigation_DiscardsBeforeDeselectionAndActivatesDestination()
    {
        router.SelectPage("settings");
        draft.Dirty = true;
        router.SelectPage("fly");

        discard.onClick.Invoke();

        Assert.That(draft.DiscardCalls, Is.EqualTo(1));
        Assert.That(draft.DirtyAtDeselection, Is.False);
        Assert.That(draft.Deselected, Is.EqualTo(1));
        Assert.That(router.ActivePageId, Is.EqualTo("fly"));
        Assert.That(router.HasPendingOperation, Is.False);
        Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(fly.initialSelectable.gameObject));
    }

    [Test]
    public void RequestAction_AppliesDirtySettingsBeforeExecutingOnce()
    {
        router.SelectPage("settings");
        draft.Dirty = true;
        int calls = 0;
        router.RequestAction(() => { Assert.That(draft.Dirty, Is.False); calls++; });
        router.RequestAction(() => calls += 100);
        Assert.That(calls, Is.Zero);

        router.ResolvePending(DirtyPageChoice.Apply);
        router.ResolvePending(DirtyPageChoice.Apply);

        Assert.That(calls, Is.EqualTo(1));
        Assert.That(draft.ApplyCalls, Is.EqualTo(1));
        Assert.That(router.HasPendingOperation, Is.False);
        Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(settings.initialSelectable.gameObject));
    }

    [Test]
    public void RequestAction_CancelNeverExecutesDeferredAction()
    {
        router.SelectPage("settings");
        draft.Dirty = true;
        int calls = 0;
        router.RequestAction(() => calls++);
        cancel.onClick.Invoke();
        router.ResolvePending(DirtyPageChoice.Discard);

        Assert.That(calls, Is.Zero);
        Assert.That(draft.Dirty, Is.True);
    }

    [Test]
    public void NavigationButtons_AreRewiredWithoutDuplicateCallbacksOnConfigure()
    {
        router.Configure(new[] { fly, settings }, modal, apply, discard, cancel, eventSystem);
        int changes = 0;
        router.PageChanged += _ => changes++;

        settings.navigationButton.onClick.Invoke();

        Assert.That(changes, Is.EqualTo(1));
        Assert.That(draft.Selected, Is.EqualTo(1));
    }

    [Test]
    public void Escape_CancelsDirtyModalBeforeReturningToFly()
    {
        router.SelectPage("settings");
        draft.Dirty = true;
        HandleEscape();
        Assert.That(router.HasPendingOperation, Is.True);
        HandleEscape();
        Assert.That(router.HasPendingOperation, Is.False);
        Assert.That(router.ActivePageId, Is.EqualTo("settings"));
        draft.Dirty = false;
        HandleEscape();
        Assert.That(router.ActivePageId, Is.EqualTo("fly"));
    }

    [Test]
    public void Start_PrefersFlyRegardlessOfRegistrationOrder()
    {
        router.Configure(new[] { settings, fly }, modal, apply, discard, cancel, eventSystem);

        typeof(MainMenuPageRouter).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(router, null);

        Assert.That(router.ActivePageId, Is.EqualTo("fly"));
        Assert.That(fly.root.activeSelf, Is.True);
        Assert.That(settings.root.activeSelf, Is.False);
    }

    [Test]
    public void Start_WithoutFlySelectsFirstRegistration()
    {
        router.Configure(new[] { settings }, modal, apply, discard, cancel, eventSystem);

        typeof(MainMenuPageRouter).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(router, null);

        Assert.That(router.ActivePageId, Is.EqualTo("settings"));
        Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(settings.initialSelectable.gameObject));
    }

    [Test]
    public void DuplicateId_RejectsSecondRootWithoutLeavingItVisible()
    {
        settings.id = "fly";
        settings.root.SetActive(true);
        LogAssert.Expect(LogType.Error, "Duplicate main menu page id or root: fly");

        router.Configure(new[] { fly, settings }, modal, apply, discard, cancel, eventSystem);
        router.SelectPage("fly");

        Assert.That(fly.root.activeSelf, Is.True);
        Assert.That(settings.root.activeSelf, Is.False);
        Assert.That(draft.Selected, Is.Zero);
    }

    [Test]
    public void DisablingRouter_CancelsPendingActionAndDeselectsSettings()
    {
        router.SelectPage("settings");
        draft.Dirty = true;
        int calls = 0;
        router.RequestAction(() => calls++);

        typeof(MainMenuPageRouter).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(router, null);
        router.ResolvePending(DirtyPageChoice.Apply);

        Assert.That(calls, Is.Zero);
        Assert.That(router.HasPendingOperation, Is.False);
        Assert.That(draft.Deselected, Is.EqualTo(1));
        Assert.That(settings.root.activeSelf, Is.False);
        Assert.That(settings.initialSelectable.interactable, Is.True);
    }

    [Test]
    public void PageSwitch_ResetsSharedScrollPositionToTop()
    {
        ScrollRect scroll = CreatePageScroll();
        router.SetScrollRect(scroll);
        scroll.verticalNormalizedPosition = 0.2f;

        router.SelectPage("settings");

        Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void KeyboardFocusBelowViewport_IsRevealedWithoutOverridingSubsequentMouseScroll()
    {
        ScrollRect scroll = CreatePageScroll();
        router.SetScrollRect(scroll);
        router.SelectPage("settings");
        Button lowerButton = Button("LowerControl", settings.root.transform);
        RectTransform lower = lowerButton.GetComponent<RectTransform>();
        lower.anchorMin = lower.anchorMax = new Vector2(0f, 1f);
        lower.pivot = new Vector2(0f, 1f);
        lower.sizeDelta = new Vector2(100f, 30f);
        lower.anchoredPosition = new Vector2(0f, -350f);
        Canvas.ForceUpdateCanvases();

        eventSystem.SetSelectedGameObject(lowerButton.gameObject);
        RunLateUpdate();

        Bounds focusedBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, lower);
        Assert.That(focusedBounds.min.y, Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - 0.01f));
        Assert.That(focusedBounds.max.y, Is.LessThanOrEqualTo(scroll.viewport.rect.yMax + 0.01f));
        Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(1f));
        scroll.verticalNormalizedPosition = 1f;
        RunLateUpdate();
        Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void SuspendedNavigation_IgnoresPageActionAndEscapeUntilReleased()
    {
        router.SelectPage("settings");
        int actions = 0;
        router.NavigationSuspended = true;

        Assert.That(router.SelectPage("fly"), Is.False);
        router.RequestAction(() => actions++);
        HandleEscape();

        Assert.That(router.ActivePageId, Is.EqualTo("settings"));
        Assert.That(actions, Is.Zero);
        Assert.That(router.HasPendingOperation, Is.False);
        router.NavigationSuspended = false;
        HandleEscape();
        Assert.That(router.ActivePageId, Is.EqualTo("fly"));
    }

    [Test]
    public void Escape_WithExpandedDropdownKeepsCurrentPage()
    {
        router.SelectPage("settings");
        TMP_Dropdown dropdown = CreateExpandedDropdown();
        try
        {
            HandleEscape();

            Assert.That(router.ActivePageId, Is.EqualTo("settings"));
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(dropdown.gameObject));
        }
        finally
        {
            ClearDropdownPopup(dropdown);
        }
    }

    [Test]
    public void Escape_AfterInputModuleClosedObservedDropdownDoesNotAlsoNavigate()
    {
        router.SelectPage("settings");
        TMP_Dropdown dropdown = CreateExpandedDropdown();
        RunLateUpdate();
        ClearDropdownPopup(dropdown);

        HandleEscape();

        Assert.That(router.ActivePageId, Is.EqualTo("settings"));
    }

    private TMP_Dropdown CreateExpandedDropdown()
    {
        TMP_Dropdown dropdown = Create("Dropdown", settings.root.transform).AddComponent<TMP_Dropdown>();
        GameObject popup = Create("ExpandedPopup", dropdown.transform);
        popup.AddComponent<CanvasGroup>().alpha = 0f;
        // Configure the package's open-popup state without running its animated PlayMode coroutine.
        typeof(TMP_Dropdown).GetField("m_Dropdown", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(dropdown, popup);
        return dropdown;
    }

    private static void ClearDropdownPopup(TMP_Dropdown dropdown)
    {
        dropdown.StopAllCoroutines();
        typeof(TMP_Dropdown).GetField("m_Dropdown", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(dropdown, null);
    }

    private void RunLateUpdate()
    {
        typeof(MainMenuPageRouter).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(router, null);
    }

    private ScrollRect CreatePageScroll()
    {
        GameObject scrollRoot = Create("PageScroll", router.transform);
        ScrollRect scroll = scrollRoot.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.viewport = Create("Viewport", scrollRoot.transform).GetComponent<RectTransform>();
        scroll.viewport.sizeDelta = new Vector2(200f, 100f);
        scroll.content = Create("Content", scroll.viewport).GetComponent<RectTransform>();
        scroll.content.anchorMin = scroll.content.anchorMax = new Vector2(0f, 1f);
        scroll.content.pivot = new Vector2(0f, 1f);
        scroll.content.sizeDelta = new Vector2(200f, 500f);
        foreach (MainMenuPageEntry entry in new[] { fly, settings })
        {
            RectTransform pageRoot = entry.root.GetComponent<RectTransform>();
            pageRoot.SetParent(scroll.content, false);
            pageRoot.anchorMin = pageRoot.anchorMax = new Vector2(0f, 1f);
            pageRoot.pivot = new Vector2(0f, 1f);
            pageRoot.sizeDelta = new Vector2(200f, 500f);
            RectTransform initial = entry.initialSelectable.GetComponent<RectTransform>();
            initial.anchorMin = initial.anchorMax = new Vector2(0f, 1f);
            initial.pivot = new Vector2(0f, 1f);
            initial.sizeDelta = new Vector2(100f, 30f);
            initial.anchoredPosition = Vector2.zero;
        }
        Canvas.ForceUpdateCanvases();
        return scroll;
    }

    private void HandleEscape()
    {
        typeof(MainMenuPageRouter).GetMethod("HandleEscape", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(router, null);
    }

    private MainMenuPageEntry Page(string id, Transform parent)
    {
        GameObject root = Create(id, parent);
        return new MainMenuPageEntry
        {
            id = id,
            root = root,
            navigationButton = Button(id + "Nav", parent),
            initialSelectable = Button(id + "Initial", root.transform)
        };
    }

    private GameObject Create(string name, Transform parent = null)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        if (parent != null) result.transform.SetParent(parent, false);
        roots.Add(result);
        return result;
    }

    private Button Button(string name, Transform parent)
    {
        return Create(name, parent).AddComponent<Button>();
    }

    private sealed class RouterSettingsPage : InFlightMenuPage
    {
        public bool Dirty;
        public bool ApplySucceeds = true;
        public int Selected;
        public int Deselected;
        public int ApplyCalls;
        public int DiscardCalls;
        public bool DirtyAtDeselection;
        public override bool IsDirty => Dirty;
        public override void OnPageSelected() { Selected++; }
        public override void OnPageDeselected() { Deselected++; DirtyAtDeselection = Dirty; }
        public override bool TryApplyChanges()
        {
            ApplyCalls++;
            if (ApplySucceeds) Dirty = false;
            return ApplySucceeds;
        }
        public override void DiscardChanges() { DiscardCalls++; Dirty = false; }
    }
}
