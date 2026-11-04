using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed partial class RoleSelectionUI
{
    private void CreateStageSelection()
    {
        fullscreenSelection = true;
        var desk = gameObject.scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Canvas>(true))
            .FirstOrDefault(c => c.name == "HelpDesk UI");
        if (desk == null)
            desk = new GameObject("Crew selection canvas",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();
        lobbyRoot = desk.gameObject;
        lobbyRoot.transform.SetParent(null,false); lobbyRoot.transform.localScale=Vector3.one;
        desk.renderMode = RenderMode.ScreenSpaceOverlay; desk.overrideSorting = true; desk.sortingOrder = 200;
        foreach (Transform child in desk.transform) child.gameObject.SetActive(false);
        // Retire the painted tablet and paper backdrop for this scene only.
        foreach (var graphic in desk.GetComponents<Graphic>()) graphic.enabled = false;
        var scaler = desk.GetComponent<CanvasScaler>() ?? desk.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        if (desk.GetComponent<GraphicRaycaster>() == null) desk.gameObject.AddComponent<GraphicRaycaster>();
        var frame = LobbyRect(desk.transform,"Full-screen crew selection",0,0,1920,1080);
        frame.anchorMin = frame.anchorMax = frame.pivot = Vector2.one * .5f; frame.anchoredPosition = Vector2.zero;
        for (int i=0;i<3;i++)
        {
            var page = LobbyRect(frame,new[]{"Roles","Role help","Selection settings"}[i],0,0,1920,1080);
            lobbyPages[i] = page.gameObject;
        }
        var stageObject = new GameObject("3D crew selection studio");
        stageObject.transform.SetParent(transform,false);
        selectionStage = stageObject.AddComponent<CrewSelectionStage>();
        var title = LobbyText(frame,"Selection title",80,44,1400,72,52);
        title.text = "CHOOSE YOUR CREW ROLE"; ExportUIArt.OutlineText(title);
        roomCode = LobbyText(frame,"Room code",1430,65,300,30,23,TextAlignmentOptions.Right);
        roomCode.color = CrewPaperStyle.Paper;
        StageButton(frame,"Help","ROLE HELP",1340,138,180,52,()=>SelectLobbyPage(1));
        StageButton(frame,"Settings","SETTINGS",1538,138,180,52,()=>
        {
            if (lobbyOptions == null) lobbyOptions = new SharedOptionsPanel(lobbyRoot.transform,()=>{Open=true;});
            lobbyOptions.Open();
        });
        var close = LobbyBlock(frame,"Leave room",1770,48,72,72,Color.white);
        ExportUIArt.Apply(close,"close"); close.raycastTarget=true;
        var closeButton=close.gameObject.AddComponent<Button>(); closeButton.targetGraphic=close;
        closeButton.onClick.AddListener(()=> {
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
            else LoadingScreenController.LoadScene("Main Menu");
        });
        var start = lobbyPages[0].transform;
        StagePaper(start,"Role details",80,220,400,535);
        roleTitle = LobbyText(start,"Focused role",108,248,344,52,38); roleTitle.fontStyle=FontStyles.Bold;
        roleDescription = LobbyText(start,"Role responsibilities",108,325,344,178,27,TextAlignmentOptions.TopLeft);
        LobbyBlock(start,"Description rule",108,525,344,2,new Color32(193,164,111,255));
        roleControls = LobbyText(start,"Role controls",108,548,344,66,23,TextAlignmentOptions.TopLeft);
        selectedRoles = LobbyText(start,"Your roles",108,650,344,76,23,TextAlignmentOptions.TopLeft);
        localUsername = LobbyText(start,"Your username",620,144,680,50,38,TextAlignmentOptions.Center);
        localUsername.fontStyle=FontStyles.Bold; localUsername.color=CrewPaperStyle.Paper; localUsername.richText=false;
        StageButton(start,"Rotate left","‹",605,700,70,62,()=>selectionStage.Rotate(-30));
        StageButton(start,"Rotate right","›",1245,700,70,62,()=>selectionStage.Rotate(30));
        StagePaper(start,"Role choices",1340,220,500,535);
        LobbyHeading(start,"SELECT ROLES",1368,246,444);
        for (int i=0;i<4;i++)
        {
            int index=i; float x=1368+i%2*230, y=312+i/2*102;
            roleButtons[i]=StageButton(start,LobbyRoles[i]+" role",MultiplayerContractManager.RoleName(LobbyRoles[i]).ToUpperInvariant(),x,y,214,86,()=>ChooseLobbyRole(index));
            var caption=roleButtons[i].GetComponentInChildren<TMP_Text>();
            caption.rectTransform.anchoredPosition=new Vector2(12,-10); caption.rectTransform.sizeDelta=new Vector2(190,34);
            caption.fontSize=caption.fontSizeMax=24; caption.fontSizeMin=20; caption.enableWordWrapping=false;
            roleOwners[i]=LobbyText(roleButtons[i].transform,"Assigned username",12,48,190,26,18,TextAlignmentOptions.Center);
            roleOwners[i].richText=false;
            roleFocus[i]=roleButtons[i].gameObject.AddComponent<Outline>();
            roleFocus[i].effectColor=CrewPaperStyle.Gold; roleFocus[i].effectDistance=new Vector2(3,-3);
        }
        lockButton=StageButton(start,"Lock roles","LOCK ROLES",1368,548,214,62,()=>Send("lockRoles",LocalMember()?.rolesLocked==true?0:1));
        readyButton=StageButton(start,"Ready","READY",1598,548,214,62,()=>Send("ready"),true);
        contractChoice=StageButton(start,"Selected contract","CONTRACT",1368,651,444,64,()=> {
            if (PhotonNetwork.IsMasterClient && Crew?.State!=null) Send("contract",MultiplayerContractManager.NextContractChoice(Crew.State.contractLevel));
        });
        startButton=StageButton(start,"Start contract","START CONTRACT",1340,770,500,64,()=>Send("start"),true);
        retryButton=StageButton(start,"Retry connection","RETRY CONNECTION",1340,770,500,64,()=>Crew.RequestRoomState());
        var teamTitle=LobbyText(start,"Team heading",80,814,700,34,27); teamTitle.text="YOUR TEAM"; teamTitle.color=CrewPaperStyle.Paper;
        memberCount=LobbyText(start,"Crew count",1080,814,220,34,23,TextAlignmentOptions.Right); memberCount.color=CrewPaperStyle.Paper;
        for (int i=0;i<4;i++)
        {
            int row=i; float x=80+i*445;
            memberCards[i]=StagePaper(start,"Crew member "+i,x,865,427,124);
            var card=memberCards[i].transform;
            memberAvatars[i]=CreateCrewAvatar(card,"Customized teammate "+i,12,12,96,96);
            memberLabels[i]=LobbyText(card,"Username "+i,120,12,184,30,24);
            memberMicrophones[i]=CrewVoiceBadge.Attach(LobbyRect(card,"Crew microphone "+i,312,15,103,25));
            memberLabels[i].fontStyle=FontStyles.Bold; memberLabels[i].richText=false;
            memberRoles[i]=LobbyText(card,"Roles "+i,120,49,292,42,20,TextAlignmentOptions.TopLeft);
            memberStates[i]=LobbyText(card,"Readiness "+i,120,99,178,19,17);
            unlockButtons[i]=StageButton(card,"Unlock "+i,"UNLOCK",320,94,93,24,()=> {
                var members=Crew?.State?.members;
                if (members!=null && row<members.Count) Crew.Send(new CrewCommand{action="lockRoles",id=members[row].id,value=0});
            });
            var label=unlockButtons[i].GetComponentInChildren<TMP_Text>();
            label.rectTransform.anchoredPosition=new Vector2(3,-1); label.rectTransform.sizeDelta=new Vector2(87,22);
            label.fontSize=label.fontSizeMax=14; label.fontSizeMin=14;
        }
        lobbyNotice=LobbyText(start,"Selection status",80,1012,1760,32,24,TextAlignmentOptions.Center);
        lobbyNotice.color=CrewPaperStyle.Paper;
        var help=lobbyPages[1].transform;
        StagePaper(help,"Role help paper",480,250,960,540);
        helpDescription=LobbyText(help,"Role help description",800,295,600,370,27,TextAlignmentOptions.TopLeft);
        for (int i=0;i<4;i++) {int role=i;StageButton(help,"Help "+i,MultiplayerContractManager.RoleName(LobbyRoles[i]),510,295+i*92,260,72,()=>ShowRoleHelp(role));}
        ShowRoleHelp(0);
        StageButton(help,"Back to selection","BACK TO ROLES",800,705,600,56,()=>SelectLobbyPage(0),true);
        var inputSystem=EventSystem.current;
        if (inputSystem==null) inputSystem=new GameObject("Crew selection input",typeof(EventSystem)).GetComponent<EventSystem>();
        foreach(var legacy in inputSystem.GetComponents<StandaloneInputModule>()) legacy.enabled=false;
        var input=inputSystem.GetComponent<InputSystemUIInputModule>()??inputSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        if(input.actionsAsset==null) input.AssignDefaultActions(); input.enabled=true;
        inputSystem.SetSelectedGameObject(null);
        SelectLobbyPage(0); desk.enabled=true; lobbyRoot.SetActive(true);
    }

    private static Image StagePaper(Transform parent,string name,float x,float y,float width,float height)
    {
        var paper=LobbyBlock(parent,name,x,y,width,height,CrewPaperStyle.Paper);
        CrewPaperStyle.Card(paper); return paper;
    }
    private static Button StageButton(Transform parent,string name,string caption,float x,float y,float width,float height,UnityAction action,bool primary=false)
    {
        var image=LobbyBlock(parent,name,x,y,width,height,CrewPaperStyle.Paper);
        var button=image.gameObject.AddComponent<Button>(); button.onClick.AddListener(action);
        CrewPaperStyle.ActionButton(button,primary:primary);
        var label=LobbyText(image.transform,"Label",12,6,width-24,height-12,26,TextAlignmentOptions.Center);
        label.text=caption; label.fontStyle=FontStyles.Bold; label.color=CrewPaperStyle.Ink;
        button.navigation=new Navigation{mode=Navigation.Mode.None}; return button;
    }
}
