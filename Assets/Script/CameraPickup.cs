using UnityEngine;

public class CameraPickup : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public GameObject floorCamera;   // Active at start, placed on floor
    public GameObject playerCamera;  // Inactive at start, inside player

    // Called when player presses E
    public void Equip()
    {
        if (floorCamera != null && playerCamera != null)
        {
            // Disable floor camera
            floorCamera.SetActive(false);

            // Enable player camera
            playerCamera.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Camera references not assigned in Inspector!");
        }
    }
}
