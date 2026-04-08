using UnityEngine;
using UnityEngine.EventSystems;

public class InspectorTrimHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public bool isLeftEdge;

    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log($"INSPECTOR: You grabbed the {(isLeftEdge ? "Left" : "Right")} handle!");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ClipInspector.Instance != null)
        {
            ClipInspector.Instance.HandleDragged(isLeftEdge, eventData);
        }
        else
        {
            Debug.LogError("INSPECTOR ERROR: The ClipInspector brain is disconnected!");
        }
    }
}