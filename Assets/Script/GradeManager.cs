using PlayerPrefs = GameSavePrefs;
using UnityEngine;

using System.Collections;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GradeManager : MonoBehaviour
{
    [Header("UI Reference")]
    public FinalGradePanelUI gradePanelUI;

    [Header("Failure Dialogue")]
    public GameObject bossDialoguePrefab;

    [Header("Campaign Continuation")]
    [Tooltip("How long a passing grade stays visible before the next level starts. Opening feedback pauses this timer.")]
    [Min(0f)] public float successfulResultHoldSeconds = 5f;

    [SerializeField] private GameObject failureDialogue;
    private bool isLoadingScene = false;
    private Coroutine successfulContinuation;

    private void Start()
    {
        // --- THE FIX: Force the mouse to unlock so you can click buttons ---
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EvaluateSmuggledGrades();
    }

    public void EvaluateSmuggledGrades()
    {
        ProductionGrades grades = CrossSceneData.finalGrades;

        if (string.IsNullOrEmpty(grades.letterGrade))
        {
            Debug.LogWarning("No submitted commercial result was found. Return to the Studio and submit a commercial through the Editor.");
            return;
        }

        int submittedLevel = Mathf.Clamp(CrossSceneData.submittedLevel, CampaignProgression.MinimumLevel, CampaignProgression.MaximumLevel);

        if (!CrossSceneData.resultApplied)
        {
            // Keep a personal best per brief so retries have visible progress.
            string bestKey = "ContractBestScore_Level" + submittedLevel;
            float score = Mathf.Clamp((grades.preProductionScore + grades.productionScore + grades.postProductionScore) / 3f, 0f, 100f);
            bool hasBest = PlayerPrefs.HasKey(bestKey);
            float previousBest = PlayerPrefs.GetFloat(bestKey, 0f);
            if (!hasBest || score > previousBest)
                PlayerPrefs.SetFloat(bestKey, score);
            string progress = !hasBest ? $"First attempt: {score:F1}/100."
                : score > previousBest ? $"New personal best: {score:F1}/100 (+{score - previousBest:F1})."
                : $"Personal best: {previousBest:F1}/100. This attempt: {score:F1}/100.";
            grades.feedback += "\n<color=white><b>YOUR PROGRESS</b></color>\n" + progress
                + "\nAim to improve your weakest department. Mandatory contract requirements still apply.\n";
            if (grades.letterGrade != "F")
            {
                string rewardKey = CampaignProgression.GetRewardKey(submittedLevel);
                bool rewardClaimed = PlayerPrefs.GetInt(rewardKey, 0) == 1;
                if (rewardClaimed) grades.earnedBCoins = 0;

                if (!rewardClaimed)
                {
                    if (CareerManager.Instance != null)
                    {
                        CareerManager.Instance.AddMoney(grades.earnedBCoins);
                    }
                    else if (grades.earnedBCoins > 0)
                    {
                        int savedMoney = PlayerPrefs.GetInt("PlayerMoney", 0);
                        PlayerPrefs.SetInt("PlayerMoney", (int)System.Math.Min(int.MaxValue, (long)Mathf.Max(0, savedMoney) + grades.earnedBCoins));
                    }

                    PlayerPrefs.SetInt(rewardKey, 1);
                }

                if (CareerManager.Instance != null) CareerManager.Instance.CompleteActiveJob(grades.earnedBCoins);

                CampaignProgression.CompleteLevel(submittedLevel);
            }

            CrossSceneData.resultApplied = true;
            CrossSceneData.finalGrades = grades;
            PlayerPrefs.Save();
        }

        if (gradePanelUI != null)
        {
            gradePanelUI.DisplayResults(grades);
            if (grades.letterGrade != "F") gradePanelUI.SetSuccessfulContinueLabel(submittedLevel);
        }

        if (grades.letterGrade == "F")
        {
            ShowFailureDialogue(submittedLevel);
        }
        // Results stay open until the player chooses Continue to Level.
        // A reading timer must not skip the review or erase the current project.
    }

    private IEnumerator ContinueAfterSuccessfulResult()
    {
        float remaining = successfulResultHoldSeconds;

        while (remaining > 0f && !isLoadingScene)
        {
            // Detailed feedback pauses the automatic handoff so it remains readable.
            if (gradePanelUI == null || !gradePanelUI.IsFeedbackOpen)
            {
                remaining -= Time.unscaledDeltaTime;
            }

            yield return null;
        }

        successfulContinuation = null;
        if (!isLoadingScene) ReturnToStudio();
    }

#if UNITY_EDITOR
    public void BakeHierarchyUI()
    {
        ShowFailureDialogue(1);
        if(failureDialogue!=null)failureDialogue.SetActive(false);
    }
#endif

    private void ShowFailureDialogue(int submittedLevel)
    {
        if (failureDialogue != null)
        {
            foreach(var text in failureDialogue.GetComponentsInChildren<TextMeshProUGUI>(true))
                if(text.name=="Dialogue")text.text="The client needs a few changes. Review the feedback, then try Level "+submittedLevel+": "+CampaignProgression.GetContractName(submittedLevel)+" again.";
            foreach(var button in failureDialogue.GetComponentsInChildren<Button>(true))
            {
                if(button.name!="Retry Contract Button"&&button.name!="Not Now Button")continue;
                button.onClick.RemoveAllListeners();
                if(button.name=="Retry Contract Button")button.onClick.AddListener(RetryContract);
                else button.onClick.AddListener(CloseFailureDialogue);
            }
            if(gradePanelUI!=null)gradePanelUI.ShowFailureQuestion(submittedLevel);
            failureDialogue.SetActive(true);return;
        }

        if (gradePanelUI != null) gradePanelUI.ShowFailureQuestion(submittedLevel);
        if (bossDialoguePrefab == null) return;

        failureDialogue = Instantiate(bossDialoguePrefab);
        failureDialogue.name = "Contract Failed Dialogue";

        RectTransform dialogueRect = failureDialogue.GetComponent<RectTransform>();
        if (dialogueRect != null)
        {
            dialogueRect.anchorMin = Vector2.zero;
            dialogueRect.anchorMax = Vector2.one;
            dialogueRect.offsetMin = Vector2.zero;
            dialogueRect.offsetMax = Vector2.zero;
            dialogueRect.localScale = Vector3.one;
        }

        Canvas dialogueCanvas = failureDialogue.GetComponent<Canvas>();
        if (dialogueCanvas != null)
        {
            dialogueCanvas.overrideSorting = true;
            dialogueCanvas.sortingOrder = 100;
        }

        TextMeshProUGUI dialogueText = null;
        TextMeshProUGUI continueText = null;
        TextMeshProUGUI[] dialogueTexts = failureDialogue.GetComponentsInChildren<TextMeshProUGUI>(true);

        foreach (TextMeshProUGUI currentText in dialogueTexts)
        {
            if (currentText.gameObject.name == "Dialogue") dialogueText = currentText;
            else if (currentText.gameObject.name == "Continue") continueText = currentText;
        }

        if (dialogueText != null)
        {
            string contractName = CampaignProgression.GetContractName(submittedLevel);
            dialogueText.text = "The client needs a few changes. Have a look at the feedback; want another take on <color=#7A3E12>Level " + submittedLevel + ": " + contractName + "</color>?";
        }

        if (continueText != null) continueText.gameObject.SetActive(false);

        Transform buttonParent = dialogueText != null ? dialogueText.transform.parent : failureDialogue.transform;
        TMP_FontAsset buttonFont = continueText != null ? continueText.font : (dialogueText != null ? dialogueText.font : null);

        Button retryButton = CreateFailureButton("Retry Contract Button", buttonParent, "REPLAY CONTRACT", new Vector2(-220f, -180f), new Color32(35, 115, 220, 255), buttonFont);
        Button notNowButton = CreateFailureButton("Not Now Button", buttonParent, "NOT NOW", new Vector2(220f, -180f), new Color32(85, 85, 95, 255), buttonFont);

        retryButton.onClick.AddListener(RetryContract);
        notNowButton.onClick.AddListener(CloseFailureDialogue);
        failureDialogue.SetActive(true);
    }

    private Button CreateFailureButton(string objectName, Transform parent, string label, Vector2 position, Color color, TMP_FontAsset font)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.layer = parent.gameObject.layer;
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = position;
        buttonRect.sizeDelta = new Vector2(380f, 72f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = color;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;

        GameObject labelObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.layer = buttonObject.layer;
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.font = font;
        labelText.fontSize = 30f;
        labelText.fontStyle = FontStyles.Bold;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;

        return button;
    }

    private void CloseFailureDialogue()
    {
        if (failureDialogue == null) return;

        failureDialogue.SetActive(false);
    }

    public void RetryContract()
    {
        if (isLoadingScene) return;

        StopSuccessfulContinuation();

        int submittedLevel = Mathf.Clamp(CrossSceneData.submittedLevel, CampaignProgression.MinimumLevel, CampaignProgression.MaximumLevel);
        CampaignProgression.SetRetryLevel(submittedLevel);

        if (ProjectDataManager.Instance != null) ProjectDataManager.Instance.ClearProject();

        CrossSceneData.finalGrades = default(ProductionGrades);
        CrossSceneData.submittedLevel = 0;
        CrossSceneData.resultApplied = false;

        isLoadingScene = true;
        PrepareForStudioLoad();
        SceneManager.LoadScene("SingleStudio");
    }


    public void ReturnToStudio()
    {
        if (isLoadingScene) return;

        if (CrossSceneData.finalGrades.letterGrade == "F")
        {
            RetryContract();
            return;
        }

        int completedLevel = Mathf.Clamp(CrossSceneData.submittedLevel, CampaignProgression.MinimumLevel, CampaignProgression.MaximumLevel);
        if (!string.IsNullOrEmpty(CrossSceneData.finalGrades.letterGrade))
        {
            int currentLevel = CampaignProgression.GetCurrentLevel();
            int expectedLevel = Mathf.Min(completedLevel + 1, CampaignProgression.MaximumLevel);

            // Recover older saves/results whose grade was shown before the next
            // campaign level had been persisted.
            if (completedLevel < CampaignProgression.MaximumLevel && currentLevel < expectedLevel)
            {
                CampaignProgression.SetCurrentLevel(expectedLevel);
            }
        }

        StopSuccessfulContinuation();
        if (ProjectDataManager.Instance != null) ProjectDataManager.Instance.ClearProject();

        isLoadingScene = true;
        PrepareForStudioLoad();
        SceneManager.LoadScene("SingleStudio");
    }

    private void StopSuccessfulContinuation()
    {
        if (successfulContinuation == null) return;

        StopCoroutine(successfulContinuation);
        successfulContinuation = null;
    }

    private void PrepareForStudioLoad()
    {
        PauseManager.isPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
