using TMPro;
using UnityEngine;

namespace HeliSim.UI.Theme
{
    /// <summary>Text roles on the menu type scale.</summary>
    public enum MenuTextRole
    {
        Display,
        Heading,
        Body,
        /// <summary>Row labels and navigation text (Barlow Medium, body size).</summary>
        Label,
        BodyStrong,
        Hint,
        Eyebrow,
        Value,
        /// <summary>Card and tile titles (Barlow Condensed SemiBold).</summary>
        Title,
        /// <summary>Status values and step numbers (Barlow Condensed SemiBold).</summary>
        Subtitle,
        /// <summary>Buttons, navigation items and inputs (Barlow SemiBold).</summary>
        Control,
        /// <summary>Chips, card keys and dialog eyebrows (Barlow Condensed SemiBold, caps).</summary>
        Small,
        /// <summary>Brand subtitle, rail label and version (caps or mono).</summary>
        Micro,
        /// <summary>HELISIM wordmark (Barlow Condensed Bold).</summary>
        Brand,
        /// <summary>Time-trial clock (JetBrains Mono).</summary>
        Clock,
    }

    /// <summary>
    /// Runtime-safe menu theme: palette, font assets, type scale and shared sprites.
    /// Holds no Editor references so menus can read it in player builds.
    /// </summary>
    [CreateAssetMenu(menuName = "HeliSim/Menu Theme", fileName = "MenuTheme")]
    public sealed class MenuTheme : ScriptableObject
    {
        [SerializeField] private MenuPalette palette = new MenuPalette();

        [Header("Fonts")]
        [SerializeField] private TMP_FontAsset bodyFont;
        [SerializeField] private TMP_FontAsset bodyMediumFont;
        [SerializeField] private TMP_FontAsset bodySemiBoldFont;
        [SerializeField] private TMP_FontAsset displayFont;
        [SerializeField] private TMP_FontAsset displayBoldFont;
        [SerializeField] private TMP_FontAsset monoFont;

        [Header("Type scale (reference 1920x1080)")]
        [SerializeField] private float displaySize = 60f;
        [SerializeField] private float headingSize = 39f;
        [SerializeField] private float titleSize = 30f;
        [SerializeField] private float subtitleSize = 27f;
        [SerializeField] private float bodySize = 24f;
        [SerializeField] private float controlSize = 22f;
        [SerializeField] private float hintSize = 21f;
        [SerializeField] private float eyebrowSize = 19f;
        [SerializeField] private float smallSize = 18f;
        [SerializeField] private float microSize = 16f;
        [SerializeField] private float valueSize = 22f;
        [SerializeField] private float brandSize = 48f;
        [SerializeField] private float clockSize = 42f;

        [Header("Sprites")]
        [SerializeField] private Sprite controlSprite;
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite focusRingSprite;
        [SerializeField] private Sprite controlBorderSprite;
        [SerializeField] private Sprite panelBorderSprite;
        [SerializeField] private Sprite circleSprite;
        [SerializeField] private Sprite pillBorderSprite;
        [SerializeField] private Sprite keycapSprite;

        public MenuPalette Palette => palette ?? MenuPalette.Default;
        public TMP_FontAsset BodyFont => bodyFont;
        public TMP_FontAsset BodyMediumFont => bodyMediumFont != null ? bodyMediumFont : bodyFont;
        public TMP_FontAsset BodySemiBoldFont => bodySemiBoldFont != null ? bodySemiBoldFont : BodyMediumFont;
        public TMP_FontAsset DisplayFont => displayFont;
        public TMP_FontAsset DisplayBoldFont => displayBoldFont != null ? displayBoldFont : displayFont;
        public TMP_FontAsset MonoFont => monoFont;
        public Sprite ControlSprite => controlSprite;
        public Sprite PanelSprite => panelSprite;
        public Sprite FocusRingSprite => focusRingSprite;
        /// <summary>1.5 px ring matching <see cref="ControlSprite"/> (radius 9).</summary>
        public Sprite ControlBorderSprite => controlBorderSprite;
        /// <summary>1.5 px ring matching <see cref="PanelSprite"/> (radius 15).</summary>
        public Sprite PanelBorderSprite => panelBorderSprite;
        /// <summary>Filled circle; sliced it draws pills, dots and slider thumbs.</summary>
        public Sprite CircleSprite => circleSprite;
        /// <summary>1.5 px ring around a pill (chips).</summary>
        public Sprite PillBorderSprite => pillBorderSprite;
        /// <summary>Keycap ring with a heavier bottom edge.</summary>
        public Sprite KeycapSprite => keycapSprite;

        /// <summary>Resolves the palette of an optional theme.</summary>
        public static MenuPalette PaletteOf(MenuTheme theme)
        {
            return theme != null ? theme.Palette : MenuPalette.Default;
        }

        /// <summary>Font asset for a text role; may be null if the theme has not been populated.</summary>
        public TMP_FontAsset FontFor(MenuTextRole role)
        {
            switch (role)
            {
                case MenuTextRole.Display:
                case MenuTextRole.Heading:
                case MenuTextRole.Eyebrow:
                case MenuTextRole.Title:
                case MenuTextRole.Subtitle:
                case MenuTextRole.Small:
                case MenuTextRole.Micro:
                    return DisplayFont;
                case MenuTextRole.Brand:
                    return DisplayBoldFont;
                case MenuTextRole.Clock:
                    return MonoFont;
                case MenuTextRole.BodyStrong:
                case MenuTextRole.Control:
                    return BodySemiBoldFont;
                case MenuTextRole.Label:
                    return BodyMediumFont;
                case MenuTextRole.Value:
                    return MonoFont;
                default:
                    return BodyFont;
            }
        }

        /// <summary>Point size for a text role on the 1920x1080 reference canvas.</summary>
        public float SizeFor(MenuTextRole role)
        {
            switch (role)
            {
                case MenuTextRole.Display: return displaySize;
                case MenuTextRole.Heading: return headingSize;
                case MenuTextRole.Hint: return hintSize;
                case MenuTextRole.Eyebrow: return eyebrowSize;
                case MenuTextRole.Value: return valueSize;
                case MenuTextRole.Title: return titleSize;
                case MenuTextRole.Subtitle: return subtitleSize;
                case MenuTextRole.Control: return controlSize;
                case MenuTextRole.Small: return smallSize;
                case MenuTextRole.Micro: return microSize;
                case MenuTextRole.Brand: return brandSize;
                case MenuTextRole.Clock: return clockSize;
                default: return bodySize;
            }
        }

        /// <summary>Default colour for a text role.</summary>
        public Color ColorFor(MenuTextRole role)
        {
            MenuPalette p = Palette;
            switch (role)
            {
                case MenuTextRole.Hint: return p.text2;
                case MenuTextRole.Eyebrow:
                case MenuTextRole.Small:
                case MenuTextRole.Micro:
                    return p.text3;
                default: return p.text1;
            }
        }

        /// <summary>Every size on the type scale.</summary>
        public float[] TypeScale()
        {
            return new[]
            {
                displaySize, headingSize, titleSize, subtitleSize, bodySize, controlSize, hintSize,
                eyebrowSize, smallSize, microSize, valueSize, brandSize, clockSize,
            };
        }

        /// <summary>Every font asset owned by the theme (null entries removed).</summary>
        public TMP_FontAsset[] Fonts()
        {
            var fonts = new System.Collections.Generic.List<TMP_FontAsset>();
            foreach (TMP_FontAsset font in new[] { bodyFont, bodyMediumFont, bodySemiBoldFont, displayFont, displayBoldFont, monoFont })
            {
                if (font != null && !fonts.Contains(font))
                {
                    fonts.Add(font);
                }
            }

            return fonts.ToArray();
        }
    }
}
