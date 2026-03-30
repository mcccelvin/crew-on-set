using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraggableClip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public string clipFilePath;
    [HideInInspector] public Transform originalParent;

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
            if (clipBank != null && transform.parent != clipBank) transform.SetParent(clipBank);
        }
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (ClipInspector.Instance == null)
            {
                ClipInspector fallback = FindObjectOfType<ClipInspector>(true);
                if (fallback != null) ClipInspector.Instance = fallback;
            }

            if (ClipInspector.Instance != null)
            {
                ClipInspector.Instance.OpenInspector(this);

                // --- NEW TUTORIAL PING ---
                if (EditorTutorialManager.Instance != null) EditorTutorialManager.Instance.OnVideoTrimWindowOpened();
            }
            else
            {
                Debug.LogError("CLIP ERROR: The Clip Inspector is completely missing from the scene!");
            }
        }
    }
}