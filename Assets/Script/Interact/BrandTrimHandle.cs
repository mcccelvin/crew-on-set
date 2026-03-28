using UnityEngine;
using UnityEngine.EventSystems;

public class BrandTrimHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public bool isLeftEdge;

    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log($"BRANDING: You grabbed the {(isLeftEdge ? "Left" : "Right")} handle!");
    }

    public void OnDrag(PointerEventData eventData)
    {
        BrandingClip parentClip = GetComponentInParent<BrandingClip>();
        if (parentClip != null)
        {
            parentClip.Trim(isLeftEdge, eventData);
        }
    }
}