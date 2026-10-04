using PlayerPrefs = GameSavePrefs;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FinalGradePanelUI : MonoBehaviour
{
    [Header("Main Panel Text Elements")]
    public TextMeshProUGUI overallScoreText;
    public TextMeshProUGUI finalGradeLetterText;
    public TextMeshProUGUI contractStatusText;
    public TextMeshProUGUI bCoinsText;

    [Header("Parallel Phase Scores")]
    public TextMeshProUGUI preProdText;
    public TextMeshProUGUI prodText;
    public TextMeshProUGUI postProdText;

    [Header("Grade Visuals")]
    public Image gradeColorImage;

    [Header("Feedback Panel UI")]
    public GameObject feedbackPanel;
    public TextMeshProUGUI feedbackDetailedText;
    public TextMeshProUGUI feedbackButtonText;
    public TextMeshProUGUI returnButtonText;
    private FeedbackPaperUI paperReview;
    private ProductionGrades displayedGrades;
    private System.Action continueAction;
    private string continueLabel;

    private void Awake() { HideLegacySummary(); }

    private void HideLegacySummary()
    {
        // Keep the scene controller and serialized UnityEvents intact, but retire
        // its old summary/feedback visuals. The paper review owns a separate canvas.
        foreach (Transform child in transform) child.gameObject.SetActive(false);
        var canvas = GetComponent<Canvas>();
        if (canvas != null) canvas.enabled = false;
        var raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster != null) raycaster.enabled = false;
    }

    public bool IsFeedbackOpen
    {
        get { return paperReview != null && paperReview.IsOpen; }
    }

    public void DisplayResults(ProductionGrades grades)
    {
        gameObject.SetActive(true);
        HideLegacySummary();
        displayedGrades = grades;

        // Score Calculation
        float overallScore = (grades.preProductionScore + grades.productionScore + grades.postProductionScore) / 3f;

        if (overallScoreText != null) overallScoreText.text = $"OVERALL GRADE: {overallScore:F0}/100";
        if (finalGradeLetterText != null) finalGradeLetterText.text = grades.letterGrade.ToUpper();

        if (contractStatusText != null)
            contractStatusText.text = (grades.letterGrade == "F") ? "CONTRACT FAILED" : "CONTRACT PASSED";

        if (preProdText != null) preProdText.text = $"PRE-PRODUCTION\t{grades.preProductionScore:F0}";
        if (prodText != null) prodText.text = $"PRODUCTION\t\t{grades.productionScore:F0}";
        if (postProdText != null) postProdText.text = $"POST-PRODUCTION\t{grades.postProductionScore:F0}";

        if (bCoinsText != null)
        {
            bCoinsText.enableAutoSizing = true;
            bCoinsText.fontSizeMin = 22;
            bCoinsText.fontSizeMax = 44;
            bCoinsText.text = (grades.earnedBCoins > 0 ? $"PAYMENT +{grades.earnedBCoins:N0}" : "NO NEW PAYMENT")
                + $"\nBALANCE {PlayerPrefs.GetInt("PlayerMoney", 0):N0} B-COINS";
        }

        if (gradeColorImage != null)
        {
            switch (grades.letterGrade.ToUpper())
            {
                case "S": gradeColorImage.color = new Color32(210, 180, 112, 255); break;
                case "A": gradeColorImage.color = new Color32(130, 164, 117, 255); break;
                case "B": gradeColorImage.color = new Color32(107, 143, 180, 255); break;
                case "C": gradeColorImage.color = new Color32(180, 143, 110, 255); break;
                default: gradeColorImage.color = new Color32(173, 85, 84, 255); break;
            }
        }

        if (feedbackDetailedText != null)
        {
            if (grades.letterGrade == "F")
                feedbackDetailedText.text = "<color=#7A3E12><b>BOSS:</b> We've got some client notes. Let's look through what needs changing, then give this contract another shot.</color>\n\n" + grades.feedback;
            else
                feedbackDetailedText.text = grades.feedback;
            feedbackDetailedText.text = feedbackDetailedText.text.Replace("<color=white>", "<color=#181818>")
                .Replace("<color=green>", "<color=#22652D>").Replace("<color=yellow>", "<color=#795012>")
                .Replace("<color=red>", "<color=#A32323>");
            feedbackDetailedText.text = $"<b>PRE-PRODUCTION: {grades.preProductionScore:F1}/100\nPRODUCTION: {grades.productionScore:F1}/100\nPOST-PRODUCTION: {grades.postProductionScore:F1}/100</b>\n\n" + feedbackDetailedText.text;
        }

        if (feedbackButtonText != null) feedbackButtonText.text = "FEEDBACK & BUDGET";
        if (returnButtonText != null) returnButtonText.text = grades.letterGrade == "F" ? "REPLAY CONTRACT" : "RETURN TO STUDIO";
        continueLabel = grades.letterGrade == "F" ? "REPLAY CONTRACT" : "RETURN TO STUDIO";
        if (feedbackPanel != null) feedbackPanel.SetActive(false);
        OpenFeedbackPanel();
    }

    public void ShowFailureQuestion(int level)
    {
        if (contractStatusText != null) contractStatusText.text = "REPLAY LEVEL " + level + "?";
    }

    [SerializeField] private ScrollRect feedbackScroll;
#if UNITY_EDITOR
    public void BakeHierarchyUI() { ApplyMenuTheme(); }
#endif

    private void ApplyMenuTheme()
    {
        foreach (var canvas in GetComponentsInParent<Canvas>(true))
            FeedbackPaperUI.ConfigureScale(canvas.rootCanvas);
    }

    public void SetSuccessfulContinueLabel(int completedLevel)
    {
        continueLabel = completedLevel >= CampaignProgression.MaximumLevel ? "FINISH CAMPAIGN" : "CONTINUE TO LEVEL " + (completedLevel + 1);
        if (returnButtonText != null) returnButtonText.text = continueLabel;
        if (paperReview != null) paperReview.SetContinueLabel(continueLabel);
    }

    public void SetContinueAction(System.Action action) { continueAction = action; }

    public void OpenFeedbackPanel()
    {
        if (string.IsNullOrEmpty(displayedGrades.letterGrade)) return;
        if (paperReview == null) paperReview = FeedbackPaperUI.Create();
        // There is no older result screen to return to. Continue/Replay on the
        // decision paper is the exit, without silently advancing the contract.
        paperReview.AllowClose = false;
        int level = Mathf.Clamp(CrossSceneData.submittedLevel,1,5);
        string budget = "Current balance: " + PlayerPrefs.GetInt("PlayerMoney",0).ToString("N0") + " B-Coins.\n" +
            "Detailed spending was not tracked for this older result. Owned equipment is not charged again.";
        paperReview.Present(displayedGrades,level,budget,continueLabel,continueAction,null,
            CrossSceneData.submittedWithTutorial && !DevTutorialBypass.Disabled
            && level == 1 && PlayerPrefs.GetInt("FeedbackPaper.TutorialSeen",0) == 0);
    }
    public void CloseFeedbackPanel()
    {
        if (paperReview != null) paperReview.RequestClose();
        if (feedbackPanel != null) feedbackPanel.SetActive(false);
    }
    private void OnDestroy() { if (paperReview != null) Destroy(paperReview.gameObject); }
}
