using UnityEngine;
using Player.PlayerController; // Needed to pause the player's movement

public class DirectorTerminal : MonoBehaviour
{
    [Header("Cameras")]
    [Tooltip("The camera looking down at the stage")]
    public GameObject topDownCamera;

    [Header("UI")]
    [Tooltip("The Canvas with your builder buttons")]
    public GameObject tabletUI;

    // We store these when the player interacts so we can give them back later
    private GameObject playerCameraObj;
    private PlayerController playerController;

    private void Start()
    {
        // Ensure builder mode is off when the game starts
        if (topDownCamera != null) topDownCamera.SetActive(false);
        if (tabletUI != null) tabletUI.SetActive(false);
    }

    public void OpenTerminal(GameObject pCam, PlayerController pController)
    {
        playerCameraObj = pCam;
        playerController = pController;

        // 1. Disable player controls and camera
        if (playerController != null) playerController.enabled = false;
        if (playerCameraObj != null) playerCameraObj.SetActive(false);

        // 2. Enable top-down view and UI
        if (topDownCamera != null) topDownCamera.SetActive(true);
        if (tabletUI != null) tabletUI.SetActive(true);

        // 3. Unlock mouse so you can click the UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("Entered Director Mode!");
    }

    public void CloseTerminal()
    {
        // 1. Re-enable player controls and camera
        if (playerController != null) playerController.enabled = true;
        if (playerCameraObj != null) playerCameraObj.SetActive(true);

        // 2. Disable top-down view and UI
        if (topDownCamera != null) topDownCamera.SetActive(false);
        if (tabletUI != null) tabletUI.SetActive(false);

        // 3. Lock mouse back to the game
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("Exited Director Mode!");
    }
}