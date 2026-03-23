using UnityEngine;
using Player.PlayerController;

public class DirectorTerminal : MonoBehaviour
{
    [Header("Cameras")]
    public GameObject topDownCamera;

    [Header("UI")]
    public GameObject tabletUI;

    private GameObject playerCameraObj;
    private PlayerController playerController;

    private void Start()
    {
        if (topDownCamera != null) topDownCamera.SetActive(false);
        if (tabletUI != null) tabletUI.SetActive(false);
    }

    public void OpenTerminal(GameObject pCam, PlayerController pController)
    {
        playerCameraObj = pCam;
        playerController = pController;

        if (playerController != null) playerController.enabled = false;
        if (playerCameraObj != null) playerCameraObj.SetActive(false);

        if (topDownCamera != null) topDownCamera.SetActive(true);
        if (tabletUI != null) tabletUI.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseTerminal()
    {
        if (playerController != null) playerController.enabled = true;
        if (playerCameraObj != null) playerCameraObj.SetActive(true);

        if (topDownCamera != null) topDownCamera.SetActive(false);
        if (tabletUI != null) tabletUI.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}