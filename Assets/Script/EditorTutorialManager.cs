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
        PlayBrandingPreview, DragToOtherTimeline, PositionSecondBranding, PrepareForColorGrade,
        ExplainColorGrading, AdjustBrightness, AdjustContrast, AdjustSaturation, ExplainColorSettings,
        ClickExport, ExplainReviewPanel, ReviewAndSubmit
    }

    public EditorStep currentStep;
    private bool isTransitioning = false;
    public bool isTaskPhaseActive = false;

    public bool isWarningActive = false;

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
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (TutorialUIManager.Instance != null) TutorialUIManager.Instance.HideBossDialogue();
        if (PlayerPrefs.GetInt("Level1RetryActive", 0) == 1)
        {
            if (TutorialUIManager.Instance != null && TutorialUIManager.Instance.taskPanel != null) TutorialUIManager.Instance.taskPanel.SetActive(false);
            if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();
            Destroy(gameObject);
            return;
        }

        if (PlayerPrefs.GetInt("TutorialProgress", 0) >= 2) { Destroy(gameObject); return; }

        if (spacePromptText != null) spacePromptText.gameObject.SetActive(false);

        StartCoroutine(StartTutorialWithDelay());
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (spacePromptText != null)
        {
            bool canShowPrompt = (!isTaskPhaseActive || isWarningActive) && !isTransitioning && (Time.time >= spacebarCooldown) &&
                                 currentStep != EditorStep.ShowPostProductionTitle;
            spacePromptText.gameObject.SetActive(canShowPrompt);
        }

        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame && !isTransitioning)
        {
            if (Time.time >= spacebarCooldown)
            {
                if (isWarningActive)
                {
                    isWarningActive = false;
                    TutorialUIManager.Instance.HideBossDialogue();

                    if (TutorialUIManager.Instance.taskPanel != null) TutorialUIManager.Instance.taskPanel.SetActive(true);

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
        if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
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
            case EditorStep.DragVideoToTimeline: StartCoroutine(TransitionToNextStep(EditorStep.PlayPreview, true)); break;
            case EditorStep.PlayPreview: StartCoroutine(TransitionToNextStep(EditorStep.DoubleClickToTrim, true)); break;
            case EditorStep.DoubleClickToTrim: StartCoroutine(TransitionToNextStep(EditorStep.TrimLeftHandle, true)); break;
            case EditorStep.TrimLeftHandle: leftTrimmed = true; StartCoroutine(TransitionToNextStep(EditorStep.TrimRightHandle, true)); break;
            case EditorStep.TrimRightHandle: rightTrimmed = true; StartCoroutine(TransitionToNextStep(EditorStep.TrimTo10Seconds, true)); break;
            case EditorStep.TrimTo10Seconds: StartCoroutine(TransitionToNextStep(EditorStep.PositionVideoAtStart, true)); break;
            case EditorStep.PositionVideoAtStart: StartCoroutine(TransitionToNextStep(EditorStep.GoToBrandingPhase, true)); break;
            case EditorStep.GoToBrandingPhase: StartCoroutine(TransitionToNextStep(EditorStep.ExplainBrandingPhase, true)); break;

            case EditorStep.DragLogoToScreen: StartCoroutine(TransitionToNextStep(EditorStep.ExplainBrandingTimeline, true)); break;
            case EditorStep.TrimBranding: StartCoroutine(TransitionToNextStep(EditorStep.PlayBrandingPreview, true)); break;
            case EditorStep.PlayBrandingPreview: StartCoroutine(TransitionToNextStep(EditorStep.DragToOtherTimeline, true)); break;
            case EditorStep.DragToOtherTimeline: StartCoroutine(TransitionToNextStep(EditorStep.PositionSecondBranding, true)); break;
            case EditorStep.PositionSecondBranding: StartCoroutine(TransitionToNextStep(EditorStep.PrepareForColorGrade, true)); break;
            case EditorStep.PrepareForColorGrade: StartCoroutine(TransitionToNextStep(EditorStep.ExplainColorGrading, true)); break;

            case EditorStep.AdjustBrightness: brightAdjusted = true; StartCoroutine(TransitionToNextStep(EditorStep.AdjustContrast, true)); break;
            case EditorStep.AdjustContrast: contAdjusted = true; StartCoroutine(TransitionToNextStep(EditorStep.AdjustSaturation, true)); break;
            case EditorStep.AdjustSaturation: satAdjusted = true; StartCoroutine(TransitionToNextStep(EditorStep.ExplainColorSettings, true)); break;

            case EditorStep.ClickExport: exported = true; StartCoroutine(TransitionToNextStep(EditorStep.ExplainReviewPanel, true)); break;
            case EditorStep.ReviewAndSubmit:
                submitted = true;
                if (TutorialUIManager.Instance.taskPanel != null) TutorialUIManager.Instance.taskPanel.SetActive(false);
                isTaskPhaseActive = false;
                break;
        }
    }
    private IEnumerator StartTutorialWithDelay() { yield return new WaitForSeconds(1.0f); currentStep = EditorStep.ShowPostProductionTitle; UpdateBossDialogue(); }

    private IEnumerator FadeTitleCardSequence(CanvasGroup cg, EditorStep nextStep)
    {
        isTransitioning = true;
        TutorialUIManager.Instance.HideBossDialogue();
        cg.alpha = 0f; cg.gameObject.SetActive(true);
        float speed = 1.5f;
        while (cg.alpha < 1f) { cg.alpha += Time.deltaTime * speed; yield return null; }
        cg.alpha = 1f;
        yield return new WaitForSeconds(2.5f);
        while (cg.alpha > 0f) { cg.alpha -= Time.deltaTime * speed; yield return null; }
        cg.alpha = 0f; cg.gameObject.SetActive(false);
        isTransitioning = false;
        currentStep = nextStep;
        UpdateBossDialogue();
    }

    public void AdvanceDialogue()
    {
        if (isTransitioning) return;

        if (currentStep == EditorStep.ExplainPostProduction) { StartCoroutine(TransitionToNextStep(EditorStep.DragVideoToTimeline, false)); return; }
        if (currentStep == EditorStep.ExplainBrandingPhase) { StartCoroutine(TransitionToNextStep(EditorStep.DragLogoToScreen, false)); return; }
        if (currentStep == EditorStep.ExplainBrandingTimeline) { StartCoroutine(TransitionToNextStep(EditorStep.TrimBranding, false)); return; }

        if (currentStep == EditorStep.ExplainColorGrading) { StartCoroutine(TransitionToNextStep(EditorStep.AdjustBrightness, false)); return; }
        if (currentStep == EditorStep.ExplainColorSettings) { StartCoroutine(TransitionToNextStep(EditorStep.ClickExport, false)); return; }
        if (currentStep == EditorStep.ExplainReviewPanel) { StartCoroutine(TransitionToNextStep(EditorStep.ReviewAndSubmit, false)); return; }

        if (currentStep == EditorStep.PrepareForColorGrade) { StartTaskPhase(); return; }

        StartTaskPhase();
    }

    private void StartTaskPhase()
    {
        TutorialUIManager.Instance.HideBossDialogue();
        isTaskPhaseActive = true;
        if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();

        switch (currentStep)
        {
            case EditorStep.DragVideoToTimeline:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Drag your recorded clip to the Timeline" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(videoBinClipRect); break;
            case EditorStep.PlayPreview:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click Play to preview your raw footage" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(playButtonRect); break;
            case EditorStep.DoubleClickToTrim:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Double-Click the video clip on the Timeline to trim it" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(timelineVideoTrackRect); break;

            case EditorStep.TrimLeftHandle:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Drag the Left Handle" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(leftTrimHandleRect); break;
            case EditorStep.TrimRightHandle:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Drag the Right Handle" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(rightTrimHandleRect); break;

            case EditorStep.TrimTo10Seconds:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Make video exactly 10s", "- Close window when finished" });
                break;

            case EditorStep.PositionVideoAtStart:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Drag the blue video clip left so it starts at 0.0s" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(timelineVideoTrackRect);
                break;

            case EditorStep.GoToBrandingPhase:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click the 'Branding Phase' tab" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(brandingTabBtnRect); break;

            case EditorStep.DragLogoToScreen:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Drag 1st logo to the LOWER SIDE of the screen", "- Do NOT block the main product!" });
                if (TutorialHighlighter.Instance != null)
                {
                    if (brandingBinClipRect != null && brandingBinClipRect.childCount > 0) TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect.GetChild(0).GetComponent<RectTransform>());
                    else TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect);
                }
                break;

            case EditorStep.TrimBranding:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Trim 1st logo so it starts at 0.0s and ends exactly at 5.0s" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(brandingTimelineClipRect); break;

            case EditorStep.PlayBrandingPreview:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Press Play", "- Wait until the video finishes playing" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(playButtonRect); break;

            case EditorStep.DragToOtherTimeline:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Drag the 2nd logo to the LOWER RIGHT of the screen" });
                if (TutorialHighlighter.Instance != null)
                {
                    if (brandingBinClipRect != null && brandingBinClipRect.childCount > 1) TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect.GetChild(1).GetComponent<RectTransform>());
                    else if (brandingBinClipRect != null && brandingBinClipRect.childCount > 0) TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect.GetChild(0).GetComponent<RectTransform>());
                    else TutorialHighlighter.Instance.HighlightElement(brandingBinClipRect);
                }
                break;

            case EditorStep.PositionSecondBranding:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Trim the 2nd logo to start at 5.0s and end at 10.0s" });
                if (timelineScrollRect != null) timelineScrollRect.verticalNormalizedPosition = 0f;
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(otherBrandingTrackRect);
                break;

            case EditorStep.PrepareForColorGrade:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click 'Color Grade' tab" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();
                break;

            case EditorStep.AdjustBrightness: TutorialUIManager.Instance.SetupTasks(new string[] { "- Set Brightness to exactly 0.95" }); if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(brightnessSliderRect); brightAdjusted = false; break;
            case EditorStep.AdjustContrast: TutorialUIManager.Instance.SetupTasks(new string[] { "- Set Contrast to exactly 1.15" }); if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(contrastSliderRect); contAdjusted = false; break;
            case EditorStep.AdjustSaturation: TutorialUIManager.Instance.SetupTasks(new string[] { "- Set Saturation to exactly 1.10" }); if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(saturationSliderRect); satAdjusted = false; break;

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
        if (didTaskJustComplete) yield return new WaitForSeconds(0.5f);
        TutorialUIManager.Instance.HideBossDialogue();
        if (TutorialUIManager.Instance.taskPanel != null) TutorialUIManager.Instance.taskPanel.SetActive(false);
        yield return new WaitForSeconds(0.1f);
        currentStep = nextStep;
        UpdateBossDialogue();
        isTransitioning = false;
    }

    public void ShowWarning(string message)
    {
        isWarningActive = true;
        spacebarCooldown = Time.time + 0.2f;
        TutorialUIManager.Instance.ShowBossDialogue(message, TutorialUIManager.Instance.poseBoss, false, false);

        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public void OnVideoDropped() { if (currentStep == EditorStep.DragVideoToTimeline && isTaskPhaseActive) { TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.PlayPreview, true)); } }

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
    }

    public void OnPlaybackFinished()
    {
        if (currentStep == EditorStep.PlayBrandingPreview && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(1);
            StartCoroutine(TransitionToNextStep(EditorStep.DragToOtherTimeline, true));
        }
    }

    public void OnVideoDoubleClicked() { if (currentStep == EditorStep.DoubleClickToTrim && isTaskPhaseActive) { TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.TrimLeftHandle, true)); } }
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
            StartCoroutine(TransitionToNextStep(EditorStep.GoToBrandingPhase, true));
        }
    }

    private bool CheckBrandingPlacement()
    {
        bool branding1Valid = false;
        bool branding2Valid = false;

        BrandingClip[] allClips = FindObjectsOfType<BrandingClip>();
        foreach (BrandingClip clip in allClips)
        {
            if (clip.linkedOverlay != null)
            {
                float startSec = clip.linkedOverlay.startFrame / TapeSettings.framesPerSecond;
                float endSec = clip.linkedOverlay.endFrame / TapeSettings.framesPerSecond;

                if (EditorManager.Instance != null && clip.transform.parent == EditorManager.Instance.brandingTracks[0])
                {
                    if (startSec <= 0.5f && endSec >= 4.5f && endSec <= 5.5f) branding1Valid = true;
                }
                else if (EditorManager.Instance != null && EditorManager.Instance.brandingTracks.Length > 1 && clip.transform.parent == EditorManager.Instance.brandingTracks[1])
                {
                    if (startSec >= 4.5f && startSec <= 5.5f && endSec >= 9.0f) branding2Valid = true;
                }
            }
        }

        return branding1Valid && branding2Valid;
    }

    public void OnPhaseChanged(int phaseIndex)
    {
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        if (currentStep == EditorStep.GoToBrandingPhase && isTaskPhaseActive && phaseIndex == 1) { TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.ExplainBrandingPhase, true)); }

        if (currentStep == EditorStep.PrepareForColorGrade && isTaskPhaseActive && phaseIndex == 2)
        {
            if (CheckBrandingPlacement())
            {
                TutorialUIManager.Instance.HideBossDialogue();
                StartCoroutine(TransitionToNextStep(EditorStep.ExplainColorGrading, true));
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
            StartCoroutine(TransitionToNextStep(EditorStep.ExplainBrandingTimeline, true));
        }
        else if (currentStep == EditorStep.DragToOtherTimeline && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(EditorStep.PositionSecondBranding, true));
        }
    }

    public void OnBrandTrimmed()
    {
        if (currentStep == EditorStep.TrimBranding && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(EditorStep.PlayBrandingPreview, true));
        }
        else if (currentStep == EditorStep.PositionSecondBranding && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(EditorStep.PrepareForColorGrade, true));
        }
    }

    public void OnBrandMovedToOtherTrack() { }

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

    public void OnVideoSubmitted() { if (currentStep == EditorStep.ReviewAndSubmit && isTaskPhaseActive && !submitted) { submitted = true; TutorialUIManager.Instance.MarkTaskComplete(1); if (TutorialUIManager.Instance.taskPanel != null) TutorialUIManager.Instance.taskPanel.SetActive(false); isTaskPhaseActive = false; } }

    private void UpdateBossDialogue()
    {
        TutorialUIManager ui = TutorialUIManager.Instance;
        if (ui == null) return;
        spacebarCooldown = Time.time + 1f;
        if (spacePromptText != null) spacePromptText.gameObject.SetActive(false);

        switch (currentStep)
        {
            case EditorStep.ShowPostProductionTitle: if (postProductionTitleCard != null) StartCoroutine(FadeTitleCardSequence(postProductionTitleCard, EditorStep.ExplainPostProduction)); else StartCoroutine(TransitionToNextStep(EditorStep.ExplainPostProduction, false)); break;
            case EditorStep.ExplainPostProduction: ui.ShowBossDialogue("Welcome to Post-Production. This is where we craft the story. All the raw footage you recorded is sitting right here in your media bin.", ui.poseHappy, false, false); break;
            case EditorStep.DragVideoToTimeline: ui.ShowBossDialogue("Let's build our sequence. Drag your clip from the bin down onto the Video Track in the timeline.", ui.posePoint, false, false); break;
            case EditorStep.PlayPreview: ui.ShowBossDialogue("Excellent. Before we cut, we review. Hit the Play button to see how your raw footage looks on the big screen.", ui.poseOpenHand, false, false); break;
            case EditorStep.DoubleClickToTrim: ui.ShowBossDialogue("It looks okay, but we need to tighten it up. Double-click the video clip on the timeline to open the Trim Inspector.", ui.posePointUp, false, false); break;

            case EditorStep.TrimLeftHandle: ui.ShowBossDialogue("See the pink handle on the left? Drag it inward to cut out the beginning of the clip.", ui.posePoint, false, false); break;
            case EditorStep.TrimRightHandle: ui.ShowBossDialogue("Now see the handle on the right? Drag it inward to cut the end of the clip.", ui.posePointUp, false, false); break;
            case EditorStep.TrimTo10Seconds: ui.ShowBossDialogue("The client wants a punchy ad. Try to make it exactly 10 seconds long, then press the 'X' button to close the window.", ui.poseBoss, false, false); break;
            case EditorStep.PositionVideoAtStart: ui.ShowBossDialogue("Since you trimmed the beginning, there's a gap! Drag the video clip in the timeline all the way to the left so it starts exactly at 0 seconds.", ui.posePoint, false, false); break;

            case EditorStep.GoToBrandingPhase: ui.ShowBossDialogue("Now we need to add the company's logos. Click the Branding Phase tab to open your graphics bin.", ui.posePoint, false, false); break;

            case EditorStep.ExplainBrandingPhase: ui.ShowBossDialogue("Welcome to the Branding Phase! Branding is what turns a regular video into a real commercial. We overlay logos and text to make the project official.", ui.poseOpenHand, false, false); break;
            case EditorStep.DragLogoToScreen: ui.ShowBossDialogue("Try it out. Drag the first branding logo from the bin directly into the lower area of the video preview screen. Make sure it's not blocking the product!", ui.posePoint, false, false); break;

            case EditorStep.ExplainBrandingTimeline: ui.ShowBossDialogue("Great! Notice how a pink clip just appeared in your Branding Timeline below? That represents your logo's lifespan on screen.", ui.poseSmile, false, false); break;
            case EditorStep.TrimBranding: ui.ShowBossDialogue("Just like the video, you can adjust when the logo appears. Drag the handles on the pink clip so it starts exactly at 0.0s and ends exactly at 5.0s.", ui.poseBoss, false, false); break;
            case EditorStep.PlayBrandingPreview: ui.ShowBossDialogue("Let's see how that looks. Hit Play and watch the screen. The logo should disappear right at the 5-second mark! (The playhead will reset to 0 when it finishes).", ui.poseOpenHand, false, false); break;
            case EditorStep.DragToOtherTimeline: ui.ShowBossDialogue("Now we need a second graphic. Drag the next logo from the bin to the lower right corner of the screen.", ui.posePointUp, false, false); break;
            case EditorStep.PositionSecondBranding: ui.ShowBossDialogue("Now, adjust the new pink clip on the second timeline track. Make sure it starts exactly at 5 seconds and ends at 10 seconds.", ui.poseBoss, false, false); break;

            case EditorStep.PrepareForColorGrade: ui.ShowBossDialogue("Take your time organizing your branding. When you are completely ready and the branding is set properly, click the Color Grade phase.", ui.poseHappy, false, false); break;

            case EditorStep.ExplainColorGrading: ui.ShowBossDialogue("Color Grading is where we set the mood of the commercial. We can completely change how the footage feels with these three sliders.", ui.poseOpenHand, false, false); break;
            case EditorStep.AdjustBrightness: ui.ShowBossDialogue("First, drop the Brightness to exactly 0.95. The slider will lock in place when you find the perfect spot.", ui.poseHappy, false, false); break;
            case EditorStep.AdjustContrast: ui.ShowBossDialogue("Next, push the Contrast up to exactly 1.15 to deepen the shadows and make the subject stand out.", ui.poseSmile, false, false); break;
            case EditorStep.AdjustSaturation: ui.ShowBossDialogue("Finally, a simple color pop. Set the Saturation to exactly 1.10 to make those colors vibrant.", ui.posePoint, false, false); break;

            case EditorStep.ExplainColorSettings: ui.ShowBossDialogue("See what those did? The lower brightness created a moody vibe, the high contrast sharpened the image, and the extra saturation made the product pop! It's a completely different video now.", ui.poseHappy, false, false); break;
            case EditorStep.ClickExport: ui.ShowBossDialogue("We are officially done! Hit the Export button when you are ready to render the final commercial.", ui.poseBoss, false, false); break;

            case EditorStep.ExplainReviewPanel: ui.ShowBossDialogue("This is the Review Panel. Here you can watch your final rendered commercial to make sure everything looks perfect before we send it out.", ui.poseOpenHand, false, false); break;
            case EditorStep.ReviewAndSubmit: ui.ShowBossDialogue("If you are happy with your work, hit the Submit Video button to complete the contract and get paid!", ui.poseHappy, false, false); break;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
