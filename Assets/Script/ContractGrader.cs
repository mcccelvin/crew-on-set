using UnityEngine;
using System.Collections.Generic;

public class ContractGrader : MonoBehaviour
{
    public enum GameLevel { Level1 = 1, Level2 = 2, Level3 = 3, Level4 = 4, Level5 = 5 }

    public ProductionGrades GenerateGrades(float avgCam, float avgLight, float totalSeconds)
    {
        int currentLevel = CampaignProgression.GetCurrentLevel();
        CrossSceneData.submittedLevel = currentLevel;
        CrossSceneData.resultApplied = false;

        if (currentLevel == 1) return GradeLevel1(avgCam, avgLight, totalSeconds);
        if (currentLevel == 2) return GradeLevel2(avgCam, avgLight, totalSeconds);
        if (currentLevel == 3) return GradeLevel3(avgCam, avgLight, totalSeconds);
        if (currentLevel == 4) return GradeLevel4(avgCam, avgLight, totalSeconds);
        return GradeLevel5(avgCam, avgLight, totalSeconds);
    }

    private ProductionGrades GradeLevel1(float avgCam, float avgLight, float totalSeconds)
    {
        float pre = GetPreProductionScore(out string feedback);
        float prod = GetProductionScore(avgCam, avgLight);
        float post = 100f;

        AddProductionFeedback(GameLevel.Level1, avgCam, avgLight, ref feedback);
        feedback += "<color=white><b>--- POST-PRODUCTION ---</b></color>\n";

        if (Mathf.Abs(totalSeconds - 10f) <= 1.5f) feedback += "<color=green>+ Clean 10-second commercial cut.</color>\n";
        else
        {
            post -= 30f;
            feedback += $"<color=red>- Timing: Target 10.0 seconds. Your cut is {totalSeconds:F1} seconds.</color>\n";
        }

        int logoCount = FindObjectsOfType<BrandingClip>(true).Length;
        if (logoCount == 2) feedback += "<color=green>+ Correct 2-graphic branding sequence.</color>\n";
        else
        {
            post -= 20f;
            feedback += $"<color=red>- Branding: Place 2 graphics. Found {logoCount}.</color>\n";
        }

        ColorGradingManager grading = FindObjectOfType<ColorGradingManager>(true);
        if (grading != null)
        {
            if (grading.saturationSlider.value >= 1.05f && grading.saturationSlider.value <= 1.25f)
                feedback += "<color=green>+ Natural, balanced saturation.</color>\n";
            else
            {
                post -= 10f;
                feedback += "<color=yellow>- Saturation: Keep the product natural around 1.05 to 1.25.</color>\n";
            }

            if (grading.contrastSlider.value >= 1.1f && grading.contrastSlider.value <= 1.4f)
                feedback += "<color=green>+ Controlled product contrast.</color>\n";
            else
            {
                post -= 10f;
                feedback += "<color=yellow>- Contrast: Use about 1.10 to 1.40.</color>\n";
            }

            if (grading.brightnessSlider.value >= 0.8f && grading.brightnessSlider.value <= 1.1f)
                feedback += "<color=green>+ Flower highlights retain detail.</color>\n";
            else
            {
                post -= 10f;
                feedback += "<color=yellow>- Brightness: Keep exposure between 0.80 and 1.10.</color>\n";
            }
        }

        return CompileFinalGrade(pre, prod, post, avgCam, avgLight, feedback, 15000, IsRequiredSetupComplete());
    }

    private ProductionGrades GradeLevel2(float avgCam, float avgLight, float totalSeconds)
    {
        float pre = GetPreProductionScore(out string feedback);
        float prod = GetProductionScore(avgCam, avgLight);
        float post = 100f;

        AddProductionFeedback(GameLevel.Level2, avgCam, avgLight, ref feedback);
        feedback += "<color=white><b>--- POST-PRODUCTION ---</b></color>\n";

        if (Mathf.Abs(totalSeconds - 10f) <= 1f) feedback += "<color=green>+ Precise 10-second commercial cut.</color>\n";
        else
        {
            post -= 30f;
            feedback += $"<color=red>- Timing: Target 10.0 seconds. Your cut is {totalSeconds:F1} seconds.</color>\n";
        }

        int logoCount = FindObjectsOfType<BrandingClip>(true).Length;
        if (logoCount == 3) feedback += "<color=green>+ Correct 3-graphic Goke sequence.</color>\n";
        else
        {
            post -= 25f;
            feedback += $"<color=red>- Branding: Place 3 graphics. Found {logoCount}.</color>\n";
        }

        ColorGradingManager grading = FindObjectOfType<ColorGradingManager>(true);
        if (grading != null)
        {
            if (grading.contrastSlider.value >= 1.15f && grading.contrastSlider.value <= 1.7f)
                feedback += "<color=green>+ Strong commercial contrast.</color>\n";
            else
            {
                post -= 20f;
                feedback += "<color=yellow>- Contrast: Goke needs 1.15 to 1.70.</color>\n";
            }

            if (grading.saturationSlider.value >= 1.2f && grading.saturationSlider.value <= 1.6f)
                feedback += "<color=green>+ Vibrant Goke color.</color>\n";
            else
            {
                post -= 15f;
                feedback += "<color=yellow>- Saturation: Use 1.20 to 1.60 for a vibrant result.</color>\n";
            }

            if (grading.brightnessSlider.value >= 0.85f && grading.brightnessSlider.value <= 1.15f)
                feedback += "<color=green>+ Controlled exposure.</color>\n";
            else
            {
                post -= 10f;
                feedback += "<color=yellow>- Brightness: Keep exposure between 0.85 and 1.15.</color>\n";
            }
        }

        return CompileFinalGrade(pre, prod, post, avgCam, avgLight, feedback, 60000, IsRequiredSetupComplete());
    }

    private ProductionGrades GradeLevel3(float avgCam, float avgLight, float totalSeconds)
    {
        float pre = GetPreProductionScore(out string feedback);
        float prod = GetProductionScore(avgCam, avgLight);
        float post = 100f;

        AddProductionFeedback(GameLevel.Level3, avgCam, avgLight, ref feedback);
        feedback += "<color=white><b>--- POST-PRODUCTION ---</b></color>\n";

        if (Mathf.Abs(totalSeconds - 10f) <= 1.5f) feedback += "<color=green>+ Premium commercial pacing.</color>\n";
        else
        {
            post -= 25f;
            feedback += $"<color=red>- Timing: Target 10.0 seconds. Your cut is {totalSeconds:F1} seconds.</color>\n";
        }

        ColorGradingManager grading = FindObjectOfType<ColorGradingManager>(true);
        if (grading != null)
        {
            if (grading.contrastSlider.value >= 1.15f && grading.contrastSlider.value <= 1.45f)
                feedback += "<color=green>+ Vehicle shape has premium contrast.</color>\n";
            else
            {
                post -= 20f;
                feedback += "<color=yellow>- Contrast: Keep reflective body detail between 1.15 and 1.45.</color>\n";
            }

            if (grading.saturationSlider.value >= 0.95f && grading.saturationSlider.value <= 1.2f)
                feedback += "<color=green>+ Paint color stays refined.</color>\n";
            else
            {
                post -= 15f;
                feedback += "<color=yellow>- Saturation: Use 0.95 to 1.20 for a premium automotive look.</color>\n";
            }

            if (grading.brightnessSlider.value >= 0.9f && grading.brightnessSlider.value <= 1.1f)
                feedback += "<color=green>+ Reflections retain highlight detail.</color>\n";
            else
            {
                post -= 15f;
                feedback += "<color=yellow>- Brightness: Keep reflections between 0.90 and 1.10.</color>\n";
            }
        }

        return CompileFinalGrade(pre, prod, post, avgCam, avgLight, feedback, 80000, IsRequiredSetupComplete());
    }

    private ProductionGrades GradeLevel4(float avgCam, float avgLight, float totalSeconds)
    {
        float pre = GetPreProductionScore(out string feedback);
        float prod = GetProductionScore(avgCam, avgLight);
        float post = 100f;
        List<DraggableClip> clips = GetCampaignClips(4);

        bool hasWideShot = HasShotType(clips, 1);
        bool hasMediumShot = HasShotType(clips, 2);
        bool hasCloseShot = HasShotType(clips, 3);
        bool hasMinimumClips = clips.Count >= 3;
        bool hasShotSequence = hasWideShot && hasMediumShot && hasCloseShot;
        bool hasConsistentDirection = HasConsistentScreenDirection(clips);
        bool hasVisibleSubjects = HaveVisibleRequiredSubjects(clips);
        bool hasActorPose = HasConsistentActorPose(clips);
        bool hasSoftLight = HasSoftLight(clips);
        bool contractRequirementsMet = IsRequiredSetupComplete() && hasMinimumClips && hasShotSequence && hasConsistentDirection && hasVisibleSubjects && hasActorPose && hasSoftLight;

        AddProductionFeedback(GameLevel.Level4, avgCam, avgLight, ref feedback);
        feedback += "<color=white><b>--- POST-PRODUCTION ---</b></color>\n";

        if (Mathf.Abs(totalSeconds - 15f) <= 1.5f) feedback += "<color=green>+ The daily-story commercial meets the 15-second brief.</color>\n";
        else
        {
            post -= 15f;
            feedback += $"<color=red>- Timing: Target 15.0 seconds. Your cut is {totalSeconds:F1} seconds.</color>\n";
        }

        if (hasMinimumClips) feedback += "<color=green>+ At least three separate takes are used.</color>\n";
        else
        {
            post -= 15f;
            feedback += $"<color=red>- Coverage: Use at least 3 clips. Found {clips.Count}.</color>\n";
        }

        if (hasShotSequence) feedback += "<color=green>+ Establishing, medium action, and product close-up shots are present.</color>\n";
        else
        {
            post -= 20f;
            feedback += "<color=red>- Shot coverage: Include one wide, one medium, and one close-up shot.</color>\n";
        }

        if (hasConsistentDirection) feedback += "<color=green>+ Screen direction remains continuous across the action.</color>\n";
        else
        {
            post -= 15f;
            feedback += "<color=red>- Continuity: Keep the actor moving in the same screen direction across matching shots.</color>\n";
        }

        if (hasVisibleSubjects) feedback += "<color=green>+ Required subjects remain visible in every selected shot.</color>\n";
        else
        {
            post -= 15f;
            feedback += "<color=red>- Visibility: The actor or Kape Kultura product is missing or blocked in one or more shots.</color>\n";
        }

        if (hasActorPose) feedback += "<color=green>+ The actor keeps the same non-neutral action pose across the sequence.</color>\n";
        else
        {
            post -= 10f;
            feedback += "<color=red>- Match-on-action: Use the same non-neutral actor pose in every selected shot.</color>\n";
        }

        if (hasSoftLight) feedback += "<color=green>+ The Level 3 Soft Light remains consistent in every selected shot.</color>\n";
        else
        {
            post -= 10f;
            feedback += "<color=red>- Equipment: Use the Level 3 Soft Light in every Kape Kultura clip.</color>\n";
        }

        int logoCount = FindObjectsOfType<BrandingClip>(true).Length;
        if (logoCount == 2) feedback += "<color=green>+ Correct 2-graphic Kape Kultura sequence.</color>\n";
        else
        {
            post -= 15f;
            feedback += $"<color=red>- Branding: Place 2 graphics. Found {logoCount}.</color>\n";
        }

        ColorGradingManager grading = FindObjectOfType<ColorGradingManager>(true);
        if (grading != null)
        {
            GradeColorRange(grading.brightnessSlider.value, 0.95f, 1.15f, 10f, "Brightness", "Keep the café scene warm without losing highlight detail.", ref post, ref feedback);
            GradeColorRange(grading.contrastSlider.value, 1.05f, 1.3f, 10f, "Contrast", "Use 1.05 to 1.30 for a natural daily-story image.", ref post, ref feedback);
            GradeColorRange(grading.saturationSlider.value, 1.05f, 1.3f, 10f, "Saturation", "Use 1.05 to 1.30 for a warm Kape Kultura palette.", ref post, ref feedback);
        }
        else
        {
            post -= 30f;
            feedback += "<color=red>- Color grade data is missing.</color>\n";
        }

        return CompileFinalGrade(pre, prod, post, avgCam, avgLight, feedback, 100000, contractRequirementsMet);
    }

    private ProductionGrades GradeLevel5(float avgCam, float avgLight, float totalSeconds)
    {
        float pre = GetPreProductionScore(out string feedback);
        float prod = GetProductionScore(avgCam, avgLight);
        float post = 100f;
        List<DraggableClip> clips = GetCampaignClips(5);

        bool hasWideShot = HasShotType(clips, 1);
        bool hasMediumShot = HasShotType(clips, 2);
        bool hasCloseShot = HasShotType(clips, 3);
        bool hasMinimumClips = clips.Count >= 4;
        bool hasShotSequence = hasWideShot && hasMediumShot && hasCloseShot;
        bool hasConsistentDirection = HasConsistentScreenDirection(clips);
        bool hasVisibleSubjects = HaveVisibleRequiredSubjects(clips);
        bool hasActorPose = HasActorPose(clips);
        bool hasThreePointRoles = HasThreePointLighting(clips);
        bool contractRequirementsMet = IsRequiredSetupComplete() && hasMinimumClips && hasShotSequence && hasConsistentDirection && hasVisibleSubjects && hasActorPose && hasThreePointRoles;

        AddProductionFeedback(GameLevel.Level5, avgCam, avgLight, ref feedback);
        feedback += "<color=white><b>--- POST-PRODUCTION ---</b></color>\n";

        if (Mathf.Abs(totalSeconds - 20f) <= 1.5f) feedback += "<color=green>+ The final campaign meets the 20-second brief.</color>\n";
        else
        {
            post -= 15f;
            feedback += $"<color=red>- Timing: Target 20.0 seconds. Your cut is {totalSeconds:F1} seconds.</color>\n";
        }

        if (hasMinimumClips) feedback += "<color=green>+ The final edit uses at least four separate takes.</color>\n";
        else
        {
            post -= 15f;
            feedback += $"<color=red>- Coverage: Use at least 4 clips. Found {clips.Count}.</color>\n";
        }

        if (hasShotSequence) feedback += "<color=green>+ The final campaign includes wide, medium, and close-up coverage.</color>\n";
        else
        {
            post -= 20f;
            feedback += "<color=red>- Shot coverage: The final campaign needs wide, medium, and close-up shots.</color>\n";
        }

        if (hasConsistentDirection) feedback += "<color=green>+ Spatial continuity is maintained.</color>\n";
        else
        {
            post -= 15f;
            feedback += "<color=red>- Continuity: Screen direction changes between matching shots.</color>\n";
        }

        if (hasVisibleSubjects) feedback += "<color=green>+ All required campaign subjects remain readable.</color>\n";
        else
        {
            post -= 15f;
            feedback += "<color=red>- Visibility: A required campaign subject is missing or blocked.</color>\n";
        }

        if (hasActorPose) feedback += "<color=green>+ Actor direction supports the campaign.</color>\n";
        else
        {
            post -= 10f;
            feedback += "<color=red>- Performance: Use a deliberate actor pose.</color>\n";
        }

        if (hasThreePointRoles) feedback += "<color=green>+ Distinct Key, Fill, and Back roles are recorded in every selected shot.</color>\n";
        else
        {
            post -= 15f;
            feedback += "<color=red>- Lighting continuity: Every Haraya clip must record distinct, aimed Key, Fill, and Back Light roles.</color>\n";
        }

        int logoCount = FindObjectsOfType<BrandingClip>(true).Length;
        if (logoCount == 3) feedback += "<color=green>+ Correct 3-graphic Haraya branding sequence.</color>\n";
        else
        {
            post -= 15f;
            feedback += $"<color=red>- Branding: Place 3 graphics. Found {logoCount}.</color>\n";
        }

        ColorGradingManager grading = FindObjectOfType<ColorGradingManager>(true);
        if (grading != null)
        {
            GradeColorRange(grading.brightnessSlider.value, 0.95f, 1.1f, 10f, "Brightness", "Keep final exposure between 0.95 and 1.10.", ref post, ref feedback);
            GradeColorRange(grading.contrastSlider.value, 1.1f, 1.4f, 10f, "Contrast", "Use 1.10 to 1.40 for a polished campaign finish.", ref post, ref feedback);
            GradeColorRange(grading.saturationSlider.value, 1f, 1.25f, 10f, "Saturation", "Use 1.00 to 1.25 to protect brand and product colors.", ref post, ref feedback);
        }
        else
        {
            post -= 30f;
            feedback += "<color=red>- Color grade data is missing.</color>\n";
        }

        return CompileFinalGrade(pre, prod, post, avgCam, avgLight, feedback, 150000, contractRequirementsMet);
    }

    private float GetPreProductionScore(out string feedback)
    {
        feedback = "";
        if (ProjectDataManager.Instance == null) return 100f;

        feedback = ProjectDataManager.Instance.savedPreProdFeedback;
        return Mathf.Clamp(ProjectDataManager.Instance.savedPreProdScore, 0f, 100f);
    }

    private float GetProductionScore(float avgCam, float avgLight)
    {
        return Mathf.Clamp(avgCam + avgLight, 0f, 100f);
    }

    private bool IsRequiredSetupComplete()
    {
        return ProjectDataManager.Instance != null && ProjectDataManager.Instance.savedRequiredSetupMet;
    }

    private void AddProductionFeedback(GameLevel level, float avgCam, float avgLight, ref string feedback)
    {
        feedback += "<color=white><b>--- PRODUCTION ---</b></color>\n";
        feedback += $"Camera setup: <b>{avgCam:F1}/70</b> | Lighting setup: <b>{avgLight:F1}/30</b>\n";

        if (avgCam >= 60f) feedback += "<color=green>+ Camera settings and composition were excellent.</color>\n";
        else if (avgCam >= 42f) feedback += "<color=yellow>~ Camera result is usable, but framing and zoom can improve.</color>\n";
        else feedback += "<color=red>- Camera result needs a major framing and zoom adjustment.</color>\n";

        if (avgLight >= 25f) feedback += "<color=green>+ Light placement, aim, and intensity were excellent.</color>\n";
        else if (avgLight >= 16f) feedback += "<color=yellow>~ Lighting is usable, but intensity or aim can improve.</color>\n";
        else feedback += "<color=red>- Lighting needs better placement, aim, and intensity.</color>\n";

        if (level == GameLevel.Level1)
        {
            feedback += "<color=white>Tip: Center the full product, use the zoom to fill the frame, then set the light near 45% with about -5 degrees tilt.</color>\n\n";
        }
        else if (level == GameLevel.Level2)
        {
            feedback += "<color=white>Tip: Place Goke near a grid intersection. Try Key 75%, Fill 40%, Back 60%, and aim every beam at the product.</color>\n\n";
        }
        else if (level == GameLevel.Level3)
        {
            feedback += "<color=white>Tip: Keep the actor and car fully visible with low overlap. Use the Soft Light near 75%, about -10 degrees tilt, aimed at both subjects.</color>\n\n";
        }
        else if (level == GameLevel.Level4)
        {
            feedback += "<color=white>Tip: Record a wide, medium, and close-up while preserving the actor's screen direction and keeping the Kape Kultura product visible.</color>\n\n";
        }
        else
        {
            feedback += "<color=white>Tip: Treat the Haraya campaign as a complete production. Protect subject visibility, continuity, lighting, and shot variety in every take.</color>\n\n";
        }
    }

    private List<DraggableClip> GetCampaignClips(int campaignLevel)
    {
        List<DraggableClip> campaignClips = new List<DraggableClip>();
        DraggableClip[] allClips = FindObjectsOfType<DraggableClip>(true);

        foreach (DraggableClip clip in allClips)
        {
            if (clip != null && clip.isOnTimeline && clip.campaignLevel == campaignLevel)
            {
                campaignClips.Add(clip);
            }
        }

        return campaignClips;
    }

    private bool HasShotType(List<DraggableClip> clips, int shotType)
    {
        foreach (DraggableClip clip in clips)
        {
            if (clip.shotType == shotType) return true;
        }

        return false;
    }

    private bool HasConsistentScreenDirection(List<DraggableClip> clips)
    {
        float expectedDirection = 0f;
        int directedShots = 0;

        foreach (DraggableClip clip in clips)
        {
            if (Mathf.Abs(clip.screenDirection) <= 0.1f) continue;

            float direction = Mathf.Sign(clip.screenDirection);
            if (expectedDirection == 0f) expectedDirection = direction;
            else if (direction != expectedDirection) return false;

            directedShots++;
        }

        return directedShots >= 2;
    }

    private bool HaveVisibleRequiredSubjects(List<DraggableClip> clips)
    {
        if (clips.Count == 0) return false;

        foreach (DraggableClip clip in clips)
        {
            if (!clip.requiredSubjectsVisible) return false;
        }

        return true;
    }

    private bool HasActorPose(List<DraggableClip> clips)
    {
        foreach (DraggableClip clip in clips)
        {
            if (!string.IsNullOrEmpty(clip.actorPose) && clip.actorPose != "Neutral") return true;
        }

        return false;
    }

    private bool HasConsistentActorPose(List<DraggableClip> clips)
    {
        if (clips.Count == 0) return false;

        string expectedPose = "";

        foreach (DraggableClip clip in clips)
        {
            if (string.IsNullOrEmpty(clip.actorPose) || clip.actorPose == "Neutral") return false;

            if (string.IsNullOrEmpty(expectedPose)) expectedPose = clip.actorPose;
            else if (clip.actorPose != expectedPose) return false;
        }

        return true;
    }

    private bool HasSoftLight(List<DraggableClip> clips)
    {
        if (clips.Count == 0) return false;

        foreach (DraggableClip clip in clips)
        {
            if (!clip.usedSoftLight) return false;
        }

        return true;
    }

    private bool HasThreePointLighting(List<DraggableClip> clips)
    {
        if (clips.Count == 0) return false;

        foreach (DraggableClip clip in clips)
        {
            if (!clip.hasThreePointRoles) return false;
        }

        return true;
    }

    private void GradeColorRange(float value, float minimum, float maximum, float deduction, string label, string correction, ref float post, ref string feedback)
    {
        if (value >= minimum && value <= maximum)
        {
            feedback += "<color=green>+ " + label + " supports the requested commercial look.</color>\n";
        }
        else
        {
            post -= deduction;
            feedback += "<color=yellow>- " + label + ": " + correction + "</color>\n";
        }
    }

    private ProductionGrades CompileFinalGrade(float pre, float prod, float post, float avgCam, float avgLight, string feedback, int maxPayout, bool contractRequirementsMet = true)
    {
        pre = Mathf.Clamp(pre, 0f, 100f);
        prod = Mathf.Clamp(prod, 0f, 100f);
        post = Mathf.Clamp(post, 0f, 100f);

        float finalScore = (pre + prod + post) / 3f;
        string letterGrade = "F";
        int payout = 0;

        if (finalScore >= 90f && pre >= 90f && post >= 85f && avgCam >= 60f && avgLight >= 25f)
        {
            letterGrade = "S";
            payout = maxPayout;
        }
        else if (finalScore >= 80f && pre >= 80f && post >= 75f && avgCam >= 50f && avgLight >= 20f)
        {
            letterGrade = "A";
            payout = (int)(maxPayout * 0.8f);
        }
        else if (finalScore >= 70f && pre >= 70f && post >= 65f && avgCam >= 42f && avgLight >= 16f)
        {
            letterGrade = "B";
            payout = (int)(maxPayout * 0.6f);
        }
        else if (finalScore >= 60f && pre >= 60f && post >= 50f && avgCam >= 30f && avgLight >= 8f)
        {
            letterGrade = "C";
            payout = (int)(maxPayout * 0.3f);
        }

        if (!contractRequirementsMet)
        {
            letterGrade = "F";
            payout = 0;
            feedback += "\n<color=red><b>CONTRACT GATE:</b> One or more mandatory qualifications are missing. Complete every required shot, visibility, continuity, performance, and equipment condition before resubmitting.</color>\n";
        }

        if (contractRequirementsMet && finalScore >= 90f && letterGrade != "S")
        {
            feedback += "\n<color=yellow><b>QUALITY GATE:</b> An S rank requires Pre-Production 90, Post-Production 85, Camera 60/70, and Lighting 25/30. Your rank was capped by the weaker department.</color>\n";
        }
        else if (contractRequirementsMet && finalScore >= 80f && letterGrade != "S" && letterGrade != "A")
        {
            feedback += "\n<color=yellow><b>QUALITY GATE:</b> An A rank requires Pre-Production 80, Post-Production 75, Camera 50/70, and Lighting 20/30.</color>\n";
        }
        else if (contractRequirementsMet && finalScore >= 70f && letterGrade != "S" && letterGrade != "A" && letterGrade != "B")
        {
            feedback += "\n<color=yellow><b>QUALITY GATE:</b> A B rank requires Pre-Production 70, Post-Production 65, Camera 42/70, and Lighting 16/30.</color>\n";
        }
        else if (contractRequirementsMet && finalScore >= 60f && letterGrade == "F")
        {
            feedback += "\n<color=red><b>QUALITY GATE:</b> A passing rank requires Pre-Production 60, Post-Production 50, Camera 30/70, and Lighting 8/30. Improve the weakest department and try again.</color>\n";
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
