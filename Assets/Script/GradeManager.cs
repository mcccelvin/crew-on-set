using UnityEngine;

using UnityEngine.UI;
using TMPro;

public class GradeManager : MonoBehaviour
{
    [Header("UI Reference")]
    public FinalGradePanelUI gradePanelUI;

    [Header("Failure Dialogue")]
    public GameObject bossDialoguePrefab;

    private GameObject failureDialogue;
    private bool isLoadingScene = false;

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
            if (grades.letterGrade != "F")
            {
                string rewardKey = CampaignProgression.GetRewardKey(submittedLevel);
                bool rewardClaimed = PlayerPrefs.GetInt(rewardKey, 0) == 1;

                if (!rewardClaimed)
                {
                    if (CareerManager.Instance != null)
                    {
                        CareerManager.Instance.AddMoney(grades.earnedBCoins);
                    }
                    else if (grades.earnedBCoins > 0)
                    {
                        int savedMoney = PlayerPrefs.GetInt("PlayerMoney", 0);
                        PlayerPrefs.SetInt("PlayerMoney", savedMoney + grades.earnedBCoins);
                    }

                    PlayerPrefs.SetInt(rewardKey, 1);
                }

                if (CareerManager.Instance != null) CareerManager.Instance.CompleteActiveJob(grades.earnedBCoins);

                string gradedKey = CampaignProgression.GetGradedKey(submittedLevel);
                if (PlayerPrefs.GetInt(gradedKey, 0) == 0) CampaignProgression.CompleteLevel(submittedLevel);
            }

            CrossSceneData.resultApplied = true;
            PlayerPrefs.Save();
        }

        if (gradePanelUI != null)
        {
            gradePanelUI.DisplayResults(grades);
        }

        if (grades.letterGrade == "F") ShowFailureDialogue(submittedLevel);
    }

    private void ShowFailureDialogue(int submittedLevel)
    {
        if (failureDialogue != null) return;

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
            dialogueText.text = "This commercial did not meet the client's requirements, but the contract is still active. Do you want to continue and replay <color=yellow>Level " + submittedLevel + ": " + contractName + "</color>?";
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

        Destroy(failureDialogue);
        failureDialogue = null;
    }

    public void RetryContract()
    {
        if (isLoadingScene) return;

        int submittedLevel = Mathf.Clamp(CrossSceneData.submittedLevel, CampaignProgression.MinimumLevel, CampaignProgression.MaximumLevel);
        CampaignProgression.SetCurrentLevel(submittedLevel);

        if (submittedLevel == 1) PlayerPrefs.SetInt("Level1RetryActive", 1);
        PlayerPrefs.Save();

        if (ProjectDataManager.Instance != null) ProjectDataManager.Instance.ClearProject();

        CrossSceneData.finalGrades = default(ProductionGrades);
        CrossSceneData.submittedLevel = 0;
        CrossSceneData.resultApplied = false;

        isLoadingScene = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene("SingleStudio");
    }


    public void ReturnToStudio()
    {
        if (CrossSceneData.finalGrades.letterGrade == "F")
        {
            RetryContract();
            return;
        }

        if (CrossSceneData.submittedLevel == 1 && !string.IsNullOrEmpty(CrossSceneData.finalGrades.letterGrade) && CrossSceneData.finalGrades.letterGrade != "F")
        {
            int currentLevel = CampaignProgression.GetCurrentLevel();
            if (currentLevel <= 1) CampaignProgression.SetCurrentLevel(2);
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene("SingleStudio");
    }
}
