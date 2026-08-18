using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.InputSystem;

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
    private Player.Manager.InputManager inputManager;
    private bool[] completedTaskRows;

    private void Awake() { Instance = this; }

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
        }
    }

    public void ShowBossDialogue(string message, Sprite pose, bool showOk, bool showSkip)
    {
        if (bossHUDCanvas != null) bossHUDCanvas.SetActive(true);
        if (taskPanel != null) taskPanel.SetActive(false);
        if (bossText != null) bossText.text = message;
        if (bossPortraitDisplay != null) bossPortraitDisplay.sprite = pose;
        if (okButton != null) okButton.SetActive(showOk);
        if (skipButton != null) skipButton.SetActive(showSkip);
    }

    public void HideBossDialogue()
    {
        if (bossHUDCanvas != null) bossHUDCanvas.SetActive(false);
    }

    public bool IsBossDialogueOpen()
    {
        return bossHUDCanvas != null && bossHUDCanvas.activeSelf;
    }

    public void SetupTasks(string[] tasks)
    {
        if (taskPanel != null) taskPanel.SetActive(true);

        completedTaskRows = new bool[taskRows.Length];

        // Hide all rows initially
        foreach (var row in taskRows)
        {
            if (row.rowContainer != null) row.rowContainer.SetActive(false);
        }

        if (taskRevealCoroutine != null) StopCoroutine(taskRevealCoroutine);
        taskRevealCoroutine = StartCoroutine(RevealTasksSequentially(tasks));

        if (notificationCoroutine != null) StopCoroutine(notificationCoroutine);
        if (newTaskNotification != null) newTaskNotification.SetActive(false);

        notificationCoroutine = StartCoroutine(ShowNewTaskNotification());
    }

    private IEnumerator RevealTasksSequentially(string[] tasks)
    {
        for (int i = 0; i < tasks.Length; i++)
        {
            if (i < taskRows.Length && taskRows[i] != null && taskRows[i].rowContainer != null)
            {
                // Clean up the text (remove the dash if it exists so it looks cleaner next to the icon)
                string cleanText = tasks[i].StartsWith("- ") ? tasks[i].Substring(2) : tasks[i];

                taskRows[i].taskText.text = cleanText;
                taskRows[i].taskText.enableWordWrapping = false;
                taskRows[i].taskText.overflowMode = TextOverflowModes.Ellipsis;
                taskRows[i].taskText.enableAutoSizing = true;
                taskRows[i].taskText.fontSizeMin = 14f;
                ApplyTaskState(i);

                taskRows[i].rowContainer.SetActive(true);
                yield return new WaitForSeconds(0.4f);
            }
        }
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

        if (taskPanel != null) taskPanel.SetActive(false);
        if (newTaskNotification != null) newTaskNotification.SetActive(false);
    }

    public void MarkTaskComplete(int index)
    {
        if (index < 0 || index >= taskRows.Length || taskRows[index] == null) return;

        if (completedTaskRows == null || completedTaskRows.Length != taskRows.Length)
        {
            completedTaskRows = new bool[taskRows.Length];
        }

        completedTaskRows[index] = true;
        if (taskRows[index].rowContainer != null && taskRows[index].rowContainer.activeSelf) ApplyTaskState(index);
    }

    private void ApplyTaskState(int index)
    {
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
            newTaskNotification.SetActive(true);
            yield return new WaitForSeconds(4f);
            if (newTaskNotification != null) newTaskNotification.SetActive(false);
        }
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
