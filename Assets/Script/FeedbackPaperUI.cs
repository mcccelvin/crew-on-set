using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// The same paper presentation is used for career and crew results. All navigation
// is local; callbacks, not this view, own rewards, retries and campaign changes.
public sealed class FeedbackPaperUI : MonoBehaviour
{
    public bool IsOpen => gameObject.activeInHierarchy;
    public bool HandlesEscape { get; set; } = true;
    public bool AllowClose { get; set; } = true;
    private FeedbackReport report;
    private int page, level, highestPage;
    private bool busy, tutorial, actionTaken;
    private Action closed, proceed;
    private string proceedLabel;
    private readonly RectTransform[] papers = new RectTransform[5];
    private readonly GameObject[] contents = new GameObject[5];
    private readonly ScrollRect[] scrolls = new ScrollRect[5];
    private readonly Vector2[] resting = { new Vector2(581,-81), new Vector2(599,-63), new Vector2(570,-105), new Vector2(580,-81), new Vector2(561,-71) };
    private TMP_Text boss, counter, nextLabel;
    private Image portrait, stamp;
    private RectTransform hand, paperclip;
    private Button back, next, close;
    private Coroutine transition;
    private GameObject contentsIndex;
    private readonly TMP_Text[] indexLabels = new TMP_Text[5];
    private readonly Image[] indexSelection = new Image[5];
    private static readonly Color Ink = new Color32(35,57,77,255);
    private static readonly Color Blue = new Color32(37,68,106,255);
    private readonly Sprite[] bossSprites = new Sprite[3];

    public static void ConfigureScale(Canvas canvas)
    {
        if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) return;
        var scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080);
        // Fit the whole reference design; unusual aspect ratios gain margins, not cropping.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.matchWidthOrHeight = .5f;
    }
    public static FeedbackPaperUI Create()
    {
        var root = new GameObject("Animated client feedback papers", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.SetActive(false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
        ConfigureScale(canvas);
        return root.AddComponent<FeedbackPaperUI>();
    }
    public void Present(ProductionGrades grades, int contractLevel, string budget, string actionLabel, Action action, Action onClose, bool teach = false)
    {
        StopTransition();
        report = new FeedbackReport(grades,budget); level = contractLevel;
        proceed = action; closed = onClose; proceedLabel = actionLabel; tutorial = teach;
        page = highestPage = 0; actionTaken = false;
        if (papers[0] == null) Build();
        close.gameObject.SetActive(AllowClose);
        SetBossVisibility();
        gameObject.SetActive(true);
        EnsureEventSystem();
        for (int i = 0; i < papers.Length; i++)
        {
            papers[i].gameObject.SetActive(false); papers[i].anchoredPosition = resting[i];
            papers[i].localScale = Vector3.one; contents[i].SetActive(false);
            var heading = contents[i].transform.Find("Department title").GetComponent<TMP_Text>(); heading.text = FeedbackTypography.Heading(FeedbackReport.Titles[i]);
            contents[i].transform.Find("Department score").GetComponent<TMP_Text>().text = report.scores[i];
            var body = scrolls[i].content.GetComponent<TMP_Text>(); body.text = report.bodies[i];
            float height = Mathf.Max(i == 4 ? 300 : 460, body.GetPreferredValues(body.text,1040,0).y + 24);
            scrolls[i].content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
            scrolls[i].verticalNormalizedPosition = 1;
        }
        stamp.sprite = ExportUIArt.Get(report.passed ? "feedbackApproved" : "feedbackFailed");
        stamp.gameObject.SetActive(false);
        ShowPage(0, true);
    }
    public void SetContinueLabel(string value) { proceedLabel = value; UpdateButtons(); }
    private void SetBossVisibility()
    {
        portrait.gameObject.SetActive(tutorial);
        boss.transform.parent.gameObject.SetActive(tutorial);
        contentsIndex.SetActive(!tutorial);
    }
    private void Build()
    {
        var backdrop = new GameObject("Feedback modal backdrop", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        backdrop.transform.SetParent(transform, false);
        var backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero; backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = backdropRect.offsetMax = Vector2.zero;
        backdrop.GetComponent<UnityEngine.UI.Image>().color = new Color32(22,16,12,255);
        // Fixed 1920x1080 artwork frame lives inside the scaling canvas.
        var frame = Rect(transform,"Feedback design frame",0,0,1920,1080);
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f,.5f); frame.anchoredPosition = Vector2.zero;
        Image(frame,"Wood desk","feedbackBackground",0,0,1920,1080,true);
        var titleCard = Image(frame,"Review title paper","feedbackPaper2",36,36,487,242,false); PaperShadow(titleCard);
        var title = Text(frame,"Feedback heading","Client Review",48,43,465,141,80,TextAlignmentOptions.Center);
        FeedbackTypography.Apply(title,true); title.enableAutoSizing = true; title.fontSizeMin = 60; title.fontSizeMax = 80;
        counter = Text(frame,"Paper counter","",48,192,465,65,25,TextAlignmentOptions.Center);
        var index = Image(frame,"Report contents paper","feedbackPaper3",48,305,469,681,false); PaperShadow(index); contentsIndex = index.gameObject;
        var indexTitle = Text(index.transform,"Contents heading","Review contents",36,25,397,77,48,TextAlignmentOptions.Center); FeedbackTypography.Apply(indexTitle,true);
        Stroke(index.transform,"Contents underline",76,102,317,2,new Color32(35,57,77,140),-.6f);
        for (int i = 0; i < 5; i++)
        {
            indexSelection[i] = Image(index.transform,"Current department " + i,null,30,132+i*86,409,70,false);
            indexSelection[i].color = new Color32(35,57,77,18);
            indexLabels[i] = Text(index.transform,"Contents item " + i,(i+1).ToString("00") + "   " + FeedbackTypography.Heading(FeedbackReport.Titles[i]),43,144+i*86,386,48,26,TextAlignmentOptions.Left);
        }
        Text(index.transform,"Contents reading note","Use Back / Next to turn the papers.\nScroll to read every note.",38,591,393,76,23,TextAlignmentOptions.Center);
        portrait = Image(frame,"Boss portrait","feedbackBossOpen",35,273,475,435,false); portrait.preserveAspect = true;
        var speech = Image(frame,"Boss dialogue paper","feedbackPaper1",36,730,487,274,false);
        boss = Text(speech.transform,"Boss dialogue","",20,18,447,232,30,TextAlignmentOptions.Center);
        BossDialogueStyle.Apply(boss); FeedbackTypography.Apply(boss); boss.color = Ink;
        boss.fontSize = boss.fontSizeMax = 30; boss.fontSizeMin = 24;
        for (int i = 0; i < 5; i++)
        {
            float[] widths = {1254,1234,1274,1275,1283}, heights = {902,935,863,925,941};
            var paper = Image(frame,"Result paper " + (i+1),"feedbackPaper" + (i+1),resting[i].x,-resting[i].y,widths[i],heights[i],false);
            PaperShadow(paper);
            papers[i] = paper.rectTransform;
            var content = Rect(paper.transform,"Paper content",0,0,widths[i],heights[i]); contents[i] = content.gameObject;
            Text(content,"Report folio","CREW ON SET   /   CLIENT NOTES",150,35,936,33,19,TextAlignmentOptions.Center);
            var department = Text(content,"Department title","",110,68,1040,83,65,TextAlignmentOptions.Center); FeedbackTypography.Apply(department,true);
            department.enableAutoSizing = true; department.fontSizeMin = 50; department.fontSizeMax = 65;
            Text(content,"Department score","",110,157,1040,53,32,TextAlignmentOptions.Center);
            Stroke(content,"Hand drawn underline",424,219,404,2,new Color32(35,57,77,180),-.65f);
            Stroke(content,"Header rule",104,238,1040,1,new Color32(35,57,77,42));
            Stroke(content,"Red notebook margin",82,255,1,i==4?300:460,new Color32(145,53,48,48));
            var viewport = Rect(content,"Notes viewport",104,260,1040,i==4?300:460);
            var surface = viewport.gameObject.AddComponent<UnityEngine.UI.Image>(); surface.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            for (int line = 1; line <= (i == 4 ? 5 : 8); line++)
                Stroke(viewport,"Writing rule " + line,0,line*53,1040,1,new Color32(35,57,77,19));
            var body = Text(viewport,"Department notes","",0,0,1040,460,32,TextAlignmentOptions.TopLeft);
            body.enableAutoSizing = false; body.lineSpacing = 4;
            body.rectTransform.anchorMin = new Vector2(0,1); body.rectTransform.anchorMax = new Vector2(1,1);
            body.rectTransform.pivot = new Vector2(0,1); body.rectTransform.anchoredPosition = Vector2.zero;
            body.rectTransform.sizeDelta = new Vector2(0,460);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = body.rectTransform;
            scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 42; scroll.movementType = ScrollRect.MovementType.Clamped;
            scrolls[i] = scroll;
            Text(content,"Reading hint","Scroll for all notes   /   Back & Next to turn papers",90,heights[i]-103,1060,40,23,TextAlignmentOptions.Center);
            if (i == 4)
            {
                stamp = Image(content,"Client decision stamp",null,690,640,440,170,false); stamp.preserveAspect = true;
                Text(content,"Decision caption","Client's final decision\nBased on the submitted contract",100,650,520,122,28,TextAlignmentOptions.Center);
            }
        }
        paperclip = Image(frame,"Paperclip","feedbackPaperclip",649,50,111,166,false).rectTransform;
        hand = Image(frame,"Stamp hand","feedbackHand",1100,200,940,645,false).rectTransform; hand.gameObject.SetActive(false);
        back = Button(frame,"Previous result","BACK",650,999,240,62,() => ShowPage(page-1,false));
        next = Button(frame,"Next result","NEXT",1350,999,480,62,Next); nextLabel = next.GetComponentInChildren<TMP_Text>();
        close = Button(frame,"Close feedback","CLOSE",42,1014,470,48,RequestClose);
    }
    private void ShowPage(int target, bool forceEntrance)
    {
        if (busy || target < 0 || target > 4) return;
        page = target;
        for (int i=0;i<5;i++)
        {
            papers[i].gameObject.SetActive(i<=highestPage); contents[i].SetActive(i==page);
        }
        papers[page].gameObject.SetActive(true); papers[page].SetAsLastSibling();
        paperclip.SetAsLastSibling();
        // Keep navigational controls and the hand above papers, not under the stack.
        back.transform.SetAsLastSibling(); next.transform.SetAsLastSibling(); hand.SetAsLastSibling();
        boss.text = tutorial ? BossDialogueStyle.HighlightControls(FeedbackReport.BossLine(page,report.passed,true)) : "";
        counter.text = CampaignProgression.GetContractName(level) + "\nPAPER " + (page+1) + " / 5";
        counter.enableAutoSizing = true; counter.fontSizeMin = 18; counter.fontSizeMax = 25;
        for (int i=0;i<5;i++)
        {
            indexSelection[i].gameObject.SetActive(i==page);
            indexLabels[i].color = i==page ? Ink : new Color32(98,88,78,255);
        }
        if (tutorial) portrait.sprite = BossPortrait(page == 4 && !report.passed ? 1 : 0);
        bool entrance = forceEntrance || page > highestPage; highestPage = Mathf.Max(highestPage,page);
        UpdateButtons();
        if (entrance || page == 4) { busy = true; UpdateButtons(); transition = StartCoroutine(Reveal(entrance)); }
    }
    private IEnumerator Reveal(bool entrance)
    {
        var paper = papers[page]; Vector2 end = resting[page];
        if (entrance)
        {
            Vector2 start = end + new Vector2(1150,720);
            for (float t=0;t<.55f;t+=Time.unscaledDeltaTime)
            {
                float u = Mathf.SmoothStep(0,1,t/.55f); paper.anchoredPosition = Vector2.LerpUnclamped(start,end,u);
                paper.localRotation = Quaternion.Euler(0,0,Mathf.Lerp(-9,0,u)); yield return null;
            }
            paper.anchoredPosition = end; paper.localRotation = Quaternion.identity;
        }
        if (page == 4)
        {
            stamp.gameObject.SetActive(false); hand.gameObject.SetActive(true);
            Vector2 away = new Vector2(1800,-300), contact = new Vector2(1270,-540);
            for (float t=0;t<.35f;t+=Time.unscaledDeltaTime)
            { hand.anchoredPosition = Vector2.Lerp(away,contact,Mathf.SmoothStep(0,1,t/.35f)); yield return null; }
            hand.anchoredPosition = contact; stamp.gameObject.SetActive(true);
            for (float t=0;t<.22f;t+=Time.unscaledDeltaTime)
            { stamp.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f,1,Mathf.SmoothStep(0,1,t/.22f)); yield return null; }
            stamp.rectTransform.localScale = Vector3.one;
            for (float t=0;t<.3f;t+=Time.unscaledDeltaTime)
            { hand.anchoredPosition = Vector2.Lerp(contact,away,Mathf.SmoothStep(0,1,t/.3f)); yield return null; }
            hand.gameObject.SetActive(false);
        }
        busy = false; transition = null; UpdateButtons();
    }
    private void UpdateButtons()
    {
        if (next == null) return;
        back.interactable = !busy && page > 0; next.interactable = !busy && !actionTaken;
        nextLabel.text = page == 4 ? proceedLabel : "NEXT";
    }
    private void Next()
    {
        if (busy || actionTaken) return;
        if (page < 4) { ShowPage(page+1,false); return; }
        actionTaken = true; UpdateButtons();
        if (tutorial && !GameSavePrefs.IsRoomSession) { GameSavePrefs.SetInt("FeedbackPaper.TutorialSeen",1); GameSavePrefs.Save(); }
        var action = proceed; Hide(); action?.Invoke();
    }
    public void RequestClose()
    {
        if (AllowClose) Hide();
    }
    public void Hide()
    {
        if (!IsOpen) return;
        StopTransition(); gameObject.SetActive(false); closed?.Invoke();
    }
    private void StopTransition()
    {
        if (transition != null) StopCoroutine(transition);
        transition = null; busy = false;
        if (hand != null) hand.gameObject.SetActive(false);
        for (int i=0;i<5;i++) if(papers[i]!=null) { papers[i].anchoredPosition = resting[i]; papers[i].localRotation = Quaternion.identity; }
    }
    private void Update()
    {
        var key = Keyboard.current;
        if (key == null || !Application.isFocused) return;
        if (HandlesEscape && key.escapeKey.wasPressedThisFrame) RequestClose();
        else if (key.rightArrowKey.wasPressedThisFrame) Next();
        else if (key.leftArrowKey.wasPressedThisFrame) ShowPage(page-1,false);
    }
    private void OnDisable() { StopTransition(); }
    private Sprite BossPortrait(int pose)
    {
        if (bossSprites[pose] != null) return bossSprites[pose];
        string[] keys = { "feedbackBossOpen", "feedbackBossHappy", "feedbackBoss" };
        var original = ExportUIArt.Get(keys[pose]);
        if (original == null) return null;
        // These original 1920x1080 PNGs contain large transparent margins.
        // Crop only the sprite UV rectangle, never the source asset or import settings.
        float[] widths = { 810f,769f,651f }, heights = { 872f,863f,871f };
        var texture = original.texture;
        bossSprites[pose] = Sprite.Create(texture,new UnityEngine.Rect(0,0,texture.width*widths[pose]/1920f,texture.height*heights[pose]/1080f),new Vector2(.5f,.5f),100);
        return bossSprites[pose];
    }
    private void OnDestroy() { foreach (var sprite in bossSprites) if (sprite != null) Destroy(sprite); }
    private static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {
        var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1); rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(w,h); return rect;
    }
    private static Image Image(Transform parent,string name,string key,float x,float y,float w,float h,bool blocks)
    {
        var image = Rect(parent,name,x,y,w,h).gameObject.AddComponent<Image>(); image.sprite = key != null ? ExportUIArt.Get(key) : null;
        image.color = image.sprite != null ? Color.white : new Color32(244,225,189,255); image.raycastTarget = blocks; return image;
    }
    private static TMP_Text Text(Transform parent,string name,string value,float x,float y,float w,float h,float size,TextAlignmentOptions align)
    {
        var text = Rect(parent,name,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>(); FeedbackTypography.Apply(text);
        text.text = value; text.fontSize = size; text.color = Ink; text.alignment = align; text.richText = true; text.raycastTarget = false;
        text.enableWordWrapping = true; text.overflowMode = TextOverflowModes.Overflow; return text;
    }
    private static Button Button(Transform parent,string name,string value,float x,float y,float w,float h,UnityEngine.Events.UnityAction action)
    {
        var image = Image(parent,name,null,x,y,w,h,true); image.color = Blue;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
        var face = Image(image.transform,"Paper button face",null,2,2,w-4,h-4,false); face.color = new Color32(241,225,195,255);
        button.targetGraphic = face;
        var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color32(255,247,228,255);
        colors.pressedColor = new Color32(200,218,227,255); colors.disabledColor = new Color(1,1,1,.45f); colors.fadeDuration = .16f; button.colors = colors;
        var label = Text(image.transform,"Label",value,12,3,w-24,h-6,31,TextAlignmentOptions.Center);
        label.enableAutoSizing = true; label.fontSizeMin = 20; label.fontSizeMax = 31; return button;
    }
    private static void Stroke(Transform parent,string name,float x,float y,float w,float h,Color color,float angle = 0)
    {
        var line = Image(parent,name,null,x,y,w,h,false); line.color = color;
        line.rectTransform.localRotation = Quaternion.Euler(0,0,angle);
    }
    private static void PaperShadow(Image paper)
    {
        var shadow = paper.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0.12f,.08f,.04f,.2f);
        shadow.effectDistance = new Vector2(7,-9); shadow.useGraphicAlpha = true;
    }
    private static void EnsureEventSystem()
    {
        var events = EventSystem.current;
        if (events == null) events = new GameObject("Feedback EventSystem",typeof(EventSystem)).GetComponent<EventSystem>();
        foreach (var legacy in events.GetComponents<StandaloneInputModule>()) legacy.enabled = false;
        var input = events.GetComponent<InputSystemUIInputModule>() ?? events.gameObject.AddComponent<InputSystemUIInputModule>();
        if (input.actionsAsset == null) input.AssignDefaultActions(); input.enabled = true;
    }
}
