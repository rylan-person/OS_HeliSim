using System;
using System.Collections.Generic;
using HeliSim.InFlightMenu;
using HeliSim.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Button looks from the redesign preview (.btn, .btn.primary, .btn.ghost, .btn.danger).</summary>
public enum MenuButtonLook
{
    Default,
    Primary,
    Ghost,
    Danger,
}

/// <summary>Button heights from the preview (.btn.sm, .btn, .btn.lg, square icon button).</summary>
public enum MenuButtonSize
{
    Small,
    Normal,
    Large,
    Icon,
}

/// <summary>
/// Editor-only component kit that builds the redesigned menus (plan 2026-11-17) from the HTML
/// reference, docs/design/menu-redesign-preview.html. Every size is 1.5 x the preview's CSS pixel
/// value, matching the 1920 x 1080 Canvas Scaler reference. Interactive objects passed in are
/// restyled in place so their serialized references and persistent listeners survive; every
/// visual child is rebuilt.
/// </summary>
public sealed class MenuKit
{
    public const float RowMinHeight = 80f;      // user-approved setting row density
    public const float ControlColumn = 480f;    // .row second column, 20rem
    public const float PageWidth = 1296f;       // .page max-width, 54rem
    public const float FieldHeight = 60f;       // --h-ctl
    public const float Hairline = 1.5f;         // 1 px CSS border
    public const float NumberFieldWidth = 144f; // .field.num, 6rem

    private readonly MenuTheme theme;

    public MenuKit(MenuTheme theme)
    {
        this.theme = theme ?? throw new ArgumentNullException(nameof(theme));
        if (theme.ControlBorderSprite == null || theme.CircleSprite == null || theme.KeycapSprite == null)
        {
            throw new InvalidOperationException("Run HeliSim/Create Menu Theme Assets first: the theme is missing kit sprites.");
        }
    }

    public MenuTheme Theme => theme;
    public MenuPalette P => theme.Palette;

    // ------------------------------------------------------------------ primitives

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }

        return (RectTransform)go.transform;
    }

    /// <summary>Stretches to the parent with the given insets (left, bottom, right, top).</summary>
    public static RectTransform Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    public static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T existing = go.GetComponent<T>();
        return existing != null ? existing : go.AddComponent<T>();
    }

    public static LayoutElement Layout(GameObject go, float preferredWidth = -1f, float preferredHeight = -1f,
        float flexibleWidth = -1f, float minWidth = -1f, float minHeight = -1f, float flexibleHeight = -1f)
    {
        LayoutElement element = GetOrAdd<LayoutElement>(go);
        element.ignoreLayout = false;
        element.preferredWidth = preferredWidth;
        element.preferredHeight = preferredHeight;
        element.flexibleWidth = flexibleWidth;
        element.flexibleHeight = flexibleHeight;
        element.minWidth = minWidth;
        element.minHeight = minHeight;
        return element;
    }

    public static VerticalLayoutGroup Column(GameObject go, int left, int right, int top, int bottom, float spacing,
        TextAnchor alignment = TextAnchor.UpperLeft, bool expandWidth = true)
    {
        RemoveLayoutGroups(go, typeof(VerticalLayoutGroup));
        VerticalLayoutGroup group = GetOrAdd<VerticalLayoutGroup>(go);
        group.padding = new RectOffset(left, right, top, bottom);
        group.spacing = spacing;
        group.childAlignment = alignment;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = expandWidth;
        group.childForceExpandHeight = false;
        group.childScaleWidth = false;
        group.childScaleHeight = false;
        return group;
    }

    public static HorizontalLayoutGroup RowLayout(GameObject go, int left, int right, int top, int bottom, float spacing,
        TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        RemoveLayoutGroups(go, typeof(HorizontalLayoutGroup));
        HorizontalLayoutGroup group = GetOrAdd<HorizontalLayoutGroup>(go);
        group.padding = new RectOffset(left, right, top, bottom);
        group.spacing = spacing;
        group.childAlignment = alignment;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;
        group.childScaleWidth = false;
        group.childScaleHeight = false;
        return group;
    }

    public static void FitVertically(GameObject go)
    {
        ContentSizeFitter fitter = GetOrAdd<ContentSizeFitter>(go);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    public static void RemoveLayoutGroups(GameObject go, Type keep = null)
    {
        foreach (LayoutGroup group in go.GetComponents<LayoutGroup>())
        {
            if (keep == null || group.GetType() != keep)
            {
                Object.DestroyImmediate(group);
            }
        }
    }

    /// <summary>Removes layout and decoration components left by the previous menu system.</summary>
    public static void StripDecoration(GameObject go)
    {
        foreach (Shadow effect in go.GetComponents<Shadow>())
        {
            Object.DestroyImmediate(effect);
        }

        foreach (ContentSizeFitter fitter in go.GetComponents<ContentSizeFitter>())
        {
            Object.DestroyImmediate(fitter);
        }

        RemoveLayoutGroups(go);
    }

    public static void DestroyChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    public Image Fill(GameObject go, Color color, Sprite sprite = null, bool raycast = false)
    {
        Image image = GetOrAdd<Image>(go);
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.fillCenter = true;
        image.pixelsPerUnitMultiplier = 1f;
        image.preserveAspect = false;
        image.color = color;
        image.raycastTarget = raycast;
        image.enabled = true;
        return image;
    }

    /// <summary>A 1.5 px ring drawn over the parent's fill (rounded like the given ring sprite).</summary>
    public Image Border(Transform parent, Color color, Sprite ring = null)
    {
        RectTransform rect = Rect("Border", parent);
        rect.SetAsFirstSibling();
        Stretch(rect);
        Layout(rect.gameObject).ignoreLayout = true;
        return Fill(rect.gameObject, color, ring != null ? ring : theme.ControlBorderSprite);
    }

    /// <summary>A hairline divider along the top edge of a row.</summary>
    public Image TopDivider(Transform parent)
    {
        RectTransform rect = Rect("Divider", parent);
        rect.SetAsFirstSibling();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, Hairline);
        rect.anchoredPosition = Vector2.zero;
        Layout(rect.gameObject).ignoreLayout = true;
        return Fill(rect.gameObject, P.line);
    }

    public Image Dot(Transform parent, string name, Color color, float size = 9f)
    {
        RectTransform rect = Rect(name, parent);
        rect.sizeDelta = new Vector2(size, size);
        Layout(rect.gameObject, size, size, 0f, size, size);
        return Fill(rect.gameObject, color, theme.CircleSprite);
    }

    public Image Icon(Transform parent, string icon, Color color, float size = 27f, string name = "Icon")
    {
        RectTransform rect = Rect(name, parent);
        rect.sizeDelta = new Vector2(size, size);
        Layout(rect.gameObject, size, size, 0f, size, size);
        Image image = GetOrAdd<Image>(rect.gameObject);
        image.sprite = MenuThemeAssets.LoadIcon(icon);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static RectTransform Spacer(Transform parent, float height = 0f, float flexibleWidth = -1f, float flexibleHeight = -1f, string name = "Spacer")
    {
        RectTransform rect = Rect(name, parent);
        Layout(rect.gameObject, -1f, height, flexibleWidth, -1f, height, flexibleHeight);
        return rect;
    }

    // ------------------------------------------------------------------ text

    public TextMeshProUGUI Text(Transform parent, string name, string text, MenuTextRole role, Color? color = null,
        TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft, bool caps = false, float tracking = 0f, bool wrap = true)
    {
        RectTransform rect = Rect(name, parent);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        StyleText(label, role, color, alignment, caps, tracking, wrap);
        label.text = text ?? string.Empty;
        return label;
    }

    public TMP_Text StyleText(TMP_Text label, MenuTextRole role, Color? color = null,
        TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft, bool caps = false, float tracking = 0f, bool wrap = true)
    {
        StripDecoration(label.gameObject);
        label.font = theme.FontFor(role);
        label.fontSharedMaterial = label.font != null ? label.font.material : label.fontSharedMaterial;
        label.fontSize = theme.SizeFor(role);
        label.enableAutoSizing = false;
        label.fontStyle = caps ? FontStyles.UpperCase : FontStyles.Normal;
        label.characterSpacing = tracking;
        label.wordSpacing = 0f;
        label.lineSpacing = 0f;
        label.paragraphSpacing = 0f;
        label.color = color ?? theme.ColorFor(role);
        label.alignment = alignment;
        label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.margin = Vector4.zero;
        label.richText = true;
        label.raycastTarget = false;
        label.enabled = true;
        LayoutElement element = label.GetComponent<LayoutElement>();
        if (element != null)
        {
            Object.DestroyImmediate(element);
        }

        return label;
    }

    /// <summary>Eyebrow heading used above row groups (.sec-h).</summary>
    public TextMeshProUGUI Eyebrow(Transform parent, string text, string name = "SectionHeading", Color? color = null)
    {
        return Text(parent, name, text, MenuTextRole.Eyebrow, color ?? P.text3, TextAlignmentOptions.BottomLeft, true, 14f, false);
    }

    /// <summary>Wordmark "HELISIM" with the accent on SIM.</summary>
    public TextMeshProUGUI Wordmark(Transform parent)
    {
        return Text(parent, "BrandMark", "HELI<color=#" + ColorUtility.ToHtmlStringRGB(P.accent) + ">SIM</color>",
            MenuTextRole.Brand, P.text1, TextAlignmentOptions.TopLeft, false, 3f, false);
    }

    // ------------------------------------------------------------------ page scaffolding

    /// <summary>Turns a page root into the reference column: max 1296 wide, 54/60/72 padding, 48 gaps.</summary>
    public void Page(GameObject page)
    {
        StripDecoration(page);
        foreach (Graphic graphic in page.GetComponents<Graphic>())
        {
            Object.DestroyImmediate(graphic);
        }

        foreach (CanvasRenderer renderer in page.GetComponents<CanvasRenderer>())
        {
            Object.DestroyImmediate(renderer);
        }

        RectTransform rect = (RectTransform)page.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        Column(page, 60, 60, 54, 72, 48f);
        Layout(page, PageWidth, -1f, 0f, 0f);
    }

    public RectTransform PageHead(Transform page, string title, string description)
    {
        RectTransform head = Rect("PageHead", page);
        Column(head.gameObject, 0, 0, 0, 0, 12f);
        Text(head, "PageTitle", title, MenuTextRole.Display, P.text1, TextAlignmentOptions.TopLeft, false, 0.5f);
        if (!string.IsNullOrEmpty(description))
        {
            Text(head, "Description", description, MenuTextRole.Body, P.text2);
        }

        return head;
    }

    /// <summary>A section (.sec): eyebrow heading followed by content. Reuses an existing object when given.</summary>
    public RectTransform Section(Transform page, string name, string heading, GameObject existing = null)
    {
        RectTransform section;
        if (existing != null)
        {
            section = (RectTransform)existing.transform;
            section.SetParent(page, false);
            section.SetAsLastSibling();
            StripDecoration(existing);
            foreach (Graphic graphic in existing.GetComponents<Graphic>())
            {
                Object.DestroyImmediate(graphic);
            }
        }
        else
        {
            section = Rect(name, page);
        }

        Column(section.gameObject, 0, 0, 0, 0, 18f);
        if (!string.IsNullOrEmpty(heading))
        {
            Eyebrow(section, heading);
        }

        return section;
    }

    /// <summary>The bordered group that holds setting rows (.rows).</summary>
    public RectTransform Rows(Transform section, string name = "Rows")
    {
        RectTransform rows = Rect(name, section);
        Fill(rows.gameObject, MenuPalette.WithAlpha(P.bg1, 0.5f), theme.PanelSprite);
        Border(rows, P.line, theme.PanelBorderSprite);
        Column(rows.gameObject, 0, 0, 0, 0, 0f);
        return rows;
    }

    /// <summary>
    /// A setting row (.row): label and optional hint on the left, control column on the right.
    /// Pass a control width of 0 for an auto-width control column (.row.wide).
    /// </summary>
    public RectTransform Row(Transform rows, string name, string label, string hint, out RectTransform control,
        float controlWidth = ControlColumn, GameObject existing = null, TMP_Text hintText = null, TMP_Text labelText = null)
    {
        bool divider = CountRows(rows) > 0;
        RectTransform row = existing != null ? (RectTransform)existing.transform : Rect(name, rows);
        if (existing != null)
        {
            StripDecoration(existing);
            foreach (Graphic graphic in existing.GetComponents<Graphic>())
            {
                Object.DestroyImmediate(graphic);
            }
        }

        row.SetParent(rows, false);
        row.SetAsLastSibling();
        RowLayout(row.gameObject, 30, 30, 18, 18, 36f);
        Layout(row.gameObject, -1f, -1f, -1f, -1f, RowMinHeight);
        if (divider)
        {
            TopDivider(row);
        }

        RectTransform text = Rect("Text", row);
        Column(text.gameObject, 0, 0, 0, 0, 2f);
        Layout(text.gameObject, -1f, -1f, 1f, 0f);
        if (labelText != null)
        {
            labelText.transform.SetParent(text, false);
            StyleText(labelText, MenuTextRole.Label, P.text1);
        }
        else
        {
            Text(text, "Label", label, MenuTextRole.Label, P.text1);
        }

        if (hintText != null)
        {
            hintText.transform.SetParent(text, false);
            StyleText(hintText, MenuTextRole.Hint, P.text2);
        }
        else if (!string.IsNullOrEmpty(hint))
        {
            Text(text, "Hint", hint, MenuTextRole.Hint, P.text2);
        }

        control = Rect("Control", row);
        RowLayout(control.gameObject, 0, 0, 0, 0, 12f, TextAnchor.MiddleRight);
        if (controlWidth > 0f)
        {
            Layout(control.gameObject, controlWidth, -1f, 0f, controlWidth);
        }
        else
        {
            Layout(control.gameObject, -1f, -1f, 0f);
        }

        return row;
    }

    private static int CountRows(Transform rows)
    {
        int count = 0;
        foreach (Transform child in rows)
        {
            if (child.name != "Border" && child.GetComponent<LayoutElement>()?.ignoreLayout != true)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Moves an object into a layout parent. Only the sizes passed (0 or more) are written, so sizes a
    /// kit builder already set are kept.
    /// </summary>
    public static T Place<T>(T component, Transform parent, float preferredWidth = -1f, float preferredHeight = -1f,
        float flexibleWidth = -1f, float minWidth = -1f) where T : Component
    {
        component.transform.SetParent(parent, false);
        component.transform.SetAsLastSibling();
        component.transform.localScale = Vector3.one;
        component.transform.localRotation = Quaternion.identity;
        component.gameObject.SetActive(true);
        LayoutElement element = GetOrAdd<LayoutElement>(component.gameObject);
        element.ignoreLayout = false;
        if (preferredWidth >= 0f) element.preferredWidth = preferredWidth;
        if (preferredHeight >= 0f)
        {
            element.preferredHeight = preferredHeight;
            element.minHeight = preferredHeight;
        }

        if (flexibleWidth >= 0f) element.flexibleWidth = flexibleWidth;
        if (minWidth >= 0f) element.minWidth = minWidth;
        return component;
    }

    /// <summary>Callout (.note): icon and secondary text in a quiet panel.</summary>
    public RectTransform Note(Transform parent, string name, string text, string icon = "info", TMP_Text existing = null)
    {
        RectTransform note = Rect(name, parent);
        Fill(note.gameObject, P.bg1, theme.ControlSprite);
        Border(note, P.line, theme.ControlBorderSprite);
        RowLayout(note.gameObject, 24, 24, 21, 21, 18f, TextAnchor.UpperLeft);
        Icon(note, icon, P.text3);
        TMP_Text body = existing;
        if (body != null)
        {
            body.transform.SetParent(note, false);
            StyleText(body, MenuTextRole.Hint, P.text2);
        }
        else
        {
            body = Text(note, "Text", text, MenuTextRole.Hint, P.text2);
        }

        Layout(body.gameObject, -1f, -1f, 1f, 0f);
        return note;
    }

    /// <summary>Chip (.chip): pill with a status dot.</summary>
    public RectTransform Chip(Transform parent, string name, string text, Color dot)
    {
        RectTransform chip = Rect(name, parent);
        Fill(chip.gameObject, P.bg1, theme.CircleSprite);
        Border(chip, P.line, theme.PillBorderSprite);
        RowLayout(chip.gameObject, 18, 18, 0, 0, 12f, TextAnchor.MiddleCenter);
        Layout(chip.gameObject, -1f, 42f, 0f, -1f, 42f);
        Dot(chip, "Dot", dot);
        Text(chip, "Label", text, MenuTextRole.Small, P.text2, TextAlignmentOptions.MidlineLeft, true, 8f, false);
        return chip;
    }

    /// <summary>Wraps a label in a keycap (.kc).</summary>
    public RectTransform Keycap(Transform parent, string name, TMP_Text label, float height = 42f)
    {
        RectTransform cap = Rect(name, parent);
        Fill(cap.gameObject, P.bg1, theme.ControlSprite);
        Border(cap, P.line2, theme.KeycapSprite);
        RowLayout(cap.gameObject, 15, 15, 0, 3, 0f, TextAnchor.MiddleCenter);
        Layout(cap.gameObject, -1f, height, 0f, -1f, height);
        label.transform.SetParent(cap, false);
        StyleText(label, MenuTextRole.Eyebrow, P.text1, TextAlignmentOptions.Midline, false, 0f, false);
        label.font = theme.MonoFont;
        label.fontSharedMaterial = theme.MonoFont.material;
        return cap;
    }

    // ------------------------------------------------------------------ interactive controls

    /// <summary>Adds the hidden 2 px teal focus ring that MenuSelectableStyle shows for keyboard/HOTAS focus.</summary>
    public Image FocusRing(Transform target, float outset = 4.5f)
    {
        Transform old = target.Find("FocusRing");
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        RectTransform rect = Rect("FocusRing", target);
        rect.SetAsLastSibling();
        Stretch(rect, -outset, -outset, -outset, -outset);
        Layout(rect.gameObject).ignoreLayout = true;
        Image ring = Fill(rect.gameObject, P.accent, theme.FocusRingSprite);
        ring.enabled = false;
        return ring;
    }

    private static void Neutralise(Selectable selectable)
    {
        selectable.transition = Selectable.Transition.None;
        StripDecoration(selectable.gameObject);
    }

    public MenuSelectableStyle Style(Selectable selectable, MenuSelectableVariant variant, Graphic background,
        Graphic ring, TMP_Text[] labels = null, params Graphic[] icons)
    {
        MenuSelectableStyle style = GetOrAdd<MenuSelectableStyle>(selectable.gameObject);
        style.Configure(theme, variant, selectable, background, labels ?? new TMP_Text[0], ring);
        style.SetIcons(icons ?? new Graphic[0]);
        EditorUtility.SetDirty(style);
        return style;
    }

    public Button Button(Transform parent, string name, string label, MenuButtonLook look,
        string icon = null, MenuButtonSize size = MenuButtonSize.Normal)
    {
        RectTransform rect = Rect(name, parent);
        Button button = rect.gameObject.AddComponent<Button>();
        return StyleButton(button, label, look, icon, size);
    }

    /// <summary>Rebuilds a button's visuals (.btn) while keeping the Button component and its listeners.</summary>
    public Button StyleButton(Button button, string label, MenuButtonLook look, string icon = null,
        MenuButtonSize size = MenuButtonSize.Normal, bool stretchWidth = false)
    {
        Neutralise(button);
        DestroyChildren(button.transform);
        float height = size == MenuButtonSize.Small ? 51f : size == MenuButtonSize.Large ? 78f : 66f;
        int pad = size == MenuButtonSize.Small ? 18 : size == MenuButtonSize.Large ? 36 : size == MenuButtonSize.Icon ? 0 : 27;
        Image background = Fill(button.gameObject, Color.white, theme.ControlSprite, true);
        button.targetGraphic = background;
        if (look == MenuButtonLook.Default)
        {
            Border(button.transform, P.line2);
        }
        else if (look == MenuButtonLook.Danger)
        {
            Border(button.transform, MenuPalette.WithAlpha(P.warning, 0.45f));
        }

        RowLayout(button.gameObject, pad, pad, 0, 0, 12f, TextAnchor.MiddleCenter);
        var icons = new List<Graphic>();
        if (!string.IsNullOrEmpty(icon))
        {
            icons.Add(Icon(button.transform, icon, P.text1));
        }

        var labels = new List<TMP_Text>();
        if (!string.IsNullOrEmpty(label))
        {
            MenuTextRole role = size == MenuButtonSize.Large ? MenuTextRole.BodyStrong : MenuTextRole.Control;
            TextMeshProUGUI text = Text(button.transform, "Label", label, role, P.text1, TextAlignmentOptions.Midline, false, 0f, false);
            if (size == MenuButtonSize.Small)
            {
                text.fontSize = theme.SizeFor(MenuTextRole.Hint);
            }

            labels.Add(text);
        }

        if (size == MenuButtonSize.Icon)
        {
            Layout(button.gameObject, height, height, 0f, height, height);
        }
        else
        {
            Layout(button.gameObject, -1f, height, stretchWidth ? 1f : 0f, -1f, height);
        }

        Image ring = FocusRing(button.transform);
        MenuSelectableVariant variant = look == MenuButtonLook.Primary ? MenuSelectableVariant.Primary
            : look == MenuButtonLook.Ghost ? MenuSelectableVariant.Ghost
            : look == MenuButtonLook.Danger ? MenuSelectableVariant.Destructive
            : MenuSelectableVariant.Default;
        Style(button, variant, background, ring, labels.ToArray(), icons.ToArray());
        return button;
    }

    /// <summary>Rail navigation item (.nav button); the resume item is the primary variant.</summary>
    public Button NavItem(Button button, string label, string icon, bool primary = false)
    {
        Neutralise(button);
        DestroyChildren(button.transform);
        Image background = Fill(button.gameObject, Color.white, theme.ControlSprite, true);
        button.targetGraphic = background;
        RowLayout(button.gameObject, 18, 18, 0, 0, 18f, TextAnchor.MiddleLeft);
        Layout(button.gameObject, -1f, 66f, 1f, -1f, 66f);
        Image iconImage = Icon(button.transform, icon, primary ? P.accentInk : P.text3);
        TextMeshProUGUI text = Text(button.transform, "Label", label, primary ? MenuTextRole.Control : MenuTextRole.Label,
            primary ? P.accentInk : P.text2, TextAlignmentOptions.MidlineLeft, false, 0f, false);
        text.fontSize = theme.SizeFor(MenuTextRole.Control);
        Layout(text.gameObject, -1f, -1f, 1f, 0f);
        Image ring = FocusRing(button.transform);
        Style(button, primary ? MenuSelectableVariant.Primary : MenuSelectableVariant.NavItem, background, ring,
            new TMP_Text[] { text }, iconImage);
        return button;
    }

    /// <summary>
    /// Off / On segmented control (.seg) on a Toggle that carries SettingsToggleVisual. By default the
    /// toggle itself becomes the segmented container; pass a parent to build the segments there instead
    /// (when the toggle is a whole row).
    /// </summary>
    public Toggle Switch(Toggle toggle, string offText = "Off", string onText = "On", bool neutral = false, Transform segmentParent = null)
    {
        if (segmentParent == null)
        {
            Neutralise(toggle);
        }
        else
        {
            // The toggle is a whole row: keep the row layout it already has.
            toggle.transition = Selectable.Transition.None;
        }

        GameObject containerObject;
        if (segmentParent == null)
        {
            DestroyChildren(toggle.transform);
            containerObject = toggle.gameObject;
        }
        else
        {
            containerObject = Rect("SwitchSegments", segmentParent).gameObject;
        }

        Image container = Fill(containerObject, P.bg1, theme.ControlSprite, true);
        Border(containerObject.transform, P.line2);
        RowLayout(containerObject, 4, 4, 4, 4, 2f, TextAnchor.MiddleCenter);
        Layout(containerObject, -1f, 56f, 0f, -1f, 56f);
        Image offBackground = Segment(containerObject.transform, "OffSegment", offText, out TMP_Text offLabel);
        Image onBackground = Segment(containerObject.transform, "OnSegment", onText, out TMP_Text onLabel);
        toggle.targetGraphic = container;
        toggle.graphic = null;
        SettingsToggleVisual visual = GetOrAdd<SettingsToggleVisual>(toggle.gameObject);
        var serialized = new SerializedObject(visual);
        serialized.FindProperty("settingToggle").objectReferenceValue = toggle;
        serialized.FindProperty("offLabel").objectReferenceValue = offLabel;
        serialized.FindProperty("onLabel").objectReferenceValue = onLabel;
        serialized.FindProperty("offBackground").objectReferenceValue = offBackground;
        serialized.FindProperty("onBackground").objectReferenceValue = onBackground;
        serialized.FindProperty("theme").objectReferenceValue = theme;
        serialized.FindProperty("neutralSelection").boolValue = neutral;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        visual.Refresh();
        Image ring = FocusRing(containerObject.transform);
        Style(toggle, MenuSelectableVariant.FocusOnly, null, ring);
        return toggle;
    }

    private Image Segment(Transform parent, string name, string text, out TMP_Text label)
    {
        RectTransform segment = Rect(name, parent);
        Image background = Fill(segment.gameObject, MenuPalette.WithAlpha(P.bg1, 0f), theme.ControlSprite);
        RowLayout(segment.gameObject, 21, 21, 0, 0, 0f, TextAnchor.MiddleCenter);
        Layout(segment.gameObject, -1f, 48f, 0f, 84f, 48f);
        label = Text(segment, "Label", text, MenuTextRole.Eyebrow, P.text3, TextAlignmentOptions.Midline, true, 8f, false);
        return background;
    }

    /// <summary>Range slider (.range): 6 px track, accent fill and a 24 px round thumb.</summary>
    public Slider Slider(Slider slider)
    {
        Neutralise(slider);
        DestroyChildren(slider.transform);
        Image hit = Fill(slider.gameObject, MenuPalette.WithAlpha(P.bg1, 0f), null, true);
        RectTransform track = Rect("Background", slider.transform);
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.sizeDelta = new Vector2(0f, 6f);
        Fill(track.gameObject, P.line2, theme.CircleSprite);

        RectTransform fillArea = Rect("Fill Area", slider.transform);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(-24f, 6f);
        RectTransform fill = Rect("Fill", fillArea);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = new Vector2(-12f, 0f);
        fill.offsetMax = Vector2.zero;
        Fill(fill.gameObject, P.accent, theme.CircleSprite);

        // Slider stretches the handle across the slide area's height, so the area is zero-height and the
        // thumb's own height comes from its size delta.
        RectTransform handleArea = Rect("Handle Slide Area", slider.transform);
        handleArea.anchorMin = new Vector2(0f, 0.5f);
        handleArea.anchorMax = new Vector2(1f, 0.5f);
        handleArea.sizeDelta = new Vector2(-24f, 0f);
        RectTransform handle = Rect("Handle", handleArea);
        handle.anchorMin = new Vector2(0f, 0.5f);
        handle.anchorMax = new Vector2(0f, 0.5f);
        handle.sizeDelta = new Vector2(24f, 24f);
        Image thumb = Fill(handle.gameObject, P.text1, theme.CircleSprite, true);

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = thumb;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        Layout(slider.gameObject, -1f, 36f, 1f, 120f, 36f);
        hit.raycastTarget = true;
        Image ring = FocusRing(handle, 6f);
        ring.sprite = theme.CircleSprite;
        ring.type = Image.Type.Sliced;
        ring.color = MenuPalette.WithAlpha(P.accent, 0.45f);
        Style(slider, MenuSelectableVariant.FocusOnly, null, ring);
        return slider;
    }

    /// <summary>Text field (.field): bordered, bg-1; mono for numbers, addresses and file names.</summary>
    public TMP_InputField Field(TMP_InputField field, bool mono, float width = -1f, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft,
        string placeholder = "")
    {
        string value = field.text;
        Neutralise(field);
        DestroyChildren(field.transform);
        Image background = Fill(field.gameObject, P.bg1, theme.ControlSprite, true);
        Border(field.transform, P.line2);
        RectTransform area = Rect("Text Area", field.transform);
        Stretch(area, 18f, 0f, 18f, 0f);
        area.gameObject.AddComponent<RectMask2D>();
        MenuTextRole role = mono ? MenuTextRole.Value : MenuTextRole.Body;
        TextMeshProUGUI hint = Text(area, "Placeholder", placeholder, role, P.text3, alignment, false, 0f, false);
        Stretch(hint.rectTransform);
        TextMeshProUGUI text = Text(area, "Text", value, role, P.text1, alignment, false, 0f, false);
        Stretch(text.rectTransform);
        field.textViewport = area;
        field.textComponent = text;
        field.placeholder = hint;
        field.fontAsset = text.font;
        field.pointSize = text.fontSize;
        field.targetGraphic = background;
        field.caretColor = P.accent;
        field.customCaretColor = true;
        field.selectionColor = MenuPalette.WithAlpha(P.accent, 0.3f);
        field.lineType = TMP_InputField.LineType.SingleLine;
        text.verticalAlignment = VerticalAlignmentOptions.Middle;
        hint.verticalAlignment = VerticalAlignmentOptions.Middle;
        field.SetTextWithoutNotify(value);
        if (width > 0f)
        {
            Layout(field.gameObject, width, FieldHeight, 0f, width, FieldHeight);
        }
        else
        {
            Layout(field.gameObject, -1f, FieldHeight, 1f, 96f, FieldHeight);
        }

        Image ring = FocusRing(field.transform);
        Style(field, MenuSelectableVariant.Field, background, ring);
        return field;
    }

    /// <summary>Select (.select): keeps the repaired option template, rebuilds the field and adds a chevron.</summary>
    public TMP_Dropdown Dropdown(TMP_Dropdown dropdown)
    {
        Neutralise(dropdown);
        for (int i = dropdown.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = dropdown.transform.GetChild(i);
            if (dropdown.template == null || child != dropdown.template)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        Image background = Fill(dropdown.gameObject, P.bg1, theme.ControlSprite, true);
        Border(dropdown.transform, P.line2);
        TextMeshProUGUI caption = Text(dropdown.transform, "Label", string.Empty, MenuTextRole.Body, P.text1,
            TextAlignmentOptions.MidlineLeft, false, 0f, false);
        Stretch(caption.rectTransform, 18f, 0f, 54f, 0f);
        caption.overflowMode = TextOverflowModes.Ellipsis;
        Image chevron = Icon(dropdown.transform, "chev", P.text2, 21f, "Arrow");
        RectTransform chevronRect = chevron.rectTransform;
        Layout(chevron.gameObject).ignoreLayout = true;
        chevronRect.anchorMin = chevronRect.anchorMax = new Vector2(1f, 0.5f);
        chevronRect.pivot = new Vector2(0.5f, 0.5f);
        chevronRect.anchoredPosition = new Vector2(-30f, 0f);
        chevronRect.localEulerAngles = new Vector3(0f, 0f, -90f);
        dropdown.captionText = caption;
        dropdown.targetGraphic = background;
        if (dropdown.template != null)
        {
            dropdown.template.SetAsLastSibling();
            StyleOptions(dropdown);
        }

        dropdown.RefreshShownValue();
        Layout(dropdown.gameObject, -1f, FieldHeight, 1f, 120f, FieldHeight);
        Image ring = FocusRing(dropdown.transform);
        Style(dropdown, MenuSelectableVariant.Field, background, ring, new TMP_Text[] { caption }, chevron);
        return dropdown;
    }

    private void StyleOptions(TMP_Dropdown dropdown)
    {
        if (dropdown.itemText != null)
        {
            dropdown.itemText.font = theme.BodyFont;
            dropdown.itemText.fontSharedMaterial = theme.BodyFont.material;
            dropdown.itemText.fontSize = theme.SizeFor(MenuTextRole.Body);
            dropdown.itemText.fontStyle = FontStyles.Normal;
        }
    }

    /// <summary>Full-width disclosure row (.disclose) on an existing Toggle; the chevron turns when open.</summary>
    public Toggle Disclosure(Toggle toggle, string label, GameObject content)
    {
        Neutralise(toggle);
        DestroyChildren(toggle.transform);
        Image background = Fill(toggle.gameObject, MenuPalette.WithAlpha(P.bg3, 0f), null, true);
        RowLayout(toggle.gameObject, 30, 30, 21, 21, 18f, TextAnchor.MiddleLeft);
        Layout(toggle.gameObject, -1f, 72f, 1f, -1f, 72f);
        TextMeshProUGUI text = Text(toggle.transform, "Label", label, MenuTextRole.Label, P.text1, TextAlignmentOptions.MidlineLeft, false, 0f, false);
        Layout(text.gameObject, -1f, -1f, 1f, 0f);
        Image chevron = Icon(toggle.transform, "chev", P.text3, 24f);
        toggle.targetGraphic = background;
        toggle.graphic = null;
        MenuDisclosure disclosure = GetOrAdd<MenuDisclosure>(toggle.gameObject);
        disclosure.Configure(toggle, content, chevron.rectTransform);
        Image ring = FocusRing(toggle.transform, 0f);
        Style(toggle, MenuSelectableVariant.Ghost, background, ring, new TMP_Text[] { text });
        return toggle;
    }

    /// <summary>Segmented tab (.seg button) on a Button that the page disables while it is showing.</summary>
    public Button Tab(Button button, string label)
    {
        Neutralise(button);
        DestroyChildren(button.transform);
        Image background = Fill(button.gameObject, Color.white, theme.ControlSprite, true);
        button.targetGraphic = background;
        RowLayout(button.gameObject, 21, 21, 0, 0, 0f, TextAnchor.MiddleCenter);
        Layout(button.gameObject, -1f, 48f, 0f, 84f, 48f);
        TextMeshProUGUI text = Text(button.transform, "Label", label, MenuTextRole.Eyebrow, P.text3, TextAlignmentOptions.Midline, true, 8f, false);
        Image ring = FocusRing(button.transform);
        Style(button, MenuSelectableVariant.Tab, background, ring, new TMP_Text[] { text });
        return button;
    }

    // ------------------------------------------------------------------ dialogs

    /// <summary>
    /// Modal (.modal-back + .modal): full-screen dim and a 672 px panel with eyebrow, title, body,
    /// optional extra content and right-aligned actions.
    /// </summary>
    public RectTransform Dialog(GameObject root, string eyebrow, string title, string body, out RectTransform extra,
        out RectTransform actions, TMP_Text titleText = null, TMP_Text bodyText = null, float width = 672f)
    {
        StripDecoration(root);
        RectTransform rect = (RectTransform)root.transform;
        Stretch(rect);
        LayoutElement element = root.GetComponent<LayoutElement>();
        if (element != null)
        {
            element.ignoreLayout = true;
        }

        Fill(root, MenuPalette.WithAlpha(P.scrim, 0.72f), null, true);
        RectTransform panel = Rect("DialogPanel", root.transform);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(width, 300f);
        Fill(panel.gameObject, P.bg2, theme.PanelSprite, true);
        Border(panel, P.line2, theme.PanelBorderSprite);
        Column(panel.gameObject, 42, 42, 42, 42, 12f);
        FitVertically(panel.gameObject);
        if (!string.IsNullOrEmpty(eyebrow))
        {
            Text(panel, "Eyebrow", eyebrow, MenuTextRole.Small, P.text3, TextAlignmentOptions.TopLeft, true, 16f);
        }

        TMP_Text heading = titleText;
        if (heading != null)
        {
            heading.transform.SetParent(panel, false);
            StyleText(heading, MenuTextRole.Heading, P.text1);
            if (!string.IsNullOrEmpty(title))
            {
                heading.text = title;
            }
        }
        else
        {
            Text(panel, "Title", title, MenuTextRole.Heading, P.text1);
        }

        if (bodyText != null)
        {
            bodyText.transform.SetParent(panel, false);
            StyleText(bodyText, MenuTextRole.Body, P.text2);
            if (!string.IsNullOrEmpty(body))
            {
                bodyText.text = body;
            }
        }
        else if (!string.IsNullOrEmpty(body))
        {
            Text(panel, "Description", body, MenuTextRole.Body, P.text2);
        }

        extra = Rect("Content", panel);
        Column(extra.gameObject, 0, 0, 0, 0, 12f);
        Spacer(panel, 12f);
        actions = Rect("DialogActions", panel);
        RowLayout(actions.gameObject, 0, 0, 0, 0, 12f, TextAnchor.MiddleRight);
        return panel;
    }
}
