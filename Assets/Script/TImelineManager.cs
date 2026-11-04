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
    private RectTransform[] videoTracks;
    public bool HasMultipleVideoTracks => videoTracks != null && videoTracks.Length > 1;

    // Resolve stacked video into visible intervals for both playback and review.
    // VIDEO 1 (the upper lane) wins where both lanes contain footage.
    public static System.Collections.Generic.List<ClipSegment> ResolveVideoLayers(System.Collections.Generic.List<ClipSegment> sequence, float fps)
    {
        var manager = Instance;
        if (manager == null || !manager.HasMultipleVideoTracks || sequence == null || sequence.Count < 2) return sequence;
        float pixelsPerFrame = manager.pixelsPerSecond / Mathf.Max(1f, fps);
        if (pixelsPerFrame <= 0) return sequence;
        var boundaries = new System.Collections.Generic.SortedSet<int>();
        foreach (var segment in sequence)
        {
            int first = Mathf.RoundToInt(segment.uiStartX / pixelsPerFrame);
            boundaries.Add(first); boundaries.Add(first + segment.endFrame - segment.startFrame);
        }
        var edges = new System.Collections.Generic.List<int>(boundaries);
        var result = new System.Collections.Generic.List<ClipSegment>();
        for (int i = 0; i + 1 < edges.Count; i++)
        {
            int from = edges[i], to = edges[i+1], priority = int.MaxValue;
            ClipSegment chosen = null;
            foreach (var segment in sequence)
            {
                int first = Mathf.RoundToInt(segment.uiStartX / pixelsPerFrame);
                if (from < first || from >= first + segment.endFrame - segment.startFrame) continue;
                int lane = segment.sourceClip != null ? System.Array.IndexOf(manager.videoTracks, segment.sourceClip.transform.parent as RectTransform) : 0;
                if (lane < 0) lane = 0;
                if (chosen == null || lane < priority) { chosen = segment; priority = lane; }
            }
            if (chosen == null || to <= from) continue;
            int start = chosen.startFrame + from - Mathf.RoundToInt(chosen.uiStartX / pixelsPerFrame);
            var previous = result.Count > 0 ? result[result.Count-1] : null;
            if (previous != null && previous.sourceClip == chosen.sourceClip && previous.path == chosen.path && previous.endFrame == start &&
                Mathf.Abs(previous.uiStartX + previous.uiWidth - from * pixelsPerFrame) < .01f)
            { previous.endFrame += to - from; previous.uiWidth += (to-from)*pixelsPerFrame; continue; }
            result.Add(new ClipSegment { sourceClip = chosen.sourceClip, path = chosen.path,
                startFrame = start, endFrame = start + to - from,
                uiStartX = from * pixelsPerFrame, uiWidth = (to-from)*pixelsPerFrame,
                useClipGrade = chosen.useClipGrade, brightness = chosen.brightness,
                contrast = chosen.contrast, saturation = chosen.saturation });
        }
        return result;
    }

    public void InstallVideoTracks(EditorManager editor)
    {
        if (HasMultipleVideoTracks || editor == null || editor.EditingLevel < 2) return;
        var first = editor.timelineContainer as RectTransform;
        if (first == null || first.GetComponent<TimelineDropZone>() == null || first.parent != scrollContent) return;
        float spacing = first.rect.height + 16f;
        Vector2 position = first.anchoredPosition;
        // Existing artwork uses bottom-anchored lanes; make room below them.
        foreach (RectTransform row in scrollContent) row.anchoredPosition += Vector2.up * spacing;
        scrollContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, scrollContent.rect.height + spacing);
        var second = new GameObject("Video Track 2", typeof(RectTransform), typeof(Image), typeof(TimelineDropZone)).GetComponent<RectTransform>();
        second.gameObject.layer = first.gameObject.layer;
        second.SetParent(scrollContent, false);
        second.anchorMin = first.anchorMin; second.anchorMax = first.anchorMax;
        second.pivot = first.pivot; second.sizeDelta = first.sizeDelta;
        second.anchoredPosition = position;
        var source = first.GetComponent<Image>();
        var image = second.GetComponent<Image>();
        image.sprite = source != null ? source.sprite : null;
        image.type = source != null ? source.type : Image.Type.Simple;
        image.color = source != null ? source.color : new Color(.13f,.13f,.15f);
        videoTracks = new[] { first, second };
        editor.timelineContainer = scrollContent;
        foreach (var compiler in FindObjectsOfType<CommercialCompiler>(true))
            if (compiler.videoTimelineContainer == first || first.IsChildOf(compiler.videoTimelineContainer))
                compiler.videoTimelineContainer = scrollContent;
        for (int i = 0; i < videoTracks.Length; i++)
        {
            var label = new GameObject("Video track label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(videoTracks[i], false);
            var rt = (RectTransform)label.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0,1); rt.pivot = new Vector2(0,1);
            rt.anchoredPosition = new Vector2(8,-4); rt.sizeDelta = new Vector2(140,24);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.text = "VIDEO " + (i+1); text.fontSize = 16; text.color = Color.white; text.raycastTarget = false;
        }
        RefreshTimeline();
    }

    public bool TryFitVideoRange(DraggableClip clip, float start, float width)
    {
        if (!HasMultipleVideoTracks) return true;
        var current = clip.transform.parent as RectTransform;
        if (System.Array.IndexOf(videoTracks, current) < 0) return true;
        if (VideoRangeFree(current, clip, start, width)) return true;
        foreach (var track in videoTracks)
        {
            if (track == current || !VideoRangeFree(track, clip, start, width)) continue;
            var rect = (RectTransform)clip.transform;
            Vector2 position = rect.anchoredPosition;
            rect.SetParent(track, false); rect.anchoredPosition = position;
            RevealTrack(track);
            return true;
        }
        return false;
    }

    private static bool VideoRangeFree(RectTransform track, DraggableClip moving, float start, float width)
    {
        if (start < -.01f) return false;
        foreach (var clip in track.GetComponentsInChildren<DraggableClip>())
        {
            if (clip == moving || !clip.isOnTimeline) continue;
            float left = GokeSequence.Left(clip);
            float right = left + ((RectTransform)clip.transform).rect.width;
            if (start < right - .01f && start + width > left + .01f) return false;
        }
        return true;
    }

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
