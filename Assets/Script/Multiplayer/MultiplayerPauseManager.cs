using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using UnityEngine.InputSystem;

public class MultiplayerPauseManager : MonoBehaviour
{
    public static bool isPaused = false;

    [Header("UI References")]
    public GameObject pauseMenuCanvas;
    private SharedOptionsPanel options;
    private UnityEngine.UI.Button optionsButton;

    void Start()
    {
        // Force the game to unpause and hide the menu when the scene loads
        isPaused = false;
        if (pauseMenuCanvas != null) pauseMenuCanvas.SetActive(false);
        if (pauseMenuCanvas != null)
            foreach (var button in pauseMenuCanvas.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                if (button.name == "Option" || button.name == "Options")
                { optionsButton = button; button.onClick.AddListener(OpenOptions); break; }
    }

    void Update()
    {
        // Listen for the Escape key to toggle the menu
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (options != null && options.IsOpen) { options.Close(false); return; }
            if (isPaused) Resume();
            else Pause();
        }
    }

    public void Resume()
    {
        options?.Close(false);
        pauseMenuCanvas.SetActive(false);
        isPaused = false;

        // Re-lock the cursor so the player can look around again
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        EnableLocalPlayerControls(true);
    }

    private void Pause()
    {
        pauseMenuCanvas.SetActive(true);
        isPaused = true;

        // Free the mouse so you can click the UI buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnableLocalPlayerControls(false);
    }

    // --- Wire this to your "Exit" or "Main Menu" UI Button ---
    public void ExitToMain()
    {
        options?.Close(false);
        // We MUST disconnect from the room before switching scenes
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }

        isPaused = false;
        SceneManager.LoadScene(0);
    }

    public void OpenOptions()
    {
        if (!isPaused || pauseMenuCanvas == null) return;
        if (options == null) options = new SharedOptionsPanel(pauseMenuCanvas.transform, () => {});
        options.Open();
    }

    private void OnDestroy()
    {
        if (optionsButton != null) optionsButton.onClick.RemoveListener(OpenOptions);
    }

    private void EnableLocalPlayerControls(bool state)
    {
        // --- THE FIX: We are now searching for your new SimpleMultiplayerPlayer script! ---
        MultiplayerPlayerController[] players = FindObjectsOfType<MultiplayerPlayerController>();

        foreach (MultiplayerPlayerController player in players)
        {
            // Only disable the WASD controls if it is OUR character!
            if (player.photonView.IsMine)
            {
                player.enabled = state;
            }
        }
    }
}
