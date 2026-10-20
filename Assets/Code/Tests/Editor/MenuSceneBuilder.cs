using System;
using System.Collections.Generic;
using System.Linq;
using HeliSim.FrontEnd;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds serialized menu assets through Unity APIs. Never rebuilds gameplay scenes.
public static class MenuSceneBuilder
{
    public const string PrefabPath = "Assets/Level/Prefabs/UI/MainMenu.prefab";
    private static readonly Color Background = new Color32(9, 17, 25, 255);
    private static readonly Color Panel = new Color32(17, 30, 41, 255);
    private static readonly Color Accent = new Color32(58, 196, 183, 255);
    private static readonly Color Text = new Color32(232, 241, 246, 255);
    private static readonly Color Muted = new Color32(157, 180, 193, 255);

    [MenuItem("HeliSim/Build Main Menu Assets")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("The Editor must be idle to build menu assets.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Preserve unsaved scenes before building menu assets.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        try
        {
            var root = Rect("MainMenu", null);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var screen = root.gameObject.AddComponent<MainMenuScreen>();
            var router = root.gameObject.AddComponent<MainMenuPageRouter>();
            var prefs = root.gameObject.AddComponent<PrefSettings>();
            var vrPresentation = root.gameObject.AddComponent<MenuVrPresentation>();
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(root, false);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var body = Rect("MenuContent", root);
            Stretch(body);
            body.gameObject.AddComponent<CanvasGroup>();
            body.gameObject.AddComponent<MainMenuContentMarker>();
            Image(body, Background);

            var nav = Rect("Navigation", body); Position(nav, new Vector2(0, 0), new Vector2(0, 1), new Vector2(350, 0), new Vector2(175, 0)); Image(nav, Panel);
            Label(nav, "Brand", "HELI<color=#3AC4B7>SIM</color>", 46, new Vector2(30, -55), new Vector2(290, 75));
            Label(nav, "BrandSubtitle", "FLIGHT SIMULATOR", 17, new Vector2(32, -122), new Vector2(280, 35), Muted);
            var version = Label(nav, "Version", "BUILD " + Application.version + "  /  UNITY " + Application.unityVersion, 14, new Vector2(30, 80), new Vector2(290, 55), Muted);
            version.rectTransform.anchorMin = version.rectTransform.anchorMax = Vector2.zero;
            version.rectTransform.pivot = Vector2.zero;
            var quit = Button(nav, "Quit", "Quit", new Vector2(30, 25), new Vector2(290, 52));
            quit.GetComponent<RectTransform>().anchorMin = quit.GetComponent<RectTransform>().anchorMax = Vector2.zero;
            quit.GetComponent<RectTransform>().pivot = Vector2.zero;

            var header = Rect("Header", body); Position(header, new Vector2(0, 1), Vector2.one, new Vector2(-410, 125), new Vector2(175, -95));
            Label(header, "Heading", "READY FOR FLIGHT", 18, new Vector2(0, 0), new Vector2(1100, 36), Accent);
            var status = Label(header, "Status", "", 21, new Vector2(0, -43), new Vector2(1370, 60), Muted);
            var viewport = Rect("PageViewport", body); viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = new Vector2(410, 70); viewport.offsetMax = new Vector2(-60, -140);
            Image(viewport, new Color(0, 0, 0, 0.01f)); viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = Rect("PageContent", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1); content.sizeDelta = Vector2.zero;
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>(); contentLayout.childControlHeight = true; contentLayout.childControlWidth = true; contentLayout.childForceExpandHeight = false; contentLayout.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 35;
            var bar = Rect("PageScrollbar", viewport); bar.anchorMin = new Vector2(1, 0); bar.anchorMax = Vector2.one; bar.sizeDelta = new Vector2(12, 0); bar.anchoredPosition = new Vector2(-6, 0); Image(bar, Panel);
            var handle = Rect("Handle", bar); Stretch(handle); Image(handle, Accent);
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            var pages = new List<MainMenuPageEntry>();
            int navIndex = 0;
            MainMenuPageEntry Page(string id, string title, string objectName)
            {
                var page = Rect(objectName, content);
                var layout = page.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 14; layout.padding = new RectOffset(5, 15, 5, 30); layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false;
                var navigation = Button(nav, id + "Navigation", title, new Vector2(30, -210 - navIndex++ * 73), new Vector2(290, 58));
                var entry = new MainMenuPageEntry { id = id, root = page.gameObject, navigationButton = navigation };
                pages.Add(entry); return entry;
            }

            var fly = Page("fly", "Fly", "FlyPage");
            Heading(fly.root.transform, "Your next flight", "Configure a local session, or jump back in with your last valid setup.");
            Card(fly.root.transform, "AvailableAircraft", "AIRCRAFT   R66\nLOCATION   Multiplayer environment", 90);
            var assistance = Dropdown(fly.root.transform, "Assistance", "Assistance", new[] { "Keep aircraft default", "Manual Torque", "Manual", "Low", "Medium", "High", "Very High" });
            var record = Toggle(fly.root.transform, "RecordFlight", "Record CSV flight data for this session");
            var flightSummary = FlowLabel(fly.root.transform, "FlightSummary", "", 23, 100);
            var quick = FlowButton(fly.root.transform, "QuickStart", "Quick Start - last setup", true);
            quick.transform.SetSiblingIndex(2);
            var solo = FlowButton(fly.root.transform, "StartSolo", "Start configured flight");
            fly.initialSelectable = quick;

            var multiplayer = Page("multiplayer", "Multiplayer", "MultiplayerPage");
            Heading(multiplayer.root.transform, "Fly together", "Host a session or join by IPv4 address. Spectators do not need to own a helicopter.");
            var role = Dropdown(multiplayer.root.transform, "Role", "Your role", new[] { "Pilot", "Spectator" });
            var address = Input(multiplayer.root.transform, "Address", "Server IPv4 address", "127.0.0.1");
            var port = Input(multiplayer.root.transform, "Port", "Port", "7777"); port.contentType = TMP_InputField.ContentType.IntegerNumber;
            FlowLabel(multiplayer.root.transform, "NetworkingHelp", "Hosting listens on the LAN. Joining uses the address above. Flight assistance and recording choices come from Fly. The host owns the shared environment.", 22, 80);
            var host = FlowButton(multiplayer.root.transform, "StartHost", "Host session", true);
            var join = FlowButton(multiplayer.root.transform, "StartJoin", "Join session");
            multiplayer.initialSelectable = role;

            var settings = Page("settings", "Settings", "SettingsPage");
            Heading(settings.root.transform, "Make it yours", "These preferences and control bindings are shared with the in-flight menu.");
            var categories = new Dictionary<string, Button>();
            foreach (var item in new[] { new[] { "display", "Display & graphics" }, new[] { "audio", "Audio & simulator preferences" }, new[] { "controls", "Controls & bindings" }, new[] { "streaming", "Advanced world streaming" } })
                categories[item[0]] = FlowButton(settings.root.transform, item[0] + "Settings", item[1]);
            FlowLabel(settings.root.transform, "VrHelp", "VR uses the configured OpenXR runtime. Head tracking and seated recenter remain under the existing XR system. Keyboard and mouse menu navigation works without hand controllers.\n\nDisplay settings include UI scale. Unsupported options are identified on their pages.", 23, 180);
            settings.initialSelectable = categories["display"];

            var hardware = Page("hardware", "Hardware & setup", "HardwarePage");
            Heading(hardware.root.transform, "Check your setup", "Connected devices are optional. Detection does not verify calibration or a physical motion-platform connection.");
            var hardwareSummary = FlowLabel(hardware.root.transform, "HardwareSummary", "", 24, 190);
            FlowLabel(hardware.root.transform, "HardwareGuide", "FLIGHT CONTROLS\nOpen Controls & bindings to inspect or change assignments. Keyboard fallback remains available.\n\nSEATED VR\nStart your PC VR runtime before launching HeliSim. Use the in-flight recenter action once seated.\n\nNOVA\nUse the established operator setup and safety procedures. This menu does not connect, calibrate or send motion commands.", 24, 380);
            hardware.initialSelectable = FlowButton(hardware.root.transform, "OpenBindings", "Open Controls & bindings");
            UnityEventTools.AddStringPersistentListener(((Button)hardware.initialSelectable).onClick, router.SelectPageFromButton, "controls");
            var recenter = FlowButton(hardware.root.transform, "RecenterMenu", "Recenter VR menu");
            UnityEventTools.AddPersistentListener(recenter.onClick, vrPresentation.RecenterMenu);

            var recordings = Page("recordings", "Recordings", "RecordingsPage");
            Heading(recordings.root.transform, "Flight recordings", "Manage the recording preference and access existing CSV files.");
            var recordPreference = Toggle(recordings.root.transform, "RecordPreference", "Record CSV data by default");
            var recordingSummary = FlowLabel(recordings.root.transform, "RecordingSummary", "", 23, 300);
            var folder = FlowButton(recordings.root.transform, "OpenRecordings", "Open recordings folder");
            recordings.initialSelectable = recordPreference;

            var help = Page("help", "Help & about", "HelpPage");
            Heading(help.root.transform, "Before you take off", "HeliSim supports desktop flying, seated VR and simulator installations.");
            FlowLabel(help.root.transform, "QuickGuide", "1   Configure controls in Settings > Controls & bindings.\n2   Choose assistance and recording options on Fly.\n3   Start a local flight, or host/join from Multiplayer.\n4   Press Escape in flight for session controls and settings.\n5   Use Return to Menu to close the session before starting another.\n\nCONNECTION TROUBLE?\nConfirm the host IPv4 address and port, host availability and firewall permissions. A failed join returns here with feedback.\n\nVR OR HARDWARE UNAVAILABLE?\nContinue on desktop with keyboard and mouse. NOVA operation requires the normal operator checks.\n\n" + Application.productName + "  " + Application.version, 24, 650);

            // Clone the existing hierarchy once so Unity remaps cross-page/dialog references.
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/UI/Menu.prefab");
            var clone = Object.Instantiate(source); clone.SetActive(false);
            var all = clone.GetComponentsInChildren<Transform>(true);
            var dialog = all.First(t => t.name == "BindingDialog"); dialog.SetParent(root, false); dialog.gameObject.SetActive(false);
            foreach (var pair in new[] { new[] { "display", "DisplayPage" }, new[] { "audio", "SettingsPage" }, new[] { "controls", "ControlsPage" }, new[] { "streaming", "CesiumPage" } })
            {
                var page = all.First(t => t.name == pair[1] && t.GetComponent<InFlightMenuPage>() != null);
                page.SetParent(content, false);
                var presenter = page.GetComponent<InFlightMenuPage>();
                var data = new SerializedObject(presenter);
                var pref = data.FindProperty("prefSettings"); if (pref != null) pref.objectReferenceValue = prefs;
                var scale = data.FindProperty("menuCanvasScaler"); if (scale != null) scale.objectReferenceValue = scaler;
                data.ApplyModifiedPropertiesWithoutUndo();
                pages.Add(new MainMenuPageEntry { id = pair[0], root = page.gameObject, settingsPage = presenter, navigationButton = categories[pair[0]] });
                var back = FlowButton(page, "BackToSettings", "Back to Settings");
                UnityEventTools.AddStringPersistentListener(back.onClick, router.SelectPageFromButton, "settings");
            }
            Object.DestroyImmediate(clone);
            var changes = Modal(root, "ConfirmChanges", "Unsaved settings", "Apply your changes, discard them, or keep editing.", out var apply, out var discard, out var cancel);
            apply.GetComponentInChildren<TMP_Text>().text = "Apply"; discard.GetComponentInChildren<TMP_Text>().text = "Discard"; cancel.GetComponentInChildren<TMP_Text>().text = "Keep editing";
            var quitDialog = Modal(root, "ConfirmQuit", "Quit HeliSim?", "Your applied preferences are saved.", out var yes, out var unused, out var no);
            yes.GetComponentInChildren<TMP_Text>().text = "Quit"; unused.gameObject.SetActive(false); no.GetComponentInChildren<TMP_Text>().text = "Cancel";
            var loading = Rect("LoadingOverlay", root); Stretch(loading); loading.gameObject.SetActive(false);
            router.Configure(pages.ToArray(), changes, apply, discard, cancel, events.GetComponent<EventSystem>());
            router.SetScrollRect(scroll);
            Set(screen, "router", router); Set(screen, "address", address); Set(screen, "port", port); Set(screen, "role", role); Set(screen, "assistance", assistance);
            Set(screen, "recordFlight", record); Set(screen, "recordPreference", recordPreference); Set(screen, "flightSummary", flightSummary); Set(screen, "hardwareSummary", hardwareSummary); Set(screen, "recordingSummary", recordingSummary); Set(screen, "status", status);
            Set(screen, "quickStart", quick); Set(screen, "startSolo", solo); Set(screen, "startHost", host); Set(screen, "startJoin", join); Set(screen, "recordingsFolder", folder); Set(screen, "quit", quit); Set(screen, "quitConfirmation", quitDialog); Set(screen, "confirmQuit", yes); Set(screen, "cancelQuit", no);
            foreach (var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true)) ConfigureDropdown(dropdown);
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = LayerMask.NameToLayer("UI");
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            Object.DestroyImmediate(root.gameObject);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            var camera = new GameObject("MenuCamera", typeof(Camera), typeof(AudioListener));
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor = Background;
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

    [MenuItem("HeliSim/Repair Menu Dropdowns and Entry Focus")]
    public static void RepairUsabilityAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("The Editor must be idle to repair menu assets.");
        foreach (string path in new[] { "Assets/Level/Prefabs/UI/Menu.prefab", PrefabPath })
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

    private static void ConfigureDropdown(TMP_Dropdown dropdown)
    {
        var background = dropdown.GetComponent<UnityEngine.UI.Image>();
        if (background == null || dropdown.template == null)
            throw new InvalidOperationException("Menu dropdown requires a field Image and option template: " + dropdown.name);
        background.enabled = true;
        background.raycastTarget = true;
        background.color = Panel;
        background.sprite = null;
        background.type = UnityEngine.UI.Image.Type.Simple;
        dropdown.targetGraphic = background;
        if (dropdown.captionText != null)
        {
            Stretch(dropdown.captionText.rectTransform);
            dropdown.captionText.rectTransform.offsetMin = new Vector2(18, 4);
            dropdown.captionText.rectTransform.offsetMax = new Vector2(-46, -4);
            dropdown.captionText.alignment = TextAlignmentOptions.MidlineLeft;
        }
        var template = dropdown.template;
        template.anchorMin = Vector2.zero;
        template.anchorMax = Vector2.right;
        template.pivot = new Vector2(0.5f, 1f);
        template.sizeDelta = new Vector2(0, 240);
        template.anchoredPosition = Vector2.zero;
        var viewport = template.Find("Viewport");
        if (viewport != null && viewport.TryGetComponent<UnityEngine.UI.Image>(out var viewportImage))
        {
            viewportImage.enabled = true;
            viewportImage.color = Panel;
            viewportImage.sprite = null;
            viewportImage.type = UnityEngine.UI.Image.Type.Simple;
            if (viewport.TryGetComponent<Mask>(out var mask)) mask.showMaskGraphic = true;
        }
    }

    private static void Set(Object target, string name, Object value) { var data = new SerializedObject(target); data.FindProperty(name).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static RectTransform Rect(string name, Transform parent) { var item = new GameObject(name, typeof(RectTransform)); if (parent != null) item.transform.SetParent(parent, false); return item.GetComponent<RectTransform>(); }
    private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void Position(RectTransform rect, Vector2 min, Vector2 max, Vector2 size, Vector2 pos) { rect.anchorMin = min; rect.anchorMax = max; rect.sizeDelta = size; rect.anchoredPosition = pos; }
    private static void Image(RectTransform rect, Color color) { var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; }
    private static TMP_Text Label(Transform parent, string name, string text, int size, Vector2 position, Vector2 dimensions, Color? color = null)
    {
        var rect = Rect(name, parent); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = dimensions;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = TMP_Settings.defaultFontAsset; label.text = text; label.fontSize = size; label.color = color ?? Text; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal; return label;
    }
    private static TMP_Text FlowLabel(Transform parent, string name, string text, int size, float height) { var label = Label(parent, name, text, size, Vector2.zero, Vector2.zero); label.gameObject.AddComponent<LayoutElement>().preferredHeight = height; return label; }
    private static void Heading(Transform parent, string title, string description) { FlowLabel(parent, "PageTitle", title, 52, 70); FlowLabel(parent, "Description", description, 24, 65).color = Muted; }
    private static void Card(Transform parent, string name, string text, float height) { var rect = Rect(name, parent); Image(rect, Panel); rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height; var label = Label(rect, "Label", text, 23, new Vector2(20, -15), new Vector2(1000, height - 20)); }
    private static Button Button(Transform parent, string name, string title, Vector2 position, Vector2 size)
    {
        var rect = Rect(name, parent); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
        Image(rect, new Color32(29, 51, 65, 255)); var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); var colors = button.colors; colors.highlightedColor = new Color(0.65f, 0.9f, 0.9f); colors.selectedColor = new Color(0.65f, 0.9f, 0.9f); button.colors = colors;
        var text = Label(rect, "Label", title, 23, Vector2.zero, size); Stretch(text.rectTransform); text.alignment = TextAlignmentOptions.Midline; return button;
    }
    private static Button FlowButton(Transform parent, string name, string title, bool primary = false) { var button = Button(parent, name, title, Vector2.zero, new Vector2(1000, 60)); button.gameObject.AddComponent<LayoutElement>().preferredHeight = 60; if (primary) { button.GetComponent<UnityEngine.UI.Image>().color = Accent; button.GetComponentInChildren<TMP_Text>().color = Background; } return button; }
    private static Toggle Toggle(Transform parent, string name, string title)
    {
        var rect = Rect(name, parent); rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 58; var toggle = rect.gameObject.AddComponent<UnityEngine.UI.Toggle>();
        var box = Rect("Background", rect); box.anchorMin = box.anchorMax = new Vector2(0, 0.5f); box.pivot = new Vector2(0, 0.5f); box.sizeDelta = new Vector2(38, 38); Image(box, Panel);
        var check = Rect("Checkmark", box); Stretch(check); check.offsetMin = new Vector2(8, 8); check.offsetMax = new Vector2(-8, -8); Image(check, Accent); toggle.targetGraphic = box.GetComponent<UnityEngine.UI.Image>(); toggle.graphic = check.GetComponent<UnityEngine.UI.Image>();
        var text = Label(rect, "Label", title, 24, new Vector2(60, 0), new Vector2(1000, 58)); text.alignment = TextAlignmentOptions.MidlineLeft; return toggle;
    }
    private static TMP_InputField Input(Transform parent, string name, string title, string initial)
    {
        FlowLabel(parent, name + "Label", title, 22, 28);
        var rect = Rect(name, parent); rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 54; Image(rect, Panel);
        var field = rect.gameObject.AddComponent<TMP_InputField>(); var text = Label(rect, "Text", initial, 25, Vector2.zero, Vector2.zero); Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(18, 4); text.rectTransform.offsetMax = new Vector2(-18, -4); text.alignment = TextAlignmentOptions.MidlineLeft;
        field.textViewport = text.rectTransform; field.textComponent = text; field.text = initial; field.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>(); return field;
    }
    private static TMP_Dropdown Dropdown(Transform parent, string name, string title, string[] options)
    {
        FlowLabel(parent, name + "Label", title, 22, 28);
        // Reuse a working TMP dropdown hierarchy (template, item toggle and viewport).
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/UI/Menu.prefab").GetComponentInChildren<HeliSim.InFlightMenu.DisplaySettingsPage>(true);
        var dropdownSource = new SerializedObject(source).FindProperty("windowMode").objectReferenceValue as TMP_Dropdown;
        var dropdown = Object.Instantiate(dropdownSource, parent); dropdown.name = name; dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent(); dropdown.ClearOptions(); dropdown.AddOptions(options.ToList()); dropdown.gameObject.SetActive(true);
        var layout = dropdown.GetComponent<LayoutElement>(); if (layout == null) layout = dropdown.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = 54; return dropdown;
    }
    private static GameObject Modal(Transform parent, string name, string title, string message, out Button apply, out Button discard, out Button cancel)
    {
        var modal = Rect(name, parent); Stretch(modal); Image(modal, new Color(0, 0, 0, 0.86f));
        var panel = Rect("Panel", modal); panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f); panel.sizeDelta = new Vector2(1000, 360); Image(panel, Panel);
        Label(panel, "Title", title, 37, new Vector2(35, -30), new Vector2(930, 65)); Label(panel, "Message", message, 24, new Vector2(35, -105), new Vector2(930, 80));
        apply = Button(panel, "Apply", "Apply", new Vector2(35, -240), new Vector2(280, 65)); discard = Button(panel, "Discard", "Discard", new Vector2(355, -240), new Vector2(280, 65)); cancel = Button(panel, "Cancel", "Cancel", new Vector2(675, -240), new Vector2(280, 65)); modal.gameObject.SetActive(false); return modal.gameObject;
    }
}
