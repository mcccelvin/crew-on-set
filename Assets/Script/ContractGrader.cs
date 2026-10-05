using UnityEngine;
using System.Collections.Generic;

public class ContractGrader : MonoBehaviour
{
    public enum GameLevel { Level1 = 1, Level2 = 2, Level3 = 3, Level4 = 4, Level5 = 5 }

    public ProductionGrades GenerateGrades(float avgCam, float avgLight, float totalSeconds)
    {
        // Grade the contract that opened this editor, not progression changed by a result callback.
        int currentLevel = EditorManager.Instance != null && EditorManager.Instance.EditingLevel >= 1
            ? EditorManager.Instance.EditingLevel : CampaignProgression.GetCurrentLevel();
        if (EditorManager.Instance == null || !EditorManager.Instance.IsRoomEditor)
        {
            CrossSceneData.submittedLevel = currentLevel;
            CrossSceneData.resultApplied = false;
            CrossSceneData.submittedWithTutorial = false;
            PlayerAnalytics.CaptureScores(currentLevel, avgCam, avgLight);
        }

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

        if (Mathf.Abs(totalSeconds - 10f) <= 0.75f) feedback += "<color=green>+ Precise 10-second commercial cut.</color>\n";
        else
        {
            post -= 30f;
            feedback += $"<color=red>- Timing: Deliver 10.0 seconds within a 0.75-second tolerance. Your cut is {totalSeconds:F1} seconds.</color>\n";
        }

        GradeBrandingQuality(2, "Artisan Flower Vase", ref post, ref feedback);
        GradePlayerCreatedFinish(true, ref post, ref feedback);

        ColorGradingManager grading = FindObjectOfType<ColorGradingManager>(true);
        if (HasColorControls(grading))
        {
            GradeColorRange(grading.brightnessSlider.value, ColorGradingManager.BeginnerBrightnessMin, ColorGradingManager.BeginnerBrightnessMax, 16f, "Exposure", "Use the brightness control's supported range, 0.75 to 1.25. Any value in that range earns full credit.", ref post, ref feedback);
            GradeColorRange(grading.contrastSlider.value, ColorGradingManager.BeginnerContrastMin, ColorGradingManager.BeginnerContrastMax, 16f, "Contrast", "Use the contrast control's supported range, 0.75 to 1.50. Any value in that range earns full credit.", ref post, ref feedback);
            GradeColorRange(grading.saturationSlider.value, ColorGradingManager.BeginnerSaturationMin, ColorGradingManager.BeginnerSaturationMax, 16f, "Saturation", "Use the saturation control's supported range, 0.65 to 1.40. Any value in that range earns full credit.", ref post, ref feedback);
        }
        else
        {
            post -= 36f;
            feedback += "<color=red>- Color grade data is missing.</color>\n";
        }

        return CompileFinalGrade(pre, prod, post, avgCam, avgLight, feedback, ProductionEconomy.CompletionBonus(1), IsRequiredSetupComplete());
    }

    private ProductionGrades GradeLevel2(float avgCam, float avgLight, float totalSeconds)
    {
        float pre = GetPreProductionScore(out string feedback);
        float prod = GetProductionScore(avgCam, avgLight);
        float post = 100f;

        AddProductionFeedback(GameLevel.Level2, avgCam, avgLight, ref feedback);
        feedback += "<color=white><b>--- POST-PRODUCTION ---</b></color>\n";

        if (Mathf.Abs(totalSeconds - GokeSequence.TargetSeconds) <= 0.75f) feedback += "<color=green>+ Precise 12-second commercial cut.</color>\n";
        else
        {
            post -= 30f;
            feedback += $"<color=red>- Timing: Deliver 12.0 seconds within a 0.75-second tolerance. Your cut is {totalSeconds:F1} seconds.</color>\n";
        }

        EditorManager editor = EditorManager.Instance;
        float pps = TimelineManager.Instance != null ? TimelineManager.Instance.pixelsPerSecond : (editor != null ? editor.pixelsPerSecond : 0f);
        var sequence = GokeSequence.Evaluate(editor != null ? editor.timelineContainer : null, pps);
        if (sequence.intro) feedback += "<color=green>+ Intro introduces Goke at the start.</color>\n";
        else { post -= 25f; feedback += "<color=red>- Place the full 2-second GOKE INTRO from CLIPS at 0s.</color>\n"; }
        if (sequence.outro) feedback += "<color=green>+ Outro leaves a clear brand sign-off.</color>\n";
        else { post -= 25f; feedback += "<color=red>- Place the full 2-second GOKE OUTRO after the product footage.</color>\n"; }
        if (sequence.product) feedback += "<color=green>+ Your recorded Goke footage connects the opening and ending.</color>\n";
        else { post -= 25f; feedback += "<color=red>- Put your recorded Goke product footage between the intro and outro.</color>\n"; }
        if (!sequence.continuous) { post -= 15f; feedback += "<color=red>- Join the three sections without gaps or overlaps.</color>\n"; }

        bool hasTwoOverlays = GradeGokeOverlays(totalSeconds, ref post, ref feedback);
        return CompileFinalGrade(pre, prod, post, avgCam, avgLight, feedback, ProductionEconomy.CompletionBonus(2), IsRequiredSetupComplete() && sequence.Complete && hasTwoOverlays);
    }

    private bool GradeGokeOverlays(float totalSeconds, ref float post, ref string feedback)
    {
        // Count only distinct overlays linked to the actual graphics tracks.
        // Export preview clones and graphics still in the bank are not submissions.
        var overlays = new HashSet<DraggableOverlay>();
        EditorManager editor = EditorManager.Instance;
        int finalFrame = Mathf.RoundToInt(totalSeconds * TapeSettings.framesPerSecond);
        if (editor != null && editor.brandingTracks != null)
        {
            foreach (Transform track in editor.brandingTracks)
            {
                if (track == null) continue;
                foreach (BrandingClip clip in track.GetComponentsInChildren<BrandingClip>(true))
                {
                    if (clip == null || !clip.gameObject.activeSelf) continue;
                    DraggableOverlay overlay = clip.linkedOverlay;
                    if (overlay == null || !overlay.isOnTimeline) continue;
                    var image = overlay.GetComponent<UnityEngine.UI.Image>();
                    if (image == null || !image.enabled || image.sprite == null || image.color.a <= 0f) continue;
                    // Any positive appearance in the commercial counts; no minimum
                    // hold, prescribed order, or overlap restriction for Goke.
                    if (Mathf.Min(overlay.endFrame, finalFrame) > Mathf.Max(overlay.startFrame, 0)) overlays.Add(overlay);
                }
            }
        }
        if (overlays.Count == 2)
        {
            feedback += "<color=green>+ Two overlays used. Their timing, duration and arrangement are your creative choice.</color>\n";
            return true;
        }
        post -= 25f;
        feedback += $"<color=red>- Use exactly two overlays during the commercial. Found {overlays.Count}. Choose their timing and duration yourself.</color>\n";
        return false;
    }

    private ProductionGrades GradeLevel3(float avgCam, float avgLight, float totalSeconds)
    {
        float pre = GetPreProductionScore(out string feedback);
        float prod = GetProductionScore(avgCam, avgLight);
        float post = 100f;

        AddProductionFeedback(GameLevel.Level3, avgCam, avgLight, ref feedback);
        feedback += "<color=white><b>--- POST-PRODUCTION ---</b></color>\n";

        if (LamborminiBrief.InRange(totalSeconds, LamborminiBrief.MinimumSeconds, LamborminiBrief.MaximumSeconds)) feedback += "<color=green>+ Premium commercial pacing.</color>\n";
        else
        {
            post -= 25f;
            feedback += $"<color=red>- Timing: Target 25 seconds. Your cut is {totalSeconds:F1} seconds.</color>\n";
        }

        var clips = GetCampaignClips(3);
        var editor = FindObjectOfType<EditorManager>(true);
        clips.RemoveAll(clip => !clip.gameObject.activeSelf || clip.providedRole != ProvidedClipRole.None || clip.endFrame <= clip.startFrame || editor == null || editor.timelineContainer == null || !clip.transform.IsChildOf(editor.timelineContainer));
        float pps = TimelineManager.Instance != null ? TimelineManager.Instance.pixelsPerSecond : (editor != null ? editor.pixelsPerSecond : 0);
        bool requiredTakes = LamborminiBrief.HasRequiredRecordedTakes(editor != null ? editor.timelineContainer : null, pps);
        if (requiredTakes) feedback += "<color=green>+ Terrari intro, three separate recordings and outro form a continuous 25-second commercial.</color>\n";
        else { post -= 35f; feedback += "<color=red>- Build this continuous 25-second sequence: full 2s TERRARI INTRO, three different SD-card recordings (back, side and overall), then full 2s TERRARI OUTRO. Duplicating one take does not count.</color>\n"; }
        bool hasVehicleFootage = clips.Count > 0 && HasSoftLight(clips);
        if (hasVehicleFootage) feedback += "<color=green>+ Recorded vehicle footage uses soft lighting.</color>\n";
        else { post -= 35f; feedback += "<color=red>- Use your Level 3 car footage, recorded with a powered Soft Light aimed at the vehicle.</color>\n"; }
        feedback += "<color=white>Creative finish: use Ctrl movement for a smooth take. Keep overlays title-safe and away from the car; upper-left is usually the clearest space.</color>\n";

        ColorGradingManager grading = FindObjectOfType<ColorGradingManager>(true);
        if (HasColorControls(grading))
        {
            if (LamborminiBrief.InRange(grading.contrastSlider.value, LamborminiBrief.ContrastMin, LamborminiBrief.ContrastMax))
                feedback += "<color=green>+ Vehicle shape has premium contrast.</color>\n";
            else
            {
                post -= 20f;
                feedback += "<color=yellow>- Contrast: Keep reflective body detail between 1.05 and 1.45.</color>\n";
            }

            if (LamborminiBrief.InRange(grading.saturationSlider.value, LamborminiBrief.SaturationMin, LamborminiBrief.SaturationMax))
                feedback += "<color=green>+ Paint color stays refined.</color>\n";
            else
            {
                post -= 15f;
                feedback += "<color=yellow>- Saturation: Use 0.95 to 1.30 for readable orange paint.</color>\n";
            }

            if (LamborminiBrief.InRange(grading.brightnessSlider.value, LamborminiBrief.BrightnessMin, LamborminiBrief.BrightnessMax))
                feedback += "<color=green>+ Reflections retain highlight detail.</color>\n";
            else
            {
                post -= 15f;
                feedback += "<color=yellow>- Brightness: Keep reflections between 0.85 and 1.15.</color>\n";
            }
        }
        else
        {
            post -= 35f;
            feedback += "<color=red>- Color grade data is missing.</color>\n";
        }

        return CompileFinalGrade(pre, prod, post, avgCam, avgLight, feedback, ProductionEconomy.CompletionBonus(3), IsRequiredSetupComplete() && hasVehicleFootage && requiredTakes);
    }

    private ProductionGrades GradeLevel4(float avgCam, float avgLight, float totalSeconds)
    {
        float pre = GetPreProductionScore(out string feedback);
        float prod = GetProductionScore(avgCam, avgLight);
        var editor = EditorManager.Instance;
        var story = CoffeeStoryRules.Evaluate(editor != null ? editor.timelineContainer : null,
            editor != null ? editor.pixelsPerSecond : 40f);
        float post = 100f;
        AddProductionFeedback(GameLevel.Level4, avgCam, avgLight, ref feedback);
        feedback += "<color=white><b>--- POST-PRODUCTION ---</b></color>\nCOFFEE COMMERCIAL CHECKLIST\n";
        if (!story.beats)
        {
            post -= 40f;
            if (!story.overview) feedback += "<color=red>- REQUIRED: Add a valid Level 4 Product Overview take lasting at least 2s, with the complete cup and packaging visible throughout the original recording.</color>\n";
            if (!story.coffeeUse) feedback += "<color=red>- REQUIRED: Add a valid Level 4 Coffee Use take lasting at least 2s. Record the full actor and cup in the coffee shop while drinking (Action) or Using Machine. A wave or seated rest does not count.</color>\n";
            if (story.overview && story.coffeeUse) feedback += "<color=red>- REQUIRED ORDER: Move the Product Overview before the Coffee Use take.</color>\n";
            if (story.invalidEvidence > 0) feedback += "Retake mixed/incomplete recordings: set the action before recording and keep all required subjects visible until recording stops. Trimming does not repair the original take's saved evidence.\n";
        }
        else feedback += "+ Product Overview appears before Coffee Use; both required takes have valid evidence.\n";
        if (!story.continuous)
        {
            post -= 20f;
            feedback += "<color=red>- REQUIRED: Start the first clip at 0s and join all clips without gaps or overlaps. Keep trims inside the original source recording.</color>\n";
            if (story.shortSegments > 0) feedback += $"<color=red>- {story.shortSegments} segment(s) are below {CoffeeStoryRules.MinimumSegmentSeconds:0}s. Lengthen, remove or undo these cuts; the minimum applies to EVERY segment, including split pieces.</color>\n";
        }
        else feedback += "+ Continuous timeline from 0s; every segment is at least 2s.\n";
        if (!story.duration) { post -= 20f; feedback += $"<color=red>- REQUIRED LENGTH: {CoffeeStoryRules.MinimumSeconds:0}-{CoffeeStoryRules.MaximumSeconds:0}s. Your timeline ends at {story.seconds:F2}s.</color>\n"; }
        else feedback += $"+ Final duration {story.seconds:F2}s meets the 30-45s brief.\n";
        if (!story.brand) { post -= 20f; feedback += "<color=red>- REQUIRED: Remove every logo, tagline and other overlay from the timeline for this contract.</color>\n"; }
        else feedback += "+ No overlays on the timeline.\n";
        feedback += "Shot sizes, set color, light model, music, transitions and color grade are creative choices, not repeated checklist requirements.\n";
        bool complete = IsRequiredSetupComplete() && story.beats && story.continuous && story.duration && story.brand;
        return CompileFinalGrade(pre, prod, post, avgCam, avgLight, feedback, ProductionEconomy.CompletionBonus(4), complete);
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

        if (hasMinimumClips) feedback += "<color=green>+ The final edit contains at least four clips. Review their source variety and narrative purpose yourself.</color>\n";
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

        if (hasConsistentDirection) feedback += "<color=green>+ Recorded screen-direction checks passed. Review spatial continuity across the actual cuts as well.</color>\n";
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
        if (HasColorControls(grading))
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

        return CompileFinalGrade(pre, prod, post, avgCam, avgLight, feedback, ProductionEconomy.CompletionBonus(5), contractRequirementsMet);
    }

    private float GetPreProductionScore(out string feedback)
    {
        feedback = "";
        if (EditorManager.Instance != null && EditorManager.Instance.IsRoomEditor)
            return MultiplayerContractManager.PreProduction(MultiplayerRoleManager.Instance.State, out feedback);
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
        if (EditorManager.Instance != null && EditorManager.Instance.IsRoomEditor)
        {
            MultiplayerContractManager.PreProduction(MultiplayerRoleManager.Instance.State, out _, out bool complete);
            return complete;
        }
        return ProjectDataManager.Instance != null && ProjectDataManager.Instance.savedRequiredSetupMet;
    }

    private void AddProductionFeedback(GameLevel level, float avgCam, float avgLight, ref string feedback)
    {
        feedback += "<color=white><b>--- PRODUCTION ---</b></color>\n";
        feedback += $"Camera setup: <b>{avgCam:F1}/70</b> | Lighting setup: <b>{avgLight:F1}/30</b>\n";

        if (avgCam >= 60f) feedback += "<color=green>+ Camera setup scored strongly against the game's checks. Review the image's visual hierarchy and narrative purpose yourself.</color>\n";
        else if (avgCam >= 42f) feedback += "<color=yellow>~ Camera setup has room to improve. Compare subject framing, camera position and zoom in the recorded takes.</color>\n";
        else feedback += "<color=red>- Low camera setup score: review framing, zoom and required subject visibility before recording again.</color>\n";

        if (avgLight >= 25f) feedback += "<color=green>+ Lighting setup scored strongly against the game's checks. Inspect the finished image's contrast, texture and separation yourself.</color>\n";
        else if (avgLight >= 16f) feedback += "<color=yellow>~ Lighting setup has room to improve. Compare placement, aim and intensity in the recorded takes.</color>\n";
        else feedback += "<color=red>- Low lighting setup score: check power, placement, aim and intensity before recording again.</color>\n";

        if (level == GameLevel.Level1)
        {
            feedback += "<color=white>Tip: Center the full product, use the zoom to fill the frame, then set the light near 45% with about -5 degrees tilt.</color>\n\n";
        }
        else if (level == GameLevel.Level2)
        {
            feedback += "<color=white>Tip: Keep the whole subject near a thirds intersection and leave space for branding. One powered, aimed light is enough for Goke; three-point lighting is a Level 3 lesson.</color>\n\n";
        }
        else if (level == GameLevel.Level3)
        {
            feedback += "<color=white>Tip: Show the back, side and overall view of the car. Lighting rewards Better-Light aim, output, temperature and diffusion (80%), plus distinct Key, Fill and Back roles (20%). Practice percentages are examples, not mandatory settings.</color>\n\n";
        }
        else if (level == GameLevel.Level4)
        {
            feedback += "<color=white>Tip: Frame the full cup and packaging for Product Overview; frame the full actor and cup for Coffee Use. Leave room around frame edges. Shot-size variety and three-point lighting are optional for this contract.</color>\n\n";
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

    private void GradeBrandingQuality(int requiredGraphics, string clientName, ref float post, ref string feedback)
    {
        BrandingClip[] allBrandingClips = FindObjectsOfType<BrandingClip>(true);
        List<BrandingClip> timelineBranding = new List<BrandingClip>();

        foreach (BrandingClip brandingClip in allBrandingClips)
        {
            if (brandingClip != null && brandingClip.linkedOverlay != null && brandingClip.linkedOverlay.isOnTimeline)
            {
                timelineBranding.Add(brandingClip);
            }
        }

        if (timelineBranding.Count == requiredGraphics)
        {
            feedback += "<color=green>+ Correct " + requiredGraphics + "-graphic " + clientName + " branding sequence.</color>\n";
        }
        else
        {
            post -= 25f;
            feedback += "<color=red>- Branding count: Place exactly " + requiredGraphics + " graphics. Found " + timelineBranding.Count + ".</color>\n";
        }

        bool allGraphicsProfessional = timelineBranding.Count > 0;

        foreach (BrandingClip brandingClip in timelineBranding)
        {
            string issue = brandingClip.linkedOverlay.GetPlacementIssue();
            if (issue != null)
            {
                allGraphicsProfessional = false;
                feedback += "<color=yellow>- " + brandingClip.linkedOverlay.name.Replace("Logo_", "") + ": " + issue + "</color>\n";
            }
        }

        if (allGraphicsProfessional)
        {
            feedback += "<color=green>+ Graphics are readable, title-safe, and held long enough to read.</color>\n";
        }
        else
        {
            post -= 20f;
            feedback += "<color=yellow>- Graphic layout deduction: correct the specific issue listed above.</color>\n";
        }
    }

    private void GradePlayerCreatedFinish(bool requiresGraphicAnimation, ref float post, ref string feedback)
    {
        PlayerEditTools tools = PlayerEditTools.Instance;
        if (tools == null)
        {
            post -= requiresGraphicAnimation ? 48f : 32f;
            feedback += "<color=red>- Editorial finish data is missing. Use the player-controlled tools in Branding before export.</color>\n";
            return;
        }

        if (tools.selectedCameraMotion != PlayerEditTools.CameraMotionMode.None)
        {
            feedback += "<color=green>+ You selected a deliberate camera movement for the final edit.</color>\n";
        }
        else
        {
            post -= 16f;
            feedback += "<color=yellow>- Camera motion: Choose a motivated Push In or Pan in the Branding tools.</color>\n";
        }

        if (tools.selectedMusic != PlayerEditTools.MusicMode.None)
        {
            feedback += "<color=green>+ You selected a soundtrack to support the commercial's pacing.</color>\n";
        }
        else
        {
            post -= 16f;
            feedback += "<color=yellow>- Soundtrack: Select music in the Branding tools instead of submitting silent footage.</color>\n";
        }

        if (tools.selectedTransition != PlayerEditTools.TransitionMode.Cut)
        {
            feedback += "<color=green>+ You selected an opening and closing transition for a finished delivery.</color>\n";
        }
        else
        {
            post -= 12f;
            feedback += "<color=yellow>- Transition: Choose Fade In / Out or Dip To Black before export.</color>\n";
        }

        if (!requiresGraphicAnimation) return;

        if (tools.selectedGraphicAnimation != PlayerEditTools.GraphicAnimationMode.Cut)
        {
            feedback += "<color=green>+ Your placed graphics use a player-selected entrance animation.</color>\n";
        }
        else
        {
            post -= 16f;
            feedback += "<color=yellow>- Graphic animation: Choose Fade or Slide Up for the graphics you placed.</color>\n";
        }
    }

    private void GradeColorRange(float value, float minimum, float maximum, float deduction, string label, string correction, ref float post, ref string feedback)
    {
        if (value >= minimum && value <= maximum)
        {
            feedback += $"<color=green>+ {label}: {value:F2}, within this brief's {minimum:F2}-{maximum:F2} range. Review the actual image before finalizing the look.</color>\n";
        }
        else
        {
            post -= deduction;
            feedback += $"<color=yellow>- {label}: {value:F2}, outside this brief's {minimum:F2}-{maximum:F2} range. {correction}</color>\n";
        }
    }

    private bool HasColorControls(ColorGradingManager grading)
    {
        return grading != null && grading.brightnessSlider != null && grading.contrastSlider != null && grading.saturationSlider != null;
    }

    public static string QualityRank(float pre, float post, float camera, float light, float overall)
    {
        if (overall >= 90 && pre >= 90 && post >= 85 && camera >= 60 && light >= 25) return "S";
        if (overall >= 80 && pre >= 80 && post >= 75 && camera >= 50 && light >= 20) return "A";
        if (overall >= 70 && pre >= 70 && post >= 65 && camera >= 42 && light >= 16) return "B";
        if (overall >= 60 && pre >= 60 && post >= 50 && camera >= 30 && light >= 8) return "C";
        return "F";
    }

    private ProductionGrades CompileFinalGrade(float pre, float prod, float post, float avgCam, float avgLight, string feedback, int maxPayout, bool contractRequirementsMet = true)
    {
        pre = Mathf.Clamp(pre, 0f, 100f);
        prod = Mathf.Clamp(prod, 0f, 100f);
        post = Mathf.Clamp(post, 0f, 100f);

        // Keep all three departments explicit in the detailed feedback as well as the summary.
        feedback = feedback.Replace("--- PRE-PRODUCTION ---", $"1. PRE-PRODUCTION — {pre:F1}/100")
            .Replace("--- PRODUCTION ---", $"2. PRODUCTION — {prod:F1}/100")
            .Replace("--- POST-PRODUCTION ---", $"3. POST-PRODUCTION — {post:F1}/100");

        float finalScore = (pre + prod + post) / 3f;
        string letterGrade = QualityRank(pre, post, avgCam, avgLight, finalScore);
        int payout = letterGrade == "S" ? maxPayout : letterGrade == "A" ? (int)(maxPayout * .8f) :
            letterGrade == "B" ? (int)(maxPayout * .6f) : letterGrade == "C" ? (int)(maxPayout * .3f) : 0;

        if (!contractRequirementsMet)
        {
            letterGrade = "F";
            payout = 0;
            feedback += "\n<color=red><b>CONTRACT GATE:</b> Complete the missing requirements listed above before resubmitting.</color>\n";
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
