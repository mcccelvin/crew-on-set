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
    public static bool Available {
        get {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }
    }
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
        shade.gameObject.AddComponent<Image>().color=new Color(0,0,0,.85f);
        var paper=CCoinShopUI.Rect(shade,"Command sheet",Vector2.zero,new Vector2(1580,920));paper.gameObject.AddComponent<Image>().color=new Color32(248,238,208,255);
        Label(paper,"Title","DEVELOPER COMMANDS",new Vector2(0,380),new Vector2(1260,76),46);
        Label(paper,"Hint","F12 MENU  /  EDITOR & DEVELOPMENT BUILDS ONLY",new Vector2(0,319),new Vector2(1340,42),23);
        Button(paper,"Close","CLOSE",new Vector2(641,390),new Vector2(200,60),()=>Close());
        int index=0;
        Button Add(string label,Action action){int i=index++;return Button(paper,"Command "+i,label,new Vector2(-485+(i%3)*485,226-(i/3)*106),new Vector2(450,82),()=>action());}
        fastLabel=Add("FAST DIALOGUE",()=>Execute(DevTutorialBypass.ToggleFastBossDialogue,"Dialogue pacing changed.")).GetComponentInChildren<TMP_Text>();
        Add("DISABLE TUTORIALS",()=>Execute(DevTutorialBypass.DisableTutorials,"Tutorials disabled for this session.",true));
        Add("SKIP TUTORIAL STEP",()=>{
            if(EditorTutorialManager.Instance!=null && EditorTutorialManager.Instance.isActiveAndEnabled)Execute(()=>EditorTutorialManager.Instance.CheatCompleteCurrentStep(),"Tutorial step requested.",true);
            else if(TutorialManager.Instance!=null && TutorialManager.Instance.isActiveAndEnabled)Execute(()=>TutorialManager.Instance.CheatCompleteCurrentStep(),"Tutorial step requested.",true);
            else status.text="No active studio/editor tutorial in this scene.";
        });
        Add("+1,000 B-COINS",()=>Execute(CareerManager.DevAddBudget,"Added 1,000 B-Coins to the current budget."));
        Add("-1,000 B-COINS",()=>Execute(CareerManager.DevRemoveBudget,"Removed up to 1,000 B-Coins. Balance cannot go below zero."));
        Add("+100 TEST C-COINS",()=>Execute(()=>CCoinService.Ensure().DevAddCoins(100),"Added 100 TEST C-Coins. Open Profile > Shop to test buying/equipping.\nSession-only test wallet; your real wallet is unchanged."));
        Add("CREATE 12-SECOND CLIP",()=>{var editor=FindObjectOfType<EditorManager>();if(editor!=null)Execute(editor.GenerateCheatClip,"Test clip created.",true);else status.text="Open the editing scene to generate a test clip.";});
        Add("SPAWN TEST SD CARD",()=>{if(TutorialManager.Instance!=null)Execute(()=>TutorialManager.Instance.SpawnCheatSDCard(),"Test SD card requested.",true);else status.text="Enter the studio to spawn a test SD card.";});
        Add("RESTORE REAL C-WALLET",()=>Execute(()=>CCoinService.Ensure().DevEndWallet(),"Discarded test coins, test purchases and test equipment.\nYour real account wallet is restored; sync requires the backend."));
        for(int level=1;level<=4;level++){int target=level;Add("LOAD CONTRACT "+target,()=>Execute(()=>CareerManager.SwitchLevelCheat(target),"Loading contract "+target,true));}
        resetLabel=Add("RESET CURRENT CAREER",()=>{
            if(!CommandsAllowed)return;
            if(resetUntil>Time.unscaledTime)Execute(CareerManager.DevRestartCareer,"Career reset requested.",true);
            else{resetUntil=Time.unscaledTime+8;resetLabel.text="CONFIRM CAREER RESET";status.text="This resets the CURRENT career's progress. Other careers and account identity stay.\nClick CONFIRM CAREER RESET within 8 seconds, or choose another command to cancel.";}
        }).GetComponentInChildren<TMP_Text>();
        status=Label(paper,"Command feedback","",new Vector2(0,-350),new Vector2(1450,130),25);
        canvasRoot.SetActive(false);
    }
    static TMP_Text Label(Transform parent,string name,string text,Vector2 pos,Vector2 size,int font)
    {
        var label=CCoinShopUI.Rect(parent,name,pos,size).gameObject.AddComponent<TextMeshProUGUI>();label.font=TMP_Settings.defaultFontAsset;
        label.text=text;label.color=new Color32(50,37,30,255);label.fontSize=font;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
        label.enableAutoSizing=true;label.fontSizeMin=font-3;label.fontSizeMax=font;label.richText=false;return label;
    }
    static Button Button(Transform parent,string name,string caption,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action)
    {
        var image=CCoinShopUI.Rect(parent,name,pos,size).gameObject.AddComponent<Image>();ExportUIArt.Apply(image,"blueButton");
        var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(action);
        var text=Label(image.transform,"Caption",caption,Vector2.zero,size-new Vector2(28,16),24);text.color=Color.white;return button;
    }
}
