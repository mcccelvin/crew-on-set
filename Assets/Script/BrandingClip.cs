using UnityEngine;
using UnityEngine.EventSystems;

public class BrandingClip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public DraggableOverlay linkedOverlay;

    [Header("Bulletproof Handles")]
    public RectTransform leftHandle;
    public RectTransform rightHandle;

    private RectTransform myRect;
    private CanvasGroup canvasGroup;

    private Canvas parentCanvas;
    private Transform dragOriginalParent;
    private int dragOriginalSiblingIndex;
    private Vector2 dragOriginalPosition;

    void Awake()
    {
        myRect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        parentCanvas = GetComponentInParent<Canvas>();

        myRect.anchorMin = new Vector2(0, 0.5f);
        myRect.anchorMax = new Vector2(0, 0.5f);
        myRect.pivot = new Vector2(0, 0.5f);
    }

    void Start()
    {
        if (myRect.sizeDelta.x < 30f) myRect.sizeDelta = new Vector2(80f, myRect.sizeDelta.y);
        myRect.anchoredPosition = new Vector2(myRect.anchoredPosition.x, 0f);
        SetupHandles();
        UpdateFrameMath();
    }

    private void SetupHandles()
    {
        if (leftHandle != null)
        {
            leftHandle.anchorMin = new Vector2(0, 0.5f);
            leftHandle.anchorMax = new Vector2(0, 0.5f);
            leftHandle.anchoredPosition = new Vector2(0, 0);
        }
        if (rightHandle != null)
        {
            rightHandle.anchorMin = new Vector2(1, 0.5f);
            rightHandle.anchorMax = new Vector2(1, 0.5f);
            rightHandle.anchoredPosition = new Vector2(0, 0);
        }
    }

    public void AdjustToNewZoom(float zoomRatio)
    {
        float newX = myRect.anchoredPosition.x * zoomRatio;
        myRect.anchoredPosition = new Vector2(newX, myRect.anchoredPosition.y);

        float newWidth = myRect.sizeDelta.x * zoomRatio;
        myRect.sizeDelta = new Vector2(newWidth, myRect.sizeDelta.y);

        UpdateFrameMath();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            Debug.LogWarning("BRANDING: The timeline clip is not inside a Canvas and cannot be dragged.");
            return;
        }

        dragOriginalParent = transform.parent;
        dragOriginalSiblingIndex = transform.GetSiblingIndex();
        dragOriginalPosition = myRect.anchoredPosition;

        // Let it float freely while dragging so we can switch tracks!
        transform.SetParent(parentCanvas.transform, true);

        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (parentCanvas == null) return;
        myRect.anchoredPosition += eventData.delta / parentCanvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        GameObject droppedOn = eventData.pointerCurrentRaycast.gameObject;
        Transform newTrack = null;

        // Check if we dropped it on a new Branding Track!
        if (droppedOn != null && EditorManager.Instance != null && EditorManager.Instance.brandingTracks != null)
        {
            foreach (Transform track in EditorManager.Instance.brandingTracks)
            {
                if (track != null && (droppedOn.transform == track || droppedOn.transform.IsChildOf(track)))
                {
                    newTrack = track;
                    break;
                }
            }
        }

        if (newTrack == null)
        {
            RestoreAfterCancelledDrag();
            return;
        }

        if (newTrack != null)
        {
            transform.SetParent(newTrack, true);

            // Check for overlaps on the new track
            float proposedX = myRect.anchoredPosition.x;
            if (proposedX < 0) proposedX = 0;

            float myWidth = myRect.rect.width;
            float dragMinX = 0f;
            float dragMaxX = float.MaxValue;

            foreach (Transform child in newTrack)
            {
                if (child == this.transform) continue;
                RectTransform otherRT = child.GetComponent<RectTransform>();
                if (otherRT != null)
                {
                    float otherStart = otherRT.anchoredPosition.x;
                    float otherEnd = otherStart + otherRT.rect.width;

                    if (otherEnd <= proposedX + 0.1f && otherEnd > dragMinX) dragMinX = otherEnd;
                    if (otherStart >= proposedX + myWidth - 0.1f && otherStart < dragMaxX) dragMaxX = otherStart;
                }
            }

            if (proposedX < dragMinX) proposedX = dragMinX;
            if (proposedX + myWidth > dragMaxX) proposedX = dragMaxX - myWidth;

            // Lock it perfectly into the track
            myRect.anchoredPosition = new Vector2(proposedX, 0f);
        }

        UpdateFrameMath();
        NotifyTutorialClipChanged();
    }

    public void Trim(bool isLeft, PointerEventData eventData)
    {
        if (myRect == null) return;
        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();

        float dragAmount = eventData.delta.x / parentCanvas.scaleFactor;

        float currentWidth = myRect.sizeDelta.x;
        float myStart = myRect.anchoredPosition.x;
        float myEnd = myStart + currentWidth;

        float dragMinX = 0f;
        float dragMaxX = float.MaxValue;

        if (transform.parent != null)
        {
            foreach (Transform child in transform.parent)
            {
                if (child == this.transform) continue;
                RectTransform otherRT = child.GetComponent<RectTransform>();
                if (otherRT != null)
                {
                    float otherStart = otherRT.anchoredPosition.x;
                    float otherEnd = otherStart + otherRT.rect.width;

                    if (otherEnd <= myStart + 0.1f && otherEnd > dragMinX) dragMinX = otherEnd;
                    if (otherStart >= myEnd - 0.1f && otherStart < dragMaxX) dragMaxX = otherStart;
                }
            }
        }

        if (isLeft)
        {
            float proposedLeftEdge = myStart + dragAmount;

            if (dragAmount > 0 && proposedLeftEdge < myStart + 5f) proposedLeftEdge = myStart + dragAmount;

            if (proposedLeftEdge < dragMinX) proposedLeftEdge = dragMinX;
            if (proposedLeftEdge < 0f) proposedLeftEdge = 0f; // Hard wall at 0s

            float newWidth = myEnd - proposedLeftEdge;
            if (newWidth < 30f)
            {
                newWidth = 30f;
                proposedLeftEdge = myEnd - 30f;
            }

            myRect.sizeDelta = new Vector2(newWidth, myRect.sizeDelta.y);
            myRect.anchoredPosition = new Vector2(proposedLeftEdge, 0f);
        }
        else
        {
            float proposedRightEdge = myEnd + dragAmount;

            if (proposedRightEdge > dragMaxX) proposedRightEdge = dragMaxX;

            float newWidth = proposedRightEdge - myStart;
            if (newWidth < 30f) newWidth = 30f;

            myRect.sizeDelta = new Vector2(newWidth, myRect.sizeDelta.y);
        }

        UpdateFrameMath();
        NotifyTutorialClipChanged();
    }

    private void UpdateFrameMath()
    {
        if (linkedOverlay != null)
        {
            float pps = 50f;
            if (TimelineManager.Instance != null && TimelineManager.Instance.pixelsPerSecond > 0)
            {
                pps = TimelineManager.Instance.pixelsPerSecond;
            }
            float pixelsPerFrame = pps / TapeSettings.framesPerSecond;

            float startX = myRect.anchoredPosition.x;
            float width = myRect.sizeDelta.x;

            linkedOverlay.startFrame = Mathf.RoundToInt(startX / pixelsPerFrame);
            linkedOverlay.endFrame = Mathf.RoundToInt((startX + width) / pixelsPerFrame);
        }
    }

    private void RestoreAfterCancelledDrag()
    {
        if (dragOriginalParent != null)
        {
            transform.SetParent(dragOriginalParent, false);
            transform.SetSiblingIndex(dragOriginalSiblingIndex);
            myRect.anchoredPosition = dragOriginalPosition;
        }

        UpdateFrameMath();

        if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
        {
            EditorTutorialManager.Instance.OnClipDragCancelled();
        }
    }

    private void NotifyTutorialClipChanged()
    {
        if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
        {
            EditorTutorialManager.Instance.OnBrandingClipChanged(this);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (linkedOverlay != null) linkedOverlay.ReturnToBin();
            Destroy(gameObject);
        }
    }
}
