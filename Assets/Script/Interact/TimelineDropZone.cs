using UnityEngine;
using UnityEngine.EventSystems;

public class TimelineDropZone : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null)
        {
            DraggableClip droppedClip = eventData.pointerDrag.GetComponent<DraggableClip>();
            if (droppedClip != null)
            {
                droppedClip.transform.SetParent(this.transform);
                droppedClip.originalParent = this.transform;
            }
        }
    }
}