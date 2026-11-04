using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The waiting room uses the existing main-menu folder. Roles open only after
// the host advances the authoritative room state.
public sealed class MultiplayerWaitingLobby : MonoBehaviour
{
    private GameObject view;
    private TMP_Text code, count, contract, budget, status, voiceHint;
    private readonly TMP_Text[] names = new TMP_Text[4], states = new TMP_Text[4];
    private readonly Image[] cards = new Image[4];
    private readonly CrewVoiceBadge[] microphones = new CrewVoiceBadge[4];
    private SharedOptionsPanel options;
    private Button ready, start, contractButton;
    private MultiplayerRoleManager Crew => MultiplayerRoleManager.Instance;
    private static readonly Color32 Ink = new Color32(65,40,20,255);
    private static readonly Color32 Tan = new Color32(193,164,111,255);
    private static readonly Color32 Paper = new Color32(255,244,210,255);

    private void Awake()
    {
        var folder = GameSaveMenu.OpenMultiplayerLobby();
        if (folder == null) { Debug.LogError("The main-menu folder could not be opened for the crew lobby."); return; }
        view = Rect(folder,"Multiplayer folder content",Vector2.zero,Vector2.one).gameObject;
        var content = view.transform;
        var title = Text(content,"Title","CREW LOBBY",.17f,.735f,.64f,.795f,36);
        title.fontStyle = FontStyles.Bold;
        Action(content,"Lobby options","OPTIONS",.68f,.742f,.83f,.793f,()=>{
            if(options==null)options=new SharedOptionsPanel(view.transform,()=>{Cursor.lockState=CursorLockMode.None;Cursor.visible=true;});
            options.Open();
        });
        Box(content,"Heading rule",.17f,.718f,.83f,.721f,Tan);
        Text(content,"Room heading","ROOM CODE",.17f,.66f,.445f,.70f,24);
        Text(content,"Crew heading","YOUR CREW",.49f,.66f,.71f,.70f,24);
        count = Text(content,"Count","0 / 4",.73f,.66f,.83f,.70f,24);
        count.alignment = TextAlignmentOptions.Right;

        var codeBox = Box(content,"Room code paper",.17f,.52f,.445f,.64f,new Color32(243,222,175,255));
        Border(codeBox);
        var invite = Text(content,"Invite label","SHARE WITH YOUR CREW",.183f,.598f,.432f,.63f,19);
        invite.alignment = TextAlignmentOptions.Center;
        code = Text(content,"Code","CONNECTING",.183f,.535f,.432f,.597f,44);
        code.characterSpacing = 7; code.alignment = TextAlignmentOptions.Center;

        Text(content,"Contract heading","CONTRACT",.17f,.46f,.445f,.50f,24);
        contractButton = Action(content,"Contract","CONTRACT",.17f,.385f,.445f,.45f,() => {
            if (PhotonNetwork.IsMasterClient && Crew?.State != null)
                Crew.Send(new CrewCommand { action="contract",value=MultiplayerContractManager.NextContractChoice(Crew.State.contractLevel) });
        });
        contract = contractButton.GetComponentInChildren<TMP_Text>();
        budget = Text(content,"Budget","",.17f,.333f,.445f,.37f,23);
        voiceHint=Text(content,"Voice controls","",.17f,.312f,.83f,.332f,18);
        voiceHint.alignment=TextAlignmentOptions.Center;voiceHint.color=CrewPaperStyle.MutedInk;
        for (int i=0;i<4;i++)
        {
            float top=.65f-i*.083f, bottom=top-.068f;
            cards[i]=Box(content,"Crew member "+i,.49f,bottom,.83f,top,Paper);
            Border(cards[i]); UISurfaceDepth.Apply(cards[i]);
            names[i]=Text(content,"Player "+i,"OPEN SLOT",.502f,bottom+.013f,.708f,top-.013f,25);
            names[i].richText=false;
            states[i]=Text(content,"Ready "+i,"WAITING",.715f,bottom+.032f,.818f,top-.005f,21);
            states[i].alignment=TextAlignmentOptions.Right;
            microphones[i]=CrewVoiceBadge.Attach(Rect(content,"Crew microphone "+i,new Vector2(.715f,bottom+.003f),new Vector2(.818f,bottom+.029f)));
        }
        status=Text(content,"Lobby status","Connecting to your crew…",.17f,.268f,.83f,.31f,23);
        status.alignment=TextAlignmentOptions.Center;
        ready=Action(content,"Ready","READY",.26f,.148f,.41f,.251f,
            () => Crew?.Send(new CrewCommand{action="ready"}),"blueButton");
        start=Action(content,"Start","START",.425f,.148f,.575f,.251f,
            () => Crew?.Send(new CrewCommand{action="start"}),"greenButton");
        Action(content,"Leave","LEAVE",.59f,.148f,.74f,.251f,() => {
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        },"redButton");
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
    }

    private void Update()
    {
        if (view==null || !PhotonNetwork.InRoom) return;
        if(options!=null && options.IsOpen)
        {
            if(UnityEngine.InputSystem.Keyboard.current!=null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)options.Close(false);
        }
        var session=Crew?.State;
        if (session!=null && session.phase!="waiting")
        {
            ready.interactable=start.interactable=contractButton.interactable=false;
            status.text="Opening role selection…"; return;
        }
        Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
        code.text=PhotonNetwork.CurrentRoom.Name;
        voiceHint.text=CrewVoiceChat.Instance!=null?CrewVoiceChat.Instance.Instruction:"";
        count.text=PhotonNetwork.CurrentRoom.PlayerCount+" / 4";
        var local=session?.members.Find(m=>m.id==PhotonNetwork.LocalPlayer.ActorNumber);
        for (int i=0;i<4;i++)
        {
            var member=session!=null && i<session.members.Count ? session.members[i] : null;
            cards[i].color=member==local && member!=null ? new Color32(255,248,227,255) : Paper;
            if (member==null)
            {
                names[i].text="OPEN SLOT"; states[i].text="WAITING";microphones[i].Bind(null);
                names[i].color=states[i].color=CrewPaperStyle.MutedInk; continue;
            }
            PhotonNetwork.CurrentRoom.Players.TryGetValue(member.id,out var player);
            microphones[i].Bind(player);
            string name=CrewPlayerIdentity.Username(player);
            names[i].text=name+(PhotonNetwork.MasterClient.ActorNumber==member.id?" (HOST)":"");
            names[i].color=Ink;
            states[i].text=member.ready?"READY":"NOT READY";
            states[i].color=member.ready?new Color32(39,104,69,255):CrewPaperStyle.MutedInk;
        }
        ready.interactable=local!=null;
        ready.GetComponentInChildren<TMP_Text>().text=local?.ready==true?"NOT READY":"READY";
        start.interactable=PhotonNetwork.IsMasterClient && MultiplayerContractManager.CanBeginRoleSelection(session);
        contractButton.interactable=session!=null && PhotonNetwork.IsMasterClient;
        contract.text=session!=null?MultiplayerContractManager.Title(session.contractLevel)+(PhotonNetwork.IsMasterClient?"  ›":""):"CONTRACT";
        budget.text=session!=null?"SHARED BUDGET  ·  B "+session.budget.ToString("N0"):"";
        status.text=local==null?"Connecting to your crew…":session.members.Count<2?"Invite at least one friend to begin.":
            !local.ready?"Press READY when you are set.":MultiplayerContractManager.CanBeginRoleSelection(session)?
            (PhotonNetwork.IsMasterClient?"Everyone is ready. Press START to choose roles.":"Everyone is ready. Waiting for the host."):"Waiting for everyone to ready up.";
    }

    private void OnDestroy() { if (view!=null) Destroy(view); }

    private static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max)
    {
        var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent,false); rt.anchorMin=min; rt.anchorMax=max;
        rt.offsetMin=rt.offsetMax=Vector2.zero; return rt;
    }
    private static Image Box(Transform parent,string name,float left,float bottom,float right,float top,Color color)
    {
        var image=Rect(parent,name,new Vector2(left,bottom),new Vector2(right,top)).gameObject.AddComponent<Image>();
        image.color=color; image.raycastTarget=false; return image;
    }
    private static void Border(Image image)
    {
        var border=image.gameObject.AddComponent<Outline>();
        border.effectColor=Tan; border.effectDistance=new Vector2(2,-2);
    }
    private static TextMeshProUGUI Text(Transform parent,string name,string value,float left,float bottom,float right,float top,float size)
    {
        var label=Rect(parent,name,new Vector2(left,bottom),new Vector2(right,top)).gameObject.AddComponent<TextMeshProUGUI>();
        label.font=Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF")??TMP_Settings.defaultFontAsset;
        label.text=value; label.fontSize=label.fontSizeMax=size; label.fontSizeMin=size*.8f;
        label.enableAutoSizing=true; label.enableWordWrapping=false; label.color=Ink;
        label.alignment=TextAlignmentOptions.MidlineLeft; label.raycastTarget=false;
        label.overflowMode=TextOverflowModes.Ellipsis; return label;
    }
    private static Button Action(Transform parent,string name,string value,float left,float bottom,float right,float top,
        UnityEngine.Events.UnityAction callback,string art=null)
    {
        var image=Box(parent,name,left,bottom,right,top,Paper);
        if (art==null) { Border(image); UISurfaceDepth.Apply(image); }
        else RoleSelectionUI.LobbyButtonArt(image,art);
        image.raycastTarget=true;
        var button=image.gameObject.AddComponent<Button>(); button.onClick.AddListener(callback); button.targetGraphic=image;
        var label=Text(image.transform,"Label",value,.055f,.10f,.945f,.90f,art==null?28:40);
        label.alignment=TextAlignmentOptions.Center; label.fontStyle=FontStyles.Bold;
        if (art!=null) ExportUIArt.OutlineText(label);
        var colors=button.colors; colors.normalColor=Color.white; colors.highlightedColor=new Color(1,.95f,.82f);
        colors.pressedColor=new Color(.8f,.74f,.64f); colors.selectedColor=Color.white;
        colors.disabledColor=new Color(.62f,.65f,.68f,1); button.colors=colors;
        button.navigation=new Navigation {mode=Navigation.Mode.None};
        return button;
    }
}
