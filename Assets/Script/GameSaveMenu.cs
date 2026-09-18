using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameSaveMenu : MonoBehaviour
{
    private GameSaveManager saves;
    [SerializeField] private RectTransform rows;
    [SerializeField] private TMP_Text status;
    [SerializeField] private TMP_InputField nameInput;
    private Button create;
    private GameObject newGameDialog;
    private GameSaveSlot selected;
    private bool joining;
    [SerializeField] private Transform paper;
    private SaveLoadPanelHost sourceHost;

    [SerializeField] private GameObject createDialogLayout, deleteDialogLayout;
    [SerializeField] private TMP_Text deleteName;
    [SerializeField] private GameSetupMenu createSetup;
    private void BindMainButtons()
    {
        Bind(paper.Find("Close"),CloseMenu);
        Bind(paper.Find("JOIN"),()=>{
            if(sourceHost==null)return;
            var joinPanel=sourceHost.transform.parent.Find("join");if(joinPanel==null)return;
            joining=true;sourceHost.gameObject.SetActive(false);joinPanel.gameObject.SetActive(true);
            sourceHost.transform.parent.gameObject.SetActive(true);CloseMenu();
        });
        Bind(paper.Find("Start selected save"),()=>{if(selected!=null&&!saves.Syncing)saves.StartGame(selected,false);});
        Bind(paper.Find("Delete selected save"),ShowDeleteDialog);
    }
    private static void Bind(Transform target,UnityEngine.Events.UnityAction action)
    {
        var button=target.GetComponent<UnityEngine.UI.Button>();
        button.onClick.RemoveAllListeners();button.onClick.AddListener(action);
    }
    private void CloseMenu()
    {
        CloseDialog();
        if(saves!=null)saves.Changed-=Refresh;
        gameObject.SetActive(false);
        if(!joining&&sourceHost!=null&&sourceHost.transform.parent!=null)sourceHost.transform.parent.gameObject.SetActive(false);
    }
    private void ShowDeleteDialog()
    {
        if(selected==null||saves.Syncing||newGameDialog!=null)return;
        if(deleteDialogLayout==null) BuildDeleteDialog();
        var chosen=selected;newGameDialog=deleteDialogLayout;newGameDialog.SetActive(true);deleteName.text=chosen.name;
        var box=deleteDialogLayout.transform.Find("Creation style dialog");
        Bind(box.Find("CANCEL"),CloseDialog);
        Bind(box.Find("DELETE"),()=>{chosen.values.RemoveAll(v=>v.key=="SaveDeleted");chosen.values.Add(new GameSaveValue{key="SaveDeleted",integer=1});saves.Repository.Commit(chosen);selected=null;CloseDialog();Refresh();saves.SyncCloud();});
    }
    private void BuildDeleteDialog()
    {
        var box=CreateFolderDialog("Delete this saved game?");
        deleteDialogLayout=newGameDialog;deleteDialogLayout.name="Delete Save Dialog";
        deleteName=Text(box,"Selected save",new Vector2(.18f,.42f),new Vector2(.8f,.64f),30);deleteName.richText=false;
        Button(box,"CANCEL",new Vector2(.2f,.17f),new Vector2(.45f,.3f),CloseDialog);
        Button(box,"DELETE",new Vector2(.53f,.17f),new Vector2(.78f,.3f),()=>{});
        deleteDialogLayout.SetActive(false);newGameDialog=null;
    }
#if UNITY_EDITOR
    public void BakeHierarchyUI()
    {
        sourceHost=FindObjectsOfType<SaveLoadPanelHost>(true).FirstOrDefault(host=>host.gameObject.scene==gameObject.scene);
        if(createSetup==null && createDialogLayout!=null && sourceHost!=null && sourceHost.transform.Find("createpanel")!=null)
        {
            DestroyImmediate(createDialogLayout);createDialogLayout=null;newGameDialog=null;
        }
        if(paper==null) Build();
        if(deleteDialogLayout==null) BuildDeleteDialog();
        if(createDialogLayout==null) {ShowNewGameDialog();CloseDialog();}
        gameObject.SetActive(false);
    }
#endif

    public static void Show(GameSaveManager manager)
    {
        var existing = FindObjectOfType<GameSaveMenu>(true);
        if (existing != null)
        {
            if(existing.gameObject.activeSelf)return;
            existing.saves=manager;existing.joining=false;
            existing.sourceHost=FindObjectOfType<SaveLoadPanelHost>(true);
            if(existing.sourceHost!=null&&existing.sourceHost.transform.parent!=null)existing.sourceHost.transform.parent.gameObject.SetActive(false);
            existing.BindMainButtons();existing.gameObject.SetActive(true);
            manager.Changed-=existing.Refresh;manager.Changed+=existing.Refresh;existing.Refresh();return;
        }
        var go = new GameObject("Saved Games", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var menu = go.AddComponent<GameSaveMenu>();
        menu.saves = manager;
        menu.Build();
        menu.BindMainButtons();
        manager.Changed += menu.Refresh;
        menu.Refresh();
    }
    private void OnDestroy()
    {
        if(saves!=null)saves.Changed-=Refresh;
        var host=FindObjectOfType<SaveLoadPanelHost>();
        if(!joining&&host!=null&&host.transform.parent!=null)host.transform.parent.gameObject.SetActive(false);
    }
    private void Update()
    {
        if(UnityEngine.InputSystem.Keyboard.current==null||!UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)return;
        if(newGameDialog!=null)CloseDialog();else CloseMenu();
    }
    private Button Artwork(string name,string key,Transform parent,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
    {
        var r=Rect(name,parent,min,max);var image=r.gameObject.AddComponent<Image>();ExportUIArt.Apply(image,key);
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;if(action!=null)b.onClick.AddListener(action);return b;
    }
    private void Build()
    {
        sourceHost=FindObjectOfType<SaveLoadPanelHost>(true);
        if(Application.isPlaying&&sourceHost!=null&&sourceHost.transform.parent!=null)sourceHost.transform.parent.gameObject.SetActive(false);
        var backdrop=Rect("PLAY artwork",transform,Vector2.zero,Vector2.one);
        ExportUIArt.Apply(backdrop.gameObject.AddComponent<Image>(),"playFolder");paper=backdrop;
        Artwork("Close","close",paper,new Vector2(.905f,.783f),new Vector2(.949f,.862f),CloseMenu);
        var join=Rect("JOIN",paper,new Vector2(.277f,.867f),new Vector2(.38f,.94f));join.gameObject.AddComponent<Image>().color=Color.clear;
        join.gameObject.AddComponent<UnityEngine.UI.Button>();
        var viewport=Rect("Save grid viewport",paper,new Vector2(.158f,.32f),new Vector2(.845f,.79f));
        viewport.gameObject.AddComponent<Image>().color=Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        rows=Rect("Save cards",viewport,new Vector2(0,1),Vector2.one);rows.pivot=new Vector2(.5f,1);
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=rows;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=45;
        Artwork("Start selected save","playStart",paper,new Vector2(.326f,.148f),new Vector2(.468f,.251f),()=>{});
        Artwork("Delete selected save","playDelete",paper,new Vector2(.521f,.148f),new Vector2(.664f,.251f),()=>{});
        status=Text(paper,"",new Vector2(.16f,.265f),new Vector2(.8f,.305f),20);
    }
    private void Refresh()
    {
        if(rows==null)return;
        foreach(Transform child in rows){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        var slots=saves.Repository.Slots.Where(s=>s.Int("SaveDeleted",0)==0).OrderByDescending(s=>s.updatedUtc).ToList();
        int rowCount=(slots.Count+3)/3;
        float height=Math.Max(1,rowCount)*295f;
        rows.sizeDelta=new Vector2(0,height);
        status.text=saves.Status;
        if(selected==null||!slots.Contains(selected))selected=slots.FirstOrDefault();
        for(int i=0;i<slots.Count;i++)
        {
            var slot=slots[i];float left=(i%3)*.35f;float top=1-(i/3)*295f/height;
            var cardRect=Rect("Save "+slot.id,rows,new Vector2(left,top-265f/height),new Vector2(left+.30f,top));
            var border=cardRect.gameObject.AddComponent<Image>();border.color=slot==selected?new Color32(208,147,42,255):new Color32(210,188,143,255);
            var card=cardRect.gameObject.AddComponent<Button>();card.targetGraphic=border;card.onClick.AddListener(()=>{selected=slot;Refresh();});
            var surface=Rect("Cream surface",cardRect,new Vector2(.012f,.018f),new Vector2(.988f,.982f));surface.gameObject.AddComponent<Image>().color=new Color32(255,245,217,255);
            surface.GetComponent<Image>().raycastTarget=false;
            var footer=Rect("Name band",surface,Vector2.zero,new Vector2(1,.23f));footer.gameObject.AddComponent<Image>().color=new Color32(239,215,168,255);footer.GetComponent<Image>().raycastTarget=false;
            var name=Text(card.transform,slot.name,new Vector2(.05f,.025f),new Vector2(.95f,.21f),23);name.richText=false;
            Text(card.transform,slot==selected?"SELECTED":"SELECT SAVE",new Vector2(.05f,.30f),new Vector2(.95f,.58f),27).alignment=TextAlignmentOptions.Center;
            Text(card.transform,"Level "+slot.Level+"  ·  "+slot.Money.ToString("N0")+" B",new Vector2(.05f,.68f),new Vector2(.95f,.87f),23);
        }
        float plusLeft=(slots.Count%3)*.35f;float plusTop=1-(slots.Count/3)*295f/height;
        create=Artwork("Create save","playCreate",rows,new Vector2(plusLeft,plusTop-265f/height),new Vector2(plusLeft+.30f,plusTop),ShowNewGameDialog);
        create.interactable=!saves.Syncing;
    }
    private Transform CreateFolderDialog(string title)
    {
        var shade=Rect("New Game Dialog",transform,Vector2.zero,Vector2.one);newGameDialog=shade.gameObject;shade.gameObject.AddComponent<Image>().color=new Color(.25f,.16f,.07f,.45f);
        var box=Rect("Creation style dialog",shade,new Vector2(.3f,.29f),new Vector2(.7f,.71f));
        var background=box.gameObject.AddComponent<Image>();
        var original=sourceHost!=null?sourceHost.transform.Find("createpanel"):null;
        var artwork=original!=null?original.GetComponent<Image>():null;
        if(artwork!=null){background.sprite=artwork.sprite;background.type=artwork.type;background.color=artwork.color;}
        else background.color=new Color32(255,244,210,255);
        Text(box,title,new Vector2(.17f,.66f),new Vector2(.85f,.8f),32);return box;
    }
    private void CloseDialog(){if(newGameDialog!=null)newGameDialog.SetActive(false);newGameDialog=null;}
    private void CreateGame()
    {
        if (saves.Syncing) return;
        try { saves.StartGame(saves.CreateGame(nameInput.text), true); }
        catch (Exception) { status.text = "Could not create a save. Check available disk space and try again."; }
    }
    private void ShowNewGameDialog()
    {
        if(newGameDialog!=null)return;
        if(createDialogLayout!=null)
        {
            newGameDialog=createDialogLayout;newGameDialog.SetActive(true);
            if(createSetup!=null)
            {
                foreach(var button in createSetup.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    Bind(button.transform,button.name=="Button"?(UnityEngine.Events.UnityAction)createSetup.OnCreateButtonPressed:CloseDialog);
                createSetup.singlePlayerToggle.isOn=true;
                createSetup.gameNameInput.text="Game "+(saves.Repository.Slots.Count(s=>s.Int("SaveDeleted",0)==0)+1);
            }
            else
            {
                var existingBox=createDialogLayout.transform.Find("Creation style dialog");
                Bind(existingBox.Find("START GAME"),CreateGame);Bind(existingBox.Find("CANCEL"),CloseDialog);
                nameInput.text="Game "+(saves.Repository.Slots.Count+1);
            }
            return;
        }
        var original=sourceHost!=null?sourceHost.transform.Find("createpanel"):null;
        if(original!=null)
        {
            var shade=Rect("New Game Dialog",transform,Vector2.zero,Vector2.one);
            newGameDialog=shade.gameObject;shade.gameObject.AddComponent<Image>().color=new Color(0,0,0,.6f);
            var panel=Instantiate(original.gameObject,shade,false);panel.SetActive(true);
            var rect=panel.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;rect.localScale=Vector3.one;
            var setup=shade.gameObject.AddComponent<GameSetupMenu>();createSetup=setup;createDialogLayout=shade.gameObject;
            setup.gameNameInput=panel.GetComponentInChildren<TMP_InputField>(true);
            setup.singlePlayerToggle=panel.GetComponentsInChildren<Toggle>(true).First(t=>t.name=="Single");
            setup.multiPlayerToggle=panel.GetComponentsInChildren<Toggle>(true).First(t=>t.name=="Multi");
            setup.errorText=Text(shade,"",new Vector2(.32f,.23f),new Vector2(.68f,.29f),23);
            var originalSetup=sourceHost.GetComponentInParent<GameSetupMenu>();
            if(originalSetup!=null){setup.singlePlayerScene=originalSetup.singlePlayerScene;setup.multiplayerScene=originalSetup.multiplayerScene;}
            foreach(var button in panel.GetComponentsInChildren<Button>(true))
            {
                button.onClick=new Button.ButtonClickedEvent();
                if(button.name=="Button")button.onClick.AddListener(setup.OnCreateButtonPressed);
                else button.onClick.AddListener(CloseDialog);
            }
            setup.singlePlayerToggle.isOn=true;
            setup.gameNameInput.text="Game "+(saves!=null?saves.Repository.Slots.Count(s=>s.Int("SaveDeleted",0)==0)+1:1);
            return;
        }
        var box=CreateFolderDialog("NAME YOUR NEW GAME");createDialogLayout=newGameDialog;
        Text(box,"Your commercial checkpoints save automatically.",new Vector2(.18f,.48f),new Vector2(.84f,.62f),24);
        var field=Rect("Save name",box,new Vector2(.18f,.35f),new Vector2(.84f,.46f));field.gameObject.AddComponent<Image>().color=new Color32(237,213,167,255);
        var input=field.gameObject.AddComponent<TMP_InputField>();var text=Text(field,"",new Vector2(.03f,0),new Vector2(.97f,1),25);text.richText=false;input.textViewport=field;input.textComponent=text;input.characterLimit=40;input.text="Game "+(saves!=null?saves.Repository.Slots.Count+1:1);nameInput=input;
        Button(box,"START GAME",new Vector2(.53f,.17f),new Vector2(.8f,.3f),CreateGame);
        Button(box,"CANCEL",new Vector2(.19f,.17f),new Vector2(.46f,.3f),CloseDialog);
        if(Application.isPlaying) input.Select();
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    private static TextMeshProUGUI Text(Transform parent, string value, Vector2 min, Vector2 max, float size)
    {
        var text = Rect("Label", parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value; text.fontSize = size; text.color = new Color32(75,43,19,255);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableAutoSizing = true; text.fontSizeMin = size * .75f; text.fontSizeMax = size;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
    private static Button Button(Transform parent, string title, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
    {
        var rect = Rect(title, parent, min, max);
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color32(30, 87, 55, 255);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var text = Text(rect, title, new Vector2(.03f, 0), new Vector2(.97f, 1), 23);
        text.color=Color.white;ExportUIArt.Apply(image,"blueButton");
        text.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener(action);
        return button;
    }
}

