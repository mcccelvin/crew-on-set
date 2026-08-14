using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

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

    private void Awake() { Instance = this; }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (AlmanacManager.Instance != null && AlmanacManager.Instance.IsOpen()) return;

        if (Input.GetKeyDown(KeyCode.Tab) && taskPanel != null && taskPanel.activeSelf)
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

    public void SetupTasks(string[] tasks)
    {
        if (taskPanel != null) taskPanel.SetActive(true);

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
                taskRows[i].taskText.color = activeTextColor;

                // Reset the icon to the diamond
                if (taskRows[i].taskIcon != null && defaultDiamondIcon != null)
                    taskRows[i].taskIcon.sprite = defaultDiamondIcon;

                // Show the gold line
                if (taskRows[i].underline != null)
                    taskRows[i].underline.gameObject.SetActive(true);

                taskRows[i].rowContainer.SetActive(true);
                yield return new WaitForSeconds(0.4f);
            }
        }
    }

    public void MarkTaskComplete(int index)
    {
        if (index < taskRows.Length && taskRows[index] != null && taskRows[index].rowContainer != null)
        {
            if (!taskRows[index].taskText.text.StartsWith("<s>"))
            {
                // Strikethrough and dim the text
                taskRows[index].taskText.text = "<s>" + taskRows[index].taskText.text + "</s>";
                taskRows[index].taskText.color = completedTextColor;

                // Optional: Change the diamond icon to a checkmark!
                if (taskRows[index].taskIcon != null && completedCheckIcon != null)
                    taskRows[index].taskIcon.sprite = completedCheckIcon;

                // Optional: Hide the underline when complete for a cleaner look
                if (taskRows[index].underline != null)
                    taskRows[index].underline.gameObject.SetActive(false);
            }
        }
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
        TutorialGlowTarget[] glows = FindObjectsOfType<TutorialGlowTarget>();
        foreach (var g in glows) if (g.gameObject.name.ToLower().Contains(keyword.ToLower())) { if (state) g.StartGlowing(); else g.StopGlowing(); }
    }
}
