using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraggableOverlay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public Transform originalParent;
    private int originalSiblingIndex;
    private CanvasGroup canvasGroup;

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

            RectTransform rect = GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = origSizeDelta;
                rect.localScale = origLocalScale;
                rect.anchorMin = origAnchorMin;
                rect.anchorMax = origAnchorMax;
                rect.pivot = origPivot;
                rect.anchoredPosition = Vector2.zero;
            }
        }

        // --- THE FIX: Revert the highlight back to the bin if dropped in the wrong spot! ---
        try
        {
            if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
                EditorTutorialManager.Instance.OnClipDragCancelled();
        }
        catch { }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();

        if (!isOnTimeline)
        {
            RectTransform rect = GetComponent<RectTransform>();
            if (rect != null)
            {
                origSizeDelta = rect.sizeDelta;
                origLocalScale = rect.localScale;
                origAnchorMin = rect.anchorMin;
                origAnchorMax = rect.anchorMax;
                origPivot = rect.pivot;
            }

            Canvas parentCanvas = GetComponentInParent<Canvas>();
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
            catch { }
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0.6f;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            RectTransform rect = GetComponent<RectTransform>();
            rect.anchoredPosition += eventData.delta / parentCanvas.scaleFactor;

            if (isOnTimeline)
            {
                ClampToParent();
            }
        }
    }

    public void ClampToParent()
    {
        RectTransform rect = GetComponent<RectTransform>();
        RectTransform parentRect = transform.parent as RectTransform;

        if (rect != null && parentRect != null)
        {
            Vector3[] parentCorners = new Vector3[4];
            parentRect.GetLocalCorners(parentCorners);

            float width = rect.rect.width * rect.localScale.x;
            float height = rect.rect.height * rect.localScale.y;

            Vector2 minPos = new Vector2(parentCorners[0].x + (width * rect.pivot.x),
                                         parentCorners[0].y + (height * rect.pivot.y));

            Vector2 maxPos = new Vector2(parentCorners[2].x - (width * (1f - rect.pivot.x)),
                                         parentCorners[2].y - (height * (1f - rect.pivot.y)));

            Vector2 clampedPos = rect.localPosition;

            if (minPos.x <= maxPos.x) clampedPos.x = Mathf.Clamp(clampedPos.x, minPos.x, maxPos.x);
            if (minPos.y <= maxPos.y) clampedPos.y = Mathf.Clamp(clampedPos.y, minPos.y, maxPos.y);

            rect.localPosition = clampedPos;
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
                    GetComponent<RectTransform>().anchoredPosition = tvLocal;

                    ClampToParent();
                }
                else
                {
                    GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
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