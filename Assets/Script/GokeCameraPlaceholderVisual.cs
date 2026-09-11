using UnityEngine;

public class GokeCameraPlaceholderVisual : MonoBehaviour
{
    private void Awake()
    {
        // Keep this legacy component compatible with existing prefab references.
        // The prefab already contains the real camera mesh; do not replace it.
        enabled = false;
    }
}
