using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[System.Serializable]
public class TaskUIRow
{
    public GameObject rowContainer; // The main empty object holding the task
    public Image taskIcon;          // The Diamond Icon
    public TextMeshProUGUI taskText;// The actual text
    public Image underline;         // The gold line below the text
}

public class TutorialUIManager : MonoBehaviour
{
    public static TutorialUIManager Instance;

    [Header("UI: Boss Dialogue")]
    public GameObject bossHUDCanvas;
    public TextMeshProUGUI bossText;
    public Image bossPortraitDisplay;
    public GameObject okButton;
    public GameObject skipButton;

    [Header("Boss Dialogue Pacing")]
    [Tooltip("How quickly dialogue characters appear. Lower values give new players more time to read.")]
    [SerializeField] private float dialogueCharactersPerSecond = 72f;
    [Tooltip("Extra pause after the last character appears before Space can continue.")]
    [SerializeField] private float dialogueReadingPause = 0.45f;
    [Tooltip("Prevents very long messages from locking the player for too long.")]
    [SerializeField] private float maximumDialogueRevealTime = 3.25f;
    [Tooltip("Replaces the harsh yellow emphasis used by older dialogue with the contract-style brown accent.")]
    [SerializeField] private string bossEmphasisColor = "#7A3E12";

    [Header("Boss 2D Poses")]
    public Sprite poseBoss;
    public Sprite poseChill;
    public Sprite poseEndWave;
    public Sprite poseHappy;
    public Sprite poseOpenHand;
    public Sprite posePointUp;
    public Sprite posePoint;
    public Sprite poseSmile;

    [Header("UI: Task Checklist")]
    public GameObject taskPanel;
    public GameObject taskOpenView;
    public GameObject taskClosedView;
    public GameObject newTaskNotification;

    [Header("--- NEW: Genshin Style Task Rows ---")]
    public TaskUIRow[] taskRows;
    public Sprite defaultDiamondIcon;
    public Sprite completedCheckIcon; // Optional: A checkmark for when it's done!
    public Color activeTextColor = Color.white;
    public Color completedTextColor = new Color(0.6f, 0.6f, 0.6f, 1f); // Greyed out

    [Header("Tutorial Guidance Systems")]
    public TutorialGlowTarget directorTerminalGlow;
    public TutorialGlowTarget shopTerminalGlow;
    public TutorialGlowTarget computerGlow;
    public TutorialGlowTarget stageGlow;
    public TutorialGlowTarget pointAGlow;
    public TutorialGlowTarget pointBGlow;
    public TutorialGlowTarget pointCGlow;

    private bool isTaskUIExpanded = false;
    private Coroutine notificationCoroutine;
    private Coroutine taskRevealCoroutine;
    private Coroutine bossRevealCoroutine;
    private Player.Manager.InputManager inputManager;
    private bool[] completedTaskRows;
    private CanvasGroup bossCanvasGroup;
    private Vector2 bossTextBasePosition;
    private Vector3 bossPortraitBaseScale = Vector3.one;
    private float bossDialogueReadyAt;
    private float currentDialogueRevealDuration;
    private int currentDialogueVisibleCharacters;
    private readonly Dictionary<GameObject, Vector2> taskRowBasePositions = new Dictionary<GameObject, Vector2>();
    private readonly Dictionary<GameObject, Vector3> taskRowBaseScales = new Dictionary<GameObject, Vector3>();

    private void Awake()
    {
        Instance = this;
        RecoverTaskPanel();

        if (bossHUDCanvas != null)
        {
            bossCanvasGroup = bossHUDCanvas.GetComponent<CanvasGroup>();
            if (bossCanvasGroup == null) bossCanvasGroup = bossHUDCanvas.AddComponent<CanvasGroup>();
        }

        if (bossText != null) bossTextBasePosition = bossText.rectTransform.anchoredPosition;
        if (bossPortraitDisplay != null) bossPortraitBaseScale = bossPortraitDisplay.rectTransform.localScale;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (PauseManager.isPaused) return;
        if (AlmanacManager.Instance != null && AlmanacManager.Instance.IsOpen()) return;

        if (inputManager == null) inputManager = FindObjectOfType<Player.Manager.InputManager>();

        Keyboard keyboard = Keyboard.current;
        bool contextPanelPressed = (inputManager != null && inputManager.ContextPanel) ||
                                   (keyboard != null && keyboard.tabKey.wasPressedThisFrame);

        if (contextPanelPressed && taskPanel != null && taskPanel.activeSelf)
        {
            if (ContractUIManager.Instance != null && ContractUIManager.Instance.CanToggleQualifications()) return;

            isTaskUIExpanded = !isTaskUIExpanded;
            if (taskOpenView != null) taskOpenView.SetActive(isTaskUIExpanded);
            if (taskClosedView != null) taskClosedView.SetActive(!isTaskUIExpanded);
            if (isTaskUIExpanded && newTaskNotification != null) newTaskNotification.SetActive(false);

            GameObject revealedView = isTaskUIExpanded ? taskOpenView : taskClosedView;
            if (revealedView != null) StartCoroutine(AnimateQuickReveal(revealedView));
        }
    }

    public void ShowBossDialogue(string message, Sprite pose, bool showOk, bool showSkip)
    {
        if (DevTutorialBypass.Disabled) { HideBossDialogue(); return; }
        RecoverTaskPanel();

        if (!string.IsNullOrEmpty(message) && !string.IsNullOrEmpty(bossEmphasisColor))
        {
            message = message.Replace("<color=yellow>", "<color=" + bossEmphasisColor + ">");
        }

        if (bossRevealCoroutine != null)
        {
            StopCoroutine(bossRevealCoroutine);
            bossRevealCoroutine = null;
        }

        if (bossHUDCanvas != null) bossHUDCanvas.SetActive(true);
        if (taskPanel != null) taskPanel.SetActive(false);
        if (bossText != null)
        {
            bossText.text = message;
            bossText.maxVisibleCharacters = 0;
        }
        currentDialogueVisibleCharacters = CountVisibleCharacters(message);
        currentDialogueRevealDuration = Mathf.Clamp(
            currentDialogueVisibleCharacters / Mathf.Max(1f, dialogueCharactersPerSecond),
            0.65f,
            Mathf.Max(0.65f, maximumDialogueRevealTime));
        bossDialogueReadyAt = Time.unscaledTime + currentDialogueRevealDuration + Mathf.Max(0f, dialogueReadingPause);
        if (bossPortraitDisplay != null) bossPortraitDisplay.sprite = pose;
        if (okButton != null) okButton.SetActive(showOk);
        if (skipButton != null) skipButton.SetActive(showSkip);

        if (DevTutorialBypass.FastBossDialogue) CompleteBossRevealForTesting();
        else bossRevealCoroutine = StartCoroutine(AnimateBossDialogueIn());
    }

    public void CompleteBossRevealForTesting()
    {
        if (!DevTutorialBypass.FastBossDialogue) return;
        if (bossRevealCoroutine != null) StopCoroutine(bossRevealCoroutine);
        bossRevealCoroutine = null;
        ResetBossAnimationState();
        bossDialogueReadyAt = 0f;
    }

    public void HideBossDialogue()
    {
        if (bossRevealCoroutine != null)
        {
            StopCoroutine(bossRevealCoroutine);
            bossRevealCoroutine = null;
        }

        ResetBossAnimationState();
        bossDialogueReadyAt = 0f;
        if (bossHUDCanvas != null) bossHUDCanvas.SetActive(false);
    }

    public bool IsBossDialogueOpen()
    {
        return bossHUDCanvas != null && bossHUDCanvas.activeSelf;
    }

    public bool CanAdvanceBossDialogue()
    {
        return !IsBossDialogueOpen() || Time.unscaledTime >= bossDialogueReadyAt;
    }

    public float GetBossDialogueReadyDelay()
    {
        if (!IsBossDialogueOpen()) return 0f;
        return Mathf.Max(0f, bossDialogueReadyAt - Time.unscaledTime);
    }

    public void SetupTasks(string[] tasks)
    {
        if (DevTutorialBypass.Disabled) { HideTasks(); return; }
        if (tasks == null || tasks.Length == 0)
        {
            HideTasks();
            return;
        }

        RecoverTaskPanel();

        if (taskRevealCoroutine != null)
        {
            StopCoroutine(taskRevealCoroutine);
            taskRevealCoroutine = null;
        }

        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
            notificationCoroutine = null;
        }

        HideTaskRows();

        int taskRowCount = taskRows != null ? taskRows.Length : 0;
        completedTaskRows = new bool[taskRowCount];

        if (taskPanel != null) taskPanel.SetActive(true);
        if (taskRowCount > 0) taskRevealCoroutine = StartCoroutine(RevealTasksSequentially(tasks));

        if (newTaskNotification != null) newTaskNotification.SetActive(false);

        if (newTaskNotification != null) notificationCoroutine = StartCoroutine(ShowNewTaskNotification());
    }

    private IEnumerator RevealTasksSequentially(string[] tasks)
    {
        if (taskRows == null) yield break;

        for (int i = 0; i < tasks.Length; i++)
        {
            if (i < taskRows.Length && taskRows[i] != null && taskRows[i].rowContainer != null)
            {
                // Clean up the text (remove the dash if it exists so it looks cleaner next to the icon)
                string cleanText = tasks[i].StartsWith("- ") ? tasks[i].Substring(2) : tasks[i];

                if (taskRows[i].taskText != null)
                {
                    taskRows[i].taskText.text = cleanText;
                    taskRows[i].taskText.enableWordWrapping = false;
                    taskRows[i].taskText.overflowMode = TextOverflowModes.Ellipsis;
                    taskRows[i].taskText.enableAutoSizing = true;
                    taskRows[i].taskText.fontSizeMin = 14f;
                }
                ApplyTaskState(i);

                taskRows[i].rowContainer.SetActive(true);
                yield return AnimateTaskRowIn(taskRows[i]);
                yield return new WaitForSecondsRealtime(0.08f);
            }
        }

        taskRevealCoroutine = null;
    }

    public void ShowActiveContract(string contractName)
    {
        SetupTasks(new string[]
        {
            "- " + contractName + " CONTRACT ACTIVE",
            "- Press <color=red>[TAB]</color> to view the selected contract"
        });
    }

    public void HideTasks()
    {
        if (taskRevealCoroutine != null)
        {
            StopCoroutine(taskRevealCoroutine);
            taskRevealCoroutine = null;
        }

        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
            notificationCoroutine = null;
        }

        HideTaskRows();

        RecoverTaskPanel();
        if (taskPanel != null) taskPanel.SetActive(false);
        if (newTaskNotification != null) newTaskNotification.SetActive(false);
    }

    public void ShowTasks()
    {
        RecoverTaskPanel();
        if (taskPanel != null) taskPanel.SetActive(true);
    }

    private void HideTaskRows()
    {
        if (taskRows == null) return;

        foreach (TaskUIRow row in taskRows)
        {
            if (row != null && row.rowContainer != null) row.rowContainer.SetActive(false);
        }
    }

    private void RecoverTaskPanel()
    {
        if (taskPanel != null || taskRows == null) return;

        Transform commonParent = null;

        foreach (TaskUIRow row in taskRows)
        {
            if (row == null || row.rowContainer == null) continue;

            Transform rowTransform = row.rowContainer.transform;
            if (commonParent == null)
            {
                commonParent = rowTransform.parent != null ? rowTransform.parent : rowTransform;
                continue;
            }

            while (commonParent != null && rowTransform != commonParent && !rowTransform.IsChildOf(commonParent))
            {
                commonParent = commonParent.parent;
            }
        }

        if (commonParent != null) taskPanel = commonParent.gameObject;
    }

    public void MarkTaskComplete(int index)
    {
        if (taskRows == null || index < 0 || index >= taskRows.Length || taskRows[index] == null) return;

        if (completedTaskRows == null || completedTaskRows.Length != taskRows.Length)
        {
            completedTaskRows = new bool[taskRows.Length];
        }

        completedTaskRows[index] = true;
        if (taskRows[index].rowContainer != null && taskRows[index].rowContainer.activeSelf)
        {
            ApplyTaskState(index);
            StartCoroutine(AnimateTaskCompleted(taskRows[index]));
        }
    }

    private void ApplyTaskState(int index)
    {
        if (taskRows == null || index < 0 || index >= taskRows.Length || taskRows[index] == null) return;

        TaskUIRow row = taskRows[index];
        bool isComplete = completedTaskRows != null && index < completedTaskRows.Length && completedTaskRows[index];

        if (isComplete)
        {
            if (row.taskText != null && !row.taskText.text.StartsWith("<s>")) row.taskText.text = "<s>" + row.taskText.text + "</s>";
            if (row.taskText != null) row.taskText.color = completedTextColor;
            if (row.taskIcon != null && completedCheckIcon != null) row.taskIcon.sprite = completedCheckIcon;
            if (row.underline != null) row.underline.gameObject.SetActive(false);
            return;
        }

        if (row.taskText != null) row.taskText.color = activeTextColor;
        if (row.taskIcon != null && defaultDiamondIcon != null) row.taskIcon.sprite = defaultDiamondIcon;
        if (row.underline != null) row.underline.gameObject.SetActive(true);
    }

    private IEnumerator ShowNewTaskNotification()
    {
        if (newTaskNotification != null)
        {
            CanvasGroup notificationGroup = newTaskNotification.GetComponent<CanvasGroup>();
            if (notificationGroup == null) notificationGroup = newTaskNotification.AddComponent<CanvasGroup>();

            newTaskNotification.SetActive(true);
            notificationGroup.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < 0.2f)
            {
                elapsed += Time.unscaledDeltaTime;
                notificationGroup.alpha = EaseOutCubic(Mathf.Clamp01(elapsed / 0.2f));
                yield return null;
            }

            notificationGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(3.4f);

            elapsed = 0f;
            while (elapsed < 0.25f)
            {
                elapsed += Time.unscaledDeltaTime;
                notificationGroup.alpha = 1f - Mathf.Clamp01(elapsed / 0.25f);
                yield return null;
            }

            notificationGroup.alpha = 1f;
            if (newTaskNotification != null) newTaskNotification.SetActive(false);
        }

        notificationCoroutine = null;
    }

    private IEnumerator AnimateBossDialogueIn()
    {
        if (bossCanvasGroup == null && bossHUDCanvas != null)
        {
            bossCanvasGroup = bossHUDCanvas.GetComponent<CanvasGroup>();
            if (bossCanvasGroup == null) bossCanvasGroup = bossHUDCanvas.AddComponent<CanvasGroup>();
        }

        RectTransform textRect = bossText != null ? bossText.rectTransform : null;
        RectTransform portraitRect = bossPortraitDisplay != null ? bossPortraitDisplay.rectTransform : null;

        if (bossCanvasGroup != null) bossCanvasGroup.alpha = 0f;
        if (textRect != null) textRect.anchoredPosition = bossTextBasePosition + new Vector2(0f, -16f);
        if (portraitRect != null) portraitRect.localScale = bossPortraitBaseScale * 0.93f;

        float elapsed = 0f;
        const float panelAnimationDuration = 0.28f;
        float revealDuration = Mathf.Max(panelAnimationDuration, currentDialogueRevealDuration);
        while (elapsed < revealDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float panelT = EaseOutCubic(Mathf.Clamp01(elapsed / panelAnimationDuration));
            float textT = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, currentDialogueRevealDuration));

            if (bossCanvasGroup != null) bossCanvasGroup.alpha = panelT;
            if (textRect != null) textRect.anchoredPosition = Vector2.LerpUnclamped(bossTextBasePosition + new Vector2(0f, -16f), bossTextBasePosition, panelT);
            if (bossText != null) bossText.maxVisibleCharacters = Mathf.CeilToInt(currentDialogueVisibleCharacters * textT);
            if (portraitRect != null)
            {
                float portraitT = EaseOutBack(Mathf.Clamp01(elapsed / panelAnimationDuration));
                portraitRect.localScale = Vector3.LerpUnclamped(bossPortraitBaseScale * 0.93f, bossPortraitBaseScale, portraitT);
            }

            yield return null;
        }

        ResetBossAnimationState();
        bossRevealCoroutine = null;
    }

    private void ResetBossAnimationState()
    {
        if (bossCanvasGroup != null) bossCanvasGroup.alpha = 1f;
        if (bossText != null)
        {
            bossText.rectTransform.anchoredPosition = bossTextBasePosition;
            bossText.maxVisibleCharacters = int.MaxValue;
        }
        if (bossPortraitDisplay != null) bossPortraitDisplay.rectTransform.localScale = bossPortraitBaseScale;
    }

    private static int CountVisibleCharacters(string message)
    {
        if (string.IsNullOrEmpty(message)) return 0;

        int count = 0;
        bool insideTag = false;
        for (int i = 0; i < message.Length; i++)
        {
            char character = message[i];
            if (character == '<')
            {
                insideTag = true;
                continue;
            }
            if (character == '>' && insideTag)
            {
                insideTag = false;
                continue;
            }
            if (!insideTag) count++;
        }

        return count;
    }

    private IEnumerator AnimateTaskRowIn(TaskUIRow row)
    {
        if (row == null || row.rowContainer == null) yield break;

        RectTransform rect = row.rowContainer.GetComponent<RectTransform>();
        CanvasGroup group = row.rowContainer.GetComponent<CanvasGroup>();
        if (group == null) group = row.rowContainer.AddComponent<CanvasGroup>();

        CacheTaskRowTransform(row.rowContainer, rect);
        Vector2 basePosition = rect != null ? taskRowBasePositions[row.rowContainer] : Vector2.zero;
        Vector3 baseScale = taskRowBaseScales[row.rowContainer];

        group.alpha = 0f;
        if (rect != null) rect.anchoredPosition = basePosition + new Vector2(-22f, 0f);
        row.rowContainer.transform.localScale = baseScale * 0.98f;

        float elapsed = 0f;
        const float duration = 0.22f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOutCubic(Mathf.Clamp01(elapsed / duration));
            group.alpha = t;
            if (rect != null) rect.anchoredPosition = Vector2.LerpUnclamped(basePosition + new Vector2(-22f, 0f), basePosition, t);
            row.rowContainer.transform.localScale = Vector3.LerpUnclamped(baseScale * 0.98f, baseScale, t);
            yield return null;
        }

        group.alpha = 1f;
        if (rect != null) rect.anchoredPosition = basePosition;
        row.rowContainer.transform.localScale = baseScale;
    }

    private IEnumerator AnimateTaskCompleted(TaskUIRow row)
    {
        if (row == null || row.rowContainer == null) yield break;

        CacheTaskRowTransform(row.rowContainer, row.rowContainer.GetComponent<RectTransform>());
        Vector3 baseScale = taskRowBaseScales[row.rowContainer];
        float elapsed = 0f;
        const float duration = 0.3f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float pulse = normalized < 0.45f
                ? Mathf.Lerp(1f, 1.075f, EaseOutCubic(normalized / 0.45f))
                : Mathf.Lerp(1.075f, 1f, EaseOutCubic((normalized - 0.45f) / 0.55f));
            row.rowContainer.transform.localScale = baseScale * pulse;
            yield return null;
        }

        row.rowContainer.transform.localScale = baseScale;
    }

    private IEnumerator AnimateQuickReveal(GameObject target)
    {
        if (target == null) yield break;

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null) group = target.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        float elapsed = 0f;
        const float duration = 0.18f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = EaseOutCubic(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        group.alpha = 1f;
    }

    private void CacheTaskRowTransform(GameObject rowObject, RectTransform rect)
    {
        if (!taskRowBasePositions.ContainsKey(rowObject) && rect != null) taskRowBasePositions[rowObject] = rect.anchoredPosition;
        if (!taskRowBaseScales.ContainsKey(rowObject)) taskRowBaseScales[rowObject] = rowObject.transform.localScale;
    }

    private float EaseOutCubic(float t)
    {
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float shifted = t - 1f;
        return 1f + c3 * shifted * shifted * shifted + c1 * shifted * shifted;
    }

    public void SetDynamicGlow(string keyword, bool state)
    {
        TutorialGlowTarget[] glows = FindObjectsOfType<TutorialGlowTarget>(true);

        if (state) ClearDynamicGlows(glows);

        TutorialGlowTarget assignedGlow = GetAssignedGlow(keyword);
        if (assignedGlow != null)
        {
            if (state && assignedGlow.gameObject.activeInHierarchy) assignedGlow.StartGlowing();
            else assignedGlow.StopGlowing();
            return;
        }

        foreach (TutorialGlowTarget glow in glows)
        {
            if (glow == null || !glow.gameObject.name.ToLower().Contains(keyword.ToLower())) continue;

            if (state && glow.gameObject.activeInHierarchy) glow.StartGlowing();
            else glow.StopGlowing();
        }
    }

    public void SetDynamicGlow(TutorialGlowTarget glowTarget, bool state)
    {
        if (glowTarget == null) return;

        if (state) ClearDynamicGlows();

        if (state && glowTarget.gameObject.activeInHierarchy) glowTarget.StartGlowing();
        else glowTarget.StopGlowing();
    }

    private TutorialGlowTarget GetAssignedGlow(string keyword)
    {
        switch (keyword.ToLower())
        {
            case "director": return directorTerminalGlow;
            case "shop": return shopTerminalGlow;
            case "computer": return computerGlow;
            case "stage": return stageGlow;
            case "pointa": return pointAGlow;
            case "pointb": return pointBGlow;
            case "pointc": return pointCGlow;
            default: return null;
        }
    }

    public void ClearDynamicGlows()
    {
        ClearDynamicGlows(FindObjectsOfType<TutorialGlowTarget>(true));
    }

    private void ClearDynamicGlows(TutorialGlowTarget[] glows)
    {
        foreach (TutorialGlowTarget glow in glows)
        {
            if (glow != null) glow.StopGlowing();
        }
    }
}

/// <summary>
/// Adds consistent, lightweight interaction feedback to scene and runtime-created
/// UI buttons without requiring every prefab to be edited by hand.
/// </summary>
public sealed class UIPolishBootstrap : MonoBehaviour
{
    private static UIPolishBootstrap instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;

        GameObject bootstrapObject = new GameObject("UI Polish System");
        instance = bootstrapObject.AddComponent<UIPolishBootstrap>();
        DontDestroyOnLoad(bootstrapObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(ScanForNewButtons());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PolishButtons();
    }

    private IEnumerator ScanForNewButtons()
    {
        while (true)
        {
            PolishButtons();
            yield return new WaitForSecondsRealtime(0.75f);
        }
    }

    private void PolishButtons()
    {
        Button[] buttons = FindObjectsOfType<Button>(true);
        foreach (Button button in buttons)
        {
            if (button == null || button.GetComponent<UIButtonPolish>() != null) continue;
            button.gameObject.AddComponent<UIButtonPolish>();
        }
    }
}

[DisallowMultipleComponent]
public sealed class UIButtonPolish : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, ISelectHandler, IDeselectHandler
{
    private const float hoverScale = 1.035f;
    private const float selectedScale = 1.02f;
    private const float pressedScale = 0.965f;

    private Button button;
    private Vector3 baseScale;
    private bool hovered;
    private bool pressed;
    private bool selected;
    private float clickPulse = 1f;

    private void Awake()
    {
        button = GetComponent<Button>();
        baseScale = transform.localScale;
    }

    private void Update()
    {
        bool canInteract = button != null && button.IsInteractable();
        float stateScale = 1f;

        if (canInteract)
        {
            if (pressed) stateScale = pressedScale;
            else if (hovered) stateScale = hoverScale;
            else if (selected) stateScale = selectedScale;
        }

        clickPulse = Mathf.MoveTowards(clickPulse, 1f, Time.unscaledDeltaTime * 0.65f);
        Vector3 targetScale = baseScale * stateScale * clickPulse;
        float blend = 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, blend);
    }

    private void OnDisable()
    {
        hovered = false;
        pressed = false;
        selected = false;
        clickPulse = 1f;
        transform.localScale = baseScale;
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData eventData) { if (IsPrimary(eventData)) pressed = true; }
    public void OnPointerUp(PointerEventData eventData) { if (IsPrimary(eventData)) pressed = false; }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsPrimary(eventData) || button == null || !button.IsInteractable()) return;
        clickPulse = 1.075f;
    }

    public void OnSelect(BaseEventData eventData) { selected = true; }
    public void OnDeselect(BaseEventData eventData) { selected = false; pressed = false; }

    private bool IsPrimary(PointerEventData eventData)
    {
        return eventData == null || eventData.button == PointerEventData.InputButton.Left;
    }
}
