using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class AlmanacManager
{
    [SerializeField] private RectTransform illustrationNote;
    [SerializeField] private RawImage illustrationDrawing;
    [SerializeField] private TextMeshProUGUI illustrationCaption;
    private bool restoreIllustration;

    private void EnsureIllustrationNote()
    {
        if (illustrationNote != null && illustrationNote.parent == knowledgePanel.transform) return;
        var existing = knowledgePanel.transform.Find("Attached field note");
        if (existing != null)
        {
            illustrationNote = existing as RectTransform;
            illustrationDrawing = existing.GetComponentInChildren<RawImage>(true);
            illustrationCaption = existing.GetComponentInChildren<TextMeshProUGUI>(true);
            if (illustrationDrawing != null && illustrationCaption != null) return;
        }
        var note = CreatePanel("Attached field note", knowledgePanel.transform, new Color32(255, 244, 204, 255));
        illustrationNote = note.GetComponent<RectTransform>();
        SetRect(illustrationNote, Vector2.one * .5f, Vector2.one * .5f, new Vector2(-365, -65), new Vector2(568, 465));
        illustrationNote.localRotation = Quaternion.Euler(0, 0, -1.4f);
        note.GetComponent<Image>().raycastTarget = false;
        var shadow = note.AddComponent<Shadow>();
        shadow.effectColor = new Color(.18f, .12f, .05f, .25f);
        shadow.effectDistance = new Vector2(7, -9);
        var drawing = new GameObject("Technique illustration", typeof(RectTransform), typeof(RawImage));
        drawing.transform.SetParent(note.transform, false);
        illustrationDrawing = drawing.GetComponent<RawImage>();
        illustrationDrawing.raycastTarget = false;
        SetRect(drawing.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, new Vector2(0, 15), new Vector2(536, 402));
        illustrationCaption = BookText(note.transform, "Field note caption", new Vector2(0, -211), new Vector2(510, 28), 17);
        illustrationCaption.fontSizeMin = 15;
        illustrationCaption.fontStyle = FontStyles.Italic;
        illustrationCaption.color = new Color32(105, 78, 40, 255);
        for (int i = 0; i < 2; i++)
        {
            var tape = CreatePanel("Paper tape", note.transform, new Color32(222, 196, 139, 200));
            tape.GetComponent<Image>().raycastTarget = false;
            var rect = tape.GetComponent<RectTransform>();
            SetRect(rect, Vector2.one * .5f, Vector2.one * .5f, new Vector2(i == 0 ? -160 : 165, 229), new Vector2(122, 34));
            rect.localRotation = Quaternion.Euler(0, 0, i == 0 ? 8 : -7);
        }
    }

    private void StyleBookArticle(TextMeshProUGUI text)
    {
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSharedMaterial = text.font.material;
        text.color = new Color32(54, 46, 38, 255);
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableAutoSizing = true;
        text.fontSizeMin = 21;
        text.fontSizeMax = 26;
        text.fontSize = 26;
        text.lineSpacing = 5;
        text.paragraphSpacing = 10;
        text.characterSpacing = .15f;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    private static string FormatBookArticle(string body)
    {
        body = Regex.Replace(body, @"(?m)^\s*LEVEL \d+ TECHNIQUE\s*\n*", "");
        body = Regex.Replace(body, @"(?m)^[-•] +", "•  ");
        return Regex.Replace(body.Trim(), @"\[([^\]\r\n]+)\]", "<b><color=#765329>[$1]</color></b>");
    }

    private static Sprite ShopEquipmentPicture(KnowledgeEntry entry)
    {
        if (entry.category != "Equipment") return null;
        switch (entry.id)
        {
            case "director_tablet": return EquipmentIconArt.Get("DIRECTOR TABLET");
            case "nony_fx_camera": return EquipmentIconArt.Get("NONY FX");
            case "level_2_camera": return EquipmentIconArt.Get("LEVEL 2 CAMERA");
            case "led_panel": return EquipmentIconArt.Get("160 LED PANEL");
            case "level_3_soft_light": return EquipmentIconArt.Get("LEVEL 3 SOFT LIGHT");
            case "light_strip": return EquipmentIconArt.Get("LIGHT STRIP");
            case "sd_card": return EquipmentIconArt.Get("SD CARD");
            case "actor_megaphone": return EquipmentIconArt.Get("DIRECTOR MEGAPHONE");
            default: return EquipmentIconArt.Get(entry.title);
        }
    }

    private void ApplyIllustratedArticle(KnowledgeEntry entry, string body)
    {
        EnsureIllustrationNote();
        StyleBookArticle(bookLeftText);
        StyleBookArticle(bookRightText);
        bookHeading.font = TMP_Settings.defaultFontAsset;
        bookHeading.fontSharedMaterial = bookHeading.font.material;
        bookHeading.fontStyle = FontStyles.Bold;
        bookHeading.fontSizeMax = 32;
        bookHeading.fontSizeMin = 28;
        bookHeading.characterSpacing = 3;
        bookHeading.color = new Color32(118, 84, 42, 255);
        bookEntryTitle.font = TMP_Settings.defaultFontAsset;
        bookEntryTitle.fontSharedMaterial = bookEntryTitle.font.material;
        bookEntryTitle.fontStyle = FontStyles.Bold;
        bookEntryTitle.fontSizeMin = 24;
        bookEntryTitle.fontSizeMax = 34;
        bookEntryTitle.color = new Color32(42, 48, 50, 255);
        var title = Regex.Replace(entry.title, @"^(TECHNIQUE|EQUIPMENT|CAMERA UNLOCK|LEVEL \d+)\s*[-:]\s*", "");
        bookEntryTitle.text = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(title.ToLowerInvariant());
        // Only the same equipment artwork used by the shop is allowed here.
        // Other articles stay text-only; never fall back to generated illustrations.
        Sprite shopPicture = ShopEquipmentPicture(entry);
        Texture2D texture = shopPicture != null ? shopPicture.texture : null;
        restoreIllustration = false;
        illustrationDrawing.texture = texture;
        illustrationNote.gameObject.SetActive(texture != null);
        bookLeftText.gameObject.SetActive(texture == null);
        if (texture != null)
        {
            illustrationDrawing.texture = texture;
            Rect pictureRect = shopPicture.rect;
            illustrationDrawing.uvRect = new Rect(pictureRect.x / texture.width, pictureRect.y / texture.height, pictureRect.width / texture.width, pictureRect.height / texture.height);
            float ratio = pictureRect.width / pictureRect.height;
            illustrationDrawing.rectTransform.sizeDelta = ratio >= 536f / 402f
                ? new Vector2(536, 536 / ratio) : new Vector2(402 * ratio, 402);
            illustrationCaption.text = "SHOP EQUIPMENT";
            bookRightText.text = FormatBookArticle(body);
        }
        else
        {
            bookLeftText.text = FormatBookArticle(bookLeftText.text);
            bookRightText.text = FormatBookArticle(bookRightText.text);
        }
    }

    private void RestoreIllustrationAfterTurn()
    {
        if (restoreIllustration && illustrationNote != null) illustrationNote.gameObject.SetActive(true);
        restoreIllustration = false;
    }

    private void CopyIllustrationForTurn(Transform paper, bool leftPage)
    {
        if (!leftPage || illustrationNote == null || !illustrationNote.gameObject.activeSelf) return;
        var copy = Instantiate(illustrationNote, illustrationNote.parent);
        copy.SetParent(paper, true);
        illustrationNote.gameObject.SetActive(false);
        restoreIllustration = true;
    }
}
