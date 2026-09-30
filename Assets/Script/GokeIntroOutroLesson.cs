using System.Collections;
using UnityEngine;

public sealed class GokeIntroOutroLesson : MonoBehaviour
{
    private enum Step { ExplainIntro, ExplainOutro }
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
        if (!TutorialUIManager.BossContinuePressed || !ui.CanAdvanceBossDialogue()) return;
        ui.HideBossDialogue();
        if (step == Step.ExplainIntro) Explain(Step.ExplainOutro);
        else { Cleanup(); Destroy(this); }
    }

    private void Explain(Step next)
    {
        step = next;
        var ui = TutorialUIManager.Instance;
        ui.HideTasks();
        string text;
        if (next == Step.ExplainIntro)
            text = "An INTRO catches attention and introduces the brand before the product footage. Goke's supplied intro gives your advert a clear opening.";
        else
            text = "An OUTRO leaves viewers with the brand and a final message to remember. Both clips are in CLIPS: 2 seconds intro, 8 seconds of your footage, then 2 seconds outro. You're ready to edit!";
        ui.ShowBossDialogue(text, ui.poseOpenHand, false, false);
    }

    private void Cleanup()
    {
        ready = false;
        if (TutorialUIManager.Instance == null) return;
        TutorialUIManager.Instance.HideBossDialogue();
        TutorialUIManager.Instance.HideTasks();
    }
}
