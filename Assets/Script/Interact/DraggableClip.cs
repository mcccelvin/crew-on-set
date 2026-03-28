using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraggableClip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public string clipFilePath;
    [HideInInspector] public Transform originalParent;

    // --- TRIM DATA ---
    [HideInInspector] public int totalFrames;
    [HideInInspector] public int startFrame;
    [HideInInspector] public int endFrame;

    private Transform clipBank;
    private CanvasGroup canvasGroup;

    public float cameraScore;
    public float lightScore;
    private void Start()
    {
        GameObject bankObj = GameObject.Find("Clipbank");
        if (bankObj != null) clipBank = bankObj.transform;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        transform.SetParent(transform.root);
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData) { transform.position = Input.mousePosition; }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        if (transform.parent == transform.root)
        {
            if (clipBank != null) transform.SetParent(clipBank);
            else transform.SetParent(originalParent);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            // Right click sends it back to the Clipbank
            if (clipBank != null && transform.parent != clipBank) transform.SetParent(clipBank);
        }
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            Debug.Log("CLIP: You left-clicked me!");

            // FAILSAFE: If the window is turned off, hunt it down and wake it up!
            if (ClipInspector.Instance == null)
            {
                ClipInspector fallback = FindObjectOfType<ClipInspector>(true); // 'true' searches hidden objects!
                if (fallback != null)
                {
                    ClipInspector.Instance = fallback;
                }
            }

            // Open the window!
            if (ClipInspector.Instance != null)
            {
                ClipInspector.Instance.OpenInspector(this);
            }
            else
            {
                Debug.LogError("CLIP ERROR: The Clip Inspector is completely missing from the scene!");
            }
        }
    }
}
