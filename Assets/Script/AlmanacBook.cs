using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public partial class AlmanacManager
{
    [SerializeField] private TextMeshProUGUI bookEntryTitle,bookLeftText,bookRightText,bookHeading,bookPageNumber;
    [SerializeField] private Button bookPrevious,bookNext,bookVideo;
    private int bookPage,bookCategory = -1;
    private readonly List<string> bookBodies=new List<string>();
    private readonly List<KnowledgeEntry> bookEntries=new List<KnowledgeEntry>();
    private static readonly string[] BookCategories={"DIRECTOR","LIGHTING","AUDIO","CAMERA","EDITING"};

    private int navigationStep = -1;
    private const string NavigationLessonKey = "AlmanacGuideShown";
    private const int NavigationLessonVersion = 2;
    private bool navigationLessonRequested;
    public void RequestNavigationLesson()
    {
        // Replay for an explicit tutorial introduction without erasing saved progress.
        navigationLessonRequested = true;
    }
    public bool IsNavigationLessonActive => navigationStep >= 0;
    private TutorialUIManager navigationUI;
    private Canvas navigationCanvas;
    private CanvasGroup navigationGroup;
    private bool navigationOverrideSorting, navigationBlocksRaycasts, navigationTasksVisible;
    private int navigationSortingOrder;
    private bool navigationAwaitingSpace;
    [SerializeField] private Button navigationLightingButton;
    [SerializeField] private GameObject navigationFrame;
    private struct NavigationButtonGate
    {
        public CanvasGroup group;
        public bool interactable;
        public bool blocksRaycasts;
    }
    private readonly List<NavigationButtonGate> navigationButtonGates = new List<NavigationButtonGate>();
    private TutorialHighlighter navigationHighlighter;
    private Canvas navigationHighlightCanvas;
    private int navigationHighlightOrder;
    private bool navigationHighlightOverride;
    private GameObject navigationTaskPanel;
    private bool techniqueReviewRequested;
    private Canvas techniqueHighlightCanvas;
    private int techniqueHighlightOrder;
    private bool techniqueHighlightOverride, techniqueHighlightOwned;

    public void RequestTechniqueReviewHighlight()
    {
        techniqueReviewRequested = true;
    }

    private void UpdateTechniqueReviewHighlight()
    {
        bool show = techniqueReviewRequested && isAlmanacOpen && !AlmanacTransitionBusy() && navigationStep < 0 && knowledgeCategoryFilter != 2;
        if (!show)
        {
            if (techniqueHighlightOwned)
            {
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();
                if (techniqueHighlightCanvas != null)
                {
                    techniqueHighlightCanvas.sortingOrder = techniqueHighlightOrder;
                    techniqueHighlightCanvas.overrideSorting = techniqueHighlightOverride;
                }
                techniqueHighlightOwned = false;
            }
            if (isAlmanacOpen && !AlmanacTransitionBusy() && navigationStep < 0 && knowledgeCategoryFilter == 2) techniqueReviewRequested = false;
            return;
        }
        if (techniquesKnowledgeButton == null || TutorialHighlighter.Instance == null) return;
        if (!techniqueHighlightOwned)
        {
            techniqueHighlightCanvas = TutorialHighlighter.Instance.GetComponentInParent<Canvas>();
            if (techniqueHighlightCanvas != null)
            {
                techniqueHighlightOrder = techniqueHighlightCanvas.sortingOrder;
                techniqueHighlightOverride = techniqueHighlightCanvas.overrideSorting;
                techniqueHighlightCanvas.overrideSorting = true;
                var bookCanvas = almanacCanvas.GetComponent<Canvas>();
                techniqueHighlightCanvas.sortingOrder = bookCanvas != null ? bookCanvas.sortingOrder + 1 : 61;
            }
            techniqueHighlightOwned = true;
            TutorialHighlighter.Instance.HighlightElement(techniquesKnowledgeButton.GetComponent<RectTransform>());
        }
    }
    private TextMeshProUGUI navigationTaskText;
    private static readonly string[] NavigationTasks = {
        "Click EQUIPMENTS above the book",
        "Click the LIGHTING ribbon on the right",
        "Click TECHNIQUES above the book",
        "Click the right arrow to turn the page"
    };

    private Button NavigationTarget()
    {
        switch (navigationStep)
        {
            case 0: return equipmentKnowledgeButton;
            case 1: return navigationLightingButton;
            case 2: return techniquesKnowledgeButton;
            case 3: return bookNext;
            default: return null;
        }
    }

    private void ClearNavigationFocus()
    {
        if (navigationHighlighter != null) navigationHighlighter.HideHighlight();
        if (navigationTaskPanel != null) navigationTaskPanel.SetActive(false);
        if (navigationFrame != null) navigationFrame.SetActive(false);
        foreach (var gate in navigationButtonGates)
        {
            if (gate.group == null) continue;
            gate.group.interactable = gate.interactable;
            gate.group.blocksRaycasts = gate.blocksRaycasts;
        }
        navigationButtonGates.Clear();
    }

    private void SetNavigationFocus(Button target, bool allowTargetClick = true)
    {
        ClearNavigationFocus();
        if (AlmanacTransitionBusy()) return;
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        foreach (var button in Application.isPlaying ? almanacCanvas.GetComponentsInChildren<Button>(true) : new Button[0])
        {
            if ((button == target && allowTargetClick) || button == closeButton) continue;
            // Reuse groups: Destroy is deferred and re-adding in the same frame fails.
            var gate = button.GetComponent<CanvasGroup>();
            if (gate == null) gate = button.gameObject.AddComponent<CanvasGroup>();
            navigationButtonGates.Add(new NavigationButtonGate
            {
                group = gate,
                interactable = gate.interactable,
                blocksRaycasts = gate.blocksRaycasts
            });
            gate.interactable = false;
            gate.blocksRaycasts = false;
        }
        if (target == null) return;
        if (Application.isPlaying && navigationHighlighter != null)
        {
            navigationHighlighter.HighlightElement(target.GetComponent<RectTransform>());
            return;
        }
        if (navigationFrame != null)
        {
            navigationFrame.transform.SetParent(target.transform, false);
            navigationFrame.SetActive(true);
            return;
        }
        navigationFrame = new GameObject("Almanac tutorial square", typeof(RectTransform));
        var frame = navigationFrame.GetComponent<RectTransform>();
        frame.SetParent(target.transform, false);
        frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one;
        frame.offsetMin = new Vector2(-8, -8); frame.offsetMax = new Vector2(8, 8);
        for (int i = 0; i < 4; i++)
        {
            var edge = new GameObject("Border", typeof(RectTransform), typeof(Image));
            var rect = edge.GetComponent<RectTransform>();
            rect.SetParent(frame, false);
            bool horizontal = i < 2;
            rect.anchorMin = horizontal ? new Vector2(0, i) : new Vector2(i - 2, 0);
            rect.anchorMax = horizontal ? new Vector2(1, i) : new Vector2(i - 2, 1);
            rect.sizeDelta = horizontal ? new Vector2(0, 4) : new Vector2(4, 0);
            rect.anchoredPosition = Vector2.zero;
            var image = edge.GetComponent<Image>();
            image.color = new Color(1f, .8f, .15f);
            image.raycastTarget = false;
        }
    }

    private void UpdateNavigationLesson()
    {
        if (AlmanacTransitionBusy()) return;
        if (navigationStep >= 0 && DevTutorialBypass.Disabled) { EndNavigationLesson(); return; }
        if (!isAlmanacOpen || !navigationAwaitingSpace || navigationUI == null ||
            PauseManager.isPaused || !Application.isFocused ||
            !TutorialUIManager.BossContinuePressed) return;
        if (navigationUI.TryAdvanceBossDialoguePage()) return;
        if (!navigationUI.CanAdvanceBossDialogue()) return;
        navigationAwaitingSpace = false;
        navigationUI.HideBossDialogue();
        if (navigationStep >= 4) { EndNavigationLesson(); return; }
        SetNavigationFocus(NavigationTarget());
        ShowNavigationTask();
    }

    private void ShowNavigationTask()
    {
        if (navigationTaskPanel == null)
        {
            var rows = navigationUI != null ? navigationUI.taskRows : null;
            var sourceRow = rows != null && rows.Length > 0 ? rows[0] : null;
            var sourceRect = sourceRow != null && sourceRow.rowContainer != null
                ? sourceRow.rowContainer.transform as RectTransform : null;
            var sourceImage = sourceRect != null ? sourceRect.GetComponent<Image>() : null;
            navigationTaskPanel = CreatePanel("Almanac tutorial task",
                sourceRect != null ? sourceRect.parent : almanacCanvas.transform,
                sourceImage != null ? sourceImage.color : new Color(0, 0, 0, .2f));
            navigationTaskPanel.GetComponent<Image>().raycastTarget = false;
            var rect = navigationTaskPanel.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0, 1);
            SetRect(rect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -135), new Vector2(330, 90));
            if (sourceRect != null)
            {
                rect.anchorMin = sourceRect.anchorMin;
                rect.anchorMax = sourceRect.anchorMax;
                rect.pivot = sourceRect.pivot;
                rect.sizeDelta = sourceRect.sizeDelta;
                rect.anchoredPosition = sourceRect.anchoredPosition;
                rect.localScale = sourceRect.localScale;
            }
            var canvas = navigationTaskPanel.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            var bookCanvas = almanacCanvas.GetComponent<Canvas>();
            canvas.sortingLayerID = bookCanvas != null ? bookCanvas.sortingLayerID : 0;
            canvas.sortingOrder = bookCanvas != null ? bookCanvas.sortingOrder + 2 : 62;
            navigationTaskText = CreateText("Instruction", navigationTaskPanel.transform, "", 20, TextAlignmentOptions.MidlineLeft);
            navigationTaskText.color = Color.white;
            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null) navigationTaskText.font = font;
            navigationTaskText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            navigationTaskText.enableAutoSizing = false;
            navigationTaskText.enableWordWrapping = true;
            navigationTaskText.overflowMode = TextOverflowModes.Overflow;
            navigationTaskText.raycastTarget = false;
            navigationTaskText.characterSpacing = 0;
            SetStretchRect(navigationTaskText.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 6), new Vector2(-4, -6));
        }
        navigationTaskText.text = NavigationTasks[navigationStep];
        var taskRect = navigationTaskPanel.GetComponent<RectTransform>();
        float previousHeight = taskRect.rect.height;
        float height = Mathf.Max(36f, navigationTaskText.GetPreferredValues(
            navigationTaskText.text, Mathf.Max(1f, taskRect.rect.width - 8f), Mathf.Infinity).y + 12f);
        taskRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        taskRect.anchoredPosition += Vector2.down * ((height - previousHeight) * (1f - taskRect.pivot.y));
        navigationTaskPanel.SetActive(true);
    }

    private void BeginNavigationLesson()
    {
        if (AlmanacTransitionBusy()) { navigationAfterCover = true; return; }
        var ui = TutorialUIManager.Instance;
        if (DevTutorialBypass.Disabled || ui == null || ui.bossHUDCanvas == null ||
            (!navigationLessonRequested && GameSavePrefs.GetInt(NavigationLessonKey, 0) >= NavigationLessonVersion)) return;

        navigationLessonRequested = false;

        navigationUI = ui;
        navigationHighlighter = TutorialHighlighter.Instance;
        navigationHighlightCanvas = navigationHighlighter != null ? navigationHighlighter.GetComponentInParent<Canvas>() : null;
        if (navigationHighlightCanvas != null)
        {
            navigationHighlightOrder = navigationHighlightCanvas.sortingOrder;
            navigationHighlightOverride = navigationHighlightCanvas.overrideSorting;
            navigationHighlightCanvas.overrideSorting = true;
            var bookCanvas = almanacCanvas.GetComponent<Canvas>();
            navigationHighlightCanvas.sortingOrder = bookCanvas != null ? bookCanvas.sortingOrder + 1 : 61;
        }
        navigationTasksVisible = ui.taskPanel != null && ui.taskPanel.activeSelf;
        navigationCanvas = ui.bossHUDCanvas.GetComponent<Canvas>();
        navigationGroup = ui.bossHUDCanvas.GetComponent<CanvasGroup>();
        if (navigationCanvas != null)
        {
            navigationOverrideSorting = navigationCanvas.overrideSorting;
            navigationSortingOrder = navigationCanvas.sortingOrder;
            navigationCanvas.overrideSorting = true;
            var bookCanvas = almanacCanvas.GetComponent<Canvas>();
            navigationCanvas.sortingOrder = Mathf.Max(navigationSortingOrder, bookCanvas != null ? bookCanvas.sortingOrder + 3 : 63);
        }
        if (navigationGroup != null)
        {
            navigationBlocksRaycasts = navigationGroup.blocksRaycasts;
            navigationGroup.blocksRaycasts = false;
        }
        navigationStep = 0;
        ShowNavigationLesson();
    }

    private void NavigationAction(int step)
    {
        if (navigationStep != step || navigationAwaitingSpace) return;
        navigationStep++;
        if (navigationStep == 3 && !bookNext.interactable) navigationStep++;
        ShowNavigationLesson();
    }

    private void ShowNavigationLesson()
    {
        if (navigationUI == null || navigationStep < 0) return;
        if (AlmanacTransitionBusy()) { navigationPresentationPending = true; return; }
        string[] instructions = {
            "Welcome to your Almanac! Click EQUIPMENTS above the book. These pages explain what each tool does and its controls.",
            "The ribbons on the right filter the book by subject. Let's try Lighting. After our chat, click the highlighted light ribbon.",
            "Now click TECHNIQUES above the book. Equipment pages explain the tools; techniques teach you how and why to use them in your commercial.",
            "Click the right arrow to turn a page. The left arrow takes you back, and the page number tells you where you are.",
            "You've got it! Read at your own pace. Press P or the red X to close. You can reopen the Almanac with P whenever you need a reminder."
        };
        navigationAwaitingSpace = true;
        SetNavigationFocus(null, false);
        navigationUI.ShowBossDialogue(instructions[Mathf.Min(navigationStep, 4)], navigationUI.poseOpenHand, true, false);
    }

    private void EndNavigationLesson()
    {
        navigationAfterCover = navigationPresentationPending = false;
        if (navigationStep < 0) return;
        if (navigationStep >= 4)
        {
            GameSavePrefs.SetInt(NavigationLessonKey, NavigationLessonVersion);
            GameSavePrefs.Save();
        }
        navigationStep = -1;
        navigationAwaitingSpace = false;
        ClearNavigationFocus();
        if (navigationHighlightCanvas != null)
        {
            navigationHighlightCanvas.sortingOrder = navigationHighlightOrder;
            navigationHighlightCanvas.overrideSorting = navigationHighlightOverride;
        }
        navigationHighlighter = null;
        navigationHighlightCanvas = null;
        if (navigationUI != null)
        {
            navigationUI.HideBossDialogue();
            if (navigationUI.taskPanel != null) navigationUI.taskPanel.SetActive(navigationTasksVisible);
        }
        if (navigationCanvas != null)
        {
            navigationCanvas.sortingOrder = navigationSortingOrder;
            navigationCanvas.overrideSorting = navigationOverrideSorting;
        }
        if (navigationGroup != null) navigationGroup.blocksRaycasts = navigationBlocksRaycasts;
        navigationUI = null;
    }
    private GameObject boundBookCanvas;
    private void BindBookButtons()
    {
        if (bookEntryTitle == null || boundBookCanvas == almanacCanvas) return;
        boundBookCanvas = almanacCanvas;
        bookPrevious.onClick.AddListener(() => TurnBookPage(-1));
        bookNext.onClick.AddListener(() => TurnBookPage(1));
        if (bookVideo != null) bookVideo.gameObject.SetActive(false);
        equipmentKnowledgeButton.onClick.AddListener(() => AnimateBookSelection(equipmentKnowledgeButton, true, () => { bookPage=0; bookCategory=-1; ShowEquipmentKnowledge(); NavigationAction(0); }));
        techniquesKnowledgeButton.onClick.AddListener(() => AnimateBookSelection(techniquesKnowledgeButton, true, () => { bookPage=0; bookCategory=-1; ShowTechniqueKnowledge(); NavigationAction(2); }));
        var book = equipmentKnowledgeButton.transform.parent;
        for (int i=0;i<BookCategories.Length;i++)
        {
            int category=i;
            var bookmark = book.Find("Category " + BookCategories[i]).GetComponent<Button>();
            bookmark.onClick.AddListener(() => AnimateBookSelection(bookmark, false, () =>
            { bookCategory=category; bookPage=0; OpenTab(1); RefreshBookPage(); if (category == 1) NavigationAction(1); }));
        }
        if (techniqueGuidePanel != null && ruleOfThirdsGuidePlayer != null)
        {
            techniqueGuidePanel.transform.Find("Close Guide Button").GetComponent<Button>().onClick.AddListener(CloseTechniqueGuide);
            techniqueGuidePanel.transform.Find("Play Pause Button").GetComponent<Button>().onClick.AddListener(ruleOfThirdsGuidePlayer.TogglePlayPause);
            techniqueGuidePanel.transform.Find("Replay Button").GetComponent<Button>().onClick.AddListener(ruleOfThirdsGuidePlayer.Replay);
        }
    }
#if UNITY_EDITOR
    public void BakeHierarchyUI()
    {
        BuildIllustratedBook();
        EnsureIllustrationNote();
        BuildProfileUI();
        SetNavigationFocus(bookNext);
        ClearNavigationFocus();
        almanacCanvas.SetActive(false);
    }
#endif

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


        closeButton=BookButton(root.transform,"Close book","","close",new Vector2(820,440),new Vector2(86,99));
        bookPrevious=BookButton(root.transform,"Previous page","","left",new Vector2(-813,49),new Vector2(62,93));
        bookNext=BookButton(root.transform,"Next page","","right",new Vector2(898,49),new Vector2(62,93));

        for(int i=0;i<BookCategories.Length;i++)
        {
            int category=i;
            var button=BookButton(root.transform,"Category "+BookCategories[i],"",new[]{"psdDirector","psdLight","psdAudio","psdCamera","psdEdit"}[i],new Vector2(780,215-i*108),new Vector2(122,82));
            if (category == 1) navigationLightingButton = button;

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
        // The Almanac uses illustrated notes; no Watch Guide button is created.

        // Preserve profile, milestones and the interactive guide without crowding the two main tabs.
        var other=CreatePanel("Other book pages",root.transform,Color.clear);SetStretchRect(other.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,new Vector2(95,70),new Vector2(-95,-100));
        BuildPlayerInfoPanel(other.transform);BuildAchievementsPanel(other.transform);
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
    private Coroutine pageTurn;
    private bool bookSelectionAnimating;
    private bool finishingBookClose;

    private System.Collections.IEnumerator AnimateBookClose()
    {
        bookSelectionAnimating = true;
        try
        {
            EndNavigationLesson();
            ClearNavigationFocus();
            UpdateTechniqueReviewHighlight();
            var motion = PlayAlmanacCoverClosing(true);
            while (isAlmanacOpen && motion != null && motion.IsPlaying) yield return null;
        }
        finally { bookSelectionAnimating = false; }
        if (!isAlmanacOpen) yield break;
        finishingBookClose = true;
        try { ToggleAlmanac(); }
        finally { finishingBookClose = false; }
    }

    private void AnimateBookSelection(Button button, bool newBook, System.Action select)
    {
        if (AlmanacTransitionBusy() || !isAlmanacOpen) return;
        CancelPageTurn();
        if (newBook) StartCoroutine(SwitchAlmanacSection(select));
        else StartCoroutine(FlipBookStack(1, select, false, 1, .45f));
    }

    private System.Collections.IEnumerator SwitchAlmanacSection(System.Action select)
    {
        bookSelectionAnimating = true;
        try
        {
            ClearNavigationFocus();
            UpdateTechniqueReviewHighlight();
            PaperMenuAudio.Play(false);
            var motion = PlayAlmanacCoverClosing(false);
            while (isAlmanacOpen && motion != null && motion.IsPlaying) yield return null;
            if (!isAlmanacOpen) yield break;
            select?.Invoke();
            PlayAlmanacCoverOpening(false);
            while (isAlmanacOpen && AlmanacCoverOpening()) yield return null;
        }
        finally { bookSelectionAnimating = false; }
        UpdateAlmanacPresentation();
    }

    private GameObject turningPaper;
    private bool applyingPageTurn;
    private readonly System.Collections.Generic.Dictionary<TextMeshProUGUI, bool> turningTextVisibility = new System.Collections.Generic.Dictionary<TextMeshProUGUI, bool>();

    private void RestoreTurningText()
    {
        foreach (var entry in turningTextVisibility)
            if (entry.Key != null) entry.Key.enabled = entry.Value;
        turningTextVisibility.Clear();
        RestoreIllustrationAfterTurn();
        RestoreFieldNotesAfterTurn();
    }

    private void CopyWholePage(Transform paper, bool leftPage)
    {
        RestoreTurningText();
        CopyIllustrationForTurn(paper, leftPage);
        CopyFieldNotesForTurn(paper, leftPage);
        foreach (var text in leftPage ? new[] { bookHeading, bookEntryTitle, bookLeftText } : new[] { bookRightText, bookPageNumber })
        {
            if (text == null) continue;
            CopyTurningText(text, paper);
            turningTextVisibility[text] = text.enabled;
            text.enabled = false;
        }
    }

    private void CancelPageTurn()
    {
        RestoreTurningText();
        if (pageTurn != null) StopCoroutine(pageTurn);
        pageTurn = null;
        if (turningPaper != null)
        {
            turningPaper.SetActive(false);
            Destroy(turningPaper);
        }
        turningPaper = null;
    }

    private void TurnBookPage(int direction)
    {
        if (pageTurn != null || bookSelectionAnimating || !isAlmanacOpen) return;
        int next = bookPage + direction;
        if (next < 0 || next >= bookBodies.Count) return;
        StartCoroutine(FlipBookStack(direction, () => bookPage = next, true, 1, .45f));
    }

    private System.Collections.IEnumerator FlipBookStack(int direction, System.Action select, bool notify = true, int turns = 5, float turnDuration = .18f)
    {
        bookSelectionAnimating = true;
        try
        {
            for (int i = 0; i < turns && isAlmanacOpen; i++)
                yield return AnimateBookPage(direction, bookPage, turnDuration, i == turns - 1 ? select : null, false);
            if (isAlmanacOpen && notify && direction > 0) NavigationAction(3);
        }
        finally
        {
            CancelPageTurn();
            bookSelectionAnimating = false;
        }
    }

    private void CopyTurningText(TextMeshProUGUI source, Transform paper)
    {
        var copy = Instantiate(source, source.transform.parent);
        copy.transform.SetParent(paper, true);
        copy.raycastTarget = false;
    }

    private System.Collections.IEnumerator AnimateBookPage(int direction, int next, float turnDuration = 1.1f, System.Action select = null, bool notify = true)
    {
        PaperMenuAudio.Play(false);
        // Original spine-hinged turn, using the book's own illustrated paper.
        turningPaper = new GameObject("Turning Almanac leaf", typeof(RectTransform), typeof(AlmanacRoundedPage));
        turningPaper.transform.SetParent(knowledgePanel.transform, false);
        var paper = turningPaper.GetComponent<RectTransform>();
        paper.anchorMin = paper.anchorMax = Vector2.one * .5f;
        paper.pivot = new Vector2(direction > 0 ? 0 : 1, .5f);
        paper.anchoredPosition = new Vector2(0, 15);
        paper.sizeDelta = new Vector2(705, 865);
        var leaf = turningPaper.GetComponent<RawImage>();
        var artwork = ExportUIArt.Get("psdBook");
        if (artwork != null) leaf.texture = artwork.texture;
        leaf.uvRect = new Rect((direction > 0 ? 800f : 40f) / 1495f, 50f / 937f, 650f / 1495f, 865f / 937f);
        leaf.raycastTarget = false;
        var shadow = turningPaper.AddComponent<Shadow>();
        shadow.effectColor = Color.clear;
        shadow.effectDistance = Vector2.zero;
        CopyWholePage(paper, direction < 0);
        bookPrevious.interactable = bookNext.interactable = false;
        bool swapped = false;
        float elapsed = 0f;
        while (elapsed < turnDuration && isAlmanacOpen && knowledgePanel != null && knowledgePanel.activeInHierarchy)
        {
            float t = Mathf.Clamp01(elapsed / turnDuration);
            float progress = .5f - .5f * Mathf.Cos(t * Mathf.PI);
            if (!swapped && progress >= .5f)
            {
                // Replace content while the leaf is edge-on, avoiding mirrored text.
                paper.localScale = Vector3.one;
                paper.localRotation = Quaternion.identity;
                foreach (Transform child in paper) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                bookPage = next;
                applyingPageTurn = true;
                if (select != null) { select(); next = bookPage; }
                RefreshBookPage();
                applyingPageTurn = false;
                paper.pivot = new Vector2(direction > 0 ? 1 : 0, .5f);
                leaf.uvRect = new Rect((direction > 0 ? 40f : 800f) / 1495f, 50f / 937f, 650f / 1495f, 865f / 937f);
                CopyWholePage(paper, direction > 0);
                swapped = true;
                bookPrevious.interactable = bookNext.interactable = false;
            }
            float lift = Mathf.Sin(progress * Mathf.PI);
            paper.localScale = new Vector3(Mathf.Max(.001f, Mathf.Abs(Mathf.Cos(progress * Mathf.PI))), 1f + lift * .008f, 1f);
            // Continuous tilt crosses zero at the spine instead of snapping signs.
            paper.localRotation = Quaternion.Euler(0, 0, direction * Mathf.Sin(progress * Mathf.PI * 2f) * .8f);
            leaf.color = Color.Lerp(Color.white, new Color(.76f, .7f, .6f), lift * .45f);
            shadow.effectColor = new Color(0, 0, 0, .18f * lift);
            shadow.effectDistance = new Vector2(direction * 20f * lift * Mathf.Cos(progress * Mathf.PI), -3f * lift);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        bool completed = isAlmanacOpen && knowledgePanel != null && knowledgePanel.activeInHierarchy;
        pageTurn = null;
        if (turningPaper != null) { turningPaper.SetActive(false); Destroy(turningPaper); }
        turningPaper = null;
        RestoreTurningText();
        if (completed) bookPage = next;
        if (bookEntryTitle != null) RefreshBookPage();
        if (completed && notify && direction > 0) NavigationAction(3);
    }

    // One owner per guide. Titles are editable and must not determine equipment ownership.
    // General production/staging guides stay in All Guides, not the actor megaphone ribbon.
    private static int GetKnowledgeRibbon(string id)
    {
        switch (id)
        {
            case "actor_megaphone":
            case "hiring_and_posing_actors":
            case "lifestyle_staging":
                return 0; // Director / megaphone: actor performance only.

            case "led_panel":
            case "level_3_soft_light":
            case "basic_product_lighting":
            case "three_point_lighting":
            case "soft_light_technique":
            case "motivated_lighting":
                return 1;

            case "quiet_movement":
                return 2; // Avoiding footstep noise on set.

            case "nony_fx_camera":
            case "sd_card":
            case "camera_white_balance":
            case "camera_exposure":
            case "center_framing":
            case "rule_of_thirds":
            case "recording_workflow":
            case "recording_technique":
            case "automotive_staging":
            case "vehicle_rim_lighting": // Legacy ID; this now teaches smooth camera movement.
            case "shot_coverage":
            case "visual_hierarchy":
                return 3;

            case "editing_computer":
            case "post_production_workflow":
            case "post_production_technique":
            case "commercial_color_grading":
            case "advertising_post_production":
            case "screen_continuity": // Splitting and trimming timeline clips.
            case "warm_commercial_grade":
            case "quality_control":
                return 4;

            default:
                return -1; // General or unclassified guides are available in All Guides only.
        }
    }

    private void RefreshBookPage()
    {
        if (!applyingPageTurn) CancelPageTurn();
        HideFieldNotes();
        bookBodies.Clear();bookEntries.Clear();var entries=new List<KnowledgeEntry>();
        foreach(var entry in database)
        {
            if(!entry.isUnlocked||stagedHiddenKnowledge.Contains(entry.id))continue;
            if(entry.category!=(knowledgeCategoryFilter==2?"Technique":"Equipment"))continue;
            bool match=bookCategory<0 || GetKnowledgeRibbon(entry.id)==bookCategory;
            if(match)entries.Add(entry);
        }
        entries.Sort(CompareKnowledgeEntries);
        foreach(var entry in entries)
        {
            var notes = BuildFieldNoteBlocks(entry);
            for (int i = 0; i < notes.Count; i += 3)
            {
                bookBodies.Add(string.Join("\n\n", notes.GetRange(i, Mathf.Min(3, notes.Count - i))));
                bookEntries.Add(entry);
            }
        }
        if (!string.IsNullOrEmpty(fieldNotesAnchorId))
        {
            int anchored = bookEntries.FindIndex(entry => entry.id == fieldNotesAnchorId);
            if (anchored >= 0) bookPage = anchored;
            fieldNotesAnchorId = null;
        }
        bookPage=Mathf.Clamp(bookPage,0,Mathf.Max(0,bookBodies.Count-1));bookHeading.text=knowledgeCategoryFilter==2?"TECHNIQUES":"EQUIPMENTS";
        bookPrevious.interactable=bookPage>0;bookNext.interactable=bookPage+1<bookBodies.Count;
        if (bookVideo != null) bookVideo.gameObject.SetActive(false);
        if(bookEntries.Count==0){if(illustrationNote!=null)illustrationNote.gameObject.SetActive(false);bookLeftText.gameObject.SetActive(true);bookEntryTitle.text=(bookCategory < 0 ? "ALL GUIDES" : BookCategories[bookCategory]);bookLeftText.text="Complete the matching lessons to unlock these pages.";bookRightText.text="Your equipment controls and techniques appear here as you learn them.";bookPageNumber.text="0 / 0";return;}
        bookEntryTitle.text=bookEntries[bookPage].title;string body=bookBodies[bookPage];int cut=Mathf.Min(360,body.Length);
        int instructions=body.IndexOf("HOW TO USE",System.StringComparison.OrdinalIgnoreCase);
        if(instructions>0&&instructions<450)cut=instructions;
        else if(cut<body.Length){int paragraph=body.LastIndexOf('\n',cut-1,cut);if(paragraph>120)cut=paragraph;else {int space=body.LastIndexOf(' ',cut-1,cut);if(space>0)cut=space;}}
        bookLeftText.text=body.Substring(0,cut);bookRightText.text=body.Substring(cut).TrimStart();bookPageNumber.text=(bookPage+1)+" / "+bookBodies.Count;
        ApplyIllustratedArticle(bookEntries[bookPage], body);
        ShowFieldNotes(bookEntries[bookPage], body);
        UpdateKnowledgeFilterButtons();
    }
}

