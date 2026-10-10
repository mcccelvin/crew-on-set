using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Runtime UI and career-local recovery data also work in standalone builds.
public sealed class BudgetRetryPrompt : MonoBehaviour
{
    internal const string SnapshotKey = "BudgetRetry.Start.v1";
    private const string SnapshotCountKey = SnapshotKey + ".Count";
    private const int SnapshotChunkSize = 8000;
    private const int MaxSnapshotChunks = 1500;
    [Serializable] private sealed class Snapshot
    {
        public int level;
        public List<GameSaveValue> values;
    }
    private static BudgetRetryPrompt instance;
    private float previousTime;
    private bool previousPause, previousCursor;
    private CursorLockMode previousLock;
    private bool leaving;

    public static void CaptureStart(bool fresh = false)
    {
        if (Photon.Pun.PhotonNetwork.InRoom) return;
        int level = CampaignProgression.GetCurrentLevel();
        Snapshot existing = Read();
        if (!fresh && existing != null && existing.level == level) return;
        var values = GameSaveRepository.Clone(GameSavePrefs.Values ?? LegacyGameSave.Read(false));
        values.RemoveAll(v => IsSnapshotKey(v.key) || GameSavePrefs.IsGlobal(v.key));
        // Old careers have no acceptance snapshot: give their first recovery a usable budget.
        if (!fresh)
        {
            var money = values.Find(v => v.key == "PlayerMoney");
            if (money == null) values.Add(new GameSaveValue { key = "PlayerMoney", integer = ProductionEconomy.Advance(level) });
            else money.integer = Mathf.Max(money.integer, ProductionEconomy.Advance(level));
        }
        var storage = new List<GameSaveValue>
        {
            new GameSaveValue { key = SnapshotKey, kind = 2, text = JsonUtility.ToJson(new Snapshot { level = level, values = values }) }
        };
        NormalizeSnapshotValues(storage);
        int oldCount = Mathf.Clamp(GameSavePrefs.GetInt(SnapshotCountKey, 0), 0, MaxSnapshotChunks);
        for (int i = 0; i < oldCount; i++) GameSavePrefs.DeleteKey(SnapshotKey + "." + i);
        GameSavePrefs.DeleteKey(SnapshotKey);
        GameSavePrefs.DeleteKey(SnapshotCountKey);
        foreach (var value in storage)
        {
            if (value.kind == 0) GameSavePrefs.SetInt(value.key, value.integer);
            else GameSavePrefs.SetString(value.key, value.text);
        }
        GameSavePrefs.Save();
    }

    internal static bool IsSnapshotKey(string key) => key == SnapshotKey ||
        key.StartsWith(SnapshotKey + ".", StringComparison.Ordinal);

    // Keep the old single-value form for small snapshots; large ones use the same
    // career-value envelope in pieces below the repository's 10,000-character cap.
    internal static void NormalizeSnapshotValues(List<GameSaveValue> values)
    {
        var legacy = values.Find(v => v.key == SnapshotKey && v.kind == 2);
        if (legacy == null || string.IsNullOrEmpty(legacy.text) || legacy.text.Length <= SnapshotChunkSize) return;
        string json = legacy.text;
        int count = (json.Length - 1) / SnapshotChunkSize + 1;
        if (count > MaxSnapshotChunks) throw new InvalidDataException("Contract retry snapshot is too large.");
        var chunks = new List<GameSaveValue>();
        for (int i = 0; i < count; i++)
            chunks.Add(new GameSaveValue { key = SnapshotKey + "." + i, kind = 2,
                text = json.Substring(i * SnapshotChunkSize, Math.Min(SnapshotChunkSize, json.Length - i * SnapshotChunkSize)) });
        chunks.Add(new GameSaveValue { key = SnapshotCountKey, integer = count });
        values.RemoveAll(v => IsSnapshotKey(v.key));
        values.AddRange(chunks);
    }

    internal static string ReadSnapshotJson(List<GameSaveValue> values)
    {
        int count = values.Find(v => v.key == SnapshotCountKey && v.kind == 0)?.integer ?? 0;
        return ReadSnapshotJson(count, key => values.Find(v => v.key == key && v.kind == 2)?.text);
    }

    private static string ReadSnapshotJson(int count, Func<string, string> read)
    {
        if (count < 0 || count > MaxSnapshotChunks) throw new InvalidDataException("Invalid contract retry snapshot chunk count.");
        if (count == 0) return read(SnapshotKey);
        var json = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            string chunk = read(SnapshotKey + "." + i);
            if (string.IsNullOrEmpty(chunk) || chunk.Length > 10000)
                throw new InvalidDataException("Contract retry snapshot has a missing or invalid chunk.");
            json.Append(chunk);
        }
        return json.ToString();
    }

    private static Snapshot Read()
    {
        try { return JsonUtility.FromJson<Snapshot>(ReadSnapshotJson(GameSavePrefs.GetInt(SnapshotCountKey, 0), key => GameSavePrefs.GetString(key, ""))); }
        catch (ArgumentException) { return null; }
        catch (InvalidDataException) { return null; }
    }

    public static void Show(int cost, int balance)
    {
        if (instance != null || Photon.Pun.PhotonNetwork.InRoom) return;
        instance = new GameObject("Director budget recovery").AddComponent<BudgetRetryPrompt>();
        instance.Build(cost, balance);
    }

    private void Build(int cost, int balance)
    {
        previousTime = Time.timeScale;
        previousPause = PauseManager.isPaused;
        previousCursor = Cursor.visible;
        previousLock = Cursor.lockState;
        PauseManager.isPaused = true;
        Time.timeScale = 0;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        gameObject.AddComponent<GraphicRaycaster>();

        var ui = TutorialUIManager.Instance != null ? TutorialUIManager.Instance : FindObjectOfType<TutorialUIManager>(true);
        if (ui == null || ui.bossHUDCanvas == null || ui.bossText == null)
        {
            GameFeedback.Show("Your budget is too low for this purchase. The Boss dialogue is unavailable.", true);
            Destroy(gameObject);
            return;
        }

        // Use the authored Boss artwork on an independent copy. An interrupted
        // tutorial retains its dialogue, reveal state and progression underneath.
        var dialogue = Instantiate(ui.bossHUDCanvas, transform, false);
        dialogue.name = "Boss budget dialogue";
        var textTransform = FindCopiedTransform(ui.bossText.transform, ui.bossHUDCanvas.transform, dialogue.transform);
        var portraitTransform = ui.bossPortraitDisplay != null ?
            FindCopiedTransform(ui.bossPortraitDisplay.transform, ui.bossHUDCanvas.transform, dialogue.transform) : null;
        var text = textTransform != null ? textTransform.GetComponent<TMPro.TextMeshProUGUI>() : null;
        if (text == null) { Destroy(gameObject); return; }
        foreach (var animator in dialogue.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var transition in dialogue.GetComponentsInChildren<UITransition>(true)) transition.enabled = false;
        foreach (var button in dialogue.GetComponentsInChildren<Button>(true)) button.gameObject.SetActive(false);
        foreach (var label in dialogue.GetComponentsInChildren<TMPro.TMP_Text>(true))
            if (label != text) label.gameObject.SetActive(false);
        foreach (var group in dialogue.GetComponentsInChildren<CanvasGroup>(true))
        {
            group.alpha = 1;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
        var dialogueRect = (RectTransform)dialogue.transform;
        dialogueRect.anchorMin = Vector2.zero; dialogueRect.anchorMax = Vector2.one;
        dialogueRect.offsetMin = dialogueRect.offsetMax = Vector2.zero;
        dialogueRect.localScale = Vector3.one;
        foreach (var nestedScaler in dialogue.GetComponentsInChildren<CanvasScaler>(true)) nestedScaler.enabled = false;
        var dialogueCanvas = dialogue.GetComponent<Canvas>();
        if (dialogueCanvas != null)
        {
            dialogueCanvas.overrideSorting = true;
            dialogueCanvas.sortingOrder = 32761;
        }
        if (portraitTransform != null)
        {
            var portrait = portraitTransform.GetComponent<Image>();
            if (portrait != null && ui.poseOpenHand != null) portrait.sprite = ui.poseOpenHand;
            portraitTransform.localScale = Vector3.one;
        }
        text.gameObject.SetActive(true);
        BossDialogueStyle.Apply(text);
        text.text = BossDialogueStyle.HighlightControls($"We need <color=red>{cost:N0} B-Coins</color>, but you have <color=red>{balance:N0}</color>. Can't finish the commercial? Choose RETRY CONTRACT to restore its starting budget and purchases, or NEW GAME for a separate save.");
        text.maxVisibleCharacters = int.MaxValue;
        text.rectTransform.anchoredPosition = new Vector2(0, 35);
        text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x, 210);
        var panel = text.transform.parent;
        MakeButton(panel, "RETRY CONTRACT", "greenButton", new Vector2(-420, -155), Retry);
        MakeButton(panel, "NEW GAME", "redButton", new Vector2(0, -155), NewGame);
        MakeButton(panel, "KEEP WORKING", "blueButton", new Vector2(420, -155), () => Destroy(gameObject));
        dialogue.SetActive(true);
    }

    private static Transform FindCopiedTransform(Transform original, Transform root, Transform copy)
    {
        var indices = new Stack<int>();
        while (original != root)
        {
            if (original == null) return null;
            indices.Push(original.GetSiblingIndex());
            original = original.parent;
        }
        while (indices.Count > 0) copy = copy.GetChild(indices.Pop());
        return copy;
    }

    private void Retry()
    {
        if (leaving) return;
        var snapshot = Read();
        if (snapshot == null || snapshot.values == null) return;
        var current = GameSavePrefs.Values ?? LegacyGameSave.Read(false);
        foreach (var value in new List<GameSaveValue>(current))
            if (!GameSavePrefs.IsGlobal(value.key) && !IsSnapshotKey(value.key) && !CareerProfileProgress.RetainOnContractRetry(value.key)) GameSavePrefs.DeleteKey(value.key);
        foreach (var value in snapshot.values)
        {
            if (GameSavePrefs.IsGlobal(value.key) || IsSnapshotKey(value.key) || CareerProfileProgress.RetainOnContractRetry(value.key)) continue;
            if (value.kind == 0) GameSavePrefs.SetInt(value.key, value.integer);
            else if (value.kind == 1) GameSavePrefs.SetFloat(value.key, value.number);
            else GameSavePrefs.SetString(value.key, value.text);
        }
        CampaignProgression.SetRetryLevel(snapshot.level);
        PlayerAnalytics.Begin(snapshot.level, true);
        GameSaveManager.Instance?.SaveBudgetCheckpoint();
        if (ProjectDataManager.Instance != null) ProjectDataManager.Instance.ClearProject();
        CrossSceneData.finalGrades = default;
        CrossSceneData.submittedLevel = 0;
        CrossSceneData.resultApplied = false;
        PrepareLoad();
        LoadingScreenController.LoadScene("SingleStudio");
    }

    private void NewGame()
    {
        if (leaving) return;
        var saves = GameSaveManager.Ensure();
        if (saves.Syncing) { GameFeedback.Show("Please wait for save sync to finish, then try again."); return; }
        saves.SaveCheckpoint();
        var slot = saves.CreateGame("New career " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        PrepareLoad();
        saves.StartGame(slot, true);
    }

    private void PrepareLoad()
    {
        leaving = true;
        PauseManager.isPaused = false;
        Time.timeScale = 1;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (leaving) return;
        Time.timeScale = previousTime;
        PauseManager.isPaused = previousPause;
        Cursor.visible = previousCursor;
        Cursor.lockState = previousLock;
    }

    private static RectTransform Box(Transform parent, string name, Color color, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = size; rect.anchoredPosition = position;
        obj.GetComponent<Image>().color = color;
        return rect;
    }

    private static void Label(Transform parent, string content, Vector2 position, Vector2 size, int fontSize)
    {
        var obj = new GameObject("Label", typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = size; rect.anchoredPosition = position;
        var text = obj.AddComponent<TMPro.TextMeshProUGUI>();
        text.text = content; text.fontSize = fontSize; text.alignment = TMPro.TextAlignmentOptions.Center;
        text.color = new Color32(69, 40, 19, 255); text.raycastTarget = false;
    }

    private static void MakeButton(Transform parent, string title, string artwork, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var rect = Box(parent, title, Color.white, position, new Vector2(340, 70));
        ExportUIArt.Apply(rect.GetComponent<Image>(), artwork);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(action);
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        Label(rect, title, Vector2.zero, new Vector2(310, 60), 26);
        var label = rect.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        BossDialogueStyle.Apply(label, true);
    }
}
