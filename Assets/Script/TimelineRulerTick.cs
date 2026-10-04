using UnityEngine;
using UnityEngine.EventSystems;

public sealed class TimelineRulerTick : MonoBehaviour, IPointerDownHandler
{
    public TimelineRuler ruler;
    public float seconds;
    public void OnPointerDown(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left && ruler != null) ruler.Seek(seconds);
    }
}
