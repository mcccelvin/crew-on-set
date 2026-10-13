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
    private int selectedContract = 1;
    private bool joining;
    private bool closingMenu;
    [SerializeField] private Transform paper;
    private SaveLoadPanelHost sourceHost;

    [SerializeField] private GameObject createDialogLayout, deleteDialogLayout;
    [SerializeField] private TMP_Text deleteName;
    private TMP_Text deleteDetails, deleteError;
    private Button deleteConfirm;
    [SerializeField] private GameSetupMenu createSetup;
    private void BindMainButtons()
    {
        var sync = paper.Find("Retry cloud sync");
        if (sync != null) sync.gameObject.SetActive(false); // Retire older authored copies; sync is automatic.
        Bind(paper.Find("Close"),CloseMenu);
        Bind(paper.Find("JOIN"),()=>{
            if(sourceHost==null)return;
            var joinPanel=sourceHost.transform.parent.Find("join");if(joinPanel==null)return;
            joining=true;sourceHost.gameObject.SetActive(false);joinPanel.gameObject.SetActive(true);
            sourceHost.transform.parent.gameObject.SetActive(true);CloseMenu();
        });
        Bind(paper.Find("Start selected save"),StartContract);
        var deleteButton = paper.Find("Delete selected save");
        if (deleteButton != null) deleteButton.gameObject.SetActive(false);
        var startButton = paper.Find("Start selected save").GetComponent<Button>();
        StyleSaveAction(startButton, "START", false, .495f);
    }
    private void StartContract()
    {
        if (saves.Syncing) return;
        if (selectedContract == 6) { saves.StartTrainingRoom(); return; }
        try
        {
            // Only the first play creates a career. Replays keep its identity and rewards.
            if (selected == null) selected = saves.CreateGame("My contracts");
            saves.StartGame(selected, false, selectedContract);
        }
        catch (Exception) { status.text = "Could not open this contract. Please try again."; }
    }
    private static TMP_FontAsset saveActionFont;
    private static Material saveActionMaterial;
    private static void StyleSaveAction(Button button, string caption, bool destructive, float centre)
    {
        // Both captions are live text on blank artwork; baked START lettering
        // cannot share font metrics or padding with a generated DELETE caption.
        StyleDeleteButton(button, caption, destructive);
        var rect = (RectTransform)button.transform;
        rect.anchorMin = new Vector2(centre - .0715f, .148f);
        rect.anchorMax = new Vector2(centre + .0715f, .251f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        if (saveActionFont == null)
            saveActionFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF") ?? TMP_Settings.defaultFontAsset;
        if (saveActionMaterial == null && saveActionFont != null)
        {
            saveActionMaterial = new Material(saveActionFont.material) { name = "Save actions outlined lettering" };
            saveActionMaterial.EnableKeyword("OUTLINE_ON");
            saveActionMaterial.SetColor("_FaceColor", Color.white);
            saveActionMaterial.SetColor("_OutlineColor", Color.black);
            saveActionMaterial.SetFloat("_OutlineWidth", .22f);
            saveActionMaterial.SetFloat("_OutlineSoftness", 0);
        }
        var label = button.GetComponentInChildren<TMP_Text>(true);
        label.font = saveActionFont;
        label.fontSharedMaterial = saveActionMaterial;
        label.fontStyle = FontStyles.Bold;
        label.fontSize = label.fontSizeMax = 40;
        label.fontSizeMin = 40;
        label.enableAutoSizing = false;
        label.enableWordWrapping = false;
        label.characterSpacing = 0;
        label.margin = Vector4.zero;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(18, 10);
        label.rectTransform.offsetMax = new Vector2(-18, -10);
        label.UpdateMeshPadding();
    }
    private static void Bind(Transform target,UnityEngine.Events.UnityAction action)
    {
        var button=target.GetComponent<UnityEngine.UI.Button>();
        button.onClick.RemoveAllListeners();button.onClick.AddListener(action);
    }
    private void CloseMenu()
    {
        if (closingMenu) return;
        closingMenu = true;
        CloseDialog();
        if(saves!=null)saves.Changed-=Refresh;
        UITransition.Hide(gameObject, () =>
        {
            closingMenu = false;
            if(!joining&&sourceHost!=null&&sourceHost.transform.parent!=null)sourceHost.transform.parent.gameObject.SetActive(false);
        });
    }
    private void ShowDeleteDialog()
    {
        if(selected==null||saves.Syncing||newGameDialog!=null)return;
        if(deleteDialogLayout==null || deleteDialogLayout.transform.Find("Creation style dialog/Blank delete panel interior")==null)
        {
            if(deleteDialogLayout!=null) deleteDialogLayout.SetActive(false);
            BuildDeleteDialog();
        }
        var chosen=selected;newGameDialog=deleteDialogLayout;
        newGameDialog.transform.SetAsLastSibling();
        UITransition.Show(newGameDialog);deleteName.text=chosen.name;
        var box=deleteDialogLayout.transform.Find("Creation style dialog");
        if(deleteDetails==null) deleteDetails=box.Find("Save details").GetComponent<TMP_Text>();
        if(deleteError==null) deleteError=box.Find("Delete error").GetComponent<TMP_Text>();
        deleteConfirm=box.Find("DELETE").GetComponent<Button>();
        deleteDetails.text="LEVEL "+chosen.Level+"   |   "+chosen.Money.ToString("N0")+" B-COINS";
        deleteError.text="";
        deleteConfirm.interactable=true;
        Bind(box.Find("CANCEL"),CloseDialog);
        Bind(box.Find("DELETE"),()=>ConfirmDelete(chosen));
        box.Find("CANCEL").GetComponent<Button>().Select();
    }

    private void ConfirmDelete(GameSaveSlot chosen)
    {
        if(saves.Syncing || chosen==null || chosen.Int("SaveDeleted",0)!=0) return;
        deleteConfirm.interactable=false;
        var previous=chosen.values.Where(v=>v.key=="SaveDeleted").ToList();
        try
        {
            chosen.values.RemoveAll(v=>v.key=="SaveDeleted");
            chosen.values.Add(new GameSaveValue{key="SaveDeleted",integer=1});
            saves.Repository.Commit(chosen);
        }
        catch(Exception)
        {
            chosen.values.RemoveAll(v=>v.key=="SaveDeleted");chosen.values.AddRange(previous);
            deleteError.text="COULD NOT DELETE. PLEASE TRY AGAIN.";
            deleteConfirm.interactable=true;
            return;
        }
        selected=null;CloseDialog();Refresh();saves.SyncCloud();
    }

    private static void StyleDeleteButton(Button button, string label, bool destructive)
    {
        var image=button.GetComponent<Image>();
        ExportUIArt.Apply(image,destructive?"redButton":"greenButton");
        image.color=Color.white;
        var text=button.GetComponentInChildren<TMP_Text>(true);
        if(text==null) text=Text(button.transform,label,new Vector2(.06f,.08f),new Vector2(.94f,.92f),26);
        text.text=label;text.font=TMP_Settings.defaultFontAsset;text.fontStyle=FontStyles.Bold;
        text.color=Color.white;
        if(text is TextMeshProUGUI outlinedText) ExportUIArt.OutlineText(outlinedText);
        text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
        text.enableAutoSizing=true;text.fontSizeMin=20;text.fontSizeMax=28;
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.9f,.78f);
        colors.pressedColor=new Color(.75f,.7f,.65f);colors.disabledColor=new Color(.55f,.55f,.55f,.6f);button.colors=colors;
        var oldOutline=button.GetComponent<Outline>();
        if(oldOutline!=null) oldOutline.enabled=false;
    }
    private void BuildDeleteDialog()
    {
        var shade=Rect("Delete Save Dialog",transform,Vector2.zero,Vector2.one);
        deleteDialogLayout=shade.gameObject;
        shade.gameObject.AddComponent<Image>().color=new Color(0,0,0,.6f);
        var box=Rect("Creation style dialog",shade,new Vector2(.315f,.29f),new Vector2(.685f,.71f));
        var background=box.gameObject.AddComponent<Image>();
        var original=sourceHost!=null?sourceHost.transform.Find("createpanel"):null;
        var artwork=original!=null?original.GetComponent<Image>():null;
        if(artwork!=null && artwork.sprite!=null)
        {
            background.sprite=artwork.sprite;background.type=artwork.type;background.color=artwork.color;
        }
        else
        {
            background.color=new Color32(255,244,210,255);
            var border=box.gameObject.AddComponent<Outline>();border.effectColor=new Color32(167,134,76,255);border.effectDistance=new Vector2(6,-6);
        }
        // The creation-panel sprite includes its title. Keep its frame, but cover
        // the printed interior before adding the delete dialog's own content.
        var interior=Rect("Blank delete panel interior",box,new Vector2(.025f,.035f),new Vector2(.975f,.965f));
        var interiorImage=interior.gameObject.AddComponent<Image>();
        interiorImage.color=new Color32(255,244,210,255);
        interiorImage.raycastTarget=false;
        var title=Text(box,"DELETE SAVE?",new Vector2(.07f,.81f),new Vector2(.93f,.94f),36);
        title.name="Styled delete heading";
        title.fontStyle=FontStyles.Bold;title.color=new Color32(75,43,19,255);title.alignment=TextAlignmentOptions.Center;
        deleteName=Text(box,"Selected save",new Vector2(.07f,.60f),new Vector2(.93f,.78f),32);
        deleteName.name="Selected save";
        deleteName.richText=false;deleteName.fontStyle=FontStyles.Bold;
        deleteName.alignment=TextAlignmentOptions.Center;
        deleteName.enableAutoSizing=true;deleteName.fontSizeMin=22;deleteName.fontSizeMax=32;
        deleteDetails=Text(box,"Save details",new Vector2(.07f,.50f),new Vector2(.93f,.60f),22);
        deleteDetails.name="Save details";
        deleteDetails.alignment=TextAlignmentOptions.Center;
        var warning=Text(box,"Remove this save and its career progress?\nYour account and other saves are kept.\nThis cannot be undone in the game.",new Vector2(.08f,.28f),new Vector2(.92f,.48f),22);
        warning.alignment=TextAlignmentOptions.Center;
        deleteError=Text(box,"Delete error",new Vector2(.07f,.21f),new Vector2(.93f,.28f),20);deleteError.text="";
        deleteError.name="Delete error";
        deleteError.color=new Color32(150,42,38,255);
        var cancel=Button(box,"CANCEL",new Vector2(.13f,.09f),new Vector2(.42f,.22f),CloseDialog);
        deleteConfirm=Button(box,"DELETE",new Vector2(.58f,.09f),new Vector2(.87f,.22f),()=>{});
        StyleDeleteButton(cancel,"KEEP SAVE",false);StyleDeleteButton(deleteConfirm,"DELETE SAVE",true);
        cancel.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnRight=deleteConfirm};
        deleteConfirm.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=cancel};
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
            if(existing.gameObject.activeSelf && !UITransition.IsClosing(existing.gameObject))return;
            existing.closingMenu = false;
            existing.saves=manager;existing.joining=false;
            existing.sourceHost=FindObjectOfType<SaveLoadPanelHost>(true);
            if(existing.sourceHost!=null&&existing.sourceHost.transform.parent!=null)existing.sourceHost.transform.parent.gameObject.SetActive(false);
            existing.BindMainButtons();UITransition.Show(existing.gameObject);
            if (existing.paper != null) UITransition.ConfigurePanel(existing.paper.gameObject);
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
        UITransition.Show(go);
        if (menu.paper != null) UITransition.ConfigurePanel(menu.paper.gameObject);
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
        int rowCount=2;
        const float rowHeight = 245f;
        float height=Math.Max(1,rowCount)*rowHeight;
        rows.sizeDelta=new Vector2(0,height);
        // Project legacy careers into a single contract catalogue, without deleting files.
        if(selected==null||!slots.Contains(selected))
            selected=slots.OrderByDescending(GameSaveManager.UnlockedContractLevel).ThenByDescending(s=>s.updatedUtc).FirstOrDefault();
        int unlocked=selected==null?1:GameSaveManager.UnlockedContractLevel(selected);
        if(selectedContract!=6) selectedContract=Mathf.Clamp(selectedContract,1,unlocked);
        status.text=saves.Syncing?"Syncing contracts…":"";
        paper.Find("Start selected save").GetComponent<Button>().interactable=!saves.Syncing;
        if(deleteConfirm!=null && newGameDialog==deleteDialogLayout) deleteConfirm.interactable=!saves.Syncing;
        for(int i=0;i<6;i++)
        {
            int level=i+1;
            bool training=level==6;
            bool available=training||level<=unlocked;
            bool completed=!training&&selected!=null&&selected.Int(CampaignProgression.GetGradedKey(level),0)==1;
            // Match the surrounding cream folder, without product-cover artwork.
            // Keep two compact rows and reserve a gutter for the scrollbar.
            float left=.012f+(i%3)*.322f;float top=1-(i/3)*rowHeight/height;
            var cardRect=Rect("Contract "+level,rows,new Vector2(left,top-225f/height),new Vector2(left+.292f,top));
            Color ink=new Color32(65, 40,20,255);
            bool isSelected=level==selectedContract;
            var border=cardRect.gameObject.AddComponent<Image>();
            border.color=Color.clear;
            var tab=ContractBoxBand(cardRect,"Folder tab outline",new Vector2(.035f,.82f),new Vector2(.60f,1f),ink);
            var band=ContractBoxBand(tab,"Tan folder tab",Vector2.zero,Vector2.one,available?new Color32(237,207,148,255):new Color32(149,124,84,255));
            band.offsetMin=new Vector2(3,0);band.offsetMax=new Vector2(-3,-3);
            var body=ContractBoxBand(cardRect,"Folder box outline",Vector2.zero,new Vector2(1,.85f),ink);
            var shadow=body.gameObject.AddComponent<Shadow>();
            shadow.effectColor=new Color(0.18f,.1f,.03f,.20f);shadow.effectDistance=new Vector2(3,-4);
            var surface=Rect("Contract paper",body,Vector2.zero,Vector2.one);
            surface.offsetMin=new Vector2(4,4);surface.offsetMax=new Vector2(-4,-4);
            var surfaceImage=surface.gameObject.AddComponent<Image>();surfaceImage.raycastTarget=false;
            Color paperColor=available?new Color32(255,244,210,255):new Color32(185,165,129,255);
            surfaceImage.color=paperColor;
            // A tan inset and restrained selection edge echo the large folder frame.
            var inset=ContractBoxBand(surface,"Paper inset",new Vector2(.025f,.04f),new Vector2(.975f,.96f),new Color32(193,164,111,255));
            var inner=ContractBoxBand(inset,"Paper inset fill",Vector2.zero,Vector2.one,paperColor);
            inner.offsetMin=new Vector2(2,2);inner.offsetMax=new Vector2(-2,-2);
            if(isSelected)
            {
                var selection=body.gameObject.AddComponent<Outline>();
                selection.effectColor=new Color32(213,153,42,255);selection.effectDistance=new Vector2(3,-3);
            }
            ContractBoxBand(surface,"Title rule",new Vector2(.075f,.29f),new Vector2(.925f,.297f),new Color32(193,164,111,255));
            var card=cardRect.gameObject.AddComponent<Button>();card.targetGraphic=surfaceImage;
            card.interactable=available&&!saves.Syncing;card.onClick.AddListener(()=>{selectedContract=level;Refresh();});
            var colors=card.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1f,.94f,.8f);
            colors.pressedColor=new Color(.88f,.79f,.61f);colors.disabledColor=Color.white;card.colors=colors;
            var heading=Text(band,training?"FREE PRACTICE":"CONTRACT 0"+level,new Vector2(.07f,.08f),new Vector2(.93f,.92f),20);
            heading.color=ink;heading.fontStyle=FontStyles.Bold;heading.alignment=TextAlignmentOptions.Center;
            var marker=Text(surface,isSelected?"SELECTED":!available?"LOCKED":"",new Vector2(.55f,.77f),new Vector2(.92f,.94f),16);
            marker.color=heading.color;marker.alignment=TextAlignmentOptions.MidlineRight;
            var title=Text(surface,training?"Training Room":CampaignProgression.GetContractName(level),new Vector2(.07f,.34f),new Vector2(.93f,.69f),27);
            title.fontStyle=FontStyles.Bold;title.alignment=TextAlignmentOptions.MidlineLeft;
            title.enableAutoSizing=false;title.fontSize=27;title.enableWordWrapping=true;
            var footer=Rect("Contract status",surface,new Vector2(.035f,.025f),new Vector2(.965f,.24f));
            var footerImage=footer.gameObject.AddComponent<Image>();footerImage.raycastTarget=false;
            footerImage.color=Color.clear;
            var label=Text(footer,training?"ALL PRODUCTS & EQUIPMENT":completed?"COMPLETED · PLAY AGAIN":available?"":"FINISH CONTRACT "+(level-1)+" TO UNLOCK",new Vector2(.04f,.1f),new Vector2(.96f,.9f),18);
            label.alignment=TextAlignmentOptions.Center;label.fontStyle=FontStyles.Bold;label.enableWordWrapping=false;
            label.color=ink;
        }
    }
    private static RectTransform ContractBoxBand(Transform parent,string name,Vector2 min,Vector2 max,Color color)
    {
        var rect=Rect(name,parent,min,max);
        var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;
        return rect;
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
    private void CloseDialog(){var dialog=newGameDialog;newGameDialog=null;if(dialog!=null)UITransition.Hide(dialog);}
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
            newGameDialog=createDialogLayout;UITransition.Show(newGameDialog);
            if(createSetup!=null)
            {
                foreach(var button in createSetup.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    Bind(button.transform,button.name=="Button"?(UnityEngine.Events.UnityAction)createSetup.OnCreateButtonPressed:CloseDialog);
                createSetup.ResetGameMode();
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
            setup.ResetGameMode();
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

