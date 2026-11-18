using System.Collections.Generic;
using HeliSim.FrontEnd;
using HeliSim.UI.Theme;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MenuStructureRuntimeTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        }

        created.Clear();
    }

    [Test]
    public void ActionBar_ShowsUnsavedStateFromThePage()
    {
        var page = Create("Page").AddComponent<FakePage>();
        TMP_Text label = Create("SaveState").AddComponent<TextMeshProUGUI>();
        Image dot = Create("StateDot").AddComponent<Image>();
        CanvasGroup apply = Create("ApplyButton").AddComponent<CanvasGroup>();
        var bar = Create("ActionBar").AddComponent<MenuActionBar>();
        bar.Configure(null, page, label, dot, apply);

        Assert.That(label.text, Is.EqualTo(MenuActionBar.CleanText));
        Assert.That(apply.interactable, Is.False, "Apply is disabled while nothing has changed");
        Assert.That(dot.color, Is.EqualTo(MenuPalette.Default.ok));

        page.Dirty = true;
        bar.Refresh();
        Assert.That(label.text, Is.EqualTo(MenuActionBar.DirtyText));
        Assert.That(apply.interactable, Is.True);
        Assert.That(dot.color, Is.EqualTo(MenuPalette.Default.accent));
    }

    [Test]
    public void ActionBar_ToleratesMissingPageAndGraphics()
    {
        var bar = Create("ActionBar").AddComponent<MenuActionBar>();
        Assert.DoesNotThrow(() => bar.Configure(null, null, null, null));
    }

    [Test]
    public void Disclosure_ShowsContentOnlyWhileOpenAndTurnsChevron()
    {
        GameObject root = Create("Disclosure");
        Toggle toggle = root.AddComponent<Toggle>();
        GameObject list = Create("List");
        RectTransform chevron = Create("Icon").AddComponent<RectTransform>();
        var disclosure = root.AddComponent<MenuDisclosure>();
        toggle.SetIsOnWithoutNotify(false);
        disclosure.Configure(toggle, list, chevron);

        Assert.That(list.activeSelf, Is.False);

        toggle.SetIsOnWithoutNotify(true);
        disclosure.Refresh();
        Assert.That(list.activeSelf, Is.True);
        Assert.That(chevron.localEulerAngles.z, Is.EqualTo(270f).Within(0.01f));

        toggle.SetIsOnWithoutNotify(false);
        disclosure.Refresh();
        Assert.That(list.activeSelf, Is.False);
    }

    [Test]
    public void PageAttachment_FollowsThePageActiveState()
    {
        GameObject page = Create("Page");
        GameObject bar = Create("ActionBar");
        var attachment = page.AddComponent<MenuPageAttachment>();
        attachment.Configure(bar);
        Assert.That(bar.activeSelf, Is.True);
        // Edit Mode does not send OnEnable/OnDisable to runtime components; send them as the player loop would.
        page.SetActive(false);
        Invoke(attachment, "OnDisable");
        Assert.That(bar.activeSelf, Is.False);
        page.SetActive(true);
        Invoke(attachment, "OnEnable");
        Assert.That(bar.activeSelf, Is.True);
    }

    [Test]
    public void Breadcrumb_LinksTheParentOnlyOnSubPages()
    {
        Button parent = Button("Parent");
        TMP_Text parentLabel = Create("ParentLabel").AddComponent<TextMeshProUGUI>();
        TMP_Text current = Create("Current").AddComponent<TextMeshProUGUI>();
        var crumb = Create("Crumbs").AddComponent<MenuBreadcrumb>();
        crumb.Configure(null, "Main menu", parent, parentLabel, current);

        crumb.Show("Fly");
        Assert.That(parentLabel.text, Is.EqualTo("Main menu"));
        Assert.That(parent.interactable, Is.False);
        Assert.That(current.text, Is.EqualTo("Fly"));

        crumb.Show("Display & graphics", "Settings");
        Assert.That(parentLabel.text, Is.EqualTo("Settings"));
        Assert.That(parent.interactable, Is.True);
    }

    [Test]
    public void MainMenuRouter_MarksTheCurrentRailItemAndKeepsSettingsCurrentOnSubPages()
    {
        GameObject root = Create("Menu");
        var router = root.AddComponent<MainMenuPageRouter>();
        EventSystem events = Create("EventSystem").AddComponent<EventSystem>();
        MainMenuPageEntry fly = Entry("fly", root.transform, true);
        MainMenuPageEntry settings = Entry("settings", root.transform, true);
        MainMenuPageEntry display = Entry("display", root.transform, false);
        display.settingsPage = display.root.AddComponent<FakePage>();
        GameObject modal = Create("Confirm");
        router.Configure(new[] { fly, settings, display }, modal, Button("Apply"), Button("Discard"), Button("Cancel"), events);

        router.SelectPage("fly");
        Assert.That(Style(fly).IsCurrent, Is.True);
        Assert.That(Style(settings).IsCurrent, Is.False);

        router.SelectPage("display");
        Assert.That(Style(fly).IsCurrent, Is.False);
        Assert.That(Style(settings).IsCurrent, Is.True, "Settings stays current on its sub-pages");
    }

    private MainMenuPageEntry Entry(string id, Transform parent, bool navItem)
    {
        GameObject page = Create(id + "Page");
        page.transform.SetParent(parent, false);
        Button nav = Button(id + "Navigation");
        nav.gameObject.AddComponent<MenuSelectableStyle>().Configure(null,
            navItem ? MenuSelectableVariant.NavItem : MenuSelectableVariant.Row, nav, null, null, null);
        return new MainMenuPageEntry { id = id, root = page, navigationButton = nav, initialSelectable = Button(id + "Initial") };
    }

    private static void Invoke(Object target, string method)
    {
        target.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(target, null);
    }

    private static MenuSelectableStyle Style(MainMenuPageEntry entry)
    {
        return entry.navigationButton.GetComponent<MenuSelectableStyle>();
    }

    private Button Button(string name)
    {
        return Create(name).AddComponent<Button>();
    }

    private GameObject Create(string name)
    {
        var go = new GameObject(name);
        created.Add(go);
        return go;
    }

    private sealed class FakePage : InFlightMenuPage
    {
        public bool Dirty;
        public override bool IsDirty => Dirty;
        public override bool TryApplyChanges() { Dirty = false; return true; }
        public override void DiscardChanges() { Dirty = false; }
    }
}
