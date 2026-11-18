using System;
using System.Collections.Generic;
using System.Linq;
using HeliSim.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Rebuilds the in-flight menu (Menu.prefab) to match docs/design/menu-redesign-preview.html
/// (plan 2026-11-17). Every visual object is recreated by <see cref="MenuKit"/>. Objects that carry
/// behaviour are kept: presenters and controllers, every Selectable (persistent listeners and
/// UI.prefab / scene overrides target them), the labels presenters write to, and the session
/// sections that UI.prefab hides by object ID. Saving is refused if any serialized reference held by
/// a presenter or controller would be lost.
/// </summary>
public static class MenuViewRebuilder
{
    public const string InFlightMenuPath = "Assets/Level/Prefabs/UI/Menu.prefab";
    private const string ContentPath = "Overlay/ContentPanel/PageViewport/Viewport/Content";

    [MenuItem("HeliSim/Rebuild Menu Views")]
    public static void RebuildAll()
    {
        RebuildInFlightMenu();
        MenuSceneBuilder.BuildMainMenuPrefab();
    }

    public static void RebuildInFlightMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
        {
            throw new InvalidOperationException("The Editor must be idle to rebuild menu views.");
        }

        MenuThemeAssets.CreateAll();
        var kit = new MenuKit(MenuThemeAssets.LoadTheme());
        GameObject root = PrefabUtility.LoadPrefabContents(InFlightMenuPath);
        try
        {
            if (root.transform.Find("Overlay/Frame") != null)
            {
                throw new InvalidOperationException("Menu.prefab is already rebuilt; restore it before running the rebuild again.");
            }

            ReferenceSnapshot before = ReferenceSnapshot.Take(root);
            new InFlightMenuBuild(kit, root).Run();
            before.AssertPreserved(root);
            PrefabUtility.SaveAsPrefabAsset(root, InFlightMenuPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>Records every non-null object reference held by behaviour components and every persistent listener.</summary>
    public sealed class ReferenceSnapshot
    {
        private readonly List<(Component owner, string property)> references = new List<(Component, string)>();
        private readonly List<(UnityEngine.Events.UnityEventBase evt, Component owner, int count)> listeners =
            new List<(UnityEngine.Events.UnityEventBase, Component, int)>();

        public static ReferenceSnapshot Take(GameObject root)
        {
            var snapshot = new ReferenceSnapshot();
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || !IsBehaviour(behaviour))
                {
                    continue;
                }

                var serialized = new SerializedObject(behaviour);
                SerializedProperty property = serialized.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null &&
                        property.propertyPath != "m_Script")
                    {
                        snapshot.references.Add((behaviour, property.propertyPath));
                    }
                }
            }

            foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                UnityEngine.Events.UnityEventBase evt = selectable is Button button ? button.onClick
                    : selectable is Toggle toggle ? toggle.onValueChanged
                    : selectable is Slider slider ? slider.onValueChanged
                    : selectable is TMP_Dropdown dropdown ? dropdown.onValueChanged
                    : selectable is TMP_InputField field ? (UnityEngine.Events.UnityEventBase)field.onEndEdit
                    : null;
                if (evt != null && evt.GetPersistentEventCount() > 0)
                {
                    snapshot.listeners.Add((evt, selectable, evt.GetPersistentEventCount()));
                }
            }

            return snapshot;
        }

        private static bool IsBehaviour(MonoBehaviour behaviour)
        {
            string ns = behaviour.GetType().Namespace ?? string.Empty;
            if (ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro"))
            {
                return false;
            }

            // Visual helpers are rebuilt by the kit and re-pointed at new graphics.
            return !(behaviour is MenuSelectableStyle) && !(behaviour is HeliSim.InFlightMenu.SettingsToggleVisual) &&
                   !(behaviour is MenuDisclosure) && !(behaviour is MenuActionBar) && !(behaviour is MenuBreadcrumb) &&
                   !(behaviour is MenuPageAttachment);
        }

        public void AssertPreserved(GameObject root)
        {
            var failures = new List<string>();
            foreach ((Component owner, string path) in references)
            {
                if (owner == null)
                {
                    failures.Add("a behaviour component was destroyed (" + path + ")");
                    continue;
                }

                SerializedProperty property = new SerializedObject(owner).FindProperty(path);
                if (property == null || property.objectReferenceValue == null)
                {
                    failures.Add(owner.name + "<" + owner.GetType().Name + ">." + path + " lost its reference");
                }
                else if (property.objectReferenceValue is Component component && !component.transform.IsChildOf(root.transform) &&
                         !EditorUtility.IsPersistent(component))
                {
                    failures.Add(owner.name + "." + path + " now points outside the menu");
                }
            }

            foreach ((UnityEngine.Events.UnityEventBase evt, Component owner, int count) in listeners)
            {
                if (owner == null)
                {
                    failures.Add("a Selectable with persistent listeners was destroyed");
                }
                else if (evt.GetPersistentEventCount() != count)
                {
                    failures.Add(owner.name + " listener count changed");
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException("Rebuild would lose wiring; nothing was saved:\n" + string.Join("\n", failures));
            }
        }
    }

    /// <summary>Holds kept objects while their old parents are destroyed.</summary>
    public sealed class Keeper
    {
        private readonly Transform holder;
        private readonly List<Transform> kept = new List<Transform>();

        public Keeper(Transform root)
        {
            holder = MenuKit.Rect("__Kept", root);
        }

        public T Keep<T>(Transform from, string path) where T : Component
        {
            Transform target = Find(from, path);
            T component = target.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException(path + " has no " + typeof(T).Name);
            }

            kept.Add(target);
            return component;
        }

        public Transform Keep(Transform from, string path)
        {
            Transform target = Find(from, path);
            kept.Add(target);
            return target;
        }

        public static Transform Find(Transform from, string path)
        {
            Transform target = from.Find(path);
            if (target == null)
            {
                throw new InvalidOperationException("Expected " + path + " under " + from.name);
            }

            return target;
        }

        /// <summary>Moves kept objects out (deepest first) and clears what their old containers still hold.</summary>
        public void Detach()
        {
            foreach (Transform target in kept.Distinct().OrderByDescending(Depth))
            {
                target.SetParent(holder, false);
            }

            foreach (Transform target in kept.Distinct())
            {
                if (target.GetComponent<Selectable>() == null)
                {
                    MenuKit.DestroyChildren(target);
                }
            }
        }

        public void Finish()
        {
            if (holder.childCount > 0)
            {
                string names = string.Join(", ", holder.Cast<Transform>().Select(t => t.name));
                throw new InvalidOperationException("Kept objects were not placed: " + names);
            }

            Object.DestroyImmediate(holder.gameObject);
        }

        private static int Depth(Transform transform)
        {
            int depth = 0;
            for (Transform t = transform; t != null; t = t.parent)
            {
                depth++;
            }

            return depth;
        }
    }

    /// <summary>Shell parts shared by the in-flight and main menus.</summary>
    public static class MenuShell
    {
        public static void Brand(MenuKit kit, Transform parent, string subtitle)
        {
            RectTransform brand = MenuKit.Rect("Brand", parent);
            MenuKit.Column(brand.gameObject, 18, 18, 0, 0, 6f);
            kit.Wordmark(brand);
            kit.Text(brand, "BrandSubtitle", subtitle, MenuTextRole.Micro, kit.P.text3, TextAlignmentOptions.TopLeft, true, 24f, false);
        }

        public static MenuBreadcrumb Breadcrumb(MenuKit kit, Transform parent, string rootText, Button parentButton)
        {
            RectTransform crumbs = MenuKit.Rect("Crumbs", parent);
            MenuKit.RowLayout(crumbs.gameObject, 0, 0, 0, 0, 12f);
            TMP_Text parentLabel;
            if (parentButton != null)
            {
                parentButton.transform.SetParent(crumbs, false);
                MenuKit.StripDecoration(parentButton.gameObject);
                MenuKit.DestroyChildren(parentButton.transform);
                Image hit = kit.Fill(parentButton.gameObject, MenuPalette.WithAlpha(kit.P.bg3, 0f), kit.Theme.ControlSprite, true);
                parentButton.targetGraphic = hit;
                parentButton.transition = Selectable.Transition.None;
                MenuKit.RowLayout(parentButton.gameObject, 0, 0, 0, 0, 0f);
                parentLabel = kit.Eyebrow(parentButton.transform, rootText, "Label", kit.P.text3);
                Image ring = kit.FocusRing(parentButton.transform, 6f);
                kit.Style(parentButton, MenuSelectableVariant.FocusOnly, null, ring);
            }
            else
            {
                parentLabel = kit.Eyebrow(crumbs, rootText, "Parent", kit.P.text3);
            }

            ((TextMeshProUGUI)parentLabel).alignment = TextAlignmentOptions.MidlineLeft;
            TextMeshProUGUI separator = kit.Eyebrow(crumbs, "/", "Separator", kit.P.line2);
            separator.alignment = TextAlignmentOptions.MidlineLeft;
            TextMeshProUGUI current = kit.Eyebrow(crumbs, string.Empty, "Current", kit.P.text1);
            current.alignment = TextAlignmentOptions.MidlineLeft;
            var breadcrumb = crumbs.gameObject.AddComponent<MenuBreadcrumb>();
            breadcrumb.Configure(kit.Theme, rootText, parentButton, parentLabel, current);
            return breadcrumb;
        }

        public static ScrollRect Scroll(MenuKit kit, Transform scrollHost, Transform viewportTransform, Transform contentTransform, Transform parent)
        {
            scrollHost.SetParent(parent, false);
            MenuKit.Layout(scrollHost.gameObject, -1f, -1f, 1f, -1f, 0f, 1f);
            RemoveGraphics(scrollHost.gameObject);
            foreach (Transform child in scrollHost.Cast<Transform>().ToArray())
            {
                if (child != viewportTransform)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            viewportTransform.SetParent(scrollHost, false);
            MenuKit.Stretch((RectTransform)viewportTransform);
            foreach (Mask mask in viewportTransform.GetComponents<Mask>())
            {
                Object.DestroyImmediate(mask);
            }

            RemoveGraphics(viewportTransform.gameObject);
            kit.Fill(viewportTransform.gameObject, MenuPalette.WithAlpha(kit.P.bg2, 0f), null, true);
            MenuKit.GetOrAdd<RectMask2D>(viewportTransform.gameObject);

            var contentRect = (RectTransform)contentTransform;
            contentRect.SetParent(viewportTransform, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            RemoveGraphics(contentTransform.gameObject);
            MenuKit.Column(contentTransform.gameObject, 0, 0, 0, 0, 0f, TextAnchor.UpperLeft, false);
            MenuKit.FitVertically(contentTransform.gameObject);

            RectTransform bar = MenuKit.Rect("Scrollbar", scrollHost);
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(1f, 0.5f);
            bar.offsetMin = new Vector2(-12f, 9f);
            bar.offsetMax = new Vector2(-3f, -9f);
            kit.Fill(bar.gameObject, MenuPalette.WithAlpha(kit.P.bg1, 0f), null, true);
            RectTransform slide = MenuKit.Rect("Sliding Area", bar);
            MenuKit.Stretch(slide);
            RectTransform handle = MenuKit.Rect("Handle", slide);
            MenuKit.Stretch(handle);
            Image handleImage = kit.Fill(handle.gameObject, kit.P.line2, kit.Theme.CircleSprite, true);
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.transition = Selectable.Transition.None;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            Image ring = kit.FocusRing(bar, 3f);
            kit.Style(scrollbar, MenuSelectableVariant.FocusOnly, null, ring);

            ScrollRect scroll = MenuKit.GetOrAdd<ScrollRect>(scrollHost.gameObject);
            scroll.content = contentRect;
            scroll.viewport = (RectTransform)viewportTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scroll.horizontalScrollbar = null;
            return scroll;
        }

        public static void BottomDivider(MenuKit kit, Transform parent)
        {
            Image divider = kit.TopDivider(parent);
            RectTransform rect = divider.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
        }

        public static void RemoveGraphics(GameObject go)
        {
            foreach (Graphic graphic in go.GetComponents<Graphic>())
            {
                Object.DestroyImmediate(graphic);
            }
        }
    }

    private sealed class InFlightMenuBuild
    {
        private readonly MenuKit kit;
        private readonly GameObject root;
        private readonly Keeper keeper;
        private MenuPalette P => kit.P;

        // Kept objects, captured before anything moves.
        private Transform overlay, rail, pageNavigation, contentPanel, pageViewport, viewport, content, scrim;
        private Button resume, returnToMenu;
        private readonly List<(Button button, string label, string icon)> navItems = new List<(Button, string, string)>();
        private Transform unsavedDialog, bindingDialog, novaPopup;
        private TMP_Text unsavedTitle, unsavedBody, bindingTitle, bindingCurrent, bindingStatus;
        private Button unsavedApply, unsavedDiscard, unsavedCancel, bindingCancel, bindingClear, bindingRestore, novaButton;
        private Transform actionBars;

        public InFlightMenuBuild(MenuKit kit, GameObject root)
        {
            this.kit = kit;
            this.root = root;
            keeper = new Keeper(root.transform);
        }

        public void Run()
        {
            Transform r = root.transform;
            Transform contentRoot = Keeper.Find(r, ContentPath);
            var flight = new FlightPage(this, Keeper.Find(contentRoot, "FlightSessionPage"));
            var controls = new ControlsPage(this, Keeper.Find(contentRoot, "ControlsPage"));
            var general = new GeneralPage(this, Keeper.Find(contentRoot, "SettingsPage"));
            var display = new DisplayPage(this, Keeper.Find(contentRoot, "DisplayPage"));
            var cesium = new CesiumPage(this, Keeper.Find(contentRoot, "CesiumPage"));
            CaptureShell(r);

            keeper.Detach();
            DestroyLeftovers(r);

            BuildShell();
            flight.Build();
            controls.Build();
            general.Build();
            display.Build();
            cesium.Build();
            BuildDialogs();
            BuildNovaPopup();
            keeper.Finish();
        }

        private void CaptureShell(Transform r)
        {
            overlay = keeper.Keep(r, "Overlay");
            scrim = keeper.Keep(r, "Scrim");
            rail = keeper.Keep(r, "Overlay/NavigationRail");
            pageNavigation = keeper.Keep(r, "Overlay/NavigationRail/PageNavigation");
            resume = keeper.Keep<Button>(r, "Overlay/NavigationRail/ResumeButton");
            returnToMenu = keeper.Keep<Button>(r, "Overlay/NavigationRail/ReturnToMenuButton");
            foreach ((string name, string label, string icon) in new[]
                     {
                         ("FlightSessionButton", "Flight & session", "gauge"), ("ControlsButton", "Controls", "stick"),
                         ("SettingsButton", "Settings", "sliders"), ("CesiumButton", "Cesium", "globe"), ("DisplayButton", "Display", "display"),
                     })
            {
                navItems.Add((keeper.Keep<Button>(r, "Overlay/NavigationRail/PageNavigation/" + name), label, icon));
            }

            contentPanel = keeper.Keep(r, "Overlay/ContentPanel");
            pageViewport = keeper.Keep(r, "Overlay/ContentPanel/PageViewport");
            viewport = keeper.Keep(r, "Overlay/ContentPanel/PageViewport/Viewport");
            content = keeper.Keep(r, ContentPath);

            unsavedDialog = keeper.Keep(r, "Overlay/UnsavedChangesDialog");
            unsavedTitle = keeper.Keep<TMP_Text>(unsavedDialog, "DialogPanel/Title");
            unsavedBody = keeper.Keep<TMP_Text>(unsavedDialog, "DialogPanel/Description");
            unsavedApply = keeper.Keep<Button>(unsavedDialog, "DialogPanel/ApplyButton");
            unsavedDiscard = keeper.Keep<Button>(unsavedDialog, "DialogPanel/DiscardButton");
            unsavedCancel = keeper.Keep<Button>(unsavedDialog, "DialogPanel/CancelButton");

            bindingDialog = keeper.Keep(r, "Overlay/ContentPanel/BindingDialog");
            bindingTitle = keeper.Keep<TMP_Text>(bindingDialog, "DialogPanel/DialogTitle");
            bindingCurrent = keeper.Keep<TMP_Text>(bindingDialog, "DialogPanel/DialogBinding");
            bindingStatus = keeper.Keep<TMP_Text>(bindingDialog, "DialogPanel/DialogStatus");
            bindingCancel = keeper.Keep<Button>(bindingDialog, "DialogPanel/DialogActions/DialogCancel");
            bindingClear = keeper.Keep<Button>(bindingDialog, "DialogPanel/DialogActions/DialogClear");
            bindingRestore = keeper.Keep<Button>(bindingDialog, "DialogPanel/DialogActions/DialogRestore");

            novaPopup = keeper.Keep(r, "UnlockCollectivePopup");
            novaButton = keeper.Keep<Button>(novaPopup, "Button");
        }

        /// <summary>After kept objects moved out, removes the rest of the old overlay (header, cards, dividers).</summary>
        private void DestroyLeftovers(Transform r)
        {
            // Root children other than kept ones and behaviour hosts (SessionActions, PauseMenuController) stay as they are.
            foreach (Transform kept in new[] { overlay, rail, pageNavigation, contentPanel, pageViewport, viewport, content })
            {
                MenuKit.StripDecoration(kept.gameObject);
            }
        }

        // ------------------------------------------------------------- shell

        private void BuildShell()
        {
            Transform r = root.transform;
            scrim.SetParent(r, false);
            scrim.SetAsLastSibling();
            MenuKit.Stretch((RectTransform)scrim);
            RemoveGraphics(scrim.gameObject);
            kit.Fill(scrim.gameObject, P.scrim, null, true);

            overlay.SetParent(r, false);
            overlay.SetAsLastSibling();
            MenuKit.Stretch((RectTransform)overlay);
            RemoveGraphics(overlay.gameObject);

            RectTransform frame = MenuKit.Rect("Frame", overlay);
            MenuKit.Stretch(frame, 60f, 60f, 60f, 60f);
            kit.Fill(frame.gameObject, P.bg2, kit.Theme.PanelSprite, true);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            RectTransform frameBorder = MenuKit.Rect("FrameBorder", overlay);
            MenuKit.Stretch(frameBorder, 60f, 60f, 60f, 60f);
            kit.Fill(frameBorder.gameObject, P.line2, kit.Theme.PanelBorderSprite);

            BuildRail(frame);
            BuildContentPanel(frame);
        }

        private void BuildRail(Transform frame)
        {
            var railRect = (RectTransform)rail;
            railRect.SetParent(frame, false);
            railRect.anchorMin = Vector2.zero;
            railRect.anchorMax = new Vector2(0f, 1f);
            railRect.pivot = new Vector2(0f, 0.5f);
            railRect.sizeDelta = new Vector2(384f, 0f);
            railRect.anchoredPosition = Vector2.zero;
            RemoveGraphics(rail.gameObject);
            kit.Fill(rail.gameObject, P.bg1);
            MenuKit.Column(rail.gameObject, 24, 24, 42, 30, 36f);
            RectTransform edge = MenuKit.Rect("Edge", rail);
            edge.anchorMin = new Vector2(1f, 0f);
            edge.anchorMax = Vector2.one;
            edge.pivot = new Vector2(1f, 0.5f);
            edge.sizeDelta = new Vector2(MenuKit.Hairline, 0f);
            MenuKit.Layout(edge.gameObject).ignoreLayout = true;
            kit.Fill(edge.gameObject, P.line);

            MenuShell.Brand(kit, rail, "In-flight menu");
            pageNavigation.SetParent(rail, false);
            MenuKit.Layout(pageNavigation.gameObject);
            RemoveGraphics(pageNavigation.gameObject);
            MenuKit.Column(pageNavigation.gameObject, 0, 0, 0, 0, 2f);
            MenuKit.Place(kit.NavItem(resume, "Resume flight", "play", true), pageNavigation);
            MenuKit.Spacer(pageNavigation, 10f);
            foreach ((Button button, string label, string icon) in navItems)
            {
                MenuKit.Place(kit.NavItem(button, label, icon), pageNavigation);
            }

            MenuKit.Spacer(rail, 0f, -1f, 1f);
            RectTransform foot = MenuKit.Rect("RailFoot", rail);
            MenuKit.Column(foot.gameObject, 0, 0, 24, 0, 2f);
            kit.TopDivider(foot);
            MenuKit.Place(kit.NavItem(returnToMenu, "Return to menu", "exit"), foot);
        }

        private void BuildContentPanel(Transform frame)
        {
            var panel = (RectTransform)contentPanel;
            panel.SetParent(frame, false);
            MenuKit.Stretch(panel, 384f);
            RemoveGraphics(contentPanel.gameObject);
            MenuKit.Column(contentPanel.gameObject, 0, 0, 0, 0, 0f);

            RectTransform topbar = MenuKit.Rect("Topbar", contentPanel);
            MenuKit.RowLayout(topbar.gameObject, 60, 60, 24, 24, 24f);
            MenuKit.Layout(topbar.gameObject, -1f, 96f, -1f, -1f, 96f);
            MenuShell.BottomDivider(kit, topbar);
            MenuBreadcrumb crumbs = MenuShell.Breadcrumb(kit, topbar, "In flight", null);
            MenuKit.Spacer(topbar, 0f, 1f);
            RectTransform live = MenuKit.Rect("Live", topbar);
            MenuKit.RowLayout(live.gameObject, 0, 0, 0, 0, 12f);
            kit.Dot(live, "Dot", P.ok);
            TextMeshProUGUI liveText = kit.Text(live, "Text", "Simulation keeps running while this menu is open",
                MenuTextRole.Eyebrow, P.text2, TextAlignmentOptions.MidlineLeft, false, 0f, false);
            liveText.font = kit.Theme.BodyFont;
            liveText.fontSharedMaterial = kit.Theme.BodyFont.material;

            ScrollRect scroll = MenuShell.Scroll(kit, pageViewport, viewport, content, contentPanel);
            actionBars = MenuKit.Rect("ActionBars", contentPanel);
            MenuKit.Column(actionBars.gameObject, 0, 0, 0, 0, 0f);

            var controller = root.GetComponentInChildren<PauseMenuController>(true);
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("pageScrollRect").objectReferenceValue = scroll;
            serialized.FindProperty("breadcrumb").objectReferenceValue = crumbs;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void RemoveGraphics(GameObject go)
        {
            foreach (Graphic graphic in go.GetComponents<Graphic>())
            {
                Object.DestroyImmediate(graphic);
            }
        }

        // ------------------------------------------------------------- shared page parts

        public MenuKit Kit => kit;
        public Keeper Keeper => keeper;
        public Transform ActionBars => actionBars;
        public Transform ContentForPages => content;

        /// <summary>Slider with a numeric field (.range-wrap) and an optional validation message below.</summary>
        public void RangeWithField(Transform container, Slider slider, TMP_InputField field, TMP_Text error, Transform control)
        {
            MenuKit.Layout(container.gameObject);
            MenuKit.Place(container, control, -1f, -1f, 1f, 0f);
            MenuKit.StripDecoration(container.gameObject);
            RemoveGraphics(container.gameObject);
            MenuKit.Column(container.gameObject, 0, 0, 0, 0, 6f, TextAnchor.UpperRight);
            RectTransform wrap = MenuKit.Rect("Range", container);
            MenuKit.RowLayout(wrap.gameObject, 0, 0, 0, 0, 18f, TextAnchor.MiddleRight);
            MenuKit.Place(kit.Slider(slider), wrap, -1f, 36f, 1f, 120f);
            MenuKit.Place(kit.Field(field, true, MenuKit.NumberFieldWidth, TextAlignmentOptions.MidlineRight), wrap,
                MenuKit.NumberFieldWidth, MenuKit.FieldHeight, 0f, MenuKit.NumberFieldWidth);
            if (error != null)
            {
                Message(error, container);
            }
        }

        /// <summary>Validation / feedback line (.msg) in caution.</summary>
        public void Message(TMP_Text message, Transform parent)
        {
            message.transform.SetParent(parent, false);
            kit.StyleText(message, MenuTextRole.Hint, P.caution, TextAlignmentOptions.TopRight);
        }

        public TMP_Text Readout(TMP_Text label, Transform parent, float width = 84f, bool mono = true)
        {
            MenuKit.Place(label, parent, width, -1f, 0f, width);
            kit.StyleText(label, mono ? MenuTextRole.Value : MenuTextRole.Body, P.text1, TextAlignmentOptions.MidlineRight, false, 0f, false);
            if (!mono)
            {
                label.fontSize = kit.Theme.SizeFor(MenuTextRole.Control);
            }

            MenuKit.Layout(label.gameObject, width, -1f, 0f, width);
            return label;
        }

        public void Diagnostic(TMP_Text diagnostic, Transform page)
        {
            MenuKit.Place(diagnostic, page);
            kit.StyleText(diagnostic, MenuTextRole.Hint, P.caution);
        }

        /// <summary>"N options are not available" disclosure (details.na) with its list collapsed.</summary>
        public void Unavailable(Transform page, string summary, params string[] items)
        {
            RectTransform details = MenuKit.Rect("Unavailable", page);
            kit.Fill(details.gameObject, P.bg1, kit.Theme.PanelSprite);
            kit.Border(details, P.line, kit.Theme.PanelBorderSprite);
            MenuKit.Column(details.gameObject, 0, 0, 0, 0, 0f);
            RectTransform toggleRect = MenuKit.Rect("UnavailableDisclosure", details);
            Toggle toggle = toggleRect.gameObject.AddComponent<Toggle>();
            toggle.isOn = false;
            Image hit = kit.Fill(toggle.gameObject, MenuPalette.WithAlpha(P.bg3, 0f), kit.Theme.PanelSprite, true);
            MenuKit.RowLayout(toggle.gameObject, 30, 30, 21, 21, 18f);
            Image chevron = kit.Icon(toggle.transform, "chev", P.text3, 24f);
            TextMeshProUGUI text = kit.Text(toggle.transform, "Label", summary, MenuTextRole.Hint, P.text2);
            MenuKit.Layout(text.gameObject, -1f, -1f, 1f, 0f);
            toggle.targetGraphic = hit;
            toggle.graphic = null;
            toggle.transition = Selectable.Transition.None;

            RectTransform list = MenuKit.Rect("UnavailableList", details);
            MenuKit.Column(list.gameObject, 30, 30, 0, 24, 6f);
            for (int i = 0; i < items.Length; i++)
            {
                TextMeshProUGUI item = kit.Text(list, "Item" + (i + 1), "•  " + items[i], MenuTextRole.Hint, P.text2);
                item.margin = new Vector4(42f, 0f, 0f, 0f);
            }

            list.gameObject.SetActive(false);
            MenuDisclosure disclosure = toggle.gameObject.AddComponent<MenuDisclosure>();
            disclosure.Configure(toggle, list.gameObject, chevron.rectTransform);
            Image ring = kit.FocusRing(toggle.transform, 0f);
            kit.Style(toggle, MenuSelectableVariant.Ghost, hit, ring, new TMP_Text[] { text });
        }

        /// <summary>Pinned action bar (.actionbar) shown only while its page is.</summary>
        public void ActionBar(InFlightMenuPage page, Button apply, Button revert)
        {
            RectTransform bar = MenuKit.Rect(page.name + "ActionBar", actionBars);
            kit.Fill(bar.gameObject, P.bg1);
            MenuKit.RowLayout(bar.gameObject, 60, 60, 21, 21, 24f);
            MenuKit.Layout(bar.gameObject, -1f, 108f, -1f, -1f, 108f);
            kit.TopDivider(bar);
            RectTransform state = MenuKit.Rect("State", bar);
            MenuKit.RowLayout(state.gameObject, 0, 0, 0, 0, 12f);
            Image dot = kit.Dot(state, "StateDot", P.ok);
            TextMeshProUGUI label = kit.Text(state, "SaveState", MenuActionBar.CleanText, MenuTextRole.Hint, P.text2,
                TextAlignmentOptions.MidlineLeft, false, 0f, false);
            MenuKit.Spacer(bar, 0f, 1f);
            RectTransform buttons = MenuKit.Rect("Buttons", bar);
            MenuKit.RowLayout(buttons.gameObject, 0, 0, 0, 0, 12f, TextAnchor.MiddleRight);
            revert.name = "RevertButton";
            apply.name = "ApplyButton";
            MenuKit.Place(kit.StyleButton(revert, "Revert", MenuButtonLook.Ghost), buttons);
            MenuKit.Place(kit.StyleButton(apply, "Apply changes", MenuButtonLook.Primary), buttons);
            CanvasGroup revertGroup = MenuKit.GetOrAdd<CanvasGroup>(revert.gameObject);
            CanvasGroup applyGroup = MenuKit.GetOrAdd<CanvasGroup>(apply.gameObject);
            var actionBar = bar.gameObject.AddComponent<MenuActionBar>();
            actionBar.Configure(kit.Theme, page, label, dot, revertGroup, applyGroup);
            MenuKit.GetOrAdd<MenuPageAttachment>(page.gameObject).Configure(bar.gameObject);
        }

        // ------------------------------------------------------------- dialogs and NOVA

        private void BuildDialogs()
        {
            unsavedDialog.SetParent(overlay, false);
            unsavedDialog.SetAsLastSibling();
            kit.Dialog(unsavedDialog.gameObject, "In flight", "Unsaved changes",
                "Apply your changes, discard them, or keep editing.", out RectTransform _, out RectTransform actions,
                unsavedTitle, unsavedBody);
            MenuKit.Place(kit.StyleButton(unsavedCancel, "Keep editing", MenuButtonLook.Ghost), actions);
            MenuKit.Place(kit.StyleButton(unsavedDiscard, "Discard", MenuButtonLook.Default), actions);
            MenuKit.Place(kit.StyleButton(unsavedApply, "Apply", MenuButtonLook.Primary), actions);

            // Step 4: Return to menu asks first. "Stay in flight" is the safe default and has focus.
            RectTransform returnDialog = MenuKit.Rect("ReturnConfirmationDialog", overlay);
            kit.Dialog(returnDialog.gameObject, "In flight", "Return to the main menu?", "This ends your current session.",
                out RectTransform _, out RectTransform returnActions);
            Button stay = kit.Button(returnActions, "StayInFlight", "Stay in flight", MenuButtonLook.Ghost);
            Button leave = kit.Button(returnActions, "ConfirmReturn", "Return to menu", MenuButtonLook.Danger);
            returnDialog.gameObject.SetActive(false);
            var controller = new SerializedObject(root.GetComponentInChildren<PauseMenuController>(true));
            controller.FindProperty("actionConfirmationRoot").objectReferenceValue = returnDialog.gameObject;
            controller.FindProperty("confirmActionButton").objectReferenceValue = leave;
            controller.FindProperty("cancelActionButton").objectReferenceValue = stay;
            controller.ApplyModifiedPropertiesWithoutUndo();

            bindingDialog.SetParent(overlay, false);
            bindingDialog.SetAsLastSibling();
            BuildBindingDialog(kit, bindingDialog, bindingTitle, bindingCurrent, bindingStatus, bindingRestore, bindingClear, bindingCancel);
        }

        public static void BuildBindingDialog(MenuKit kit, Transform dialog, TMP_Text title, TMP_Text current, TMP_Text status,
            Button restore, Button clear, Button cancel)
        {
            MenuPalette p = kit.P;
            kit.Dialog(dialog.gameObject, "Change binding", "Command", null, out RectTransform extra, out RectTransform actions, title);
            RectTransform currentRow = MenuKit.Rect("CurrentBinding", extra);
            MenuKit.RowLayout(currentRow.gameObject, 0, 0, 6, 0, 12f);
            kit.Text(currentRow, "Label", "Current", MenuTextRole.Hint, p.text2, TextAlignmentOptions.MidlineLeft, false, 0f, false);
            kit.Keycap(currentRow, "Key", current);

            RectTransform listen = MenuKit.Rect("Listen", extra);
            // Linear colour space strengthens alpha blends; 0.07 reads like the preview's 12 % tint.
            kit.Fill(listen.gameObject, MenuPalette.WithAlpha(p.accent, 0.07f), kit.Theme.ControlSprite);
            kit.Border(listen, MenuPalette.WithAlpha(p.accent, 0.5f));
            MenuKit.RowLayout(listen.gameObject, 30, 30, 30, 30, 18f, TextAnchor.MiddleCenter);
            kit.Dot(listen, "Pulse", p.accent, 15f);
            status.transform.SetParent(listen, false);
            kit.StyleText(status, MenuTextRole.Body, p.text1, TextAlignmentOptions.Midline);
            MenuKit.Layout(status.gameObject, -1f, -1f, 1f, 0f);
            kit.Text(extra, "EscapeNote", "Esc cancels. It’s reserved for menu recovery and can’t be bound.",
                MenuTextRole.Hint, p.text2, TextAlignmentOptions.Top);

            MenuKit.Place(kit.StyleButton(restore, "Restore default", MenuButtonLook.Ghost), actions);
            MenuKit.Place(kit.StyleButton(clear, "Clear", MenuButtonLook.Ghost), actions);
            MenuKit.Place(kit.StyleButton(cancel, "Cancel", MenuButtonLook.Default), actions);
            dialog.gameObject.SetActive(false);
        }

        private void BuildNovaPopup()
        {
            novaPopup.SetParent(root.transform, false);
            novaPopup.SetAsLastSibling();
            MenuKit.StripDecoration(novaPopup.gameObject);
            MenuKit.Stretch((RectTransform)novaPopup);
            RemoveGraphics(novaPopup.gameObject);
            kit.Fill(novaPopup.gameObject, P.scrim, null, true);
            RectTransform card = MenuKit.Rect("Card", novaPopup);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(624f, 360f);
            kit.Fill(card.gameObject, P.bg2, kit.Theme.PanelSprite, true);
            kit.Border(card, MenuPalette.WithAlpha(P.caution, 0.45f), kit.Theme.PanelBorderSprite);
            MenuKit.Column(card.gameObject, 42, 42, 42, 42, 12f, TextAnchor.UpperCenter);
            MenuKit.FitVertically(card.gameObject);
            kit.Text(card, "Eyebrow", "Collective locked", MenuTextRole.Small, P.caution, TextAlignmentOptions.Top, true, 16f);
            kit.Text(card, "Title", "Enable the helicopter", MenuTextRole.Heading, P.text1, TextAlignmentOptions.Top);
            TextMeshProUGUI body = kit.Text(card, "Body",
                "Make sure NOVA is running before you continue. The collective stays locked until you enable it.",
                MenuTextRole.Body, P.text2, TextAlignmentOptions.Top);
            body.fontSize = kit.Theme.SizeFor(MenuTextRole.Control);
            MenuKit.Spacer(card, 6f);
            MenuKit.Place(kit.StyleButton(novaButton, "Enable helicopter", MenuButtonLook.Primary, null, MenuButtonSize.Large, true),
                card, -1f, 78f, 1f);
        }
    }

    // ----------------------------------------------------------------- pages

    private abstract class PageBuild
    {
        protected readonly InFlightMenuBuild Owner;
        protected readonly Transform Page;
        protected readonly MenuKit Kit;
        protected readonly Keeper Keep;

        protected PageBuild(InFlightMenuBuild owner, Transform page)
        {
            Owner = owner;
            Page = page;
            Kit = owner.Kit;
            Keep = owner.Keeper;
            Keep.Keep(page.parent, page.name);
        }

        protected MenuPalette P => Kit.P;

        public abstract void Build();

        protected RectTransform Rows(string sectionName, string heading, Transform existing = null)
        {
            return Kit.Rows(Kit.Section(Page, sectionName, heading, existing != null ? existing.gameObject : null));
        }

        protected void SwitchRow(Transform rows, Toggle toggle, string label, string hint, TMP_Text hintText = null)
        {
            Kit.Row(rows, toggle.name + "Row", label, hint, out RectTransform control, MenuKit.ControlColumn, null, hintText);
            MenuKit.Place(Kit.Switch(toggle), control);
        }

        protected void DropdownRow(Transform rows, string rowName, TMP_Dropdown dropdown, string label, string hint = null)
        {
            Kit.Row(rows, rowName, label, hint, out RectTransform control);
            MenuKit.Place(Kit.Dropdown(dropdown), control, -1f, MenuKit.FieldHeight, 1f, 120f);
        }

        protected void ButtonRow(Transform rows, string rowName, Button button, string label, string hint, string buttonText,
            string icon, MenuButtonLook look = MenuButtonLook.Default)
        {
            Kit.Row(rows, rowName, label, hint, out RectTransform control, 0f);
            MenuKit.Place(Kit.StyleButton(button, buttonText, look, icon), control);
        }

        protected void SliderRow(Transform rows, string rowName, Slider slider, string label, string hint, TMP_Text readout,
            float readoutWidth = 84f, bool monoReadout = true, Transform existingRow = null)
        {
            Kit.Row(rows, rowName, label, hint, out RectTransform control, MenuKit.ControlColumn,
                existingRow != null ? existingRow.gameObject : null);
            MenuKit.Place(Kit.Slider(slider), control, -1f, 36f, 1f, 120f);
            if (readout != null)
            {
                Owner.Readout(readout, control, readoutWidth, monoReadout);
            }
        }
    }

    private sealed class FlightPage : PageBuild
    {
        private readonly Transform aircraft, assists, advancedSettings, timeTrial, timeControls, world, journey, vr;
        private readonly Button startEngine, resetHelicopter, remove30, play, pause, add30, resetScene, resetVr;
        private readonly Slider assistPresets, subtitleScale;
        private readonly TMP_Text presetName, clock, subtitleScaleLabel, subtitleAvailability;
        private readonly Toggle advancedToggle, autoTrim, offline, subtitles;
        private readonly TMP_InputField journeyFile;
        private readonly List<(Transform row, Slider slider, string label)> advancedSliders = new List<(Transform, Slider, string)>();

        public FlightPage(InFlightMenuBuild owner, Transform page) : base(owner, page)
        {
            aircraft = Keep.Keep(page, "Aircraft");
            startEngine = Keep.Keep<Button>(aircraft, "StartEngine");
            resetHelicopter = Keep.Keep<Button>(aircraft, "ResetHelicopter");
            assists = Keep.Keep(page, "Assists Section");
            assistPresets = Keep.Keep<Slider>(assists, "Assist Presets/Slider");
            presetName = Keep.Keep<TMP_Text>(assists, "Assist Presets/Current Preset Label");
            advancedToggle = Keep.Keep<Toggle>(assists, "Advanced/Toggle");
            advancedSettings = Keep.Keep(assists, "Advanced Control Settings");
            autoTrim = Keep.Keep<Toggle>(advancedSettings, "ToggleAutoTrim");
            foreach ((string name, string label) in new[]
                     {
                         ("TailRotorTorque", "Tail rotor torque"), ("MainRotorTorque", "Main rotor torque"),
                         ("RollAssists", "Roll rate limiter"), ("StabiliztionAssists", "Stabilization assist"),
                     })
            {
                Transform row = Keep.Keep(advancedSettings, name);
                advancedSliders.Add((row, Keep.Keep<Slider>(row, "Slider"), label));
            }

            timeTrial = Keep.Keep(page, "Time Trial");
            timeControls = Keep.Keep(timeTrial, "TimeControls");
            remove30 = Keep.Keep<Button>(timeControls, "Remove30sButton");
            play = Keep.Keep<Button>(timeControls, "Play Button");
            pause = Keep.Keep<Button>(timeControls, "Pause Button");
            add30 = Keep.Keep<Button>(timeControls, "Add30sButton");
            clock = Keep.Keep<TMP_Text>(timeTrial, "Time Remaining/Text");
            world = Keep.Keep(page, "World & Map");
            offline = Keep.Keep<Toggle>(world, "OfflineOnly");
            journey = Keep.Keep(page, "Journey");
            journeyFile = Keep.Keep<TMP_InputField>(journey, "FileLocation/Input Label");
            resetScene = Keep.Keep<Button>(journey, "ResetScene");
            subtitles = Keep.Keep<Toggle>(journey, "SubtitlesEnabled");
            subtitleScale = Keep.Keep<Slider>(journey, "SubtitleScale");
            subtitleScaleLabel = Keep.Keep<TMP_Text>(journey, "SubtitleScaleLabel");
            subtitleAvailability = Keep.Keep<TMP_Text>(journey, "SubtitleAvailability");
            vr = Keep.Keep(page, "VR");
            resetVr = Keep.Keep<Button>(vr, "ResetVrView");
        }

        public override void Build()
        {
            Page.SetParent(Owner.ContentForPages, false);
            Kit.Page(Page.gameObject);
            Kit.PageHead(Page, "Flight & session", "Aircraft actions, assists and session tools. Changes take effect immediately.");

            Transform rows = Rows("Aircraft", "Aircraft", aircraft);
            ButtonRow(rows, "EngineRow", startEngine, "Engine", "Start before lifting the collective.", "Start engine", "engine", MenuButtonLook.Primary);
            ButtonRow(rows, "ResetHelicopterRow", resetHelicopter, "Reset helicopter", "Returns the aircraft to its starting state.", "Reset", "reset");

            rows = Rows("Assists Section", "Assists", assists);
            SliderRow(rows, "Assist Presets", assistPresets, "Overall assist level", "Sets all assists together.", presetName, 132f, false);
            MenuKit.Place(advancedToggle, rows);
            advancedToggle.name = "AdvancedToggle";
            Kit.Disclosure(advancedToggle, "Advanced control settings", advancedSettings.gameObject);
            Kit.TopDivider(advancedToggle.transform);
            advancedSettings.SetParent(rows, false);
            advancedSettings.SetAsLastSibling();
            MenuKit.StripDecoration(advancedSettings.gameObject);
            InFlightMenuBuildHelpers.RemoveGraphics(advancedSettings.gameObject);
            Kit.Fill(advancedSettings.gameObject, MenuPalette.WithAlpha(Color.black, 0.18f), Kit.Theme.PanelSprite);
            MenuKit.Column(advancedSettings.gameObject, 0, 0, 0, 0, 0f);
            Kit.TopDivider(advancedSettings);
            SwitchRow(advancedSettings, autoTrim, "Auto trim", null);
            foreach ((Transform row, Slider slider, string label) in advancedSliders)
            {
                SliderRow(advancedSettings, row.name, slider, label, null, null, 84f, true, row);
            }

            foreach (Transform nested in advancedSettings)
            {
                Transform text = nested.Find("Text");
                if (text != null)
                {
                    text.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(24, 24, 0, 0);
                }
            }

            advancedSettings.gameObject.SetActive(false);

            rows = Rows("Time Trial", "Time trial", timeTrial);
            Kit.Row(rows, "TimeRemainingRow", "Time remaining", "Counts down while the timer runs.", out RectTransform control, 0f);
            MenuKit.Layout(timeControls.gameObject);
            MenuKit.Place(timeControls, control);
            MenuKit.RowLayout(timeControls.gameObject, 0, 0, 0, 0, 18f, TextAnchor.MiddleRight);
            MenuKit.Place(Kit.StyleButton(remove30, "−30 s", MenuButtonLook.Default, null, MenuButtonSize.Small), timeControls);
            MenuKit.Place(Kit.StyleButton(play, null, MenuButtonLook.Primary, "play", MenuButtonSize.Icon), timeControls);
            MenuKit.Place(Kit.StyleButton(pause, null, MenuButtonLook.Default, "pause", MenuButtonSize.Icon), timeControls);
            MenuKit.Place(Kit.StyleButton(add30, "+30 s", MenuButtonLook.Default, null, MenuButtonSize.Small), timeControls);
            MenuKit.Place(clock, timeControls, 132f, -1f, 0f, 132f);
            Kit.StyleText(clock, MenuTextRole.Clock, P.text1, TextAlignmentOptions.MidlineRight, false, 0f, false);
            MenuKit.Layout(clock.gameObject, 132f, -1f, 0f, 132f);

            rows = Rows("World & Map", "World & map", world);
            SwitchRow(rows, offline, "Offline only", "Use cached map data without streaming.");

            rows = Rows("Journey", "Journey", journey);
            Kit.Row(rows, "JourneyFileRow", "Journey file", "CSV of waypoints for this session.", out control);
            MenuKit.Place(Kit.Field(journeyFile, true), control, -1f, MenuKit.FieldHeight, 1f, 96f);
            ButtonRow(rows, "ResetSceneRow", resetScene, "Reset scene", "Reloads the journey and world state.", "Reset scene", "reset");
            SwitchRow(rows, subtitles, "Subtitles", null, subtitleAvailability);
            SliderRow(rows, "SubtitleScaleRow", subtitleScale, "Subtitle scale", null, subtitleScaleLabel);
            // Edit-time text in the runtime formats (FlightSessionPage overwrites both when shown).
            subtitleAvailability.text = "Unavailable: no subtitle source is connected.";
            subtitleScaleLabel.text = "100%";

            rows = Rows("VR", "VR", vr);
            ButtonRow(rows, "ResetVrRow", resetVr, "Reset VR origin", "Recenters the seated view on your current head position.", "Reset VR origin", "target");
        }
    }

    private sealed class ControlsPage : PageBuild
    {
        private readonly Transform tabs, bindingRows, template;
        private readonly Button keyboardTab, joystickTab, reset, rebind, clear, resetRow;
        private readonly TMP_Text hardwareSummary, diagnostic, command, binding, primaryBinding, feedback, heading;
        private readonly Toggle yawMode;

        public ControlsPage(InFlightMenuBuild owner, Transform page) : base(owner, page)
        {
            tabs = Keep.Keep(page, "DeviceTabs");
            keyboardTab = Keep.Keep<Button>(tabs, "KeyboardTab");
            joystickTab = Keep.Keep<Button>(tabs, "JoystickTab");
            hardwareSummary = Keep.Keep<TMP_Text>(page, "HardwareSummary");
            diagnostic = Keep.Keep<TMP_Text>(page, "BindingDiagnostic");
            bindingRows = Keep.Keep(page, "BindingRows");
            template = Keep.Keep(bindingRows, "BindingRowTemplate");
            command = Keep.Keep<TMP_Text>(template, "Command");
            binding = Keep.Keep<TMP_Text>(template, "Binding");
            primaryBinding = Keep.Keep<TMP_Text>(template, "PrimaryBinding");
            feedback = Keep.Keep<TMP_Text>(template, "InlineFeedback");
            rebind = Keep.Keep<Button>(template, "Actions/Rebind");
            clear = Keep.Keep<Button>(template, "Actions/Clear");
            resetRow = Keep.Keep<Button>(template, "Actions/Reset");
            heading = Keep.Keep<TMP_Text>(bindingRows, "CategoryHeadingTemplate");
            yawMode = Keep.Keep<Toggle>(bindingRows, "YawModeToggle");
            reset = Keep.Keep<Button>(page, "ResetProfile");
        }

        public override void Build()
        {
            Page.SetParent(Owner.ContentForPages, false);
            Kit.Page(Page.gameObject);
            Kit.PageHead(Page, "Controls", "Rebind any command. Keyboard bindings always remain as a fallback.");

            RectTransform head = MenuKit.Rect("ControlsHead", Page);
            MenuKit.RowLayout(head.gameObject, 0, 0, 0, 0, 24f);
            MenuKit.Layout(tabs.gameObject);
            MenuKit.Place(tabs, head);
            InFlightMenuBuildHelpers.RemoveGraphics(tabs.gameObject);
            Kit.Fill(tabs.gameObject, P.bg1, Kit.Theme.ControlSprite);
            Kit.Border(tabs, P.line2);
            MenuKit.RowLayout(tabs.gameObject, 4, 4, 4, 4, 2f, TextAnchor.MiddleCenter);
            MenuKit.Layout(tabs.gameObject, -1f, 56f, 0f, -1f, 56f);
            MenuKit.Place(Kit.Tab(keyboardTab, "Keyboard & mouse"), tabs);
            MenuKit.Place(Kit.Tab(joystickTab, "Joystick"), tabs);
            MenuKit.Place(hardwareSummary, head, -1f, -1f, 1f, 0f);
            Kit.StyleText(hardwareSummary, MenuTextRole.Hint, P.text2, TextAlignmentOptions.MidlineRight);
            MenuKit.Layout(hardwareSummary.gameObject, -1f, -1f, 1f, 0f);
            Owner.Diagnostic(diagnostic, Page);

            MenuKit.Layout(bindingRows.gameObject);
            MenuKit.Place(bindingRows, Page);
            MenuKit.StripDecoration(bindingRows.gameObject);
            InFlightMenuBuildHelpers.RemoveGraphics(bindingRows.gameObject);
            MenuKit.Column(bindingRows.gameObject, 0, 0, 0, 0, 0f);
            BuildTemplate();
            MenuKit.Place(heading, bindingRows);
            Kit.StyleText(heading, MenuTextRole.Eyebrow, P.text3, TextAlignmentOptions.BottomLeft, true, 14f, false);
            heading.margin = new Vector4(0f, 0f, 0f, 15f);
            MenuKit.Layout(heading.gameObject, -1f, 72f, -1f, -1f, 72f);
            heading.gameObject.SetActive(false);

            MenuKit.DestroyChildren(yawMode.transform);
            MenuKit.StripDecoration(yawMode.gameObject);
            Kit.Row(bindingRows, "YawModeToggle", "Pedal yaw mode", "Buttons uses two keys or hat directions. Axis uses rudder pedals.",
                out RectTransform control, 0f, yawMode.gameObject);
            Kit.Fill(yawMode.gameObject, MenuPalette.WithAlpha(P.bg1, 0.5f), null, true);
            Kit.Switch(yawMode, "Buttons", "Axis", true, control);

            RectTransform foot = MenuKit.Rect("ResetRow", Page);
            MenuKit.RowLayout(foot.gameObject, 0, 0, 0, 0, 24f);
            TextMeshProUGUI note = Kit.Text(foot, "Summary", "Changes save immediately.", MenuTextRole.Hint, P.text2,
                TextAlignmentOptions.MidlineLeft);
            MenuKit.Layout(note.gameObject, -1f, -1f, 1f, 0f);
            MenuKit.Place(Kit.StyleButton(reset, "Reset all bindings", MenuButtonLook.Danger), foot);
            SetRequired(Page.GetComponent<ControlsRebindingPage>(), "changeSummaryLabel", note);
        }

        private static void SetRequired(Component owner, string property, Object value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty field = serialized.FindProperty(property) ??
                throw new InvalidOperationException(owner.GetType().Name + " has no serialized field " + property);
            field.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void BuildTemplate()
        {
            var row = template.GetComponent<Button>();
            MenuKit.Place(row, bindingRows);
            row.transition = Selectable.Transition.None;
            MenuKit.StripDecoration(row.gameObject);
            MenuKit.DestroyChildren(row.transform);
            Image background = Kit.Fill(row.gameObject, P.bg2, null, true);
            row.targetGraphic = background;
            Kit.TopDivider(row.transform);
            MenuKit.RowLayout(row.gameObject, 30, 30, 7, 7, 24f);
            MenuKit.Layout(row.gameObject, -1f, 56f, -1f, -1f, 56f);
            MenuKit.Place(command, row.transform, 336f, -1f, 0f, 216f);
            Kit.StyleText(command, MenuTextRole.Label, P.text1, TextAlignmentOptions.MidlineLeft, false, 0f, false);
            MenuKit.Layout(command.gameObject, 336f, -1f, 0f, 216f);
            // Step 4: shown by ControlsBindingRow when the binding differs from its default (preview .changed).
            RectTransform badge = MenuKit.Rect("ChangedBadge", row.transform);
            Kit.Border(badge, MenuPalette.WithAlpha(P.accent, 0.4f));
            MenuKit.RowLayout(badge.gameObject, 9, 9, 0, 0, 0f, TextAnchor.MiddleCenter);
            MenuKit.Layout(badge.gameObject, -1f, 30f, 0f, -1f, 30f);
            Kit.Text(badge, "Label", "Changed", MenuTextRole.Micro, P.accent, TextAlignmentOptions.Center, true, 10f, false);
            badge.gameObject.SetActive(false);
            SetRequired(row.GetComponent<ControlsBindingRow>(), "changedBadge", badge.gameObject);
            RectTransform keys = MenuKit.Rect("Keys", row.transform);
            MenuKit.RowLayout(keys.gameObject, 0, 0, 0, 0, 6f);
            MenuKit.Layout(keys.gameObject, -1f, -1f, 1f, 0f);
            Kit.Keycap(keys, "BindingKey", binding);
            primaryBinding.transform.SetParent(keys, false);
            Kit.StyleText(primaryBinding, MenuTextRole.Hint, P.text3, TextAlignmentOptions.MidlineLeft, false, 0f, false);
            feedback.transform.SetParent(row.transform, false);
            Kit.StyleText(feedback, MenuTextRole.Hint, P.caution, TextAlignmentOptions.MidlineLeft, false, 0f, false);
            RectTransform actions = MenuKit.Rect("Actions", row.transform);
            MenuKit.RowLayout(actions.gameObject, 0, 0, 0, 0, 6f, TextAnchor.MiddleRight);
            foreach ((Button button, string label, string icon) in new[] { (rebind, "Rebind", (string)null), (clear, "Clear", null), (resetRow, "Default", "reset") })
            {
                Kit.StyleButton(button, label, MenuButtonLook.Ghost, icon, MenuButtonSize.Small);
                MenuKit.Place(button, actions, -1f, 42f, 0f);
                MenuKit.Layout(button.gameObject, -1f, 42f, 0f, -1f, 42f);
            }

            Image ring = Kit.FocusRing(row.transform, 0f);
            Kit.Style(row, MenuSelectableVariant.Row, background, ring, new[] { command });
            row.gameObject.SetActive(false);
        }
    }

    private sealed class GeneralPage : PageBuild
    {
        private readonly Transform volume;
        private readonly Slider volumeSlider;
        private readonly TMP_InputField volumeValue;
        private readonly Toggle nova;
        private readonly TMP_Text status, diagnostic;
        private readonly Button apply, revert;

        public GeneralPage(InFlightMenuBuild owner, Transform page) : base(owner, page)
        {
            volume = Keep.Keep(page, "Volume");
            volumeSlider = Keep.Keep<Slider>(volume, "Slider");
            volumeValue = Keep.Keep<TMP_InputField>(volume, "Value");
            nova = Keep.Keep<Toggle>(page, "EnableNovaPopup");
            status = Keep.Keep<TMP_Text>(page, "HardwareStatus");
            diagnostic = Keep.Keep<TMP_Text>(page, "Diagnostic");
            apply = Keep.Keep<Button>(page, "ActionBar/ApplyButton");
            revert = Keep.Keep<Button>(page, "ActionBar/RevertButton");
        }

        public override void Build()
        {
            Page.SetParent(Owner.ContentForPages, false);
            Kit.Page(Page.gameObject);
            Kit.PageHead(Page, "Settings", "Master volume and simulator hardware preferences.");
            Transform rows = Rows("Audio", "Audio");
            Kit.Row(rows, "VolumeRow", "Master volume", "Range 0 to 200.", out RectTransform control);
            Owner.RangeWithField(volume, volumeSlider, volumeValue, null, control);
            Transform hardware = Kit.Section(Page, "Hardware", "Hardware");
            rows = Kit.Rows(hardware);
            SwitchRow(rows, nova, "NOVA popup and collective lock", "Holds the collective at flight start until the pilot enables the helicopter.");
            Kit.Note(hardware, "HardwareStatusNote", null, "info", status);
            Owner.Diagnostic(diagnostic, Page);
            Owner.Unavailable(Page, "5 options are not available in this build",
                new[]
                {
                    "Engine, environment, UI and radio/intercom volume, and mute: no separate audio buses exist yet.",
                    "VR render scale, refresh rate, seated offset, vignette and spectator display: no runtime owner yet. Recenter is in Flight & session.",
                    "Control sensitivity, response curves and dead zones: no safe adapter yet. Assist presets are in Flight & session.",
                    "Units, hints, confirmation prompts and autosave: no runtime owner yet.",
                    "High contrast and other visual modes: no runtime owner yet. UI scale is under Display.",
                });
            Owner.ActionBar(Page.GetComponent<InFlightMenuPage>(), apply, revert);
        }
    }

    private sealed class DisplayPage : PageBuild
    {
        private readonly TMP_Dropdown windowMode, resolution, quality;
        private readonly Toggle vSync;
        private readonly TMP_InputField frameCap;
        private readonly Slider uiScale;
        private readonly TMP_Text uiScaleValue, diagnostic;
        private readonly Button apply, revert;

        public DisplayPage(InFlightMenuBuild owner, Transform page) : base(owner, page)
        {
            windowMode = Keep.Keep<TMP_Dropdown>(page, "WindowModeSetting/WindowMode");
            resolution = Keep.Keep<TMP_Dropdown>(page, "ResolutionSetting/Resolution");
            quality = Keep.Keep<TMP_Dropdown>(page, "QualityPresetSetting/QualityPreset");
            vSync = Keep.Keep<Toggle>(page, "VSync");
            frameCap = Keep.Keep<TMP_InputField>(page, "FrameRateSetting/FrameRateCap");
            uiScale = Keep.Keep<Slider>(page, "UiScaleSetting/UiScale");
            uiScaleValue = Keep.Keep<TMP_Text>(page, "UiScaleSetting/UiScaleValue");
            diagnostic = Keep.Keep<TMP_Text>(page, "Diagnostic");
            apply = Keep.Keep<Button>(page, "ActionBar/ApplyButton");
            revert = Keep.Keep<Button>(page, "ActionBar/RevertButton");
        }

        public override void Build()
        {
            Page.SetParent(Owner.ContentForPages, false);
            Kit.Page(Page.gameObject);
            Kit.PageHead(Page, "Display & graphics", "Screen mode, rendering quality and interface scale.");
            Transform rows = Rows("Screen", "Screen");
            DropdownRow(rows, "WindowModeSetting", windowMode, "Window mode");
            DropdownRow(rows, "ResolutionSetting", resolution, "Resolution");
            SwitchRow(rows, vSync, "VSync", "Syncs frames to the display refresh rate.");
            Kit.Row(rows, "FrameRateSetting", "Frame-rate cap", "−1 for no cap, or 30 to 240.", out RectTransform control);
            MenuKit.Place(Kit.Field(frameCap, true, MenuKit.NumberFieldWidth, TextAlignmentOptions.MidlineRight), control,
                MenuKit.NumberFieldWidth, MenuKit.FieldHeight, 0f, MenuKit.NumberFieldWidth);
            Kit.Text(control, "Unit", "fps", MenuTextRole.Hint, P.text2, TextAlignmentOptions.MidlineLeft, false, 0f, false);
            rows = Rows("Quality", "Quality");
            DropdownRow(rows, "QualityPresetSetting", quality, "Quality preset");
            rows = Rows("Interface", "Interface");
            SliderRow(rows, "UiScaleSetting", uiScale, "UI scale", "Applies to menus and data panels.", uiScaleValue);
            uiScaleValue.text = Mathf.RoundToInt(uiScale.value * 100f) + "%";
            Owner.Diagnostic(diagnostic, Page);
            Owner.Unavailable(Page,
                "3 options are not available in this build. These need a safe runtime owner before they can be offered.",
                new[] { "Brightness", "Colour-blind mode", "Camera field of view" });
            Owner.ActionBar(Page.GetComponent<InFlightMenuPage>(), apply, revert);
        }
    }

    private sealed class CesiumPage : PageBuild
    {
        private readonly List<(Transform container, Slider slider, TMP_InputField field, TMP_Text error)> ranges =
            new List<(Transform, Slider, TMP_InputField, TMP_Text)>();
        private readonly Dictionary<string, Toggle> toggles = new Dictionary<string, Toggle>();
        private readonly Transform cache;
        private readonly TMP_InputField cacheField;
        private readonly TMP_Text cacheError, diagnostic;
        private readonly Button apply, revert;

        public CesiumPage(InFlightMenuBuild owner, Transform page) : base(owner, page)
        {
            foreach (string name in new[] { "ScreenSpaceError", "MaxSimultaneousTileLoads", "LoadingDescendantLimit", "CulledScreenSpaceError" })
            {
                Transform container = Keep.Keep(page, name);
                ranges.Add((container, Keep.Keep<Slider>(container, "Slider"), Keep.Keep<TMP_InputField>(container, "Value"),
                    Keep.Keep<TMP_Text>(container, "ValidationError")));
            }

            foreach (string name in new[] { "PreloadAncestors", "PreloadSiblings", "ForbidHoles", "EnableFrustumCulling", "EnableFogCulling", "EnableForceScreenSpaceError" })
            {
                toggles[name] = Keep.Keep<Toggle>(page, name);
            }

            cache = Keep.Keep(page, "MaxCachedBytes");
            cacheField = Keep.Keep<TMP_InputField>(cache, "Input Label");
            cacheError = Keep.Keep<TMP_Text>(cache, "ValidationError");
            diagnostic = Keep.Keep<TMP_Text>(page, "Diagnostic");
            apply = Keep.Keep<Button>(page, "ActionBar/ApplyButton");
            revert = Keep.Keep<Button>(page, "ActionBar/RevertButton");
        }

        private void Range(Transform rows, int index, string label, string hint = null)
        {
            (Transform container, Slider slider, TMP_InputField field, TMP_Text error) = ranges[index];
            Kit.Row(rows, container.name + "Row", label, hint, out RectTransform control);
            Owner.RangeWithField(container, slider, field, error, control);
        }

        public override void Build()
        {
            Page.SetParent(Owner.ContentForPages, false);
            Kit.Page(Page.gameObject);
            Kit.PageHead(Page, "World streaming",
                "Cesium 3D Tiles streaming and culling. Lower screen-space error looks sharper and loads more tiles.");
            Transform rows = Rows("Streaming", "Streaming");
            Range(rows, 0, "Maximum screen-space error");
            SwitchRow(rows, toggles["PreloadAncestors"], "Preload ancestors", null);
            SwitchRow(rows, toggles["PreloadSiblings"], "Preload siblings", null);
            SwitchRow(rows, toggles["ForbidHoles"], "Forbid holes", "Waits for all child tiles before refining.");
            Range(rows, 1, "Maximum simultaneous tile loads");
            Range(rows, 2, "Loading descendant limit");
            Kit.Row(rows, "MaxCachedBytesRow", "Maximum cached bytes", "Whole number of bytes, 0 or more.", out RectTransform control);
            MenuKit.Layout(cache.gameObject);
            MenuKit.Place(cache, control, -1f, -1f, 1f, 0f);
            MenuKit.StripDecoration(cache.gameObject);
            InFlightMenuBuildHelpers.RemoveGraphics(cache.gameObject);
            MenuKit.Column(cache.gameObject, 0, 0, 0, 0, 6f, TextAnchor.UpperRight, false);
            MenuKit.Place(Kit.Field(cacheField, true, 228f, TextAlignmentOptions.MidlineRight), cache, 228f, MenuKit.FieldHeight, 0f, 228f);
            Owner.Message(cacheError, cache);

            rows = Rows("Culling", "Culling");
            SwitchRow(rows, toggles["EnableFrustumCulling"], "Frustum culling", null);
            SwitchRow(rows, toggles["EnableFogCulling"], "Fog culling", null);
            SwitchRow(rows, toggles["EnableForceScreenSpaceError"], "Force culled screen-space error", null);
            Range(rows, 3, "Culled screen-space error");
            Owner.Diagnostic(diagnostic, Page);
            Owner.ActionBar(Page.GetComponent<InFlightMenuPage>(), apply, revert);
        }
    }

    private static class InFlightMenuBuildHelpers
    {
        public static void RemoveGraphics(GameObject go)
        {
            foreach (Graphic graphic in go.GetComponents<Graphic>())
            {
                Object.DestroyImmediate(graphic);
            }
        }
    }
}
