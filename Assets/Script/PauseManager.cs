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

    public static void EnsureEditorPause()
    {
        var manager = FindObjectOfType<PauseManager>();
        if (manager == null) manager = new GameObject("Editor Pause Manager").AddComponent<PauseManager>();
        if (manager.pauseMenuCanvas != null) return;
        var root = new GameObject("Editor Pause", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
        root.GetComponent<Image>().color = new Color(0,0,0,0.82f);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080);
        manager.pauseMenuCanvas = root;
        EditorPauseButton(root.transform, "Resume", 90, manager.Resume);
        EditorPauseButton(root.transform, "Options", 0, manager.OpenOptions);
        EditorPauseButton(root.transform, "Main Menu", -90, manager.ExitToMain);
        root.SetActive(false);
    }

    private static void EditorPauseButton(Transform parent, string title, float y, UnityEngine.Events.UnityAction action)
    {
        var obj = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(360,68); rect.anchoredPosition = new Vector2(0,y);
        obj.GetComponent<Image>().color = new Color(0.12f,0.29f,0.45f);
        obj.GetComponent<Button>().onClick.AddListener(action);
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(obj.transform,false);
        var text = label.GetComponent<TextMeshProUGUI>(); text.text = title; text.fontSize = 30; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
    }

    // --- FIX: Reset the static variable when the scene loads ---
    void Start()
    {
        isPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        inputManager = FindObjectOfType<Player.Manager.InputManager>();
        if (pauseMenuCanvas != null)
        {
            foreach (Button button in pauseMenuCanvas.GetComponentsInChildren<Button>(true))
            {
                if (button.name != "Option" && button.name != "Options") continue;
                optionsButton = button;
                bool optionsWired = false;
                for (int i = 0; i < optionsButton.onClick.GetPersistentEventCount(); i++)
                    if (optionsButton.onClick.GetPersistentTarget(i) == this && optionsButton.onClick.GetPersistentMethodName(i) == nameof(OpenOptions)) optionsWired = true;
                if (!optionsWired) optionsButton.onClick.AddListener(OpenOptions);
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
        LoadingScreenController.LoadScene(0);
        AudioListener.pause = false;
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
        AudioListener.pause = false;
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

        PutOverlayOnTop(pauseMenuCanvas, 30000);
        UITransition.Show(pauseMenuCanvas);
        Time.timeScale = 0f; 
        isPaused = true;
        AudioListener.pause = true;


        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // PlayerController checks isPaused without changing its enabled state.
    }

    public static void PutOverlayOnTop(GameObject root, int order)
    {
        if (root == null) return;
        var canvas = root.GetComponent<Canvas>() ?? root.AddComponent<Canvas>();
        if (canvas.isRootCanvas) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        // Use the highest sorting layer as well as a reserved UI order.
        var layers = SortingLayer.layers;
        int highest = int.MinValue;
        foreach (var layer in layers)
            if (layer.value > highest) { highest = layer.value; canvas.sortingLayerID = layer.id; }
        canvas.sortingOrder = order;
        if (root.GetComponent<GraphicRaycaster>() == null) root.AddComponent<GraphicRaycaster>();
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
        if (isPaused) { isPaused = false; Time.timeScale = 1f; AudioListener.pause = false; }
        if (optionsButton != null) optionsButton.onClick.RemoveListener(OpenOptions);
    }

}

