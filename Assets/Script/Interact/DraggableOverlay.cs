using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableOverlay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public Transform originalParent;
    private CanvasGroup canvasGroup;

    public int startFrame = 0;
    public int endFrame = 48;
    private bool isOnTimeline = false;

    private void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void EvaluateVisibility(int currentPlaybackFrame, bool isVideoPlaying)
    {
        if (canvasGroup == null) return;

        if (!isVideoPlaying)
        {
            canvasGroup.alpha = 1f;
            return;
        }

        if (currentPlaybackFrame >= startFrame && currentPlaybackFrame <= endFrame)
            canvasGroup.alpha = 1f;
        else
            canvasGroup.alpha = 0f;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isOnTimeline) originalParent = transform.parent;
        transform.SetParent(transform.root);
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = Input.mousePosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        GameObject droppedOn = eventData.pointerCurrentRaycast.gameObject;

        if (droppedOn != null && droppedOn.name == "Screen")
        {
            transform.SetParent(droppedOn.transform);

            if (!isOnTimeline && EditorManager.Instance != null)
            {
                Transform[] availableTracks = EditorManager.Instance.brandingTracks;

                if (availableTracks == null || availableTracks.Length == 0)
                {
                    Debug.LogError("CRITICAL: You forgot to assign your manual Tracks in EditorManager!");
                    return;
                }

                float newStartX = 0f;
                float newEndX = 80f;

                Transform targetTrack = null;

                foreach (Transform track in availableTracks)
                {
                    bool overlapFound = false;

                    foreach (Transform childClip in track)
                    {
                        RectTransform existingRect = childClip.GetComponent<RectTransform>();
                        if (existingRect != null)
                        {
                            float existStart = existingRect.anchoredPosition.x;
                            float existEnd = existStart + existingRect.rect.width;

                            if (newStartX < existEnd && newEndX > existStart)
                            {
                                overlapFound = true;
                                break;
                            }
                        }
                    }

                    if (!overlapFound)
                    {
                        targetTrack = track;
                        break;
                    }
                }

                if (targetTrack != null)
                {
                    GameObject newClip = Instantiate(EditorManager.Instance.brandClipPrefab, targetTrack);
                    RectTransform newRect = newClip.GetComponent<RectTransform>();

                    if (newRect != null) newRect.anchoredPosition = new Vector2(0, 0);

                    BrandingClip clipScript = newClip.GetComponent<BrandingClip>();
                    if (clipScript != null) clipScript.linkedOverlay = this;

                    isOnTimeline = true;

                    // --- NEW TUTORIAL PING ---
                    if (EditorTutorialManager.Instance != null) EditorTutorialManager.Instance.OnBrandDropped();
                }
                else
                {
                    Debug.LogWarning("TIMELINE FULL: No empty branding tracks available at the 0 second mark!");
                    transform.SetParent(originalParent);
                }
            }
        }
        else
        {
            if (!isOnTimeline) transform.SetParent(originalParent);
        }
    }
}