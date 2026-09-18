using PlayerPrefs = GameSavePrefs;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

// Independent of tutorial visibility and the lifetime of the career manager.
public sealed class GameFeedback : MonoBehaviour
{
    private static GameFeedback instance;
    private readonly List<TMP_Text> balances = new List<TMP_Text>();
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text message;
    [SerializeField] private TMP_Text heading;
    [SerializeField] private TMP_Text badge;
    private float hideAt;
    private int displayedBalance = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install() { EnsureInstance(); }

    private static GameFeedback EnsureInstance()
    {
        if (instance != null) return instance;
        instance = FindObjectOfType<GameFeedback>(true);
        if (instance == null)
            instance = new GameObject("Game Feedback").AddComponent<GameFeedback>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        // Each scene owns its editable notification canvas.
    }

    private void OnEnable()
    {
        instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindBalanceLabels();
    }

    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }
    private void OnDestroy() { if (instance == this) instance = null; }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { BindBalanceLabels(); }

    private void BindBalanceLabels()
    {
        balances.Clear();
        foreach (TMP_Text label in FindObjectsOfType<TMP_Text>(true))
        {
            // This is the actual studio HUD path, not the shop's cart total.
            if (label.name == "Quantity" && label.transform.parent != null && label.transform.parent.name == "BCoins")
                balances.Add(label);
        }
        displayedBalance = -1;
        RefreshBalances();
    }

    public static void RefreshBalance() { EnsureInstance().RefreshBalances(); }

    private void RefreshBalances()
    {
        int balance = Mathf.Max(0, PlayerPrefs.GetInt("PlayerMoney", 0));
        string value = balance.ToString("N0");
        foreach (TMP_Text label in balances)
            if (label != null && (balance != displayedBalance || label.text != value)) label.text = value;
        displayedBalance = balance;
    }

    private void LateUpdate()
    {
        RefreshBalances();
        if (panel != null && panel.activeSelf && Time.unscaledTime >= hideAt) panel.SetActive(false);
    }

    public static void Show(string text, bool error = false)
    {
        GameFeedback feedback = EnsureInstance();
        if (feedback.panel == null) feedback.BuildNotification();
        string[] lines = (text ?? "").Split(new[] { '\n' }, 2);
        string title = lines[0];
        string detail = lines.Length > 1 ? lines[1] : "";
        const string purchase = "PURCHASE CONFIRMED";
        if (title.StartsWith(purchase))
        {
            detail = "Spent " + title.Substring(purchase.Length).Trim().TrimStart('-') +
                (detail.Length > 0 ? "  •  " + detail : "");
            title = purchase;
        }
        feedback.heading.text = title;
        feedback.message.text = detail;
        feedback.badge.text = error ? "!" : title == purchase ? "B" : "i";
        feedback.heading.ForceMeshUpdate();
        float height = Mathf.Clamp(feedback.heading.GetPreferredValues(title, 440, 0).y +
            feedback.message.GetPreferredValues(detail, 440, 0).y + 38, 100, 240);
        ((RectTransform)feedback.panel.transform).sizeDelta = new Vector2(560, height);
        feedback.heading.rectTransform.sizeDelta = new Vector2(440, detail.Length == 0 ? height - 30 : Mathf.Max(30, height * .45f - 10));
        feedback.message.rectTransform.offsetMax = new Vector2(-20, -height * .45f - 8);
        feedback.panel.SetActive(true);
        feedback.hideAt = Time.unscaledTime + 4f;
        feedback.RefreshBalances();
    }

#if UNITY_EDITOR
    public void BakeHierarchyUI()
    {
        if (panel == null) BuildNotification();
        panel.SetActive(false);
    }
#endif

    private void BuildNotification()
    {
        var canvasObject = new GameObject("Notifications", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        panel = new GameObject("Notice", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0, -110);
        rect.sizeDelta = new Vector2(560, 114);
        var background = panel.GetComponent<Image>();
        background.color = new Color32(49, 32, 12, 250); background.raycastTarget = false;
        var border = panel.AddComponent<Outline>(); border.effectColor = new Color32(20, 13, 6, 255); border.effectDistance = new Vector2(4, -4);
        var shadow = panel.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .5f); shadow.effectDistance = new Vector2(8, -8);
        var accent = new GameObject("Gold edge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        accent.transform.SetParent(panel.transform, false); accent.color = new Color32(181, 130, 49, 255); accent.raycastTarget = false;
        accent.rectTransform.anchorMin = Vector2.zero; accent.rectTransform.anchorMax = new Vector2(0, 1);
        accent.rectTransform.offsetMin = Vector2.zero; accent.rectTransform.offsetMax = new Vector2(6, 0);
        var key = new GameObject("Notice badge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        key.transform.SetParent(panel.transform, false); key.color = new Color32(121, 78, 28, 255); key.raycastTarget = false;
        key.rectTransform.anchorMin = key.rectTransform.anchorMax = new Vector2(0, .5f);
        key.rectTransform.anchoredPosition = new Vector2(52, 0); key.rectTransform.sizeDelta = new Vector2(58, 58);
        var gold = key.gameObject.AddComponent<Outline>(); gold.effectColor = new Color32(239, 184, 71, 255); gold.effectDistance = new Vector2(2, -2);
        NotificationStyle style = Resources.Load<NotificationStyle>("NotificationStyle");
        TMP_FontAsset font = style != null && style.font != null ? style.font : TMP_Settings.defaultFontAsset;
        badge = CreateText("Symbol", key.transform, font, 28, Color.white);
        badge.alignment = TextAlignmentOptions.Center;
        badge.rectTransform.anchorMin = Vector2.zero; badge.rectTransform.anchorMax = Vector2.one;
        badge.rectTransform.offsetMin = badge.rectTransform.offsetMax = Vector2.zero;
        heading = CreateText("Title", panel.transform, font, 23, new Color32(255, 231, 173, 255));
        heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = heading.rectTransform.pivot = new Vector2(0, 1);
        heading.rectTransform.anchoredPosition = new Vector2(100, -15);
        heading.rectTransform.sizeDelta = new Vector2(440, 34);
        message = CreateText("Message", panel.transform, font, 18, new Color32(218, 184, 120, 255));
        message.fontStyle = FontStyles.Normal;
        message.rectTransform.anchorMin = Vector2.zero; message.rectTransform.anchorMax = Vector2.one;
        message.rectTransform.offsetMin = new Vector2(100, 14); message.rectTransform.offsetMax = new Vector2(-20, -50);
    }

    private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, float size, Color color)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false); text.font = font; text.fontSize = size;
        text.fontStyle = FontStyles.Bold; text.color = color; text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = true; text.raycastTarget = false;
        return text;
    }
}
