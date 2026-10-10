using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-2000)]
public sealed class DevCommandsPanel : MonoBehaviour
{
    // Installed before the first scene and retained across scene changes in all builds.
    public static bool Available => true;
    public static bool CommandsAllowed=>Available && !Photon.Pun.PhotonNetwork.InRoom;
    public static bool IsOpen=>instance!=null && instance.open;
    public static bool BlocksInputThisFrame=>IsOpen || closeFrame==Time.frameCount;
    static DevCommandsPanel instance;
    static int closeFrame=-1;
    bool open,oldPaused,oldCursor,oldAudio;
    float oldTime;
    CursorLockMode oldLock;
    GameObject canvasRoot,ownEvents;
    TMP_Text status,fastLabel,resetLabel;
    Image fastBackground;
    float resetUntil;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){instance=null;closeFrame=-1;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if(!Available || instance!=null)return;
        var host=new GameObject("Developer commands");DontDestroyOnLoad(host);host.AddComponent<DevCommandsPanel>();
    }
    void Awake(){if(instance!=null && instance!=this){Destroy(gameObject);return;}instance=this;SceneManager.sceneUnloaded+=OnSceneUnloaded;}
    void OnDestroy(){SceneManager.sceneUnloaded-=OnSceneUnloaded;if(instance==this){Close();instance=null;}}
    void OnDisable(){Close();}
    void OnSceneUnloaded(Scene scene){Close();}
    void Update()
    {
        if(!Available || !Application.isFocused)return;
        var keyboard=Keyboard.current;
        if(keyboard!=null && keyboard.f12Key.wasPressedThisFrame){if(open)Close();else Open();}
        else if(open && keyboard!=null && keyboard.escapeKey.wasPressedThisFrame)Close();
        if(open)
        {
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            RefreshLabels();
        }
    }
    public void Open()
    {
        if(!Available || open)return;
        if(canvasRoot==null)Build();
        oldPaused=PauseManager.isPaused;oldTime=Time.timeScale;oldAudio=AudioListener.pause;
        oldLock=Cursor.lockState;oldCursor=Cursor.visible;
        open=true;resetUntil=0;
        PauseManager.isPaused=true;Time.timeScale=0;AudioListener.pause=true;
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        if(EventSystem.current==null)
        {
            if(ownEvents==null){ownEvents=new GameObject("Dev panel input",typeof(EventSystem),typeof(InputSystemUIInputModule));ownEvents.transform.SetParent(transform,false);}
            ownEvents.SetActive(true);
        }
        canvasRoot.SetActive(true);
        RefreshLabels();
        status.text=CommandsAllowed?"Choose a command. F12 or Escape closes this panel.\nC-Coin testing is local to this session; nothing is sent to PlayFab.":"Commands are unavailable while in a multiplayer room.";
        foreach(var button in canvasRoot.GetComponentsInChildren<Button>())if(button.name!="Close")button.interactable=CommandsAllowed;
    }
    void LateUpdate(){if(open){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
    void RefreshLabels()
    {
        fastLabel.text="FAST DIALOGUE: "+(DevTutorialBypass.FastBossDialogue?"ON":"OFF");
        if (fastBackground != null) fastBackground.color = DevTutorialBypass.FastBossDialogue ? CrewPaperStyle.Gold : CrewPaperStyle.Paper;
        resetLabel.text=resetUntil>Time.unscaledTime?"CONFIRM CAREER RESET":"RESET CURRENT CAREER";
    }
    public void Close()
    {
        if(!open)return;
        open=false;closeFrame=Time.frameCount;resetUntil=0;
        canvasRoot.SetActive(false);if(ownEvents!=null)ownEvents.SetActive(false);
        PauseManager.isPaused=oldPaused;Time.timeScale=oldTime;AudioListener.pause=oldAudio;
        Cursor.lockState=oldLock;Cursor.visible=oldCursor;
        EventSystem.current?.SetSelectedGameObject(null);
    }
    void Execute(Action action,string message,bool close=false)
    {
        if(!CommandsAllowed)return;
        resetUntil=0;if(close)Close();action();if(status!=null)status.text=message;
    }
    void Build()
    {
        canvasRoot=new GameObject("F12 developer panel",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        canvasRoot.transform.SetParent(transform,false);
        var canvas=canvasRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32760;
        var scaler=canvasRoot.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var shade=CCoinShopUI.Rect(canvasRoot.transform,"Input shield",Vector2.zero,Vector2.zero);shade.anchorMin=Vector2.zero;shade.anchorMax=Vector2.one;shade.offsetMin=shade.offsetMax=Vector2.zero;
        shade.gameObject.AddComponent<Image>().color=new Color32(31,21,15,210);
        var paper=Surface(shade,"Command sheet",Vector2.zero,new Vector2(1580,920),CrewPaperStyle.Paper);
        var header=Surface(paper,"Studio tools header",new Vector2(0,367),new Vector2(1444,112),CrewPaperStyle.Ink);
        var shortcut=Surface(header,"F12 badge",new Vector2(-654,0),new Vector2(86,66),CrewPaperStyle.Gold);
        var shortcutLabel=Label(shortcut,"Shortcut","F12",Vector2.zero,new Vector2(80,60),32);shortcutLabel.fontStyle=FontStyles.Bold;
        var title=Label(header,"Title","DEVELOPER COMMANDS",new Vector2(-210,12),new Vector2(700,52),40);
        title.color=CrewPaperStyle.Paper;title.fontStyle=FontStyles.Bold;title.alignment=TextAlignmentOptions.MidlineLeft;
        var subtitle=Label(header,"Hint","CREW / STUDIO TOOLS  -  F12 IN EVERY SCENE",new Vector2(-210,-27),new Vector2(700,28),19);
        subtitle.color=new Color32(223,198,155,255);subtitle.alignment=TextAlignmentOptions.MidlineLeft;
        Button(header,"Close","CLOSE",new Vector2(615,0),new Vector2(178,54),()=>Close());
        var tutorials=Group(paper,"Tutorial controls","TUTORIALS","Pacing, guidance and practice",new Vector2(-488,125),new Vector2(468,346));
        var media=Group(paper,"Budget and media","BUDGET & MEDIA","Current career budget / test footage",new Vector2(0,125),new Vector2(468,346));
        var wallet=Group(paper,"Local test wallet","TEST C-WALLET","Session-only coins, not paid balance",new Vector2(488,125),new Vector2(468,346));
        var contracts=Group(paper,"Contract shortcuts","CONTRACTS","Jump to a contract; this closes the panel",new Vector2(-244,-170),new Vector2(956,216));
        var recovery=Group(paper,"Career recovery","CAREER RESET","Current save only. Account identity stays.",new Vector2(488,-170),new Vector2(468,216));
        var note=Label(tutorials,"Tutorial note","FAST dialogue keeps the tasks active.",new Vector2(0,-136),new Vector2(412,36),18);note.color=CrewPaperStyle.MutedInk;
        note=Label(media,"Media note","Test clip: editor  /  Test card: studio",new Vector2(0,-136),new Vector2(412,30),17);note.color=CrewPaperStyle.MutedInk;
        var walletNote=Surface(wallet,"Test wallet notice",new Vector2(0,-104),new Vector2(412,92),new Color32(255,240,194,255));
        note=Label(walletNote,"Test wallet explanation","LOCAL TEST ONLY\nReal coins and ownership are untouched.",Vector2.zero,new Vector2(388,78),20);note.fontStyle=FontStyles.Bold;
        note=Label(contracts,"Contract names","1 / VASE    2 / GOKE    3 / TERRARI    4 / COFFEE",new Vector2(0,-78),new Vector2(880,24),16);note.color=CrewPaperStyle.MutedInk;
        note=Label(recovery,"Reset warning","Two clicks within 8 seconds to confirm.",new Vector2(0,-82),new Vector2(412,28),17);note.color=new Color32(158,62,39,255);
        Transform[] owners={tutorials,tutorials,tutorials,media,media,wallet,media,media,wallet,contracts,contracts,contracts,contracts,recovery};
        Vector2[] positions={new Vector2(0,64),new Vector2(0,-6),new Vector2(0,-76),new Vector2(-107,64),new Vector2(107,64),new Vector2(0,64),new Vector2(0,-10),new Vector2(0,-86),new Vector2(0,-6),new Vector2(-330,-18),new Vector2(-110,-18),new Vector2(110,-18),new Vector2(330,-18),new Vector2(0,-30)};
        int index=0;
        Button Add(string label,Action action)
        {
            int i=index++;
            bool compact=i==3 || i==4 || (i>=9 && i<=12);
            return Button(owners[i],"Command "+i,label,positions[i],new Vector2(compact?198:412,i>=9?58:54),()=>action(),i==13,i==3 || i==5);
        }
        fastLabel=Add("FAST DIALOGUE",()=>Execute(DevTutorialBypass.ToggleFastBossDialogue,"Dialogue pacing changed.")).GetComponentInChildren<TMP_Text>();
        fastBackground=fastLabel.GetComponentInParent<Button>().targetGraphic as Image;
        Add("DISABLE TUTORIALS",()=>Execute(DevTutorialBypass.DisableTutorials,"Tutorials disabled for this session.",true));
        Add("SKIP TUTORIAL STEP",()=>{
            if(EditorTutorialManager.Instance!=null && EditorTutorialManager.Instance.isActiveAndEnabled)Execute(()=>EditorTutorialManager.Instance.CheatCompleteCurrentStep(),"Tutorial step requested.",true);
            else if(TutorialManager.Instance!=null && TutorialManager.Instance.isActiveAndEnabled)Execute(()=>TutorialManager.Instance.CheatCompleteCurrentStep(),"Tutorial step requested.",true);
            else status.text="No active studio/editor tutorial in this scene.";
        });
        Add("+1,000 B-COINS",()=>Execute(CareerManager.DevAddBudget,"Added 1,000 B-Coins to the current budget."));
        Add("-1,000 B-COINS",()=>Execute(CareerManager.DevRemoveBudget,"Removed up to 1,000 B-Coins. Balance cannot go below zero."));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Add("+100 TEST C-COINS",()=>Execute(()=>CCoinService.Ensure().DevAddCoins(100),"Added 100 TEST C-Coins. Open Profile > Shop to test buying/equipping.\nSession-only test wallet; your real wallet is unchanged."));
#else
        Add("TEST C-COINS / DEV BUILD",()=>status.text="Test C-Coins require a Development Build. Other available developer commands can be used here.");
#endif
        Add("CREATE 12-SECOND CLIP",()=>{var editor=FindObjectOfType<EditorManager>();if(editor!=null)Execute(editor.GenerateCheatClip,"Test clip created.",true);else status.text="Open the editing scene to generate a test clip.";});
        Add("SPAWN TEST SD CARD",()=>{if(TutorialManager.Instance!=null)Execute(()=>TutorialManager.Instance.SpawnCheatSDCard(),"Test SD card requested.",true);else status.text="Enter the studio to spawn a test SD card.";});
        Add("RESTORE REAL C-WALLET",()=>Execute(()=>CCoinService.Ensure().DevEndWallet(),"Discarded test coins, test purchases and test equipment.\nYour real account wallet is restored; sync requires the backend."));
        for(int level=1;level<=4;level++){int target=level;Add("LOAD CONTRACT "+target,()=>Execute(()=>CareerManager.SwitchLevelCheat(target),"Loading contract "+target,true));}
        resetLabel=Add("RESET CURRENT CAREER",()=>{
            if(!CommandsAllowed)return;
            if(resetUntil>Time.unscaledTime)Execute(CareerManager.DevRestartCareer,"Career reset requested.",true);
            else{resetUntil=Time.unscaledTime+8;resetLabel.text="CONFIRM CAREER RESET";status.text="This resets the CURRENT career's progress. Other careers and account identity stay.\nClick CONFIRM CAREER RESET within 8 seconds, or choose another command to cancel.";}
        }).GetComponentInChildren<TMP_Text>();
        var feedback=Surface(paper,"Command feedback paper",new Vector2(0,-358),new Vector2(1444,132),new Color32(246,233,201,255));
        var feedbackHeading=Label(feedback,"Feedback heading","COMMAND FEEDBACK  /  F12 OR ESC TO CLOSE",new Vector2(0,43),new Vector2(1380,25),15);
        feedbackHeading.fontStyle=FontStyles.Bold;feedbackHeading.color=CrewPaperStyle.MutedInk;feedbackHeading.alignment=TextAlignmentOptions.MidlineLeft;
        status=Label(feedback,"Command feedback","",new Vector2(0,-11),new Vector2(1380,78),22);
        status.alignment=TextAlignmentOptions.MidlineLeft;status.lineSpacing=2;
        canvasRoot.SetActive(false);
    }
    static RectTransform Surface(Transform parent,string name,Vector2 position,Vector2 size,Color colour)
    {
        var rect=CCoinShopUI.Rect(parent,name,position,size);
        var image=rect.gameObject.AddComponent<Image>();CrewPaperStyle.Card(image);image.color=colour;image.raycastTarget=false;
        return rect;
    }
    static RectTransform Group(Transform parent,string name,string title,string caption,Vector2 position,Vector2 size)
    {
        var rect=Surface(parent,name,position,size,new Color32(245,231,203,255));
        var heading=Label(rect,"Group title",title,new Vector2(0,size.y*.5f-34),new Vector2(size.x-40,30),24);
        heading.fontStyle=FontStyles.Bold;heading.alignment=TextAlignmentOptions.MidlineLeft;
        var hint=Label(rect,"Group hint",caption,new Vector2(0,size.y*.5f-63),new Vector2(size.x-40,24),17);
        hint.color=CrewPaperStyle.MutedInk;hint.alignment=TextAlignmentOptions.MidlineLeft;
        return rect;
    }
    static TMP_Text Label(Transform parent,string name,string text,Vector2 pos,Vector2 size,int font)
    {
        var label=CCoinShopUI.Rect(parent,name,pos,size).gameObject.AddComponent<TextMeshProUGUI>();label.font=TMP_Settings.defaultFontAsset;
        label.text=text;label.color=CrewPaperStyle.Ink;label.fontSize=font;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
        label.enableAutoSizing=true;label.fontSizeMin=font-3;label.fontSizeMax=font;label.richText=false;return label;
    }
    static Button Button(Transform parent,string name,string caption,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action,bool danger=false,bool primary=false)
    {
        var image=CCoinShopUI.Rect(parent,name,pos,size).gameObject.AddComponent<Image>();
        var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(action);
        var text=Label(image.transform,"Caption",caption,Vector2.zero,size-new Vector2(24,12),24);text.fontStyle=FontStyles.Bold;
        CrewPaperStyle.ActionButton(button,danger,primary);return button;
    }
}
