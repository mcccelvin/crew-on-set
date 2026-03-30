using System.Collections;
using UnityEngine;

public class EditorTutorialManager : MonoBehaviour
{
    public static EditorTutorialManager Instance;

    public enum EditorStep
    {
        Intro,
        DragVideoToTimeline, ClickVideoToTrim, TrimLeftHandle, TrimRightHandle,
        GoToBrandingPhase, DragBrandingToTrack, TrimBranding, ArrangeBrandingAndWait,
        AdjustBrightness, AdjustContrast, AdjustSaturation,
        ClickExport
    }

    public EditorStep currentStep;
    private bool isTransitioning = false;
    private bool isTaskPhaseActive = false;

    private bool videoDropped = false, trimOpened = false, leftTrimmed = false, rightTrimmed = false;
    private bool brandingPhaseReached = false, brandDropped = false, brandTrimmed = false;
    private bool colorPhaseReached = false, brightAdjusted = false, contAdjusted = false, satAdjusted = false;
    public bool exported = false;

    private void Awake() { if (Instance == null) Instance = this; else Destroy(gameObject); }

    private void Start()
    {
        // --- NEW: Instantly hide any leftover dialogue from the Studio scene! ---
        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
        }

        // If the tutorial is completely over, destroy the boss
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

            // --- UPDATED TASKS ---
            case EditorStep.TrimLeftHandle: TutorialUIManager.Instance.SetupTasks(new string[] { "- Drag the Left Handle inward to cut shaking" }); leftTrimmed = false; break;
            case EditorStep.TrimRightHandle: TutorialUIManager.Instance.SetupTasks(new string[] { "- Drag Right Handle to exactly 10.0s" }); rightTrimmed = false; break;
            case EditorStep.GoToBrandingPhase: TutorialUIManager.Instance.SetupTasks(new string[] { "- Arrange clip on the timeline", "- Click 'Next: Add Branding'" }); brandingPhaseReached = false; break;
            case EditorStep.DragBrandingToTrack: TutorialUIManager.Instance.SetupTasks(new string[] { "- Add 3 logos (Intro, Tagline, Outro)" }); brandDropped = false; break;
            case EditorStep.TrimBranding: TutorialUIManager.Instance.SetupTasks(new string[] { "- Trim pink edges to adjust logo timing" }); brandTrimmed = false; break;
            case EditorStep.ArrangeBrandingAndWait: TutorialUIManager.Instance.SetupTasks(new string[] { "- Arrange all 3 logos", "- Click 'Next: Color Grade'" }); colorPhaseReached = false; break;
            case EditorStep.AdjustBrightness: TutorialUIManager.Instance.SetupTasks(new string[] { "- Keep Brightness balanced" }); brightAdjusted = false; break;
            case EditorStep.AdjustContrast: TutorialUIManager.Instance.SetupTasks(new string[] { "- Boost Contrast to add depth" }); contAdjusted = false; break;
            case EditorStep.AdjustSaturation: TutorialUIManager.Instance.SetupTasks(new string[] { "- Boost Saturation for 'Vibrant Pop'" }); satAdjusted = false; break;
            // ---------------------

            case EditorStep.ClickExport: TutorialUIManager.Instance.SetupTasks(new string[] { "- Click the 'Export' button" }); exported = false; break;
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

    public void OnVideoDropped() { if (currentStep == EditorStep.DragVideoToTimeline && isTaskPhaseActive && !videoDropped) { videoDropped = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.ClickVideoToTrim, true)); } }
    public void OnVideoTrimWindowOpened() { if (currentStep == EditorStep.ClickVideoToTrim && isTaskPhaseActive && !trimOpened) { trimOpened = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(WaitForInspectorAndTransition()); } }
    private IEnumerator WaitForInspectorAndTransition() { yield return new WaitUntil(() => ClipInspector.Instance != null && ClipInspector.Instance.gameObject.activeInHierarchy); StartCoroutine(TransitionToNextStep(EditorStep.TrimLeftHandle, true)); }
    public void OnLeftHandleTrimmed() { if (currentStep == EditorStep.TrimLeftHandle && isTaskPhaseActive && !leftTrimmed) { leftTrimmed = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.TrimRightHandle, true)); } }
    public void OnRightHandleTrimmed() { if (currentStep == EditorStep.TrimRightHandle && isTaskPhaseActive && !rightTrimmed) { rightTrimmed = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.GoToBrandingPhase, true)); } }

    public void OnPhaseChanged(int phaseIndex)
    {
        if (currentStep == EditorStep.GoToBrandingPhase && isTaskPhaseActive && !brandingPhaseReached && phaseIndex == 1) { brandingPhaseReached = true; TutorialUIManager.Instance.MarkTaskComplete(1); StartCoroutine(TransitionToNextStep(EditorStep.DragBrandingToTrack, true)); }
        if (currentStep == EditorStep.ArrangeBrandingAndWait && isTaskPhaseActive && !colorPhaseReached && phaseIndex == 2) { colorPhaseReached = true; TutorialUIManager.Instance.MarkTaskComplete(1); StartCoroutine(TransitionToNextStep(EditorStep.AdjustBrightness, true)); }
    }

    public void OnBrandDropped() { if (currentStep == EditorStep.DragBrandingToTrack && isTaskPhaseActive && !brandDropped) { brandDropped = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.TrimBranding, true)); } }
    public void OnBrandTrimmed() { if (currentStep == EditorStep.TrimBranding && isTaskPhaseActive && !brandTrimmed) { brandTrimmed = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.ArrangeBrandingAndWait, true)); } }

    public void OnBrightnessAdjusted() { if (currentStep == EditorStep.AdjustBrightness && isTaskPhaseActive && !brightAdjusted) { brightAdjusted = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.AdjustContrast, true)); } }
    public void OnContrastAdjusted() { if (currentStep == EditorStep.AdjustContrast && isTaskPhaseActive && !contAdjusted) { contAdjusted = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.AdjustSaturation, true)); } }
    public void OnSaturationAdjusted() { if (currentStep == EditorStep.AdjustSaturation && isTaskPhaseActive && !satAdjusted) { satAdjusted = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(EditorStep.ClickExport, true)); } }

    public void OnExportClicked()
    {
        if (currentStep == EditorStep.ClickExport && isTaskPhaseActive && !exported)
        {
            exported = true;
            TutorialUIManager.Instance.MarkTaskComplete(0);
            if (TutorialUIManager.Instance.taskPanel != null) TutorialUIManager.Instance.taskPanel.SetActive(false);
            isTaskPhaseActive = false;
        }
    }

    private void UpdateBossDialogue()
    {
        TutorialUIManager ui = TutorialUIManager.Instance;
        if (ui == null) return;

        switch (currentStep)
        {
            case EditorStep.Intro:
                ui.ShowBossDialogue("Welcome to the Editing Bay! Let's cut our raw footage into an S-Rank commercial.", ui.poseHappy, true, false);
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
            case EditorStep.GoToBrandingPhase:
                ui.ShowBossDialogue("Perfect cut! Now click the 'Overlays' tab on the left to open the graphics bin.", ui.posePoint, true, false);
                break;
            case EditorStep.DragBrandingToTrack:
                ui.ShowBossDialogue("Drag the Company Logo down into the top Branding Track on the timeline.", ui.poseOpenHand, true, false);
                break;
            case EditorStep.TrimBranding:
                ui.ShowBossDialogue("Just like the video, you can trim the edges of the pink graphic clip to adjust exactly when it appears and disappears.", ui.posePointUp, true, false);
                break;
            case EditorStep.ArrangeBrandingAndWait:
                ui.ShowBossDialogue("Drag ONE more graphic down (a Tagline) so you have a Logo and a Tagline. I'll wait here until you click 'Next: Color Grade' on the left menu!", ui.poseChill, true, false);
                break;
            case EditorStep.AdjustBrightness:
                ui.ShowBossDialogue("Final step for an S-Rank: The Color Grade. Start with Brightness. Set the Brightness slider to around 1.05.", ui.poseHappy, true, false);
                break;
            case EditorStep.AdjustContrast:
                ui.ShowBossDialogue("Next is Contrast. Boosting Contrast gives the flower 3D depth! Push the Contrast slider past 1.1 for an S-Rank.", ui.poseSmile, true, false);
                break;
            case EditorStep.AdjustSaturation:
                ui.ShowBossDialogue("Finally, Saturation. Crank the Saturation slider past 1.2 to make the yellow petals incredibly vibrant.", ui.posePoint, true, false);
                break;
            case EditorStep.ClickExport:
                ui.ShowBossDialogue("Amazing job. The editing is tight, the branding is clear, and the colors are cinematic. Hit 'Export' to let the system grade your final commercial!", ui.poseBoss, true, false);
                break;
        }
    }
}