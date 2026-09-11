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
    private GameObject panel;
    private TMP_Text message;
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
        DontDestroyOnLoad(gameObject);
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
        feedback.message.text = text;
        feedback.panel.GetComponent<Image>().color = Color.white;
        feedback.panel.SetActive(true);
        feedback.hideAt = Time.unscaledTime + 4f;
        feedback.RefreshBalances();
    }

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
        NotificationStyle style = Resources.Load<NotificationStyle>("NotificationStyle");
        if (style != null) panel.GetComponent<Image>().sprite = style.container;
        panel.GetComponent<Image>().raycastTarget = false;
        var textObject = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);
        message = textObject.GetComponent<TextMeshProUGUI>();
        message.rectTransform.anchorMin = Vector2.zero;
        message.rectTransform.anchorMax = Vector2.one;
        message.rectTransform.offsetMin = new Vector2(24, 16);
        message.rectTransform.offsetMax = new Vector2(-24, -16);
        message.font = style != null && style.font != null ? style.font : TMP_Settings.defaultFontAsset;
        message.fontSize = 23;
        message.enableAutoSizing = true;
        message.fontSizeMin = 18;
        message.fontSizeMax = 23;
        message.alignment = TextAlignmentOptions.Center;
        message.color = Color.white;
        message.raycastTarget = false;
    }
}
