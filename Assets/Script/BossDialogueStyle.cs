using TMPro;
using UnityEngine;
using System.Text.RegularExpressions;

// Shared typography for tutorial, contract and recovery Boss conversations.
public static class BossDialogueStyle
{
    private static TMP_FontAsset font;
    private static Material outlinedMaterial;
    private static Material dialogueMaterial;
    public const string ControlColor = "#B00020";
    private static readonly Color DialogueInk = new Color32(20, 16, 14, 255);
    private const string ContinueHint = "PRESS <color=" + ControlColor + ">[SPACE / LMB]</color> TO CONTINUE";
    private static readonly Regex RichTextParts = new Regex("(<[^>]+>)");
    private static readonly Regex Controls = new Regex(@"\[[^\]\r\n]+\]|[""'“”‘’]\s*(?:[A-Z]|F\d{1,2})\s*[""'“”‘’]|\b(?:ADD TO CART|ADD WALL|COLOR GRADE|BEFORE / AFTER|BRANDING|CONFIRM|SELECT|EXPORT|PLAY|RESET|SKIP|RETRY CONTRACT|NEW GAME|KEEP WORKING)\b");

    // Preserve authored emphasis, and catch plain key/button instructions in newer lessons.
    public static string HighlightControls(string message)
    {
        if (string.IsNullOrEmpty(message)) return message ?? string.Empty;
        var parts = RichTextParts.Split(message);
        int colorDepth = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("<color=", System.StringComparison.OrdinalIgnoreCase))
            {
                parts[i] = "<color=" + ControlColor + ">";
                colorDepth++;
            }
            else if (parts[i].Equals("</color>", System.StringComparison.OrdinalIgnoreCase))
                colorDepth = Mathf.Max(0, colorDepth - 1);
            else if (!parts[i].StartsWith("<") && colorDepth == 0)
                parts[i] = Controls.Replace(parts[i], match => "<color=" + ControlColor + ">" + match.Value + "</color>");
        }
        return string.Concat(parts);
    }

    public static void UpdateContinueHint(TMP_Text text, bool visible)
    {
        if (text == null) return;
        if (text.text != ContinueHint || text.fontSizeMax != 22f)
        {
            Apply(text, true);
            text.text = ContinueHint;
            text.fontSize = text.fontSizeMax = 22f;
            text.fontSizeMin = 18f;
            text.fontSharedMaterial = dialogueMaterial;
            text.color = DialogueInk;
        }
        text.gameObject.SetActive(visible);
        // Slow, gentle pulse: remains readable and works during paused tutorial time.
        text.alpha = visible ? .8f + .2f * (.5f + .5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 1.25f)) : 1f;
    }

    public static void Apply(TMP_Text text, bool button = false)
    {
        if (text == null) return;
        if (font == null)
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF") ??
                Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null)
        {
            text.font = font;
            if (outlinedMaterial == null)
            {
                outlinedMaterial = new Material(font.material) { name = "Boss white text - black outline" };
                outlinedMaterial.EnableKeyword("OUTLINE_ON");
                outlinedMaterial.SetColor("_FaceColor", Color.white);
                outlinedMaterial.SetColor("_OutlineColor", Color.black);
                outlinedMaterial.SetFloat("_OutlineWidth", .38f);
                outlinedMaterial.SetFloat("_OutlineSoftness", 0f);
                outlinedMaterial.EnableKeyword("UNDERLAY_ON");
                outlinedMaterial.SetColor("_UnderlayColor", Color.black);
                outlinedMaterial.SetFloat("_UnderlayOffsetX", 1f);
                outlinedMaterial.SetFloat("_UnderlayOffsetY", -1f);
                outlinedMaterial.SetFloat("_UnderlaySoftness", .1f);
            }
            if (dialogueMaterial == null)
            {
                // The authored paper is light: solid dark glyphs stay readable without
                // relying on a thin white-on-paper outline. Keep white outlined buttons.
                dialogueMaterial = new Material(font.material) { name = "Boss solid dialogue ink" };
                dialogueMaterial.SetColor("_FaceColor", Color.white);
                dialogueMaterial.SetFloat("_FaceDilate", .08f);
                dialogueMaterial.SetFloat("_OutlineWidth", 0f);
                dialogueMaterial.SetFloat("_OutlineSoftness", 0f);
                dialogueMaterial.DisableKeyword("OUTLINE_ON");
                dialogueMaterial.DisableKeyword("UNDERLAY_ON");
                dialogueMaterial.DisableKeyword("UNDERLAY_INNER");
            }
            text.fontSharedMaterial = button ? outlinedMaterial : dialogueMaterial;
        }
        // Roboto's bold face already provides the weight; synthetic bold makes it too heavy.
        text.fontStyle = FontStyles.Normal;
        text.color = button ? Color.white : DialogueInk;
        text.enableVertexGradient = false;
        text.richText = true;
        text.alignment = TextAlignmentOptions.Center;
        text.characterSpacing = 0;
        text.wordSpacing = 0;
        text.lineSpacing = button ? 0 : 4;
        text.paragraphSpacing = 0;
        text.margin = button ? new Vector4(6, 2, 6, 2) : new Vector4(12, 8, 12, 8);
        text.enableAutoSizing = true;
        text.fontSize = button ? 28 : 44;
        text.fontSizeMin = button ? 20 : 32;
        text.fontSizeMax = button ? 28 : 44;
        text.raycastTarget = false;
        text.UpdateMeshPadding();
    }
}
