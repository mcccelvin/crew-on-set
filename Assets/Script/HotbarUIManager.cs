using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class HotbarUIManager : MonoBehaviour
{
    [Header("UI References")]
    public Image[] slotBackgrounds;
    public TextMeshProUGUI[] slotTexts;
    public Image[] slotIcons;

    [Header("Colors")]
    public Color activeColor = new Color(1f, 1f, 1f, 0.8f);
    public Color inactiveColor = new Color(0f, 0f, 0f, 0.4f);

    [Header("Equipment Guide")]
    public TextMeshProUGUI equipmentGuideText;

    private string currentGuideText;
    [SerializeField] private RectTransform equipmentControlsRoot;
    [SerializeField] private List<TextMeshProUGUI> controlLabels = new List<TextMeshProUGUI>();
    [SerializeField] private List<TextMeshProUGUI> controlKeys = new List<TextMeshProUGUI>();
    private bool showingEquipment;
    private static readonly Regex ControlPattern = new Regex(@"\[([^\]]+)\]\s*([^\[]*)");
    private bool isInteractionPrompt;
    [SerializeField] private GameObject directorPromptRoot;
    [SerializeField] private RectTransform directorPromptRect;
    [SerializeField] private CanvasGroup directorPromptGroup;
    [SerializeField] private Image directorPromptAccent;
    [SerializeField] private TextMeshProUGUI directorPromptKeyText;
    [SerializeField] private TextMeshProUGUI directorPromptTitleText;
    [SerializeField] private TextMeshProUGUI directorPromptActionText;
    private float directorPromptVisibility;

#if UNITY_EDITOR
    public void BakeHierarchyUI()
    {
        CreateDirectorTabletPrompt();
        // A reusable pool covers the largest equipment guide without runtime layout creation.
        UpdateEquipmentGuide("[LMB] Select actor");
        if (equipmentControlsRoot != null)
        {
            while (controlLabels.Count < 16) CreateControlRow(controlLabels.Count);
            equipmentControlsRoot.sizeDelta = new Vector2(310, 628);
            equipmentControlsRoot.gameObject.SetActive(false);
        }
        foreach (var image in slotBackgrounds)
            if (image != null && image.GetComponent<Outline>() == null) image.gameObject.AddComponent<Outline>();
    }
#endif

    private void Start()
    {
        // Override the old translucent colors serialized into the studio scenes.
        activeColor = new Color32(83, 63, 40, 245);
        inactiveColor = new Color32(24, 22, 20, 235);
        foreach (var text in slotTexts)
            if (text != null) { text.color = Color.white; text.fontStyle |= FontStyles.Bold; }
        HighlightSlot(0);

        for (int i = 0; i < slotIcons.Length; i++)
        {
            if (slotIcons[i] != null) slotIcons[i].gameObject.SetActive(false);
        }

        CreateDirectorTabletPrompt();

        UpdateGuideText("");
    }

    private void Update()
    {
        if (directorPromptGroup == null || directorPromptRect == null) return;

        float target = isInteractionPrompt ? 1f : 0f;
        directorPromptVisibility = Mathf.MoveTowards(directorPromptVisibility, target, Time.unscaledDeltaTime * 7f);
        float eased = 1f - Mathf.Pow(1f - directorPromptVisibility, 3f);
        directorPromptGroup.alpha = eased;
        directorPromptRect.localScale = Vector3.one;

        if (directorPromptAccent != null && isInteractionPrompt)
        {
            directorPromptAccent.color = CrewPaperStyle.Gold;
        }
    }

    public void HighlightSlot(int activeIndex)
    {
        for (int i = 0; i < slotBackgrounds.Length; i++)
        {
            if (slotBackgrounds[i] != null)
            {
                slotBackgrounds[i].color = (i == activeIndex) ? activeColor : inactiveColor;
                var border = slotBackgrounds[i].GetComponent<Outline>();
                if (border == null) border = slotBackgrounds[i].gameObject.AddComponent<Outline>();
                border.effectColor = new Color32(235,181,73,255);
                border.effectDistance = new Vector2(2,-2);
                border.enabled = i == activeIndex;
            }
        }
    }

    public void UpdateSlot(int index, string itemName, Sprite itemIcon)
    {
        if (index >= 0 && index < slotTexts.Length && slotTexts[index] != null)
        {
            slotTexts[index].text = (index + 1).ToString();

            if (index < slotIcons.Length && slotIcons[index] != null)
            {
                if (itemIcon != null)
                {
                    slotIcons[index].sprite = itemIcon;
                    slotIcons[index].gameObject.SetActive(true);
                }
                else
                {
                    slotIcons[index].sprite = null;
                    slotIcons[index].gameObject.SetActive(false);
                }
            }
        }
    }

    public void UpdateGuideText(string newText)
    {
        newText = newText ?? "";
        if (newText.StartsWith("LIGHT_CONTROL_ROWS|")) { UpdateEquipmentGuide(newText); return; }
        if (currentGuideText == newText && !showingEquipment) return;
        currentGuideText = newText;
        showingEquipment = false;
        if (equipmentControlsRoot != null) equipmentControlsRoot.gameObject.SetActive(false);
        isInteractionPrompt = TryBuildInteractionPrompt(newText, out string key, out string title, out string action);

        if (isInteractionPrompt)
        {
            if (directorPromptKeyText != null) directorPromptKeyText.text = key;
            if (directorPromptTitleText != null) directorPromptTitleText.text = title;
            if (directorPromptActionText != null) directorPromptActionText.text = action;
            FitInteractionPrompt();
        }

        if (equipmentGuideText != null)
        {
            equipmentGuideText.text = isInteractionPrompt ? "" : newText;
        }
    }

    // Equipment hints have their own route so single-key controls never become interaction popups.
    public void UpdateEquipmentGuide(string controls)
    {
        controls = controls ?? "";
        if (showingEquipment && currentGuideText == controls) return;
        currentGuideText = controls;
        showingEquipment = true;
        isInteractionPrompt = false;
        if (equipmentGuideText == null) return;
        equipmentGuideText.text = "";
        if (equipmentControlsRoot == null)
        {
            var canvas = equipmentGuideText.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            equipmentControlsRoot = new GameObject("Equipment Key Guide", typeof(RectTransform)).GetComponent<RectTransform>();
            equipmentControlsRoot.SetParent(canvas.transform, false);
            equipmentControlsRoot.anchorMin = equipmentControlsRoot.anchorMax = equipmentControlsRoot.pivot = new Vector2(1, .5f);
            equipmentControlsRoot.anchoredPosition = new Vector2(-28, 0);
        }
        string source = controls;
        if (source.StartsWith("LIGHT_CONTROL_ROWS|"))
        {
            string[] fields = source.Split('|');
            source = "[LMB] Toggle power | [SCROLL] Intensity | [ARROWS] Tilt | [Q UP / E DOWN] Height +" + fields[1] + " m | [G] Drop";
            if (controls.Contains("ADVANCED")) source += " | [Z / X] Temperature | [V / B] Diffusion";
        }
        var matches = ControlPattern.Matches(source);
        var panel = equipmentControlsRoot.GetComponent<Image>();
        if (panel == null) panel = equipmentControlsRoot.gameObject.AddComponent<Image>();
        CrewPaperStyle.Card(panel);
        var headingRect = equipmentControlsRoot.Find("Controls Heading") as RectTransform;
        if (headingRect == null)
        {
            var heading = CreatePromptText("Controls Heading", equipmentControlsRoot,
                "EQUIPMENT CONTROLS", 16, TextAlignmentOptions.MidlineLeft);
            headingRect = heading.rectTransform;
            heading.fontStyle = FontStyles.Bold;
        }
        SetRect(headingRect, new Vector2(16, -12), new Vector2(328, 24), new Vector2(0, 1));
        var headingText = headingRect.GetComponent<TMP_Text>();
        headingText.text = controls.IndexOf("Viewfinder", System.StringComparison.OrdinalIgnoreCase) >= 0
            ? "CAMERA CONTROLS" : "EQUIPMENT CONTROLS";
        headingText.color = CrewPaperStyle.MutedInk;
        for (int i = 0; i < matches.Count; i++)
        {
            if (i == controlLabels.Count) CreateControlRow(i);
            var label = controlLabels[i];
            var key = controlKeys[i];
            label.transform.parent.gameObject.SetActive(true);
            label.text = matches[i].Groups[2].Value.Trim(' ', '|', '\n', '\r').ToUpperInvariant();
            key.text = matches[i].Groups[1].Value.Trim().ToUpperInvariant();
            var row = (RectTransform)label.transform.parent;
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(0, 1);
            row.anchoredPosition = new Vector2(16, -44 - i * 38);
            row.sizeDelta = new Vector2(328, 28);
            label.color = CrewPaperStyle.Ink; label.alignment = TextAlignmentOptions.MidlineLeft;
            label.margin = Vector4.zero;
            var shadow = label.GetComponent<Shadow>(); if (shadow != null) shadow.enabled = false;
            key.color = CrewPaperStyle.Ink; key.margin = Vector4.zero;
            var badge = (RectTransform)key.transform.parent;
            CrewPaperStyle.Card(badge.GetComponent<Image>());
            badge.GetComponent<Image>().color = CrewPaperStyle.Gold;
            float width = Mathf.Clamp(key.GetPreferredValues(key.text).x + 20, 40, 144);
            badge.sizeDelta = new Vector2(width, 28);
            label.rectTransform.offsetMax = new Vector2(-width - 12, 0);
        }
        for (int i = matches.Count; i < controlLabels.Count; i++) controlLabels[i].transform.parent.gameObject.SetActive(false);
        equipmentControlsRoot.sizeDelta = new Vector2(360, 44 + Mathf.Max(0, matches.Count * 38 - 10) + 16);
        equipmentControlsRoot.gameObject.SetActive(matches.Count > 0);
    }

    private void CreateControlRow(int index)
    {
        var row = new GameObject("Control " + index, typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(equipmentControlsRoot, false);
        row.anchorMin = row.anchorMax = row.pivot = new Vector2(1, 1);
        row.anchoredPosition = new Vector2(0, -index * 40);
        row.sizeDelta = new Vector2(310, 28);
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(row, false);
        label.font = equipmentGuideText.font;
        label.fontSize = 18; label.fontStyle = FontStyles.Bold;
        label.color = Color.white; label.alignment = TextAlignmentOptions.MidlineRight;
        label.enableWordWrapping = false; label.enableAutoSizing = true;
        label.fontSizeMin = 14; label.fontSizeMax = 18; label.raycastTarget = false;
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        var shadow = label.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .65f); shadow.effectDistance = new Vector2(1, -1);
        var badge = new GameObject("Key Badge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        badge.transform.SetParent(row, false); badge.color = Color.white; badge.raycastTarget = false;
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = badge.rectTransform.pivot = new Vector2(1, .5f);
        var key = new GameObject("Key", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        key.transform.SetParent(badge.transform, false); key.font = equipmentGuideText.font;
        key.fontSize = 16; key.fontStyle = FontStyles.Bold; key.color = new Color32(30, 27, 24, 255);
        key.alignment = TextAlignmentOptions.Center; key.enableWordWrapping = false; key.raycastTarget = false;
        key.rectTransform.anchorMin = Vector2.zero; key.rectTransform.anchorMax = Vector2.one;
        key.rectTransform.offsetMin = key.rectTransform.offsetMax = Vector2.zero;
        controlLabels.Add(label); controlKeys.Add(key);
    }

    private static bool TryBuildInteractionPrompt(string source, out string key, out string title, out string action)
    {
        key = "E";
        title = "INTERACT";
        action = "PRESS E TO INTERACT";

        if (string.IsNullOrWhiteSpace(source)) return false;

        if (source.Contains("ENTER DIRECTOR TABLET"))
        {
            title = "DIRECTOR TABLET";
            action = "PRESS E TO ENTER  •  BUILD  •  COLOR  •  PLACE";
            return true;
        }

        string trimmed = source.Trim();
        if (trimmed.Length < 4 || trimmed[0] != '[' || trimmed[2] != ']') return false;

        key = char.ToUpperInvariant(trimmed[1]).ToString();
        string command = trimmed.Substring(3).Trim();
        string primaryCommand = command.Split('|')[0].Trim();

        if (primaryCommand.StartsWith("Pick Up ", System.StringComparison.OrdinalIgnoreCase))
        {
            title = primaryCommand.ToUpperInvariant();
            action = "PRESS " + key + " TO PICK UP";
        }
        else if (primaryCommand.IndexOf("Shop Terminal", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            title = "EQUIPMENT SHOP";
            action = "PRESS " + key + " TO OPEN";
        }
        else if (primaryCommand.IndexOf("Computer Menu", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            title = "COMPUTER TERMINAL";
            action = "PRESS " + key + " TO OPEN";
        }
        else if (primaryCommand.IndexOf("Insert SD Card", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            title = "COMPUTER TERMINAL";
            action = command.Replace("[", "").Replace("]", "").Replace("|", " • ").ToUpperInvariant();
        }
        else
        {
            title = primaryCommand.ToUpperInvariant();
            action = "PRESS " + key + " TO INTERACT";
        }

        return true;
    }

    private void CreateDirectorTabletPrompt()
    {
        if (directorPromptRoot != null) { StyleInteractionPrompt(); return; }
        if (equipmentGuideText == null) return;

        Transform parent = equipmentGuideText.transform.parent;
        Transform existing = parent.Find("DirectorTabletPrompt");
        if (existing != null) Destroy(existing.gameObject);

        directorPromptRoot = new GameObject("DirectorTabletPrompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        directorPromptRoot.transform.SetParent(parent, false);
        directorPromptRect = directorPromptRoot.GetComponent<RectTransform>();

        RectTransform sourceRect = equipmentGuideText.rectTransform;
        directorPromptRect.anchorMin = sourceRect.anchorMin;
        directorPromptRect.anchorMax = sourceRect.anchorMax;
        directorPromptRect.pivot = sourceRect.pivot;
        directorPromptRect.anchoredPosition = sourceRect.anchoredPosition;
        directorPromptRect.sizeDelta = new Vector2(440f, 82f);

        Image background = directorPromptRoot.GetComponent<Image>();
        background.color = new Color(0.20f, 0.125f, 0.055f, 0.97f);
        background.raycastTarget = false;

        Outline outline = directorPromptRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0.10f, 0.055f, 0.02f, 0.98f);
        outline.effectDistance = new Vector2(3f, -3f);

        Shadow shadow = directorPromptRoot.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(7f, -7f);

        directorPromptGroup = directorPromptRoot.GetComponent<CanvasGroup>();
        directorPromptGroup.alpha = 0f;
        directorPromptGroup.interactable = false;
        directorPromptGroup.blocksRaycasts = false;

        GameObject accentObject = CreateImage("ContractAccent", directorPromptRoot.transform,
            new Color(0.72f, 0.46f, 0.16f, 1f));
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(7f, 0f);
        directorPromptAccent = accentObject.GetComponent<Image>();

        GameObject keyObject = CreateImage("Keycap", directorPromptRoot.transform,
            new Color(0.48f, 0.29f, 0.105f, 1f));
        RectTransform keyRect = keyObject.GetComponent<RectTransform>();
        SetRect(keyRect, new Vector2(21f, -12f), new Vector2(58f, 58f), new Vector2(0f, 1f));

        Outline keyOutline = keyObject.AddComponent<Outline>();
        keyOutline.effectColor = new Color(0.96f, 0.72f, 0.30f, 1f);
        keyOutline.effectDistance = new Vector2(2f, -2f);

        directorPromptKeyText = CreatePromptText("Key", keyObject.transform, "E", 30f, TextAlignmentOptions.Center);
        directorPromptKeyText.fontStyle = FontStyles.Bold;
        Stretch(directorPromptKeyText.rectTransform, 0f, 0f, 0f, 0f);

        directorPromptTitleText = CreatePromptText("Title", directorPromptRoot.transform,
            "DIRECTOR TABLET", 23f, TextAlignmentOptions.Left);
        directorPromptTitleText.fontStyle = FontStyles.Bold;
        directorPromptTitleText.color = new Color(1f, 0.91f, 0.70f, 1f);
        directorPromptTitleText.characterSpacing = 1.5f;
        directorPromptTitleText.enableAutoSizing = true;
        directorPromptTitleText.fontSizeMin = 14f;
        directorPromptTitleText.fontSizeMax = 23f;
        SetRect(directorPromptTitleText.rectTransform, new Vector2(99f, -13f), new Vector2(320f, 30f), new Vector2(0f, 1f));

        directorPromptActionText = CreatePromptText("Action", directorPromptRoot.transform,
            "PRESS E TO ENTER  •  BUILD  •  COLOR  •  PLACE", 11f, TextAlignmentOptions.Left);
        directorPromptActionText.color = new Color(0.88f, 0.74f, 0.49f, 1f);
        directorPromptActionText.fontStyle = FontStyles.Bold;
        directorPromptActionText.enableAutoSizing = true;
        directorPromptActionText.fontSizeMin = 8f;
        directorPromptActionText.fontSizeMax = 11f;
        SetRect(directorPromptActionText.rectTransform, new Vector2(100f, -49f), new Vector2(320f, 20f), new Vector2(0f, 1f));

        directorPromptRoot.transform.SetAsLastSibling();
        StyleInteractionPrompt();
    }

    private void StyleInteractionPrompt()
    {
        if (directorPromptRoot == null) return;
        if (directorPromptRect == null) directorPromptRect = directorPromptRoot.GetComponent<RectTransform>();
        CrewPaperStyle.Card(directorPromptRoot.GetComponent<Image>());
        if (directorPromptKeyText != null)
        {
            var badge = directorPromptKeyText.GetComponentInParent<Image>();
            CrewPaperStyle.Card(badge);
            if (badge != null) badge.color = CrewPaperStyle.Gold;
            directorPromptKeyText.color = CrewPaperStyle.Ink;
            directorPromptKeyText.fontSize = 28;
            directorPromptKeyText.margin = Vector4.zero;
        }
        if (directorPromptTitleText != null)
        {
            directorPromptTitleText.color = CrewPaperStyle.Ink;
            directorPromptTitleText.alignment = TextAlignmentOptions.MidlineLeft;
            directorPromptTitleText.characterSpacing = .5f;
            directorPromptTitleText.fontSize = 22;
            directorPromptTitleText.fontSizeMin = 16; directorPromptTitleText.fontSizeMax = 22;
            directorPromptTitleText.margin = Vector4.zero;
            directorPromptTitleText.overflowMode = TextOverflowModes.Ellipsis;
        }
        if (directorPromptActionText != null)
        {
            directorPromptActionText.color = CrewPaperStyle.MutedInk;
            directorPromptActionText.alignment = TextAlignmentOptions.MidlineLeft;
            directorPromptActionText.enableAutoSizing = false;
            directorPromptActionText.fontSize = 14;
            directorPromptActionText.enableWordWrapping = true;
            directorPromptActionText.margin = Vector4.zero;
        }
        if (directorPromptAccent != null)
        {
            var rule = directorPromptAccent.rectTransform;
            rule.anchorMin = Vector2.zero; rule.anchorMax = new Vector2(1, 0);
            rule.pivot = new Vector2(.5f, 0);
            rule.offsetMin = new Vector2(16, 6); rule.offsetMax = new Vector2(-16, 9);
            directorPromptAccent.color = CrewPaperStyle.Gold;
        }
        FitInteractionPrompt();
    }

    private void FitInteractionPrompt()
    {
        if (directorPromptRect == null) return;
        const float textWidth = 320;
        float actionHeight = directorPromptActionText == null ? 24 : Mathf.Max(24,
            Mathf.Ceil(directorPromptActionText.GetPreferredValues(directorPromptActionText.text, textWidth, Mathf.Infinity).y));
        float height = Mathf.Max(92, 52 + actionHeight + 16);
        directorPromptRect.sizeDelta = new Vector2(440, height);
        if (directorPromptKeyText != null && directorPromptKeyText.transform.parent is RectTransform badge)
            SetRect(badge, new Vector2(16, -(height - 60) * .5f), new Vector2(60, 60), new Vector2(0, 1));
        if (directorPromptTitleText != null)
            SetRect(directorPromptTitleText.rectTransform, new Vector2(96, -14), new Vector2(textWidth, 30), new Vector2(0, 1));
        if (directorPromptActionText != null)
            SetRect(directorPromptActionText.rectTransform, new Vector2(96, -52), new Vector2(textWidth, actionHeight), new Vector2(0, 1));
    }

    private GameObject CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        result.transform.SetParent(parent, false);
        Image image = result.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return result;
    }

    private TextMeshProUGUI CreatePromptText(string objectName, Transform parent, string value, float size, TextAlignmentOptions alignment)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        TextMeshProUGUI text = result.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = equipmentGuideText.font;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = pivot;
        rect.anchorMax = pivot;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
