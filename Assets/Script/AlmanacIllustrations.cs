using System.Collections.Generic;
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
    private readonly Dictionary<string, Texture2D> illustrationCache = new Dictionary<string, Texture2D>();
    private static readonly Dictionary<string, string> IllustrationFiles = new Dictionary<string, string>
    {
        {"director_tablet", "Almanac_L1_DirectorTablet"},
        {"led_panel", "Almanac_L1_LEDPanel"},
        {"sd_card", "Almanac_L1_SDCard"},
        {"actor_megaphone", "Almanac_L4_DirectorMegaphone"},
        {"creative_brief", "Almanac_L5_CreativeBrief"},
        {"visual_hierarchy", "Almanac_L5_VisualHierarchy"},
        {"quality_control", "Almanac_L5_QualityReview"},
        {"nony_fx_camera", "00_Camera_Illustration"},
        {"level_2_camera", "00_Camera_Illustration"},
        {"set_building_technique", "L1_Set_Building_Product_Staging"},
        {"center_framing", "L1_Center_Framing"},
        {"basic_product_lighting", "L1_Basic_Product_Lighting"},
        {"quiet_movement", "L1_Quiet_Movement_On_Set"},
        {"recording_technique", "L1_Stable_10_Second_Recording"},
        {"post_production_technique", "L1_Trimming_Branding_Color"},
        {"rule_of_thirds", "L2_Rule_of_Thirds"},
        {"three_point_lighting", "L2_Three_Point_Lighting"},
        {"product_separation", "L2_Product_Backdrop_Separation"},
        {"commercial_color_grading", "L2_Commercial_Color_Grading"},
        {"advertising_post_production", "L2_Intro_Outro"},
        {"automotive_staging", "L3_Automotive_Staging_Composition"},
        {"camera_white_balance", "L3_Camera_White_Balance"},
        {"soft_light_technique", "L3_Soft_Light_Reflective_Surfaces"},
        {"vehicle_rim_lighting", "L3_Smooth_Vehicle_Camera_Move"},
        {"camera_exposure", "L4_Camera_Exposure"},
        {"coffee_story_workflow", "L4_Three_Beat_Coffee_Story"},
        {"hiring_and_posing_actors", "L4_Directing_Blocking_Actors"},
        {"lifestyle_staging", "L4_Performance_Space"},
        {"motivated_lighting", "L4_Story_Mood_Motivated_Lighting"},
        {"shot_coverage", "L4_Performance_Beats"},
        {"screen_continuity", "L4_Elliptical_Editing"},
        {"warm_commercial_grade", "L4_Closing_Brand_Rhythm"}
    };

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
        // Leave the Director Tablet's illustration space empty until replacement art is ready.
        if (entry.id == "director_tablet")
        {
            illustrationNote.gameObject.SetActive(false);
            illustrationDrawing.texture = null;
            bookLeftText.gameObject.SetActive(false);
            bookRightText.text = FormatBookArticle(body);
            restoreIllustration = false;
            return;
        }
        string file;
        Texture2D texture = null;
        if (IllustrationFiles.TryGetValue(entry.id, out file))
        {
            if (!illustrationCache.TryGetValue(file, out texture))
            {
                texture = Resources.Load<Texture2D>("AlmanacIllustrations/" + file);
                illustrationCache[file] = texture;
            }
        }
        illustrationNote.gameObject.SetActive(texture != null);
        bookLeftText.gameObject.SetActive(texture == null);
        if (texture != null)
        {
            illustrationDrawing.texture = texture;
            float ratio = (float)texture.width / texture.height;
            illustrationDrawing.rectTransform.sizeDelta = ratio >= 536f / 402f
                ? new Vector2(536, 536 / ratio) : new Vector2(402 * ratio, 402);
            illustrationCaption.text = "FIELD NOTES";
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
