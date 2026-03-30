using UnityEngine;
using TMPro;

public class ContractGrader : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI gradeText;
    public TextMeshProUGUI feedbackText;
    public TextMeshProUGUI payoutText;

    [Header("Level 1 Requirements")]
    public float targetDuration = 10f;
    public int requiredBrandingCount = 1; // --- UPDATED: Only 1 for Level 1 ---

    public void GenerateFinalReport(float avgCam, float avgLight, float totalSeconds)
    {
        int score = 100;
        string feedback = "Client Feedback:\n\n";

        // 1. Check Duration (10 Seconds)
        if (Mathf.Abs(totalSeconds - targetDuration) <= 1.5f)
        {
            feedback += "<color=green>+ Perfect Timing (10s)</color>\n";
        }
        else
        {
            score -= 20;
            feedback += $"<color=red>- Timing off. We asked for 10s, you gave us {totalSeconds:F1}s.</color>\n";
        }

        // 2. Check Branding (EXACTLY 1 Logo)
        int brandingCount = FindObjectsOfType<BrandingClip>().Length;
        if (brandingCount == requiredBrandingCount)
        {
            feedback += "<color=green>+ Clean Branding (Only the Logo used)</color>\n";
        }
        else if (brandingCount > requiredBrandingCount)
        {
            score -= 20;
            feedback += $"<color=red>- Too cluttered! We only asked for {requiredBrandingCount} Logo, but you added {brandingCount}.</color>\n";
        }
        else
        {
            score -= 20;
            feedback += $"<color=red>- Missing graphics! You forgot to add our Logo.</color>\n";
        }

        // 3. Check Background Color & Lighting
        StageSetupManager stage = FindObjectOfType<StageSetupManager>();
        if (stage != null)
        {
            Color bg = stage.currentWallColor;
            // Check if the wall is very red and has low green/blue!
            if (bg.r > 0.5f && bg.g < 0.4f && bg.b < 0.4f)
            {
                feedback += "<color=green>+ Great Set Design (Perfect Red Backdrop)</color>\n";
            }
            else
            {
                score -= 15;
                feedback += "<color=yellow>- Wrong set color. We requested a RED backdrop.</color>\n";
            }
        }

        // 4. Check for Soft Light (No reflective hard light)
        if (avgLight >= 15f) // High score means they avoided bad reflective angles!
        {
            feedback += "<color=green>+ Excellent Lighting (No harsh reflections)</color>\n";
        }
        else
        {
            score -= 10;
            feedback += "<color=yellow>- Lighting was a bit harsh/reflective. Try tilting the light higher next time.</color>\n";
        }

        // 5. Check Color Grading (Vibrant Pop)
        ColorGradingManager grading = FindObjectOfType<ColorGradingManager>();
        if (grading != null)
        {
            if (grading.saturationSlider.value > 1.2f && grading.contrastSlider.value > 1.1f)
            {
                feedback += "<color=green>+ Excellent Color Grade! The Goke Cola bottle looks incredibly refreshing.</color>\n";
            }
            else
            {
                score -= 15;
                feedback += "<color=yellow>- Color is a bit flat. Try boosting Saturation > 1.2 and Contrast > 1.1 next time.</color>\n";
            }
        }

        // Calculate Final Letter Grade and Payout
        string letterGrade = "F";
        int payout = 0;

        if (score >= 90) { letterGrade = "S"; payout = 30000; }
        else if (score >= 80) { letterGrade = "A"; payout = 25000; }
        else if (score >= 70) { letterGrade = "B"; payout = 15000; }
        else if (score >= 60) { letterGrade = "C"; payout = 5000; }
        else { letterGrade = "F"; payout = 0; }

        if (gradeText != null) gradeText.text = letterGrade;
        if (feedbackText != null) feedbackText.text = feedback;
        if (payoutText != null) payoutText.text = $"Payout: B {payout}";

        if (CareerManager.Instance != null)
        {
            CareerManager.Instance.playerMoney += payout;
            CareerManager.Instance.UpdateMoneyUI();
        }
    }
}