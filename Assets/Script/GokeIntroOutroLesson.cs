using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GokeIntroOutroLesson : MonoBehaviour
{
    private enum Step { ExplainIntro, PlaceIntro, ExplainOutro, Assemble, Finish }
    private Step step;
    private bool ready;
    private EditorManager editor;

    private IEnumerator Start()
    {
        // Let the previous scene lesson release its shared Boss UI first.
        yield return null;
        editor = GetComponent<EditorManager>();
        if (CampaignProgression.GetCurrentLevel() != 2 || DevTutorialBypass.Disabled
            || editor == null || TutorialUIManager.Instance == null)
        { Destroy(this); yield break; }
        editor.GoToVideoEditing();
        ready = true;
        Explain(Step.ExplainIntro);
    }

    private void Update()
    {
        if (!ready) return;
        if (DevTutorialBypass.Disabled) { Cleanup(); Destroy(this); return; }
        var ui = TutorialUIManager.Instance;
        if (ui == null || PauseManager.isPaused) return;
        if (step == Step.ExplainIntro || step == Step.ExplainOutro || step == Step.Finish)
        {
            if (Keyboard.current == null || !Keyboard.current.spaceKey.wasPressedThisFrame || !ui.CanAdvanceBossDialogue()) return;
            ui.HideBossDialogue();
            if (step == Step.Finish) { Cleanup(); Destroy(this); return; }
            if (step == Step.ExplainIntro)
            {
                step = Step.PlaceIntro;
                ui.SetupTasks(new[] { "Drag GOKE INTRO from CLIPS to 0s on the video timeline." });
            }
            else
            {
                step = Step.Assemble;
                ui.SetupTasks(new[] {
                    "Keep the full INTRO at 0-2s.",
                    "Trim your Goke footage to 6s; place it at 2-8s.",
                    "Drag the full OUTRO after the footage at 10-12s.",
                    "Join the clips with no gaps or overlaps." });
            }
            return;
        }

        float pps = TimelineManager.Instance != null ? TimelineManager.Instance.pixelsPerSecond : editor.pixelsPerSecond;
        var sequence = GokeSequence.Evaluate(editor.timelineContainer, pps);
        if (step == Step.PlaceIntro && sequence.intro) Explain(Step.ExplainOutro);
        // Placement completes the lesson. Exact timing remains a contract check,
        // so a longer take does not leave the checklist covering other tools.
        else if (step == Step.Assemble && sequence.intro && sequence.product && sequence.outro) Explain(Step.Finish);
    }

    private void Explain(Step next)
    {
        step = next;
        var ui = TutorialUIManager.Instance;
        ui.HideTasks();
        string text;
        if (next == Step.ExplainIntro)
            text = "Goke's intro and outro are supplied in CLIPS. An INTRO introduces the brand and sets the tone, so viewers know whose commercial they are watching. Put the 2-second GOKE INTRO at the very beginning.\n\nPress SPACE to try it.";
        else if (next == Step.ExplainOutro)
            text = "An OUTRO reinforces the brand and gives viewers a final message to remember. 'Make it a Goke' invites them to choose the product. Put it after your footage: INTRO 2s > PRODUCT 8s > OUTRO 2s. Double-click the footage to trim it; right-click a clip to return it to CLIPS.\n\nPress SPACE to build the ending.";
        else
            text = "You've placed the intro, product footage and outro. The intro introduces Goke; the outro leaves a memorable sign-off. The lesson is finished. Aim for 2s intro + 8s footage + 2s outro, with no gaps. Add the logo and tagline overlays whenever you like; their timing is yours to choose.\n\nPress SPACE to continue editing.";
        ui.ShowBossDialogue(text, next == Step.Finish ? ui.poseHappy : ui.poseOpenHand, false, false);
    }

    private void Cleanup()
    {
        ready = false;
        if (TutorialUIManager.Instance == null) return;
        TutorialUIManager.Instance.HideBossDialogue();
        TutorialUIManager.Instance.HideTasks();
    }
}
