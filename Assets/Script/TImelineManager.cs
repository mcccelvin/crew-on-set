using UnityEngine;
using TMPro;

public class TimelineManager : MonoBehaviour
{
    public static TimelineManager Instance;

    [Header("Timeline Settings")]
    public RectTransform scrollContent; 
    public RectTransform timestampContainer; 
    
    [Header("Time Math")]
    public float minSeconds = 10f; 
    public float freeSpacePercentage = 0.25f;

    [HideInInspector] 
    public float pixelsPerSecond = 0f; 

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        RefreshTimeline();
    }

    public void RefreshTimeline()
    {
        if (scrollContent == null || timestampContainer == null) return;

        Canvas.ForceUpdateCanvases();

        RectTransform viewport = scrollContent.parent as RectTransform;
        if (viewport == null) return;

        float safeMinSeconds = Mathf.Max(1f, minSeconds);
        float windowWidth = viewport.rect.width;
        if (windowWidth <= 1f) windowWidth = scrollContent.rect.width;
        if (windowWidth <= 1f) windowWidth = safeMinSeconds * 40f;

        if (pixelsPerSecond <= 0f) pixelsPerSecond = windowWidth / safeMinSeconds;
        pixelsPerSecond = Mathf.Max(1f, pixelsPerSecond);

        float maxRightPixel = 0f;
        DraggableClip[] clips = scrollContent.GetComponentsInChildren<DraggableClip>();
        BrandingClip[] bClips = scrollContent.GetComponentsInChildren<BrandingClip>();

        foreach (var clip in clips)
        {
            RectTransform rt = clip.GetComponent<RectTransform>();
            float rightEdge = rt.anchoredPosition.x + (rt.rect.width * (1f - rt.pivot.x));
            if (rightEdge > maxRightPixel) maxRightPixel = rightEdge;
        }

        foreach (var bClip in bClips)
        {
            RectTransform rt = bClip.GetComponent<RectTransform>();
            float rightEdge = rt.anchoredPosition.x + (rt.rect.width * (1f - rt.pivot.x));
            if (rightEdge > maxRightPixel) maxRightPixel = rightEdge;
        }

        float currentContentSeconds = maxRightPixel / pixelsPerSecond;
        float usablePercentage = Mathf.Clamp(1f - freeSpacePercentage, 0.1f, 1f);
        float requiredSeconds = currentContentSeconds / usablePercentage;
        float finalSeconds = Mathf.Max(safeMinSeconds, requiredSeconds);

        float newPixelsPerSecond = Mathf.Max(1f, windowWidth / finalSeconds);

        if (Mathf.Abs(newPixelsPerSecond - pixelsPerSecond) > 0.01f)
        {
            float zoomRatio = newPixelsPerSecond / pixelsPerSecond;
            pixelsPerSecond = newPixelsPerSecond;

            foreach (var clip in clips)
            {
                clip.AdjustToNewZoom(zoomRatio);
            }
            
            // ========================================================
            // --- THE FIX: ADDED BRANDING CLIPS TO THE ZOOM LOGIC ---
            // ========================================================
            foreach (var bClip in bClips)
            {
                bClip.AdjustToNewZoom(zoomRatio);
            }
        }

        scrollContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, windowWidth);
        timestampContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, windowWidth);
        
        DrawTimestamps(finalSeconds);
    }

    private void DrawTimestamps(float totalSeconds)
    {
        if (timestampContainer == null) return;

        foreach (Transform child in timestampContainer)
        {
            Destroy(child.gameObject);
        }

        int interval = 1;
        if (pixelsPerSecond < 40) interval = 2; 
        if (pixelsPerSecond < 20) interval = 5; 
        if (pixelsPerSecond < 8) interval = 10; 

        int totalTicks = Mathf.CeilToInt(totalSeconds);
        for (int i = 0; i <= totalTicks; i += interval)
        {
            GameObject tickObj = new GameObject("Tick_" + i);
            tickObj.transform.SetParent(timestampContainer, false);
            
            RectTransform rt = tickObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f); 
            
            rt.anchoredPosition = new Vector2(i * pixelsPerSecond, 0);
            rt.sizeDelta = new Vector2(100, 50); 

            TextMeshProUGUI tmp = tickObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "|\n" + i + "s"; 
            tmp.fontSize = 14;
            tmp.alignment = TextAlignmentOptions.Top; 
            tmp.color = new Color(0.8f, 0.8f, 0.8f, 1f); 
            tmp.raycastTarget = false; 
            tmp.overflowMode = TextOverflowModes.Overflow;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
