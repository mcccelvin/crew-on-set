using System;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Gear = Player.Equipment.Equipment;

public sealed partial class EquipmentStorageCabinet
{
    private GameObject menu;
    private RectTransform carriedRows,storedRows;
    private TMP_Text storedCount;

    private RectTransform Rect(Transform parent,string title,Vector2 position,Vector2 size)
    {
        var rect=new GameObject(title,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=Vector2.one*.5f;
        rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
    }
    private TMP_Text Label(Transform parent,string value,Vector2 position,Vector2 size,float font=24)
    {
        var label=Rect(parent,value,position,size).gameObject.AddComponent<TextMeshProUGUI>();
        label.text=value;label.fontSize=font;label.color=CrewPaperStyle.Ink;
        label.alignment=TextAlignmentOptions.MidlineLeft;label.raycastTarget=false;
        label.enableWordWrapping=false;label.overflowMode=TextOverflowModes.Ellipsis;
        return label;
    }
    private Button Button(Transform parent,string title,Vector2 position,Vector2 size,Action action,bool close=false)
    {
        var rect=Rect(parent,title,position,size);var image=rect.gameObject.AddComponent<Image>();
        var button=rect.gameObject.AddComponent<Button>();
        var text=Label(rect,title,Vector2.zero,size-new Vector2(18,4),22);
        text.alignment=TextAlignmentOptions.Center;text.fontStyle=FontStyles.Bold;
        ExportUIArt.Apply(image,close?"close":"blueButton");image.color=Color.white;image.raycastTarget=true;
        if(close)text.text="";
        text.color=Color.white;button.targetGraphic=image;button.navigation=new Navigation{mode=Navigation.Mode.None};
        button.onClick.AddListener(()=>action());return button;
    }
    private void EnsureMenu()
    {
        if(menu!=null)return;
        menu=new GameObject("Equipment storage menu",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        SceneManagerMove(menu);
        var canvas=menu.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=160;
        var scaler=menu.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var dim=Rect(menu.transform,"Dim",Vector2.zero,Vector2.zero);
        dim.anchorMin=Vector2.zero;dim.anchorMax=Vector2.one;dim.offsetMin=dim.offsetMax=Vector2.zero;
        dim.gameObject.AddComponent<Image>().color=new Color(0,0,0,.65f);
        var paper=Rect(menu.transform,"Equipment cabinet inventory",Vector2.zero,new Vector2(1180,720));
        CrewPaperStyle.Card(paper.gameObject.AddComponent<Image>());
        var header=Rect(paper,"Header",new Vector2(0,307),new Vector2(1136,64));
        CrewPaperStyle.Round(header.gameObject.AddComponent<Image>());header.GetComponent<Image>().color=new Color32(20,70,83,255);
        var title=Label(header,"EQUIPMENT STORAGE",new Vector2(-28,0),new Vector2(1020,50),32);
        title.fontStyle=FontStyles.Bold;title.color=Color.white;
        Button(paper,"X",new Vector2(525,307),new Vector2(54,54),CloseStorage,true);
        Label(paper,"Put away equipment or take it back to your hotbar.",new Vector2(0,245),new Vector2(1080,40),23);
        var carriedTitle=Label(paper,"YOUR HOTBAR",new Vector2(-286,189),new Vector2(510,34),23);
        carriedTitle.fontStyle=FontStyles.Bold;
        storedCount=Label(paper,"STORED EQUIPMENT",new Vector2(286,189),new Vector2(510,34),23);
        storedCount.fontStyle=FontStyles.Bold;
        carriedRows=ScrollColumn(paper,"Carried equipment",-286);
        storedRows=ScrollColumn(paper,"Stored equipment",286);
        Label(paper,"[E] / [ESC] Close",new Vector2(0,-318),new Vector2(1080,32),21).color=CrewPaperStyle.MutedInk;
        menu.SetActive(false);
    }
    private void SceneManagerMove(GameObject root) => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,gameObject.scene);

    private RectTransform ScrollColumn(Transform parent,string title,float x)
    {
        var scrollRoot=Rect(parent,title,new Vector2(x,-54),new Vector2(532,434));
        var bg=scrollRoot.gameObject.AddComponent<Image>();CrewPaperStyle.Round(bg);
        bg.color=new Color32(239,227,196,255);bg.raycastTarget=true;
        var viewport=Rect(scrollRoot,"Viewport",Vector2.zero,new Vector2(508,414));
        viewport.gameObject.AddComponent<RectMask2D>();
        var content=Rect(viewport,"Rows",Vector2.zero,new Vector2(508,0));
        content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);
        content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
        var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=12;
        layout.childControlWidth=layout.childForceExpandWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;
        layout.padding=new RectOffset(4,4,4,4);
        var fitter=content.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var scroll=scrollRoot.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;
        scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=38;scroll.movementType=ScrollRect.MovementType.Clamped;
        return content;
    }
    private void ClearRows(RectTransform rows)
    {
        for(int i=rows.childCount-1;i>=0;i--){var child=rows.GetChild(i);child.gameObject.SetActive(false);Destroy(child.gameObject);}
    }
    private void EquipmentRow(RectTransform rows,string title,string detail,Sprite icon,string action,Action click,bool allowed=true)
    {
        var row=Rect(rows,title,Vector2.zero,new Vector2(500,94));
        row.gameObject.AddComponent<LayoutElement>().preferredHeight=94;
        var background=row.gameObject.AddComponent<Image>();CrewPaperStyle.Round(background);background.color=CrewPaperStyle.Paper;
        if(icon!=null)
        {
            var image=Rect(row,"Equipment icon",new Vector2(-206,0),new Vector2(54,62)).gameObject.AddComponent<Image>();
            image.sprite=icon;image.preserveAspect=true;image.raycastTarget=false;
        }
        var name=Label(row,title,new Vector2(-42,15),new Vector2(260,34),21);name.fontStyle=FontStyles.Bold;name.richText=false;
        Label(row,detail,new Vector2(-42,-20),new Vector2(260,28),17).color=CrewPaperStyle.MutedInk;
        if(!string.IsNullOrEmpty(action))Button(row,action,new Vector2(177,0),new Vector2(122,46),click).interactable=allowed;
    }
    private void Empty(RectTransform rows,string text) => EquipmentRow(rows,text,"",null,"",()=>{},false);
    private static string Title(Gear item) => item is Player.Equipment.SDCardItem card ?
        card.DisplayName+(card.isUsedCard?" (recorded)":"") : ShopTerminal.DisplayEquipmentName(item.EquipmentName);

    private void RefreshContents()
    {
        if(carriedRows==null || storedRows==null)return;
        ClearRows(carriedRows);ClearRows(storedRows);
        if(Room)
        {
            var crew=MultiplayerRoleManager.Instance;var state=crew?.State;if(state==null)return;
            int owner=PhotonNetwork.LocalPlayer.ActorNumber;
            var carried=state.objects.Where(o=>o.holder==owner && o.loadedInto==0 && MultiplayerRoomActions.IsEquipment(o.kind)).OrderBy(o=>o.slot).ToArray();
            var stock=state.objects.Where(o=>o.action=="stored" && MultiplayerRoomActions.IsEquipment(o.kind)).OrderBy(o=>o.kind).ThenBy(o=>o.id).ToArray();
            foreach(var item in carried)
                EquipmentRow(carriedRows,MultiplayerAuthoredUI.EquipmentName(item.kind,item.tape!=0),"HOTBAR "+item.slot,
                    EquipmentIconArt.Get(MultiplayerAuthoredUI.EquipmentName(item.kind,item.tape!=0)),"STORE",
                    ()=>crew.Send(new CrewCommand{action="storeEquipment",id=item.id}));
            foreach(var item in stock)
            {
                bool role=crew.HasRole(MultiplayerContractManager.RoleFor(item.kind));
                EquipmentRow(storedRows,MultiplayerAuthoredUI.EquipmentName(item.kind,item.tape!=0),role?"AVAILABLE":"ASSIGNED TO ANOTHER ROLE",
                    EquipmentIconArt.Get(MultiplayerAuthoredUI.EquipmentName(item.kind,item.tape!=0)),"TAKE",
                    ()=>crew.Send(new CrewCommand{action="retrieveEquipment",id=item.id}),role);
            }
            storedCount.text="STORED EQUIPMENT  ·  "+stock.Length;
            if(carried.Length==0)Empty(carriedRows,"Your hotbar is empty");
            if(stock.Length==0)Empty(storedRows,"No equipment stored");
        }
        else
        {
            stored.RemoveAll(item=>item==null);
            var carried=inventory!=null?inventory.CarriedEquipment().Where(item=>item!=null).ToArray():new Gear[0];
            foreach(var item in carried)EquipmentRow(carriedRows,Title(item),"IN YOUR HOTBAR",EquipmentIconArt.Get(item.EquipmentName,item.EquipmentIcon),"STORE",()=>Store(item));
            foreach(var item in stored)EquipmentRow(storedRows,Title(item),item is Player.Equipment.SDCardItem card && card.isUsedCard?"RECORDINGS KEPT":"AVAILABLE",
                EquipmentIconArt.Get(item.EquipmentName,item.EquipmentIcon),"TAKE",()=>Retrieve(item));
            storedCount.text="STORED EQUIPMENT  ·  "+stored.Count;
            if(carried.Length==0)Empty(carriedRows,"Your hotbar is empty");
            if(stored.Count==0)Empty(storedRows,"No equipment stored");
        }
    }
}
