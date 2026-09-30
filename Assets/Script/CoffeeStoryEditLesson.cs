using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class CoffeeStoryEditLesson : MonoBehaviour
{
    private bool showing;
    private int step;
    private bool waitingForAction;
    private int initialSplitCount;
    private int initialUndoCount;
    private DraggableClip gradeTarget;
    private Vector3 initialGrade;
    private IEnumerator Start()
    {
        yield return null;
        if (CampaignProgression.GetCurrentLevel() != 4 || DevTutorialBypass.Disabled ||
            TutorialUIManager.Instance == null) { Destroy(this); yield break; }
        showing = true;
        initialSplitCount = DraggableClip.SplitCount;
        initialUndoCount = DraggableClip.SplitUndoCount;
        Show();
    }
    private void Show()
    {
        var ui = TutorialUIManager.Instance;
        ui.HideTasks();
        string[] messages = {
            "Build a 30–45-second commercial. Start with coffee and packaging, then show an actor using coffee in the shop. No overlays. First drag a recording to the timeline and click it.",
            "Drag the playback slider below the preview to move the red timeline line. Stop inside your selected clip where you want the cut, then press B. The cut follows the red line, not the middle.",
            "Want to reverse that split? Press Ctrl + Z to join the pieces back together. Try undoing the split now.",
            "Select a clip, then open COLOR GRADE. Adjust a slider: it changes only the selected clip, not the whole commercial.",
            "Finish your edit and preview all 30–45 seconds. Keep at least 2 seconds of each required shot. Press TAB to read your current contract; press TAB again to return to editing."
        };
        ui.ShowBossDialogue(messages[step], ui.poseOpenHand, false, false);
    }
    private void Update()
    {
        if (!showing || PauseManager.isPaused) return;
        var ui = TutorialUIManager.Instance;
        if (ui == null) { Destroy(this); return; }
        if (ContractUIManager.Instance != null && ContractUIManager.Instance.IsQualificationsOpen()) return;
        if (DevTutorialBypass.Disabled) { ui.HideBossDialogue(); ui.HideTasks(); Destroy(this); return; }
        if (waitingForAction)
        {
            var selected = DraggableClip.Selected;
            bool done = step == 0 ? selected != null && selected.isOnTimeline :
                step == 1 ? DraggableClip.SplitCount > initialSplitCount : step == 2 ? DraggableClip.SplitUndoCount > initialUndoCount : false;
            if (step == 3 && selected != null && selected.isOnTimeline)
            {
                Vector3 grade = new Vector3(selected.gradeBrightness, selected.gradeContrast, selected.gradeSaturation);
                if (gradeTarget != selected) { gradeTarget = selected; initialGrade = grade; }
                else done = (grade - initialGrade).sqrMagnitude > .0001f;
            }
            if (done) { waitingForAction = false; step++; Show(); }
            return;
        }
        if (!TutorialUIManager.BossContinuePressed || !ui.CanAdvanceBossDialogue()) return;
        ui.HideBossDialogue();
        if (step == 4) { showing = false; ui.HideTasks(); Destroy(this); return; }
        waitingForAction = true;
        ui.SetupTasks(new[] { step == 0 ? "ADD A CLIP TO THE TIMELINE AND CLICK IT" : step == 1 ? "DRAG THE PLAYBACK SLIDER TO POSITION THE RED LINE; PRESS [B] TO CUT THERE" : step == 2 ? "[CTRL + Z] UNDO THE SPLIT" : "SELECT ONE CLIP; OPEN COLOR GRADE AND ADJUST A SLIDER" });
    }
}
