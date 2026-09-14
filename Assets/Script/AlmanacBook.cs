using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public partial class AlmanacManager
{
    private TextMeshProUGUI bookEntryTitle,bookLeftText,bookRightText,bookHeading,bookPageNumber;
    private Button bookPrevious,bookNext,bookVideo;
    private int bookPage,bookCategory;
    private readonly List<string> bookBodies=new List<string>();
    private readonly List<KnowledgeEntry> bookEntries=new List<KnowledgeEntry>();
    private static readonly string[] BookCategories={"DIRECTOR","LIGHTING","AUDIO","CAMERA","EDITING"};

    private void BuildIllustratedBook()
    {
        if(almanacCanvas==null||bookEntryTitle!=null)return;
        var scaler=almanacCanvas.GetComponent<CanvasScaler>();
        if(scaler!=null){scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;}
        var canvas=almanacCanvas.GetComponent<Canvas>();if(canvas!=null)canvas.sortingOrder=60;
        var shade=CreatePanel("Book backdrop",almanacCanvas.transform,new Color(0,0,0,.65f));
        SetStretchRect(shade.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        var root=CreatePanel("Illustrated Almanac",shade.transform,Color.white);
        SetRect(root.GetComponent<RectTransform>(),Vector2.one*.5f,Vector2.one*.5f,new Vector2(0,-15),new Vector2(1495,937));
        ExportUIArt.Apply(root.GetComponent<Image>(),"psdBook");
        knowledgePanel=CreatePanel("Book pages",root.transform,Color.clear);
        SetStretchRect(knowledgePanel.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        knowledgeCategoryFilter=1;
        equipmentKnowledgeButton=BookButton(root.transform,"Equipment tab","EQUIPMENTS","psdTab",new Vector2(-493,487),new Vector2(236,73));
        techniquesKnowledgeButton=BookButton(root.transform,"Techniques tab","TECHNIQUES","psdTab",new Vector2(-244,487),new Vector2(236,73));
        equipmentKnowledgeButton.onClick.AddListener(()=>{OpenTab(1);bookPage=0;bookCategory=0;});
        techniquesKnowledgeButton.onClick.AddListener(()=>{OpenTab(1);bookPage=0;bookCategory=0;});
        closeButton=BookButton(root.transform,"Close book","","close",new Vector2(820,440),new Vector2(86,99));
        bookPrevious=BookButton(root.transform,"Previous page","","left",new Vector2(-813,49),new Vector2(62,93));
        bookNext=BookButton(root.transform,"Next page","","right",new Vector2(898,49),new Vector2(62,93));
        bookPrevious.onClick.AddListener(()=>{bookPage--;RefreshBookPage();});bookNext.onClick.AddListener(()=>{bookPage++;RefreshBookPage();});
        for(int i=0;i<BookCategories.Length;i++)
        {
            int category=i;
            var button=BookButton(root.transform,"Category "+BookCategories[i],"",new[]{"psdDirector","psdLight","psdAudio","psdCamera","psdEdit"}[i],new Vector2(780,215-i*108),new Vector2(122,82));
            button.onClick.AddListener(()=>{bookCategory=category;bookPage=0;OpenTab(1);RefreshBookPage();});
        }
        bookHeading=BookText(knowledgePanel.transform,"Section",new Vector2(-365,338),new Vector2(610,80),60);
        bookEntryTitle=BookText(knowledgePanel.transform,"Entry title",new Vector2(-365,262),new Vector2(595,70),30);
        OutlineBookHeading(bookHeading);OutlineBookHeading(bookEntryTitle);
        bookEntryTitle.fontStyle=FontStyles.Bold;
        bookLeftText=BookText(knowledgePanel.transform,"Left page",new Vector2(-365,-65),new Vector2(565,465),26);
        bookRightText=BookText(knowledgePanel.transform,"Right page",new Vector2(365,30),new Vector2(565,640),26);
        bookLeftText.alignment=TextAlignmentOptions.Center;bookLeftText.fontStyle=FontStyles.Bold;
        bookRightText.alignment=TextAlignmentOptions.TopLeft;
        bookPageNumber=BookText(knowledgePanel.transform,"Page number",new Vector2(360,-365),new Vector2(500,35),20);
        bookVideo=BookButton(knowledgePanel.transform,"Watch guide","WATCH GUIDE","blueButton",new Vector2(-360,-357),new Vector2(270,52));
        bookVideo.onClick.AddListener(ShowRuleOfThirdsGuide);
        // Preserve profile, milestones and the interactive guide without crowding the two main tabs.
        var other=CreatePanel("Other book pages",root.transform,Color.clear);SetStretchRect(other.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,new Vector2(95,70),new Vector2(-95,-100));
        BuildPlayerInfoPanel(other.transform);BuildAchievementsPanel(other.transform);
        playerInfoTabBtn=BookButton(root.transform,"Director record","DIRECTOR RECORD","psdTab",new Vector2(-490,-413),new Vector2(230,40));
        achievementsTabBtn=BookButton(root.transform,"Milestones","MILESTONES","psdTab",new Vector2(-235,-413),new Vector2(230,40));
        BuildTechniqueGuidePanel();
        other.GetComponent<Image>().raycastTarget=false;
        OpenTab(1);RefreshBookPage();
    }
    private Button BookButton(Transform parent,string name,string label,string artwork,Vector2 position,Vector2 size)
    {
        var button=CreateButton(name,parent,label);ExportUIArt.Apply(button.GetComponent<Image>(),artwork);
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.93f,.75f);colors.selectedColor=colors.highlightedColor;colors.disabledColor=new Color(.65f,.65f,.65f);button.colors=colors;
        SetRect(button.GetComponent<RectTransform>(),Vector2.one*.5f,Vector2.one*.5f,position,size);
        var text=button.GetComponentInChildren<TextMeshProUGUI>();if(text!=null){text.enableAutoSizing=true;text.fontSizeMin=14;text.fontSizeMax=24;text.fontSize=24;OutlineBookHeading(text);}return button;
    }
    private void OutlineBookHeading(TextMeshProUGUI text)
    {
        ExportUIArt.OutlineText(text);
    }
    private TextMeshProUGUI BookText(Transform parent,string name,Vector2 position,Vector2 size,float font)
    {
        var text=CreateText(name,parent,"",font,TextAlignmentOptions.Center);SetRect(text.rectTransform,Vector2.one*.5f,Vector2.one*.5f,position,size);text.color=new Color(.08f,.065f,.04f);text.enableAutoSizing=true;text.fontSizeMin=20;text.fontSizeMax=font;return text;
    }
    private void RefreshBookPage()
    {
        bookBodies.Clear();bookEntries.Clear();var entries=new List<KnowledgeEntry>();
        foreach(var entry in database)
        {
            if(!entry.isUnlocked||stagedHiddenKnowledge.Contains(entry.id))continue;
            if(entry.category!=(knowledgeCategoryFilter==2?"Technique":"Equipment"))continue;
            string content=(entry.id+" "+entry.title).ToLowerInvariant();
            bool match=bookCategory==0 || bookCategory==1&&(content.Contains("light")||content.Contains("diffusion")) || bookCategory==2&&(content.Contains("audio")||content.Contains("sound")||content.Contains("music")) || bookCategory==3&&(content.Contains("camera")||content.Contains("shot")||content.Contains("framing")||content.Contains("thirds")) || bookCategory==4&&(content.Contains("edit")||content.Contains("trim")||content.Contains("color")||content.Contains("branding")||content.Contains("overlay"));
            if(match)entries.Add(entry);
        }
        entries.Sort(CompareKnowledgeEntries);
        foreach(var entry in entries)
        {
            string remaining=entry.description??"";
            do{int count=System.Math.Min(remaining.Length,900);if(count<remaining.Length){int split=remaining.LastIndexOf(' ',count-1,count);if(split>0)count=split;}bookBodies.Add(remaining.Substring(0,count));bookEntries.Add(entry);remaining=remaining.Substring(count).TrimStart();}while(remaining.Length>0);
        }
        bookPage=Mathf.Clamp(bookPage,0,Mathf.Max(0,bookBodies.Count-1));bookHeading.text=knowledgeCategoryFilter==2?"TECHNIQUES":"EQUIPMENTS";
        bookPrevious.interactable=bookPage>0;bookNext.interactable=bookPage+1<bookBodies.Count;
        bookVideo.gameObject.SetActive(bookEntries.Count>0&&bookEntries[bookPage].id=="rule_of_thirds");
        if(bookEntries.Count==0){bookEntryTitle.text=BookCategories[bookCategory];bookLeftText.text="Complete the matching lessons to unlock these pages.";bookRightText.text="Your equipment controls and techniques appear here as you learn them.";bookPageNumber.text="0 / 0";return;}
        bookEntryTitle.text=bookEntries[bookPage].title;string body=bookBodies[bookPage];int cut=Mathf.Min(360,body.Length);
        int instructions=body.IndexOf("HOW TO USE",System.StringComparison.OrdinalIgnoreCase);
        if(instructions>0&&instructions<450)cut=instructions;
        else if(cut<body.Length){int paragraph=body.LastIndexOf('\n',cut-1,cut);if(paragraph>120)cut=paragraph;else {int space=body.LastIndexOf(' ',cut-1,cut);if(space>0)cut=space;}}
        bookLeftText.text=body.Substring(0,cut);bookRightText.text=body.Substring(cut).TrimStart();bookPageNumber.text=(bookPage+1)+" / "+bookBodies.Count;
        UpdateKnowledgeFilterButtons();
    }
}
