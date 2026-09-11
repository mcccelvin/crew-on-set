using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Frame-driven title cards: scrubbing, pausing and export use the same timing.
public sealed class GokeCommercialCards : MonoBehaviour
{
    public const float CardSeconds = 2f;
    private CanvasGroup card;
    private RectTransform logoRect;
    private TMP_Text caption;

    public void Initialize(Sprite logo, TMP_FontAsset font)
    {
        if (card != null) return;
        var root = new GameObject("Goke Intro and Outro", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        Place(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        root.GetComponent<Image>().color = new Color32(237, 24, 46, 255);
        root.GetComponent<Image>().raycastTarget = false;
        card = root.GetComponent<CanvasGroup>();
        card.interactable = card.blocksRaycasts = false;

        var logoObject = new GameObject("Goke Logo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        logoObject.transform.SetParent(root.transform, false);
        logoRect = logoObject.GetComponent<RectTransform>();
        Place(logoRect, new Vector2(.2f, .37f), new Vector2(.8f, .75f));
        Image logoImage = logoObject.GetComponent<Image>();
        logoImage.sprite = logo;
        logoImage.preserveAspect = true;
        logoImage.raycastTarget = false;
        logoImage.enabled = logo != null;

        var textObject = new GameObject("Goke Message", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root.transform, false);
        caption = textObject.GetComponent<TextMeshProUGUI>();
        Place(caption.rectTransform, new Vector2(.1f, .16f), new Vector2(.9f, .34f));
        caption.font = font != null ? font : TMP_Settings.defaultFontAsset;
        caption.color = Color.white;
        caption.alignment = TextAlignmentOptions.Center;
        caption.raycastTarget = false;
        caption.enableAutoSizing = true;
        float height = ((RectTransform)transform).rect.height;
        caption.fontSizeMax = Mathf.Max(18f, height * .075f);
        caption.fontSizeMin = caption.fontSizeMax * .55f;
        caption.fontSize = caption.fontSizeMax;
        Hide();
    }

    // Returns 1 for the opening, 2 for the ending, and 0 for the product section.
    public static int Sample(float seconds, float duration, bool intro, bool outro, out float opacity)
    {
        opacity = 0f;
        if (duration <= 0f || seconds < 0f || seconds >= duration) return 0;
        float window = Mathf.Min(CardSeconds, duration * .25f);
        if (intro && seconds < window)
        {
            opacity = Mathf.Clamp01((window - seconds) / .35f);
            return 1;
        }
        if (outro && seconds >= duration - window)
        {
            opacity = Mathf.Clamp01((seconds - (duration - window)) / .35f);
            return 2;
        }
        return 0;
    }

    public void Render(float seconds, float duration, bool intro, bool outro)
    {
        if (card == null) return;
        int section = Sample(seconds, duration, intro, outro, out float alpha);
        card.gameObject.SetActive(section != 0);
        if (section == 0) return;
        card.transform.SetAsLastSibling();
        card.alpha = alpha;
        caption.text = section == 1 ? "CRACK OPEN THE MOMENT" : "MAKE IT A GOKE";
        float start = section == 1 ? 0f : duration - Mathf.Min(CardSeconds, duration * .25f);
        float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((seconds - start) / .45f));
        logoRect.localScale = Vector3.one * Mathf.Lerp(.9f, 1f, reveal);
    }

    public void Hide() { if (card != null) card.gameObject.SetActive(false); }

    // Used when baking the supplied tape assets. Every frame is a complete card.
    public void RenderProvidedFrame(bool outro, float seconds)
    {
        if (card == null) return;
        card.gameObject.SetActive(true);
        card.alpha = 1f;
        caption.text = outro ? "MAKE IT A GOKE" : "CRACK OPEN THE MOMENT";
        float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(seconds / .45f));
        logoRect.localScale = Vector3.one * Mathf.Lerp(.9f, 1f, reveal);
    }
    private void OnDisable() { Hide(); }
    private void OnDestroy() { if (card != null) Destroy(card.gameObject); }

    private static void Place(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
