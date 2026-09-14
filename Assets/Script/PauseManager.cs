using PlayerPrefs = GameSavePrefs;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class PauseManager : MonoBehaviour
{
    public static bool isPaused = false;

    [Header("UI References")]
    public GameObject pauseMenuCanvas; // The main Panel holding your design

    private Player.Manager.InputManager inputManager;
    private int lastTransitionFrame = -1;
    private CursorLockMode previousCursorLockState = CursorLockMode.Locked;
    private bool previousCursorVisible = false;
    private Coroutine resumeCursorRoutine;
    private Button optionsButton;
    private SharedOptionsPanel sharedOptions;
    private bool OptionsOpen => sharedOptions != null && sharedOptions.IsOpen;

    // --- FIX: Reset the static variable when the scene loads ---
    void Start()
    {
        isPaused = false;
        Time.timeScale = 1f;
        inputManager = FindObjectOfType<Player.Manager.InputManager>();
        if (pauseMenuCanvas != null)
        {
            foreach (Button button in pauseMenuCanvas.GetComponentsInChildren<Button>(true))
            {
                if (button.name != "Option" && button.name != "Options") continue;
                optionsButton = button;
                optionsButton.onClick.AddListener(OpenOptions);
                break;
            }
        }
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
            if (isPaused && OptionsOpen) { CloseOptions(); return; }
            if (AlmanacManager.Instance != null && AlmanacManager.Instance.IsOpen())
            {
                AlmanacManager.Instance.ToggleAlmanac();
                return;
            }

            if (isPaused) Resume();
            else Pause();
        }
    }


    public void ExitToMain()
    {
        CloseOptions();
        Time.timeScale = 1f;
        isPaused = false; // --- FIX: Reset the static variable before leaving ---
        SceneManager.LoadScene(0);
    }
    public void Resume()
    {
        if (!isPaused || lastTransitionFrame == Time.frameCount) return;
        lastTransitionFrame = Time.frameCount;
        CloseOptions();
        if (pauseMenuCanvas != null) pauseMenuCanvas.SetActive(false);
        Time.timeScale = 1f; // Resumes game physics/animations
        isPaused = false;

        Cursor.lockState = previousCursorLockState;
        Cursor.visible = previousCursorVisible;

        // Tutorial movement permissions remain owned by the tutorial.
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        if (inputManager != null) inputManager.CancelPendingFocusRestore();
        // A UI click/focus event can unlock the cursor again later in this frame.
        resumeCursorRoutine = StartCoroutine(RestoreCursorAfterResume());
    }

    private System.Collections.IEnumerator RestoreCursorAfterResume()
    {
        yield return null;
        resumeCursorRoutine = null;
        if (isPaused || !Application.isFocused) yield break;
        if (AlmanacManager.Instance != null && AlmanacManager.Instance.IsOpen()) yield break;
        if (ContractUIManager.Instance != null && ContractUIManager.Instance.IsContractUIOpen()) yield break;
        if (TutorialUIManager.Instance != null && TutorialUIManager.Instance.IsBossDialogueOpen()) yield break;
        Cursor.lockState = previousCursorLockState;
        Cursor.visible = previousCursorVisible;
    }

    void Pause()
    {
        if (isPaused || lastTransitionFrame == Time.frameCount) return;
        lastTransitionFrame = Time.frameCount;
        if (resumeCursorRoutine != null) { StopCoroutine(resumeCursorRoutine); resumeCursorRoutine = null; }
        previousCursorLockState = Cursor.lockState;
        previousCursorVisible = previousCursorLockState == CursorLockMode.Locked ? false : Cursor.visible;
        // No movement snapshot: a lesson may change permissions while paused.

        if (pauseMenuCanvas != null) pauseMenuCanvas.SetActive(true);
        Time.timeScale = 0f; 
        isPaused = true;


        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // PlayerController checks isPaused without changing its enabled state.
    }

    public void OpenOptions()
    {
        if (!isPaused || pauseMenuCanvas == null) return;
        if(sharedOptions==null)sharedOptions=new SharedOptionsPanel(pauseMenuCanvas.transform,()=>{});
        sharedOptions.Open();
    }

    public void CloseOptions()
    {
        if (!OptionsOpen) return;
        sharedOptions.Close(false);
    }

    private void OnDestroy()
    {
        if (optionsButton != null) optionsButton.onClick.RemoveListener(OpenOptions);
    }

}

