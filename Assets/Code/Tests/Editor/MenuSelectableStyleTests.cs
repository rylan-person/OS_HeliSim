using System.Collections.Generic;
using HeliSim.UI.Theme;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MenuSelectableStyleTests
{
    private readonly List<Object> createdObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object createdObject in createdObjects)
        {
            if (createdObject != null)
            {
                Object.DestroyImmediate(createdObject);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void Resolve_HoverAndFocusBrightenAndDisabledNeverMatchesHighlighted(
        [Values] MenuSelectableVariant variant)
    {
        if (variant == MenuSelectableVariant.FocusOnly)
        {
            Assert.Pass("FocusOnly does not own colours.");
        }

        MenuPalette palette = MenuPalette.Default;
        MenuStateColors normal = MenuSelectableStyle.Resolve(palette, variant, MenuVisualState.Normal);
        MenuStateColors hover = MenuSelectableStyle.Resolve(palette, variant, MenuVisualState.Highlighted);
        MenuStateColors selected = MenuSelectableStyle.Resolve(palette, variant, MenuVisualState.Selected);
        MenuStateColors disabled = MenuSelectableStyle.Resolve(palette, variant, MenuVisualState.Disabled);

        Assert.That(Luminance(hover.Background) * hover.Background.a,
            Is.GreaterThan(Luminance(normal.Background) * normal.Background.a), "Hover must brighten, not darken.");
        Assert.That(selected.Background, Is.EqualTo(hover.Background));
        Assert.That(disabled.Background, Is.Not.EqualTo(hover.Background));
        if (variant != MenuSelectableVariant.Tab)
        {
            // A disabled tab is the active segment (bg-4, tx-1); only its background differs from hover.
            Assert.That(disabled.Label, Is.Not.EqualTo(hover.Label));
            Assert.That(disabled.Label.a, Is.EqualTo(normal.Label.a * MenuPalette.DisabledAlpha).Within(0.001f));
        }
    }

    [Test]
    public void Resolve_OnlyDestructiveUsesWarningRed()
    {
        MenuPalette palette = MenuPalette.Default;
        foreach (MenuSelectableVariant variant in System.Enum.GetValues(typeof(MenuSelectableVariant)))
        {
            MenuStateColors normal = MenuSelectableStyle.Resolve(palette, variant, MenuVisualState.Normal);
            bool usesWarning = SameRgb(normal.Label, palette.warning) || SameRgb(normal.Background, palette.warning);
            Assert.That(usesWarning, Is.EqualTo(variant == MenuSelectableVariant.Destructive), variant.ToString());
        }
    }

    [Test]
    public void FocusRing_ShowsOnlyWhileSelectedByNavigationAndHidesForPointerOrDisabled()
    {
        CreateStyledButton(MenuSelectableVariant.Default, out Button button, out Image background,
            out TMP_Text label, out Image ring, out MenuSelectableStyle style);

        Assert.That(ring.enabled, Is.False, "Ring hidden initially.");

        style.OnSelect(new BaseEventData(null));
        Assert.That(ring.enabled, Is.True, "Navigation selection shows the ring.");
        Assert.That(style.CurrentState, Is.EqualTo(MenuVisualState.Selected));
        Assert.That(background.color, Is.EqualTo(MenuSelectableStyle.Resolve(
            MenuPalette.Default, MenuSelectableVariant.Default, MenuVisualState.Selected).Background));

        style.OnDeselect(new BaseEventData(null));
        Assert.That(ring.enabled, Is.False, "Deselect hides the ring.");

        style.OnSelect(new PointerEventData(null));
        Assert.That(ring.enabled, Is.False, "Mouse-click selection does not show the ring.");
        style.OnDeselect(new BaseEventData(null));

        style.OnSelect(new BaseEventData(null));
        button.interactable = false;
        style.Apply();
        Assert.That(ring.enabled, Is.False, "Disabled controls never show the ring.");
        Assert.That(style.CurrentState, Is.EqualTo(MenuVisualState.Disabled));
        Assert.That(label.color.a, Is.LessThan(1f));
    }

    [Test]
    public void PointerStates_MapToHighlightedAndPressed()
    {
        CreateStyledButton(MenuSelectableVariant.Primary, out _, out Image background,
            out TMP_Text label, out _, out MenuSelectableStyle style);
        MenuPalette p = MenuPalette.Default;

        Assert.That(background.color, Is.EqualTo(p.accent));
        Assert.That(label.color, Is.EqualTo(p.accentInk));

        style.OnPointerEnter(new PointerEventData(null));
        Assert.That(style.CurrentState, Is.EqualTo(MenuVisualState.Highlighted));
        Assert.That(background.color, Is.EqualTo(p.accentHi));

        style.OnPointerDown(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(style.CurrentState, Is.EqualTo(MenuVisualState.Pressed));

        style.OnPointerUp(new PointerEventData(null));
        style.OnPointerExit(new PointerEventData(null));
        Assert.That(style.CurrentState, Is.EqualTo(MenuVisualState.Normal));
        Assert.That(background.color, Is.EqualTo(p.accent));
    }

    [Test]
    public void NavItem_CurrentPageStaysHighlighted()
    {
        CreateStyledButton(MenuSelectableVariant.NavItem, out _, out Image background,
            out _, out _, out MenuSelectableStyle style);
        Color idle = background.color;

        style.IsCurrent = true;

        Assert.That(background.color, Is.Not.EqualTo(idle));
        Assert.That(background.color, Is.EqualTo(MenuPalette.Default.bg3));
    }

    [Test]
    public void Tab_ActiveTabIsDisabledButShowsAsTheSelectedSegmentNotDimmed()
    {
        MenuPalette p = MenuPalette.Default;
        MenuStateColors active = MenuSelectableStyle.Resolve(p, MenuSelectableVariant.Tab, MenuVisualState.Disabled);
        Assert.That(active.Background, Is.EqualTo(p.bg4));
        Assert.That(active.Label, Is.EqualTo(p.text1));
        Assert.That(active.Label.a, Is.EqualTo(1f));
    }

    [Test]
    public void MissingThemeGraphicsAndSelectable_AreTolerated()
    {
        GameObject go = new GameObject("Bare");
        createdObjects.Add(go);
        MenuSelectableStyle style = go.AddComponent<MenuSelectableStyle>();

        Assert.DoesNotThrow(() =>
        {
            style.Configure(null, MenuSelectableVariant.Ghost, null, null, null, null);
            style.OnPointerEnter(new PointerEventData(null));
            style.OnSelect(new BaseEventData(null));
            style.OnDeselect(new BaseEventData(null));
            style.IsCurrent = true;
            style.Apply();
        });
    }

    private void CreateStyledButton(MenuSelectableVariant variant, out Button button, out Image background,
        out TMP_Text label, out Image ring, out MenuSelectableStyle style)
    {
        GameObject root = new GameObject("Button", typeof(RectTransform));
        createdObjects.Add(root);
        background = root.AddComponent<Image>();
        button = root.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(root.transform, false);
        label = labelObject.AddComponent<TextMeshProUGUI>();
        GameObject ringObject = new GameObject("FocusRing", typeof(RectTransform));
        ringObject.transform.SetParent(root.transform, false);
        ring = ringObject.AddComponent<Image>();
        ring.enabled = false;
        style = root.AddComponent<MenuSelectableStyle>();
        style.Configure(null, variant, button, background, new[] { label }, ring);
    }

    private static float Luminance(Color c)
    {
        return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
    }

    private static bool SameRgb(Color a, Color b)
    {
        return Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g) && Mathf.Approximately(a.b, b.b);
    }
}
