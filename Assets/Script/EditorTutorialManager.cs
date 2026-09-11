using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class EditorTutorialManager : MonoBehaviour
{
    public static EditorTutorialManager Instance;

    public enum EditorStep
    {
        ShowPostProductionTitle, ExplainPostProduction, DragVideoToTimeline, PlayPreview,
        DoubleClickToTrim, TrimLeftHandle, TrimRightHandle, TrimTo10Seconds, CloseTrimWindow,
        PositionVideoAtStart, GoToBrandingPhase, ExplainBrandingPhase, DragLogoToScreen, ExplainBrandingTimeline, TrimBranding,
        PlayBrandingPreview, DragToOtherTimeline, PositionSecondBranding, ExplainPlayerEditTools,
        ChooseCameraMotion, ChooseGraphicAnimation, ChooseTransition, ChooseMusic, PreviewCommercialFinish, PrepareForColorGrade,
        ExplainColorGrading, AdjustBrightness, AdjustContrast, AdjustSaturation, ExplainColorSettings,
        ClickExport, ExplainReviewPanel, ReviewAndSubmit,
        ExplainGokePostProduction, ExplainGokePacing, ExplainGokeVisualHierarchy,
        ExplainGokeGraphicTiming, ExplainGokeColorSeparation, ChooseGokeIntro, ChooseGokeOutro
    }

    public EditorStep currentStep;
    private bool isTransitioning = false;
    public bool isTaskPhaseActive = false;

    public bool isWarningActive = false;
    private bool isTutorialReady = false;
    private bool ownsInstance = false;
    private bool isGokeTutorial = false;

    private const float brandingTimeTolerance = 0.15f;

    [Header("UI References")]
    public TextMeshProUGUI spacePromptText;
    private float spacebarCooldown = 0f;

    private bool leftTrimmed = false, rightTrimmed = false;
    private bool brightAdjusted = false, contAdjusted = false, satAdjusted = false;
    public bool exported = false, submitted = false;

    [Header("Cinematic Title Cards")]
    public CanvasGroup postProductionTitleCard;

    [Header("--- UI Highlight Targets ---")]
    public RectTransform videoBinClipRect;
    public RectTransform playButtonRect;
    public RectTransform timelineVideoTrackRect;
    public RectTransform leftTrimHandleRect;
    public RectTransform rightTrimHandleRect;
    public RectTransform closeTrimWindowBtnRect;

    public RectTransform brandingTabBtnRect;
    public RectTransform brandingBinClipRect;
    public RectTransform previewScreenRect;

    public RectTransform brandingTimelineClipRect;
    public RectTransform otherBrandingTrackRect;

    public RectTransform colorGradeTabBtnRect;
    public RectTransform brightnessSliderRect;
    public RectTransform contrastSliderRect;
    public RectTransform saturationSliderRect;
    public RectTransform exportButtonRect;
    public RectTransform submitButtonRect;

    [Header("UI Components")]
    public ScrollRect timelineScrollRect;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            ownsInstance = true;
            currentStep = EditorStep.ShowPostProductionTitle;
            isTransitioning = true;
            isTaskPhaseActive = false;
            isWarningActive = false;
        }
        else
        {
            // This object can also hold the shared TutorialUIManager.
            Destroy(this);
            return;
        }
    }

    private void Start()
    {
        if (DevTutorialBypass.Disabled) { DisableForDevTesting(); return; }
        CleanupTutorialUI();

        if (!ShouldRunTutorial())
        {
            // Goke's lesson still needs the Boss UI on this same scene object.
            DisableForDevTesting();
            return;
        }

        if (TutorialUIManager.Instance == null)
        {
            Debug.LogWarning("Editor Tutorial cannot start because TutorialUIManager is missing.");
            DisableForDevTesting();
            return;
        }

        if (spacePromptText != null) spacePromptText.gameObject.SetActive(false);

        StartCoroutine(StartTutorialWithDelay());
    }

    public void DisableForDevTesting()
    {
        StopAllCoroutines();
        CleanupTutorialUI();
        enabled = false;
        if (Instance == this) Instance = null;
        Destroy(this);
    }

    private bool ShouldRunTutorial()
    {
        int currentLevel = CampaignProgression.GetCurrentLevel();
        isGokeTutorial = false;
        // Goke uses the focused clip-bank lesson on EditorManager.
        return currentLevel == 1;
    }

    private void CleanupTutorialUI()
    {
        isTutorialReady = false;
        isTaskPhaseActive = false;
        isWarningActive = false;

        if (spacePromptText != null) spacePromptText.gameObject.SetActive(false);

        if (postProductionTitleCard != null)
        {
            postProductionTitleCard.alpha = 0f;
            postProductionTitleCard.gameObject.SetActive(false);
        }

        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.HideTasks();
        }

        if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        bool bossDialogueReady = TutorialUIManager.Instance == null || TutorialUIManager.Instance.CanAdvanceBossDialogue();

        if (spacePromptText != null)
        {
            bool canShowPrompt = isTutorialReady && (!isTaskPhaseActive || isWarningActive) && !isTransitioning && (Time.unscaledTime >= spacebarCooldown) &&
                                 bossDialogueReady && currentStep != EditorStep.ShowPostProductionTitle;
            spacePromptText.gameObject.SetActive(canShowPrompt);
        }

        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame && isTutorialReady && !isTransitioning && currentStep != EditorStep.ShowPostProductionTitle)
        {
            if (Time.unscaledTime >= spacebarCooldown && bossDialogueReady)
            {
                if (isWarningActive)
                {
                    isWarningActive = false;
                    if (isTaskPhaseActive)
                    {
                        StartTaskPhase();
                    }
                    else if (TutorialUIManager.Instance != null)
                    {
                        TutorialUIManager.Instance.HideBossDialogue();
                    }

                    if (UnityEngine.EventSystems.EventSystem.current != null)
                    {
                        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                    }
                }
                else if (!isTaskPhaseActive)
                {
                    AdvanceDialogue();
                }
            }
        }
        if (keyboard != null && keyboard.f8Key.wasPressedThisFrame && isTutorialReady && currentStep != EditorStep.ShowPostProductionTitle)
        {
            CheatCompleteCurrentStep();
        }
    }

    private void CheatCompleteCurrentStep()
    {
        // Don't interrupt if we are already switching steps
        if (isTransitioning) return;

        // If we are just reading dialogue, treat F8 like pressing Space
        if (!isTaskPhaseActive)
        {
            AdvanceDialogue();
            return;
        }

        // Mark up to two possible tasks complete visually in the UI
        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            TutorialUIManager.Instance.MarkTaskComplete(1);
        }

        // Force the transition to the next state based on where we are
        switch (currentStep)
        {
            case EditorStep.DragVideoToTimeline: StartCoroutine(TransitionToNextStep(isGokeTutorial ? EditorStep.ExplainGokePacing : EditorStep.PlayPreview, true)); break;
            case EditorStep.PlayPreview: StartCoroutine(TransitionToNextStep(EditorStep.DoubleClickToTrim, true)); break;
            case EditorStep.DoubleClickToTrim: StartCoroutine(TransitionToNextStep(isGokeTutorial ? EditorStep.TrimTo10Seconds : EditorStep.TrimLeftHandle, true)); break;
            case EditorStep.TrimLeftHandle: leftTrimmed = true; StartCoroutine(TransitionToNextStep(EditorStep.TrimRightHandle, true)); break;
            case EditorStep.TrimRightHandle: rightTrimmed = true; StartCoroutine(TransitionToNextStep(EditorStep.TrimTo10Seconds, true)); break;
            case EditorStep.TrimTo10Seconds: StartCoroutine(TransitionToNextStep(EditorStep.PositionVideoAtStart, true)); break;
            case EditorStep.PositionVideoAtStart: StartCoroutine(TransitionToNextStep(isGokeTutorial ? EditorStep.ExplainGokeVisualHierarchy : EditorStep.GoToBrandingPhase, true)); break;
            case EditorStep.GoToBrandingPhase: StartCoroutine(TransitionToNextStep(isGokeTutorial ? EditorStep.DragLogoToScreen : EditorStep.ExplainBrandingPhase, true)); break;

            case EditorStep.DragLogoToScreen: StartCoroutine(TransitionToNextStep(isGokeTutorial ? EditorStep.ExplainGokeGraphicTiming : EditorStep.ExplainBrandingTimeline, true)); break;
            case EditorStep.TrimBranding: StartCoroutine(TransitionToNextStep(isGokeTutorial ? EditorStep.DragToOtherTimeline : EditorStep.PlayBrandingPreview, true)); break;
            case EditorStep.PlayBrandingPreview: StartCoroutine(TransitionToNextStep(EditorStep.DragToOtherTimeline, true)); break;
            case EditorStep.DragToOtherTimeline: StartCoroutine(TransitionToNextStep(EditorStep.PositionSecondBranding, true)); break;
            case EditorStep.PositionSecondBranding: StartCoroutine(TransitionToNextStep(EditorStep.ExplainPlayerEditTools, true)); break;
            case EditorStep.ChooseCameraMotion:
                if (PlayerEditTools.Instance != null) PlayerEditTools.Instance.selectedCameraMotion = PlayerEditTools.CameraMotionMode.SlowPushIn;
                StartCoroutine(TransitionToNextStep(EditorStep.ChooseGraphicAnimation, true));
                break;
            case EditorStep.ChooseGraphicAnimation:
                if (PlayerEditTools.Instance != null) PlayerEditTools.Instance.selectedGraphicAnimation = PlayerEditTools.GraphicAnimationMode.Fade;
                StartCoroutine(TransitionToNextStep(EditorStep.ChooseTransition, true));
                break;
            case EditorStep.ChooseTransition:
                if (PlayerEditTools.Instance != null) PlayerEditTools.Instance.selectedTransition = PlayerEditTools.TransitionMode.FadeInOut;
                StartCoroutine(TransitionToNextStep(EditorStep.ChooseMusic, true));
                break;
            case EditorStep.ChooseMusic:
                if (PlayerEditTools.Instance != null) PlayerEditTools.Instance.selectedMusic = PlayerEditTools.MusicMode.Clean;
                StartCoroutine(TransitionToNextStep(EditorStep.PreviewCommercialFinish, true));
                break;
            case EditorStep.PreviewCommercialFinish: StartCoroutine(TransitionToNextStep(EditorStep.PrepareForColorGrade, true)); break;
            case EditorStep.PrepareForColorGrade: StartCoroutine(TransitionToNextStep(isGokeTutorial ? EditorStep.ExplainGokeColorSeparation : EditorStep.ExplainColorGrading, true)); break;

            case EditorStep.AdjustBrightness: brightAdjusted = true; StartCoroutine(TransitionToNextStep(EditorStep.AdjustContrast, true)); break;
            case EditorStep.AdjustContrast: contAdjusted = true; StartCoroutine(TransitionToNextStep(EditorStep.AdjustSaturation, true)); break;
            case EditorStep.AdjustSaturation: satAdjusted = true; StartCoroutine(TransitionToNextStep(EditorStep.ExplainColorSettings, true)); break;

            case EditorStep.ClickExport: exported = true; StartCoroutine(TransitionToNextStep(EditorStep.ExplainReviewPanel, true)); break;
            case EditorStep.ReviewAndSubmit:
                submitted = true;
                if (TutorialUIManager.Instance != null) TutorialUIManager.Instance.HideTasks();
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();
                isTaskPhaseActive = false;
                isTutorialReady = false;
                break;
        }
    }
    private IEnumerator StartTutorialWithDelay()
    {
        yield return new WaitForSecondsRealtime(1.0f);
        currentStep = EditorStep.ShowPostProductionTitle;
        isTutorialReady = true;
        isTransitioning = false;
        UpdateBossDialogue();
    }

    private IEnumerator FadeTitleCardSequence(CanvasGroup cg, EditorStep nextStep)
    {
        if (cg == null) yield break;

        isTransitioning = true;
        if (TutorialUIManager.Instance != null) TutorialUIManager.Instance.HideBossDialogue();
        cg.alpha = 0f; cg.gameObject.SetActive(true);
        float speed = 1.5f;
        while (cg.alpha < 1f) { cg.alpha += Time.unscaledDeltaTime * speed; yield return null; }
        cg.alpha = 1f;
        yield return new WaitForSecondsRealtime(2.5f);
        while (cg.alpha > 0f) { cg.alpha -= Time.unscaledDeltaTime * speed; yield return null; }
        cg.alpha = 0f; cg.gameObject.SetActive(false);
        currentStep = nextStep;
        UpdateBossDialogue();
        isTransitioning = false;
    }

    public void AdvanceDialogue()
    {
        if (!isTutorialReady || isTransitioning || currentStep == EditorStep.ShowPostProductionTitle) return;

        if (currentStep == EditorStep.ExplainGokePostProduction) { StartCoroutine(TransitionToNextStep(EditorStep.DragVideoToTimeline, false)); return; }
        if (currentStep == EditorStep.ExplainGokePacing) { StartCoroutine(TransitionToNextStep(EditorStep.DoubleClickToTrim, false)); return; }
        if (currentStep == EditorStep.ExplainGokeVisualHierarchy) { StartCoroutine(TransitionToNextStep(EditorStep.GoToBrandingPhase, false)); return; }
        if (currentStep == EditorStep.ExplainGokeGraphicTiming) { StartCoroutine(TransitionToNextStep(EditorStep.TrimBranding, false)); return; }
        if (currentStep == EditorStep.ExplainGokeColorSeparation) { StartCoroutine(TransitionToNextStep(EditorStep.AdjustBrightness, false)); return; }

        if (currentStep == EditorStep.ExplainPostProduction) { StartCoroutine(TransitionToNextStep(EditorStep.DragVideoToTimeline, false)); return; }
        if (currentStep == EditorStep.ExplainBrandingPhase) { StartCoroutine(TransitionToNextStep(EditorStep.DragLogoToScreen, false)); return; }
        if (currentStep == EditorStep.ExplainBrandingTimeline) { StartCoroutine(TransitionToNextStep(EditorStep.TrimBranding, false)); return; }
        if (currentStep == EditorStep.ExplainPlayerEditTools) { StartCoroutine(TransitionToNextStep(EditorStep.ChooseCameraMotion, false)); return; }

        if (currentStep == EditorStep.ExplainColorGrading) { StartCoroutine(TransitionToNextStep(EditorStep.AdjustBrightness, false)); return; }
        if (currentStep == EditorStep.ExplainColorSettings) { StartCoroutine(TransitionToNextStep(EditorStep.ClickExport, false)); return; }
        if (currentStep == EditorStep.ExplainReviewPanel) { StartCoroutine(TransitionToNextStep(EditorStep.ReviewAndSubmit, false)); return; }

        if (currentStep == EditorStep.PrepareForColorGrade) { StartTaskPhase(); return; }

        StartTaskPhase();
    }

    private void StartTaskPhase()
    {
        if (TutorialUIManager.Instance == null) return;

        TutorialUIManager.Instance.HideBossDialogue();
        isTaskPhaseActive = true;
        isWarningActive = false;
        if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();

        switch (currentStep)
        {
            case EditorStep.DragVideoToTimeline:
                TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- Build the Goke sequence: drag the recorded clip to the Timeline" : "- Drag your recorded clip to the Timeline" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(videoBinClipRect); break;
            case EditorStep.PlayPreview:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click Play to preview your raw footage" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(playButtonRect); break;
            case EditorStep.DoubleClickToTrim:
                TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- Double-click the Goke clip to open the Trim Inspector" : "- Double-Click the video clip on the Timeline to trim it" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(timelineVideoTrackRect); break;

            case EditorStep.TrimLeftHandle:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Drag the Left Handle" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(leftTrimHandleRect); break;
            case EditorStep.TrimRightHandle:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Drag the Right Handle" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(rightTrimHandleRect); break;

            case EditorStep.TrimTo10Seconds:
                TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- Create a precise 10.0-second advertising cut" : "- Make video exactly 10s", isGokeTutorial ? "- Remove dead air, then close the Trim Inspector" : "- Close window when finished" });
                break;

            case EditorStep.PositionVideoAtStart:
                TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- Start the Goke message at 0.0s with no empty opening" : "- Drag the blue video clip left so it starts at 0.0s" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(timelineVideoTrackRect);
                break;

            case EditorStep.GoToBrandingPhase:
                TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- Open Branding to build the visual information hierarchy" : "- Click the 'Branding Phase' tab" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(brandingTabBtnRect); break;

            case EditorStep.DragLogoToScreen:
                TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- Place the Goke Main Logo inside the title-safe guide" : "- Drag 1st logo to the LOWER SIDE of the screen", isGokeTutorial ? "- Use the shot's negative space and keep the product dominant" : "- Do NOT block the main product!" });
                if (TutorialHighlighter.Instance != null)
                {
                    if (brandingBinClipRect != null && brandingBinClipRect.childCount > 0) TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect.GetChild(0).GetComponent<RectTransform>());
                    else TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect);
                }
                break;

            case EditorStep.TrimBranding:
                TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- Time the Main Logo from 0.0s to 5.0s" : "- Trim 1st logo so it starts at 0.0s and ends exactly at 5.0s" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(brandingTimelineClipRect); break;

            case EditorStep.PlayBrandingPreview:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Press Play", "- Wait until the video finishes playing" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(playButtonRect); break;

            case EditorStep.DragToOtherTimeline:
                TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- Place the Goke End Logo in the opposite safe corner" : "- Drag the 2nd logo to the LOWER RIGHT of the screen" });
                if (TutorialHighlighter.Instance != null)
                {
                    if (brandingBinClipRect != null && brandingBinClipRect.childCount > 1) TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect.GetChild(1).GetComponent<RectTransform>());
                    else if (brandingBinClipRect != null && brandingBinClipRect.childCount > 0) TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect.GetChild(0).GetComponent<RectTransform>());
                    else TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect);
                }
                break;

            case EditorStep.PositionSecondBranding:
                TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- Build the end-card beat from 5.0s to 10.0s" : "- Trim the 2nd logo to start at 5.0s and end at 10.0s" });
                if (timelineScrollRect != null) timelineScrollRect.verticalNormalizedPosition = 0f;
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(otherBrandingTrackRect);
                break;

            case EditorStep.ChooseCameraMotion:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click CAMERA MOTION and choose the movement you want" });
                if (TutorialHighlighter.Instance != null && PlayerEditTools.Instance != null) TutorialHighlighter.Instance.HighlightElement(PlayerEditTools.Instance.GetCameraMotionButtonRect());
                break;

            case EditorStep.ChooseGraphicAnimation:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click GRAPHIC ANIMATION and choose how your placed graphics enter" });
                if (TutorialHighlighter.Instance != null && PlayerEditTools.Instance != null) TutorialHighlighter.Instance.HighlightElement(PlayerEditTools.Instance.GetGraphicAnimationButtonRect());
                break;

            case EditorStep.ChooseTransition:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click TRANSITION and choose how the finished commercial opens and closes" });
                if (TutorialHighlighter.Instance != null && PlayerEditTools.Instance != null) TutorialHighlighter.Instance.HighlightElement(PlayerEditTools.Instance.GetTransitionButtonRect());
                break;

            case EditorStep.ChooseMusic:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click MUSIC and choose the soundtrack for your commercial" });
                if (TutorialHighlighter.Instance != null && PlayerEditTools.Instance != null) TutorialHighlighter.Instance.HighlightElement(PlayerEditTools.Instance.GetMusicButtonRect());
                break;

            case EditorStep.PreviewCommercialFinish:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Press Play to preview your finishing choices", "- Watch the complete commercial before color grading" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(playButtonRect);
                break;

            case EditorStep.PrepareForColorGrade:
                TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- Open Color Grade for primary color correction" : "- Click 'Color Grade' tab" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();
                break;

            case EditorStep.AdjustBrightness: TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- CORRECT EXPOSURE: set Brightness to 0.98" : "- Set Brightness to 0.98 to protect highlight detail" }); if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(brightnessSliderRect); brightAdjusted = false; break;
            case EditorStep.AdjustContrast: TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- SHAPE THE IMAGE: set Contrast to 1.20" : "- Set Contrast to 1.12 for controlled separation" }); if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(contrastSliderRect); contAdjusted = false; break;
            case EditorStep.AdjustSaturation: TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- PROTECT BRAND COLOR: set Saturation to 1.10" : "- Set Saturation to 1.08 to protect the brand color" }); if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(saturationSliderRect); satAdjusted = false; break;

            case EditorStep.ClickExport: TutorialUIManager.Instance.SetupTasks(new string[] { "- Click the 'Export' button" }); if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(exportButtonRect); exported = false; break;

            case EditorStep.ReviewAndSubmit: TutorialUIManager.Instance.SetupTasks(new string[] { "- Watch your final video", "- Click 'Submit Video'" }); submitted = false; break;
        }
    }

    private IEnumerator TransitionToNextStep(EditorStep nextStep, bool didTaskJustComplete)
    {
        if (isTransitioning) yield break;
        isTransitioning = true;
        isTaskPhaseActive = false;
        isWarningActive = false;

        if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();
        if (didTaskJustComplete) yield return new WaitForSecondsRealtime(0.5f);
        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.HideTasks();
        }
        yield return new WaitForSecondsRealtime(0.1f);
        currentStep = nextStep;
        UpdateBossDialogue();
        isTransitioning = false;
    }

    public void ShowWarning(string message)
    {
        if (TutorialUIManager.Instance == null) return;

        isWarningActive = true;
        spacebarCooldown = Time.unscaledTime + 0.2f;
        TutorialUIManager.Instance.ShowBossDialogue(message, TutorialUIManager.Instance.poseBoss, false, false);
        if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();

        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public void OnVideoDropped()
    {
        if (currentStep != EditorStep.DragVideoToTimeline || !isTaskPhaseActive) return;

        TutorialUIManager.Instance.MarkTaskComplete(0);
        StartCoroutine(TransitionToNextStep(isGokeTutorial ? EditorStep.ExplainGokePacing : EditorStep.PlayPreview, true));
    }

    public void OnTimelinePlayed()
    {
        if (currentStep == EditorStep.PlayPreview && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(EditorStep.DoubleClickToTrim, true));
        }
        else if (currentStep == EditorStep.PlayBrandingPreview && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();
        }
        else if (currentStep == EditorStep.PreviewCommercialFinish && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();
        }
    }

    public void OnPlaybackFinished()
    {
        if (currentStep == EditorStep.PlayBrandingPreview && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(1);
            StartCoroutine(TransitionToNextStep(EditorStep.DragToOtherTimeline, true));
        }
        else if (currentStep == EditorStep.PreviewCommercialFinish && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(1);
            StartCoroutine(TransitionToNextStep(EditorStep.PrepareForColorGrade, true));
        }
    }

    public void OnVideoDoubleClicked()
    {
        if (currentStep != EditorStep.DoubleClickToTrim || !isTaskPhaseActive) return;

        TutorialUIManager.Instance.MarkTaskComplete(0);
        StartCoroutine(TransitionToNextStep(isGokeTutorial ? EditorStep.TrimTo10Seconds : EditorStep.TrimLeftHandle, true));
    }
    public void OnLeftHandleTrimmed() { if (currentStep == EditorStep.TrimLeftHandle && isTaskPhaseActive && !leftTrimmed) { leftTrimmed = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.TrimRightHandle, true)); } }
    public void OnRightHandleTrimmed() { if (currentStep == EditorStep.TrimRightHandle && isTaskPhaseActive && !rightTrimmed) { rightTrimmed = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.TrimTo10Seconds, true)); } }

    public void OnTrimWindowClosed()
    {
        if ((currentStep == EditorStep.TrimTo10Seconds || currentStep == EditorStep.CloseTrimWindow) && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            TutorialUIManager.Instance.MarkTaskComplete(1);
            StartCoroutine(TransitionToNextStep(EditorStep.PositionVideoAtStart, true));
        }
    }

    public void OnVideoRepositioned()
    {
        if (currentStep == EditorStep.PositionVideoAtStart && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(isGokeTutorial ? EditorStep.ExplainGokeVisualHierarchy : EditorStep.GoToBrandingPhase, true));
        }
    }

    private bool CheckBrandingPlacement()
    {
        return HasValidBrandingClip(0, 0f, 5f) && HasValidBrandingClip(1, 5f, 10f);
    }

    private bool HasValidBrandingClip(int trackIndex, float requiredStart, float requiredEnd)
    {
        EditorManager editorManager = EditorManager.Instance;
        if (editorManager == null || editorManager.brandingTracks == null || trackIndex < 0 || trackIndex >= editorManager.brandingTracks.Length || editorManager.brandingTracks[trackIndex] == null) return false;

        BrandingClip[] allClips = FindObjectsOfType<BrandingClip>();
        foreach (BrandingClip clip in allClips)
        {
            if (IsBrandingClipValid(clip, trackIndex, requiredStart, requiredEnd)) return true;
        }

        return false;
    }

    private bool IsBrandingClipValid(BrandingClip clip, int trackIndex, float requiredStart, float requiredEnd)
    {
        EditorManager editorManager = EditorManager.Instance;
        if (clip == null || clip.linkedOverlay == null || editorManager == null || editorManager.brandingTracks == null) return false;
        if (trackIndex < 0 || trackIndex >= editorManager.brandingTracks.Length || editorManager.brandingTracks[trackIndex] == null) return false;
        if (TapeSettings.framesPerSecond <= 0 || clip.transform.parent != editorManager.brandingTracks[trackIndex]) return false;

        float startSec = (float)clip.linkedOverlay.startFrame / TapeSettings.framesPerSecond;
        float endSec = (float)clip.linkedOverlay.endFrame / TapeSettings.framesPerSecond;

        return Mathf.Abs(startSec - requiredStart) <= brandingTimeTolerance
            && Mathf.Abs(endSec - requiredEnd) <= brandingTimeTolerance
            && clip.linkedOverlay.IsProfessionalPlacement();
    }

    public void OnPhaseChanged(int phaseIndex)
    {
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        if (currentStep == EditorStep.GoToBrandingPhase && isTaskPhaseActive && phaseIndex == 1)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(isGokeTutorial ? EditorStep.DragLogoToScreen : EditorStep.ExplainBrandingPhase, true));
        }

        if (currentStep == EditorStep.PrepareForColorGrade && isTaskPhaseActive && phaseIndex == 2)
        {
            if (CheckBrandingPlacement())
            {
                TutorialUIManager.Instance.HideBossDialogue();
                StartCoroutine(TransitionToNextStep(isGokeTutorial ? EditorStep.ExplainGokeColorSeparation : EditorStep.ExplainColorGrading, true));
            }
            else
            {
                if (EditorManager.Instance != null) EditorManager.Instance.GoToBranding();
                ShowWarning("Check both graphics: keep them inside title-safe at their supplied size. Graphic 1 runs from 0 to 5 seconds, and Graphic 2 from 5 to 10. Adjust them, then try again.");
            }
        }
    }

    public void OnClipDragStarted()
    {
        if (TutorialHighlighter.Instance == null || !isTaskPhaseActive) return;

        if (currentStep == EditorStep.DragVideoToTimeline || currentStep == EditorStep.PositionVideoAtStart)
            TutorialHighlighter.Instance.HighlightElement(timelineVideoTrackRect);
        else if (currentStep == EditorStep.DragLogoToScreen || currentStep == EditorStep.DragToOtherTimeline)
            TutorialHighlighter.Instance.HighlightElement(previewScreenRect);
    }

    public void OnClipDragCancelled()
    {
        if (TutorialHighlighter.Instance == null || !isTaskPhaseActive) return;

        if (currentStep == EditorStep.DragVideoToTimeline)
            TutorialHighlighter.Instance.HighlightElement(videoBinClipRect);
        else if (currentStep == EditorStep.PositionVideoAtStart)
            TutorialHighlighter.Instance.HighlightElement(timelineVideoTrackRect);
        else if (currentStep == EditorStep.DragLogoToScreen)
        {
            if (brandingBinClipRect != null && brandingBinClipRect.childCount > 0) TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect.GetChild(0).GetComponent<RectTransform>());
            else TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect);
        }
        else if (currentStep == EditorStep.DragToOtherTimeline)
        {
            if (brandingBinClipRect != null && brandingBinClipRect.childCount > 1) TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect.GetChild(1).GetComponent<RectTransform>());
            else if (brandingBinClipRect != null && brandingBinClipRect.childCount > 0) TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect.GetChild(0).GetComponent<RectTransform>());
            else TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect);
        }
    }

    public void OnBrandDroppedToScreen()
    {
        if (currentStep == EditorStep.DragLogoToScreen && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            TutorialUIManager.Instance.MarkTaskComplete(1);
            StartCoroutine(TransitionToNextStep(isGokeTutorial ? EditorStep.ExplainGokeGraphicTiming : EditorStep.ExplainBrandingTimeline, true));
        }
        else if (currentStep == EditorStep.DragToOtherTimeline && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(EditorStep.PositionSecondBranding, true));
        }
    }

    public void OnBrandingClipChanged(BrandingClip clip)
    {
        if (!isTaskPhaseActive || isTransitioning || TutorialUIManager.Instance == null) return;

        if (currentStep == EditorStep.TrimBranding)
        {
            bool isValid = clip != null ? IsBrandingClipValid(clip, 0, 0f, 5f) : HasValidBrandingClip(0, 0f, 5f);
            if (!isValid) return;

            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(isGokeTutorial ? EditorStep.DragToOtherTimeline : EditorStep.PlayBrandingPreview, true));
        }
        else if (currentStep == EditorStep.PositionSecondBranding)
        {
            bool isValid = clip != null ? IsBrandingClipValid(clip, 1, 5f, 10f) : HasValidBrandingClip(1, 5f, 10f);
            if (!isValid) return;

            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(EditorStep.ExplainPlayerEditTools, true));
        }
    }

    public void OnBrandTrimmed() { OnBrandingClipChanged(null); }
    public void OnBrandMovedToOtherTrack() { OnBrandingClipChanged(null); }

    public void OnPlayerEditToolChanged()
    {
        if (!isTaskPhaseActive || isTransitioning || TutorialUIManager.Instance == null || PlayerEditTools.Instance == null) return;

        if (currentStep == EditorStep.ChooseCameraMotion && PlayerEditTools.Instance.selectedCameraMotion != PlayerEditTools.CameraMotionMode.None)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(EditorStep.ChooseGraphicAnimation, true));
        }
        else if (currentStep == EditorStep.ChooseGraphicAnimation && PlayerEditTools.Instance.selectedGraphicAnimation != PlayerEditTools.GraphicAnimationMode.Cut)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(EditorStep.ChooseTransition, true));
        }
        else if (currentStep == EditorStep.ChooseTransition && PlayerEditTools.Instance.selectedTransition != PlayerEditTools.TransitionMode.Cut)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(EditorStep.ChooseMusic, true));
        }
        else if (currentStep == EditorStep.ChooseMusic && PlayerEditTools.Instance.selectedMusic != PlayerEditTools.MusicMode.None)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(EditorStep.PreviewCommercialFinish, true));
        }

    }

    public void OnBrightnessAdjusted() { if (currentStep == EditorStep.AdjustBrightness && isTaskPhaseActive && !brightAdjusted) { brightAdjusted = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.AdjustContrast, true)); } }
    public void OnContrastAdjusted() { if (currentStep == EditorStep.AdjustContrast && isTaskPhaseActive && !contAdjusted) { contAdjusted = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.AdjustSaturation, true)); } }

    public void OnSaturationAdjusted()
    {
        if (currentStep == EditorStep.AdjustSaturation && isTaskPhaseActive && !satAdjusted)
        {
            satAdjusted = true;
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(EditorStep.ExplainColorSettings, true));
        }
    }

    public void OnExportClicked()
    {
        if (currentStep == EditorStep.ClickExport && isTaskPhaseActive && !exported)
        {
            exported = true;
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(EditorStep.ExplainReviewPanel, true));
        }
    }

    public void OnVideoSubmitted()
    {
        if (currentStep == EditorStep.ReviewAndSubmit && isTaskPhaseActive && !submitted)
        {
            submitted = true;
            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.MarkTaskComplete(1);
                TutorialUIManager.Instance.HideTasks();
            }
            if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();
            isTaskPhaseActive = false;
            isTutorialReady = false;
            isTransitioning = true;
            if (spacePromptText != null) spacePromptText.gameObject.SetActive(false);
        }
    }

    private void UpdateBossDialogue()
    {
        TutorialUIManager ui = TutorialUIManager.Instance;
        if (ui == null) return;
        isTaskPhaseActive = false;
        isWarningActive = false;
        spacebarCooldown = Time.unscaledTime + 1f;
        if (spacePromptText != null) spacePromptText.gameObject.SetActive(false);

        switch (currentStep)
        {
            case EditorStep.ShowPostProductionTitle: if (postProductionTitleCard != null) StartCoroutine(FadeTitleCardSequence(postProductionTitleCard, isGokeTutorial ? EditorStep.ExplainGokePostProduction : EditorStep.ExplainPostProduction)); else StartCoroutine(TransitionToNextStep(isGokeTutorial ? EditorStep.ExplainGokePostProduction : EditorStep.ExplainPostProduction, false)); break;
            case EditorStep.ExplainGokePostProduction: ui.ShowBossDialogue("We've got Goke's footage. This time we'll give the commercial a beginning, a product moment, and an ending. You'll add a 2-second intro and a 2-second outro inside the 10-second cut, then finish the sound and color.", ui.poseOpenHand, false, false); break;
            case EditorStep.ExplainGokePacing: ui.ShowBossDialogue("An ad hasn't got long to catch someone's eye. Let's start the footage at 0 seconds and keep the opening free of dead time.", ui.posePointUp, false, false); break;
            case EditorStep.ExplainGokeVisualHierarchy: ui.ShowBossDialogue("Remember that open space beside the can? That's where our graphics belong. The product should still catch your eye first.", ui.poseOpenHand, false, false); break;
            case EditorStep.ExplainGokeGraphicTiming: ui.ShowBossDialogue("Give each message a moment to land. Show the Main Logo from 0-5 seconds, then the End Logo from 5-10.", ui.poseHappy, false, false); break;
            case EditorStep.ExplainGokeColorSeparation: ui.ShowBossDialogue("We want that Goke red to stand out without losing the can's detail. We'll balance brightness first, then contrast, then saturation.", ui.poseBoss, false, false); break;
            case EditorStep.ExplainPostProduction: ui.ShowBossDialogue("Here's our footage. Let's turn it into an ad you'd actually want to watch. We'll build the edit together, one choice at a time.", ui.poseHappy, false, false); break;
            case EditorStep.DragVideoToTimeline: ui.ShowBossDialogue("Grab your take from the Clips panel and drag it onto the Video Track below. That's where we'll build the edit.", ui.posePoint, false, false); break;
            case EditorStep.PlayPreview: ui.ShowBossDialogue("Hit <color=red>PLAY</color> and watch the whole take. Get a feel for it before we start cutting.", ui.poseOpenHand, false, false); break;
            case EditorStep.DoubleClickToTrim: ui.ShowBossDialogue("Let's tidy up the take. Double-click the clip on the timeline to open the Trim Inspector.", ui.posePointUp, false, false); break;

            case EditorStep.TrimLeftHandle: ui.ShowBossDialogue("Try pulling the left pink handle inward. You're choosing where the shot begins, leaving the unwanted opening frames out.", ui.posePoint, false, false); break;
            case EditorStep.TrimRightHandle: ui.ShowBossDialogue("Now pull the right pink handle inward. That decides where we cut away at the end.", ui.posePointUp, false, false); break;
            case EditorStep.TrimTo10Seconds: ui.ShowBossDialogue("The brief calls for 10.0 seconds. Adjust the handles to that length, then click <color=red>[X]</color> to close the inspector.", ui.poseBoss, false, false); break;
            case EditorStep.PositionVideoAtStart: ui.ShowBossDialogue("Slide the trimmed clip left until it starts at 0.0 seconds. We want the picture there the moment the ad begins.", ui.posePoint, false, false); break;

            case EditorStep.GoToBrandingPhase: ui.ShowBossDialogue("Let's put the client's name on this. Click the <color=red>BRANDING</color> tab.", ui.posePoint, false, false); break;

            case EditorStep.ExplainBrandingPhase: ui.ShowBossDialogue("The graphics should help sell the product, without hiding it. Keep them readable and inside the title-safe guide so the edges won't get cut off.", ui.poseOpenHand, false, false); break;
            case EditorStep.DragLogoToScreen: ui.ShowBossDialogue("Drag the first graphic into an open part of the preview. Stay inside title-safe and leave the product clear.", ui.posePoint, false, false); break;

            case EditorStep.ExplainBrandingTimeline: ui.ShowBossDialogue("See that new pink clip? It decides when your graphic appears and how long it stays.", ui.poseSmile, false, false); break;
            case EditorStep.TrimBranding: ui.ShowBossDialogue("Give this first message five seconds. Drag its handles so it starts at 0.0 and ends at 5.0 seconds.", ui.poseBoss, false, false); break;
            case EditorStep.PlayBrandingPreview: ui.ShowBossDialogue("Hit <color=red>PLAY</color> and check the timing. The first graphic should leave at 5 seconds.", ui.poseOpenHand, false, false); break;
            case EditorStep.DragToOtherTimeline: ui.ShowBossDialogue("Now bring in the second graphic. Find another spot inside title-safe where it won't cover the product.", ui.posePointUp, false, false); break;
            case EditorStep.PositionSecondBranding: ui.ShowBossDialogue("Let the second message take over at 5.0 seconds and end at 10.0. We don't want both talking at once.", ui.poseBoss, false, false); break;

            case EditorStep.ExplainPlayerEditTools: ui.ShowBossDialogue("Let's give the edit a little personality. These finishing tools are yours to choose; anything left OFF or on CUT stays as it is.", ui.poseOpenHand, false, false); break;
            case EditorStep.ChooseCameraMotion: ui.ShowBossDialogue("What suits your shot? Push In draws us closer, Pull Out reveals the set, and Pan moves our attention sideways. Pick one and see how it feels.", ui.posePoint, false, false); break;
            case EditorStep.ChooseGraphicAnimation: ui.ShowBossDialogue("Choose an entrance for your graphics: Fade, Slide Up, or Pop. Think about how you'd like the client's message to arrive.", ui.posePointUp, false, false); break;
            case EditorStep.ChooseTransition: ui.ShowBossDialogue("How should the ad open and close? Try Fade, Dip to Black, or Straight Cut, then preview your choice.", ui.poseBoss, false, false); break;
            case EditorStep.ChooseMusic: ui.ShowBossDialogue("Let's hear it with music. Try Clean, Energy, or Cinematic and choose what fits the client. MUSIC OFF leaves the soundtrack silent.", ui.poseHappy, false, false); break;
            case EditorStep.PreviewCommercialFinish: ui.ShowBossDialogue("Play it from the top with <color=red>PLAY</color>. Do the picture, graphics, motion, and music feel like they belong together?", ui.poseOpenHand, false, false); break;

            case EditorStep.PrepareForColorGrade: ui.ShowBossDialogue("One last look at the layout: clear product, readable graphics, safe edges, and one message at a time. Then open <color=red>COLOR GRADE</color>.", ui.poseHappy, false, false); break;

            case EditorStep.ExplainColorGrading: ui.ShowBossDialogue("Let's balance the picture. We'll work through brightness, contrast, then saturation. The green markers are a guide; keep an eye on the preview too.", ui.poseOpenHand, false, false); break;
            case EditorStep.AdjustBrightness: ui.ShowBossDialogue("Bring Brightness to 0.98. We're easing back the brightest areas so the product keeps its detail.", ui.poseHappy, false, false); break;
            case EditorStep.AdjustContrast: ui.ShowBossDialogue(isGokeTutorial ? "Bring Contrast to 1.20. Watch how the can's light and dark edges become easier to read against all that red." : "Try Contrast at 1.12. We're giving the flower some shape while keeping detail in its bright and dark areas.", ui.poseSmile, false, false); break;
            case EditorStep.AdjustSaturation: ui.ShowBossDialogue(isGokeTutorial ? "Bring Saturation to 1.10. A little boost gives Goke its red, without letting the color take over the picture." : "Try Saturation at 1.08. Just a little lift to the pink; we still want your eye on the flower.", ui.posePoint, false, false); break;

            case EditorStep.ExplainColorSettings: ui.ShowBossDialogue("Take a look at the difference. We want visible detail, a clear product, and color that supports it. That's the balance we're after.", ui.poseHappy, false, false); break;
            case EditorStep.ClickExport: ui.ShowBossDialogue("Ready to see it all together? Click <color=red>EXPORT</color> to render the commercial you've made.", ui.poseBoss, false, false); break;

            case EditorStep.ExplainReviewPanel: ui.ShowBossDialogue("Here's your final cut. Watch it through once: check the opening, the product, graphic timing, sound, and color. This is our last look before delivery.", ui.poseOpenHand, false, false); break;
            case EditorStep.ReviewAndSubmit: ui.ShowBossDialogue("Happy it matches the brief? Click <color=red>SUBMIT VIDEO</color> and let's see what the client thinks.", ui.poseHappy, false, false); break;
        }
    }

    private void OnDestroy()
    {
        if (ownsInstance && Instance == this)
        {
            StopAllCoroutines();
            CleanupTutorialUI();
            Instance = null;
        }
    }
}
