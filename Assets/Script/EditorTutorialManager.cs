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
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click Play to preview your raw footage", "- Watch until the video finishes" });
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
                TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- Create a 12-second advertising cut: intro 2s + footage 8s + outro 2s" : "- Make video exactly 10s", isGokeTutorial ? "- Remove dead air, then close the Trim Inspector" : "- Close window when finished" });
                break;

            case EditorStep.PositionVideoAtStart:
                TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- Start the Goke message at 0.0s with no empty opening" : "- Drag the blue video clip left so it starts at 0.0s" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(timelineVideoTrackRect);
                break;

            case EditorStep.GoToBrandingPhase:
                TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- Open Branding to build the visual information hierarchy" : "- Click the 'Branding Phase' tab" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(brandingTabBtnRect); break;

            case EditorStep.DragLogoToScreen:
                TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- Place the Goke Main Logo inside the title-safe guide" : "- Place ECCENTRIC CENTERPIECE first, below the product", isGokeTutorial ? "- Use the shot's negative space and keep the product dominant" : "- Do NOT block the main product!" });
                if (TutorialHighlighter.Instance != null)
                {
                    if (brandingBinClipRect != null && brandingBinClipRect.childCount > 0) TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect.GetChild(0).GetComponent<RectTransform>());
                    else TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect);
                }
                break;

            case EditorStep.TrimBranding:
                TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- Choose when the Goke logo appears in your 12-second cut" : "- Time ECCENTRIC CENTERPIECE from 0.0s to 5.0s" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(brandingTimelineClipRect); break;

            case EditorStep.PlayBrandingPreview:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Press Play", "- Wait until the video finishes playing" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(playButtonRect); break;

            case EditorStep.DragToOtherTimeline:
                TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- Place the Goke End Logo in the opposite safe corner" : "- Place FLORA & FORM HOME second, at the lower right" });
                if (TutorialHighlighter.Instance != null)
                {
                    if (brandingBinClipRect != null && brandingBinClipRect.childCount > 1) TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect.GetChild(1).GetComponent<RectTransform>());
                    else if (brandingBinClipRect != null && brandingBinClipRect.childCount > 0) TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect.GetChild(0).GetComponent<RectTransform>());
                    else TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect);
                }
                break;

            case EditorStep.PositionSecondBranding:
                TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- Choose when the Goke tagline appears in your 12-second cut" : "- Time FLORA & FORM HOME from 5.0s to 10.0s" });
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
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Play the full commercial: watch framing and readable text", "- Listen to the music and check the opening and ending" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(playButtonRect);
                break;

            case EditorStep.PrepareForColorGrade:
                TutorialUIManager.Instance.SetupTasks(new string[] { isGokeTutorial ? "- Open Color Grade for primary color correction" : "- Click 'Color Grade' tab" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();
                break;

            case EditorStep.AdjustBrightness: TutorialUIManager.Instance.SetupTasks(new string[] { "- Try Brightness; pause to inspect the product" }); if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(brightnessSliderRect); brightAdjusted = false; break;
            case EditorStep.AdjustContrast: TutorialUIManager.Instance.SetupTasks(new string[] { "- Try Contrast; pause to inspect light and shadow" }); if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(contrastSliderRect); contAdjusted = false; break;
            case EditorStep.AdjustSaturation: TutorialUIManager.Instance.SetupTasks(new string[] { "- Try Saturation; pause to inspect the colors" }); if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(saturationSliderRect); satAdjusted = false; break;

            case EditorStep.ClickExport: TutorialUIManager.Instance.SetupTasks(new string[] { "- Compare your grade with Before / After", "- Click Export when you are happy with the picture" }); if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(exportButtonRect); exported = false; break;

            case EditorStep.ReviewAndSubmit: TutorialUIManager.Instance.SetupTasks(new string[] { "- Watch your final video", "- Click 'Submit Video'" }); submitted = false; break;
        }
    }

    private IEnumerator TransitionToNextStep(EditorStep nextStep, bool didTaskJustComplete)
    {
        if (isTransitioning) yield break;
        isTransitioning = true;
        tutorialPreviewStarted = false;
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

    private bool tutorialPreviewStarted;
    private EditorStep tutorialPreviewStep;

    public void OnTimelinePlayed()
    {
        if (!isTaskPhaseActive || (currentStep != EditorStep.PlayPreview &&
            currentStep != EditorStep.PlayBrandingPreview && currentStep != EditorStep.PreviewCommercialFinish)) return;
        tutorialPreviewStarted = true;
        tutorialPreviewStep = currentStep;
        TutorialUIManager.Instance.MarkTaskComplete(0);
        if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();
    }

    public void OnPlaybackFinished()
    {
        if (!tutorialPreviewStarted || tutorialPreviewStep != currentStep || !isTaskPhaseActive) return;
        tutorialPreviewStarted = false;
        TutorialUIManager.Instance.MarkTaskComplete(1);
        if (currentStep == EditorStep.PlayPreview)
            StartCoroutine(TransitionToNextStep(EditorStep.DoubleClickToTrim, true));
        else if (currentStep == EditorStep.PlayBrandingPreview)
            StartCoroutine(TransitionToNextStep(EditorStep.DragToOtherTimeline, true));
        else if (currentStep == EditorStep.PreviewCommercialFinish)
            StartCoroutine(TransitionToNextStep(EditorStep.PrepareForColorGrade, true));
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
        spacebarCooldown = Time.unscaledTime + (DevTutorialBypass.FastBossDialogue ? .2f : 1f);
        if (spacePromptText != null) spacePromptText.gameObject.SetActive(false);

        switch (currentStep)
        {
            case EditorStep.ShowPostProductionTitle: if (postProductionTitleCard != null) StartCoroutine(FadeTitleCardSequence(postProductionTitleCard, isGokeTutorial ? EditorStep.ExplainGokePostProduction : EditorStep.ExplainPostProduction)); else StartCoroutine(TransitionToNextStep(isGokeTutorial ? EditorStep.ExplainGokePostProduction : EditorStep.ExplainPostProduction, false)); break;
            case EditorStep.ExplainGokePostProduction: ui.ShowBossDialogue("We've got Goke's footage. This time we'll give the commercial a beginning, a product moment, and an ending. You'll add a 2-second intro and a 2-second outro inside the 12-second cut, then finish the sound and color.", ui.poseOpenHand, false, false); break;
            case EditorStep.ExplainGokePacing: ui.ShowBossDialogue("An ad hasn't got long to catch someone's eye. Let's start the footage at 0 seconds and keep the opening free of dead time.", ui.posePointUp, false, false); break;
            case EditorStep.ExplainGokeVisualHierarchy: ui.ShowBossDialogue("Remember that open space beside the can? That's where our graphics belong. The product should still catch your eye first.", ui.poseOpenHand, false, false); break;
            case EditorStep.ExplainGokeGraphicTiming: ui.ShowBossDialogue("Give each message a moment to land. Show the Main Logo from 0-5 seconds, then the End Logo from 5-10.", ui.poseHappy, false, false); break;
            case EditorStep.ExplainGokeColorSeparation: ui.ShowBossDialogue("We want that Goke red to stand out without losing the can's detail. We'll balance brightness first, then contrast, then saturation.", ui.poseBoss, false, false); break;
            case EditorStep.ExplainPostProduction: ui.ShowBossDialogue("Welcome to the editor! The Clips panel holds your recorded takes. The large Program Monitor shows the picture at the playhead, the red line on the timeline below. The timeline is your commercial arranged from left to right in seconds. We will place a clip, trim it, add messages and sound, then adjust its colors together.", ui.poseHappy, false, false); break;
            case EditorStep.DragVideoToTimeline: ui.ShowBossDialogue("Drag your take from the Clips panel onto the Video Track below. A clip is one piece of footage. The bank keeps your source; the timeline decides which parts the audience sees and in what order.", ui.posePoint, false, false); break;
            case EditorStep.PlayPreview: ui.ShowBossDialogue("Press PLAY to watch the timeline. The red playhead moves through time and the Program Monitor shows that moment. Pause stops there; dragging the playback bar lets you inspect a moment. For this first preview, watch the whole take before we trim it.", ui.poseOpenHand, false, false); break;
            case EditorStep.DoubleClickToTrim: ui.ShowBossDialogue("Let's tidy up the take. Double-click the clip on the timeline to open the Trim Inspector.", ui.posePointUp, false, false); break;

            case EditorStep.TrimLeftHandle: ui.ShowBossDialogue("Try pulling the left pink handle inward. You're choosing where the shot begins, leaving the unwanted opening frames out.", ui.posePoint, false, false); break;
            case EditorStep.TrimRightHandle: ui.ShowBossDialogue("Now pull the right pink handle inward. That decides where we cut away at the end.", ui.posePointUp, false, false); break;
            case EditorStep.TrimTo10Seconds: ui.ShowBossDialogue("The brief calls for 10.0 seconds. Adjust the handles to that length, then click <color=red>[X]</color> to close the inspector.", ui.poseBoss, false, false); break;
            case EditorStep.PositionVideoAtStart: ui.ShowBossDialogue("Slide the trimmed clip left until it starts at 0.0 seconds. We want the picture there the moment the ad begins.", ui.posePoint, false, false); break;

            case EditorStep.GoToBrandingPhase: ui.ShowBossDialogue("Let's put the client's name on this. Click the <color=red>BRANDING</color> tab.", ui.posePoint, false, false); break;

            case EditorStep.ExplainBrandingPhase: ui.ShowBossDialogue("The graphics should help sell the product, without hiding it. Keep them readable and inside the title-safe guide so the edges won't get cut off.", ui.poseOpenHand, false, false); break;
            case EditorStep.DragLogoToScreen: ui.ShowBossDialogue("Start with <color=red>ECCENTRIC CENTERPIECE</color>, the advertising line. Place it below the product, inside title-safe. The brand name comes second.", ui.posePoint, false, false); break;

            case EditorStep.ExplainBrandingTimeline: ui.ShowBossDialogue("See that new pink clip? It decides when your graphic appears and how long it stays.", ui.poseSmile, false, false); break;
            case EditorStep.TrimBranding: ui.ShowBossDialogue("Give this first message five seconds. Drag its handles so it starts at 0.0 and ends at 5.0 seconds.", ui.poseBoss, false, false); break;
            case EditorStep.PlayBrandingPreview: ui.ShowBossDialogue("Hit <color=red>PLAY</color> and check the timing. The first graphic should leave at 5 seconds.", ui.poseOpenHand, false, false); break;
            case EditorStep.DragToOtherTimeline: ui.ShowBossDialogue("Now bring in the second graphic. Find another spot inside title-safe where it won't cover the product.", ui.posePointUp, false, false); break;
            case EditorStep.PositionSecondBranding: ui.ShowBossDialogue("Let the second message take over at 5.0 seconds and end at 10.0. We don't want both talking at once.", ui.poseBoss, false, false); break;

            case EditorStep.ExplainPlayerEditTools: ui.ShowBossDialogue("Now let's shape how the commercial feels. In BRANDING, the four effect buttons control picture movement, text entrances, transitions and music. Click a button to cycle its choices. These choices are used in playback and the final review. We will try each tool, then watch the complete result together.", ui.poseOpenHand, false, false); break;
            case EditorStep.ChooseCameraMotion: ui.ShowBossDialogue("CAMERA MOTION moves or zooms the recorded picture in the edit; it does not move the studio camera. Slow Push In brings attention toward the product and can make a reveal feel important. Pull Out widens the view. Pan Left or Right shifts attention sideways. These effects crop the image, so keep the whole product safely in frame. Click to try a motion; OFF keeps the original framing.", ui.posePoint, false, false); break;
            case EditorStep.ChooseGraphicAnimation: ui.ShowBossDialogue("GRAPHIC ANIMATION changes how your advertising line and brand name enter the picture. Fade appears gently and can suit a calm product. Slide Up guides the eye toward the message. Pop feels lively, but can compete with the product. CUT shows the graphic immediately. Choose an entrance, and leave enough time afterward for someone to read the words.", ui.posePointUp, false, false); break;
            case EditorStep.ChooseTransition: ui.ShowBossDialogue("A TRANSITION controls how pictures begin, end or change. Fade In / Out gives the opening and ending a softer finish. Dip to Black briefly darkens the picture, and can separate clips when there is more than one. Straight Cut changes immediately. Transitions temporarily hide the picture, so check that your short commercial still gives the product and messages enough clear screen time.", ui.poseBoss, false, false); break;
            case EditorStep.ChooseMusic: ui.ShowBossDialogue("MUSIC gives the same pictures a different mood and sense of pace. Clean is lighter and relaxed, Energy has a faster beat, and Cinematic feels slower and more dramatic. Try a soundtrack that fits the product: a gentle flower commercial may feel different with a fast beat. Music does not change clip duration. OFF removes the added music. Listen during our preview, not just to the option's name.", ui.poseHappy, false, false); break;
            case EditorStep.PreviewCommercialFinish: ui.ShowBossDialogue("Press PLAY and watch the whole commercial. Notice whether the motion keeps the product in frame, whether each animated message is easy to read, and whether transitions hide anything important. Listen to how the music changes the mood. These choices will carry into the final review. You can revisit the buttons to change your treatment; stronger effects do not automatically make a better advertisement.", ui.poseOpenHand, false, false); break;

            case EditorStep.PrepareForColorGrade: ui.ShowBossDialogue("One last look at the layout: clear product, readable graphics, safe edges, and one message at a time. Then open <color=red>COLOR GRADE</color>.", ui.poseHappy, false, false); break;

            case EditorStep.ExplainColorGrading: ui.ShowBossDialogue("Color grading changes the look of your recorded picture. We will try brightness, contrast, then saturation. Watch the product in the Program Monitor. 1.00 means unchanged. Every value available on these controls earns full color credit in this first lesson; the controls limit extreme adjustments. Choose your own look and use Before / After to check that petals, shadows and vase edges stay readable.", ui.poseOpenHand, false, false); break;
            case EditorStep.AdjustBrightness: ui.ShowBossDialogue("Brightness makes the whole image lighter or darker. Move the slider right to brighten or left to darken, or type a number in its value box. Try a small change and pause to see it. Keep detail in the petals and shadows: avoid washed-out whites or a product that disappears into darkness. You can reset to 1.00 afterward.", ui.poseHappy, false, false); break;
            case EditorStep.AdjustContrast: ui.ShowBossDialogue("Contrast controls the difference between light and dark areas. More contrast makes the picture punchier, but can hide detail in shadows. Less contrast softens the picture, but too little looks flat. Try this slider and watch the leaves and vase edges. Choose what looks clear to you, then pause to inspect it.", ui.poseSmile, false, false); break;
            case EditorStep.AdjustSaturation: ui.ShowBossDialogue("Saturation controls color strength. Lower it for softer colors; raise it for richer colors. Too much can make the flowers look fluorescent. It does not fix a dark image: use brightness for that. Try a small change and watch the petals and background, then pause to inspect your result.", ui.posePoint, false, false); break;

            case EditorStep.ExplainColorSettings: ui.ShowBossDialogue("You have tried all three controls. Before / After compares your grade with the original picture. Reset returns the controls to 1.00. You can keep the original look if it reads best. Take your time comparing the product, not just the background. When ready, press SPACE for the delivery lesson.", ui.poseHappy, false, false); break;
            case EditorStep.ClickExport: ui.ShowBossDialogue("Export prepares your timeline as the finished commercial. You can still adjust your grade before clicking it. Check that the product is clear, the text is readable, and the colors suit the brief. Click EXPORT when you are satisfied; the next screen lets you watch it before submitting to the client.", ui.poseBoss, false, false); break;

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
