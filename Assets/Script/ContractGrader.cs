using UnityEngine;

public class ContractGrader : MonoBehaviour
{
    public enum GameLevel { Tutorial = 0, Stage1 = 1 }

    public ProductionGrades GenerateGrades(float avgCam, float avgLight, float totalSeconds)
    {
        int progress = PlayerPrefs.GetInt("TutorialProgress", 0);
        GameLevel currentLevel = (progress < 2) ? GameLevel.Tutorial : GameLevel.Stage1;

        if (currentLevel == GameLevel.Tutorial) return GradeTutorial(avgCam, avgLight, totalSeconds);
        else return GradeStage1(avgCam, avgLight, totalSeconds);
    }

    private ProductionGrades GradeTutorial(float avgCam, float avgLight, float totalSeconds)
    {
        float prod = 100f, post = 100f;
        string feedback = "";

        // --- 1. PRE-PRODUCTION (Smuggled from Studio) ---
        float pre = 100f;
        if (ProjectDataManager.Instance != null)
        {
            pre = ProjectDataManager.Instance.savedPreProdScore;
            feedback += ProjectDataManager.Instance.savedPreProdFeedback;
        }

        // --- 2. PRODUCTION ---
        feedback += "<color=white><b>--- PRODUCTION ---</b></color>\n";
        if (avgLight >= 10f) feedback += "<color=green>+ Subject is lit well.</color>\n";
        else { prod -= 20f; feedback += "<color=yellow>- The subject was too dark or reflective.</color>\n"; }

        if (avgCam >= 30f) feedback += "<color=green>+ Great Camera Work (Subject centered)</color>\n";
        else { prod -= 30f; feedback += "<color=red>- Poor Camera Work. Keep subject centered!</color>\n"; }
        feedback += "\n";

        // --- 3. POST-PRODUCTION ---
        feedback += "<color=white><b>--- POST-PRODUCTION ---</b></color>\n";
        if (Mathf.Abs(totalSeconds - 10f) <= 1.5f) feedback += "<color=green>+ Perfect Timing (10s)</color>\n";
        else { post -= 30f; feedback += $"<color=red>- Timing off. Target: 10s. Yours: {totalSeconds:F1}s.</color>\n"; }

        int logoCount = FindObjectsOfType<BrandingClip>(true).Length;
        if (logoCount == 1) feedback += "<color=green>+ Clean Branding (1 Logo)</color>\n";
        else { post -= 20f; feedback += "<color=red>- You must include exactly 1 logo.</color>\n"; }

        ColorGradingManager grading = FindObjectOfType<ColorGradingManager>(true);
        if (grading != null)
        {
            if (grading.saturationSlider.value > 2.5f) { post -= 20f; feedback += "<color=red>- Image deep-fried! Saturation too high.</color>\n"; }
            else if (grading.saturationSlider.value > 1.2f) feedback += "<color=green>+ Good Color Enhancement</color>\n";
            else { post -= 20f; feedback += "<color=yellow>- Boost saturation to make it pop.</color>\n"; }
        }

        return CompileFinalGrade(pre, prod, post, feedback, 15000);
    }

    private ProductionGrades GradeStage1(float avgCam, float avgLight, float totalSeconds)
    {
        float prod = 100f, post = 100f;
        string feedback = "";

        // --- 1. PRE-PRODUCTION (Smuggled from Studio) ---
        float pre = 100f;
        if (ProjectDataManager.Instance != null)
        {
            pre = ProjectDataManager.Instance.savedPreProdScore;
            feedback += ProjectDataManager.Instance.savedPreProdFeedback;
        }

        // --- 2. PRODUCTION ---
        feedback += "<color=white><b>--- PRODUCTION ---</b></color>\n";
        if (avgCam >= 35f) feedback += "<color=green>+ Perfect Rule of Thirds Composition</color>\n";
        else { prod -= 30f; feedback += "<color=red>- Poor composition. Keep subject on thirds.</color>\n"; }

        if (avgLight >= 25f) feedback += "<color=green>+ Excellent 3-Point Lighting</color>\n";
        else { prod -= 30f; feedback += "<color=yellow>- Lighting lacked depth. Need Key, Fill, Backlight.</color>\n"; }
        feedback += "\n";

        // --- 3. POST-PRODUCTION ---
        feedback += "<color=white><b>--- POST-PRODUCTION ---</b></color>\n";
        if (Mathf.Abs(totalSeconds - 10f) <= 1.0f) feedback += "<color=green>+ Perfect 10s Cut</color>\n";
        else { post -= 30f; feedback += $"<color=red>- We asked for 10s, you gave {totalSeconds:F1}s.</color>\n"; }

        int brandingCount = FindObjectsOfType<BrandingClip>(true).Length;
        if (brandingCount == 3) feedback += "<color=green>+ Excellent Paced Branding Sequence</color>\n";
        else { post -= 30f; feedback += $"<color=red>- We needed 3 logos. You placed {brandingCount}.</color>\n"; }

        ColorGradingManager grading = FindObjectOfType<ColorGradingManager>(true);
        if (grading != null)
        {
            if (grading.contrastSlider.value > 2.5f) { post -= 20f; feedback += "<color=red>- Contrast too high, shadows crushed!</color>\n"; }
            else if (grading.contrastSlider.value > 1.15f) feedback += "<color=green>+ Great contrast separation.</color>\n";
            else { post -= 20f; feedback += "<color=yellow>- Boost Contrast to separate subject.</color>\n"; }
        }

        return CompileFinalGrade(pre, prod, post, feedback, 60000);
    }

    private ProductionGrades CompileFinalGrade(float pre, float prod, float post, string feedback, int maxPayout)
    {
        float finalScore = (pre + prod + post) / 3f;
        string letterGrade = "F";
        int payout = 0;

        if (finalScore >= 90) { letterGrade = "S"; payout = maxPayout; }
        else if (finalScore >= 80) { letterGrade = "A"; payout = (int)(maxPayout * 0.8f); }
        else if (finalScore >= 70) { letterGrade = "B"; payout = (int)(maxPayout * 0.6f); }
        else if (finalScore >= 60) { letterGrade = "C"; payout = (int)(maxPayout * 0.3f); }
        else { letterGrade = "F"; payout = 0; }

        return new ProductionGrades
        {
            preProductionScore = pre,
            productionScore = prod,
            postProductionScore = post,
            letterGrade = letterGrade,
            feedback = feedback,
            earnedBCoins = payout
        };
    }
}