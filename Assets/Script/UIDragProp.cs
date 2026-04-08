using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class UIDragProp : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    private GameObject propPrefab3D;
    private DirectorTerminal terminal;

    public void Setup(GameObject prefab, DirectorTerminal term)
    {
        propPrefab3D = prefab;
        terminal = term;

        TextMeshProUGUI label = GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = prefab.name.Replace("Prefab", "");
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Instantly spawn and attach to mouse the moment you click!
        if (propPrefab3D != null && terminal != null)
        {
            terminal.StartDraggingNewProp(propPrefab3D);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // We MUST include this function! 
        // Even if it's empty, it forces Unity to not cancel our mouse click while we move the mouse.
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Drop it the moment you let go of the mouse button!
        if (terminal != null)
        {
            terminal.DropDraggedProp();
        }
    }
}