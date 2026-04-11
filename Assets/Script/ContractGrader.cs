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

        float pre = 100f;
        if (ProjectDataManager.Instance != null)
        {
            pre = ProjectDataManager.Instance.savedPreProdScore;
            feedback += ProjectDataManager.Instance.savedPreProdFeedback;
        }

        feedback += "<color=white><b>--- PRODUCTION ---</b></color>\n";

        if (avgLight >= 10f) feedback += "<color=green>+ Excellent Lighting: The subject is clearly visible at 45% intensity.</color>\n";
        else { prod -= 20f; feedback += "<color=yellow>- Lighting Error: The subject was too dark. Did you forget to turn on the light?</color>\n"; }

        if (avgCam >= 30f) feedback += "<color=green>+ Perfect Framing: The subject was kept dead-center for the entire take.</color>\n";
        else { prod -= 30f; feedback += "<color=red>- Poor Framing: The subject drifted. You must keep the camera perfectly still!</color>\n"; }
        feedback += "\n";

        feedback += "<color=white><b>--- POST-PRODUCTION ---</b></color>\n";

        if (Mathf.Abs(totalSeconds - 10f) <= 1.5f) feedback += "<color=green>+ Perfect Cut: The video is exactly 10.0 seconds long.</color>\n";
        else { post -= 30f; feedback += $"<color=red>- Timing Error: Target duration is 10.0s. Your cut is {totalSeconds:F1}s.</color>\n"; }

        int logoCount = FindObjectsOfType<BrandingClip>(true).Length;
        if (logoCount == 2) feedback += "<color=green>+ Professional Branding: 2 logos placed in perfect sequence.</color>\n";
        else { post -= 20f; feedback += $"<color=red>- Branding Error: We requested a 2-logo sequence. You placed {logoCount}.</color>\n"; }

        ColorGradingManager grading = FindObjectOfType<ColorGradingManager>(true);
        if (grading != null)
        {
            // Inside ContractGrader.cs -> GradeTutorial()
            // --- Saturation Check ---
            if (grading.saturationSlider.value >= 1.05f) // Lowered from 1.2f
                feedback += "<color=green>+ Natural Saturation: The colors look clean and balanced.</color>\n";
            else
            { post -= 10f; feedback += "<color=yellow>- Flat Colors: Boost Saturation slightly (~1.1) for a professional look.</color>\n"; }

            // --- Contrast Check ---
            if (grading.contrastSlider.value >= 1.1f) // Lowered from 1.15f
                feedback += "<color=green>+ Balanced Contrast: Good separation without crushing shadows.</color>\n";
            else
            { post -= 10f; feedback += "<color=yellow>- Washed Out: Add a tiny bit of Contrast (~1.15).</color>\n"; }

            if (grading.brightnessSlider.value > 1.1f) { post -= 10f; feedback += "<color=red>- Overexposed: Image is blown out! Lower brightness.</color>\n"; }
            else if (grading.brightnessSlider.value >= 0.8f) feedback += "<color=green>+ Controlled Brightness: Perfect exposure on the white petals.</color>\n";
            else { post -= 10f; feedback += "<color=yellow>- Underexposed: Brightness is too low. Keep it neutral (~0.95).</color>\n"; }
        }

        return CompileFinalGrade(pre, prod, post, feedback, 15000);
    }

    private ProductionGrades GradeStage1(float avgCam, float avgLight, float totalSeconds)
    {
        float prod = 100f, post = 100f;
        string feedback = "";

        float pre = 100f;
        if (ProjectDataManager.Instance != null)
        {
            pre = ProjectDataManager.Instance.savedPreProdScore;
            feedback += ProjectDataManager.Instance.savedPreProdFeedback;
        }

        feedback += "<color=white><b>--- PRODUCTION ---</b></color>\n";

        if (avgCam >= 35f) feedback += "<color=green>+ Perfect Composition: Expert use of the Rule of Thirds.</color>\n";
        else { prod -= 30f; feedback += "<color=red>- Poor Composition: Keep the subject aligned on the Thirds grid.</color>\n"; }

        if (avgLight >= 25f) feedback += "<color=green>+ Studio Lighting: Excellent 3-Point Lighting setup.</color>\n";
        else { prod -= 30f; feedback += "<color=yellow>- Flat Lighting: You need a Key, Fill, and Backlight to create depth.</color>\n"; }
        feedback += "\n";

        feedback += "<color=white><b>--- POST-PRODUCTION ---</b></color>\n";

        if (Mathf.Abs(totalSeconds - 10f) <= 1.0f) feedback += "<color=green>+ Perfect Cut: The video is exactly 10.0 seconds long.</color>\n";
        else { post -= 30f; feedback += $"<color=red>- Timing Error: We asked for 10s, you submitted {totalSeconds:F1}s.</color>\n"; }

        int brandingCount = FindObjectsOfType<BrandingClip>(true).Length;
        if (brandingCount == 3) feedback += "<color=green>+ Dynamic Branding: Excellent 3-logo paced sequence.</color>\n";
        else { post -= 30f; feedback += $"<color=red>- Branding Error: The client required 3 logos. You placed {brandingCount}.</color>\n"; }

        ColorGradingManager grading = FindObjectOfType<ColorGradingManager>(true);
        if (grading != null)
        {
            if (grading.contrastSlider.value > 1.7f) { post -= 20f; feedback += "<color=red>- Shadows Crushed: Contrast is too high!</color>\n"; }
            else if (grading.contrastSlider.value >= 1.15f) feedback += "<color=green>+ Cinematic Contrast: Great subject separation.</color>\n";
            else { post -= 20f; feedback += "<color=yellow>- Washed Out: Boost Contrast to give the video depth.</color>\n"; }

            if (grading.saturationSlider.value < 1.2f) { post -= 10f; feedback += "<color=yellow>- Flat Colors: Goke Cola requires vibrant, high-saturation colors.</color>\n"; }
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

        // --- THE FIX: We permanently save their progress when they pass a stage! ---
        if (finalScore >= 60f)
        {
            int currentProgress = PlayerPrefs.GetInt("TutorialProgress", 0);
            if (currentProgress == 0)
            {
                PlayerPrefs.SetInt("TutorialProgress", 1);
            }
            else if (currentProgress == 2)
            {
                PlayerPrefs.SetInt("TutorialProgress", 3);
            }
            PlayerPrefs.Save();
        }

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