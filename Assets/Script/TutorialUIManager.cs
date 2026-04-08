using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

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
    public TextMeshProUGUI[] taskListTexts;
    public Color pendingColor = Color.white;
    public Color completedColor = Color.green;

    [Header("Tutorial Guidance Systems")]
    public TutorialGlowTarget directorTerminalGlow;
    public TutorialGlowTarget shopTerminalGlow;
    public TutorialGlowTarget computerGlow;
    public TutorialArrowGuide navigationArrow;

    private bool isTaskUIExpanded = false;
    private Coroutine notificationCoroutine;
    private Coroutine taskRevealCoroutine;

    private void Awake() { Instance = this; }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab) && taskPanel != null && taskPanel.activeSelf)
        {
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
        foreach (var t in taskListTexts) if (t != null) t.gameObject.SetActive(false);

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
            if (i < taskListTexts.Length && taskListTexts[i] != null)
            {
                taskListTexts[i].text = tasks[i];
                taskListTexts[i].color = pendingColor;
                taskListTexts[i].gameObject.SetActive(true);
                yield return new WaitForSeconds(0.4f);
            }
        }
    }

    public void MarkTaskComplete(int index)
    {
        if (index < taskListTexts.Length && taskListTexts[index] != null)
        {
            taskListTexts[index].color = completedColor;
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

    public void PointArrowAt(string keyword)
    {
        if (navigationArrow == null) return;
        if (string.IsNullOrEmpty(keyword)) { navigationArrow.PointAt(null); return; }

        TutorialGlowTarget[] glows = FindObjectsOfType<TutorialGlowTarget>();
        foreach (var g in glows) if (g.gameObject.name.ToLower().Contains(keyword.ToLower())) { navigationArrow.PointAt(g.transform); return; }
    }
}