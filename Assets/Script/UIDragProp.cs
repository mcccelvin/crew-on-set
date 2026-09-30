using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class UIDragProp : MonoBehaviour, IPointerClickHandler
{
    private GameObject propPrefab3D;
    private DirectorTerminal terminal;

    public void Setup(GameObject prefab, DirectorTerminal term)
    {
        propPrefab3D = prefab;
        terminal = term;

        TextMeshProUGUI label = GetComponentInChildren<TextMeshProUGUI>();
        string displayName = prefab.name.ToLowerInvariant().Contains("cube")
            ? (CampaignProgression.GetCurrentLevel() == 1 ? "Table" : "Box")
            : prefab.name.Replace("Prefab", "");
        if (label != null) label.text = displayName + "\n" + ProductionEconomy.Prop + " B";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (propPrefab3D != null && terminal != null)
        {
            terminal.StartDraggingNewProp(propPrefab3D);
        }
    }
}
