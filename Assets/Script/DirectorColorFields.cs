using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Adds editable values to the existing tablet layout, retaining its font and slider rows.
public sealed class DirectorColorFields : MonoBehaviour
{
    private DirectorTerminal terminal;
    [SerializeField] private TMP_InputField[] rgb = new TMP_InputField[3];
    [SerializeField] private TMP_InputField hex;
    public bool IsEditing => (hex != null && hex.isFocused) ||
        (rgb[0] != null && rgb[0].isFocused) || (rgb[1] != null && rgb[1].isFocused) || (rgb[2] != null && rgb[2].isFocused);

    private bool listenersBound;
    public void Initialize(DirectorTerminal owner)
    {
        terminal = owner;
        if (hex != null)
        {
            if (!listenersBound)
            {
                listenersBound = true;
                for (int i=0;i<rgb.Length;i++) { int channel=i; if(rgb[i]!=null) rgb[i].onEndEdit.AddListener(value => CommitChannel(channel,value)); }
                hex.onEndEdit.AddListener(CommitHex);
            }
            Refresh(owner.CanUseColorSliders());
            return;
        }
        listenersBound = true;
        TMP_Text[] labels = { owner.rValueText, owner.gValueText, owner.bValueText };
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null) continue;
            int channel = i;
            RectTransform original = labels[i].rectTransform;
            rgb[i] = CreateField("RGB " + "RGB"[i], labels[i], original.parent, original.anchoredPosition, original.sizeDelta);
            RectTransform rect = (RectTransform)rgb[i].transform;
            rect.anchorMin = original.anchorMin; rect.anchorMax = original.anchorMax; rect.pivot = original.pivot;
            rgb[i].contentType = TMP_InputField.ContentType.IntegerNumber;
            rgb[i].characterLimit = 4;
            rgb[i].onEndEdit.AddListener(value => CommitChannel(channel, value));
            labels[i].gameObject.SetActive(false);
        }
        if (labels[2] != null)
        {
            RectTransform last = labels[2].rectTransform;
            hex = CreateField("HEX", labels[2], last.parent, new Vector2(25, last.anchoredPosition.y - 115), new Vector2(235, 48));
            hex.characterLimit = 7;
            hex.onEndEdit.AddListener(CommitHex);
            var label = Instantiate(labels[2], last.parent);
            label.name = "HEX Label"; label.gameObject.SetActive(true); label.text = "HEX";
            label.rectTransform.anchoredPosition = new Vector2(-130, last.anchoredPosition.y - 115);
            label.rectTransform.sizeDelta = new Vector2(70, 48);
            label.fontSize = 26; label.enableAutoSizing = false; label.alignment = TextAlignmentOptions.Center;
        }
        Refresh(owner.CanUseColorSliders());
    }

    private TMP_InputField CreateField(string name, TMP_Text style, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name + " Input", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size;
        var background = go.GetComponent<Image>(); background.color = new Color(.92f, .94f, .96f);
        var viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(rect, false); viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(4, 2); viewport.offsetMax = new Vector2(-4, -2);
        TMP_Text text = Instantiate(style, viewport); text.name = "Text"; text.gameObject.SetActive(true);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.Center; text.richText = false; text.raycastTarget = false;
        text.enableAutoSizing = true; text.fontSizeMin = 14; text.fontSizeMax = 30; text.color = Color.black;
        var field = go.GetComponent<TMP_InputField>(); field.targetGraphic = background;
        field.textViewport = viewport; field.textComponent = text; field.lineType = TMP_InputField.LineType.SingleLine;
        field.customCaretColor = true; field.caretColor = Color.black; field.selectionColor = new Color(.2f,.5f,1f,.3f);
        return field;
    }

    public static bool TryParseHex(string value, out Color color)
    {
        string digits = (value ?? "").Trim().TrimStart('#');
        if (digits.Length == 3) digits = string.Concat(digits[0], digits[0], digits[1], digits[1], digits[2], digits[2]);
        color = Color.white;
        return digits.Length == 6 && ColorUtility.TryParseHtmlString("#" + digits, out color);
    }

    private void CommitChannel(int channel, string value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
        {
            Color color = terminal.GetSliderColor(); color[channel] = Mathf.Clamp(number, 0, 255) / 255f;
            terminal.ApplyTypedColor(color);
        }
        RestoreValues();
    }

    private void CommitHex(string value)
    {
        if (TryParseHex(value, out Color color)) terminal.ApplyTypedColor(color);
        RestoreValues();
    }

    private void RestoreValues()
    {
        Color color = terminal.GetSliderColor();
        for (int i = 0; i < 3; i++) if (rgb[i] != null) rgb[i].SetTextWithoutNotify(Mathf.RoundToInt(color[i] * 255).ToString(CultureInfo.InvariantCulture));
        if (hex != null) hex.SetTextWithoutNotify("#" + ColorUtility.ToHtmlStringRGB(color));
    }

    public void Refresh(bool interactable)
    {
        Color color = terminal.GetSliderColor();
        for (int i = 0; i < 3; i++)
        {
            if (rgb[i] == null) continue;
            rgb[i].interactable = interactable;
            if (!rgb[i].isFocused) rgb[i].SetTextWithoutNotify(Mathf.RoundToInt(color[i] * 255).ToString(CultureInfo.InvariantCulture));
        }
        if (hex != null)
        {
            hex.interactable = interactable;
            if (!hex.isFocused) hex.SetTextWithoutNotify("#" + ColorUtility.ToHtmlStringRGB(color));
        }
    }
}
