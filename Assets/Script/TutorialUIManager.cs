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

    [Header("Editable Boss Dialogue - Studio and Editor")]
    [Tooltip("Edit wording only; task progression and poses remain unchanged. Blank text uses the original line.")]
    [SerializeField] private BossDialogueText[] bossDialogue = BossDialogueText.Defaults();

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

    [Header("Boss Dialogue Pages")]
    [Tooltip("Long lessons are split at readable sentence breaks so the Boss panel never becomes a wall of text.")]
    [SerializeField] private int dialoguePageMaxVisibleCharacters = 230;
    [Tooltip("Prevents a page from ending as a tiny fragment when a sentence break is nearby.")]
    [SerializeField] private int dialoguePageMinimumVisibleCharacters = 90;

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
    private int bossInputConsumedFrame = -1;

    public static bool BossContinuePressed => Application.isFocused && !PauseManager.isPaused &&
        ((Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
         (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame));

    private void ProcessBossRevealInput()
    {
        if (!IsBossDialogueOpen() || bossInputConsumedFrame == Time.frameCount) return;
        if (BossContinuePressed || (Application.isFocused && !PauseManager.isPaused && inputManager != null && inputManager.Continue))
            TryAdvanceBossDialoguePage();
    }
    private Player.Manager.InputManager inputManager;
    private bool[] completedTaskRows;
    private CanvasGroup bossCanvasGroup;
    private Vector2 bossTextBasePosition;
    private Vector3 bossPortraitBaseScale = Vector3.one;
    private float bossDialogueReadyAt;
    private float currentDialogueRevealDuration;
    private int currentDialogueVisibleCharacters;
    private string[] bossDialoguePages = new string[0];
    private int bossDialoguePageIndex;
    private Sprite bossDialoguePose;
    private bool bossDialogueShowOk;
    private bool bossDialogueShowSkip;

    private void Awake()
    {
        Instance = this;
        activeTextColor = CrewPaperStyle.Ink;
        completedTextColor = CrewPaperStyle.MutedInk;
        RecoverTaskPanel();
        if (taskPanel != null)
        {
            var taskCanvas = taskPanel.GetComponentInParent<Canvas>(true);
            if (taskCanvas != null) FeedbackPaperUI.ConfigureScale(taskCanvas.rootCanvas);
        }
        if (bossHUDCanvas != null)
        {
            var canvas = bossHUDCanvas.GetComponentInParent<Canvas>(true);
            if (canvas != null) FeedbackPaperUI.ConfigureScale(canvas.rootCanvas);
            bossCanvasGroup = bossHUDCanvas.GetComponent<CanvasGroup>();
            if (bossCanvasGroup == null) bossCanvasGroup = bossHUDCanvas.AddComponent<CanvasGroup>();
        }

        if (bossHUDCanvas != null)
            foreach (var label in bossHUDCanvas.GetComponentsInChildren<TMP_Text>(true))
                BossDialogueStyle.Apply(label, label != bossText);
        if (bossText != null)
        {
            bossText.rectTransform.anchoredPosition = new Vector2(0, bossText.rectTransform.anchoredPosition.y);
            bossTextBasePosition = bossText.rectTransform.anchoredPosition;
        }
        if (bossPortraitDisplay != null) bossPortraitBaseScale = bossPortraitDisplay.rectTransform.localScale;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (PauseManager.isPaused) return;

        if (inputManager == null) inputManager = FindObjectOfType<Player.Manager.InputManager>();
        Keyboard keyboard = Keyboard.current;
        ProcessBossRevealInput();

        if (AlmanacManager.Instance != null && AlmanacManager.Instance.IsOpen()) return;

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
            if (revealedView != null && EditorManager.Instance == null) StartCoroutine(AnimateQuickReveal(revealedView));
        }
    }

    public void ShowBossDialogue(string message, Sprite pose, bool showOk, bool showSkip)
    {
        message = BossDialogueText.Resolve(bossDialogue, message);
        if (DevTutorialBypass.Disabled) { HideBossDialogue(); return; }
        RecoverTaskPanel();

        if (!string.IsNullOrEmpty(message) && !string.IsNullOrEmpty(bossEmphasisColor))
        {
            message = message.Replace("<color=yellow>", "<color=" + bossEmphasisColor + ">");
        }

        bossDialoguePages = SplitDialogueIntoPages(BossDialogueStyle.HighlightControls(message));
        bossDialoguePageIndex = 0;
        bossDialoguePose = pose;
        bossDialogueShowOk = showOk;
        bossDialogueShowSkip = showSkip;
        ShowBossDialoguePage(bossDialoguePages[0]);
    }

    /// <summary>Consumes reveal/page input before the dialogue owner advances.</summary>
    public bool TryAdvanceBossDialoguePage()
    {
        if (PauseManager.isPaused || !Application.isFocused || bossInputConsumedFrame == Time.frameCount) return true;
        if (!IsBossDialogueOpen()) return false;
        if (bossRevealCoroutine != null || Time.unscaledTime < bossDialogueReadyAt)
        {
            if (bossRevealCoroutine != null) StopCoroutine(bossRevealCoroutine);
            bossRevealCoroutine = null;
            ResetBossAnimationState();
            GameplayAudioManager.StopVoice();
            bossDialogueReadyAt = 0f;
            bossInputConsumedFrame = Time.frameCount;
            return true;
        }
        if (!HasPendingBossDialoguePage()) return false;
        if (!CanAdvanceCurrentBossDialoguePage()) return true;
        AdvanceBossDialoguePage();
        return true;
    }

    private void ShowBossDialoguePage(string message)
    {
        bossInputConsumedFrame = Time.frameCount;
        GameplayAudioManager.Speak();
        if (bossRevealCoroutine != null)
        {
            StopCoroutine(bossRevealCoroutine);
            bossRevealCoroutine = null;
        }

        if (bossHUDCanvas != null)
        {
            bossHUDCanvas.SetActive(true);
            // Camera lessons can open dialogue while the viewfinder (order 150) is visible.
            var dialogueCanvas = bossHUDCanvas.GetComponent<Canvas>() ?? bossHUDCanvas.AddComponent<Canvas>();
            dialogueCanvas.overrideSorting = true;
            dialogueCanvas.sortingOrder = Mathf.Max(dialogueCanvas.sortingOrder, 160);
        }
        if (taskPanel != null) taskPanel.SetActive(false);
        if (bossText != null)
        {
            BossDialogueStyle.Apply(bossText);
            bossText.text = message;
            bossText.maxVisibleCharacters = 0;
        }
        currentDialogueVisibleCharacters = CountVisibleCharacters(message);
        currentDialogueRevealDuration = Mathf.Clamp(
            currentDialogueVisibleCharacters / Mathf.Max(1f, dialogueCharactersPerSecond),
            0.65f,
            Mathf.Max(0.65f, maximumDialogueRevealTime));
        bossDialogueReadyAt = Time.unscaledTime + currentDialogueRevealDuration + Mathf.Max(0f, dialogueReadingPause);
        if (bossPortraitDisplay != null) bossPortraitDisplay.sprite = bossDialoguePose;
        if (okButton != null) okButton.SetActive(bossDialogueShowOk);
        if (skipButton != null) skipButton.SetActive(bossDialogueShowSkip);

        if (DevTutorialBypass.FastBossDialogue) CompleteBossRevealForTesting();
        else bossRevealCoroutine = StartCoroutine(AnimateBossDialogueIn());
    }

    private bool HasPendingBossDialoguePage()
    {
        return bossDialoguePages != null && bossDialoguePageIndex < bossDialoguePages.Length - 1;
    }

    private bool CanAdvanceCurrentBossDialoguePage()
    {
        return !IsBossDialogueOpen() || Time.unscaledTime >= bossDialogueReadyAt;
    }

    private void AdvanceBossDialoguePage()
    {
        if (!HasPendingBossDialoguePage()) return;
        bossDialoguePageIndex++;
        ShowBossDialoguePage(bossDialoguePages[bossDialoguePageIndex]);
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
        GameplayAudioManager.StopVoice();
        if (bossRevealCoroutine != null)
        {
            StopCoroutine(bossRevealCoroutine);
            bossRevealCoroutine = null;
        }

        ResetBossAnimationState();
        bossDialogueReadyAt = 0f;
        bossDialoguePages = new string[0];
        bossDialoguePageIndex = 0;
        if (bossHUDCanvas != null) bossHUDCanvas.SetActive(false);
    }

    public bool IsBossDialogueOpen()
    {
        return bossHUDCanvas != null && bossHUDCanvas.activeSelf;
    }

    public bool CanAdvanceBossDialogue()
    {
        ProcessBossRevealInput();
        // Process input here too so owner Update order cannot skip a revealed page.
        return !PauseManager.isPaused && Application.isFocused && bossInputConsumedFrame != Time.frameCount &&
               !HasPendingBossDialoguePage() && CanAdvanceCurrentBossDialoguePage();
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

        if (newTaskNotification != null && EditorManager.Instance == null) notificationCoroutine = StartCoroutine(ShowNewTaskNotification());
    }

    private IEnumerator RevealTasksSequentially(string[] tasks)
    {
        if (taskRows == null) yield break;

        for (int i = 0; i < tasks.Length; i++)
        {
            if (i < taskRows.Length && taskRows[i] != null && taskRows[i].rowContainer != null)
            {
                // Clean up the text (remove the dash if it exists so it looks cleaner next to the icon)
                string cleanText = (tasks[i].StartsWith("- ") ? tasks[i].Substring(2) : tasks[i])
                    .Replace("<color=red>", "<color=#9A421E>");

                if (taskRows[i].taskText != null)
                {
                    taskRows[i].taskText.text = cleanText;
                    taskRows[i].taskText.enableWordWrapping = true;
                    taskRows[i].taskText.overflowMode = TextOverflowModes.Overflow;
                    taskRows[i].taskText.enableAutoSizing = false;
                    taskRows[i].taskText.fontSize = 20f;
                    taskRows[i].taskText.fontSizeMin = 14f;
                    if (EditorManager.Instance != null)
                    {
                        taskRows[i].taskText.fontSizeMax = 22f;
                        taskRows[i].taskText.raycastTarget = false;
                    }
                }
                ApplyTaskState(i);

                taskRows[i].rowContainer.SetActive(true);
                if (EditorManager.Instance == null)
                {
                    PrepareTaskLabel(taskRows[i].taskText);
                    FitStudioTaskRows();
                    yield return AnimateTaskRowIn(taskRows[i]);
                    yield return new WaitForSecondsRealtime(0.08f);
                }
                else
                {
                    var group = taskRows[i].rowContainer.GetComponent<CanvasGroup>();
                    if (group != null) { group.alpha = 1f; group.blocksRaycasts = false; }
                }
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

    private readonly Vector3[] editorTimelineCorners = new Vector3[4];
    private TMP_FontAsset editorChecklistFont;
    private readonly CrewPaperStyle.HintStack studioTaskStack = new CrewPaperStyle.HintStack();

    private void FitStudioTaskRows()
    {
        if (EditorManager.Instance != null || taskRows == null) return;
        bool dockForEditorApp = TutorialManager.Instance != null &&
            TutorialManager.Instance.currentStep == TutorialManager.TutorialStep.ClickEditorApp;
        bool started = false;
        foreach (var row in taskRows)
        {
            if (row == null || row.rowContainer == null || row.taskText == null) continue;
            var rect = row.rowContainer.transform as RectTransform;
            if (rect == null) continue;
            if (!started)
            {
                studioTaskStack.Begin(rect, dockForEditorApp);
                started = true;
            }
            studioTaskStack.Add(rect, row.taskText, row.taskIcon, row.underline);
            if (row.rowContainer.activeSelf)
            {
                if (taskViewfinderOpen) CameraHUDController.StyleTutorialHint(rect, row.taskText, row.taskIcon, row.underline);
                else
                {
                    row.taskText.text = row.taskText.text.Replace("#D5A26E", "#9A421E");
                    row.taskText.color = row.taskText.text.StartsWith("<s>") ? completedTextColor : activeTextColor;
                }
            }
        }
    }

    private void PrepareTaskLabel(TMP_Text label)
    {
        if (label == null) return;
        if (editorChecklistFont == null)
            editorChecklistFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (editorChecklistFont != null) label.font = editorChecklistFont;
        label.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        label.characterSpacing = 0f;
        label.wordSpacing = 0f;
        label.lineSpacing = 0f;
        label.paragraphSpacing = 0f;
        label.margin = Vector4.zero;
        label.enableAutoSizing = false;
        label.fontSize = 20f;
    }
    private readonly Dictionary<Canvas, (bool sorting, int order)> almanacTaskSorting =
        new Dictionary<Canvas, (bool sorting, int order)>();

    private bool taskViewfinderOpen;
    public void SetTaskViewfinderVisible(bool open)
    {
        taskViewfinderOpen = open;
        UpdateAlmanacTaskSorting();
    }

    private void UpdateAlmanacTaskSorting()
    {
        bool bookOpen = AlmanacManager.Instance != null && AlmanacManager.Instance.IsOpen();
        if (!bookOpen && !taskViewfinderOpen)
        {
            foreach (var entry in almanacTaskSorting)
                if (entry.Key != null)
                {
                    entry.Key.overrideSorting = entry.Value.sorting;
                    entry.Key.sortingOrder = entry.Value.order;
                }
            almanacTaskSorting.Clear();
            return;
        }
        if (taskRows == null) return;
        foreach (var row in taskRows)
        {
            if (row == null || row.rowContainer == null) continue;
            var canvas = row.rowContainer.GetComponent<Canvas>();
            if (canvas == null) canvas = row.rowContainer.AddComponent<Canvas>();
            if (!almanacTaskSorting.ContainsKey(canvas))
                almanacTaskSorting.Add(canvas, (canvas.overrideSorting, canvas.sortingOrder));
            // Viewfinder is order 150; boss dialogue remains above tasks at 160.
            canvas.overrideSorting = true;
            canvas.sortingOrder = taskViewfinderOpen ? 155 : 65;
        }
    }

    private void LateUpdate()
    {
        UpdateAlmanacTaskSorting();
        if (taskRows != null)
            foreach (var row in taskRows)
            {
                if (row != null) PrepareTaskLabel(row.taskText);
            }
        FitStudioTaskRows();
        var lesson = EditorTutorialManager.Instance;
        if (EditorManager.Instance == null || lesson == null || lesson.timelineScrollRect == null || taskRows == null) return;
        var viewport = lesson.timelineScrollRect.viewport;
        if (viewport == null || !viewport.gameObject.activeInHierarchy) return;
        if (editorChecklistFont == null)
            editorChecklistFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        viewport.GetWorldCorners(editorTimelineCorners);
        var sourceCanvas = viewport.GetComponentInParent<Canvas>();
        var sourceCamera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? sourceCanvas.worldCamera : null;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, editorTimelineCorners[1]);
        float totalHeight = 0f;
        int visibleRows = 0;
        foreach (var row in taskRows)
            if (row != null && row.rowContainer != null && row.rowContainer.activeInHierarchy)
            {
                if (row.taskText != null)
                {
                    if (editorChecklistFont != null) row.taskText.font = editorChecklistFont;
                    row.taskText.enableAutoSizing = false;
                    row.taskText.fontSize = 20f;
                    row.taskText.enableWordWrapping = true;
                    row.taskText.overflowMode = TextOverflowModes.Overflow;
                }
                if (visibleRows++ > 0) totalHeight += CrewPaperStyle.HintGap;
                totalHeight += EditorTaskHeight(row);
            }
        float rowOffset = 0f;
        foreach (var row in taskRows)
        {
            if (row == null || row.rowContainer == null || !row.rowContainer.activeInHierarchy) continue;
            var rect = row.rowContainer.transform as RectTransform;
            var parent = rect != null ? rect.parent as RectTransform : null;
            if (parent == null) continue;
            var canvas = parent.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            // Keep the checklist at the screen's left edge; use the timeline only for its lower height.
            Vector2 sideScreen = screen;
            if (canvas != null)
            {
                ((RectTransform)canvas.rootCanvas.transform).GetWorldCorners(editorTimelineCorners);
                sideScreen.x = RectTransformUtility.WorldToScreenPoint(camera, editorTimelineCorners[0]).x;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, sideScreen, camera, out var corner);
            float rowHeight = CrewPaperStyle.PlaceHint(rect, row.taskText, row.taskIcon, row.underline,
                Vector2.zero, 430f);
            // Stack above the ruler, leaving the timeline tracks clear.
            rect.position = parent.TransformPoint(corner + new Vector2(0f, 48f + totalHeight - rowOffset));
            if (lesson.currentStep == EditorTutorialManager.EditorStep.ClickExport && canvas != null)
            {
                // Use the empty area beneath the COLOR heading, above all comparison controls.
                var rootRect = (RectTransform)canvas.rootCanvas.transform;
                Vector3 safePosition = rootRect.TransformPoint(new Vector3(
                    rootRect.rect.xMin + rootRect.rect.width * .02f,
                    rootRect.rect.yMin + rootRect.rect.height * .87f, 0f));
                Vector2 safeScreen = RectTransformUtility.WorldToScreenPoint(camera, safePosition);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, safeScreen, camera, out var safeLocal);
                rect.position = parent.TransformPoint(safeLocal + Vector2.down * rowOffset);
            }
            rowOffset += rowHeight + CrewPaperStyle.HintGap;
            var background = row.rowContainer.GetComponent<Image>();
            if (background != null) background.color = new Color(.10f, .42f, .23f, .31f);
            foreach (var graphic in row.rowContainer.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            if (row.taskText != null)
            {
                var textRect = row.taskText.rectTransform;
                textRect.localScale = Vector3.one;
                textRect.localRotation = Quaternion.identity;
                textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(4f, 3f); textRect.offsetMax = new Vector2(-4f, -3f);
                if (editorChecklistFont != null && row.taskText.font != editorChecklistFont)
                {
                    row.taskText.font = editorChecklistFont;
                    row.taskText.fontSharedMaterial = editorChecklistFont.material;
                }
                row.taskText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                row.taskText.characterSpacing = 0f;
                row.taskText.wordSpacing = 0f;
                row.taskText.enableAutoSizing = false;
                row.taskText.fontSizeMin = 16f;
                row.taskText.fontSizeMax = 20f;
                row.taskText.alignment = TextAlignmentOptions.MidlineLeft;
                row.taskText.color = row.taskText.text.StartsWith("<s>") ? completedTextColor : activeTextColor;
            }
            CrewPaperStyle.Hint(rect, row.taskText, row.taskIcon, row.underline);
        }
    }

    private static float EditorTaskHeight(TaskUIRow row)
    {
        return CrewPaperStyle.HintHeight(row.taskText, 430, row.taskIcon);
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
            // Completion changes ink/checkmark only; never expand into neighboring cards.
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

    private string[] SplitDialogueIntoPages(string message)
    {
        if (string.IsNullOrEmpty(message) || CountVisibleCharacters(message) <= Mathf.Max(1, dialoguePageMaxVisibleCharacters))
            return new[] { message ?? string.Empty };

        List<string> pages = new List<string>();
        int start = 0;
        int maxCharacters = Mathf.Max(120, dialoguePageMaxVisibleCharacters);
        int minimumCharacters = Mathf.Clamp(dialoguePageMinimumVisibleCharacters, 0, maxCharacters - 1);

        while (start < message.Length)
        {
            int split = FindDialoguePageBreak(message, start, maxCharacters, minimumCharacters);
            if (split <= start || split >= message.Length)
            {
                pages.Add(message.Substring(start).Trim());
                break;
            }

            string page = message.Substring(start, split - start).Trim();
            if (!string.IsNullOrEmpty(page)) pages.Add(page);
            start = split;
            while (start < message.Length && char.IsWhiteSpace(message[start])) start++;
        }

        return pages.Count > 0 ? pages.ToArray() : new[] { message };
    }

    private int FindDialoguePageBreak(string message, int start, int maxCharacters, int minimumCharacters)
    {
        int visible = 0;
        int lastSpace = -1;
        int lastSentence = -1;
        int tagDepth = 0;
        bool insideTag = false;
        int i = start;

        for (; i < message.Length; i++)
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
                int tagStart = message.LastIndexOf('<', i);
                if (tagStart >= start)
                {
                    string tag = message.Substring(tagStart, i - tagStart + 1);
                    if (tag.StartsWith("</")) tagDepth = Mathf.Max(0, tagDepth - 1);
                    else if (!tag.EndsWith("/>") && !tag.StartsWith("<!")) tagDepth++;
                }
                continue;
            }
            if (insideTag) continue;

            visible++;
            if (char.IsWhiteSpace(character) && tagDepth == 0) lastSpace = i + 1;
            if ((character == '.' || character == '!' || character == '?' || character == '\n') && tagDepth == 0)
                lastSentence = i + 1;

            if (visible < maxCharacters) continue;

            int candidate = lastSentence > start && CountVisibleCharacters(message.Substring(start, lastSentence - start)) >= minimumCharacters
                ? lastSentence
                : lastSpace;
            if (candidate <= start) candidate = i + 1;
            return candidate;
        }

        return message.Length;
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

        CanvasGroup group = row.rowContainer.GetComponent<CanvasGroup>();
        if (group == null) group = row.rowContainer.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 0f;

        float elapsed = 0f;
        const float duration = 0.22f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOutCubic(Mathf.Clamp01(elapsed / duration));
            group.alpha = t;
            yield return null;
        }

        group.alpha = 1f;
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
