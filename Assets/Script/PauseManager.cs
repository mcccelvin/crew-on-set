using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    public static bool isPaused = false;

    [Header("UI References")]
    public GameObject pauseMenuCanvas; // The main Panel holding your design

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (isPaused) Resume();
            else Pause();
        }
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
        Time.timeScale = 0f; // Freezes everything in the game
        isPaused = true;

        // Free the mouse so you can click the buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnablePlayerControls(false);
    }

    public void ExitToMain()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu"); // Make sure your main menu scene name matches!
    }

    void EnablePlayerControls(bool state)
    {
        Player.PlayerController.PlayerController player = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (player != null) player.enabled = state;
    }
}