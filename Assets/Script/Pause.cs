using UnityEngine;

public class Pause : MonoBehaviour
{
    [Header("UI References")]
    public GameObject interactUI;     // UI that toggles with E
    public GameObject pauseMenuUI;    // Pause menu UI

    [Header("Camera References")]
    public Camera mainCamera;         // Default camera (tagged MainCamera)
    public Camera secondaryCamera;    // Alternate camera

    private bool isPaused = false;

    void Start()
    {
        // Ensure main camera is active at start
        if (mainCamera != null) mainCamera.enabled = true;
        if (secondaryCamera != null) secondaryCamera.enabled = false;
    }

    void Update()
    {
        // Toggle interact UI + camera switch with E
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (interactUI != null)
            {
                bool isActive = interactUI.activeSelf;
                interactUI.SetActive(!isActive);

                if (!isActive)
                {
                    // UI just opened → disable main camera, enable secondary
                    if (mainCamera != null) mainCamera.enabled = false;
                    if (secondaryCamera != null) secondaryCamera.enabled = true;
                }
                else
                {
                    // UI just closed → re-enable main camera, disable secondary
                    if (mainCamera != null) mainCamera.enabled = true;
                    if (secondaryCamera != null) secondaryCamera.enabled = false;
                }
            }
        }

        // Toggle pause menu with Esc
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                Resume();
            }
            else
            {
                PauseM();
            }
        }
    }

    public void Resume()
    {
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }
        Time.timeScale = 1f; // Resume game time
        isPaused = false;
    }

    private void PauseM()
    {
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(true);
        }
        Time.timeScale = 0f; // Freeze game time
        isPaused = true;
    }
}
