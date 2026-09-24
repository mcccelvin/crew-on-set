using UnityEngine;

public class GokeCameraPlaceholderVisual : MonoBehaviour
{
    private void Start()
    {
        var camera = GetComponentInParent<Player.Equipment.FilmCameraItem>();
        if (camera != null && camera.EquipmentName == "Level 2 Camera")
            EquipmentModelVisuals.Camera(camera.transform);
    }
}
