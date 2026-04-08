using System.Collections;
using UnityEngine;

public class EditorTutorialManager : MonoBehaviour
{
    public static EditorTutorialManager Instance;

    public enum EditorStep
    {
        Intro,
        DragVideoToTimeline, ClickVideoToTrim, TrimLeftHandle, TrimRightHandle,
        CloseTrimWindow, // NEW STEP
        GoToBrandingPhase, DragBrandingToTrack, TrimBranding,
        PreviewTimeline, // NEW STEP
        GoToColorGradePhase, AdjustBrightness, AdjustContrast, AdjustSaturation,
        ToggleFadeIn, // NEW STEP
        ClickExport,
        ReviewAndSubmit // NEW STEP
    }

    public EditorStep currentStep;
    private bool isTransitioning = false;
    private bool isTaskPhaseActive = false;

    // Trackers
    private bool videoDropped = false, trimOpened = false, leftTrimmed = false, rightTrimmed = false, trimClosed = false;
    private bool brandingPhaseReached = false, brandDropped = false, brandTrimmed = false, timelinePlayed = false;
    private bool colorPhaseReached = false, brightAdjusted = false, contAdjusted = false, satAdjusted = false, fadeInToggled = false;
    public bool exported = false, submitted = false;

    private void Awake() { if (Instance == null) Instance = this; else Destroy(gameObject); }

    private void Start()
    {
        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
        }

        if (PlayerPrefs.GetInt("TutorialProgress", 0) == 2)
        {
            Destroy(gameObject);
            return;
        }

        StartCoroutine(StartTutorialWithDelay());
    }

    private IEnumerator StartTutorialWithDelay() { yield return new WaitForSeconds(1.5f); currentStep = EditorStep.Intro; UpdateBossDialogue(); }

    public void OnOkButtonPressed()
    {
        if (isTransitioning) return;
        if (currentStep == EditorStep.Intro) { StartCoroutine(TransitionToNextStep(EditorStep.DragVideoToTimeline, false)); return; }
        StartTaskPhase();
    }

    private void StartTaskPhase()
    {
        TutorialUIManager.Instance.HideBossDialogue();
        isTaskPhaseActive = true;

        switch (currentStep)
        {
            case EditorStep.DragVideoToTimeline: TutorialUIManager.Instance.SetupTasks(new string[] { "- Drag a video to the Timeline" }); videoDropped = false; break;
            case EditorStep.ClickVideoToTrim: TutorialUIManager.Instance.SetupTasks(new string[] { "- Left-Click the video clip on the Timeline" }); trimOpened = false; break;
            case EditorStep.TrimLeftHandle: TutorialUIManager.Instance.SetupTasks(new string[] { "- Drag the Left Handle inward to cut shaking" }); leftTrimmed = false; break;
            case EditorStep.TrimRightHandle: TutorialUIManager.Instance.SetupTasks(new string[] { "- Drag Right Handle to exactly 10.0s" }); rightTrimmed = false; break;

            case EditorStep.CloseTrimWindow: TutorialUIManager.Instance.SetupTasks(new string[] { "- Click the 'X' to close the Trim Window" }); trimClosed = false; break;
            case EditorStep.GoToBrandingPhase: TutorialUIManager.Instance.SetupTasks(new string[] { "- Click 'Next: Add Branding'" }); brandingPhaseReached = false; break;

            case EditorStep.DragBrandingToTrack: TutorialUIManager.Instance.SetupTasks(new string[] { "- Add EXACTLY 1 Logo to the timeline" }); brandDropped = false; break;
            case EditorStep.TrimBranding: TutorialUIManager.Instance.SetupTasks(new string[] { "- Trim pink edges to adjust logo timing" }); brandTrimmed = false; break;

            case EditorStep.PreviewTimeline: TutorialUIManager.Instance.SetupTasks(new string[] { "- Click the Play button to preview your video" }); timelinePlayed = false; break;
            case EditorStep.GoToColorGradePhase: TutorialUIManager.Instance.SetupTasks(new string[] { "- Click 'Next: Color Grade'" }); colorPhaseReached = false; break;

            case EditorStep.AdjustBrightness: TutorialUIManager.Instance.SetupTasks(new string[] { "- Set Brightness to ~1.05" }); brightAdjusted = false; break;
            case EditorStep.AdjustContrast: TutorialUIManager.Instance.SetupTasks(new string[] { "- Boost Contrast slightly (>1.0)" }); contAdjusted = false; break;
            case EditorStep.AdjustSaturation: TutorialUIManager.Instance.SetupTasks(new string[] { "- Boost Saturation to pop (>1.2)" }); satAdjusted = false; break;

            case EditorStep.ToggleFadeIn: TutorialUIManager.Instance.SetupTasks(new string[] { "- Check the 'Fade In' box" }); fadeInToggled = false; break;
            case EditorStep.ClickExport: TutorialUIManager.Instance.SetupTasks(new string[] { "- Click the 'Export' button" }); exported = false; break;
            case EditorStep.ReviewAndSubmit: TutorialUIManager.Instance.SetupTasks(new string[] { "- Watch your video", "- Click 'Submit Video'" }); submitted = false; break;
        }
    }

    private IEnumerator TransitionToNextStep(EditorStep nextStep, bool didTaskJustComplete)
    {
        if (isTransitioning) yield break;
        isTransitioning = true;
        isTaskPhaseActive = false;

        if (didTaskJustComplete) yield return new WaitForSeconds(1.5f);
        TutorialUIManager.Instance.HideBossDialogue();
        if (TutorialUIManager.Instance.taskPanel != null) TutorialUIManager.Instance.taskPanel.SetActive(false);

        yield return new WaitForSeconds(0.5f);
        currentStep = nextStep;
        UpdateBossDialogue();
        isTransitioning = false;
    }

    // --- VIDEO PHASE TRIGGERS ---
    public void OnVideoDropped() { if (currentStep == EditorStep.DragVideoToTimeline && isTaskPhaseActive && !videoDropped) { videoDropped = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.ClickVideoToTrim, true)); } }
    public void OnVideoTrimWindowOpened() { if (currentStep == EditorStep.ClickVideoToTrim && isTaskPhaseActive && !trimOpened) { trimOpened = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(WaitForInspectorAndTransition()); } }
    private IEnumerator WaitForInspectorAndTransition() { yield return new WaitUntil(() => ClipInspector.Instance != null && ClipInspector.Instance.gameObject.activeInHierarchy); StartCoroutine(TransitionToNextStep(EditorStep.TrimLeftHandle, true)); }
    public void OnLeftHandleTrimmed() { if (currentStep == EditorStep.TrimLeftHandle && isTaskPhaseActive && !leftTrimmed) { leftTrimmed = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.TrimRightHandle, true)); } }
    public void OnRightHandleTrimmed() { if (currentStep == EditorStep.TrimRightHandle && isTaskPhaseActive && !rightTrimmed) { rightTrimmed = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.CloseTrimWindow, true)); } }
    public void OnTrimWindowClosed() { if (currentStep == EditorStep.CloseTrimWindow && isTaskPhaseActive && !trimClosed) { trimClosed = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.GoToBrandingPhase, true)); } }

    // --- PHASE CHANGE TRACKER ---
    public void OnPhaseChanged(int phaseIndex)
    {
        if (currentStep == EditorStep.GoToBrandingPhase && isTaskPhaseActive && !brandingPhaseReached && phaseIndex == 1) { brandingPhaseReached = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.DragBrandingToTrack, true)); }
        if (currentStep == EditorStep.GoToColorGradePhase && isTaskPhaseActive && !colorPhaseReached && phaseIndex == 2) { colorPhaseReached = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.AdjustBrightness, true)); }
    }

    // --- BRANDING PHASE TRIGGERS ---
    public void OnBrandDropped() { if (currentStep == EditorStep.DragBrandingToTrack && isTaskPhaseActive && !brandDropped) { brandDropped = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.TrimBranding, true)); } }
    public void OnBrandTrimmed() { if (currentStep == EditorStep.TrimBranding && isTaskPhaseActive && !brandTrimmed) { brandTrimmed = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.PreviewTimeline, true)); } }
    public void OnTimelinePlayed() { if (currentStep == EditorStep.PreviewTimeline && isTaskPhaseActive && !timelinePlayed) { timelinePlayed = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.GoToColorGradePhase, true)); } }

    // --- COLOR GRADING TRIGGERS ---
    public void OnBrightnessAdjusted() { if (currentStep == EditorStep.AdjustBrightness && isTaskPhaseActive && !brightAdjusted) { brightAdjusted = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.AdjustContrast, true)); } }
    public void OnContrastAdjusted() { if (currentStep == EditorStep.AdjustContrast && isTaskPhaseActive && !contAdjusted) { contAdjusted = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.AdjustSaturation, true)); } }
    public void OnSaturationAdjusted() { if (currentStep == EditorStep.AdjustSaturation && isTaskPhaseActive && !satAdjusted) { satAdjusted = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.ToggleFadeIn, true)); } }
    public void OnFadeInToggled() { if (currentStep == EditorStep.ToggleFadeIn && isTaskPhaseActive && !fadeInToggled) { fadeInToggled = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.ClickExport, true)); } }

    // --- EXPORT TRIGGERS ---
    public void OnExportClicked() { if (currentStep == EditorStep.ClickExport && isTaskPhaseActive && !exported) { exported = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.ReviewAndSubmit, true)); } }
    public void OnVideoSubmitted() { if (currentStep == EditorStep.ReviewAndSubmit && isTaskPhaseActive && !submitted) { submitted = true; TutorialUIManager.Instance.MarkTaskComplete(1); if (TutorialUIManager.Instance.taskPanel != null) TutorialUIManager.Instance.taskPanel.SetActive(false); isTaskPhaseActive = false; } }

    private void UpdateBossDialogue()
    {
        TutorialUIManager ui = TutorialUIManager.Instance;
        if (ui == null) return;

        switch (currentStep)
        {
            case EditorStep.Intro:
                ui.ShowBossDialogue("Welcome to the Editing Bay! Let's cut our raw footage into a basic commercial.", ui.poseHappy, true, false);
                break;
            case EditorStep.DragVideoToTimeline:
                ui.ShowBossDialogue("First, click and drag your video clip down from the bin into the Video Track on the timeline.", ui.posePoint, true, false);
                break;
            case EditorStep.ClickVideoToTrim:
                ui.ShowBossDialogue("Great. But we only need exactly 10 seconds of footage. Click the video clip on the timeline to open the Trim Inspector.", ui.poseOpenHand, true, false);
                break;
            case EditorStep.TrimLeftHandle:
                ui.ShowBossDialogue("Drag the Left Handle inwards to cut out the shaky camera movement at the start of the take.", ui.posePointUp, true, false);
                break;
            case EditorStep.TrimRightHandle:
                ui.ShowBossDialogue("Now drag the Right Handle inwards until your 'Trimmed Duration' is exactly 10.0 Seconds.", ui.poseSmile, true, false);
                break;
            case EditorStep.CloseTrimWindow:
                ui.ShowBossDialogue("Good! Now close the Trim Inspector using the X button so we can keep working.", ui.poseHappy, true, false);
                break;
            case EditorStep.GoToBrandingPhase:
                ui.ShowBossDialogue("Perfect cut! Now click the 'Next: Add Branding' tab on the left to open the graphics bin.", ui.posePoint, true, false);
                break;
            case EditorStep.DragBrandingToTrack:
                ui.ShowBossDialogue("Basic Branding: Drag exactly ONE Company Logo down into the top Branding Track.", ui.poseOpenHand, true, false);
                break;
            case EditorStep.TrimBranding:
                ui.ShowBossDialogue("You can trim the edges of the pink graphic clip to adjust exactly when the logo appears.", ui.posePointUp, true, false);
                break;
            case EditorStep.PreviewTimeline:
                ui.ShowBossDialogue("Before we move on, click the Play button under the screen to preview your timing.", ui.poseSmile, true, false);
                break;
            case EditorStep.GoToColorGradePhase:
                ui.ShowBossDialogue("Great. I'll wait here until you click 'Next: Color Grade' on the left menu!", ui.poseChill, true, false);
                break;
            case EditorStep.AdjustBrightness:
                ui.ShowBossDialogue("Basic Color Enhancement: Start with Brightness. Set the Brightness slider to around 1.05.", ui.poseHappy, true, false);
                break;
            case EditorStep.AdjustContrast:
                ui.ShowBossDialogue("Next, bump the Contrast slider just a little bit past 1.0 to deepen the shadows.", ui.poseSmile, true, false);
                break;
            case EditorStep.AdjustSaturation:
                ui.ShowBossDialogue("Finally, a simple color pop. Push the Saturation slider past 1.2 to make it vibrant.", ui.posePoint, true, false);
                break;
            case EditorStep.ToggleFadeIn:
                ui.ShowBossDialogue("Let's add a smooth opening. Check the 'Fade In' box to fade the video from black.", ui.poseSmile, true, false);
                break;
            case EditorStep.ClickExport:
                ui.ShowBossDialogue("Amazing job. 10 seconds, centered, lit, 1 logo, and simple color. Hit 'Export'!", ui.poseBoss, true, false);
                break;
            case EditorStep.ReviewAndSubmit:
                ui.ShowBossDialogue("Watch your masterpiece! If it looks good, hit the Submit Video button to send it to the client and get paid.", ui.poseHappy, true, false);
                break;
        }
    }
}