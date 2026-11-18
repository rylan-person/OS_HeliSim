using System.Collections.Generic;
using System.Linq;
using HeliSim.UI.Theme;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Step 3 structure of the menu redesign (plan 2026-11-17) in both menu prefabs.</summary>
public sealed class MenuStructureAssetTests
{
    private const string InFlightMenuPath = "Assets/Level/Prefabs/UI/Menu.prefab";
    private const string MainMenuPath = "Assets/Level/Prefabs/UI/MainMenu.prefab";
    private static readonly string[] PrefabPaths = { InFlightMenuPath, MainMenuPath };
    private static readonly string[] SettingsPages = { "DisplayPage", "SettingsPage", "CesiumPage" };

    [Test]
    public void SettingsPages_PinAnActionBarOutsideTheScrollAndWireItToThePage([ValueSource(nameof(PrefabPaths))] string path)
    {
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        ScrollRect scroll = menu.GetComponentsInChildren<ScrollRect>(true).First(s => s.name == "PageViewport");
        foreach (string pageName in SettingsPages)
        {
            InFlightMenuPage page = menu.GetComponentsInChildren<InFlightMenuPage>(true).FirstOrDefault(p => p.name == pageName);
            Assert.That(page, Is.Not.Null, pageName);
            MenuPageAttachment attachment = page.GetComponent<MenuPageAttachment>();
            Assert.That(attachment, Is.Not.Null, pageName + " shows its action bar through MenuPageAttachment");
            Transform bar = attachment.Attached.Single().transform;
            Assert.That(bar.IsChildOf(scroll.transform), Is.False, pageName + ": the action bar is pinned, not scrolled");
            MenuActionBar actionBar = bar.GetComponent<MenuActionBar>();
            Assert.That(actionBar, Is.Not.Null, pageName);
            Assert.That(actionBar.Page, Is.SameAs(page), pageName);
            Transform buttons = bar.Find("Buttons");
            Assert.That(buttons.Find("ApplyButton"), Is.Not.Null, pageName);
            Assert.That(buttons.Find("RevertButton"), Is.Not.Null, pageName);
            Assert.That(bar.Find("State/SaveState").GetComponent<TMP_Text>(), Is.Not.Null, pageName);
            Assert.That(buttons.Find("ApplyButton").GetSiblingIndex(), Is.GreaterThan(buttons.Find("RevertButton").GetSiblingIndex()),
                pageName + ": Apply is the right-most action");
            Assert.That(buttons.Find("ApplyButton").GetComponent<CanvasGroup>(), Is.Not.Null, pageName + ": Apply is disabled while clean");
            Assert.That(buttons.Find("ApplyButton").GetComponent<Button>().onClick.GetPersistentEventCount() +
                        CountRuntimeOwners(page, buttons.Find("ApplyButton").GetComponent<Button>()), Is.GreaterThan(0),
                pageName + ": Apply keeps its owner");
        }
    }

    [Test]
    public void UnavailableNotes_AreCollapsedBehindOneDisclosurePerPage([ValueSource(nameof(PrefabPaths))] string path)
    {
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        foreach (InFlightMenuPage page in menu.GetComponentsInChildren<InFlightMenuPage>(true))
        {
            string[] loose = page.transform.Cast<Transform>()
                .Where(t => t.name.EndsWith("Unavailable") && t.GetComponent<TMP_Text>() != null)
                .Select(t => t.name).ToArray();
            Assert.That(loose, Is.Empty, page.name + " still shows not-available notes inline");
        }

        foreach (string pageName in new[] { "SettingsPage", "DisplayPage" })
        {
            Transform page = menu.GetComponentsInChildren<InFlightMenuPage>(true).First(p => p.name == pageName).transform;
            Transform disclosure = Find(page, "UnavailableDisclosure");
            Transform list = Find(page, "UnavailableList");
            Assert.That(disclosure, Is.Not.Null, pageName);
            Assert.That(list, Is.Not.Null, pageName);
            Assert.That(list.gameObject.activeSelf, Is.False, pageName + " list starts collapsed");
            Assert.That(list.childCount, Is.GreaterThan(0), pageName);
            MenuDisclosure component = disclosure.GetComponent<MenuDisclosure>();
            Assert.That(component.Content, Is.SameAs(list.gameObject), pageName);
            Assert.That(component.Toggle.isOn, Is.False, pageName);
        }
    }

    [Test]
    public void MainMenuSettingsHub_ShowsFourTilesWithIconAndDescription()
    {
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPath);
        Transform tiles = Find(menu.transform, "CategoryTiles");
        Assert.That(tiles, Is.Not.Null);
        Button[] buttons = tiles.GetComponentsInChildren<Button>(true);
        Assert.That(buttons.Select(b => b.name), Is.EquivalentTo(new[] { "displaySettings", "audioSettings", "controlsSettings", "streamingSettings" }));
        foreach (Button button in buttons)
        {
            Assert.That(Find(button.transform, "Icon")?.GetComponent<Image>()?.sprite, Is.Not.Null, button.name);
            Assert.That(button.transform.Find("Description")?.GetComponent<TMP_Text>()?.text, Is.Not.Empty, button.name);
            // The router adds these listeners at runtime from its page entries.
            Assert.That(RouterEntryFor(menu, button), Is.True, button.name + " is still a router entry button");
        }
    }

    [Test]
    public void RailItems_HaveIcons([ValueSource(nameof(PrefabPaths))] string path)
    {
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var failures = new List<string>();
        int items = 0;
        foreach (MenuSelectableStyle style in menu.GetComponentsInChildren<MenuSelectableStyle>(true))
        {
            bool rail = style.Variant == MenuSelectableVariant.NavItem || style.name == "ResumeButton";
            if (!rail || style.GetComponent<Button>() == null)
            {
                continue;
            }

            items++;
            Transform icon = style.transform.Find("Icon");
            if (icon == null || icon.GetComponent<Image>().sprite == null)
            {
                failures.Add(style.name + " has no icon");
            }
            else if (!style.Icons.Contains(icon.GetComponent<Image>()))
            {
                failures.Add(style.name + " icon is not tinted by its style");
            }
        }

        Assert.That(items, Is.GreaterThanOrEqualTo(7), "rail items");
        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }

    [Test]
    public void BothMenus_UseTheReferenceShell([ValueSource(nameof(PrefabPaths))] string path)
    {
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(Find(menu.transform, "BrandMark")?.GetComponent<TMP_Text>()?.text, Does.Contain("SIM"), "HELISIM brand block");
        MenuBreadcrumb crumbs = menu.GetComponentInChildren<MenuBreadcrumb>(true);
        Assert.That(crumbs, Is.Not.Null, "top bar breadcrumb");
        foreach (InFlightMenuPage page in menu.GetComponentsInChildren<InFlightMenuPage>(true))
        {
            LayoutElement column = page.GetComponent<LayoutElement>();
            Assert.That(column, Is.Not.Null, page.name);
            Assert.That(column.preferredWidth, Is.EqualTo(1296f), page.name + " uses the reference page column");
            Assert.That(Find(page.transform, "PageTitle")?.GetComponent<TMP_Text>(), Is.Not.Null, page.name + " has a page title");
        }
    }

    [Test]
    public void InFlightMenu_IsAFramedPanelOverTheWorld()
    {
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(InFlightMenuPath);
        RectTransform frame = (RectTransform)menu.transform.Find("Overlay/Frame");
        Assert.That(frame, Is.Not.Null);
        Assert.That(frame.offsetMin, Is.EqualTo(new Vector2(60f, 60f)), "the world stays visible around the menu");
        Assert.That(frame.offsetMax, Is.EqualTo(new Vector2(-60f, -60f)));
        Assert.That(frame.GetComponent<Mask>(), Is.Not.Null, "rounded frame clips the rail and content");
        var controller = menu.GetComponentInChildren<PauseMenuController>(true);
        var serialized = new SerializedObject(controller);
        Assert.That(serialized.FindProperty("breadcrumb").objectReferenceValue, Is.Not.Null);
        Assert.That(serialized.FindProperty("pageScrollRect").objectReferenceValue, Is.Not.Null);
    }

    [Test]
    public void NovaPopup_IsACautionCardWithItsOriginalHandler()
    {
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(InFlightMenuPath);
        Transform popup = menu.transform.Find("UnlockCollectivePopup");
        Assert.That(popup.Find("Card/Eyebrow").GetComponent<TMP_Text>().text, Is.EqualTo("Collective locked"));
        Button button = popup.Find("Card/Button").GetComponent<Button>();
        Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
        Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo("UnlockCollective"));
        Assert.That(button.GetComponent<MenuSelectableStyle>().Variant, Is.EqualTo(MenuSelectableVariant.Primary));
    }

    [Test]
    public void TimeTrial_OrdersMinusPlayPausePlusWithOriginalHandlers()
    {
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(InFlightMenuPath);
        Transform section = Find(menu.transform, "Time Trial");
        Transform controls = Find(section, "TimeControls");
        Assert.That(controls, Is.Not.Null);
        Assert.That(controls.Cast<Transform>().Where(t => t.GetComponent<Button>() != null).Select(t => t.name),
            Is.EqualTo(new[] { "Remove30sButton", "Play Button", "Pause Button", "Add30sButton" }));
        Assert.That(Handler(controls, "Remove30sButton"), Is.EqualTo("Remove30Seconds"));
        Assert.That(Handler(controls, "Add30sButton"), Is.EqualTo("Add30Seconds"));
        Assert.That(Handler(controls, "Play Button"), Is.EqualTo("ResumeTime"));
        Assert.That(Handler(controls, "Pause Button"), Is.EqualTo("PauseTime"));
        Assert.That(Find(section, "Play/Pause"), Is.Null, "the emptied row is removed");
    }

    private static bool RouterEntryFor(GameObject menu, Button button)
    {
        var router = new SerializedObject(menu.GetComponent<HeliSim.FrontEnd.MainMenuPageRouter>());
        SerializedProperty pages = router.FindProperty("pages");
        for (int i = 0; i < pages.arraySize; i++)
        {
            if (pages.GetArrayElementAtIndex(i).FindPropertyRelative("navigationButton").objectReferenceValue == button)
            {
                return true;
            }
        }

        return false;
    }

    private static string Handler(Transform parent, string name)
    {
        Button button = parent.Find(name).GetComponent<Button>();
        Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1), name);
        return button.onClick.GetPersistentMethodName(0);
    }

    // Settings presenters add Apply listeners at runtime from a serialized reference.
    private static int CountRuntimeOwners(InFlightMenuPage page, Button button)
    {
        var serialized = new SerializedObject(page);
        SerializedProperty property = serialized.GetIterator();
        int count = 0;
        while (property.NextVisible(true))
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == button)
            {
                count++;
            }
        }

        return count;
    }

    private static Transform Find(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = Find(child, name);
            if (found != null) return found;
        }

        return null;
    }

    [Test]
    public void MainMenuTopLevelPages_FitA1080pViewportWithoutScrolling()
    {
        string[] topLevel = { "FlyPage", "MultiplayerPage", "SettingsPage", "HardwarePage", "RecordingsPage", "HelpPage" };
        GameObject root = PrefabUtility.LoadPrefabContents(MainMenuPath);
        var target = new RenderTexture(1920, 1080, 24);
        try
        {
            var cameraObject = new GameObject("FitCamera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, root.scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = root.scene;
            camera.targetTexture = target;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;

            Transform content = root.transform.Find("MenuContent/Main/PageViewport/Viewport/PageContent");
            var viewport = (RectTransform)root.transform.Find("MenuContent/Main/PageViewport/Viewport");
            foreach (Transform bar in root.transform.Find("MenuContent/Main/ActionBars"))
            {
                bar.gameObject.SetActive(false);
            }

            Transform[] pages = content.Cast<Transform>().ToArray();
            Transform[] checkedPages = pages.Where(p => topLevel.Contains(p.name) && p.GetComponent<InFlightMenuPage>() == null).ToArray();
            Assert.That(checkedPages.Select(p => p.name), Is.EquivalentTo(topLevel));
            var failures = new List<string>();
            foreach (Transform page in checkedPages)
            {
                foreach (Transform other in pages)
                {
                    other.gameObject.SetActive(other == page);
                }

                for (int pass = 0; pass < 3; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
                }

                float height = ((RectTransform)page).rect.height;
                if (height > viewport.rect.height)
                {
                    failures.Add(page.name + " is " + height.ToString("F0") + " tall in a " + viewport.rect.height.ToString("F0") + " viewport");
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            target.Release();
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void MainMenuHeader_ShowsReadOnlyStatusChipsWiredToTheScreen()
    {
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPath);
        Transform chips = Find(menu.transform, "StatusChips");
        Assert.That(chips, Is.Not.Null);
        Assert.That(chips.Cast<Transform>().Select(t => t.name), Is.EqualTo(new[] { "InputChip", "HeadsetChip", "NovaChip" }));
        Assert.That(chips.GetComponentsInChildren<Selectable>(true), Is.Empty, "status chips are read-only");
        Assert.That(chips.Find("NovaChip/Label").GetComponent<TMP_Text>().text, Is.EqualTo(HeliSim.FrontEnd.MenuHardwareStatus.NovaChipLabel));
        var screen = new SerializedObject(menu.GetComponent<HeliSim.FrontEnd.MainMenuScreen>());
        foreach (string field in new[] { "inputChip", "inputChipDot", "headsetChip", "headsetChipDot", "quickStartSummary" })
        {
            Assert.That(screen.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        }

        Assert.That(((Component)screen.FindProperty("quickStartSummary").objectReferenceValue).name, Is.EqualTo("Specs"));
    }

    [Test]
    public void MainMenu_HasAStaticTopographicBackdropBehindTheShell()
    {
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPath);
        Transform backdrop = menu.transform.Find("MenuContent/Backdrop");
        Assert.That(backdrop, Is.Not.Null);
        Assert.That(backdrop.GetSiblingIndex(), Is.Zero, "drawn first, behind rail and content");
        Image image = backdrop.GetComponent<Image>();
        Assert.That(image.sprite, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(image.sprite), Is.EqualTo(MenuThemeAssets.BackdropPath));
        Assert.That(image.raycastTarget, Is.False);
        Assert.That(backdrop.GetComponent<AspectRatioFitter>().aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
    }

    [Test]
    public void ReturnToMenu_AsksForConfirmationWithStayInFlightAsTheSafeChoice()
    {
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(InFlightMenuPath);
        Transform dialog = menu.transform.Find("Overlay/ReturnConfirmationDialog");
        Assert.That(dialog, Is.Not.Null);
        Assert.That(dialog.gameObject.activeSelf, Is.False);
        var controller = new SerializedObject(menu.GetComponentInChildren<PauseMenuController>(true));
        Assert.That(controller.FindProperty("actionConfirmationRoot").objectReferenceValue, Is.SameAs(dialog.gameObject));
        var confirm = (Button)controller.FindProperty("confirmActionButton").objectReferenceValue;
        var stay = (Button)controller.FindProperty("cancelActionButton").objectReferenceValue;
        Assert.That(confirm.transform.IsChildOf(dialog), Is.True);
        Assert.That(stay.transform.IsChildOf(dialog), Is.True);
        Assert.That(confirm.GetComponent<MenuSelectableStyle>().Variant, Is.EqualTo(MenuSelectableVariant.Destructive));
        Assert.That(stay.GetComponent<MenuSelectableStyle>().Variant, Is.EqualTo(MenuSelectableVariant.Ghost));
        Assert.That(confirm.transform.GetSiblingIndex(), Is.GreaterThan(stay.transform.GetSiblingIndex()), "confirm is right-most");

        Button rail = menu.transform.Find("Overlay/Frame/NavigationRail/RailFoot/ReturnToMenuButton").GetComponent<Button>();
        Assert.That(rail.onClick.GetPersistentEventCount(), Is.EqualTo(1));
        Assert.That(rail.onClick.GetPersistentMethodName(0), Is.EqualTo("RequestReturnToMenu"));
    }

    [Test]
    public void BindingRows_HaveAChangedBadgeAndThePageHasAChangeSummary([ValueSource(nameof(PrefabPaths))] string path)
    {
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        ControlsRebindingPage page = menu.GetComponentInChildren<ControlsRebindingPage>(true);
        Assert.That(page, Is.Not.Null);
        var pageData = new SerializedObject(page);
        Assert.That(pageData.FindProperty("changeSummaryLabel").objectReferenceValue, Is.Not.Null);
        var template = (ControlsBindingRow)pageData.FindProperty("bindingRowPrefab").objectReferenceValue;
        var badge = (GameObject)new SerializedObject(template).FindProperty("changedBadge").objectReferenceValue;
        Assert.That(badge, Is.Not.Null);
        Assert.That(badge.transform.IsChildOf(template.transform), Is.True);
        Assert.That(badge.activeSelf, Is.False, "hidden until a binding differs from its default");
        Assert.That(badge.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("Changed"));
    }
}
