using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameSaveMenu : MonoBehaviour
{
    private GameSaveManager saves;
    private RectTransform rows;
    private TMP_Text status;
    private TMP_Text pageLabel;
    private TMP_InputField nameInput;
    private Button create;
    private Button retry;
    private int page;
    private const int PageSize = 4;

    public static void Show(GameSaveManager manager)
    {
        var existing = FindObjectOfType<GameSaveMenu>();
        if (existing != null) return;
        var go = new GameObject("Saved Games", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var menu = go.AddComponent<GameSaveMenu>();
        menu.saves = manager;
        menu.Build();
        manager.Changed += menu.Refresh;
        menu.Refresh();
    }
    private void OnDestroy() { if (saves != null) saves.Changed -= Refresh; }
    private void Build()
    {
        var backdrop = Rect("Backdrop", transform, Vector2.zero, Vector2.one);
        backdrop.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .86f);
        var panel = Rect("Save Panel", backdrop, new Vector2(.17f, .12f), new Vector2(.83f, .88f));
        panel.gameObject.AddComponent<Image>().color = new Color32(28, 29, 30, 255);
        Text(panel, "YOUR GAMES", new Vector2(.04f, .89f), new Vector2(.7f, .98f), 38);
        Button(panel, "BACK", new Vector2(.83f, .91f), new Vector2(.96f, .97f), () => Destroy(gameObject));
        string player = PlayerPrefs.GetString("PlayerName", "Guest");
        string id = PlayerPrefs.GetString("PlayFabId", "");
        var identity = Text(panel, player + (string.IsNullOrEmpty(id) ? "  ·  Guest saves" : "  ·  PlayFab ID: " + id), new Vector2(.04f, .83f), new Vector2(.96f, .89f), 23);
        identity.richText = false;
        Text(panel, "Continue restarts the current commercial with its checkpoint budget.\nStage placement, recordings and unfinished edits start fresh.", new Vector2(.04f, .73f), new Vector2(.96f, .83f), 21);
        rows = Rect("Save Rows", panel, new Vector2(.04f, .29f), new Vector2(.96f, .72f));
        Button(panel, "<", new Vector2(.04f, .23f), new Vector2(.1f, .28f), () => { page = Math.Max(0, page - 1); Refresh(); });
        pageLabel = Text(panel, "", new Vector2(.12f, .23f), new Vector2(.32f, .28f), 21);
        Button(panel, ">", new Vector2(.33f, .23f), new Vector2(.39f, .28f), () => { page++; Refresh(); });
        var inputRect = Rect("New Game Name", panel, new Vector2(.04f, .14f), new Vector2(.69f, .21f));
        inputRect.gameObject.AddComponent<Image>().color = new Color32(55, 56, 58, 255);
        nameInput = inputRect.gameObject.AddComponent<TMP_InputField>();
        var label = Text(inputRect, "", new Vector2(.03f, .08f), new Vector2(.97f, .92f), 24);
        label.richText = false;
        nameInput.textViewport = inputRect;
        nameInput.textComponent = label;
        nameInput.characterLimit = 40;
        nameInput.lineType = TMP_InputField.LineType.SingleLine;
        nameInput.text = "Game " + (saves.Repository.Slots.Count + 1);
        create = Button(panel, "NEW GAME", new Vector2(.72f, .14f), new Vector2(.96f, .21f), CreateGame);
        status = Text(panel, "", new Vector2(.04f, .03f), new Vector2(.73f, .12f), 19);
        retry = Button(panel, "RETRY SYNC", new Vector2(.76f, .035f), new Vector2(.96f, .10f), saves.SyncCloud);
    }
    private void Refresh()
    {
        foreach (Transform child in rows) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        var slots = saves.Repository.Slots.OrderByDescending(s => s.updatedUtc).ToList();
        int pages = Math.Max(1, (slots.Count + PageSize - 1) / PageSize);
        page = Mathf.Clamp(page, 0, pages - 1);
        pageLabel.text = (page + 1) + " / " + pages;
        status.text = saves.Status;
        create.interactable = retry.interactable = !saves.Syncing;
        if (slots.Count == 0) Text(rows, "No saved games yet. Name your first game below.", Vector2.zero, Vector2.one, 25);
        for (int i = 0; i < PageSize && page * PageSize + i < slots.Count; i++)
        {
            var slot = slots[page * PageSize + i];
            float top = 1f - i * .25f;
            var row = Rect("Game " + slot.id, rows, new Vector2(0, top - .225f), new Vector2(1, top));
            row.gameObject.AddComponent<Image>().color = new Color32(44, 46, 47, 255);
            var title = Text(row, slot.name, new Vector2(.025f, .48f), new Vector2(.75f, .97f), 26);
            title.richText = false;
            string date = DateTime.TryParse(slot.updatedUtc, out var time) ? time.ToLocalTime().ToString("MMM d, HH:mm") : "";
            Text(row, "Level " + slot.Level + "  ·  " + slot.Money.ToString("N0") + " B-Coins  ·  " + date, new Vector2(.025f, .05f), new Vector2(.75f, .48f), 20);
            var play = Button(row, "CONTINUE", new Vector2(.78f, .19f), new Vector2(.98f, .81f), () => { saves.StartGame(slot, false); });
            play.interactable = !saves.Syncing;
        }
    }
    private void CreateGame()
    {
        if (saves.Syncing) return;
        try { saves.StartGame(saves.CreateGame(nameInput.text), true); }
        catch (Exception) { status.text = "Could not create a save. Check available disk space and try again."; }
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    private static TextMeshProUGUI Text(Transform parent, string value, Vector2 min, Vector2 max, float size)
    {
        var text = Rect("Label", parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value; text.fontSize = size; text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableAutoSizing = true; text.fontSizeMin = size * .75f; text.fontSizeMax = size;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
    private static Button Button(Transform parent, string title, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
    {
        var rect = Rect(title, parent, min, max);
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color32(30, 87, 55, 255);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var text = Text(rect, title, new Vector2(.03f, 0), new Vector2(.97f, 1), 23);
        text.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(action);
        return button;
    }
}
