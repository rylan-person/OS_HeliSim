using System.Collections.Generic;
using System.Linq;
using HeliSim.InFlightMenu;
using HeliSim.UI.Theme;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Asset checks for the menu redesign (plan 2026-11-17): both menu prefabs use the theme's
/// fonts, type scale, palette, sprites and state-driven selectables.
/// </summary>
public sealed class MenuThemeAssetTests
{
    private const string InFlightMenuPath = "Assets/Level/Prefabs/UI/Menu.prefab";
    private const string MainMenuPath = "Assets/Level/Prefabs/UI/MainMenu.prefab";
    private static readonly string[] PrefabPaths = { InFlightMenuPath, MainMenuPath };

    private MenuTheme theme;
    private readonly Dictionary<string, GameObject> loaded = new Dictionary<string, GameObject>();

    [OneTimeSetUp]
    public void LoadAssets()
    {
        theme = AssetDatabase.LoadAssetAtPath<MenuTheme>(MenuThemeAssets.ThemePath);
        foreach (string path in PrefabPaths)
        {
            loaded[path] = PrefabUtility.LoadPrefabContents(path);
        }
    }

    [OneTimeTearDown]
    public void UnloadAssets()
    {
        foreach (GameObject root in loaded.Values)
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        loaded.Clear();
    }

    [Test]
    public void ThemeAsset_HasFontsSpritesAndScale()
    {
        Assert.That(theme, Is.Not.Null, MenuThemeAssets.ThemePath);
        Assert.That(theme.BodyFont, Is.Not.Null);
        Assert.That(theme.BodyMediumFont, Is.Not.Null);
        Assert.That(theme.BodySemiBoldFont, Is.Not.Null);
        Assert.That(theme.DisplayFont, Is.Not.Null);
        Assert.That(theme.MonoFont, Is.Not.Null);
        Assert.That(theme.ControlSprite, Is.Not.Null);
        Assert.That(theme.PanelSprite, Is.Not.Null);
        Assert.That(theme.FocusRingSprite, Is.Not.Null);
        foreach (TMP_FontAsset font in theme.Fonts())
        {
            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic), font.name);
            Assert.That(font.fallbackFontAssetTable, Is.Not.Empty, font.name + " needs a fallback for missing glyphs.");
        }
    }

    [Test]
    public void EveryText_UsesAThemeFontAndATypeScaleSize([ValueSource(nameof(PrefabPaths))] string path)
    {
        TMP_FontAsset[] fonts = theme.Fonts();
        float[] scale = theme.TypeScale();
        var failures = new List<string>();
        foreach (TMP_Text text in Root(path).GetComponentsInChildren<TMP_Text>(true))
        {
            if (!fonts.Contains(text.font))
            {
                failures.Add(PathOf(text.transform) + " font " + (text.font != null ? text.font.name : "null"));
            }

            if (!text.enableAutoSizing && !scale.Any(size => Mathf.Approximately(size, text.fontSize)))
            {
                failures.Add(PathOf(text.transform) + " size " + text.fontSize);
            }

            if ((text.fontStyle & FontStyles.Bold) != 0)
            {
                failures.Add(PathOf(text.transform) + " uses faux bold; use a weight font asset instead");
            }
        }

        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }

    [Test]
    public void EveryImageAndTextColour_IsAPaletteToken([ValueSource(nameof(PrefabPaths))] string path)
    {
        MenuPalette palette = theme.Palette;
        var failures = new List<string>();
        GameObject root = Root(path);
        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if (!palette.IsToken(graphic.color))
            {
                failures.Add(PathOf(graphic.transform) + " #" + ColorUtility.ToHtmlStringRGBA(graphic.color));
            }
        }

        foreach (Outline outline in root.GetComponentsInChildren<Outline>(true))
        {
            if (!palette.IsToken(outline.effectColor))
            {
                failures.Add(PathOf(outline.transform) + " outline #" + ColorUtility.ToHtmlStringRGBA(outline.effectColor));
            }
        }

        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }

    [Test]
    public void EverySelectable_IsThemedWithoutColorTintAndHasAFocusRing([ValueSource(nameof(PrefabPaths))] string path)
    {
        var failures = new List<string>();
        foreach (Selectable selectable in Root(path).GetComponentsInChildren<Selectable>(true))
        {
            string name = PathOf(selectable.transform);
            if (selectable.transition == Selectable.Transition.ColorTint)
            {
                failures.Add(name + " still uses ColorTint");
            }

            MenuSelectableStyle style = selectable.GetComponent<MenuSelectableStyle>();
            if (style == null)
            {
                failures.Add(name + " has no MenuSelectableStyle");
                continue;
            }

            if (Field<MenuTheme>(style, "theme") != theme)
            {
                failures.Add(name + " style has no theme");
            }

            Graphic ring = style.FocusRing;
            if (ring == null)
            {
                failures.Add(name + " has no focus ring");
            }
            else if (ring.enabled || ring.raycastTarget)
            {
                failures.Add(name + " focus ring must start hidden and must not block raycasts");
            }
            else if (ring.GetComponent<LayoutElement>() == null || !ring.GetComponent<LayoutElement>().ignoreLayout)
            {
                failures.Add(name + " focus ring must ignore layout");
            }
        }

        foreach (SettingsToggleVisual visual in Root(path).GetComponentsInChildren<SettingsToggleVisual>(true))
        {
            if (Field<MenuTheme>(visual, "theme") != theme)
            {
                failures.Add(PathOf(visual.transform) + " SettingsToggleVisual has no theme");
            }
        }

        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }

    [Test]
    public void SlicedImages_UseThemeSpritesOnly([ValueSource(nameof(PrefabPaths))] string path)
    {
        var allowed = new HashSet<Sprite>
        {
            null, theme.ControlSprite, theme.PanelSprite, theme.FocusRingSprite, theme.ControlBorderSprite,
            theme.PanelBorderSprite, theme.CircleSprite, theme.PillBorderSprite, theme.KeycapSprite,
        };
        var failures = new List<string>();
        foreach (Image image in Root(path).GetComponentsInChildren<Image>(true))
        {
            if (image.type == Image.Type.Sliced && image.sprite != null && !allowed.Contains(image.sprite))
            {
                failures.Add(PathOf(image.transform) + " sprite " + image.sprite.name);
            }
        }

        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }

    [Test]
    public void PageLayouts_UseSymmetricPaddingAndScaleSpacing([ValueSource(nameof(PrefabPaths))] string path)
    {
        float[] spacingScale = { 0f, 2f, 6f, 12f, 18f, 24f, 36f, 48f, 60f };
        var failures = new List<string>();
        foreach (HorizontalOrVerticalLayoutGroup group in Root(path).GetComponentsInChildren<HorizontalOrVerticalLayoutGroup>(true))
        {
            string name = PathOf(group.transform);
            if (group.padding.left != group.padding.right)
            {
                failures.Add(name + " padding " + group.padding.left + "/" + group.padding.right);
            }

            if (!spacingScale.Any(step => Mathf.Approximately(step, group.spacing)))
            {
                failures.Add(name + " spacing " + group.spacing);
            }
        }

        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }

    [Test]
    public void ActionVariants_MatchTheirMeaning()
    {
        // Reference: one primary per view, red only for destructive actions, rail exits stay neutral.
        AssertVariant(InFlightMenuPath, "ResumeButton", MenuSelectableVariant.Primary);
        AssertVariant(InFlightMenuPath, "ReturnToMenuButton", MenuSelectableVariant.NavItem);
        AssertVariant(InFlightMenuPath, "ControlsButton", MenuSelectableVariant.NavItem);
        AssertVariant(InFlightMenuPath, "ResetProfile", MenuSelectableVariant.Destructive);
        AssertVariant(InFlightMenuPath, "SettingsPageActionBar/Buttons/ApplyButton", MenuSelectableVariant.Primary);
        AssertVariant(InFlightMenuPath, "SettingsPageActionBar/Buttons/RevertButton", MenuSelectableVariant.Ghost);
        AssertVariant(InFlightMenuPath, "KeyboardTab", MenuSelectableVariant.Tab);
        AssertVariant(InFlightMenuPath, "UnsavedChangesDialog/DialogPanel/DialogActions/ApplyButton", MenuSelectableVariant.Primary);
        AssertVariant(InFlightMenuPath, "UnsavedChangesDialog/DialogPanel/DialogActions/DiscardButton", MenuSelectableVariant.Default);
        AssertVariant(InFlightMenuPath, "UnsavedChangesDialog/DialogPanel/DialogActions/CancelButton", MenuSelectableVariant.Ghost);
        AssertVariant(InFlightMenuPath, "StartEngine", MenuSelectableVariant.Primary);
        AssertVariant(MainMenuPath, "Quit", MenuSelectableVariant.NavItem);
        AssertVariant(MainMenuPath, "flyNavigation", MenuSelectableVariant.NavItem);
        AssertVariant(MainMenuPath, "StartJoin", MenuSelectableVariant.Primary);
        AssertVariant(MainMenuPath, "StartHost", MenuSelectableVariant.Default);
        AssertVariant(MainMenuPath, "StartSolo", MenuSelectableVariant.Primary);
        AssertVariant(MainMenuPath, "ConfirmQuit/DialogPanel/DialogActions/Quit", MenuSelectableVariant.Destructive);
        AssertVariant(MainMenuPath, "ConfirmChanges/DialogPanel/DialogActions/Apply", MenuSelectableVariant.Primary);
    }

    [Test]
    public void SettingRows_AreAtLeastEightyPixelsAndBindingRowsStayCompact()
    {
        var failures = new List<string>();
        foreach (string path in PrefabPaths)
        {
            int rows = 0;
            foreach (Transform control in Root(path).GetComponentsInChildren<Transform>(true).Where(t => t.name == "Control"))
            {
                Transform row = control.parent;
                if (row == null || row.GetComponent<HorizontalLayoutGroup>() == null)
                {
                    continue;
                }

                rows++;
                LayoutElement element = row.GetComponent<LayoutElement>();
                if (element == null || element.minHeight < 80f)
                {
                    failures.Add(PathOf(row) + " min height " + (element != null ? element.minHeight : 0f));
                }
            }

            if (rows == 0)
            {
                failures.Add(path + " has no setting rows");
            }

            foreach (ControlsBindingRow row in Root(path).GetComponentsInChildren<ControlsBindingRow>(true))
            {
                LayoutElement element = row.GetComponent<LayoutElement>();
                if (element != null && element.preferredHeight > 56f)
                {
                    failures.Add(PathOf(row.transform) + " binding row " + element.preferredHeight);
                }
            }
        }

        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }

    [Test]
    public void ObjectNames_HaveNoTypos([ValueSource(nameof(PrefabPaths))] string path)
    {
        string[] typos = Root(path).GetComponentsInChildren<Transform>(true)
            .Where(t => t.name == "Asissts").Select(t => PathOf(t)).ToArray();
        Assert.That(typos, Is.Empty);
    }

    private void AssertVariant(string prefabPath, string path, MenuSelectableVariant expected)
    {
        // Paths are relative to the first object named like the path's first segment.
        string[] parts = path.Split('/');
        Transform start = Root(prefabPath).GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == parts[0]);
        Transform target = start != null && parts.Length > 1 ? start.Find(string.Join("/", parts.Skip(1))) : start;
        Assert.That(target, Is.Not.Null, prefabPath + ": " + path);
        MenuSelectableStyle style = target.GetComponent<MenuSelectableStyle>();
        Assert.That(style, Is.Not.Null, path + " has no MenuSelectableStyle");
        Assert.That(style.Variant, Is.EqualTo(expected), path);
    }

    private GameObject Root(string path)
    {
        return loaded[path];
    }

    private static T Field<T>(Object instance, string name) where T : Object
    {
        var serialized = new SerializedObject(instance);
        SerializedProperty property = serialized.FindProperty(name);
        Assert.That(property, Is.Not.Null, instance.GetType().Name + "." + name);
        return property.objectReferenceValue as T;
    }

    private static string PathOf(Transform transform)
    {
        var names = new List<string>();
        while (transform != null)
        {
            names.Insert(0, transform.name);
            transform = transform.parent;
        }

        return string.Join("/", names);
    }
}
