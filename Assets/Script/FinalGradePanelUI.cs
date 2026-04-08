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

    public void DisplayResults(ProductionGrades grades)
    {
        gameObject.SetActive(true);

        // Score Calculation
        float overallScore = (grades.preProductionScore + grades.productionScore + grades.postProductionScore) / 3f;

        if (overallScoreText != null) overallScoreText.text = $"OVERALL GRADE: {overallScore:F0}/100";
        if (finalGradeLetterText != null) finalGradeLetterText.text = grades.letterGrade.ToUpper();

        if (contractStatusText != null)
            contractStatusText.text = (grades.letterGrade == "F") ? "CONTRACT FAILED" : "CONTRACT PASSED";

        if (preProdText != null) preProdText.text = $"PRE-PRODUCTION\t{grades.preProductionScore:F0}";
        if (prodText != null) prodText.text = $"PRODUCTION\t\t{grades.productionScore:F0}";
        if (postProdText != null) postProdText.text = $"POST-PRODUCTION\t{grades.postProductionScore:F0}";

        if (bCoinsText != null) bCoinsText.text = $"+{grades.earnedBCoins} B-COINS";

        if (gradeColorImage != null)
        {
            switch (grades.letterGrade.ToUpper())
            {
                case "S": gradeColorImage.color = new Color32(138, 43, 226, 255); break;
                case "A": gradeColorImage.color = new Color32(0, 200, 50, 255); break;
                case "B": gradeColorImage.color = new Color32(255, 215, 0, 255); break;
                case "C": gradeColorImage.color = new Color32(255, 140, 0, 255); break;
                case "F": gradeColorImage.color = new Color32(220, 20, 60, 255); break;
            }
        }

        if (feedbackDetailedText != null) feedbackDetailedText.text = grades.feedback;
        if (feedbackPanel != null) feedbackPanel.SetActive(false);
    }

    public void OpenFeedbackPanel() { if (feedbackPanel != null) feedbackPanel.SetActive(true); }
    public void CloseFeedbackPanel() { if (feedbackPanel != null) feedbackPanel.SetActive(false); }
}