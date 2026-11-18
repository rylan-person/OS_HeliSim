using System;
using UnityEngine;

namespace HeliSim.UI.Theme
{
    /// <summary>
    /// Colour tokens for both menus. Teal marks active/selectable, amber marks caution and
    /// red is reserved for destructive actions. Defaults match the approved redesign preview.
    /// </summary>
    [Serializable]
    public sealed class MenuPalette
    {
        /// <summary>Alpha applied to disabled controls so they never resemble a highlighted state.</summary>
        public const float DisabledAlpha = 0.4f;

        [Header("Surfaces")]
        public Color bg0 = Hex(0x070C11);
        public Color bg1 = Hex(0x0B131A);
        public Color bg2 = Hex(0x101A23);
        public Color bg3 = Hex(0x16232E);
        public Color bg4 = Hex(0x1D2D3A);
        public Color line = Hex(0x1F2E3A);
        public Color line2 = Hex(0x2C3F4E);
        public Color scrim = new Color(4f / 255f, 8f / 255f, 12f / 255f, 0.66f);

        [Header("Text")]
        public Color text1 = Hex(0xE6EDF2);
        public Color text2 = Hex(0x9AABB8);
        public Color text3 = Hex(0x637684);

        [Header("Signal")]
        public Color accent = Hex(0x3CC8BA);
        public Color accentHi = Hex(0x63D9CD);
        public Color accentInk = Hex(0x03211E);
        public Color caution = Hex(0xF0A93C);
        public Color warning = Hex(0xEF5A5A);
        public Color ok = Hex(0x52C98C);

        /// <summary>Shared default palette used when a component has no theme assigned.</summary>
        public static MenuPalette Default { get; } = new MenuPalette();

        /// <summary>Returns <paramref name="color"/> with its alpha replaced.</summary>
        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>Returns <paramref name="color"/> with its alpha scaled for the disabled state.</summary>
        public static Color Disabled(Color color)
        {
            color.a *= DisabledAlpha;
            return color;
        }

        /// <summary>Every opaque token, used by asset checks to validate serialized colours.</summary>
        public Color[] Tokens()
        {
            return new[]
            {
                bg0, bg1, bg2, bg3, bg4, line, line2, text1, text2, text3,
                accent, accentHi, accentInk, caution, warning, ok, Color.white, Color.black,
            };
        }

        /// <summary>True when <paramref name="color"/> is a palette token at any alpha (or fully transparent).</summary>
        public bool IsToken(Color color, float tolerance = 1.5f / 255f)
        {
            if (color.a <= tolerance)
            {
                return true;
            }

            foreach (Color token in Tokens())
            {
                if (Mathf.Abs(token.r - color.r) <= tolerance &&
                    Mathf.Abs(token.g - color.g) <= tolerance &&
                    Mathf.Abs(token.b - color.b) <= tolerance)
                {
                    return true;
                }
            }

            return Mathf.Abs(scrim.r - color.r) <= tolerance &&
                   Mathf.Abs(scrim.g - color.g) <= tolerance &&
                   Mathf.Abs(scrim.b - color.b) <= tolerance;
        }

        private static Color Hex(int rgb)
        {
            return new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 255);
        }
    }
}
