using UnityEngine;
using UnityEngine.UI;

public class TutorialHighlighter : MonoBehaviour
{
    public static TutorialHighlighter Instance;

    [Header("UI References")]
    public RectTransform highlightFrame;
    public float padding = 25f;
    public float pulseSpeed = 4f;

    [Header("Darkness Mask Settings")]
    [Tooltip("How dark the screen gets behind the highlight")]
    [Range(0f, 1f)] public float backgroundDarkness = 0.8f;
    public Color dimColor = Color.black;

    private RectTransform targetElement;
    private CanvasGroup frameCanvasGroup;
    private Canvas myCanvas;
    private readonly Vector3[] targetCorners = new Vector3[4];

    // --- NEW: 4-Panel Dimmer Variables ---
    private CanvasGroup dimmerGroup;
    private RectTransform[] dimmerPanels = new RectTransform[4];
    private float currentDimmerAlpha = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        myCanvas = GetComponentInParent<Canvas>();

        if (highlightFrame != null)
        {
            frameCanvasGroup = highlightFrame.GetComponent<CanvasGroup>();
            if (frameCanvasGroup == null) frameCanvasGroup = highlightFrame.gameObject.AddComponent<CanvasGroup>();
            frameCanvasGroup.blocksRaycasts = false;

            // Auto-Generate the Dark Background so you don't have to do it manually!
            CreateDimmerPanels();

            HideHighlight();
        }
    }

    private void CreateDimmerPanels()
    {
        if (myCanvas == null || highlightFrame == null) return;

        // Create a container for the darkness
        GameObject container = new GameObject("Dynamic_Dimmer_Mask");
        RectTransform containerRect = container.AddComponent<RectTransform>();
        containerRect.SetParent(highlightFrame.parent, false);
        containerRect.SetAsFirstSibling(); // Push it behind the yellow highlight frame

        // Stretch container to fill the screen
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        dimmerGroup = container.AddComponent<CanvasGroup>();
        dimmerGroup.blocksRaycasts = false; // Don't steal clicks from the player
        dimmerGroup.alpha = 0f;

        // Create the 4 panels (Top, Bottom, Left, Right) that will wrap around the button
        for (int i = 0; i < 4; i++)
        {
            GameObject panel = new GameObject($"Dimmer_Panel_{i}");
            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.SetParent(containerRect, false);

            // Lock anchors to center so our absolute pixel math works flawlessly
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            Image img = panel.AddComponent<Image>();
            img.color = new Color(dimColor.r, dimColor.g, dimColor.b, backgroundDarkness);
            img.raycastTarget = false;

            dimmerPanels[i] = rt;
        }
    }

    private void Update()
    {
        if (highlightFrame != null && highlightFrame.gameObject.activeSelf && targetElement != null)
        {
            // 1. Pulse the yellow frame
            float alpha = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) / 2f;
            if (frameCanvasGroup != null) frameCanvasGroup.alpha = Mathf.Lerp(0.4f, 1f, alpha);

            // 2. Smoothly fade in the dark background
            if (dimmerGroup != null)
            {
                currentDimmerAlpha = Mathf.MoveTowards(currentDimmerAlpha, 1f, Time.unscaledDeltaTime * 5f);
                dimmerGroup.alpha = currentDimmerAlpha;
            }

            TrackTarget();
        }
    }

    public void HighlightElement(RectTransform uiElementToHighlight)
    {
        if (uiElementToHighlight == null)
        {
            Debug.LogWarning("TUTORIAL ERROR: You triggered a tutorial step, but the button slot in the TutorialManager Inspector is empty!");
            return;
        }
        if (highlightFrame == null)
        {
            Debug.LogError("HIGHLIGHT ERROR: You forgot to drag your Yellow Square Image into the 'Highlight Frame' slot on the TutorialHighlighter script!");
            return;
        }

        targetElement = uiElementToHighlight;
        highlightFrame.gameObject.SetActive(true);
        if (dimmerGroup != null) dimmerGroup.gameObject.SetActive(true);

        TrackTarget();
    }

    public void HideHighlight()
    {
        targetElement = null;
        if (highlightFrame != null) highlightFrame.gameObject.SetActive(false);
        if (dimmerGroup != null)
        {
            dimmerGroup.gameObject.SetActive(false);
            currentDimmerAlpha = 0f;
            dimmerGroup.alpha = 0f;
        }
    }

    private void TrackTarget()
    {
        if (targetElement == null || highlightFrame == null) return;

        if (myCanvas == null)
        {
            Debug.LogError("HIGHLIGHT ERROR: This script must be placed on a UI Canvas, or a child of a UI Canvas!");
            return;
        }

        Canvas targetCanvas = targetElement.GetComponentInParent<Canvas>();
        if (targetCanvas == null) return;

        Camera cam = (targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : targetCanvas.worldCamera;

        targetElement.GetWorldCorners(targetCorners);

        Vector2 screenBottomLeft = RectTransformUtility.WorldToScreenPoint(cam, targetCorners[0]);
        Vector2 screenTopRight = RectTransformUtility.WorldToScreenPoint(cam, targetCorners[2]);

        float width = Mathf.Abs(screenTopRight.x - screenBottomLeft.x);
        float height = Mathf.Abs(screenTopRight.y - screenBottomLeft.y);
        Vector2 screenCenter = (screenBottomLeft + screenTopRight) / 2f;

        RectTransform parentRect = highlightFrame.parent as RectTransform;
        if (parentRect == null)
        {
            Debug.LogError("HIGHLIGHT ERROR: The Highlight Frame must be a child of the Canvas, not the root object!");
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenCenter, null, out Vector2 localPoint);
        highlightFrame.localPosition = localPoint;

        float scale = myCanvas.scaleFactor > 0 ? myCanvas.scaleFactor : 1f;
        highlightFrame.sizeDelta = new Vector2((width / scale) + padding, (height / scale) + padding);
        highlightFrame.rotation = targetElement.rotation;

        // --- NEW: UPDATE THE DARKNESS MASK TO FRAME THE HOLE ---
        UpdateDimmerPanels(parentRect);
    }

    private void UpdateDimmerPanels(RectTransform parentRect)
    {
        if (dimmerPanels[0] == null) return;

        // Get the exact location of the "hole"
        float holeX = highlightFrame.localPosition.x;
        float holeY = highlightFrame.localPosition.y;
        float holeW = highlightFrame.sizeDelta.x;
        float holeH = highlightFrame.sizeDelta.y;

        float holeLeft = holeX - (holeW / 2f);
        float holeRight = holeX + (holeW / 2f);
        float holeTop = holeY + (holeH / 2f);
        float holeBottom = holeY - (holeH / 2f);

        // Get the edges of the screen
        float parentW = parentRect.rect.width;
        float parentH = parentRect.rect.height;
        float parentLeft = -(parentW / 2f);
        float parentRight = (parentW / 2f);
        float parentTop = (parentH / 2f);
        float parentBottom = -(parentH / 2f);

        // 1. Top Panel
        dimmerPanels[0].sizeDelta = new Vector2(parentW, parentTop - holeTop);
        dimmerPanels[0].localPosition = new Vector2(0, holeTop + (dimmerPanels[0].sizeDelta.y / 2f));

        // 2. Bottom Panel
        dimmerPanels[1].sizeDelta = new Vector2(parentW, holeBottom - parentBottom);
        dimmerPanels[1].localPosition = new Vector2(0, holeBottom - (dimmerPanels[1].sizeDelta.y / 2f));

        // 3. Left Panel
        dimmerPanels[2].sizeDelta = new Vector2(holeLeft - parentLeft, holeH);
        dimmerPanels[2].localPosition = new Vector2(holeLeft - (dimmerPanels[2].sizeDelta.x / 2f), holeY);

        // 4. Right Panel
        dimmerPanels[3].sizeDelta = new Vector2(parentRight - holeRight, holeH);
        dimmerPanels[3].localPosition = new Vector2(holeRight + (dimmerPanels[3].sizeDelta.x / 2f), holeY);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
