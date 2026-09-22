using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class PauseMenuControllerTests
{
    private readonly List<Object> createdObjects = new List<Object>();
    private PauseMenuController controller;
    private GameObject overlayRoot;
    private GameObject confirmationRoot;
    private EventSystem eventSystem;
    private TestInFlightMenuPage firstPage;
    private TestInFlightMenuPage secondPage;
    private TestInFlightMenuPage dirtyPage;

    [SetUp]
    public void SetUp()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        overlayRoot = CreateGameObject("Overlay");
        confirmationRoot = CreateGameObject("Confirmation");
        eventSystem = CreateGameObject("EventSystem").AddComponent<EventSystem>();
        firstPage = CreatePage("first");
        secondPage = CreatePage("second");
        dirtyPage = CreatePage("dirty");
        controller = CreateController(new[] { firstPage, secondPage, dirtyPage });
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object createdObject in createdObjects)
        {
            Object.DestroyImmediate(createdObject);
        }

        createdObjects.Clear();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    [Test]
    public void DuplicatePageIds_AreRejectedWithoutOpeningTwoPages()
    {
        Object.DestroyImmediate(controller.gameObject);
        firstPage.SetPageId("duplicate");
        secondPage.SetPageId("duplicate");
        secondPage.ContentRoot.SetActive(true);
        LogAssert.Expect(LogType.Error, new Regex("Duplicate in-flight menu page id"));
        controller = CreateController(new[] { firstPage, secondPage });

        controller.Open();

        Assert.That(firstPage.ContentRoot.activeSelf, Is.True);
        Assert.That(secondPage.ContentRoot.activeSelf, Is.False);
    }

    [Test]
    public void RequestClose_WhenActivePageIsDirty_ShowsConfirmationAndKeepsOverlayOpen()
    {
        dirtyPage.SetDirty(true);
        controller.Open();
        controller.SelectPage("dirty");

        controller.Close();

        Assert.That(overlayRoot.activeSelf, Is.True);
        Assert.That(confirmationRoot.activeSelf, Is.True);
    }

    [Test]
    public void RequestClose_WhenActivePageIsDirty_SelectsModalActionAndDisablesUnderlyingControls()
    {
        Button initialButton = CreateButton("Initial", firstPage.ContentRoot.transform);
        firstPage.SetInitialSelectable(initialButton);
        Button cancelButton = CreateButton("Cancel", confirmationRoot.transform);
        SetPrivateField(controller, "cancelCloseButton", cancelButton);
        dirtyPage.SetDirty(true);
        controller.Open();
        controller.SelectPage("dirty");

        controller.Close();

        Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(cancelButton.gameObject));
        Assert.That(initialButton.interactable, Is.False);
        Assert.That(cancelButton.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
    }

    [Test]
    public void RequestReturnToMenu_LogsWarningAndDoesNotLoadAnotherScene()
    {
        ReturnToMenuAction returnAction = CreateGameObject("ReturnToMenu").AddComponent<ReturnToMenuAction>();
        string activeScene = SceneManager.GetActiveScene().name;

        LogAssert.Expect(LogType.Warning, new Regex("Return to Menu is unavailable"));
        returnAction.RequestReturnToMenu();

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(activeScene));
    }

    [Test]
    public void SelectPage_ActivatesExactlyTheRequestedPageRoot()
    {
        controller.Open();

        controller.SelectPage("second");

        Assert.That(firstPage.ContentRoot.activeSelf, Is.False);
        Assert.That(secondPage.ContentRoot.activeSelf, Is.True);
        Assert.That(dirtyPage.ContentRoot.activeSelf, Is.False);
    }

    [Test]
    public void Open_WithoutAnEventSystem_LogsWarningAndDoesNotThrow()
    {
        EventSystem[] existingEventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool[] eventSystemWasActive = new bool[existingEventSystems.Length];
        for (int index = 0; index < existingEventSystems.Length; index++)
        {
            eventSystemWasActive[index] = existingEventSystems[index].gameObject.activeSelf;
            existingEventSystems[index].gameObject.SetActive(false);
        }

        try
        {
            Object.DestroyImmediate(controller.gameObject);
            Assert.That(EventSystem.current, Is.Null);
            LogAssert.Expect(LogType.Warning, new Regex("EventSystem"));
            controller = CreateController(new[] { firstPage });

            Assert.DoesNotThrow(() => controller.Open());
            Assert.That(overlayRoot.activeSelf, Is.True);
        }
        finally
        {
            for (int index = 0; index < existingEventSystems.Length; index++)
            {
                if (existingEventSystems[index] != null)
                {
                    existingEventSystems[index].gameObject.SetActive(eventSystemWasActive[index]);
                }
            }
        }
    }

    [Test]
    public void ResolvePendingClose_Apply_ClosesAndRestoresCursor()
    {
        dirtyPage.SetDirty(true);
        controller.Open();
        controller.SelectPage("dirty");
        controller.Close();

        controller.ResolvePendingClose(DirtyPageChoice.Apply);

        Assert.That(dirtyPage.ApplyCalls, Is.EqualTo(1));
        Assert.That(overlayRoot.activeSelf, Is.False);
        Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
        Assert.That(Cursor.visible, Is.False);
    }

    [Test]
    public void ResolvePendingClose_Discard_DiscardsAndCloses()
    {
        dirtyPage.SetDirty(true);
        controller.Open();
        controller.SelectPage("dirty");
        controller.Close();

        controller.ResolvePendingClose(DirtyPageChoice.Discard);

        Assert.That(dirtyPage.DiscardCalls, Is.EqualTo(1));
        Assert.That(overlayRoot.activeSelf, Is.False);
    }

    [Test]
    public void ResolvePendingClose_Cancel_KeepsMenuAndDirtyPageOpen()
    {
        dirtyPage.SetDirty(true);
        controller.Open();
        controller.SelectPage("dirty");
        controller.Close();

        controller.ResolvePendingClose(DirtyPageChoice.Cancel);

        Assert.That(overlayRoot.activeSelf, Is.True);
        Assert.That(confirmationRoot.activeSelf, Is.False);
        Assert.That(dirtyPage.ContentRoot.activeSelf, Is.True);
    }

    [Test]
    public void ResolvePendingClose_Cancel_RestoresThePreviousPageSelectionAndInteractivity()
    {
        Button initialButton = CreateButton("Initial", dirtyPage.ContentRoot.transform);
        dirtyPage.SetInitialSelectable(initialButton);
        Button applyButton = CreateButton("Apply", confirmationRoot.transform);
        SetPrivateField(controller, "applyChangesButton", applyButton);
        dirtyPage.SetDirty(true);
        controller.Open();
        controller.SelectPage("dirty");
        controller.Close();

        controller.ResolvePendingClose(DirtyPageChoice.Cancel);

        Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(initialButton.gameObject));
        Assert.That(initialButton.interactable, Is.True);
    }

    [Test]
    public void Close_WhenConfirmationIsOpen_CancelsWithoutOverwritingTheSavedSelection()
    {
        Button initialButton = CreateButton("Initial", dirtyPage.ContentRoot.transform);
        dirtyPage.SetInitialSelectable(initialButton);
        Button applyButton = CreateButton("Apply", confirmationRoot.transform);
        SetPrivateField(controller, "applyChangesButton", applyButton);
        dirtyPage.SetDirty(true);
        controller.Open();
        controller.SelectPage("dirty");
        controller.Close();

        controller.Close();

        Assert.That(confirmationRoot.activeSelf, Is.False);
        Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(initialButton.gameObject));
        Assert.That(initialButton.interactable, Is.True);
    }

    [Test]
    public void Open_ConfiguresPageControlsForAutomaticKeyboardNavigation()
    {
        Button initialButton = CreateButton("Initial", firstPage.ContentRoot.transform);
        initialButton.navigation = new Navigation { mode = Navigation.Mode.None };
        firstPage.SetInitialSelectable(initialButton);

        controller.Open();

        Assert.That(initialButton.navigation.mode, Is.EqualTo(Navigation.Mode.Automatic));
    }

    [Test]
    public void SelectPage_ResetsTheSharedPageScrollToTop()
    {
        ScrollRect scrollRect = CreateScrollRect(overlayRoot.transform);
        scrollRect.verticalNormalizedPosition = 0.25f;

        controller.Open();
        controller.SelectPage("second");

        Assert.That(scrollRect.verticalNormalizedPosition, Is.EqualTo(1f));
    }

    private PauseMenuController CreateController(IEnumerable<TestInFlightMenuPage> pages)
    {
        GameObject controllerRoot = CreateGameObject("Controller");
        controllerRoot.SetActive(false);
        PauseMenuController result = controllerRoot.AddComponent<PauseMenuController>();
        SetPrivateField(result, "overlayRoot", overlayRoot);
        SetPrivateField(result, "confirmationRoot", confirmationRoot);
        SetPrivateField(result, "pages", CreateRegistrations(pages));
        controllerRoot.SetActive(true);
        return result;
    }

    private List<PauseMenuPageRegistration> CreateRegistrations(IEnumerable<TestInFlightMenuPage> pages)
    {
        List<PauseMenuPageRegistration> registrations = new List<PauseMenuPageRegistration>();
        foreach (TestInFlightMenuPage page in pages)
        {
            PauseMenuPageRegistration registration = new PauseMenuPageRegistration();
            SetPrivateField(registration, "page", page);
            registrations.Add(registration);
        }

        return registrations;
    }

    private TestInFlightMenuPage CreatePage(string pageId)
    {
        GameObject contentRoot = CreateGameObject(pageId + "Content");
        TestInFlightMenuPage page = CreateGameObject(pageId + "Page").AddComponent<TestInFlightMenuPage>();
        page.Configure(pageId, contentRoot);
        return page;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private Button CreateButton(string name, Transform parent)
    {
        GameObject buttonObject = CreateUiGameObject(name);
        buttonObject.transform.SetParent(parent);
        buttonObject.AddComponent<Image>();
        return buttonObject.AddComponent<Button>();
    }

    private ScrollRect CreateScrollRect(Transform parent)
    {
        GameObject scrollObject = CreateUiGameObject("PageScroll");
        scrollObject.transform.SetParent(parent);
        scrollObject.AddComponent<Image>();
        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        GameObject viewport = CreateUiGameObject("Viewport");
        viewport.transform.SetParent(scrollObject.transform);
        viewport.AddComponent<Image>();
        GameObject content = CreateUiGameObject("Content");
        content.transform.SetParent(viewport.transform);
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = content.GetComponent<RectTransform>();
        return scrollRect;
    }

    private GameObject CreateUiGameObject(string name)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private sealed class TestInFlightMenuPage : InFlightMenuPage
    {
        private bool isDirty;

        public int ApplyCalls { get; private set; }
        public int DiscardCalls { get; private set; }
        public override bool IsDirty => isDirty;

        public void Configure(string pageId, GameObject contentRoot)
        {
            SetPageId(pageId);
            SetContentRoot(contentRoot);
        }

        public void SetPageId(string pageId)
        {
            typeof(InFlightMenuPage).GetField("pageId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, pageId);
        }

        public void SetContentRoot(GameObject contentRoot)
        {
            typeof(InFlightMenuPage).GetField("contentRoot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, contentRoot);
        }

        public void SetInitialSelectable(Selectable selectable)
        {
            typeof(InFlightMenuPage).GetField("initialSelectable", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, selectable);
        }

        public void SetDirty(bool value)
        {
            isDirty = value;
        }

        public override bool TryApplyChanges()
        {
            ApplyCalls++;
            return true;
        }

        public override void DiscardChanges()
        {
            DiscardCalls++;
            isDirty = false;
        }
    }
}
