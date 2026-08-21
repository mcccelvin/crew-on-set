using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraggableOverlay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public Transform originalParent;
    private int originalSiblingIndex;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private readonly Vector3[] parentCorners = new Vector3[4];
    private readonly Vector3[] overlayCorners = new Vector3[4];

    private Vector2 origSizeDelta;
    private Vector3 origLocalScale;
    private Vector2 origAnchorMin;
    private Vector2 origAnchorMax;
    private Vector2 origPivot;

    public int startFrame = 0;
    public int endFrame = 48;
    public bool isOnTimeline = false;

    public int fadeFrames = 24;

    private void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
    }

    public void ReturnToBin()
    {
        isOnTimeline = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        if (originalParent != null)
        {
            transform.SetParent(originalParent, false);
            transform.SetSiblingIndex(originalSiblingIndex);

            if (rectTransform != null)
            {
                rectTransform.sizeDelta = origSizeDelta;
                rectTransform.localScale = origLocalScale;
                rectTransform.anchorMin = origAnchorMin;
                rectTransform.anchorMax = origAnchorMax;
                rectTransform.pivot = origPivot;
                rectTransform.anchoredPosition = Vector2.zero;
            }
        }

        // --- THE FIX: Revert the highlight back to the bin if dropped in the wrong spot! ---
        try
        {
            if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
                EditorTutorialManager.Instance.OnClipDragCancelled();
        }
        catch (System.Exception e) { Debug.LogException(e, this); }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();

        if (!isOnTimeline)
        {
            if (rectTransform != null)
            {
                origSizeDelta = rectTransform.sizeDelta;
                origLocalScale = rectTransform.localScale;
                origAnchorMin = rectTransform.anchorMin;
                origAnchorMax = rectTransform.anchorMax;
                origPivot = rectTransform.pivot;
            }

            if (parentCanvas != null)
            {
                transform.SetParent(parentCanvas.transform, true);
            }

            // --- THE FIX: Tell the Tutorial Manager we started dragging so it highlights the TV Screen! ---
            try
            {
                if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
                    EditorTutorialManager.Instance.OnClipDragStarted();
            }
            catch (System.Exception e) { Debug.LogException(e, this); }
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0.6f;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (parentCanvas != null && rectTransform != null)
        {
            rectTransform.anchoredPosition += eventData.delta / parentCanvas.scaleFactor;

            if (isOnTimeline)
            {
                ClampToParent();
            }
        }
    }

    public void ClampToParent()
    {
        RectTransform parentRect = transform.parent as RectTransform;

        if (rectTransform != null && parentRect != null)
        {
            parentRect.GetLocalCorners(parentCorners);

            float width = rectTransform.rect.width * rectTransform.localScale.x;
            float height = rectTransform.rect.height * rectTransform.localScale.y;

            Vector2 minPos = new Vector2(parentCorners[0].x + (width * rectTransform.pivot.x),
                                         parentCorners[0].y + (height * rectTransform.pivot.y));

            Vector2 maxPos = new Vector2(parentCorners[2].x - (width * (1f - rectTransform.pivot.x)),
                                         parentCorners[2].y - (height * (1f - rectTransform.pivot.y)));

            Vector2 clampedPos = rectTransform.localPosition;

            if (minPos.x <= maxPos.x) clampedPos.x = Mathf.Clamp(clampedPos.x, minPos.x, maxPos.x);
            if (minPos.y <= maxPos.y) clampedPos.y = Mathf.Clamp(clampedPos.y, minPos.y, maxPos.y);

            rectTransform.localPosition = clampedPos;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        if (isOnTimeline)
        {
            ClampToParent();
            return;
        }

        GameObject droppedOn = eventData.pointerCurrentRaycast.gameObject;
        TruePixelPlayer tvPlayer = FindObjectOfType<TruePixelPlayer>();

        bool droppedOnTV = false;
        if (droppedOn != null && tvPlayer != null && tvPlayer.computerScreen != null)
        {
            if (droppedOn.transform == tvPlayer.computerScreen.transform || droppedOn.transform.IsChildOf(tvPlayer.computerScreen.transform))
            {
                droppedOnTV = true;
            }
        }

        bool droppedOnTrack = false;
        Transform targetTrack = null;
        float spawnX = 0f;

        if (droppedOn != null && EditorManager.Instance != null)
        {
            foreach (Transform track in EditorManager.Instance.brandingTracks)
            {
                if (droppedOn.transform == track || droppedOn.transform.IsChildOf(track))
                {
                    droppedOnTrack = true;
                    targetTrack = track;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(track.GetComponent<RectTransform>(), eventData.position, eventData.pressEventCamera, out Vector2 localPoint);
                    spawnX = Mathf.Max(0, localPoint.x);
                    break;
                }
            }
        }

        if (droppedOnTV || droppedOnTrack)
        {
            if (droppedOnTV && !droppedOnTrack)
            {
                if (TimelinePlayhead.Instance != null)
                {
                    spawnX = TimelinePlayhead.Instance.GetComponent<RectTransform>().anchoredPosition.x;
                }

                foreach (Transform track in EditorManager.Instance.brandingTracks)
                {
                    bool overlap = false;
                    foreach (Transform child in track)
                    {
                        RectTransform rt = child.GetComponent<RectTransform>();
                        if (rt != null)
                        {
                            float s = rt.anchoredPosition.x;
                            float e = s + rt.rect.width;
                            if (spawnX < e && (spawnX + 80f) > s) { overlap = true; break; }
                        }
                    }
                    if (!overlap) { targetTrack = track; break; }
                }
            }

            if (targetTrack != null)
            {
                GameObject newClip = Instantiate(EditorManager.Instance.brandClipPrefab, targetTrack);
                RectTransform newRect = newClip.GetComponent<RectTransform>();
                if (newRect != null) newRect.anchoredPosition = new Vector2(spawnX, 0);

                BrandingClip clipScript = newClip.GetComponent<BrandingClip>();
                if (clipScript != null) clipScript.linkedOverlay = this;

                isOnTimeline = true;

                transform.SetParent(tvPlayer.computerScreen.transform, false);

                if (droppedOnTV)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(tvPlayer.computerScreen.GetComponent<RectTransform>(), eventData.position, eventData.pressEventCamera, out Vector2 tvLocal);
                    rectTransform.anchoredPosition = tvLocal;

                    ClampToParent();
                }
                else
                {
                    rectTransform.anchoredPosition = Vector2.zero;
                }

                PrepareForCommercialOutput();

                if (EditorTutorialManager.Instance != null) EditorTutorialManager.Instance.OnBrandDroppedToScreen();

                // --- THE FIX: Stop the script here so it doesn't accidentally run "ReturnToBin()" below! ---
                return;
            }
        }

        // If you dropped it anywhere else, send it back!
        ReturnToBin();
    }

    private void PrepareForCommercialOutput()
    {
        RectTransform previewRect = transform.parent as RectTransform;
        if (rectTransform == null || previewRect == null) return;

        float visibleWidth = rectTransform.rect.width * Mathf.Abs(rectTransform.localScale.x);
        float visibleHeight = rectTransform.rect.height * Mathf.Abs(rectTransform.localScale.y);
        float maximumWidth = previewRect.rect.width * 0.34f;
        float maximumHeight = previewRect.rect.height * 0.22f;

        float scaleAmount = 1f;
        if (visibleWidth > maximumWidth) scaleAmount = Mathf.Min(scaleAmount, maximumWidth / visibleWidth);
        if (visibleHeight > maximumHeight) scaleAmount = Mathf.Min(scaleAmount, maximumHeight / visibleHeight);

        if (scaleAmount < 1f) rectTransform.localScale *= scaleAmount;

        AddReadabilityOutline();
        KeepInsideTitleSafeArea();
    }

    private void AddReadabilityOutline()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            if (graphic == null || graphic.GetComponent<Outline>() != null) continue;

            Outline outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.72f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
        }
    }

    private void KeepInsideTitleSafeArea()
    {
        RectTransform previewRect = transform.parent as RectTransform;
        if (rectTransform == null || previewRect == null) return;

        rectTransform.GetWorldCorners(overlayCorners);
        Vector3 localBottomLeft = previewRect.InverseTransformPoint(overlayCorners[0]);
        Vector3 localTopRight = previewRect.InverseTransformPoint(overlayCorners[2]);

        float horizontalMargin = previewRect.rect.width * 0.06f;
        float verticalMargin = previewRect.rect.height * 0.06f;
        float safeLeft = previewRect.rect.xMin + horizontalMargin;
        float safeRight = previewRect.rect.xMax - horizontalMargin;
        float safeBottom = previewRect.rect.yMin + verticalMargin;
        float safeTop = previewRect.rect.yMax - verticalMargin;

        Vector2 correction = Vector2.zero;
        if (localBottomLeft.x < safeLeft) correction.x += safeLeft - localBottomLeft.x;
        if (localTopRight.x > safeRight) correction.x -= localTopRight.x - safeRight;
        if (localBottomLeft.y < safeBottom) correction.y += safeBottom - localBottomLeft.y;
        if (localTopRight.y > safeTop) correction.y -= localTopRight.y - safeTop;

        rectTransform.anchoredPosition += correction;
    }

    public bool IsProfessionalPlacement()
    {
        RectTransform previewRect = transform.parent as RectTransform;
        if (rectTransform == null || previewRect == null || !isOnTimeline) return false;

        rectTransform.GetWorldCorners(overlayCorners);
        Vector3 localBottomLeft = previewRect.InverseTransformPoint(overlayCorners[0]);
        Vector3 localTopRight = previewRect.InverseTransformPoint(overlayCorners[2]);

        float horizontalMargin = previewRect.rect.width * 0.05f;
        float verticalMargin = previewRect.rect.height * 0.05f;
        bool insideSafeArea = localBottomLeft.x >= previewRect.rect.xMin + horizontalMargin &&
                              localTopRight.x <= previewRect.rect.xMax - horizontalMargin &&
                              localBottomLeft.y >= previewRect.rect.yMin + verticalMargin &&
                              localTopRight.y <= previewRect.rect.yMax - verticalMargin;

        float overlayArea = Mathf.Abs((localTopRight.x - localBottomLeft.x) * (localTopRight.y - localBottomLeft.y));
        float previewArea = Mathf.Max(1f, previewRect.rect.width * previewRect.rect.height);
        float coverage = overlayArea / previewArea;
        bool readableScale = coverage >= 0.0025f && coverage <= 0.22f;
        bool usefulDuration = endFrame - startFrame >= Mathf.RoundToInt(TapeSettings.framesPerSecond * 2f);

        return insideSafeArea && readableScale && usefulDuration;
    }

    public void EvaluateVisibility(int currentFrame, bool isPlaying)
    {
        if (!isOnTimeline || canvasGroup == null) return;

        if (!isPlaying && currentFrame == 0)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            return;
        }

        if (currentFrame >= startFrame && currentFrame <= endFrame)
        {
            float targetAlpha = 1f;
            if (fadeFrames > 0)
            {
                if (currentFrame < startFrame + fadeFrames)
                {
                    targetAlpha = Mathf.Clamp01((float)(currentFrame - startFrame) / fadeFrames);
                }
                else if (currentFrame > endFrame - fadeFrames)
                {
                    targetAlpha = Mathf.Clamp01((float)(endFrame - currentFrame) / fadeFrames);
                }
            }
            canvasGroup.alpha = targetAlpha;

            canvasGroup.blocksRaycasts = (targetAlpha > 0f);
        }
        else
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
