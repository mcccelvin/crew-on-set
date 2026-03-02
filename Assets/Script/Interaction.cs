using UnityEngine;

public class Interaction : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float interactRange = 3f;
    public LayerMask interactLayer;

    // Called when the player presses E
    public void OnInteract()
    {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactLayer))
        {
            CameraPickup pickup = hit.collider.GetComponent<CameraPickup>();
            if (pickup != null)
            {
                pickup.Equip();
            }
        }
    }
}
