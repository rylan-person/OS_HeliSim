using System;
using System.Collections.Generic;
using System.IO;
using HeliSim.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Creates the menu theme's generated assets: TMP font assets for the bundled OFL fonts,
/// 9-sliced rounded sprites and the HeliSimMenuTheme asset. Idempotent: existing assets are
/// reused and only missing references are filled in.
/// </summary>
public static class MenuThemeAssets
{
    public const string ThemePath = "Assets/Level/Prefabs/UI/Theme/HeliSimMenuTheme.asset";
    public const string SpriteFolder = "Assets/Art/Textures/UI/Menu";
    public const string ControlSpritePath = SpriteFolder + "/MenuControl_r9.png";
    public const string PanelSpritePath = SpriteFolder + "/MenuPanel_r15.png";
    public const string FocusRingSpritePath = SpriteFolder + "/MenuFocusRing_r11.png";
    public const string ControlBorderSpritePath = SpriteFolder + "/MenuControlBorder_r9.png";
    public const string PanelBorderSpritePath = SpriteFolder + "/MenuPanelBorder_r15.png";
    public const string CircleSpritePath = SpriteFolder + "/MenuCircle.png";
    public const string PillBorderSpritePath = SpriteFolder + "/MenuPillBorder.png";
    public const string KeycapSpritePath = SpriteFolder + "/MenuKeycap_r7.png";

    // Canonical type scale (1.5 x the preview's CSS sizes at the 1920 x 1080 reference).
    private static readonly (string field, float size)[] TypeScale =
    {
        ("displaySize", 60f), ("headingSize", 39f), ("titleSize", 30f), ("subtitleSize", 27f),
        ("bodySize", 24f), ("controlSize", 22f), ("hintSize", 21f), ("eyebrowSize", 19f),
        ("smallSize", 18f), ("microSize", 16f), ("valueSize", 22f), ("brandSize", 48f), ("clockSize", 42f),
    };
    public const string IconFolder = SpriteFolder + "/Icons";
    private const string FallbackFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    private static readonly (string field, string ttf)[] Fonts =
    {
        ("bodyFont", "Assets/Art/Fonts/Barlow/Barlow-Regular.ttf"),
        ("bodyMediumFont", "Assets/Art/Fonts/Barlow/Barlow-Medium.ttf"),
        ("bodySemiBoldFont", "Assets/Art/Fonts/Barlow/Barlow-SemiBold.ttf"),
        ("displayFont", "Assets/Art/Fonts/BarlowCondensed/BarlowCondensed-SemiBold.ttf"),
        ("displayBoldFont", "Assets/Art/Fonts/BarlowCondensed/BarlowCondensed-Bold.ttf"),
        ("monoFont", "Assets/Art/Fonts/JetBrainsMono/JetBrainsMono-Medium.ttf"),
    };

    [MenuItem("HeliSim/Create Menu Theme Assets")]
    public static void CreateAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            throw new InvalidOperationException("The Editor must be idle to create menu theme assets.");
        }

        Directory.CreateDirectory(SpriteFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
        Sprite control = EnsureSprite(ControlSpritePath, 32, 9f, 0f, 10);
        Sprite panel = EnsureSprite(PanelSpritePath, 48, 15f, 0f, 16);
        Sprite ring = EnsureSprite(FocusRingSpritePath, 40, 11f, 2f, 13);
        Sprite controlBorder = EnsureSprite(ControlBorderSpritePath, 32, 9f, 1.5f, 10);
        Sprite panelBorder = EnsureSprite(PanelBorderSpritePath, 48, 15f, 1.5f, 16);
        Sprite circle = EnsureSprite(CircleSpritePath, 64, 32f, 0f, 31);
        Sprite pillBorder = EnsureSprite(PillBorderSpritePath, 64, 32f, 1.5f, 31);
        Sprite keycap = EnsureSprite(KeycapSpritePath, 32, 7.5f, 1.5f, 10, 3f);

        EnsureIconImportSettings();
        MenuTheme theme = AssetDatabase.LoadAssetAtPath<MenuTheme>(ThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<MenuTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }

        var serialized = new SerializedObject(theme);
        foreach ((string field, string ttf) in Fonts)
        {
            serialized.FindProperty(field).objectReferenceValue = EnsureFontAsset(ttf);
        }

        serialized.FindProperty("controlSprite").objectReferenceValue = control;
        serialized.FindProperty("panelSprite").objectReferenceValue = panel;
        serialized.FindProperty("focusRingSprite").objectReferenceValue = ring;
        serialized.FindProperty("controlBorderSprite").objectReferenceValue = controlBorder;
        serialized.FindProperty("panelBorderSprite").objectReferenceValue = panelBorder;
        serialized.FindProperty("circleSprite").objectReferenceValue = circle;
        serialized.FindProperty("pillBorderSprite").objectReferenceValue = pillBorder;
        serialized.FindProperty("keycapSprite").objectReferenceValue = keycap;
        foreach ((string field, float size) in TypeScale)
        {
            serialized.FindProperty(field).floatValue = size;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
    }

    /// <summary>Loads the project theme asset, or null if it has not been created.</summary>
    public static MenuTheme LoadTheme()
    {
        return AssetDatabase.LoadAssetAtPath<MenuTheme>(ThemePath);
    }

    /// <summary>Loads a 24 px stroke icon exported from the redesign preview (72 px source).</summary>
    public static Sprite LoadIcon(string name)
    {
        string path = IconFolder + "/Icon_" + name + ".png";
        EnsureIconImport(path);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            throw new FileNotFoundException("Menu icon missing: " + path);
        }

        return sprite;
    }

    private static void EnsureIconImportSettings()
    {
        if (!AssetDatabase.IsValidFolder(IconFolder))
        {
            return;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconFolder }))
        {
            EnsureIconImport(AssetDatabase.GUIDToAssetPath(guid));
        }
    }

    private static void EnsureIconImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null || (importer.textureType == TextureImporterType.Sprite && !importer.mipmapEnabled &&
                                 importer.textureCompression == TextureImporterCompression.Uncompressed))
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 300; // 72 px source drawn at 24 reference px.
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static TMP_FontAsset EnsureFontAsset(string ttfPath)
    {
        string assetPath = Path.ChangeExtension(ttfPath, null) + " SDF.asset";
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null)
        {
            return existing;
        }

        Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null)
        {
            throw new FileNotFoundException("Font not imported: " + ttfPath);
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, true);
        string name = Path.GetFileNameWithoutExtension(ttfPath) + " SDF";
        fontAsset.name = name;
        TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);
        if (fallback != null)
        {
            fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
        }

        AssetDatabase.CreateAsset(fontAsset, assetPath);
        fontAsset.atlasTextures[0].name = name + " Atlas";
        AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
        fontAsset.material.name = name + " Material";
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        // Pre-populate printable ASCII so first use does not stall on glyph rasterisation.
        fontAsset.TryAddCharacters(AsciiCharacters(), out _);
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        return fontAsset;
    }

    private static string AsciiCharacters()
    {
        var chars = new char[95 + 4];
        for (int i = 0; i < 95; i++)
        {
            chars[i] = (char)(32 + i);
        }

        chars[95] = '–'; // en dash
        chars[96] = '—'; // em dash
        chars[97] = '…'; // ellipsis
        chars[98] = '·'; // middle dot
        return new string(chars);
    }

    private static Sprite EnsureSprite(string path, int size, float radius, float stroke, int border, float bottomStroke = -1f)
    {
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, RenderRoundedRect(size, radius, stroke, bottomStroke));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        bool changed = importer.textureType != TextureImporterType.Sprite ||
                       importer.spriteBorder != new Vector4(border, border, border, border);
        if (changed)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>
    /// White anti-aliased rounded rectangle: filled when stroke is 0, else a ring. A bottom stroke
    /// larger than the side stroke draws a keycap edge.
    /// </summary>
    internal static byte[] RenderRoundedRect(int size, float radius, float stroke, float bottomStroke = -1f)
    {
        if (bottomStroke < stroke)
        {
            bottomStroke = stroke;
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        try
        {
            float half = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = RoundedBoxDistance(x + 0.5f - half, y + 0.5f - half, half, half, radius);
                    float alpha = Mathf.Clamp01(0.5f - distance);
                    if (stroke > 0f)
                    {
                        // Texture rows start at the bottom; the inner box is raised by the extra bottom stroke.
                        float lift = (bottomStroke - stroke) * 0.5f;
                        float inner = RoundedBoxDistance(x + 0.5f - half, y + 0.5f - half - lift,
                            half - stroke, half - stroke - lift, Mathf.Max(radius - stroke, 0f));
                        alpha -= Mathf.Clamp01(0.5f - inner);
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture.EncodeToPNG();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    public const string BackdropPath = SpriteFolder + "/MenuTopographicBackdrop.png";
    public const int BackdropWidth = 1920;
    public const int BackdropHeight = 1080;

    /// <summary>
    /// The main menu's static topographic backdrop (preview drawTopo): a radial navy glow with three
    /// sets of wavy contour rings, every fifth ring tinted accent. Rendered once and imported as a sprite.
    /// </summary>
    public static Sprite EnsureBackdrop()
    {
        if (!File.Exists(BackdropPath))
        {
            File.WriteAllBytes(BackdropPath, RenderTopographicBackdrop(BackdropWidth, BackdropHeight));
            AssetDatabase.ImportAsset(BackdropPath, ImportAssetOptions.ForceSynchronousImport);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(BackdropPath);
        if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled ||
            importer.textureCompression != TextureImporterCompression.CompressedHQ)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(BackdropPath);
    }

    internal static byte[] RenderTopographicBackdrop(int width, int height)
    {
        // Preview coordinates are CSS pixels of a 1280 x 720 stage; Unity's reference is 1.5x.
        float scale = width / 1280f;
        var glow = new Color(0x10 / 255f, 0x20 / 255f, 0x2C / 255f);
        Color background = MenuPalette.Default.bg0;
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float cssY = (height - 1 - y) + 0.5f; // texture rows start at the bottom
            for (int x = 0; x < width; x++)
            {
                float dx = (x + 0.5f - 0.85f * width) / (1.2f * width);
                float dy = (cssY - 0.1f * height) / (0.9f * height);
                float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 0.6f);
                Color c = Color.Lerp(glow, background, t);
                c.a = 1f;
                pixels[y * width + x] = c;
            }
        }

        var minor = new Color(120 / 255f, 160 / 255f, 180 / 255f, 0.06f);
        var major = new Color(60 / 255f, 200 / 255f, 186 / 255f, 0.13f);
        var peaks = new[]
        {
            (cx: width * 0.78f, cy: height * 0.3f, rings: 20, s: 1.3f),
            (cx: width * 0.55f, cy: height * 0.95f, rings: 14, s: 1f),
            (cx: width * 1.02f, cy: height * 0.85f, rings: 12, s: 0.8f),
        };
        var coverage = new float[width * height];
        for (int pi = 0; pi < peaks.Length; pi++)
        {
            (float cx, float cy, int rings, float s) = peaks[pi];
            for (int i = 1; i <= rings; i++)
            {
                bool isMajor = i % 5 == 0;
                float lineWidth = (isMajor ? 1.1f : 0.8f) * scale;
                System.Array.Clear(coverage, 0, coverage.Length);
                float radius = i * 26f * s * scale;
                Vector2 previous = default;
                const int Steps = 180;
                for (int step = 0; step <= Steps; step++)
                {
                    float a = step * Mathf.PI * 2f / Steps;
                    float rr = radius + (Mathf.Sin(a * 3f + pi + i * 0.35f) * (6f + i * 1.6f) +
                                         Mathf.Sin(a * 5f - i * 0.2f) * (3f + i * 0.8f)) * scale;
                    var point = new Vector2(cx + Mathf.Cos(a) * rr * 1.25f, cy + Mathf.Sin(a) * rr * 0.8f);
                    if (step > 0)
                    {
                        StrokeSegment(coverage, width, height, previous, point, lineWidth);
                    }

                    previous = point;
                }

                Color line = isMajor ? major : minor;
                for (int y = 0; y < height; y++)
                {
                    int row = (height - 1 - y) * width; // coverage is in top-down CSS rows
                    for (int x = 0; x < width; x++)
                    {
                        float cover = coverage[y * width + x];
                        if (cover <= 0f) continue;
                        int index = row + x;
                        Color under = pixels[index];
                        float alpha = line.a * cover;
                        pixels[index] = new Color(Mathf.Lerp(under.r, line.r, alpha), Mathf.Lerp(under.g, line.g, alpha),
                            Mathf.Lerp(under.b, line.b, alpha), 1f);
                    }
                }
            }
        }

        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            texture.SetPixels(pixels);
            texture.Apply();
            return texture.EncodeToPNG();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    /// <summary>Anti-aliased line coverage, kept as the maximum so overlapping segment ends don't double up.</summary>
    private static void StrokeSegment(float[] coverage, int width, int height, Vector2 a, Vector2 b, float lineWidth)
    {
        float half = lineWidth * 0.5f;
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - half - 1f));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + half + 1f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - half - 1f));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + half + 1f));
        Vector2 ab = b - a;
        float lengthSquared = Mathf.Max(ab.sqrMagnitude, 1e-6f);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
                float distance = (p - (a + ab * t)).magnitude;
                float cover = Mathf.Clamp01(half + 0.5f - distance);
                int index = y * width + x;
                if (cover > coverage[index]) coverage[index] = cover;
            }
        }
    }

    private static float RoundedBoxDistance(float px, float py, float halfWidth, float halfHeight, float radius)
    {
        float qx = Mathf.Abs(px) - (halfWidth - radius);
        float qy = Mathf.Abs(py) - (halfHeight - radius);
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
    }
}
