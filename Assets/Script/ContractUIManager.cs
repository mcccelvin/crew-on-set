using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class ContractUIManager : MonoBehaviour
{
    public static ContractUIManager Instance;

    [Header("Optional Contract UI Override")]
    public GameObject contractCanvas;
    public GameObject offerPanel;
    public GameObject qualificationsPanel;
    public Button acceptButton;
    public Button declineButton;
    public TextMeshProUGUI declineMessageText;

    private Action acceptContractAction;
    private bool qualificationsUnlocked = false;
    private bool isQualificationsOpen = false;
    private Player.Manager.InputManager inputManager;
    private Player.PlayerController.PlayerController playerController;
    private bool playerCouldMove = true;
    private bool playerCouldLook = true;
    private bool isLevel3Contract = false;
    private int activeContractLevel = 2;
    private int browsedContractLevel = 2;
    private bool acceptanceBriefPending;
    private string liveTitle="GOKE COLA", liveDescription="", detailedRequirements="";
    private TextMeshProUGUI[] folderTitles;
    private TextMeshProUGUI selectionStatus, briefTitle, briefBody;
    private ScrollRect briefScroll;

    private readonly Color backgroundColor = new Color(0.025f, 0.035f, 0.05f, 0.96f);
    private readonly Color cardColor = new Color(0.55f, 0.35f, 0.13f, 1f);
    private readonly Color cardInnerColor = new Color(0.12f, 0.095f, 0.07f, 0.96f);
    private readonly Color blueColor = new Color(0.04f, 0.38f, 0.72f, 1f);
    private readonly Color redColor = new Color(0.68f, 0.12f, 0.08f, 1f);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        if (contractCanvas == null) BuildRuntimeUI();

        if (acceptButton != null) acceptButton.onClick.AddListener(AcceptContract);
        if (declineButton != null) declineButton.onClick.AddListener(DeclineContract);

        if (contractCanvas != null) contractCanvas.SetActive(false);
    }

    private void Update()
    {
        if (PauseManager.isPaused) return;
        if (inputManager == null) inputManager = FindObjectOfType<Player.Manager.InputManager>();

        Keyboard keyboard = Keyboard.current;
        bool contextPanelPressed = (inputManager != null && inputManager.ContextPanel) ||
                                   (keyboard != null && keyboard.tabKey.wasPressedThisFrame);

        if (contextPanelPressed && CanToggleQualifications())
        {
            ToggleQualifications();
        }
    }

    public void ShowGokeContract(Action onAccepted)
    {
        PrepareGokeContract();
        ShowContract(onAccepted);
    }

    public void ShowLevel3Contract(Action onAccepted)
    {
        PrepareLevel3Contract();
        ShowContract(onAccepted);
    }

    public void ShowLevel4Contract(Action onAccepted)
    {
        PrepareCampaignContract(4);
        ShowContract(onAccepted);
    }

    public void ShowLevel5Contract(Action onAccepted)
    {
        PrepareCampaignContract(5);
        ShowContract(onAccepted);
    }

    public void PrepareLevel3Contract()
    {
        activeContractLevel = 3;
        isLevel3Contract = true;
        ConfigureLevel3Contract();
    }

    public void PrepareGokeContract()
    {
        activeContractLevel = 2;
        isLevel3Contract = false;
        ConfigureGokeContract();
    }

    public void PrepareCampaignContract(int level)
    {
        activeContractLevel = Mathf.Clamp(level, 4, 5);
        isLevel3Contract = false;

        if (activeContractLevel == 4) ConfigureLevel4Contract();
        else ConfigureLevel5Contract();
    }

    private void ShowContract(Action onAccepted)
    {
        acceptContractAction = onAccepted;
        acceptanceBriefPending=false;
        browsedContractLevel=activeContractLevel;
        RefreshFolderSelection();
        var tutorial=FindObjectOfType<TutorialManager>();
        if(tutorial!=null&&acceptButton!=null)tutorial.acceptContractButtonRect=acceptButton.GetComponent<RectTransform>();

        if (declineMessageText != null) declineMessageText.text = "";
        if (offerPanel != null) offerPanel.SetActive(true);
        if (qualificationsPanel != null) qualificationsPanel.SetActive(false);
        if (contractCanvas != null) contractCanvas.SetActive(true);

        LockPlayer();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void UnlockQualifications()
    {
        qualificationsUnlocked = true;
    }

    public bool CanToggleQualifications()
    {
        if (!qualificationsUnlocked) return false;
        if (offerPanel != null && offerPanel.activeSelf) return false;
        if (AlmanacManager.Instance != null && AlmanacManager.Instance.IsOpen()) return false;
        if (CampaignLevelManager.Instance != null && !CampaignLevelManager.Instance.CanOpenContractQualifications()) return false;
        if (Level3Manager.Instance != null && !Level3Manager.Instance.CanOpenContractQualifications()) return false;
        if (GokeLevelManager.Instance != null && !GokeLevelManager.Instance.CanOpenContractQualifications()) return false;
        return true;
    }

    public bool IsQualificationsOpen()
    {
        return isQualificationsOpen;
    }

    public bool IsContractUIOpen()
    {
        return contractCanvas != null && contractCanvas.activeSelf;
    }

    private void AcceptContract()
    {
        if(briefBody!=null)
        {
            if(browsedContractLevel!=activeContractLevel||acceptanceBriefPending)return;
            acceptanceBriefPending=true;
            offerPanel.SetActive(false);qualificationsPanel.SetActive(true);
            RefreshBrief();
            return;
        }
        CompleteContractAcceptance();
    }

    public void ShowFlowerContract(Action onAccepted)
    {
        activeContractLevel=1;isLevel3Contract=false;
        SetContractText("ARTISAN FLOWER VASE","CLIENT: FLORA & FORM HOME\n\nCLIENT OBJECTIVE\nCreate a clear, inviting product commercial.\n\nSTAGE — Pink backdrop and one flower vase.\nCAMERA — Frame the full product in the center.\nLIGHT — Aim one panel light to show the flowers clearly.\nEDIT — A 10-second commercial.\nOVERLAYS — Eccentric Centerpiece first, then Flora & Form Home.\nCOLOR — Keep the product readable; avoid excessive brightness or darkness.\n\nSTARTING BUDGET: "+ProductionEconomy.StartingBudget.ToString("N0")+" B-COINS");
        detailedRequirements="Follow the boss's equipment, staging, filming and editing lessons. Keep graphics within the title-safe guide and leave the product visible.";
        ShowContract(onAccepted);
    }

    private void CompleteContractAcceptance()
    {
        acceptanceBriefPending=false;
        qualificationsUnlocked = true;

        if (offerPanel != null) offerPanel.SetActive(false);
        if (qualificationsPanel != null) qualificationsPanel.SetActive(false);
        if (contractCanvas != null) contractCanvas.SetActive(false);

        UnlockPlayer();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Action acceptedAction = acceptContractAction;
        acceptContractAction = null;
        if (acceptedAction != null) acceptedAction.Invoke();
    }

    private void DeclineContract()
    {
        if (declineMessageText != null)
        {
            if (activeContractLevel == 5)
                declineMessageText.text = "You can review the requirements, but the Haraya contract must be accepted to continue Level 5.";
            else if (activeContractLevel == 4)
                declineMessageText.text = "You can review the requirements, but the Kape Kultura contract must be accepted to continue Level 4.";
            else if (isLevel3Contract)
                declineMessageText.text = "You can review the requirements, but the Terrari contract must be accepted to continue Level 3.";
            else
                declineMessageText.text = "You can review the requirements, but this contract must be accepted to continue Level 2.";
        }
    }

    private void ToggleQualifications()
    {
        if(acceptanceBriefPending){CloseIllustratedBrief();return;}
        isQualificationsOpen = !isQualificationsOpen;

        if (contractCanvas != null) contractCanvas.SetActive(isQualificationsOpen);
        if (offerPanel != null) offerPanel.SetActive(false);
        if (qualificationsPanel != null) qualificationsPanel.SetActive(isQualificationsOpen);

        if (isQualificationsOpen)
        {
            RefreshBrief();
            LockPlayer();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (CampaignLevelManager.Instance != null) CampaignLevelManager.Instance.OnContractQualificationsOpened();
            if (GokeLevelManager.Instance != null) GokeLevelManager.Instance.OnContractQualificationsOpened();
            if (Level3Manager.Instance != null) Level3Manager.Instance.OnContractQualificationsOpened();
        }
        else
        {
            UnlockPlayer();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (CampaignLevelManager.Instance != null) CampaignLevelManager.Instance.OnContractQualificationsClosed();
            if (GokeLevelManager.Instance != null) GokeLevelManager.Instance.OnContractQualificationsClosed();
            if (Level3Manager.Instance != null) Level3Manager.Instance.OnContractQualificationsClosed();
        }
    }

    private void ConfigureGokeContract()
    {
        SetContractText("GOKE COLA",
            "CLIENT QUALIFICATIONS\n\n" +
            "STAGE   - Red backdrop; Goke at least 1.5 units from the wall\n" +
            "CAMERA  - Rule of Thirds; choose any grid intersection\n" +
            "LIGHT   - Key, softer opposite Fill, and Back; choose intensities\n" +
            "EDIT    - 10 seconds: INTRO 0-2s, your footage 2-8s, OUTRO 8-10s\n" +
            "OVERLAYS - Use exactly two; choose their timing and duration\n" +
            "CLIPS   - Intro/outro supplied; effects and color changes optional\n\n" +
            "UPFRONT PAYMENT: 10,500 B-COINS");

        SetQualificationSummary("STAGE: Red backdrop + depth     CAMERA: Any thirds intersection\nLIGHT: Key / Fill / Back     EDIT: 10s with intro/outro + 2 freely timed overlays");

        SetQualificationText("GOKE COLA - SELECTED CONTRACT",
            "STAGE & COMPOSITION",
            "STAGE\nRed backdrop. Place Goke at least 1.5 units away from the wall for depth. Choose its position and camera angle.\n\n" +
            "RULE OF THIRDS\nPlace the full can near any of the four grid intersections. Left or right, upper or lower: your choice. Keep it visible and leave room for graphics.",
            "LIGHTING & EDIT",
            "KEY shapes the can. FILL softens shadows from the opposite side. BACK separates it from the backdrop. Power and aim all three; keep Fill softer than Key. Choose intensities, not fixed percentages.\n\n" +
            "EDIT\n10s: supplied intro 2s + footage 6s + supplied outro 2s, joined without gaps.\n\n" +
            "Use two overlays: logo + tagline. Any timing or duration during the ad; together or separately. Choose placement and animation. Effects, music and color changes are optional.");

        if (qualificationsPanel != null)
        {
            foreach (string path in new[] { "Qualifications Book/Rule of Thirds/Description", "Qualifications Book/Three Point Lighting/Description" })
            {
                Transform body = qualificationsPanel.transform.Find(path);
                if (body == null) continue;
                var text = body.GetComponent<TextMeshProUGUI>();
                if (text == null) continue;
                text.enableAutoSizing = true;
                text.fontSizeMin = 18f;
                text.fontSizeMax = 25f;
            }
        }

        SetPreviousContractText("ARTISAN\nFLOWER VASE",
            "PREVIOUS CONTRACT\n\n" +
            "Pink backdrop\n" +
            "Centered composition\n" +
            "Single-light setup\n" +
            "10-second commercial\n" +
            "2 title-safe graphics\n" +
            "Balanced primary color grade");
    }

    private void ConfigureLevel3Contract()
    {
        SetContractText("TERRARI",
            "CLIENT QUALIFICATIONS\n\n" +
            "SET - ADD WALL; choose a dark backdrop and place one orange Terrari\n" +
            "LIGHT   - Use the Level 3 Soft Light for clean reflections\n" +
            "CAMERA  - Reveal a detail into a low front-quarter hero view\n" +
            "EDIT    - 8-12 seconds; choose your own motion and finish\n\n" +
            "UPFRONT PAYMENT: 8,500 B-COINS");

        SetQualificationSummary("STAGE: Dark backdrop + orange Terrari     CAMERA: Detail to hero reveal\nLIGHT: Soft, aimed highlights     EDIT: 8-12 seconds; creative finish");

        SetQualificationText("TERRARI - SELECTED CONTRACT",
            "AUTOMOTIVE COMPOSITION",
            "Present the vehicle as the only hero subject.\n\n" +
            "- Place exactly one Terrari car.\n" +
            "- Open with a headlight or wheel detail, then reveal the front and side.\n" +
            "- Centered and Rule of Thirds framing both work; detail shots may crop the car.\n" +
            "- Keep the camera low and avoid obstructing the vehicle.",
            "SOFT REFLECTIVE LIGHTING",
            "Use the Level 3 Soft Light to shape the vehicle.\n\n" +
            "- Light the side and front of the car.\n" +
            "- Keep highlights clean across the body.\n" +
            "- Use your Soft Light from practice; keep readable paint detail and some shadow for shape.\n" +
            "- Start near 75% output and -10 degrees tilt, then refine.\n" +
            "- Use at least 30% output and 50% diffusion; aim the beam at the car.\n\n" +
            "POST-PRODUCTION\n" +
            "Use your recorded Level 3 footage with soft lighting in an 8-12 second cut. Slow Pull Out reveals a steady hero take; separate detail and hero takes also work. Cinematic music is suggested. Music, transitions, overlays and intro/outro cards are optional. Use Contrast 1.05-1.45, Saturation 0.95-1.30, Brightness 0.85-1.15.");

        SetPreviousContractText("GOKE COLA",
            "PREVIOUS CONTRACT\n\n" +
            "Red backdrop\n" +
            "Rule of Thirds\n" +
            "3-Point Lighting\n" +
             "High-contrast commercial");
    }

    private void ConfigureLevel4Contract()
    {
        SetContractText("KAPE KULTURA",
            "CLIENT OBJECTIVE\n" +
            "Create a warm, believable everyday coffee story.\n\n" +
            "SET     - Warm brown backdrop\n" +
            "STAGE   - Exactly one Kape product and one posed actor\n" +
            "CAST    - Keep the same non-neutral pose across every clip\n" +
            "CAMERA  - At least 3 clips: Wide, Medium, and Close-Up\n" +
            "LIGHT   - Level 3 Soft Light in every selected clip\n" +
            "EDIT    - 15 seconds, 2 animated graphics, player-selected motion, transition, music, and warm grade\n\n" +
            "UPFRONT PAYMENT: 6,500 B-COINS");

        SetQualificationSummary("STAGE: Brown set + 1 product + 1 posed actor     CAMERA: Wide, Medium & Close-Up\nLIGHT: Soft Light every clip     EDIT: 15 seconds + 2 graphics");

        SetQualificationText("KAPE KULTURA - SELECTED CONTRACT",
            "COVERAGE & CONTINUITY",
            "Record at least 3 clips: one Wide, one Medium, and one Close-Up.\n\n" +
            "- Place exactly one actor and one coffee product.\n" +
            "- Keep both visible in every selected shot.\n" +
            "- Keep the actor on the same side of the product in every shot.\n" +
            "- Choose a non-neutral pose and keep that same pose in every clip.\n" +
            "- Do not change the actor-product set relationship.\n" +
            "- Arrange the three shots into a clear 15-second story.",
            "NATURAL LIGHT & WARM GRADE",
            "Create a welcoming morning-commercial look.\n\n" +
            "- Use a warm brown backdrop.\n" +
            "- Use the Soft Light in every selected clip without flattening the actor.\n" +
            "- Keep face and product detail readable.\n" +
            "- Use exactly 2 graphics and choose their entrance animation.\n" +
            "- Choose camera motion, an opening/closing transition, and music in Branding.\n" +
            "- Preview the complete 15-second story before export.\n" +
            "- Grade within Brightness 0.95-1.15, Contrast 1.05-1.30, and Saturation 1.05-1.30.");

        SetPreviousContractText("TERRARI",
            "PREVIOUS CONTRACT\n\n" +
            "Hero vehicle staging\n" +
            "Premium automotive composition\n" +
            "Soft reflective lighting\n" +
            "10-second commercial");
    }

    private void ConfigureLevel5Contract()
    {
        SetContractText("HARAYA CAMPAIGN",
            "CLIENT OBJECTIVE\n" +
            "Launch a polished Filipino lifestyle campaign.\n\n" +
            "SET     - Teal backdrop with clear visual hierarchy\n" +
            "STAGE   - Exactly one actor, one Haraya product, and one vehicle\n" +
            "CAMERA  - At least 4 shots using 3 different shot sizes\n" +
            "LIGHT   - Complete Key, Fill, and Back Light setup\n" +
            "EDIT    - 20 seconds, 3 graphics, polished color grade\n\n" +
            "UPFRONT PAYMENT: 9,500 B-COINS");

        SetQualificationSummary("STAGE: Teal set + actor + product + vehicle     CAMERA: 4 shots / 3 sizes\nLIGHT: Key, Fill & Back     EDIT: 20 seconds + 3 graphics");

        SetQualificationText("HARAYA CAMPAIGN - SELECTED CONTRACT",
            "INTEGRATED PRODUCTION",
            "Every department must support one campaign idea.\n\n" +
            "- Stage exactly one actor, one product, and one vehicle.\n" +
            "- Record at least 4 usable shots.\n" +
            "- Include Wide, Medium, and Close-Up coverage.\n" +
            "- Maintain screen direction and visual continuity.\n" +
            "- Keep the product as the main point of attention.",
            "LIGHTING & FINAL DELIVERY",
            "Deliver a technically complete 20-second commercial.\n\n" +
            "- Build distinct Key, Fill, and Back Light roles.\n" +
            "- Keep all three lighting roles readable across the coverage.\n" +
            "- Use exactly 3 readable graphics.\n" +
            "- Grade within Brightness 0.95-1.10, Contrast 1.10-1.40, and Saturation 1.00-1.25.\n" +
            "- Review the full export before submission.");

        SetPreviousContractText("KAPE KULTURA",
            "PREVIOUS CONTRACT\n\n" +
            "Warm brown set\n" +
            "Actor and coffee product\n" +
            "Wide, Medium, Close-Up continuity\n" +
            "15-second warm commercial");
    }

    private void SetContractText(string title, string description)
    {
        if (offerPanel == null) return;
        description += "\nCOMPLETION BONUS: up to " + ProductionEconomy.CompletionBonus(activeContractLevel).ToString("N0") +
            " B (S grade).\nBudget for essentials first. Rebuying sets/props costs money; deleting them gives no refund.";
        liveTitle=title;liveDescription=description;browsedContractLevel=activeContractLevel;
        RefreshFolderSelection();RefreshBrief();

        Transform titleTransform = offerPanel.transform.Find("Goke Cola Contract/Contract Details/Contract Title");
        Transform descriptionTransform = offerPanel.transform.Find("Goke Cola Contract/Contract Details/Contract Description");

        if (titleTransform != null) titleTransform.GetComponent<TextMeshProUGUI>().text = title;
        if (descriptionTransform != null) descriptionTransform.GetComponent<TextMeshProUGUI>().text = description;
    }

    private void SetQualificationText(string heading, string leftTitle, string leftDescription, string rightTitle, string rightDescription)
    {
        detailedRequirements=leftTitle+"\n"+leftDescription+"\n\n"+rightTitle+"\n"+rightDescription;
        RefreshBrief();
        if (qualificationsPanel == null) return;

        Transform book = qualificationsPanel.transform.Find("Qualifications Book");
        if (book == null) return;

        Transform headingTransform = book.Find("Heading");
        Transform leftTitleTransform = book.Find("Rule of Thirds/Title");
        Transform leftDescriptionTransform = book.Find("Rule of Thirds/Description");
        Transform rightTitleTransform = book.Find("Three Point Lighting/Title");
        Transform rightDescriptionTransform = book.Find("Three Point Lighting/Description");

        if (headingTransform != null) headingTransform.GetComponent<TextMeshProUGUI>().text = heading;
        if (leftTitleTransform != null) leftTitleTransform.GetComponent<TextMeshProUGUI>().text = leftTitle;
        if (leftDescriptionTransform != null) leftDescriptionTransform.GetComponent<TextMeshProUGUI>().text = leftDescription;
        if (rightTitleTransform != null) rightTitleTransform.GetComponent<TextMeshProUGUI>().text = rightTitle;
        if (rightDescriptionTransform != null) rightDescriptionTransform.GetComponent<TextMeshProUGUI>().text = rightDescription;
    }

    private void SetQualificationSummary(string summary)
    {
        if (qualificationsPanel == null) return;

        Transform summaryTransform = qualificationsPanel.transform.Find("Qualifications Book/Contract Summary");
        if (summaryTransform != null) summaryTransform.GetComponent<TextMeshProUGUI>().text = summary;
    }

    private void SetPreviousContractText(string title, string description)
    {
        if (offerPanel == null) return;

        Transform titleTransform = offerPanel.transform.Find("Completed Contract/Completed Details/Contract Title");
        Transform descriptionTransform = offerPanel.transform.Find("Completed Contract/Completed Details/Contract Description");

        if (titleTransform != null) titleTransform.GetComponent<TextMeshProUGUI>().text = title;
        if (descriptionTransform != null) descriptionTransform.GetComponent<TextMeshProUGUI>().text = description;
    }

    private void LockPlayer()
    {
        if (playerController != null) return;

        playerController = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (playerController == null) return;

        playerCouldMove = playerController.canMove;
        playerCouldLook = playerController.canLook;
        playerController.canMove = false;
        playerController.canLook = false;
    }

    private void UnlockPlayer()
    {
        if (playerController == null) return;

        playerController.canMove = playerCouldMove;
        playerController.canLook = playerCouldLook;
        playerController = null;
    }

    private void BuildRuntimeUI()
    {
        contractCanvas = new GameObject("Contract UI (Runtime)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        contractCanvas.transform.SetParent(transform, false);

        Canvas canvas = contractCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;

        CanvasScaler canvasScaler = contractCanvas.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;

        BuildOfferPanel();
        BuildQualificationsPanel();
    }

    private static readonly string[] ContractNames={"ARTISAN FLOWER VASE","GOKE COLA","TERRARI","KAPE KULTURA","HARAYA"};

    private GameObject ArtPanel(string name,Transform parent,string art,Vector2 position,Vector2 size)
    {
        var panel=CreatePanel(name,parent,Color.white);ExportUIArt.Apply(panel.GetComponent<Image>(),art);
        SetRect(panel.GetComponent<RectTransform>(),Vector2.one*.5f,Vector2.one*.5f,position,size);return panel;
    }

    private TextMeshProUGUI Label(Transform parent,string name,string text,Vector2 position,Vector2 size,float font,bool outlined=false)
    {
        var label=CreateText(name,parent,text,font,TextAlignmentOptions.Center);
        SetRect(label.rectTransform,Vector2.one*.5f,Vector2.one*.5f,position,size);
        label.color=outlined?Color.white:Color.black;label.fontStyle=FontStyles.Bold;
        label.enableAutoSizing=true;label.fontSizeMin=18;label.fontSizeMax=font;label.raycastTarget=false;
        if(outlined)ExportUIArt.OutlineText(label);
        return label;
    }

    private Button ArtButton(Transform parent,string name,string label,string art,Vector2 position,Vector2 size,Action action)
    {
        var button=CreateButton(name,parent,label,Color.white);ExportUIArt.Apply(button.GetComponent<Image>(),art);
        SetRect(button.GetComponent<RectTransform>(),Vector2.one*.5f,Vector2.one*.5f,position,size);
        var text=button.GetComponentInChildren<TextMeshProUGUI>();text.fontSize=36;ExportUIArt.OutlineText(text);
        if(action!=null)button.onClick.AddListener(()=>action());return button;
    }

    private void BuildOfferPanel()
    {
        offerPanel=CreatePanel("Contract Offer",contractCanvas.transform,new Color(0,0,0,.7f));
        SetStretchRect(offerPanel.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        folderTitles=new TextMeshProUGUI[3];
        for(int i=0;i<3;i++)
        {
            bool center=i==1;float width=center?572:458,height=center?691:553;
            var card=ArtPanel("Contract folder "+i,offerPanel.transform,"psdClosedFolder",new Vector2((i-1)*610,27),new Vector2(width,height));
            folderTitles[i]=Label(card.transform,"Contract title","",new Vector2(0,height*.282f),new Vector2(width*.64f,height*.11f),center?30:25,true);
        }
        acceptButton=ArtButton(offerPanel.transform,"Select contract","SELECT","blueButton",new Vector2(0,-425),new Vector2(236,96),null);
        ArtButton(offerPanel.transform,"Previous contract","","left",new Vector2(-192,-425),new Vector2(63,93),()=>BrowseContract(-1));
        ArtButton(offerPanel.transform,"Next contract","","right",new Vector2(192,-425),new Vector2(63,93),()=>BrowseContract(1));
        selectionStatus=Label(offerPanel.transform,"Contract status","",new Vector2(0,-320),new Vector2(490,50),22);
        selectionStatus.color=Color.white;
        RefreshFolderSelection();
    }

    private void BrowseContract(int direction)
    {
        browsedContractLevel=Mathf.Clamp(browsedContractLevel+direction,1,ContractNames.Length);RefreshFolderSelection();
    }

    private void RefreshFolderSelection()
    {
        if(folderTitles==null)return;
        for(int i=0;i<3;i++)
        {
            int level=(browsedContractLevel+i-2+ContractNames.Length)%ContractNames.Length+1;
            folderTitles[i].text=level==activeContractLevel?liveTitle:ContractNames[level-1];
        }
        acceptButton.interactable=browsedContractLevel==activeContractLevel;
        selectionStatus.text=browsedContractLevel<activeContractLevel?"COMPLETED":browsedContractLevel>activeContractLevel?"LOCKED — COMPLETE THE CURRENT CONTRACT":"";
    }

    private void BuildQualificationsPanel()
    {
        qualificationsPanel=CreatePanel("Contract Qualifications",contractCanvas.transform,new Color(0,0,0,.7f));
        SetStretchRect(qualificationsPanel.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        var book=ArtPanel("Qualifications Book",qualificationsPanel.transform,"psdOpenFolder",new Vector2(20,0),new Vector2(1346,860));
        ArtPanel("Project title tape",book.transform,"tape",new Vector2(-365,281),new Vector2(429,123));
        briefTitle=Label(book.transform,"Project title",liveTitle,new Vector2(-365,281),new Vector2(355,70),34,true);
        ArtPanel("Reference photo frame",book.transform,"psdPhotoFrame",new Vector2(-382,-70),new Vector2(355,434));
        var photo=ArtPanel("Product photo",book.transform,"psdVasePhoto",new Vector2(-380,-96),new Vector2(255,300));
        photo.GetComponent<Image>().preserveAspect=true;
        var photoLabel=Label(book.transform,"Product photo label","",new Vector2(-380,-96),new Vector2(230,200),28);
        photoLabel.color=Color.white;
        ArtButton(qualificationsPanel.transform,"Close brief","","close",new Vector2(720,448),new Vector2(85,85),CloseIllustratedBrief);
        var viewport=CreatePanel("Brief viewport",book.transform,Color.clear);
        SetRect(viewport.GetComponent<RectTransform>(),Vector2.one*.5f,Vector2.one*.5f,new Vector2(294,8),new Vector2(530,740));
        viewport.AddComponent<RectMask2D>();
        briefScroll=viewport.AddComponent<ScrollRect>();briefScroll.horizontal=false;briefScroll.movementType=ScrollRect.MovementType.Clamped;
        briefScroll.viewport=viewport.GetComponent<RectTransform>();briefScroll.scrollSensitivity=35;
        briefBody=CreateText("Live contract requirements",viewport.transform,"",21,TextAlignmentOptions.TopLeft);
        briefBody.color=Color.black;briefBody.fontStyle=FontStyles.Bold;briefBody.raycastTarget=false;
        var content=briefBody.rectTransform;content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
        var fitter=briefBody.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        briefScroll.content=content;
        var track=CreatePanel("Scroll track",book.transform,new Color(.3f,.24f,.1f,.5f));
        SetRect(track.GetComponent<RectTransform>(),Vector2.one*.5f,Vector2.one*.5f,new Vector2(584,-116),new Vector2(14,480));
        var handle=CreatePanel("Scroll handle",track.transform,new Color(.15f,.13f,.08f));
        SetStretchRect(handle.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        var scrollbar=track.AddComponent<Scrollbar>();scrollbar.direction=Scrollbar.Direction.BottomToTop;scrollbar.handleRect=handle.GetComponent<RectTransform>();scrollbar.targetGraphic=handle.GetComponent<Image>();
        briefScroll.verticalScrollbar=scrollbar;
        RefreshBrief();qualificationsPanel.SetActive(false);
    }

    private void RefreshBrief()
    {
        if(briefBody==null)return;
        briefTitle.text=liveTitle;
        briefBody.text="<align=center>PROJECT TITLE:\n<size=32>"+liveTitle+"</size>\nCOMMERCIAL PRODUCTION BRIEF</align>\n\n"+liveDescription+"\n\nPRODUCTION REQUIREMENTS\n\n"+detailedRequirements;
        var photo=qualificationsPanel.transform.Find("Qualifications Book/Product photo").GetComponent<Image>();
        // Use the supplied illustration only for the vase; other contracts display their own product art.
        string art=activeContractLevel==1?"psdVasePhoto":activeContractLevel==2?"gokeProduct":activeContractLevel==3?"terrariMark":activeContractLevel==4?"coffeeProduct":"harayaProduct";
        var sprite=ExportUIArt.Get(art);photo.enabled=sprite!=null;if(sprite!=null)photo.sprite=sprite;
        qualificationsPanel.transform.Find("Qualifications Book/Product photo label").GetComponent<TextMeshProUGUI>().text=sprite==null?liveTitle:"";
        Canvas.ForceUpdateCanvases();briefScroll.verticalNormalizedPosition=1;
    }

    private void CloseIllustratedBrief()
    {
        if(acceptanceBriefPending){CompleteContractAcceptance();return;}
        if(isQualificationsOpen)ToggleQualifications();
    }

    private void BuildLegacyOfferPanel()
    {
        offerPanel = CreatePanel("Contract Offer", contractCanvas.transform, backgroundColor);
        SetStretchRect(offerPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        TextMeshProUGUI headingText = CreateText("Heading", offerPanel.transform, "CONTRACT BOARD", 48, TextAlignmentOptions.Center);
        SetRect(headingText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -75f), new Vector2(900f, 70f));
        headingText.fontStyle = FontStyles.Bold;

        CreateCompletedContractCard(offerPanel.transform, new Vector2(-610f, 30f));
        CreateLockedCard("Locked Contract Right", offerPanel.transform, new Vector2(610f, 30f));

        GameObject contractCard = CreatePanel("Goke Cola Contract", offerPanel.transform, cardColor);
        SetRect(contractCard.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 35f), new Vector2(720f, 720f));

        GameObject contractInner = CreatePanel("Contract Details", contractCard.transform, cardInnerColor);
        SetStretchRect(contractInner.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(22f, 22f), new Vector2(-22f, -22f));

        TextMeshProUGUI contractTitle = CreateText("Contract Title", contractInner.transform, "GOKE COLA", 44, TextAlignmentOptions.Center);
        SetRect(contractTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -65f), new Vector2(620f, 70f));
        contractTitle.fontStyle = FontStyles.Bold;
        contractTitle.color = new Color(.2f,.12f,.07f);

        TextMeshProUGUI contractDescription = CreateText("Contract Description", contractInner.transform,
            "CLIENT QUALIFICATIONS\n\n" +
            "STAGE   • RED backdrop and Cola away from the wall\n" +
            "CAMERA  • Rule of Thirds composition\n" +
            "LIGHT   • 3-Point Lighting\n" +
            "EDIT    • 10 seconds, two title-safe graphics, balanced color\n\n" +
            "UPFRONT PAYMENT: 10,500 B-COINS",
            25, TextAlignmentOptions.TopLeft);
        SetStretchRect(contractDescription.rectTransform, Vector2.zero, Vector2.one, new Vector2(50f, 170f), new Vector2(-50f, -135f));

        acceptButton = CreateButton("Accept Button", contractInner.transform, "ACCEPT CONTRACT", blueColor);
        SetRect(acceptButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-155f, 90f), new Vector2(270f, 62f));

        declineButton = CreateButton("Decline Button", contractInner.transform, "DECLINE", redColor);
        SetRect(declineButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(155f, 90f), new Vector2(270f, 62f));

        declineMessageText = CreateText("Decline Message", contractInner.transform, "", 18, TextAlignmentOptions.Center);
        SetRect(declineMessageText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 35f), new Vector2(620f, 45f));
        declineMessageText.color = new Color(1f, 0.55f, 0.4f);
    }

    private void BuildLegacyQualificationsPanel()
    {
        qualificationsPanel = CreatePanel("Contract Qualifications", contractCanvas.transform, backgroundColor);
        SetStretchRect(qualificationsPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject mainPanel = CreatePanel("Qualifications Book", qualificationsPanel.transform, new Color(0.07f, 0.09f, 0.12f, 1f));
        SetRect(mainPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1450f, 850f));

        TextMeshProUGUI headingText = CreateText("Heading", mainPanel.transform, "GOKE COLA - SELECTED CONTRACT", 42, TextAlignmentOptions.Center);
        SetRect(headingText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(1300f, 60f));
        headingText.fontStyle = FontStyles.Bold;

        TextMeshProUGUI contractSummary = CreateText("Contract Summary", mainPanel.transform,
            "STAGE: Red backdrop + depth     CAMERA: Any thirds intersection\nLIGHT: Key / Fill / Back     EDIT: 10s with intro/outro + 2 freely timed overlays",
            21, TextAlignmentOptions.Center);
        SetRect(contractSummary.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -115f), new Vector2(1320f, 68f));
        contractSummary.color = new Color(1f, 0.82f, 0.35f);
        contractSummary.fontStyle = FontStyles.Bold;

        GameObject thirdsCard = CreatePanel("Rule of Thirds", mainPanel.transform, new Color(0.11f, 0.16f, 0.21f, 1f));
        SetRect(thirdsCard.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-335f, -25f), new Vector2(620f, 500f));

        TextMeshProUGUI thirdsTitle = CreateText("Title", thirdsCard.transform, "RULE OF THIRDS", 34, TextAlignmentOptions.Center);
        SetRect(thirdsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(550f, 60f));
        thirdsTitle.fontStyle = FontStyles.Bold;
        thirdsTitle.color = new Color(0.35f, 0.8f, 1f);

        TextMeshProUGUI thirdsDescription = CreateText("Description", thirdsCard.transform,
            "Divide the frame into a 3 × 3 grid.\n\n" +
            "• Place the Cola near a grid intersection.\n" +
            "• Choose any intersection: upper or lower, left or right.\n" +
            "• Leave intentional negative space.\n" +
            "• Do not use the tutorial's default center framing.",
            25, TextAlignmentOptions.TopLeft);
        SetStretchRect(thirdsDescription.rectTransform, Vector2.zero, Vector2.one, new Vector2(45f, 45f), new Vector2(-45f, -115f));

        GameObject lightingCard = CreatePanel("Three Point Lighting", mainPanel.transform, new Color(0.11f, 0.16f, 0.21f, 1f));
        SetRect(lightingCard.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(335f, -25f), new Vector2(620f, 500f));

        TextMeshProUGUI lightingTitle = CreateText("Title", lightingCard.transform, "3-POINT LIGHTING", 34, TextAlignmentOptions.Center);
        SetRect(lightingTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(550f, 60f));
        lightingTitle.fontStyle = FontStyles.Bold;
        lightingTitle.color = new Color(1f, 0.78f, 0.2f);

        TextMeshProUGUI lightingDescription = CreateText("Description", lightingCard.transform,
            "Build the shot using three lighting roles.\n\n" +
            "• KEY: strongest light, about 45° from the subject.\n" +
            "• FILL: softer opposite light controlling shadows.\n" +
            "• BACK: light behind the subject for separation.\n" +
            "• Keep the key dominant so the image retains depth.",
            25, TextAlignmentOptions.TopLeft);
        SetStretchRect(lightingDescription.rectTransform, Vector2.zero, Vector2.one, new Vector2(45f, 45f), new Vector2(-45f, -115f));

        TextMeshProUGUI closeHint = CreateText("Close Hint", mainPanel.transform, "Press [TAB] to close the selected contract", 24, TextAlignmentOptions.Center);
        SetRect(closeHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 45f), new Vector2(900f, 45f));

        qualificationsPanel.SetActive(false);
    }

    private void CreateLockedCard(string objectName, Transform parent, Vector2 position)
    {
        GameObject lockedCard = CreatePanel(objectName, parent, new Color(0.2f, 0.15f, 0.1f, 0.9f));
        SetRect(lockedCard.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(360f, 500f));

        TextMeshProUGUI lockedText = CreateText("Locked Text", lockedCard.transform, "LOCKED", 30, TextAlignmentOptions.Center);
        SetStretchRect(lockedText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        lockedText.fontStyle = FontStyles.Bold;
        lockedText.color = new Color(0.55f, 0.5f, 0.45f);
    }

    private void CreateCompletedContractCard(Transform parent, Vector2 position)
    {
        GameObject completedCard = CreatePanel("Completed Contract", parent, new Color(0.24f, 0.18f, 0.1f, 1f));
        SetRect(completedCard.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(360f, 500f));

        GameObject completedInner = CreatePanel("Completed Details", completedCard.transform, new Color(0.35f, 0.25f, 0.12f, 1f));
        SetStretchRect(completedInner.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(14f, 14f), new Vector2(-14f, -14f));

        TextMeshProUGUI completedTitle = CreateText("Contract Title", completedInner.transform, "ARTISAN\nFLOWER VASE", 28, TextAlignmentOptions.Center);
        SetRect(completedTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -75f), new Vector2(300f, 100f));
        completedTitle.fontStyle = FontStyles.Bold;
        completedTitle.color = new Color(1f, 0.84f, 0.45f);

        TextMeshProUGUI completedDescription = CreateText("Contract Description", completedInner.transform,
            "PREVIOUS CONTRACT\n\n" +
            "Pink backdrop\n" +
            "Centered composition\n" +
            "Single-light setup\n" +
            "10-second commercial",
            20, TextAlignmentOptions.Center);
        SetRect(completedDescription.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(300f, 230f));

        TextMeshProUGUI completedText = CreateText("Completed Text", completedInner.transform, "COMPLETED", 24, TextAlignmentOptions.Center);
        SetRect(completedText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(280f, 52f));
        completedText.fontStyle = FontStyles.Bold;
        completedText.color = new Color(0.35f, 1f, 0.45f);
    }

    private GameObject CreatePanel(string objectName, Transform parent, Color color)
    {
        GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        panelObject.GetComponent<Image>().color = color;
        if(objectName=="Goke Cola Contract"||objectName=="Qualifications Book"||objectName=="Completed Contract")ExportUIArt.Apply(panelObject.GetComponent<Image>(),"folder");
        if(objectName=="Contract Details"||objectName=="Completed Details")ExportUIArt.Apply(panelObject.GetComponent<Image>(),"paper");
        return panelObject;
    }

    private Button CreateButton(string objectName, Transform parent, string label, Color color)
    {
        GameObject buttonObject = CreatePanel(objectName, parent, color);
        Button button = buttonObject.AddComponent<Button>();

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        ExportUIArt.Apply(buttonObject.GetComponent<Image>(),objectName=="Decline Button"?"redButton":"blueButton");
        colors.normalColor=Color.white;colors.highlightedColor=new Color(.9f,.95f,1);colors.pressedColor=Color.gray;button.colors=colors;

        TextMeshProUGUI buttonText = CreateText("Text", buttonObject.transform, label, 22, TextAlignmentOptions.Center);
        SetStretchRect(buttonText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        buttonText.fontStyle = FontStyles.Bold;

        return button;
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI textComponent = textObject.GetComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = Color.white;
        if(parent.name=="Contract Details"||parent.name=="Completed Details"||parent.name=="Qualifications Book")textComponent.color=new Color(.15f,.12f,.09f);
        textComponent.enableWordWrapping = true;

        return textComponent;
    }

    private void SetRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (rectTransform == null) return;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;
    }

    private void SetStretchRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rectTransform == null) return;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    private void OnDestroy()
    {
        if (acceptButton != null) acceptButton.onClick.RemoveListener(AcceptContract);
        if (declineButton != null) declineButton.onClick.RemoveListener(DeclineContract);
        if (Instance == this) Instance = null;
    }
}
