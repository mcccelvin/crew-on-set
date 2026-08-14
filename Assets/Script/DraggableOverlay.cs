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
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(track.GetComponent<RectTransform>(), Input.mousePosition, eventData.pressEventCamera, out Vector2 localPoint);
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
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(tvPlayer.computerScreen.GetComponent<RectTransform>(), Input.mousePosition, eventData.pressEventCamera, out Vector2 tvLocal);
                    rectTransform.anchoredPosition = tvLocal;

                    ClampToParent();
                }
                else
                {
                    rectTransform.anchoredPosition = Vector2.zero;
                }

                EditorTutorialManager.Instance.OnBrandDroppedToScreen();

                // --- THE FIX: Stop the script here so it doesn't accidentally run "ReturnToBin()" below! ---
                return;
            }
        }

        // If you dropped it anywhere else, send it back!
        ReturnToBin();
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
