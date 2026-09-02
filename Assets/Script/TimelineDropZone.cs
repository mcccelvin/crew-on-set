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
                // 1. Move the clip to the timeline. 
                // We use "true" here so it perfectly freezes at the exact pixel your mouse dropped it on!
                droppedClip.transform.SetParent(this.transform, true);

                // WE COMPLETELY REMOVED THE "originalParent" OVERWRITE HERE.
                // The clip now uses its internal "True Memory" to remember the bin!

                // 2. Tell the clip it's on the timeline so it can update the TV and expand the container
                droppedClip.OnPlacedOnTimeline();
            }
        }
    }
}
