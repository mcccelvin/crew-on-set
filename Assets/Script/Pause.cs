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
