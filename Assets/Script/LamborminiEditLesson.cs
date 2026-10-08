using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class LamborminiEditLesson : MonoBehaviour
{
    private int step;
    private bool ready;
    private IEnumerator Start()
    {
        yield return null;
        // Retain the component for existing scene references, but do not run a Terrari editor lesson.
        Destroy(this);
        yield break;
    }
    private void Show()
    {
        var ui=TutorialUIManager.Instance;ui.HideTasks();
        string text=step==0
            ? "The back shows rear styling, the side shows the silhouette, and an overall view introduces the full car. Keep the warm light and orange paint consistent across the cuts.\n\nThe clip bank now includes a 2-second TERRARI INTRO and 2-second TERRARI OUTRO. Put the intro first, your three separate SD-card recordings in the middle, and the outro last. The finished commercial must be 25 seconds. Press SPACE for the editing tip."
            : "Build this order with no gaps or overlaps: TERRARI INTRO 2s, back 7s, side 7s, overall 7s, TERRARI OUTRO 2s. That makes exactly 25 seconds. Each car view must be a separate recording; copying or splitting one take does not count. One SD card can hold all three takes within its 60-second capacity. Use SLOW PULL OUT on a moving shot if it helps.\n\nPlace a readable overlay in the upper-left title-safe area where it does not cover the car. Keep Brightness 0.85-1.15, Contrast 1.05-1.45, and Saturation 0.95-1.30. Press SPACE to create your edit.";
        ui.ShowBossDialogue(text,ui.poseOpenHand,false,false);
    }
    private void Update()
    {
        if(!ready)return;
        if(DevTutorialBypass.Disabled){Cleanup();return;}
        var ui=TutorialUIManager.Instance;
        if(ui==null || PauseManager.isPaused || !TutorialUIManager.BossContinuePressed || !ui.CanAdvanceBossDialogue())return;
        if(step++==0)Show();else Cleanup();
    }
    private void Cleanup(){ready=false;if(TutorialUIManager.Instance!=null){TutorialUIManager.Instance.HideBossDialogue();TutorialUIManager.Instance.HideTasks();}Destroy(this);}
}
