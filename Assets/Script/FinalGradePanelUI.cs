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

    public bool IsFeedbackOpen
    {
        get { return feedbackPanel != null && feedbackPanel.activeInHierarchy; }
    }

    public void DisplayResults(ProductionGrades grades)
    {
        gameObject.SetActive(true);
        ApplyMenuTheme();

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

        if (feedbackButtonText != null) feedbackButtonText.text = "REVIEW FEEDBACK";
        if (returnButtonText != null) returnButtonText.text = grades.letterGrade == "F" ? "REPLAY CONTRACT" : "RETURN TO STUDIO";
        if (feedbackPanel != null) feedbackPanel.SetActive(false);
    }

    public void ShowFailureQuestion(int level)
    {
        if (contractStatusText != null) contractStatusText.text = "REPLAY LEVEL " + level + "?";
    }

    private bool themeApplied;
    private ScrollRect feedbackScroll;
    private void ApplyMenuTheme()
    {
        if (themeApplied || feedbackPanel == null || feedbackDetailedText == null) return;
        themeApplied = true;
        // Leave the original Review scene artwork, positions and buttons untouched.
        Image background = feedbackPanel.GetComponent<Image>();
        if (background != null) { background.sprite = null; background.color = EditorWorkspaceUI.Panel; }
        feedbackDetailedText.color = EditorWorkspaceUI.Ink;
        feedbackDetailedText.font = TMP_Settings.defaultFontAsset;
        feedbackDetailedText.fontSize = 28;
        feedbackDetailedText.enableAutoSizing = false;
        feedbackDetailedText.enableWordWrapping = true;
        feedbackDetailedText.alignment = TextAlignmentOptions.TopLeft;
        feedbackDetailedText.raycastTarget = false;
        feedbackDetailedText.lineSpacing = 8;
        GameObject headingObject = new GameObject("Feedback Heading", typeof(RectTransform), typeof(TextMeshProUGUI));
        headingObject.transform.SetParent(feedbackPanel.transform, false);
        TMP_Text heading = headingObject.GetComponent<TMP_Text>();
        heading.font = TMP_Settings.defaultFontAsset;
        heading.text = "CLIENT FEEDBACK  <size=55%>/  SCROLL TO READ</size>";
        heading.fontSize = 36; heading.color = EditorWorkspaceUI.Ink; heading.raycastTarget = false;
        EditorWorkspaceUI.Place(heading.rectTransform, .06f, .87f, .8f, .97f);
        GameObject viewport = new GameObject("Feedback Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewport.transform.SetParent(feedbackPanel.transform, false);
        RectTransform rect = viewport.GetComponent<RectTransform>();
        EditorWorkspaceUI.Place(rect, .06f, .08f, .94f, .84f);
        viewport.GetComponent<Image>().color = Color.clear;
        feedbackDetailedText.transform.SetParent(rect, false);
        RectTransform content = feedbackDetailedText.rectTransform;
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        feedbackScroll = viewport.GetComponent<ScrollRect>();
        feedbackScroll.viewport = rect; feedbackScroll.content = content;
        feedbackScroll.horizontal = false; feedbackScroll.scrollSensitivity = 35;
        feedbackScroll.movementType = ScrollRect.MovementType.Clamped;
        Transform close = feedbackPanel.transform.Find("Close");
        if (close != null)
        {
            close.gameObject.SetActive(false);
            Button closeButton = EditorWorkspaceUI.Button(feedbackPanel.transform, "CLOSE", .82f, .89f, .95f, .96f, CloseFeedbackPanel);
            closeButton.transform.SetAsLastSibling();
        }
    }

    public void SetSuccessfulContinueLabel(int completedLevel)
    {
        if (returnButtonText == null) return;

        if (completedLevel >= CampaignProgression.MaximumLevel)
        {
            returnButtonText.text = "FINISH CAMPAIGN";
            return;
        }

        returnButtonText.text = "CONTINUE TO LEVEL " + (completedLevel + 1);
    }

    public void OpenFeedbackPanel()
    {
        if (feedbackPanel == null) return;
        feedbackPanel.transform.SetAsLastSibling();
        feedbackPanel.SetActive(true);
        Canvas.ForceUpdateCanvases();
        if (feedbackScroll != null) feedbackScroll.verticalNormalizedPosition = 1;
    }
    public void CloseFeedbackPanel() { if (feedbackPanel != null) feedbackPanel.SetActive(false); }
}
