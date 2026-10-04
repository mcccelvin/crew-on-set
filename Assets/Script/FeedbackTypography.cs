using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Bundled fonts, never OS-installed fonts: the same pen lettering in Editor and builds.
public static class FeedbackTypography
{
    private static TMP_FontAsset notes, headings;
    public static TMP_FontAsset Notes => notes != null ? notes : notes = Create("FeedbackFonts/Kalam-Regular");
    public static TMP_FontAsset Headings => headings != null ? headings : headings = Create("ContractFonts/Allura-Regular");

    private static TMP_FontAsset Create(string path)
    {
        var source = Resources.Load<Font>(path);
        if (source == null) return TMP_Settings.defaultFontAsset;
        var font = TMP_FontAsset.CreateFontAsset(source);
        font.name = "Feedback pen lettering / " + source.name;
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        font.isMultiAtlasTexturesEnabled = true;
        font.fallbackFontAssetTable = new List<TMP_FontAsset>();
        var fallback = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF");
        if (fallback != null) font.fallbackFontAssetTable.Add(fallback);
        if (TMP_Settings.defaultFontAsset != null && TMP_Settings.defaultFontAsset != fallback)
            font.fallbackFontAssetTable.Add(TMP_Settings.defaultFontAsset);
        return font;
    }

    public static string Heading(string value) => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase((value ?? "").ToLowerInvariant());
    public static void Apply(TMP_Text text, bool calligraphy = false)
    {
        var font = calligraphy ? Headings : Notes;
        if (font == null) return;
        text.font = font; text.fontSharedMaterial = font.material;
        text.fontStyle = FontStyles.Normal;
    }
}
