using UnityEngine;
using TMPro;

public class ContractGrader : MonoBehaviour
{
    [Header("Client Feedback UI")]
    public TextMeshProUGUI clientFeedbackText;
    public ColorGradingManager gradingManager;
    public ContractGrader grader;

    public void FinalizeUltimateContract(int basePayment, float camScore, float lightScore, float seconds, bool hasBackground, Color wallColor, float b, float c, float s)
    {
        float colorScore = CalculateColorScore(b, c, s);

        float secondsScore = 100f - (Mathf.Abs(seconds - 20f) * 5f);
        secondsScore = Mathf.Clamp(secondsScore, 0, 100);

        float bgScore = 0f;
        if (hasBackground)
        {
            bgScore = 70f;
            if (wallColor != Color.white && wallColor != Color.clear) bgScore += 30f;
        }

        // --- FIX: Ensure production scores never exceed 100 ---
        float prodScore = Mathf.Clamp(camScore + lightScore, 0, 100);

        float totalRawScore = prodScore + secondsScore + bgScore + colorScore;
        float finalPercentage = totalRawScore / 400f; // 4 categories out of 100
        float finalScore100 = finalPercentage * 100f;

        string letterGrade = GetLetterGrade(finalScore100);

        // --- NEW: Check if the score is 70 or higher to give rewards ---
        int finalPayment = 0;
        string contractStatus = "";

        if (finalScore100 >= 70f)
        {
            // Passed! Calculate their payout based on the score.
            finalPayment = Mathf.RoundToInt(basePayment * finalPercentage);
            contractStatus = "CONTRACT PASSED";
        }
        else
        {
            // Failed! Score below 70 means 0 B-coins.
            finalPayment = 0;
            contractStatus = "CONTRACT FAILED - SCORE TOO LOW";
        }

        // Display the total overall score out of 100 with the Pass/Fail status
        string summary = $"FINAL GRADE: {letterGrade} ({finalScore100:F1}/100)\n" +
                         $"{contractStatus}\n\n" +
                         $"Production Qual: {prodScore:F0}/100\n" +
                         $"Timing: {secondsScore:F0}/100\n" +
                         $"Art Direction: {bgScore:F0}/100\n" +
                         $"Color Grade: {colorScore:F0}/100\n\n" +
                         $"Total Payout: {finalPayment}B";

        UpdateFeedback(summary, GetGradeColor(letterGrade));

        // --- FIX: SAVE MONEY DIRECTLY TO THE HARD DRIVE! ---
        int currentBank = PlayerPrefs.GetInt("PlayerMoney", 0);
        PlayerPrefs.SetInt("PlayerMoney", currentBank + finalPayment);
        PlayerPrefs.Save();

        Debug.Log($"<color=green>MONEY SAVED:</color> Added {finalPayment} B-Coins. You now have {PlayerPrefs.GetInt("PlayerMoney")}!");
    }

    private float CalculateColorScore(float b, float c, float s)
    {
        float score = 100f;
        score -= Mathf.Abs(b - 1.0f) * 50f;
        score -= Mathf.Abs(c - 1.2f) * 30f;
        score -= Mathf.Abs(s - 1.1f) * 20f;
        return Mathf.Clamp(score, 0, 100);
    }

    private string GetLetterGrade(float score)
    {
        if (score > 90) return "S";
        if (score > 80) return "A";
        if (score > 70) return "B";
        if (score > 50) return "C";
        return "F";
    }

    private Color GetGradeColor(string grade)
    {
        if (grade == "S" || grade == "A") return Color.green;
        if (grade == "B") return Color.yellow;
        return Color.red;
    }

    private void UpdateFeedback(string message, Color color)
    {
        if (clientFeedbackText != null)
        {
            clientFeedbackText.text = message;
            clientFeedbackText.color = color;
        }
    }

    public void GenerateFinalReport(float cam, float light, float sec)
    {
        float b = gradingManager != null ? gradingManager.brightnessSlider.value : 1f;
        float c = gradingManager != null ? gradingManager.contrastSlider.value : 1f;
        float s = gradingManager != null ? gradingManager.saturationSlider.value : 1f;

        StageSetupManager stage = FindObjectOfType<StageSetupManager>();
        bool hasBg = stage != null && stage.HasWall();
        Color bgCol = stage != null ? stage.currentWallColor : Color.clear;

        if (grader != null) grader.FinalizeUltimateContract(60000, cam, light, sec, hasBg, bgCol, b, c, s);
        else FinalizeUltimateContract(60000, cam, light, sec, hasBg, bgCol, b, c, s);
    }
}