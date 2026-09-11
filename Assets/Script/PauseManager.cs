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
    private GameObject optionsPanel;
    private Slider sensitivitySlider;
    private TMP_Text sensitivityValue;
    private TMP_Text fullscreenValue;
    private bool fullscreenEnabled;
    private bool OptionsOpen => optionsPanel != null && optionsPanel.activeSelf;

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

    // ... (Keep your existing Resume and Pause methods exactly the same) ...

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
        if (optionsPanel == null) BuildOptions();
        sensitivitySlider.SetValueWithoutNotify(Mathf.Log10(GameOptions.MouseSensitivityMultiplier));
        sensitivityValue.text = GameOptions.MouseSensitivityMultiplier.ToString("0.00") + "x";
        fullscreenEnabled = PlayerPrefs.GetInt(GameOptions.FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        RefreshFullscreenLabel();
        optionsPanel.SetActive(true);
        optionsPanel.transform.SetAsLastSibling();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(sensitivitySlider.gameObject);
    }

    public void CloseOptions()
    {
        if (!OptionsOpen) return;
        optionsPanel.SetActive(false);
        PlayerPrefs.Save();
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    }

    private void BuildOptions()
    {
        // Cover the old artwork's buttons without replacing or editing that artwork.
        RectTransform overlay = OptionsRect("Options Overlay", pauseMenuCanvas.transform, Vector2.zero, Vector2.one);
        optionsPanel = overlay.gameObject;
        overlay.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);
        RectTransform panel = OptionsRect("Settings", overlay, new Vector2(0.27f, 0.22f), new Vector2(0.73f, 0.78f));
        panel.gameObject.AddComponent<Image>().color = new Color(0.075f, 0.085f, 0.09f);
        var border = panel.gameObject.AddComponent<Outline>();
        border.effectColor = new Color(0.2f, 0.3f, 0.32f);
        border.effectDistance = new Vector2(4f, -4f);
        OptionsText(panel, "OPTIONS", new Vector2(0.08f, 0.81f), new Vector2(0.92f, 0.95f), 40);
        OptionsText(panel, "MOUSE SENSITIVITY", new Vector2(0.08f, 0.65f), new Vector2(0.7f, 0.75f), 25);
        sensitivityValue = OptionsText(panel, "1.00x", new Vector2(0.73f, 0.65f), new Vector2(0.93f, 0.75f), 25);
        RectTransform sliderRect = OptionsRect("Sensitivity", panel, new Vector2(0.1f, 0.54f), new Vector2(0.9f, 0.62f));
        Image sliderHitArea = sliderRect.gameObject.AddComponent<Image>();
        sliderHitArea.color = Color.clear;
        var track = OptionsRect("Track", sliderRect, new Vector2(0f, 0.4f), new Vector2(1f, 0.6f));
        track.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.38f, 0.4f);
        var handleArea = OptionsRect("Handle Area", sliderRect, Vector2.zero, Vector2.one);
        handleArea.offsetMin = new Vector2(13f, 0f);
        handleArea.offsetMax = new Vector2(-13f, 0f);
        var handle = OptionsRect("Handle", handleArea, Vector2.zero, Vector2.one);
        handle.sizeDelta = new Vector2(26f, 0f);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = Color.white;
        sensitivitySlider = sliderRect.gameObject.AddComponent<Slider>();
        // Logarithmic travel gives low-DPI and high-DPI mice usable fine adjustment.
        sensitivitySlider.minValue = Mathf.Log10(GameOptions.MinimumMouseSensitivity);
        sensitivitySlider.maxValue = Mathf.Log10(GameOptions.MaximumMouseSensitivity);
        sensitivitySlider.handleRect = handle;
        sensitivitySlider.targetGraphic = handleImage;
        sensitivitySlider.onValueChanged.AddListener(value =>
        {
            float multiplier = Mathf.Pow(10f, value);
            PlayerPrefs.SetFloat(GameOptions.SensitivityKey, multiplier);
            sensitivityValue.text = multiplier.ToString("0.00") + "x";
        });
        OptionsText(panel, "FULLSCREEN", new Vector2(0.08f, 0.33f), new Vector2(0.6f, 0.44f), 25);
        Button fullscreenButton = OptionsButton(panel, "Fullscreen", new Vector2(0.68f, 0.33f), new Vector2(0.92f, 0.44f), out fullscreenValue);
        fullscreenButton.onClick.AddListener(() =>
        {
            fullscreenEnabled = !fullscreenEnabled;
            PlayerPrefs.SetInt(GameOptions.FullscreenKey, fullscreenEnabled ? 1 : 0);
            GameOptions.ApplyFullscreen(fullscreenEnabled);
            RefreshFullscreenLabel();
        });
        string hint = Application.isEditor ? "Fullscreen applies in the built game." : "Changes are saved when you go back.";
        OptionsText(panel, hint, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.3f), 19);
        TMP_Text backLabel;
        Button back = OptionsButton(panel, "BACK", new Vector2(0.08f, 0.06f), new Vector2(0.45f, 0.18f), out backLabel);
        back.onClick.AddListener(CloseOptions);
        TMP_Text resetLabel;
        Button reset = OptionsButton(panel, "RESET MOUSE", new Vector2(0.53f, 0.06f), new Vector2(0.92f, 0.18f), out resetLabel);
        reset.onClick.AddListener(() => sensitivitySlider.value = 0f); // log10(1x)
    }

    private void RefreshFullscreenLabel() { fullscreenValue.text = fullscreenEnabled ? "ON" : "OFF"; }

    private static RectTransform OptionsRect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static TMP_Text OptionsText(Transform parent, string text, Vector2 min, Vector2 max, float size)
    {
        var label = OptionsRect(text, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableAutoSizing = true;
        label.fontSizeMin = size * 0.7f;
        label.fontSizeMax = size;
        label.raycastTarget = false;
        return label;
    }

    private static Button OptionsButton(Transform parent, string title, Vector2 min, Vector2 max, out TMP_Text label)
    {
        var rect = OptionsRect(title, parent, min, max);
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = new Color(0.045f, 0.2f, 0.23f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        label = OptionsText(rect, title, new Vector2(0.05f, 0f), new Vector2(0.95f, 1f), 24);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private void OnDestroy()
    {
        if (optionsButton != null) optionsButton.onClick.RemoveListener(OpenOptions);
        if (optionsPanel != null) Destroy(optionsPanel);
    }

}

public static class GameOptions
{
    public const string SensitivityKey = "Options.MouseSensitivityMultiplier";
    public const string FullscreenKey = "Options.Fullscreen";
    public const float MinimumMouseSensitivity = 0.01f;
    public const float MaximumMouseSensitivity = 3f;
    public static float MouseSensitivityMultiplier => Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), MinimumMouseSensitivity, MaximumMouseSensitivity);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void LoadDisplaySettings()
    {
        if (PlayerPrefs.HasKey(FullscreenKey)) ApplyFullscreen(PlayerPrefs.GetInt(FullscreenKey) == 1);
    }

    public static void ApplyFullscreen(bool enabled)
    {
        if (Application.isEditor) return;
        Screen.fullScreenMode = enabled ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }
}
