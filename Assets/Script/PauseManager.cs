using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class PauseManager : MonoBehaviour
{
    public static bool isPaused = false;

    [Header("UI References")]
    public GameObject pauseMenuCanvas; // The main Panel holding your design

    private Player.Manager.InputManager inputManager;
    private Player.PlayerController.PlayerController playerController;
    private bool playerWasEnabled = true;
    private bool playerCouldMove = true;
    private bool playerCouldLook = true;
    private CursorLockMode previousCursorLockState = CursorLockMode.Locked;
    private bool previousCursorVisible = false;

    // --- FIX: Reset the static variable when the scene loads ---
    void Start()
    {
        isPaused = false;
        Time.timeScale = 1f;
        inputManager = FindObjectOfType<Player.Manager.InputManager>();
    }

    void Update()
    {
        if (inputManager == null) inputManager = FindObjectOfType<Player.Manager.InputManager>();

        Keyboard keyboard = Keyboard.current;
        bool pausePressed = inputManager != null ?
                            inputManager.ConsumePause() :
                            keyboard != null && keyboard.escapeKey.wasPressedThisFrame;

        if (pausePressed)
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
        if (pauseMenuCanvas != null) pauseMenuCanvas.SetActive(false);
        Time.timeScale = 1f; // Resumes game physics/animations
        isPaused = false;

        Cursor.lockState = previousCursorLockState;
        Cursor.visible = previousCursorVisible;

        RestorePlayerControls();
    }

    void Pause()
    {
        previousCursorLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        CapturePlayerControls();

        if (pauseMenuCanvas != null) pauseMenuCanvas.SetActive(true);
        Time.timeScale = 0f; 
        isPaused = true;


        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (playerController != null) playerController.enabled = false;
    }

    void CapturePlayerControls()
    {
        playerController = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (playerController == null) return;

        playerWasEnabled = playerController.enabled;
        playerCouldMove = playerController.canMove;
        playerCouldLook = playerController.canLook;
    }

    void RestorePlayerControls()
    {
        if (playerController == null) return;

        playerController.canMove = playerCouldMove;
        playerController.canLook = playerCouldLook;
        playerController.enabled = playerWasEnabled;
        playerController = null;
    }
}
