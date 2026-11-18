using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HeliSim.UI.Theme
{
    /// <summary>Visual variants for menu selectables.</summary>
    public enum MenuSelectableVariant
    {
        /// <summary>Raised neutral button.</summary>
        Default,
        /// <summary>Teal call to action (Apply, Start, Resume) and slider handles.</summary>
        Primary,
        /// <summary>Transparent secondary action.</summary>
        Ghost,
        /// <summary>Destructive action (Quit, Reset all bindings, Return to menu).</summary>
        Destructive,
        /// <summary>Navigation rail entry.</summary>
        NavItem,
        /// <summary>Setting row that is itself selectable (toggle rows, list items).</summary>
        Row,
        /// <summary>Inset field: dropdowns, text inputs and checkboxes.</summary>
        Field,
        /// <summary>Segmented tab; the non-interactable tab is the active one and shows teal.</summary>
        Tab,
        /// <summary>Only draws the focus ring; another component owns the colours.</summary>
        FocusOnly,
    }

    /// <summary>Interaction state used to resolve colours.</summary>
    public enum MenuVisualState
    {
        Normal,
        Highlighted,
        Pressed,
        Selected,
        Disabled,
    }

    /// <summary>Background and label colour for one variant/state pair.</summary>
    public readonly struct MenuStateColors
    {
        public MenuStateColors(Color background, Color label)
        {
            Background = background;
            Label = label;
        }

        public Color Background { get; }
        public Color Label { get; }
    }

    /// <summary>
    /// Drives a Selectable's background and label colours from the menu theme. Replaces
    /// Unity's ColorTint transition, which multiplies and can only darken. Hover, press and
    /// keyboard/HOTAS focus all brighten; disabled is 40% alpha; a child focus ring is shown
    /// while the control is selected by navigation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuSelectableStyle : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        [SerializeField] private MenuTheme theme;
        [SerializeField] private MenuSelectableVariant variant = MenuSelectableVariant.Default;
        [SerializeField] private Selectable selectable;
        [SerializeField] private Graphic background;
        [SerializeField] private TMP_Text[] labels = new TMP_Text[0];
        [SerializeField] private Graphic focusRing;
        [Tooltip("Icons follow the label colour; a current NavItem shows its icon in accent.")]
        [SerializeField] private Graphic[] icons = new Graphic[0];
        [Tooltip("Hide the focus ring when selection came from a mouse click (like :focus-visible).")]
        [SerializeField] private bool hideRingForPointerSelection = true;

        private bool isHovered;
        private bool isPressed;
        private bool isSelected;
        private bool isRingVisible;
        private bool isCurrent;
        private bool lastInteractable;
        private bool hasApplied;

        public MenuSelectableVariant Variant => variant;
        public Graphic Background => background;
        public Graphic FocusRing => focusRing;
        public MenuVisualState CurrentState => ResolveState();
        public Graphic[] Icons => icons;

        /// <summary>Assigns icon graphics tinted with the label colour.</summary>
        public void SetIcons(Graphic[] iconGraphics)
        {
            icons = iconGraphics ?? new Graphic[0];
            Apply();
        }

        /// <summary>Marks a NavItem as the current page; current items stay highlighted.</summary>
        public bool IsCurrent
        {
            get => isCurrent;
            set
            {
                if (isCurrent == value)
                {
                    return;
                }

                isCurrent = value;
                Apply();
            }
        }

        /// <summary>Configures the component; used by the Editor theme tool and tests.</summary>
        public void Configure(MenuTheme menuTheme, MenuSelectableVariant menuVariant, Selectable target,
            Graphic backgroundGraphic, TMP_Text[] labelTexts, Graphic ring)
        {
            theme = menuTheme;
            variant = menuVariant;
            selectable = target;
            background = backgroundGraphic;
            labels = labelTexts ?? new TMP_Text[0];
            focusRing = ring;
            hasApplied = false;
            Apply();
        }

        private void Awake()
        {
            if (selectable == null)
            {
                selectable = GetComponent<Selectable>();
            }
        }

        private void OnEnable()
        {
            isHovered = false;
            isPressed = false;
            EventSystem eventSystem = EventSystem.current;
            isSelected = eventSystem != null && eventSystem.currentSelectedGameObject == gameObject;
            isRingVisible = isSelected;
            hasApplied = false;
            Apply();
        }

        private void OnDisable()
        {
            isHovered = false;
            isPressed = false;
            isSelected = false;
            isRingVisible = false;
            SetRing(false);
        }

        private void LateUpdate()
        {
            if (!hasApplied || IsInteractable() != lastInteractable)
            {
                Apply();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            Apply();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            isPressed = false;
            Apply();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button == PointerEventData.InputButton.Left)
            {
                isPressed = true;
                Apply();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            Apply();
        }

        public void OnSelect(BaseEventData eventData)
        {
            isSelected = true;
            isRingVisible = !(hideRingForPointerSelection && eventData is PointerEventData);
            Apply();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            isSelected = false;
            isRingVisible = false;
            Apply();
        }

        /// <summary>Re-applies colours for the current state.</summary>
        public void Apply()
        {
            lastInteractable = IsInteractable();
            MenuVisualState state = ResolveState();
            hasApplied = true;

            SetRing(isSelected && isRingVisible && state != MenuVisualState.Disabled);
            if (variant == MenuSelectableVariant.FocusOnly)
            {
                return;
            }

            MenuStateColors colors = Resolve(MenuTheme.PaletteOf(theme), variant, state, isCurrent);
            if (background != null)
            {
                background.color = colors.Background;
            }

            if (labels != null)
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    if (labels[i] != null)
                    {
                        labels[i].color = colors.Label;
                    }
                }
            }

            if (icons != null)
            {
                Color iconColor = variant == MenuSelectableVariant.NavItem && isCurrent && state != MenuVisualState.Disabled
                    ? MenuTheme.PaletteOf(theme).accent
                    : colors.Label;
                for (int i = 0; i < icons.Length; i++)
                {
                    if (icons[i] != null)
                    {
                        icons[i].color = iconColor;
                    }
                }
            }
        }

        /// <summary>Colour lookup shared by runtime and asset checks.</summary>
        public static MenuStateColors Resolve(MenuPalette p, MenuSelectableVariant variant, MenuVisualState state, bool isCurrent = false)
        {
            if (p == null)
            {
                p = MenuPalette.Default;
            }

            Color clear = MenuPalette.WithAlpha(p.bg3, 0f);
            MenuStateColors normal;
            MenuStateColors hover;
            MenuStateColors pressed;
            switch (variant)
            {
                case MenuSelectableVariant.Primary:
                    normal = new MenuStateColors(p.accent, p.accentInk);
                    hover = new MenuStateColors(p.accentHi, p.accentInk);
                    pressed = new MenuStateColors(Color.Lerp(p.accent, p.accentHi, 0.5f), p.accentInk);
                    break;
                case MenuSelectableVariant.Ghost:
                    normal = new MenuStateColors(clear, p.text2);
                    hover = new MenuStateColors(p.bg3, p.text1);
                    pressed = new MenuStateColors(p.bg4, p.text1);
                    break;
                case MenuSelectableVariant.Destructive:
                    // Low alphas: the project renders UI in linear space, which strengthens blends.
                    normal = new MenuStateColors(MenuPalette.WithAlpha(p.warning, 0.03f), p.warning);
                    hover = new MenuStateColors(MenuPalette.WithAlpha(p.warning, 0.09f), p.warning);
                    pressed = new MenuStateColors(MenuPalette.WithAlpha(p.warning, 0.14f), p.warning);
                    break;
                case MenuSelectableVariant.NavItem:
                    normal = isCurrent
                        ? new MenuStateColors(p.bg3, p.text1)
                        : new MenuStateColors(clear, p.text2);
                    hover = new MenuStateColors(p.bg4, p.text1);
                    pressed = new MenuStateColors(p.bg4, p.text1);
                    break;
                case MenuSelectableVariant.Row:
                    normal = new MenuStateColors(p.bg2, p.text1);
                    hover = new MenuStateColors(p.bg3, p.text1);
                    pressed = new MenuStateColors(p.bg4, p.text1);
                    break;
                case MenuSelectableVariant.Field:
                    normal = new MenuStateColors(p.bg1, p.text1);
                    hover = new MenuStateColors(p.bg3, p.text1);
                    pressed = new MenuStateColors(p.bg4, p.text1);
                    break;
                case MenuSelectableVariant.Tab:
                    if (state == MenuVisualState.Disabled)
                    {
                        // ControlsRebindingPage disables the tab that is already showing; it reads as
                        // the selected segment of a segmented control.
                        return new MenuStateColors(p.bg4, p.text1);
                    }

                    normal = new MenuStateColors(clear, p.text3);
                    hover = new MenuStateColors(p.bg3, p.text1);
                    pressed = new MenuStateColors(p.bg4, p.text1);
                    break;
                default:
                    normal = new MenuStateColors(p.bg3, p.text1);
                    hover = new MenuStateColors(p.bg4, p.text1);
                    pressed = new MenuStateColors(p.line2, p.text1);
                    break;
            }

            switch (state)
            {
                case MenuVisualState.Highlighted:
                case MenuVisualState.Selected:
                    return hover;
                case MenuVisualState.Pressed:
                    return pressed;
                case MenuVisualState.Disabled:
                    return new MenuStateColors(MenuPalette.Disabled(normal.Background), MenuPalette.Disabled(normal.Label));
                default:
                    return normal;
            }
        }

        private MenuVisualState ResolveState()
        {
            if (!IsInteractable())
            {
                return MenuVisualState.Disabled;
            }

            if (isPressed)
            {
                return MenuVisualState.Pressed;
            }

            if (isSelected)
            {
                return MenuVisualState.Selected;
            }

            return isHovered ? MenuVisualState.Highlighted : MenuVisualState.Normal;
        }

        private bool IsInteractable()
        {
            return selectable == null || selectable.IsInteractable();
        }

        private void SetRing(bool visible)
        {
            if (focusRing != null && focusRing.enabled != visible)
            {
                focusRing.enabled = visible;
            }
        }
    }
}
