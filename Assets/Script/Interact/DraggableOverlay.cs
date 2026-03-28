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

    // --- NEW: THE INVISIBILITY CLOAK ---
    public void EvaluateVisibility(int currentPlaybackFrame, bool isVideoPlaying)
    {
        if (canvasGroup == null) return;

        // If the video is STOPPED, always show the logo so you can edit it!
        if (!isVideoPlaying)
        {
            canvasGroup.alpha = 1f;
            return;
        }

        // If PLAYING, only show the logo if the playhead is inside its zone!
        if (currentPlaybackFrame >= startFrame && currentPlaybackFrame <= endFrame)
        {
            canvasGroup.alpha = 1f; // Visible!
        }
        else
        {
            canvasGroup.alpha = 0f; // Invisible!
        }
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
                // Grab the list of manual tracks you set up in the Inspector!
                Transform[] availableTracks = EditorManager.Instance.brandingTracks;

                if (availableTracks == null || availableTracks.Length == 0)
                {
                    Debug.LogError("CRITICAL: You forgot to assign your manual Tracks in EditorManager!");
                    return;
                }

                // Snap perfectly to the left edge (0 seconds)
                float newStartX = 0f;
                float newEndX = 80f;

                Transform targetTrack = null;

                // --- 1. SEARCH THE MANUAL TRACKS ---
                foreach (Transform track in availableTracks)
                {
                    bool overlapFound = false;

                    // Check every clip currently sitting inside this specific track
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
                                break; // Stop checking, this track is blocked!
                            }
                        }
                    }

                    if (!overlapFound)
                    {
                        targetTrack = track;
                        break; // We found an empty track, stop looking!
                    }
                }

                // --- 2. SPAWN THE CLIP ---
                if (targetTrack != null)
                {
                    // Spawn it directly inside the winning track folder!
                    GameObject newClip = Instantiate(EditorManager.Instance.brandClipPrefab, targetTrack);
                    RectTransform newRect = newClip.GetComponent<RectTransform>();

                    if (newRect != null)
                    {
                        // Because you already positioned the manual tracks, we just snap this to 0,0!
                        newRect.anchoredPosition = new Vector2(0, 0);
                    }

                    BrandingClip clipScript = newClip.GetComponent<BrandingClip>();
                    if (clipScript != null) clipScript.linkedOverlay = this;

                    isOnTimeline = true;
                }
                else
                {
                    Debug.LogWarning("TIMELINE FULL: No empty branding tracks available at the 0 second mark!");
                    transform.SetParent(originalParent); // Bounce it back to the assets bin
                }
            }
        }
        else
        {
            if (!isOnTimeline) transform.SetParent(originalParent);
        }
    }
}
