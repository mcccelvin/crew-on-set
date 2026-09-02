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
        ExplainGokeGraphicTiming, ExplainGokeColorSeparation
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
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        CleanupTutorialUI();

        if (!ShouldRunTutorial())
        {
            Destroy(gameObject);
            return;
        }

        if (TutorialUIManager.Instance == null)
        {
            Debug.LogWarning("Editor Tutorial cannot start because TutorialUIManager is missing.");
            Destroy(gameObject);
            return;
        }

        if (spacePromptText != null) spacePromptText.gameObject.SetActive(false);

        StartCoroutine(StartTutorialWithDelay());
    }

    private bool ShouldRunTutorial()
    {
        int currentLevel = CampaignProgression.GetCurrentLevel();
        isGokeTutorial = currentLevel == 2;

        // The Editor lesson belongs to Levels 1 and 2. Contract-grade flags are
        // progression data, not proof that this Editor scene already taught the
        // player. This also keeps the lesson available when testing with cheats
        // or replaying a failed contract, without reviving it in Level 3+.
        return currentLevel == 1 || isGokeTutorial;
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

        if (spacePromptText != null)
        {
            bool canShowPrompt = isTutorialReady && (!isTaskPhaseActive || isWarningActive) && !isTransitioning && (Time.unscaledTime >= spacebarCooldown) &&
                                 currentStep != EditorStep.ShowPostProductionTitle;
            spacePromptText.gameObject.SetActive(canShowPrompt);
        }

        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame && isTutorialReady && !isTransitioning && currentStep != EditorStep.ShowPostProductionTitle)
        {
            if (Time.unscaledTime >= spacebarCooldown)
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

        return Mathf.Abs(startSec - requiredStart) <= brandingTimeTolerance && Mathf.Abs(endSec - requiredEnd) <= brandingTimeTolerance;
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
                ShowWarning("Make sure Branding 1 is set exactly from 0 to 5 seconds, and Branding 2 is set exactly from 5 to 10 seconds!");
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
            case EditorStep.ExplainGokePostProduction: ui.ShowBossDialogue("Level 1 taught you the editor controls. For Goke, every edit must have a purpose: control the pacing, guide the viewer's attention, and protect the brand message.", ui.poseOpenHand, false, false); break;
            case EditorStep.ExplainGokePacing: ui.ShowBossDialogue("First, think about editorial pacing: how much information the audience receives over time. A 10-second commercial cannot waste a second. Remove dead air, begin the message immediately, and give each graphic its own readable beat.", ui.posePointUp, false, false); break;
            case EditorStep.ExplainGokeVisualHierarchy: ui.ShowBossDialogue("Your Rule of Thirds framing created negative space around the product. In advertising design, we can turn that into information space. The product stays first in the visual hierarchy, while the graphics support it from a title-safe area.", ui.poseOpenHand, false, false); break;
            case EditorStep.ExplainGokeGraphicTiming: ui.ShowBossDialogue("Do not show every message at once. Sequence the information: Main Logo from 0 to 5 seconds, then End Logo from 5 to 10 seconds. That creates rhythm and prevents the graphics from competing with each other.", ui.poseHappy, false, false); break;
            case EditorStep.ExplainGokeColorSeparation: ui.ShowBossDialogue("Now perform primary color correction in the professional order: exposure first, contrast second, saturation last. The red product can merge into the red set, so preserve its highlights and shape before strengthening the brand color.", ui.poseBoss, false, false); break;
            case EditorStep.ExplainPostProduction: ui.ShowBossDialogue("Welcome to Post-Production. This is where we craft the story. All the raw footage you recorded is sitting right here in your media bin.", ui.poseHappy, false, false); break;
            case EditorStep.DragVideoToTimeline: ui.ShowBossDialogue(isGokeTutorial ? "Start the Goke edit by moving your recorded take from the media bin to the Video Track." : "Let's build our sequence. Drag your clip from the bin down onto the Video Track in the timeline.", ui.posePoint, false, false); break;
            case EditorStep.PlayPreview: ui.ShowBossDialogue("Excellent. Before we cut, we review. Hit the Play button to see how your raw footage looks on the big screen.", ui.poseOpenHand, false, false); break;
            case EditorStep.DoubleClickToTrim: ui.ShowBossDialogue(isGokeTutorial ? "Open the Trim Inspector. Your job is not simply to shorten the clip; it is to remove dead air and create a precise 10-second delivery cut." : "It looks okay, but we need to tighten it up. Double-click the video clip on the timeline to open the Trim Inspector.", ui.posePointUp, false, false); break;

            case EditorStep.TrimLeftHandle: ui.ShowBossDialogue("See the pink handle on the left? Drag it inward to cut out the beginning of the clip.", ui.posePoint, false, false); break;
            case EditorStep.TrimRightHandle: ui.ShowBossDialogue("Now see the handle on the right? Drag it inward to cut the end of the clip.", ui.posePointUp, false, false); break;
            case EditorStep.TrimTo10Seconds: ui.ShowBossDialogue(isGokeTutorial ? "Deliver exactly 10.0 seconds. Cut away the unusable beginning or ending, then close the Trim Inspector when the duration is correct." : "The client wants a punchy ad. Try to make it exactly 10 seconds long, then press the 'X' button to close the window.", ui.poseBoss, false, false); break;
            case EditorStep.PositionVideoAtStart: ui.ShowBossDialogue(isGokeTutorial ? "A blank opening weakens an advertisement. Move the finished clip to 0.0 seconds so the first frame begins the message immediately." : "Since you trimmed the beginning, there's a gap! Drag the video clip in the timeline all the way to the left so it starts exactly at 0 seconds.", ui.posePoint, false, false); break;

            case EditorStep.GoToBrandingPhase: ui.ShowBossDialogue(isGokeTutorial ? "Open Branding. We will use the shot's negative space as information space without covering the product." : "Now we need to add the company's logos. Click the Branding Phase tab to open your graphics bin.", ui.posePoint, false, false); break;

            case EditorStep.ExplainBrandingPhase: ui.ShowBossDialogue("Welcome to the Branding Phase! Branding is what turns a regular video into a real commercial. We overlay logos and text to make the project official.", ui.poseOpenHand, false, false); break;
            case EditorStep.DragLogoToScreen: ui.ShowBossDialogue(isGokeTutorial ? "Place the Goke Main Logo in the available negative space. Keep it inside the title-safe guide and make sure the can remains the first thing the audience notices." : "Try it out. Drag the first branding logo from the bin directly into the lower area of the video preview screen. Make sure it's not blocking the product!", ui.posePoint, false, false); break;

            case EditorStep.ExplainBrandingTimeline: ui.ShowBossDialogue("Great! Notice how a pink clip just appeared in your Branding Timeline below? That represents your logo's lifespan on screen.", ui.poseSmile, false, false); break;
            case EditorStep.TrimBranding: ui.ShowBossDialogue(isGokeTutorial ? "Set the Main Logo from 0.0 to 5.0 seconds. Five seconds gives the audience time to identify the brand without leaving one static graphic across the whole commercial." : "Just like the video, you can adjust when the logo appears. Drag the handles on the pink clip so it starts exactly at 0.0s and ends exactly at 5.0s.", ui.poseBoss, false, false); break;
            case EditorStep.PlayBrandingPreview: ui.ShowBossDialogue("Let's see how that looks. Hit Play and watch the screen. The logo should disappear right at the 5-second mark! (The playhead will reset to 0 when it finishes).", ui.poseOpenHand, false, false); break;
            case EditorStep.DragToOtherTimeline: ui.ShowBossDialogue(isGokeTutorial ? "Now place the Goke End Logo in the opposite title-safe corner. Rebalancing the frame keeps the second message distinct while preserving the product silhouette." : "Now we need a second graphic. Drag the next logo from the bin to the lower right corner of the screen.", ui.posePointUp, false, false); break;
            case EditorStep.PositionSecondBranding: ui.ShowBossDialogue(isGokeTutorial ? "Time the End Logo from 5.0 to 10.0 seconds. This clean handoff creates two readable information beats across the commercial." : "Now, adjust the new pink clip on the second timeline track. Make sure it starts exactly at 5 seconds and ends at 10 seconds.", ui.poseBoss, false, false); break;

            case EditorStep.ExplainPlayerEditTools: ui.ShowBossDialogue("The editor will not create polish for you. These are your finishing tools: you choose the camera movement, animate the graphics you placed, choose the opening and closing transition, and select the music. OFF or CUT means that effect is not added.", ui.poseOpenHand, false, false); break;
            case EditorStep.ChooseCameraMotion: ui.ShowBossDialogue("Choose a motivated camera move. A Slow Push In increases product emphasis, a Slow Pull Out reveals the set, and a Pan guides the eye toward intentional negative space. Preview your choice with Play.", ui.posePoint, false, false); break;
            case EditorStep.ChooseGraphicAnimation: ui.ShowBossDialogue("Now choose how the graphics you placed enter the frame. Fade is restrained, Slide Up adds direction, and Pop creates a stronger product-ad accent. Only graphics you placed and timed will animate.", ui.posePointUp, false, false); break;
            case EditorStep.ChooseTransition: ui.ShowBossDialogue("Choose the commercial's transition. Fade In / Out gives a polished beginning and ending. Dip To Black also separates multiple edited shots. A Straight Cut leaves the footage unchanged.", ui.poseBoss, false, false); break;
            case EditorStep.ChooseMusic: ui.ShowBossDialogue("Choose the soundtrack according to the client: Clean for a simple product spot, Energy for a fast commercial, or Cinematic for a premium mood. Leaving MUSIC OFF produces no soundtrack.", ui.poseHappy, false, false); break;
            case EditorStep.PreviewCommercialFinish: ui.ShowBossDialogue("Now preview the complete edit. Watch how your camera move, graphic entrance, transition, and music work together. A commercial should build one clear rhythm, so finish the playback before moving to color grading.", ui.poseOpenHand, false, false); break;

            case EditorStep.PrepareForColorGrade: ui.ShowBossDialogue(isGokeTutorial ? "Check the hierarchy: product first, branding second, no unsafe edges, and no overlapping messages. When that reads clearly, open Color Grade." : "Take your time organizing your branding. When you are completely ready and the branding is set properly, click the Color Grade phase.", ui.poseHappy, false, false); break;

            case EditorStep.ExplainColorGrading: ui.ShowBossDialogue("Color grading starts with primary correction: first protect exposure, then shape contrast, and only then adjust saturation. The green markers and the Commercial Look monitor show the client-safe range, but you still judge the image in the Program Monitor.", ui.poseOpenHand, false, false); break;
            case EditorStep.AdjustBrightness: ui.ShowBossDialogue(isGokeTutorial ? "Correct exposure first. Set Brightness to 0.98 so the can's light areas keep detail instead of clipping." : "Start with exposure. Set Brightness to 0.98. This keeps the white petals detailed instead of clipping them into a flat white shape.", ui.poseHappy, false, false); break;
            case EditorStep.AdjustContrast: ui.ShowBossDialogue(isGokeTutorial ? "Next, shape the image. Set Contrast to 1.20 so the can separates from the red backdrop while the darker tones retain form." : "Now set Contrast to 1.12. That separates the flower from the pink set while preserving texture in both shadows and highlights.", ui.poseSmile, false, false); break;
            case EditorStep.AdjustSaturation: ui.ShowBossDialogue(isGokeTutorial ? "Adjust color last. Set Saturation to 1.10. This strengthens Goke's red identity without turning the backdrop into a distraction." : "Finish with Saturation at 1.08. A small increase supports the brand palette; too much would make the pink background compete with the product.", ui.posePoint, false, false); break;

            case EditorStep.ExplainColorSettings: ui.ShowBossDialogue(isGokeTutorial ? "That is primary correction with advertising intent: protected highlights, stronger product separation, and controlled brand color. Before export, quality control means checking timing, hierarchy, safe margins, and color as one complete message." : "That is a controlled commercial grade: detailed highlights, readable shape, and believable brand color. Notice that a polished image comes from balance, not from pushing every slider as high as possible.", ui.poseHappy, false, false); break;
            case EditorStep.ClickExport: ui.ShowBossDialogue(isGokeTutorial ? "Your Goke master is ready. Export the commercial, then review the rendered result instead of assuming the timeline is correct." : "We are officially done! Hit the Export button when you are ready to render the final commercial.", ui.poseBoss, false, false); break;

            case EditorStep.ExplainReviewPanel: ui.ShowBossDialogue(isGokeTutorial ? "Perform a final quality-control pass. Confirm the commercial starts immediately, both graphics are readable, the product remains dominant, and the red tones still retain detail." : "This is the Review Panel. Here you can watch your final rendered commercial to make sure everything looks perfect before we send it out.", ui.poseOpenHand, false, false); break;
            case EditorStep.ReviewAndSubmit: ui.ShowBossDialogue(isGokeTutorial ? "If the final render communicates the Goke message clearly, submit it for contract grading." : "If you are happy with your work, hit the Submit Video button to complete the contract and get paid!", ui.poseHappy, false, false); break;
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
