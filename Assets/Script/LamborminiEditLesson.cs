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
            ? "A detail creates curiosity; a reveal shows what that detail belongs to. For Terrari, open on the headlight or front wheel, then reveal the low front-quarter view. A smooth move helps viewers read the paint and reflections.\n\nBuild an 8-12 second commercial from your recorded car footage. Press SPACE for the editing tip."
            : "For a simple reveal, record a steady front-quarter hero shot. In BRANDING, choose CAMERA MOTION > SLOW PULL OUT to start closer and reveal the frame. You can also cut a separate detail take into a hero take.\n\nKeep the orange paint readable: Brightness 0.85-1.15, Contrast 1.05-1.45, Saturation 0.95-1.30. Cinematic music is a suggestion; graphics and intro/outro cards are optional. Press SPACE to create your edit.";
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
