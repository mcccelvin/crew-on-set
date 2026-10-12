using UnityEngine;
using TMPro;
using UnityEngine.UI;

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
    private ScrollRect trackScroll;
    private Coroutine revealTrack;

    private void ConfigureTrackScrolling()
    {
        if (scrollContent == null) return;
        trackScroll = scrollContent.GetComponentInParent<ScrollRect>();
        if (trackScroll == null || trackScroll.content != scrollContent) return;
        trackScroll.vertical = true;
        trackScroll.horizontal = false;
        trackScroll.movementType = ScrollRect.MovementType.Clamped;
        trackScroll.scrollSensitivity = 35f;
        if (trackScroll.verticalScrollbar != null)
        {
            PlaceScrollbarOnOuterRail();
            return;
        }
        var viewport = trackScroll.viewport != null ? trackScroll.viewport : (RectTransform)trackScroll.transform;
        // An actual interactive rail replaces the decorative scrollbar in the editor artwork.
        var rail = new GameObject("Timeline vertical scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        rail.layer = viewport.gameObject.layer;
        rail.transform.SetParent(viewport, false);
        var rect = (RectTransform)rail.transform;
        rect.anchorMin = new Vector2(1, 0); rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(1, .5f);
        rect.offsetMin = new Vector2(-32, 4); rect.offsetMax = new Vector2(0, -44);
        rail.GetComponent<Image>().color = new Color32(30, 31, 34, 255);
        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.layer = rail.layer; handle.transform.SetParent(rect, false);
        var handleRect = (RectTransform)handle.transform;
        handleRect.anchorMin = Vector2.zero; handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = new Vector2(5, 3); handleRect.offsetMax = new Vector2(-5, -3);
        var handleImage = handle.GetComponent<Image>();
        CrewPaperStyle.Round(handleImage); handleImage.color = new Color32(203, 212, 218, 255);
        var bar = rail.GetComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.handleRect = handleRect; bar.targetGraphic = handleImage;
        trackScroll.verticalScrollbar = bar;
        trackScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        PlaceScrollbarOnOuterRail();
    }

    private readonly Vector3[] viewportCorners = new Vector3[4];

    private void LateUpdate()
    {
        PlaceScrollbarOnOuterRail();
    }

    private void PlaceScrollbarOnOuterRail()
    {
        if (trackScroll == null || trackScroll.verticalScrollbar == null) return;
        var viewport = trackScroll.viewport != null ? trackScroll.viewport : (RectTransform)trackScroll.transform;
        var parent = viewport.parent as RectTransform;
        if (parent == null) return;
        var bar = trackScroll.verticalScrollbar;
        var rect = (RectTransform)bar.transform;
        // Put the real control over the white rail in the artwork, outside the track mask.
        if (rect.parent != parent) rect.SetParent(parent, false);
        rect.SetAsLastSibling();
        viewport.GetWorldCorners(viewportCorners);
        Vector3 bottom = parent.InverseTransformPoint(viewportCorners[3]);
        Vector3 top = parent.InverseTransformPoint(viewportCorners[2]);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(0, .5f);
        rect.localScale = Vector3.one;
        rect.localPosition = new Vector3(bottom.x, (bottom.y + top.y) * .5f, 0);
        rect.sizeDelta = new Vector2(40f, Mathf.Max(0f, top.y - bottom.y));
        var rail = bar.GetComponent<Image>();
        if (rail != null) { rail.sprite = null; rail.color = new Color32(24, 24, 24, 255); }
        if (bar.targetGraphic != null) bar.targetGraphic.color = new Color32(224, 224, 224, 255);
        if (bar.handleRect != null)
        {
            bar.handleRect.offsetMin = new Vector2(6, 3);
            bar.handleRect.offsetMax = new Vector2(-6, -3);
        }
    }

    public void RevealTrack(RectTransform track)
    {
        if (track == null || !isActiveAndEnabled) return;
        ConfigureTrackScrolling();
        if (trackScroll == null || !track.IsChildOf(scrollContent)) return;
        if (revealTrack != null) StopCoroutine(revealTrack);
        revealTrack = StartCoroutine(RevealTrackAfterLayout(track));
    }

    private System.Collections.IEnumerator RevealTrackAfterLayout(RectTransform track)
    {
        yield return null;
        if (track == null || trackScroll == null) yield break;
        Canvas.ForceUpdateCanvases();
        var viewport = trackScroll.viewport != null ? trackScroll.viewport : (RectTransform)trackScroll.transform;
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, track);
        float hidden = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, scrollContent).size.y - viewport.rect.height;
        if (hidden <= 0f) yield break;
        float move = 0f;
        if (bounds.min.y < viewport.rect.yMin + 12f) move = viewport.rect.yMin + 12f - bounds.min.y;
        if (bounds.max.y + move > viewport.rect.yMax - 48f) move = viewport.rect.yMax - 48f - bounds.max.y;
        trackScroll.StopMovement();
        float start = trackScroll.verticalNormalizedPosition;
        float destination = Mathf.Clamp01(start - move / hidden);
        float elapsed = 0f;
        while (elapsed < .45f && trackScroll != null)
        {
            if (!PauseManager.isPaused)
            {
                elapsed += Time.unscaledDeltaTime;
                trackScroll.verticalNormalizedPosition = Mathf.Lerp(start, destination, Mathf.SmoothStep(0f, 1f, elapsed / .45f));
                trackScroll.StopMovement();
            }
            yield return null;
        }
        revealTrack = null;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        TimelineRuler.Install(this);
        ConfigureTrackScrolling();
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
            child.gameObject.SetActive(false);
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
            rt.sizeDelta = new Vector2(Mathf.Min(100, pixelsPerSecond * interval), 50);

            TextMeshProUGUI tmp = tickObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "|\n" + i + "s"; 
            tmp.fontSize = 14;
            tmp.alignment = TextAlignmentOptions.Top; 
            tmp.color = new Color(0.8f, 0.8f, 0.8f, 1f); 
            tmp.raycastTarget = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            var target = tickObj.AddComponent<TimelineRulerTick>();
            target.ruler = TimelineRuler.Install(this); target.seconds = i;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
