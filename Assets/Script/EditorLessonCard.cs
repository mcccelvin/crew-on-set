using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Editor-only checklist, confined to the monitor rather than floating over tool panels.
public sealed class EditorLessonCard : MonoBehaviour
{
    private TextMeshProUGUI label;
    private string[] tasks;
    private bool[] complete;

    public static EditorLessonCard Create(RectTransform monitor)
    {
        var root = new GameObject("Editor Lesson Guide", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(monitor, false);
        rect.anchorMin = new Vector2(.02f, .02f);
        rect.anchorMax = new Vector2(.98f, .02f);
        rect.pivot = new Vector2(.5f, 0f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(0f, 94f);
        root.GetComponent<Image>().color = new Color(.07f, .11f, .16f, .96f);
        root.GetComponent<Image>().raycastTarget = false;
        root.GetComponent<CanvasGroup>().blocksRaycasts = false;
        var textObject = new GameObject("Instructions", typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 10f); textRect.offsetMax = new Vector2(-16f, -10f);
        var card = root.AddComponent<EditorLessonCard>();
        card.label = textObject.GetComponent<TextMeshProUGUI>();
        card.label.font = TMP_Settings.defaultFontAsset;
        card.label.fontSize = 19f;
        card.label.enableAutoSizing = true;
        card.label.fontSizeMin = 14f; card.label.fontSizeMax = 19f;
        card.label.enableWordWrapping = true;
        card.label.alignment = TextAlignmentOptions.MidlineLeft;
        card.label.color = new Color(.95f, .96f, .98f);
        card.label.raycastTarget = false;
        return card;
    }

    public void Show(string[] instructions)
    {
        tasks = instructions;
        complete = new bool[tasks.Length];
        gameObject.SetActive(true);
        Refresh();
    }

    public void Complete(int index)
    {
        if (complete == null || index < 0 || index >= complete.Length) return;
        complete[index] = true;
        Refresh();
    }

    private void Refresh()
    {
        string text = "<color=#E8BD6B><b>EDITOR GUIDE</b></color>";
        for (int i = 0; i < tasks.Length; i++)
        {
            string task = tasks[i].StartsWith("- ") ? tasks[i].Substring(2) : tasks[i];
            text += "\n" + (complete[i] ? "<color=#8FD6AC>Done: " + task + "</color>" : (i + 1) + ". " + task);
        }
        label.text = text;
        ((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 40f + tasks.Length * 30f);
    }
}
