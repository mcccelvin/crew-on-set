using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    public static bool isPaused = false;

    [Header("UI References")]
    public GameObject pauseMenuCanvas; // The main Panel holding your design

    // --- FIX: Reset the static variable when the scene loads ---
    void Start()
    {
        isPaused = false;
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("Escape Pressed!");
            if (AlmanacManager.Instance != null && AlmanacManager.Instance.IsOpen())
            {
                AlmanacManager.Instance.ToggleAlmanac();
                return;
            }

            if (isPaused) Resume();
            else Pause();
        }
    }

    // ... (Keep your existing Resume and Pause methods exactly the same) ...

    public void ExitToMain()
    {
        Time.timeScale = 1f;
        isPaused = false; // --- FIX: Reset the static variable before leaving ---
        SceneManager.LoadScene(0);
    }
    public void Resume()
    {
        pauseMenuCanvas.SetActive(false);
        Time.timeScale = 1f; // Resumes game physics/animations
        isPaused = false;

        // Re-lock the cursor for gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Re-enable player controls if needed
        EnablePlayerControls(true);
    }

    void Pause()
    {
        pauseMenuCanvas.SetActive(true);
        Time.timeScale = 0f; 
        isPaused = true;


        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnablePlayerControls(false);
    }



    void EnablePlayerControls(bool state)
    {
        Player.PlayerController.PlayerController player = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (player != null) player.enabled = state;
    }
}
