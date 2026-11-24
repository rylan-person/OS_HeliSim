using System;
using System.Collections.Generic;
using System.Linq;
using HeliSim.FrontEnd;
using HeliSim.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Shell = MenuViewRebuilder.MenuShell;

// Builds serialized menu assets through Unity APIs. Never rebuilds gameplay scenes.
// The main menu follows docs/design/menu-redesign-preview.html (plan 2026-11-17) and is built
// with MenuKit; its settings pages are cloned from the rebuilt in-flight menu.
public static class MenuSceneBuilder
{
    public const string PrefabPath = "Assets/Level/Prefabs/UI/MainMenu.prefab";
    private const string InFlightMenuPath = "Assets/Level/Prefabs/UI/Menu.prefab";

    [MenuItem("HeliSim/Build Main Menu Assets")]
    public static void Build()
    {
        RequireIdle();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Preserve unsaved scenes before building menu assets.");
        BuildMainMenuPrefab();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        try
        {
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            var camera = new GameObject("MenuCamera", typeof(Camera), typeof(AudioListener));
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor = MenuPalette.Default.bg0;
            camera.GetComponent<Camera>().cullingMask = 0;
            EditorSceneManager.SaveScene(scene, MenuSessionCoordinator.MenuScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != MenuSessionCoordinator.MenuScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(MenuSessionCoordinator.MenuScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    /// <summary>Rebuilds MainMenu.prefab only (the menu scene keeps its prefab instance).</summary>
    public static void BuildMainMenuPrefab()
    {
        RequireIdle();
        MenuThemeAssets.CreateAll();
        var kit = new MenuKit(MenuThemeAssets.LoadTheme());
        // Build in a scratch scene so open authoring scenes are never touched or dirtied.
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scratch);
        try
        {
            var root = MenuKit.Rect("MainMenu", null);
            new MainMenuBuild(kit, root).Run();
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
        }
        finally
        {
            if (previous.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scratch, true);
        }
    }

    [MenuItem("HeliSim/Repair Menu Dropdowns and Entry Focus")]
    public static void RepairUsabilityAssets()
    {
        RequireIdle();
        foreach (string path in new[] { InFlightMenuPath, PrefabPath })
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true)) ConfigureDropdown(dropdown);
                var router = root.GetComponent<MainMenuPageRouter>();
                if (router != null)
                {
                    var serialized = new SerializedObject(router);
                    var entries = serialized.FindProperty("pages");
                    for (int index = 0; index < entries.arraySize; index++)
                    {
                        var entry = entries.GetArrayElementAtIndex(index);
                        if (entry.FindPropertyRelative("settingsPage").objectReferenceValue != null)
                            entry.FindPropertyRelative("initialSelectable").objectReferenceValue = null;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    /// <summary>Adds only the optional token section, preserving the authored menu and existing references.</summary>
    [MenuItem("HeliSim/Add Main Menu Streaming Token Entry")]
    public static void AddMapServiceTokenEntry()
    {
        RequireIdle();
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Close Prefab Mode before updating the main menu asset.");
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (asset == null || EditorUtility.IsDirty(asset))
            throw new InvalidOperationException("Preserve unsaved main menu asset changes before adding token entry.");
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var page = root.GetComponentInChildren<CesiumSettingsPage>(true);
            if (page == null) throw new InvalidOperationException("Main menu World streaming page is missing.");
            AddTokenSection(new MenuKit(MenuThemeAssets.LoadTheme()), page.transform);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void AddTokenSection(MenuKit kit, Transform page)
    {
        var palette = MenuPalette.Default;
        const string help = "Save your own access tokens on this Windows account. HeliSim loads them when it starts. " +
            "Leave a field blank to keep its saved token.";
        var existing = page.Find("MapServiceTokens");
        if (existing != null)
        {
            existing.Find("Help").GetComponent<TMP_Text>().text = help;
            var existingEntry = existing.GetComponent<MapServiceTokenEntry>();
            var applyButton = existing.GetComponentsInChildren<Button>(true).Single(b => b.name == "ApplyTokens");
            applyButton.GetComponentInChildren<TMP_Text>(true).text = "Save tokens";
            AddTokenActions(kit, existing, existingEntry, applyButton);
            return;
        }
        var section = kit.Section(page, "MapServiceTokens", "Map service tokens (optional)");
        section.SetSiblingIndex(1);
        kit.Text(section, "Help", help, MenuTextRole.Hint, palette.text2);
        var rows = kit.Rows(section);
        var cesium = TokenField(kit, rows, "CesiumToken", "Cesium ion token", "3D Tiles and world streaming.");
        var mapbox = TokenField(kit, rows, "MapboxToken", "Mapbox token", "Map tiles and location search.");
        var links = MenuKit.Rect("AccountLinks", section);
        MenuKit.RowLayout(links.gameObject, 0, 0, 0, 0, 18f);
        var cesiumSignup = kit.Button(links, "CesiumSignup", "Create Cesium account", MenuButtonLook.Default);
        var mapboxSignup = kit.Button(links, "MapboxSignup", "Create Mapbox account", MenuButtonLook.Default);
        var apply = kit.Button(section, "ApplyTokens", "Save tokens", MenuButtonLook.Default);
        var status = kit.Text(section, "Status", string.Empty, MenuTextRole.Hint, palette.text2);
        var entry = section.gameObject.AddComponent<MapServiceTokenEntry>();
        entry.Configure(cesium, mapbox, apply, cesiumSignup, mapboxSignup, status, null);
        AddTokenActions(kit, section, entry, apply);
        foreach (var child in section.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = page.gameObject.layer;
    }

    private static void AddTokenActions(MenuKit kit, Transform section, MapServiceTokenEntry entry, Button save)
    {
        if (section.Find("TokenActions") != null) return;
        var row = MenuKit.Rect("TokenActions", section);
        row.SetSiblingIndex(save.transform.GetSiblingIndex());
        MenuKit.RowLayout(row.gameObject, 0, 0, 0, 0, 18f);
        save.transform.SetParent(row, false);
        var forget = kit.Button(row, "ForgetTokens", "Forget saved tokens", MenuButtonLook.Default);
        Set(entry, "forgetButton", forget);
        foreach (var child in row.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = section.gameObject.layer;
    }

    private static TMP_InputField TokenField(MenuKit kit, Transform rows, string name, string label, string hint)
    {
        kit.Row(rows, name + "Row", label, hint, out RectTransform control, 480f);
        var field = MenuKit.Rect(name, control).gameObject.AddComponent<TMP_InputField>();
        kit.Field(field, true, 480f, placeholder: "Optional token");
        field.contentType = TMP_InputField.ContentType.Password;
        field.richText = false;
        return field;
    }

    private static void RequireIdle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("The Editor must be idle to build menu assets.");
    }

    /// <summary>Keeps the 2026-10-20 dropdown repairs: a full-field hit target and a solid, field-wide option list.</summary>
    public static void ConfigureDropdown(TMP_Dropdown dropdown)
    {
        var background = dropdown.GetComponent<Image>();
        if (background == null || dropdown.template == null)
            throw new InvalidOperationException("Menu dropdown requires a field Image and option template: " + dropdown.name);
        background.enabled = true;
        background.raycastTarget = true;
        dropdown.targetGraphic = background;
        var template = dropdown.template;
        template.anchorMin = Vector2.zero;
        template.anchorMax = Vector2.right;
        template.pivot = new Vector2(0.5f, 1f);
        template.sizeDelta = new Vector2(0, 240);
        template.anchoredPosition = Vector2.zero;
        var viewport = template.Find("Viewport");
        if (viewport != null && viewport.TryGetComponent<Image>(out var viewportImage))
        {
            viewportImage.enabled = true;
            viewportImage.color = MenuPalette.Default.bg2;
            viewportImage.sprite = null;
            viewportImage.type = Image.Type.Simple;
            if (viewport.TryGetComponent<Mask>(out var mask)) mask.showMaskGraphic = true;
        }
    }

    private static void Set(Object target, string name, Object value)
    {
        var data = new SerializedObject(target);
        var property = data.FindProperty(name);
        if (property == null) throw new InvalidOperationException(target.GetType().Name + " has no field " + name);
        property.objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddDisplaySwitch(MenuKit kit, Transform foot, MenuVrPresentation presentation)
    {
        var button = kit.NavItem(MenuKit.Rect("SwitchMenuDisplay", foot).gameObject.AddComponent<Button>(),
            "Menu on monitor (F8)", "display");
        button.gameObject.AddComponent<CanvasGroup>();
        button.transform.SetSiblingIndex(foot.Find("Quit").GetSiblingIndex());
        foreach (var child in button.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = foot.gameObject.layer;
        Set(presentation, "switchDisplay", button);
        Set(presentation, "switchDisplayLabel", button.transform.Find("Label").GetComponent<TMP_Text>());
    }

    private sealed class MainMenuBuild
    {
        private readonly MenuKit kit;
        private readonly RectTransform root;
        private MenuPalette P => kit.P;
        private readonly List<MainMenuPageEntry> pages = new List<MainMenuPageEntry>();
        private RectTransform rail, nav, content, actionBars;
        private MainMenuPageRouter router;

        public MainMenuBuild(MenuKit kit, RectTransform root)
        {
            this.kit = kit;
            this.root = root;
        }

        public void Run()
        {
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var screen = root.gameObject.AddComponent<MainMenuScreen>();
            router = root.gameObject.AddComponent<MainMenuPageRouter>();
            var prefs = root.gameObject.AddComponent<PrefSettings>();
            var vrPresentation = root.gameObject.AddComponent<MenuVrPresentation>();
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(root, false);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var body = MenuKit.Rect("MenuContent", root);
            MenuKit.Stretch(body);
            body.gameObject.AddComponent<CanvasGroup>();
            body.gameObject.AddComponent<MainMenuContentMarker>();
            kit.Fill(body.gameObject, P.bg0);
            // Step 4: static topographic backdrop (preview .main-bg), covering the screen at any aspect ratio.
            var backdrop = MenuKit.Rect("Backdrop", body);
            backdrop.anchorMin = backdrop.anchorMax = backdrop.pivot = new Vector2(0.5f, 0.5f);
            var backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.sprite = MenuThemeAssets.EnsureBackdrop();
            backdropImage.raycastTarget = false;
            var envelope = backdrop.gameObject.AddComponent<AspectRatioFitter>();
            envelope.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            envelope.aspectRatio = (float)MenuThemeAssets.BackdropWidth / MenuThemeAssets.BackdropHeight;
            var coords =kit.Text(body, "Coordinates", "37°49′S 144°58′E · MELBOURNE", MenuTextRole.Micro, P.text3,
                TextAlignmentOptions.BottomRight, false, 4f, false);
            coords.font = kit.Theme.MonoFont;
            coords.fontSharedMaterial = kit.Theme.MonoFont.material;
            coords.rectTransform.anchorMin = coords.rectTransform.anchorMax = coords.rectTransform.pivot = new Vector2(1f, 0f);
            coords.rectTransform.sizeDelta = new Vector2(700f, 40f);
            coords.rectTransform.anchoredPosition = new Vector2(-36f, 24f);

            BuildRail(body, out Button quit);
            AddDisplaySwitch(kit, quit.transform.parent, vrPresentation);
            var main = MenuKit.Rect("Main", body);
            MenuKit.Stretch(main, 420f);
            kit.Fill(main.gameObject, MenuPalette.WithAlpha(P.bg2, 0.9f), null, true);
            MenuKit.Column(main.gameObject, 0, 0, 0, 0, 0f);
            var topbar = MenuKit.Rect("Header", main);
            MenuKit.RowLayout(topbar.gameObject, 60, 60, 24, 24, 24f);
            MenuKit.Layout(topbar.gameObject, -1f, 96f, -1f, -1f, 96f);
            Shell.BottomDivider(kit, topbar);
            var crumbParent = MenuKit.Rect("ParentCrumb", null).gameObject.AddComponent<Button>();
            UnityEventTools.AddStringPersistentListener(crumbParent.onClick, router.SelectPageFromButton, "settings");
            MenuBreadcrumb crumbs = Shell.Breadcrumb(kit, topbar, "Main menu", crumbParent);
            MenuKit.Spacer(topbar, 0f, 1f);
            var status = kit.Text(topbar, "Status", string.Empty, MenuTextRole.Hint, P.caution, TextAlignmentOptions.MidlineRight);
            MenuKit.Layout(status.gameObject, -1f, -1f, 0f, 0f);
            // Step 4: read-only status chips. Input and VR are refreshed by MainMenuScreen; NOVA is never probed.
            var chips = MenuKit.Rect("StatusChips", topbar);
            MenuKit.RowLayout(chips.gameObject, 0, 0, 0, 0, 12f);
            var inputChip = kit.Chip(chips, "InputChip", "Keyboard & mouse", P.ok);
            var headsetChip = kit.Chip(chips, "HeadsetChip", "Desktop · no VR", P.text3);
            kit.Chip(chips, "NovaChip", MenuHardwareStatus.NovaChipLabel, P.caution);
            Set(screen, "inputChip", inputChip.Find("Label").GetComponent<TMP_Text>());
            Set(screen, "inputChipDot", inputChip.Find("Dot").GetComponent<Image>());
            Set(screen, "headsetChip", headsetChip.Find("Label").GetComponent<TMP_Text>());
            Set(screen, "headsetChipDot", headsetChip.Find("Dot").GetComponent<Image>());

            var viewport = MenuKit.Rect("Viewport", null);
            content = MenuKit.Rect("PageContent", viewport);
            var pageViewport = MenuKit.Rect("PageViewport", main);
            ScrollRect scroll = Shell.Scroll(kit, pageViewport, viewport, content, main);
            actionBars = MenuKit.Rect("ActionBars", main);
            MenuKit.Column(actionBars.gameObject, 0, 0, 0, 0, 0f);

            var fly = FlyPage(out TMP_Dropdown assistance, out Toggle record, out TMP_Text flightSummary, out Button quick, out Button solo);
            var multiplayer = MultiplayerPage(out Toggle spectator, out TMP_InputField address, out TMP_InputField port, out Button host, out Button join);
            var settings = SettingsHub(out Dictionary<string, Button> categories);
            var hardware = HardwarePage(vrPresentation, out TMP_Text hardwareSummary, screen);
            var recordings = RecordingsPage(out Toggle recordPreference, out TMP_Text recordingSummary, out Button folder);
            HelpPage();

            BindingDialogAndSettings(categories, prefs, scaler);
            CompactPages();

            var changes = MenuKit.Rect("ConfirmChanges", root);
            kit.Dialog(changes.gameObject, "Settings", "Unsaved changes", "Apply your changes, discard them, or keep editing.",
                out _, out RectTransform changeActions);
            var cancel = kit.Button(changeActions, "Cancel", "Keep editing", MenuButtonLook.Ghost);
            var discard = kit.Button(changeActions, "Discard", "Discard", MenuButtonLook.Default);
            var apply = kit.Button(changeActions, "Apply", "Apply", MenuButtonLook.Primary);
            changes.gameObject.SetActive(false);

            var quitDialog = MenuKit.Rect("ConfirmQuit", root);
            kit.Dialog(quitDialog.gameObject, "Main menu", "Quit HeliSim?", "Your applied preferences are saved.", out _, out RectTransform quitActions);
            var no = kit.Button(quitActions, "Cancel", "Cancel", MenuButtonLook.Ghost);
            var yes = kit.Button(quitActions, "Quit", "Quit", MenuButtonLook.Danger);
            quitDialog.gameObject.SetActive(false);

            var loading = MenuKit.Rect("LoadingOverlay", root);
            MenuKit.Stretch(loading);
            loading.gameObject.SetActive(false);

            router.Configure(pages.ToArray(), changes.gameObject, apply, discard, cancel, events.GetComponent<EventSystem>());
            router.SetScrollRect(scroll);
            router.SetBreadcrumb(crumbs);
            Set(screen, "router", router); Set(screen, "address", address); Set(screen, "port", port); Set(screen, "spectatorRole", spectator);
            Set(screen, "assistance", assistance); Set(screen, "recordFlight", record); Set(screen, "recordPreference", recordPreference);
            Set(screen, "flightSummary", flightSummary); Set(screen, "hardwareSummary", hardwareSummary); Set(screen, "recordingSummary", recordingSummary);
            Set(screen, "status", status); Set(screen, "quickStart", quick); Set(screen, "startSolo", solo); Set(screen, "startHost", host);
            Set(screen, "startJoin", join); Set(screen, "recordingsFolder", folder); Set(screen, "quit", quit);
            Set(screen, "quitConfirmation", quitDialog.gameObject); Set(screen, "confirmQuit", yes); Set(screen, "cancelQuit", no);
            Set(screen, "theme", kit.Theme);
            foreach (var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true)) ConfigureDropdown(dropdown);
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = LayerMask.NameToLayer("UI");
        }

        // ------------------------------------------------------------ rail

        private void BuildRail(Transform body, out Button quit)
        {
            rail = MenuKit.Rect("Navigation", body);
            rail.anchorMin = Vector2.zero;
            rail.anchorMax = new Vector2(0f, 1f);
            rail.pivot = new Vector2(0f, 0.5f);
            rail.sizeDelta = new Vector2(420f, 0f);
            kit.Fill(rail.gameObject, MenuPalette.WithAlpha(P.bg1, 0.94f), null, true);
            MenuKit.Column(rail.gameObject, 24, 24, 42, 30, 36f);
            var edge = MenuKit.Rect("Edge", rail);
            edge.anchorMin = new Vector2(1f, 0f);
            edge.anchorMax = Vector2.one;
            edge.pivot = new Vector2(1f, 0.5f);
            edge.sizeDelta = new Vector2(MenuKit.Hairline, 0f);
            MenuKit.Layout(edge.gameObject).ignoreLayout = true;
            kit.Fill(edge.gameObject, P.line);
            Shell.Brand(kit, rail, "Flight simulator");
            nav = MenuKit.Rect("PageNavigation", rail);
            MenuKit.Column(nav.gameObject, 0, 0, 0, 0, 2f);
            var label = kit.Text(nav, "NavLabel", "Menu", MenuTextRole.Micro, P.text3, TextAlignmentOptions.TopLeft, true, 16f, false);
            label.margin = new Vector4(18f, 0f, 18f, 12f);
            MenuKit.Spacer(rail, 0f, -1f, 1f);
            var foot = MenuKit.Rect("RailFoot", rail);
            MenuKit.Column(foot.gameObject, 0, 0, 24, 0, 2f);
            kit.TopDivider(foot);
            quit = kit.NavItem(MenuKit.Rect("Quit", foot).gameObject.AddComponent<Button>(), "Quit", "power");
            var version = kit.Text(foot, "Version", "Build " + Application.version + " · Unity " + Application.unityVersion,
                MenuTextRole.Micro, P.text3, TextAlignmentOptions.TopLeft, false, 0f, false);
            version.font = kit.Theme.MonoFont;
            version.fontSharedMaterial = kit.Theme.MonoFont.material;
            version.margin = new Vector4(18f, 18f, 18f, 0f);
        }

        private MainMenuPageEntry Page(string id, string title, string icon, string objectName, string heading, string description)
        {
            var page = MenuKit.Rect(objectName, content);
            kit.Page(page.gameObject);
            kit.PageHead(page, heading, description);
            var button = kit.NavItem(MenuKit.Rect(id + "Navigation", nav).gameObject.AddComponent<Button>(), title, icon);
            var entry = new MainMenuPageEntry { id = id, root = page.gameObject, navigationButton = button, title = title };
            pages.Add(entry);
            return entry;
        }

        /// <summary>
        /// The main menu has a full-height header and no frame inset, so its pages use tighter vertical rhythm than
        /// the reference page so every top-level page fits a 1080p viewport without scrolling. Settings sub-pages
        /// share the same rhythm so titles don't jump; long sub-pages still scroll.
        /// </summary>
        private void CompactPages()
        {
            foreach (Transform page in content)
            {
                var column = page.GetComponent<VerticalLayoutGroup>();
                if (column == null) continue;
                column.padding.top = MainPagePadding;
                column.padding.bottom = MainPagePadding;
                column.spacing = MainPageSpacing;
            }
        }

        public const int MainPagePadding = 42;
        public const float MainPageSpacing = 36f;

        private RectTransform Rows(Transform page, string name, string heading) => kit.Rows(kit.Section(page, name, heading));

        private Toggle NewToggle(string name) => MenuKit.Rect(name, null).gameObject.AddComponent<Toggle>();

        private TMP_InputField NewField(string name, string value)
        {
            var field = MenuKit.Rect(name, null).gameObject.AddComponent<TMP_InputField>();
            field.text = value;
            return field;
        }

        private RectTransform Launch(Transform page, out TMP_Text summary, string summaryText)
        {
            var launch = MenuKit.Rect("Launch", page);
            MenuKit.RowLayout(launch.gameObject, 0, 0, 0, 0, 24f);
            summary = kit.Text(launch, "Summary", summaryText, MenuTextRole.Hint, P.text2, TextAlignmentOptions.MidlineLeft);
            MenuKit.Layout(summary.gameObject, -1f, -1f, 1f, 0f);
            return launch;
        }

        private static string Strong(string text) => "<color=#" + ColorUtility.ToHtmlStringRGB(MenuPalette.Default.text1) + ">" + text + "</color>";

        // ------------------------------------------------------------ pages

        private MainMenuPageEntry FlyPage(out TMP_Dropdown assistance, out Toggle record, out TMP_Text summary, out Button quick, out Button solo)
        {
            var fly = Page("fly", "Fly", "fly", "FlyPage", "Your next flight", "Set up a local session, or fly again with your last valid setup.");
            var card = MenuKit.Rect("QuickStartCard", fly.root.transform);
            kit.Fill(card.gameObject, MenuPalette.WithAlpha(P.accent, 0.045f), kit.Theme.PanelSprite);
            kit.Border(card, MenuPalette.WithAlpha(P.accent, 0.28f), kit.Theme.PanelBorderSprite);
            MenuKit.RowLayout(card.gameObject, 42, 42, 24, 24, 36f);
            var text = MenuKit.Rect("Text", card);
            MenuKit.Column(text.gameObject, 0, 0, 0, 0, 12f);
            MenuKit.Layout(text.gameObject, -1f, -1f, 1f, 0f);
            kit.Text(text, "Title", "Quick start", MenuTextRole.Title, P.text1);
            var specs = kit.Text(text, "Specs", "Aircraft " + Strong("R66") + "     Location " + Strong("Multiplayer environment") +
                "     Assistance " + Strong("Keep aircraft default") + "     CSV " + Strong("Off"), MenuTextRole.Hint, P.text2);
            Set(root.GetComponent<MainMenuScreen>(), "quickStartSummary", specs);
            quick = kit.Button(card, "QuickStart", "Fly last setup", MenuButtonLook.Default, "play");
            fly.initialSelectable = quick;

            var rows = Rows(fly.root.transform, "NewFlight", "New flight");
            kit.Row(rows, "AircraftRow", "Aircraft", "The only aircraft in this build.", out RectTransform control);
            kit.Text(control, "Value", "R66", MenuTextRole.Value, P.text1, TextAlignmentOptions.MidlineRight, false, 0f, false);
            kit.Row(rows, "LocationRow", "Location", "Shared with multiplayer sessions.", out control);
            kit.Text(control, "Value", "Multiplayer environment", MenuTextRole.Control, P.text1, TextAlignmentOptions.MidlineRight, false, 0f, false)
                .font = kit.Theme.BodyMediumFont;
            kit.Row(rows, "AssistanceRow", "Assistance", "Overrides the aircraft’s assist preset for this flight.", out control);
            assistance = CloneDropdown("Assistance", new[] { "Keep aircraft default", "Manual Torque", "Manual", "Low", "Medium", "High", "Very High" });
            MenuKit.Place(kit.Dropdown(assistance), control, -1f, MenuKit.FieldHeight, 1f, 120f);
            kit.Row(rows, "RecordRow", "Record flight data", "Writes a CSV file for this session.", out control);
            record = kit.Switch(NewToggle("RecordFlight"));
            MenuKit.Place(record, control);

            var launch = Launch(fly.root.transform, out summary, string.Empty);
            summary.name = "FlightSummary";
            solo = kit.Button(launch, "StartSolo", "Start flight", MenuButtonLook.Primary, "play", MenuButtonSize.Large);
            return fly;
        }

        private TMP_Dropdown CloneDropdown(string name, string[] options)
        {
            // Reuse the repaired TMP dropdown hierarchy (template, item toggle and viewport) from the in-flight menu.
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(InFlightMenuPath).GetComponentInChildren<HeliSim.InFlightMenu.DisplaySettingsPage>(true);
            var dropdownSource = new SerializedObject(source).FindProperty("windowMode").objectReferenceValue as TMP_Dropdown;
            var dropdown = Object.Instantiate(dropdownSource);
            dropdown.name = name;
            dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
            dropdown.ClearOptions();
            dropdown.AddOptions(options.ToList());
            dropdown.gameObject.SetActive(true);
            return dropdown;
        }

        private MainMenuPageEntry MultiplayerPage(out Toggle spectator, out TMP_InputField address, out TMP_InputField port, out Button host, out Button join)
        {
            var page = Page("multiplayer", "Multiplayer", "multi", "MultiplayerPage", "Fly together",
                "Host a session or join one by IPv4 address. Spectators don’t need their own helicopter.");
            var rows = Rows(page.root.transform, "Session", "Session");
            kit.Row(rows, "RoleRow", "Your role", "Pilots fly their own helicopter. Spectators watch without controlling one.", out RectTransform control);
            spectator = kit.Switch(NewToggle("Role"), "Pilot", "Spectator", true);
            MenuKit.Place(spectator, control);
            kit.Row(rows, "AddressRow", "Server address", "IPv4 of the host on your network.", out control);
            address = NewField("Address", "127.0.0.1");
            MenuKit.Place(kit.Field(address, true), control, -1f, MenuKit.FieldHeight, 1f, 96f);
            kit.Row(rows, "PortRow", "Port", "1 to 65535.", out control);
            port = NewField("Port", "7777");
            port.contentType = TMP_InputField.ContentType.IntegerNumber;
            MenuKit.Place(kit.Field(port, true, MenuKit.NumberFieldWidth, TextAlignmentOptions.MidlineRight), control,
                MenuKit.NumberFieldWidth, MenuKit.FieldHeight, 0f, MenuKit.NumberFieldWidth);
            kit.Note(page.root.transform, "NetworkingHelp",
                "Hosting listens on your local network. Assistance and recording come from the Fly page. The host owns the shared environment.");
            var launch = Launch(page.root.transform, out _, "Join connects to the address and port above.");
            var buttons = MenuKit.Rect("Buttons", launch);
            MenuKit.RowLayout(buttons.gameObject, 0, 0, 0, 0, 12f, TextAnchor.MiddleRight);
            host = kit.Button(buttons, "StartHost", "Host session", MenuButtonLook.Default);
            join = kit.Button(buttons, "StartJoin", "Join session", MenuButtonLook.Primary);
            page.initialSelectable = spectator;
            return page;
        }

        private MainMenuPageEntry SettingsHub(out Dictionary<string, Button> categories)
        {
            var page = Page("settings", "Settings", "sliders", "SettingsPage", "Settings", "These preferences and bindings are shared with the in-flight menu.");
            var tiles = MenuKit.Rect("CategoryTiles", page.root.transform);
            MenuKit.Column(tiles.gameObject, 0, 0, 0, 0, 18f);
            categories = new Dictionary<string, Button>();
            var items = new[]
            {
                ("display", "Display & graphics", "display", "Screen mode, rendering quality and interface scale."),
                ("audio", "Audio & simulator", "speaker", "Master volume and simulator hardware preferences."),
                ("controls", "Controls & bindings", "stick", "Rebind any command. Keyboard bindings always remain as a fallback."),
                ("streaming", "World streaming", "globe", "Cesium 3D Tiles streaming and culling."),
            };
            RectTransform tileRow = null;
            for (int i = 0; i < items.Length; i++)
            {
                if (i % 2 == 0)
                {
                    tileRow = MenuKit.Rect("TileRow" + (i / 2 + 1), tiles);
                    MenuKit.RowLayout(tileRow.gameObject, 0, 0, 0, 0, 18f, TextAnchor.UpperLeft);
                    tileRow.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
                }

                (string id, string title, string icon, string description) = items[i];
                categories[id] = Tile(tileRow, id + "Settings", title, icon, description);
            }

            kit.Note(page.root.transform, "VrHelp", "VR uses the configured OpenXR runtime. Head tracking and seated recenter stay with the XR system, " +
                "and every menu works with keyboard, mouse or HOTAS, without hand controllers.", "vr");
            page.initialSelectable = categories["display"];
            return page;
        }

        private Button Tile(Transform parent, string name, string title, string icon, string description)
        {
            var button = MenuKit.Rect(name, parent).gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            Image background = kit.Fill(button.gameObject, Color.white, kit.Theme.PanelSprite, true);
            button.targetGraphic = background;
            kit.Border(button.transform, P.line, kit.Theme.PanelBorderSprite);
            MenuKit.Column(button.gameObject, 30, 30, 30, 30, 12f);
            MenuKit.Layout(button.gameObject, 0f, -1f, 1f, 0f);
            var top = MenuKit.Rect("Top", button.transform);
            MenuKit.RowLayout(top.gameObject, 0, 0, 0, 6, 0f);
            var box = MenuKit.Rect("IconBox", top);
            kit.Fill(box.gameObject, P.bg3, kit.Theme.ControlSprite);
            MenuKit.RowLayout(box.gameObject, 0, 0, 0, 0, 0f, TextAnchor.MiddleCenter);
            MenuKit.Layout(box.gameObject, 54f, 54f, 0f, 54f, 54f);
            Image iconImage = kit.Icon(box, icon, P.accent);
            MenuKit.Spacer(top, 0f, 1f);
            kit.Icon(top, "chev", P.text3, 27f, "Go");
            kit.Text(button.transform, "Title", title, MenuTextRole.Title, P.text1);
            kit.Text(button.transform, "Description", description, MenuTextRole.Hint, P.text2);
            Image ring = kit.FocusRing(button.transform);
            kit.Style(button, MenuSelectableVariant.Row, background, ring);
            return button;
        }

        private MainMenuPageEntry HardwarePage(MenuVrPresentation vrPresentation, out TMP_Text summary, MainMenuScreen screen)
        {
            var page = Page("hardware", "Hardware & setup", "stick", "HardwarePage", "Check your setup",
                "Connected devices are optional. Detection doesn’t verify calibration or a physical motion-platform connection.");
            var section = kit.Section(page.root.transform, "Detected", "Detected");
            var cards = MenuKit.Rect("Cards", section);
            MenuKit.RowLayout(cards.gameObject, 0, 0, 0, 0, 18f, TextAnchor.UpperLeft);
            cards.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
            HardwareCard(cards, "FlightControlsCard", "Flight controls", "Detecting…", P.text3, "Keyboard fallback stays available.",
                out TMP_Text controlsValue, out Image controlsDot, out summary);
            summary.name = "HardwareSummary";
            HardwareCard(cards, "HeadsetCard", "VR headset", "Not active", P.text3,
                "No headset reported. Desktop flying is available. Start your PC VR runtime before launching.",
                out TMP_Text headsetValue, out Image headsetDot, out TMP_Text headsetDetail);
            HardwareCard(cards, "NovaCard", "NOVA", "Unverified", P.caution,
                "This menu never connects, calibrates or sends motion. Follow the operator setup and safety procedures.", out _, out _, out _);
            Set(screen, "controlsStatus", controlsValue); Set(screen, "controlsDot", controlsDot);
            Set(screen, "headsetStatus", headsetValue); Set(screen, "headsetDot", headsetDot); Set(screen, "headsetDetail", headsetDetail);

            var actions = MenuKit.Rect("Actions", page.root.transform);
            MenuKit.RowLayout(actions.gameObject, 0, 0, 0, 0, 12f);
            var bindings = kit.Button(actions, "OpenBindings", "Open controls & bindings", MenuButtonLook.Default, "stick");
            UnityEventTools.AddStringPersistentListener(bindings.onClick, router.SelectPageFromButton, "controls");
            var recenter = kit.Button(actions, "RecenterMenu", "Recenter VR menu", MenuButtonLook.Ghost, "target");
            UnityEventTools.AddPersistentListener(recenter.onClick, vrPresentation.RecenterMenu);
            page.initialSelectable = bindings;
            return page;
        }

        private void HardwareCard(Transform parent, string name, string key, string value, Color dot, string detail,
            out TMP_Text valueText, out Image dotImage, out TMP_Text detailText)
        {
            var card = MenuKit.Rect(name, parent);
            kit.Fill(card.gameObject, MenuPalette.WithAlpha(P.bg1, 0.5f), kit.Theme.PanelSprite);
            kit.Border(card, P.line, kit.Theme.PanelBorderSprite);
            MenuKit.Column(card.gameObject, 30, 30, 27, 27, 12f);
            MenuKit.Layout(card.gameObject, 0f, -1f, 1f, 0f);
            kit.Text(card, "Key", key, MenuTextRole.Small, P.text3, TextAlignmentOptions.TopLeft, true, 14f, false);
            var valueRow = MenuKit.Rect("Value", card);
            MenuKit.RowLayout(valueRow.gameObject, 0, 0, 0, 0, 12f);
            dotImage = kit.Dot(valueRow, "Dot", dot);
            valueText = kit.Text(valueRow, "Text", value, MenuTextRole.Subtitle, P.text1, TextAlignmentOptions.MidlineLeft, false, 0f, false);
            detailText = kit.Text(card, "Detail", detail, MenuTextRole.Eyebrow, P.text2);
            detailText.font = kit.Theme.BodyFont;
            detailText.fontSharedMaterial = kit.Theme.BodyFont.material;
        }

        private MainMenuPageEntry RecordingsPage(out Toggle preference, out TMP_Text summary, out Button folder)
        {
            var page = Page("recordings", "Recordings", "rec", "RecordingsPage", "Flight recordings",
                "Choose whether new flights record by default and open the folder of saved CSV files.");
            var rows = Rows(page.root.transform, "Preference", "Preference");
            kit.Row(rows, "RecordPreferenceRow", "Record CSV data by default", "Pre-selects recording on the Fly page.", out RectTransform control);
            preference = kit.Switch(NewToggle("RecordPreference"));
            MenuKit.Place(preference, control);
            rows = Rows(page.root.transform, "SavedFiles", "Saved files");
            summary = kit.Text(null, "RecordingSummary", "CSV recordings", MenuTextRole.Label, P.text1);
            kit.Row(rows, "SavedFilesRow", null, "Saved in the HeliSim recordings folder.", out control, 0f, null, null, summary);
            folder = kit.Button(control, "OpenRecordings", "Open folder", MenuButtonLook.Default, "folder");
            page.initialSelectable = preference;
            return page;
        }

        private void HelpPage()
        {
            var page = Page("help", "Help & about", "help", "HelpPage", "Before you take off",
                "HeliSim supports desktop flying, seated VR and simulator installations.");
            var section = kit.Section(page.root.transform, "GettingStarted", "Getting started");
            var steps = kit.Rows(section, "Steps");
            string[] items =
            {
                "Set up your controls in " + Strong("Settings › Controls & bindings") + ".",
                "Choose assistance and recording on " + Strong("Fly") + ".",
                "Start a local flight, or host or join from " + Strong("Multiplayer") + ".",
                "Press " + Strong("Esc") + " in flight for session controls and settings.",
                "Use " + Strong("Return to menu") + " to end a session before starting another.",
            };
            for (int i = 0; i < items.Length; i++)
            {
                var step = MenuKit.Rect("Step" + (i + 1), steps);
                MenuKit.RowLayout(step.gameObject, 30, 30, 21, 21, 18f, TextAnchor.UpperLeft);
                if (i > 0) kit.TopDivider(step);
                var number = kit.Text(step, "Number", (i + 1).ToString(), MenuTextRole.Subtitle, P.accent, TextAlignmentOptions.TopLeft, false, 0f, false);
                MenuKit.Layout(number.gameObject, 36f, -1f, 0f, 36f);
                var text = kit.Text(step, "Text", items[i], MenuTextRole.Body, P.text2);
                MenuKit.Layout(text.gameObject, -1f, -1f, 1f, 0f);
            }

            section = kit.Section(page.root.transform, "Troubleshooting", "Troubleshooting");
            var cards = MenuKit.Rect("Cards", section);
            MenuKit.RowLayout(cards.gameObject, 0, 0, 0, 0, 18f, TextAnchor.UpperLeft);
            cards.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
            TroubleCard(cards, "ConnectionCard", "Can’t connect",
                "Check the host’s IPv4 address and port, that the host is running, and firewall permissions. A failed join returns here with the reason.");
            TroubleCard(cards, "HardwareCard", "VR or hardware missing",
                "Carry on with keyboard and mouse on desktop. NOVA operation needs the normal operator checks.");
            kit.Text(page.root.transform, "Version", Application.productName + " " + Application.version, MenuTextRole.Micro, P.text3)
                .font = kit.Theme.MonoFont;
        }

        private void TroubleCard(Transform parent, string name, string title, string body)
        {
            var card = MenuKit.Rect(name, parent);
            kit.Fill(card.gameObject, MenuPalette.WithAlpha(P.bg1, 0f), kit.Theme.PanelSprite);
            kit.Border(card, P.line, kit.Theme.PanelBorderSprite);
            MenuKit.Column(card.gameObject, 30, 30, 24, 24, 6f);
            MenuKit.Layout(card.gameObject, 0f, -1f, 1f, 0f);
            kit.Text(card, "Title", title, MenuTextRole.Body, P.text1).font = kit.Theme.DisplayFont;
            kit.Text(card, "Body", body, MenuTextRole.Hint, P.text2);
        }

        // ------------------------------------------------------------ settings sub-pages

        private void BindingDialogAndSettings(Dictionary<string, Button> categories, PrefSettings prefs, CanvasScaler scaler)
        {
            // Clone the rebuilt in-flight hierarchy once so Unity remaps cross-page and dialog references.
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(InFlightMenuPath);
            var clone = Object.Instantiate(source);
            clone.SetActive(false);
            try
            {
                var all = clone.GetComponentsInChildren<Transform>(true);
                var dialog = all.First(t => t.name == "BindingDialog");
                dialog.SetParent(root, false);
                dialog.gameObject.SetActive(false);
                foreach ((string id, string pageName, string title) in new[]
                         {
                             ("display", "DisplayPage", "Display & graphics"), ("audio", "SettingsPage", "Audio & simulator"),
                             ("controls", "ControlsPage", "Controls & bindings"), ("streaming", "CesiumPage", "World streaming"),
                         })
                {
                    var page = all.First(t => t.name == pageName && t.GetComponent<InFlightMenuPage>() != null);
                    page.SetParent(content, false);
                    page.gameObject.SetActive(false);
                    var presenter = page.GetComponent<InFlightMenuPage>();
                    var data = new SerializedObject(presenter);
                    var pref = data.FindProperty("prefSettings"); if (pref != null) pref.objectReferenceValue = prefs;
                    var scale = data.FindProperty("menuCanvasScaler"); if (scale != null) scale.objectReferenceValue = scaler;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    var heading = page.Find("PageHead/PageTitle");
                    if (heading != null) heading.GetComponent<TMP_Text>().text = title;
                    if (id == "streaming") AddTokenSection(kit, page);
                    var attachment = page.GetComponent<MenuPageAttachment>();
                    if (attachment != null)
                    {
                        foreach (var bar in attachment.Attached.Where(b => b != null))
                        {
                            bar.transform.SetParent(actionBars, false);
                            bar.SetActive(false);
                        }
                    }

                    pages.Add(new MainMenuPageEntry { id = id, root = page.gameObject, settingsPage = presenter, navigationButton = categories[id], title = title });
                }
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }
    }
}
