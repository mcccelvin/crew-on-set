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
        if(CampaignProgression.GetCurrentLevel()!=3 || DevTutorialBypass.Disabled || TutorialUIManager.Instance==null){Destroy(this);yield break;}
        ready=true; Show();
    }
    private void Show()
    {
        var ui=TutorialUIManager.Instance;ui.HideTasks();
        string text=step==0
            ? "The back shows rear styling, the side shows the silhouette, and an overall view introduces the full car. Keep the warm light and orange paint consistent across the cuts.\n\nThe clip bank now includes a 2-second TERRARI INTRO and 2-second TERRARI OUTRO. Put the intro first, your three separate SD-card recordings in the middle, and the outro last. The finished commercial must be 25 seconds. Press SPACE for the editing tip."
            : "Build this order with no gaps or overlaps: TERRARI INTRO 2s, back 7s, side 7s, overall 7s, TERRARI OUTRO 2s. That makes exactly 25 seconds. Each car view must come from a different SD card; copying or splitting one take does not count. Use SLOW PULL OUT on a moving shot if it helps.\n\nPlace a readable overlay in the upper-left title-safe area where it does not cover the car. Keep Brightness 0.85-1.15, Contrast 1.05-1.45, and Saturation 0.95-1.30. Press SPACE to create your edit.";
        ui.ShowBossDialogue(text,ui.poseOpenHand,false,false);
    }
    private void Update()
    {
        if(!ready)return;
        if(DevTutorialBypass.Disabled){Cleanup();return;}
        var ui=TutorialUIManager.Instance;
        if(ui==null || PauseManager.isPaused || Keyboard.current==null || !Keyboard.current.spaceKey.wasPressedThisFrame || !ui.CanAdvanceBossDialogue())return;
        if(step++==0)Show();else Cleanup();
    }
    private void Cleanup(){ready=false;if(TutorialUIManager.Instance!=null){TutorialUIManager.Instance.HideBossDialogue();TutorialUIManager.Instance.HideTasks();}Destroy(this);}
}
