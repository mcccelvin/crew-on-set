using UnityEngine;
using UnityEngine.EventSystems;

// --- ADDED: IBeginDragHandler and IEndDragHandler ---
public class BrandingClip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public DraggableOverlay linkedOverlay;
    private float pixelsPerFrame = 40f / 24f;

    [Header("Bulletproof Handles")]
    public RectTransform leftHandle;
    public RectTransform rightHandle;

    private RectTransform myRect;
    private CanvasGroup canvasGroup;

    // Tracking drag data
    private Transform originalParent;
    private Vector2 originalLocalPos;
    private Canvas parentCanvas;

    void Awake()
    {
        myRect = GetComponent<RectTransform>();

        // Automatically add a CanvasGroup so we can block/unblock raycasts during drag
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        parentCanvas = GetComponentInParent<Canvas>();

        // FORCE BOTTOM-LEFT ANCHORS
        myRect.anchorMin = new Vector2(0, 0);
        myRect.anchorMax = new Vector2(0, 0);
        myRect.pivot = new Vector2(0, 0);
    }

    void Start()
    {
        if (myRect.sizeDelta.x < 30f)
        {
            myRect.sizeDelta = new Vector2(80f, myRect.sizeDelta.y);
        }
    }

    void LateUpdate()
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

    // ==========================================
    // 1. START DRAGGING
    // ==========================================
    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        originalLocalPos = myRect.anchoredPosition;

        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();

        // Move the clip to the absolute top UI layer so it renders over everything while dragging
        transform.SetParent(parentCanvas.transform);

        // Turn off raycasts so the mouse can "see" the tracks underneath the clip
        canvasGroup.blocksRaycasts = false;
    }

    // ==========================================
    // 2. WHILE DRAGGING
    // ==========================================
    public void OnDrag(PointerEventData eventData)
    {
        if (parentCanvas == null) return;

        // Allow the clip to move completely freely in X and Y
        myRect.anchoredPosition += eventData.delta / parentCanvas.scaleFactor;
    }

    // ==========================================
    // 3. DROP THE CLIP
    // ==========================================
    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true; // Turn collision back on

        // Find out what UI element our mouse was hovering over when we let go
        GameObject droppedOn = eventData.pointerCurrentRaycast.gameObject;
        Transform targetTrack = null;

        // A. Verify if we dropped it onto a valid Branding Track
        if (droppedOn != null && EditorManager.Instance != null)
        {
            foreach (Transform track in EditorManager.Instance.brandingTracks)
            {
                if (droppedOn.transform == track || droppedOn.transform.IsChildOf(track))
                {
                    targetTrack = track;
                    break;
                }
            }
        }

        // B. Snap it into the new track (If valid)
        if (targetTrack != null)
        {
            // Convert our mouse position into the new Track's local timeline space
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetTrack.GetComponent<RectTransform>(),
                Input.mousePosition,
                eventData.pressEventCamera,
                out localPoint
            );

            float newStartX = Mathf.Max(0, localPoint.x); // Don't let it go past 0 seconds
            float newEndX = newStartX + myRect.rect.width;
            bool overlapFound = false;

            // Check if the space on this new track is already occupied!
            foreach (Transform childClip in targetTrack)
            {
                if (childClip == this.transform) continue;

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
                // SUCCESS! Lock it into the target track
                transform.SetParent(targetTrack);
                myRect.anchoredPosition = new Vector2(newStartX, 0); // Force Y back to 0 (centered in track)
            }
            else
            {
                // FAIL: There is a clip in the way! Bounce it back to where it was.
                Debug.LogWarning("TRACK BLOCKED: That space is already taken!");
                transform.SetParent(originalParent);
                myRect.anchoredPosition = originalLocalPos;
            }
        }
        else
        {
            // FAIL: You dropped it outside the timeline. Bounce it back.
            transform.SetParent(originalParent);
            myRect.anchoredPosition = originalLocalPos;
        }

        UpdateFrameMath();
    }

    public void Trim(bool isLeft, PointerEventData eventData)
    {
        if (myRect == null) return;
        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();

        float dragAmount = eventData.delta.x / parentCanvas.scaleFactor;
        float currentWidth = myRect.sizeDelta.x;

        if (isLeft)
        {
            float newWidth = currentWidth - dragAmount;
            if (newWidth > 30f)
            {
                myRect.sizeDelta = new Vector2(newWidth, myRect.sizeDelta.y);
                myRect.anchoredPosition += new Vector2(dragAmount, 0);
            }
        }
        else
        {
            float newWidth = currentWidth + dragAmount;
            if (newWidth > 30f) myRect.sizeDelta = new Vector2(newWidth, myRect.sizeDelta.y);
        }

        UpdateFrameMath();

        // --- NEW TUTORIAL PING ---
        if (EditorTutorialManager.Instance != null)
        {
            EditorTutorialManager.Instance.OnBrandTrimmed();
        }
    }

    private void UpdateFrameMath()
    {
        if (linkedOverlay != null)
        {
            float startX = myRect.anchoredPosition.x;
            float width = myRect.sizeDelta.x;
            linkedOverlay.startFrame = Mathf.RoundToInt(startX / pixelsPerFrame);
            linkedOverlay.endFrame = Mathf.RoundToInt((startX + width) / pixelsPerFrame);
            Debug.Log($"<color=cyan>LOGO TIMING:</color> Appears at {linkedOverlay.startFrame}, Disappears at {linkedOverlay.endFrame}");
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            Debug.Log("BRANDING: Right-clicked! Deleting logo...");
            if (linkedOverlay != null) Destroy(linkedOverlay.gameObject);
            Destroy(gameObject);
        }
    }
}