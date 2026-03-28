using UnityEngine;
using UnityEngine.EventSystems;

// Added IPointerClickHandler here to detect Right Clicks!
public class BrandingClip : MonoBehaviour, IDragHandler, IPointerClickHandler
{
    public DraggableOverlay linkedOverlay;
    private float pixelsPerFrame = 40f / 24f;

    [Header("Bulletproof Handles")]
    public RectTransform leftHandle;
    public RectTransform rightHandle;

    private RectTransform myRect;

    void Awake()
    {
        myRect = GetComponent<RectTransform>();

        // FORCE BOTTOM-LEFT ANCHORS
        myRect.anchorMin = new Vector2(0, 0);
        myRect.anchorMax = new Vector2(0, 0);
        myRect.pivot = new Vector2(0, 0);
    }

    void Start()
    {
        // THE FIX: We removed the line that forced X to 0.
        // Now it will stay exactly where your mouse drops it!
        myRect.sizeDelta = new Vector2(80f, myRect.sizeDelta.y);
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

    public void OnDrag(PointerEventData eventData)
    {
        myRect.anchoredPosition += new Vector2(eventData.delta.x, 0);
        if (myRect.anchoredPosition.x < 0) myRect.anchoredPosition = new Vector2(0, myRect.anchoredPosition.y);
        UpdateFrameMath();
    }

    public void Trim(bool isLeft, PointerEventData eventData)
    {
        if (myRect == null) return;
        float dragAmount = eventData.delta.x;
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

    // --- THE NEW RIGHT-CLICK LOGIC ---
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            Debug.Log("BRANDING: Right-clicked! Deleting logo...");

            // 1. Destroy the actual logo overlay on the video screen
            if (linkedOverlay != null)
            {
                Destroy(linkedOverlay.gameObject);
            }

            // 2. Destroy this pink clip from the timeline
            Destroy(gameObject);
        }
    }
}