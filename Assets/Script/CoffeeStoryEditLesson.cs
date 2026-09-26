using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class CoffeeStoryEditLesson : MonoBehaviour
{
    private bool showing;
    private int step;
    private IEnumerator Start()
    {
        yield return null;
        if (CampaignProgression.GetCurrentLevel() != 4 || DevTutorialBypass.Disabled ||
            TutorialUIManager.Instance == null) { Destroy(this); yield break; }
        showing = true;
        Show();
    }
    private void Show()
    {
        var ui = TutorialUIManager.Instance;
        ui.ShowBossDialogue(step == 0
            ? "This lesson is elliptical editing: remove the waiting between moments. Put three different takes in order: Wave greeting, Action with coffee (or Using Machine), then Sitting. Press SPACE."
            : "Join them from 0s with no gaps. Keep each beat at least 2s and trim the whole story to 15s. Place a brand graphic over the final 2s. Preview, then export. Music, transitions and grading are your choice. Press SPACE to edit.",
            ui.poseOpenHand, false, false);
    }
    private void Update()
    {
        if (!showing || PauseManager.isPaused || Keyboard.current == null) return;
        var ui = TutorialUIManager.Instance;
        if (ui == null) { Destroy(this); return; }
        if (!Keyboard.current.spaceKey.wasPressedThisFrame || !ui.CanAdvanceBossDialogue()) return;
        if (step++ == 0) Show();
        else { showing = false; ui.HideBossDialogue(); Destroy(this); }
    }
}

