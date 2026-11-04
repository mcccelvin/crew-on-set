using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ContractTutorialChoice
{
    private static readonly ContractTutorialPolicy policy=new ContractTutorialPolicy();
    private static int playAfterReload;
    public static bool IsAsking {get;private set;}
    public static bool SkipThisContract=>!PhotonNetwork.InRoom && !GameSaveManager.IsTrainingRoom && policy.Skip(CampaignProgression.GetCurrentLevel());
    public static bool RestartGuidance(int level)=>!PhotonNetwork.InRoom && !GameSaveManager.IsTrainingRoom &&
        policy.Restart(level,SceneManager.GetActiveScene().handle);
    public static bool NeedsPrompt(int level)=>!PhotonNetwork.InRoom && !GameSaveManager.IsTrainingRoom &&
        !DevTutorialBypass.DeveloperDisabled && !policy.HasChoice(level);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset(){policy.Reset();IsAsking=false;playAfterReload=0;}
    public static void Retry(int level){policy.Forget(level);if(playAfterReload==level)playAfterReload=0;}
    public static void PlayInCurrentScene(int level)=>policy.Choose(level,SceneManager.GetActiveScene().handle,true);
    public static void PlayAfterReload(int level){policy.Reset();playAfterReload=level;}

    public static IEnumerator Ask(TutorialManager owner,int level)
    {
        if(!NeedsPrompt(level))yield break;
        while(IsAsking)yield return null;
        if(!NeedsPrompt(level))yield break;
        if(playAfterReload==level)
        {playAfterReload=0;policy.Choose(level,owner.gameObject.scene.handle,true);yield break;}
        var ui=TutorialUIManager.Instance;
        if(ui==null || ui.bossHUDCanvas==null || ui.bossText==null)
        {Debug.LogWarning("Tutorial choice: Boss dialogue is missing; continuing with guidance.");yield break;}
        var player=Object.FindObjectOfType<Player.PlayerController.PlayerController>();
        bool couldMove=player!=null && player.canMove,couldLook=player!=null && player.canLook;
        var cursor=Cursor.lockState;bool visible=Cursor.visible;
        if(player!=null){player.canMove=false;player.canLook=false;}
        IsAsking=true;
        ui.ShowBossDialogue("Before we start <b>Contract "+level+"</b>, would you like to play this level's tutorial?",ui.poseOpenHand,false,false);
        var hint=owner.spacePromptText;
        const string keys="PRESS <color=#B00020>[SPACE / LMB]</color> TO CONTINUE   ·   <color=#B00020>[TAB]</color> TO SKIP";
        int answer=-1;
        float ready=Time.unscaledTime+.25f;
        try
        {
            while(answer<0)
            {
                bool readyForInput=Application.isFocused && !PauseManager.isPaused && Time.unscaledTime>=ready;
                BossDialogueStyle.UpdateContinueHint(hint, !PauseManager.isPaused, keys);
                if(readyForInput)
                {
                    if(Keyboard.current!=null && Keyboard.current.tabKey.wasPressedThisFrame)answer=0;
                    else if(TutorialUIManager.BossContinuePressed)answer=1;
                }
                yield return null;
            }
            policy.Choose(level,owner.gameObject.scene.handle,answer==1);
        }
        finally
        {
            IsAsking=false;
            BossDialogueStyle.UpdateContinueHint(hint,false);
            if(ui!=null)ui.HideBossDialogue();
            if(player!=null){player.canMove=couldMove;player.canLook=couldLook;}
            Cursor.lockState=cursor;Cursor.visible=visible;
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);
        }
    }

}
