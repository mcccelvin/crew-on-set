using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraggableClip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    [Header("Clip Data")]
    public string clipFilePath;
    public int totalFrames;
    public int startFrame;
    public int endFrame;
    public float cameraScore;
    public float lightScore;

    [HideInInspector] public Transform originalParent;
    private Transform trueBankParent;
    private int trueBankSiblingIndex;

    public bool isOnTimeline = false;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Canvas parentCanvas;

    private enum DragMode { MoveFree, SlideTimeline, TrimLeft, TrimRight }
    private DragMode currentDragMode = DragMode.MoveFree;

    private float binWidth;
    private float originalWidth;
    private float leftTrimPixels = 0f;
    private float rightTrimPixels = 0f;

    // --- Collision Walls ---
    private float dragMinX;
    private float dragMaxX;

    // --- THE FIX: Custom Double Click Timer ---
    private float lastLeftClickTime = 0f;
    private const float doubleClickThreshold = 0.3f; // 0.3 seconds is the industry standard

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        parentCanvas = GetComponentInParent<Canvas>();
    }

    private void Start()
    {
        trueBankParent = transform.parent;
        trueBankSiblingIndex = transform.GetSiblingIndex();
        binWidth = rectTransform.rect.width;
        originalWidth = binWidth;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (isOnTimeline)
            {
                ReturnToBin();
            }
        }
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            // ======================================================================
            // --- THE FIX: BULLETPROOF DOUBLE CLICK ---
            // Measures raw time, immune to Unity's mouse-twitch drag bug!
            // ======================================================================
            if (Time.time - lastLeftClickTime < doubleClickThreshold)
            {
                ClipInspector inspector = ClipInspector.Instance;

                if (inspector == null)
                {
                    inspector = FindObjectOfType<ClipInspector>(true);
                }

                if (inspector != null)
                {
                    if (ClipInspector.Instance == null) ClipInspector.Instance = inspector;

                    inspector.OpenInspector(this);
                    if (EditorTutorialManager.Instance != null) EditorTutorialManager.Instance.OnVideoTrimWindowOpened();
                }
            }

            // Record the exact time you clicked!
            lastLeftClickTime = Time.time;
        }
    }

    public void ReturnToBin()
    {
        isOnTimeline = false;

        if (trueBankParent != null)
        {
            transform.SetParent(trueBankParent, false);
            transform.SetSiblingIndex(trueBankSiblingIndex);

            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, binWidth);
            leftTrimPixels = 0f;
            rightTrimPixels = 0f;
            UpdateFrameData();

            rectTransform.anchoredPosition3D = Vector3.zero;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;

            LayoutRebuilder.ForceRebuildLayoutImmediate(trueBankParent.GetComponent<RectTransform>());

            TruePixelPlayer tvPlayer = FindObjectOfType<TruePixelPlayer>();
            if (tvPlayer != null) tvPlayer.StopTape();

            if (TimelineManager.Instance != null) TimelineManager.Instance.RefreshTimeline();
        }
    }

    public void AdjustToNewZoom(float zoomRatio)
    {
        float newX = rectTransform.anchoredPosition.x * zoomRatio;
        rectTransform.anchoredPosition = new Vector2(newX, rectTransform.anchoredPosition.y);

        float newWidth = rectTransform.rect.width * zoomRatio;
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newWidth);

        originalWidth *= zoomRatio;
        leftTrimPixels *= zoomRatio;
        rightTrimPixels *= zoomRatio;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right) return;
        if (originalWidth <= 0) originalWidth = rectTransform.rect.width;

        if (isOnTimeline)
        {
            dragMinX = 0f;
            dragMaxX = float.MaxValue;

            float myStart = rectTransform.anchoredPosition.x - (rectTransform.rect.width * rectTransform.pivot.x);
            float myEnd = myStart + rectTransform.rect.width;

            foreach (Transform child in transform.parent)
            {
                if (child == this.transform) continue;
                DraggableClip otherClip = child.GetComponent<DraggableClip>();
                if (otherClip != null && otherClip.isOnTimeline)
                {
                    RectTransform otherRT = otherClip.GetComponent<RectTransform>();
                    float otherStart = otherRT.anchoredPosition.x - (otherRT.rect.width * otherRT.pivot.x);
                    float otherEnd = otherStart + otherRT.rect.width;

                    if (otherEnd <= myStart + 0.1f)
                    {
                        if (otherEnd > dragMinX) dragMinX = otherEnd;
                    }
                    if (otherStart >= myEnd - 0.1f)
                    {
                        if (otherStart < dragMaxX) dragMaxX = otherStart;
                    }
                }
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);
            float width = rectTransform.rect.width;
            float leftEdge = -(width * rectTransform.pivot.x);
            float rightEdge = width * (1f - rectTransform.pivot.x);

            float grabArea = 25f;

            if (localPoint.x < leftEdge + grabArea) currentDragMode = DragMode.TrimLeft;
            else if (localPoint.x > rightEdge - grabArea) currentDragMode = DragMode.TrimRight;
            else currentDragMode = DragMode.SlideTimeline;
        }
        else
        {
            trueBankParent = transform.parent;
            trueBankSiblingIndex = transform.GetSiblingIndex();
            currentDragMode = DragMode.MoveFree;
        }

        if (currentDragMode == DragMode.MoveFree)
        {
            transform.SetParent(parentCanvas.transform, true);
            transform.SetAsLastSibling();
        }

        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.6f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right) return;
        float deltaX = eventData.delta.x / parentCanvas.scaleFactor;

        if (currentDragMode == DragMode.MoveFree)
        {
            rectTransform.anchoredPosition += eventData.delta / parentCanvas.scaleFactor;
        }
        else if (currentDragMode == DragMode.SlideTimeline)
        {
            float newX = rectTransform.anchoredPosition.x + deltaX;
            float myWidth = rectTransform.rect.width;
            float pivotX = rectTransform.pivot.x;

            float minAllowedX = dragMinX + (myWidth * pivotX);
            float maxAllowedX = dragMaxX - (myWidth * (1f - pivotX));

            if (newX < minAllowedX) newX = minAllowedX;
            if (newX > maxAllowedX) newX = maxAllowedX;

            rectTransform.anchoredPosition = new Vector2(newX, rectTransform.anchoredPosition.y);
        }
        else if (currentDragMode == DragMode.TrimLeft)
        {
            float oldWidth = rectTransform.rect.width;
            float newWidth = oldWidth - deltaX;

            float maxWidth = originalWidth - rightTrimPixels;
            if (newWidth > maxWidth)
            {
                newWidth = maxWidth;
                deltaX = oldWidth - newWidth;
            }

            float rightEdge = rectTransform.anchoredPosition.x + (oldWidth * (1f - rectTransform.pivot.x));
            float proposedLeftEdge = rightEdge - newWidth;

            if (proposedLeftEdge < dragMinX)
            {
                newWidth = rightEdge - dragMinX;
                deltaX = oldWidth - newWidth;
            }
            if (proposedLeftEdge < 0)
            {
                newWidth = rightEdge;
                deltaX = oldWidth - newWidth;
            }

            if (newWidth < 20f) { newWidth = 20f; deltaX = oldWidth - newWidth; }

            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newWidth);
            float newX = rightEdge - (newWidth * (1f - rectTransform.pivot.x));
            rectTransform.anchoredPosition = new Vector2(newX, rectTransform.anchoredPosition.y);

            leftTrimPixels = originalWidth - newWidth - rightTrimPixels;
            UpdateFrameData();
        }
        else if (currentDragMode == DragMode.TrimRight)
        {
            float oldWidth = rectTransform.rect.width;
            float newWidth = oldWidth + deltaX;

            float maxWidth = originalWidth - leftTrimPixels;
            if (newWidth > maxWidth)
            {
                newWidth = maxWidth;
                deltaX = newWidth - oldWidth;
            }

            float leftEdge = rectTransform.anchoredPosition.x - (oldWidth * rectTransform.pivot.x);
            float proposedRightEdge = leftEdge + newWidth;

            if (proposedRightEdge > dragMaxX)
            {
                newWidth = dragMaxX - leftEdge;
                deltaX = newWidth - oldWidth;
            }

            if (newWidth < 20f) { newWidth = 20f; deltaX = newWidth - oldWidth; }

            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newWidth);
            float newX = leftEdge + (newWidth * rectTransform.pivot.x);
            rectTransform.anchoredPosition = new Vector2(newX, rectTransform.anchoredPosition.y);

            rightTrimPixels = originalWidth - newWidth - leftTrimPixels;
            UpdateFrameData();
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right) return;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        if (currentDragMode == DragMode.MoveFree && transform.parent == parentCanvas.transform) ReturnToBin();

        if (isOnTimeline)
        {
            TruePixelPlayer tvPlayer = FindObjectOfType<TruePixelPlayer>();
            if (tvPlayer != null) tvPlayer.ShowPreviewFrame(clipFilePath);
        }

        if (TimelineManager.Instance != null) TimelineManager.Instance.RefreshTimeline();
    }

    public void OnPlacedOnTimeline()
    {
        bool isNewDrop = !isOnTimeline;
        isOnTimeline = true;

        TruePixelPlayer tvPlayer = FindObjectOfType<TruePixelPlayer>();

        if (isNewDrop)
        {
            rectTransform.localPosition = new Vector3(rectTransform.localPosition.x, 0f, 0f);
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;

            if (TimelineManager.Instance != null && totalFrames > 0)
            {
                if (TimelineManager.Instance.pixelsPerSecond == 0) TimelineManager.Instance.RefreshTimeline();

                float fps = (tvPlayer != null && tvPlayer.framesPerSecond > 0) ? tvPlayer.framesPerSecond : 24f;
                float durationInSeconds = (float)totalFrames / fps;

                float oldWidth = rectTransform.rect.width;
                float trueTimelineWidth = durationInSeconds * TimelineManager.Instance.pixelsPerSecond;

                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, trueTimelineWidth);

                float widthDifference = trueTimelineWidth - oldWidth;
                rectTransform.anchoredPosition += new Vector2(widthDifference * rectTransform.pivot.x, 0);

                originalWidth = trueTimelineWidth;

                float proposedStart = rectTransform.anchoredPosition.x - (trueTimelineWidth * rectTransform.pivot.x);
                float proposedEnd = proposedStart + trueTimelineWidth;

                bool overlaps = false;
                float maxExistingEnd = 0f;

                foreach (Transform child in transform.parent)
                {
                    if (child == this.transform) continue;
                    DraggableClip otherClip = child.GetComponent<DraggableClip>();
                    if (otherClip != null && otherClip.isOnTimeline)
                    {
                        RectTransform otherRT = otherClip.GetComponent<RectTransform>();
                        float otherStart = otherRT.anchoredPosition.x - (otherRT.rect.width * otherRT.pivot.x);
                        float otherEnd = otherStart + otherRT.rect.width;

                        if (otherEnd > maxExistingEnd) maxExistingEnd = otherEnd;
                        if (proposedStart < otherEnd && proposedEnd > otherStart) overlaps = true;
                    }
                }

                if (overlaps)
                {
                    float newPivotAdjustedX = maxExistingEnd + (trueTimelineWidth * rectTransform.pivot.x);
                    rectTransform.anchoredPosition = new Vector2(newPivotAdjustedX, rectTransform.anchoredPosition.y);
                }

                float minAllowedX = trueTimelineWidth * rectTransform.pivot.x;
                if (rectTransform.anchoredPosition.x < minAllowedX)
                {
                    rectTransform.anchoredPosition = new Vector2(minAllowedX, rectTransform.anchoredPosition.y);
                }

                TimelineManager.Instance.RefreshTimeline();
            }
        }

        if (tvPlayer != null) tvPlayer.ShowPreviewFrame(clipFilePath);
    }

    private void UpdateFrameData()
    {
        if (originalWidth <= 0) originalWidth = rectTransform.rect.width;
        if (totalFrames <= 0) return;

        float pixelsPerFrame = originalWidth / totalFrames;

        startFrame = Mathf.RoundToInt(leftTrimPixels / pixelsPerFrame);
        endFrame = totalFrames - Mathf.RoundToInt(rightTrimPixels / pixelsPerFrame);

        startFrame = Mathf.Clamp(startFrame, 0, totalFrames - 1);
        endFrame = Mathf.Clamp(endFrame, startFrame + 1, totalFrames);
    }
}