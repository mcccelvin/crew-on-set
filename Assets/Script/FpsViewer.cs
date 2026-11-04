using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A single, non-interactive overlay that survives menu/gameplay scene changes.
public sealed class FpsViewer : MonoBehaviour
{
    private static FpsViewer instance;
    private TMP_Text label;
    private float elapsed;
    private int frames;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() { instance = null; }

    public static void SetVisible(bool visible)
    {
        if (instance == null)
        {
            if (!visible) return;
            var root = new GameObject("FPS viewer", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            instance = root.AddComponent<FpsViewer>();
            DontDestroyOnLoad(root);
            instance.Build();
        }
        instance.elapsed = 0f;
        instance.frames = 0;
        instance.label.text = "FPS: —";
        instance.gameObject.SetActive(visible);
    }

    private void Build()
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        var rect = new GameObject("FPS readout", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(16, -12);
        rect.sizeDelta = new Vector2(180, 40);
        label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 26;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        var shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .9f);
        shadow.effectDistance = new Vector2(1, -1);
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        frames++;
        if (elapsed < .5f) return;
        label.SetText("FPS: {0}", Mathf.Round(frames / elapsed));
        elapsed = 0f;
        frames = 0;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
