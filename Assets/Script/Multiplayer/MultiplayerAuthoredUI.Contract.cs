using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MultiplayerAuthoredUI
{
    private Image crewContractBook, crewContractPhoto;
    private TMP_Text crewContractTitle, crewContractPhotoLabel, crewContractBody;
    private ScrollRect crewContractScroll;
    private RectTransform crewContractContent;
    private RectTransform crewContractFooter;
    private TMP_Text crewContractBalance;
    private Button crewContractNext, crewContractRevise, crewContractRetry;

    private void BuildCrewContract()
    {
        var root=panels["contract"]?.transform;if(root==null)return;
        var page=refs.Get<GameObject>("ContractUIManager.qualificationsPanel");
        crewContractScroll=refs.Get<ScrollRect>("ContractUIManager.briefScroll");
        crewContractBody=refs.Get<TMP_Text>("ContractUIManager.briefBody");
        crewContractTitle=refs.Get<TMP_Text>("ContractUIManager.briefTitle");
        var book=page!=null?page.transform.Find("Qualifications Book"):null;
        crewContractBook=book!=null?book.GetComponent<Image>():null;
        if(crewContractBook==null || crewContractScroll==null || crewContractBody==null)
        {
            Debug.LogError("The shared singleplayer contract layout is missing from CrewUI/Studio.");return;
        }
        // Use the actual copied singleplayer document hierarchy. No second book,
        // alternate photo frames or independently scaled requirements are built.
        foreach(Transform child in root)child.gameObject.SetActive(false);
        page.SetActive(true);
        var pageRect=(RectTransform)page.transform;
        pageRect.anchorMin=Vector2.zero;pageRect.anchorMax=Vector2.one;
        pageRect.offsetMin=pageRect.offsetMax=Vector2.zero;
        pageRect.localScale=Vector3.one;pageRect.localRotation=Quaternion.identity;
        var shade=page.GetComponent<Image>();if(shade!=null)shade.color=new Color(0,0,0,.7f);
        foreach(Transform child in page.transform)
            if(child!=book)child.gameObject.SetActive(false);
        ContractBriefPresentation.ConfigureCanvas(root.GetComponent<Canvas>());
        PauseManager.PutOverlayOnTop(root.gameObject,200);
        crewContractPhoto=book.Find("Product photo")?.GetComponent<Image>();
        crewContractPhotoLabel=book.Find("Product photo label")?.GetComponent<TMP_Text>();
        crewContractContent=crewContractScroll.content;
        if(crewContractContent==null || crewContractContent==crewContractBody.rectTransform)
        {
            crewContractContent=ContractRect(crewContractScroll.viewport,"Contract document",Vector2.zero,Vector2.zero);
            crewContractBody.transform.SetParent(crewContractContent,false);
        }
        foreach(Transform child in crewContractContent)
            if(child!=crewContractBody.transform)child.gameObject.SetActive(false);
        var fitter=crewContractBody.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=false;
        TopStretch(crewContractContent);TopStretch(crewContractBody.rectTransform);
        crewContractScroll.content=crewContractContent;
        // Like the solo document footer, crew actions scroll within the page.
        // Keep the cover and first-page composition identical to the native brief.
        crewContractFooter=ContractRect(crewContractContent,"Crew document footer",Vector2.zero,Vector2.zero);
        TopStretch(crewContractFooter);
        crewContractBalance=ContractLabel(crewContractFooter,"Shared balance",new Vector2(0,-30),new Vector2(490,60),20);
        crewContractBalance.rectTransform.anchorMin=crewContractBalance.rectTransform.anchorMax=new Vector2(.5f,1);
        crewContractBalance.alignment=TextAlignmentOptions.TopLeft;
        crewContractNext=ContractAction(crewContractFooter,"Crew next contract","NEXT CONTRACT",()=>Crew.Send(new CrewCommand{action="nextContract"}));
        crewContractRevise=ContractAction(crewContractFooter,"Crew revise commercial","REVISE",()=>Open("computer"));
        crewContractRetry=ContractAction(crewContractFooter,"Crew retry contract","RETRY CONTRACT",RequestCrewRetry);
    }
    private void UpdateCrewContract()
    {
        if(crewContractBook==null || Crew?.State==null)return;
        var state=Crew.State;int level=Mathf.Clamp(state.contractLevel,2,5);
        // This is also called by ContractUIManager.RefreshBrief: shared image
        // dimensions, page bounds, reading surface, font and margins.
        ContractBriefPresentation.Apply(crewContractBook,level,crewContractScroll,crewContractBody,false);
        if(crewContractTitle!=null)crewContractTitle.text=MultiplayerContractManager.Title(level);
        string[] photos={"psdVasePhoto","gokeProduct","terrariMark","coffeeProduct","harayaProduct"};
        var photo=ExportUIArt.Get(photos[level-1]);
        if(crewContractPhoto!=null){crewContractPhoto.enabled=photo!=null;if(photo!=null)crewContractPhoto.sprite=photo;}
        if(crewContractPhotoLabel!=null)crewContractPhotoLabel.text=photo==null?MultiplayerContractManager.Title(level):"";
        crewContractBody.text=MultiplayerContractManager.Specifications(state);
        crewContractBalance.font=crewContractBody.font;
        crewContractBalance.fontSharedMaterial=crewContractBody.fontSharedMaterial;
        crewContractBalance.color=crewContractBody.color;
        Canvas.ForceUpdateCanvases();
        float width=crewContractScroll.viewport.rect.width;
        float textWidth=Mathf.Max(1,width-crewContractBody.margin.x-crewContractBody.margin.z);
        float height=Mathf.Ceil(crewContractBody.GetPreferredValues(crewContractBody.text,textWidth,Mathf.Infinity).y);
        crewContractBody.rectTransform.sizeDelta=new Vector2(0,height);
        RefreshCrewContractFooter();
        crewContractScroll.StopMovement();crewContractScroll.verticalNormalizedPosition=1;
    }
    private void RefreshCrewContractFooter()
    {
        if(crewContractFooter==null || Crew?.State==null)return;
        var state=Crew.State;int level=state.contractLevel;
        crewContractBalance.text="<color=#7A3F24><b>CREW BALANCE</b></color>\n"+state.budget.ToString("N0")+" B-Coins shared by the crew.";
        crewContractNext.gameObject.SetActive(state.phase=="review" && state.contractPaid && level<5 && PhotonNetwork.IsMasterClient);
        crewContractRevise.gameObject.SetActive(state.phase=="review" && Crew.HasRole(CrewRole.Editor));
        crewContractRetry.gameObject.SetActive(PhotonNetwork.IsMasterClient && !state.recording);
        var actions=new List<Button>();
        foreach(var button in new[]{crewContractRetry,crewContractNext,crewContractRevise})if(button.gameObject.activeSelf)actions.Add(button);
        float top=crewContractBody.rectTransform.sizeDelta.y+28;
        crewContractFooter.anchoredPosition=new Vector2(0,-top);
        crewContractFooter.sizeDelta=new Vector2(0,80+actions.Count*86);
        for(int i=0;i<actions.Count;i++)
        {
            var rect=(RectTransform)actions[i].transform;
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);
            rect.anchoredPosition=new Vector2(0,-110-i*86);
        }
        crewContractContent.sizeDelta=new Vector2(0,top+crewContractFooter.sizeDelta.y+12);
        LayoutRebuilder.ForceRebuildLayoutImmediate(crewContractContent);
    }
    private static void TopStretch(RectTransform rect)
    {
        rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);
        rect.localScale=Vector3.one;rect.localRotation=Quaternion.identity;rect.anchoredPosition=Vector2.zero;rect.sizeDelta=Vector2.zero;
    }
    private static RectTransform ContractRect(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
    }
    private static TextMeshProUGUI ContractLabel(Transform parent,string name,Vector2 position,Vector2 size,float fontSize)
    {
        var label=ContractRect(parent,name,position,size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font=Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF")??TMP_Settings.defaultFontAsset;
        label.fontSize=fontSize;label.color=CrewPaperStyle.Ink;label.enableWordWrapping=true;label.raycastTarget=false;return label;
    }
    private static Button ContractAction(Transform parent,string name,string caption,UnityEngine.Events.UnityAction action)
    {
        var rect=ContractRect(parent,name,new Vector2(0,-475),new Vector2(300,70));
        var image=rect.gameObject.AddComponent<Image>();RoleSelectionUI.LobbyButtonArt(image,"blueButton");
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(action);
        var label=ContractLabel(rect,"Label",Vector2.zero,new Vector2(264,50),28);
        label.text=caption;
        label.alignment=TextAlignmentOptions.Center;label.fontStyle=FontStyles.Bold;ExportUIArt.OutlineText(label);
        button.navigation=new Navigation{mode=Navigation.Mode.None};return button;
    }
}
