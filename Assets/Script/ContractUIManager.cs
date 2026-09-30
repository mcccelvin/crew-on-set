using System;
using System.Collections;
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
    private bool editorReferenceMode;

    public void ConfigureEditorReference()
    {
        editorReferenceMode = true;
        activeContractLevel = CampaignProgression.GetCurrentLevel();
        qualificationsUnlocked = true;
        if (activeContractLevel == 2) ConfigureGokeContract();
        else if (activeContractLevel == 3) ConfigureLevel3Contract();
        else if (activeContractLevel == 4) ConfigureLevel4Contract();
        else if (activeContractLevel == 5) ConfigureLevel5Contract();
        else
        {
            SetContractText("ARTISAN FLOWER VASE", "Create a 10-second product commercial. Use the pink backdrop, center the flower vase on its support, and keep the subject clearly lit.");
            SetQualificationText("CONTRACT", "EDITING", "Trim the footage to 10 seconds and start at 0 seconds without gaps.", "BRANDING & COLOR", "Show Logo 1 from 0–5 seconds and Logo 2 from 5–10 seconds. Keep the product readable and review the exported commercial.");
        }
    }
    [SerializeField] private Button briefAcceptButton;
    private string liveTitle="GOKE COLA", liveDescription="", detailedRequirements="";
    [SerializeField] private TextMeshProUGUI[] folderTitles;
    [SerializeField] private TextMeshProUGUI selectionStatus, briefTitle, briefBody;
    [SerializeField] private ScrollRect briefScroll;
    private Coroutine folderAnimation;
    private RectTransform[] animatedFolders;
    private Vector2[] folderPositions;
    private Vector3[] folderScales;
    private int[] folderOrder;

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
        HideStaticSelection();
        BindLayoutButtons();
        RefreshFolderSelection();

        if (acceptButton != null) acceptButton.onClick.AddListener(AcceptContract);
        if (declineButton != null) declineButton.onClick.AddListener(DeclineContract);

        if (contractCanvas != null) contractCanvas.SetActive(false);
    }

    private bool layoutButtonsBound;
    private void HideStaticSelection()
    {
        if (contractCanvas == null || folderTitles == null || folderTitles.Length != 3) return;
        // The authored Selection image contains the entire old board and SELECT button.
        // Its live replacements are in Contract Offer; keep the source artwork intact.
        Transform staticSelection = contractCanvas.transform.Find("Selection");
        if (staticSelection != null && staticSelection.gameObject != offerPanel)
            staticSelection.gameObject.SetActive(false);
    }

    private void BindLayoutButtons()
    {
        if (layoutButtonsBound || offerPanel == null || briefBody == null) return;
        layoutButtonsBound = true;
        offerPanel.transform.Find("Previous contract").GetComponent<Button>().onClick.AddListener(() => BrowseContract(-1));
        offerPanel.transform.Find("Next contract").GetComponent<Button>().onClick.AddListener(() => BrowseContract(1));
        var closeBrief = qualificationsPanel.transform.Find("Close brief");
        if (closeBrief != null) closeBrief.gameObject.SetActive(false);
        briefAcceptButton.onClick.AddListener(() => { if (acceptanceBriefPending) CompleteContractAcceptance(); });
    }
#if UNITY_EDITOR
    public void BakeHierarchyUI()
    {
        if (offerPanel == null)
        {
            if (contractCanvas == null) BuildRuntimeUI();
            else { BuildOfferPanel(); BuildQualificationsPanel(); }
        }
        contractCanvas.name = "Contract";
        contractCanvas.SetActive(false);
    }
#endif

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
        ResetFolderAnimation();
        HideStaticSelection();
        acceptContractAction = onAccepted;
        acceptanceBriefPending=false;
        browsedContractLevel=activeContractLevel;
        RefreshFolderSelection();
        var tutorial=FindObjectOfType<TutorialManager>();
        if(tutorial!=null&&acceptButton!=null)tutorial.acceptContractButtonRect=acceptButton.GetComponent<RectTransform>();

        if (declineMessageText != null) declineMessageText.text = "";
        if (offerPanel != null) offerPanel.SetActive(true);
        if (qualificationsPanel != null) qualificationsPanel.SetActive(false);
        UITransition.Show(contractCanvas);

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
        // A different level's dormant tutorial must not block the current brief.
        // Closing must remain possible even if opening the brief advances a lesson.
        if (isQualificationsOpen) return true;
        if (editorReferenceMode) return true;
        if (!qualificationsUnlocked) return false;
        if (offerPanel != null && offerPanel.activeInHierarchy) return false;
        if (AlmanacManager.Instance != null && AlmanacManager.Instance.IsOpen()) return false;
        int currentLevel = CampaignProgression.GetCurrentLevel();
        if (currentLevel >= 4 && CampaignLevelManager.Instance != null && CampaignLevelManager.Instance.isActiveAndEnabled && !CampaignLevelManager.Instance.CanOpenContractQualifications()) return false;
        if (currentLevel == 3 && Level3Manager.Instance != null && Level3Manager.Instance.isActiveAndEnabled && !Level3Manager.Instance.CanOpenContractQualifications()) return false;
        if (currentLevel == 2 && GokeLevelManager.Instance != null && GokeLevelManager.Instance.isActiveAndEnabled && !GokeLevelManager.Instance.CanOpenContractQualifications()) return false;
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
        if (folderAnimation != null) return;
        if(briefBody!=null)
        {
            if(browsedContractLevel!=activeContractLevel||acceptanceBriefPending)return;
            acceptanceBriefPending=true;
            offerPanel.SetActive(false);UITransition.Show(qualificationsPanel);
            RefreshBrief();
            return;
        }
        CompleteContractAcceptance();
    }

    public void ShowFlowerContract(Action onAccepted)
    {
        activeContractLevel=1;isLevel3Contract=false;
        SetContractText("ARTISAN FLOWER VASE","CLIENT: FLORA & FORM HOME\n\nCLIENT OBJECTIVE\nCreate a clear, inviting product commercial.\n\nSTAGE — Pink backdrop (RGB 255, 140, 175) and one flower vase.\nCAMERA — Frame the full product in the center.\nLIGHT — Aim one panel light to show the flowers clearly.\nEDIT — A 10-second commercial.\nOVERLAYS — Eccentric Centerpiece first, then Flora & Form Home.\nCOLOR — Keep the product readable; avoid excessive brightness or darkness.\n\nSTARTING BUDGET: "+ProductionEconomy.StartingBudget.ToString("N0")+" B-COINS");
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

        UITransition.SetVisible(contractCanvas, isQualificationsOpen);
        if (offerPanel != null) offerPanel.SetActive(false);
        if (qualificationsPanel != null) qualificationsPanel.SetActive(isQualificationsOpen);

        if (isQualificationsOpen)
        {
            RefreshBrief();
            if (editorReferenceMode)
            {
                FindObjectOfType<CommercialCompiler>()?.editorPlayer?.StopTape();
                if (briefAcceptButton != null) briefAcceptButton.gameObject.SetActive(false);
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                return;
            }
            LockPlayer();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (CampaignLevelManager.Instance != null) CampaignLevelManager.Instance.OnContractQualificationsOpened();
            if (GokeLevelManager.Instance != null) GokeLevelManager.Instance.OnContractQualificationsOpened();
            if (Level3Manager.Instance != null) Level3Manager.Instance.OnContractQualificationsOpened();
        }
        else
        {
            if (editorReferenceMode) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; return; }
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
            "STAGE   - Red backdrop; place Goke wherever you choose\n" +
            "CAMERA  - Rule of Thirds; choose any grid intersection\n" +
            "LIGHT   - Keep the subject readable with your existing light\n" +
            "EDIT    - 12 seconds: INTRO 0-2s, your footage 2-10s, OUTRO 10-12s\n" +
            "OVERLAYS - Use exactly two; choose their timing and duration\n" +
            "CLIPS   - Intro/outro supplied; effects and color changes optional\n\n" +
            "UPFRONT PAYMENT: 10,500 B-COINS");

        SetQualificationSummary("STAGE: Red backdrop + freely placed Goke     CAMERA: Any thirds intersection\nLIGHT: Readable subject     EDIT: 12s with intro/outro + 2 freely timed overlays");

        SetQualificationText("GOKE COLA - SELECTED CONTRACT",
            "STAGE & COMPOSITION",
            "STAGE\nRed backdrop. Place Goke wherever you choose; no minimum wall distance. Choose its position and camera angle.\n\n" +
            "RULE OF THIRDS\nPlace the full can near any of the four grid intersections. Left or right, upper or lower: your choice. Keep it visible and leave room for graphics.",
            "LIGHTING & EDIT",
            "Use your existing light to illuminate the subject. Leave open space beside it for your branding. Three-point lighting is taught in Level 3.\n\n" +
            "EDIT\n12s: supplied intro 2s + footage 8s + supplied outro 2s, joined without gaps.\n\n" +
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
            "Pink backdrop (RGB 255, 140, 175)\n" +
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
            "LIGHT   - Use the Better Lights for clean reflections\n" +
            "CAMERA  - Three recordings on three SD cards: back, side, overall view\n" +
            "EDIT    - 2s Terrari intro + three different recordings + 2s outro; 25 seconds\n\n" +
            "UPFRONT PAYMENT: 8,500 B-COINS");

        SetQualificationSummary("STAGE: Dark backdrop + orange Terrari     CAMERA: 3 takes / 3 SD cards: back, side, overall\nLIGHT: Warm Soft Light, aimed highlights     EDIT: Intro + 3 takes + outro; 25 seconds");

        SetQualificationText("TERRARI - SELECTED CONTRACT",
            "AUTOMOTIVE COMPOSITION",
            "Present the vehicle as the only hero subject.\n\n" +
            "- Place exactly one Terrari car.\n" +
            "- Include a back view, a side view and an overall view of the full car.\n" +
            "- Centered and Rule of Thirds framing both work from any side; keep the overall view uncropped.\n" +
            "- Keep the camera low and avoid obstructing the vehicle.",
            "SOFT REFLECTIVE LIGHTING",
            "Use the Better Lights to shape the vehicle.\n\n" +
            "- Aim the Soft Light at the visible body for each camera angle.\n" +
            "- Keep highlights clean across the body.\n" +
            "- Use your Soft Light from practice; keep readable paint detail and some shadow for shape.\n" +
            "- Start near 75% output and -10 degrees tilt, then refine.\n" +
            "- Use at least 30% output and 50% diffusion; aim the beam at the car.\n\n" +
            "POST-PRODUCTION\n" +
            "Use three separate Level 3 recordings with the warm Soft Light, one per SD card (450 B-Coins for three blank cards). Record the back, side and overall views for about 7 seconds each. Hold Ctrl with WASD and the mouse for smooth camera movement. Ingest all three cards. In the editor, join the supplied 2-second TERRARI INTRO, the three different recordings, and the supplied 2-second TERRARI OUTRO from 0s without gaps or overlaps. The finished commercial must be 25 seconds. Splitting one recording does not count. Put overlays inside title safe without covering the car; upper-left works well. Use Contrast 1.05-1.45, Saturation 0.95-1.30, Brightness 0.85-1.15.");

        SetPreviousContractText("GOKE COLA",
            "PREVIOUS CONTRACT\n\n" +
            "Red backdrop\n" +
            "Rule of Thirds\n" +
            "Readable subject lighting\n" +
             "High-contrast commercial");
    }

    private void ConfigureLevel4Contract()
    {
        SetContractText("KAPE KULTURA",
            "CLIENT OBJECTIVE\nIntroduce the coffee product, then show it being enjoyed.\n\n" +
            "SET - Coffee-shop interior with coffee in the set\n" +
            "OVERVIEW - Show the coffee cup and its packaging together\n" +
            "SCENE - An actor using coffee in the coffee shop\n" +
            "RECORD - Separate overview and coffee-use shots; additional shots are welcome\n" +
            "EDIT - 30–45 seconds, continuous cut, no overlays\n\n" +
            "UPFRONT PAYMENT: 6,500 B-COINS");
        SetQualificationSummary("STAGE: Coffee shop + actor + coffee + packaging\nEDIT: Product overview + coffee-use scene, 30–45 seconds, NO OVERLAYS");
        SetQualificationText("KAPE KULTURA - SELECTED CONTRACT",
            "PRODUCT AND COFFEE-SHOP SCENE",
            "Record an overview with the coffee cup and Kape packaging visible together. Then film an actor using coffee in a coffee-shop interior.\n\nGive the actor the coffee cup and cue Action, or cue Using Machine with coffee visible. Hold each shot steady; record enough material for a 30–45-second edit.",
            "EDITING AND DELIVERY",
            "Start with the product overview, then the coffee-shop scene. Join clips from 0s without gaps or overlaps. Keep at least 2 seconds of each required shot. Additional shots are welcome.\n\nDeliver 30–45 seconds with NO overlays. Click a timeline clip to color-grade only that clip. Press B to split the selected clip at the red playhead; double-click to trim. Preview the full edit before exporting. Color settings are creative choices.");
        SetPreviousContractText("TERRARI", "PREVIOUS CONTRACT\n\nVehicle angles and reflective lighting\n25-second automotive commercial");
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
            "Actor-led coffee story\n" +
            "Greeting, coffee moment, seated break\n" +
            "Elliptical editing\n" +
            "30–45-second product and coffee-shop commercial, no overlays");
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
    private static readonly string[] ContractFolderArt={"contractArtisan","contractGoke","contractTerrari","contractKape","contractHaraya"};

    private void SetFolderArtwork(int index, int level)
    {
        var title = folderTitles[index];
        if (title == null) return;
        title.text = level == activeContractLevel ? liveTitle : ContractNames[level - 1];
        var image = title.transform.parent.GetComponent<Image>();
        var sprite = ExportUIArt.Get(ContractFolderArt[level - 1]);
        if (image == null || sprite == null) { title.enabled = true; return; }
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        // Fill the original carousel card dimensions, including its full width.
        image.preserveAspect = false;
        image.color = Color.white;
        // The redesigned covers already include their contract titles.
        title.enabled = false;
    }

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
        ArtButton(offerPanel.transform,"Previous contract","","left",new Vector2(-192,-425),new Vector2(63,93),null);
        ArtButton(offerPanel.transform,"Next contract","","right",new Vector2(192,-425),new Vector2(63,93),null);
        selectionStatus=Label(offerPanel.transform,"Contract status","",new Vector2(0,-320),new Vector2(490,50),22);
        selectionStatus.color=Color.white;
        RefreshFolderSelection();
    }

    private void BrowseContract(int direction)
    {
        if (PauseManager.isPaused || folderAnimation != null) return;
        int next = Mathf.Clamp(browsedContractLevel + direction, 1, ContractNames.Length);
        if (next == browsedContractLevel) return;
        if (folderTitles == null || folderTitles.Length != 3) return;
        animatedFolders = new RectTransform[3];
        folderPositions = new Vector2[3];
        folderScales = new Vector3[3];
        folderOrder = new int[3];
        for (int i = 0; i < 3; i++)
        {
            animatedFolders[i] = folderTitles[i].transform.parent as RectTransform;
            folderPositions[i] = animatedFolders[i].anchoredPosition;
            folderScales[i] = animatedFolders[i].localScale;
            folderOrder[i] = animatedFolders[i].GetSiblingIndex();
        }
        folderAnimation = StartCoroutine(AnimateFolders(direction, next));
    }

    private IEnumerator AnimateFolders(int direction, int next)
    {
        acceptButton.interactable = false;
        int recycled = direction > 0 ? 0 : 2;
        int incoming = direction > 0 ? 2 : 0;
        float elapsed = 0f;
        const float duration = .7f;
        while (elapsed < duration)
        {
            if (!PauseManager.isPaused) elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = t * t * t * (t * (t * 6f - 15f) + 10f);
            animatedFolders[recycled].SetAsFirstSibling();
            animatedFolders[t < .5f ? 1 : incoming].SetAsLastSibling();
            for (int i = 0; i < 3; i++)
            {
                int target = (i - direction + 3) % 3;
                var card = animatedFolders[i];
                card.anchoredPosition = Vector2.Lerp(folderPositions[i], folderPositions[target], ease);
                float ratio = animatedFolders[target].rect.width / Mathf.Max(1f, card.rect.width);
                Vector3 endScale = folderScales[target] * ratio;
                card.localScale = Vector3.Lerp(folderScales[i], endScale, ease);
                if (i == recycled)
                {
                    // The returning folder travels behind the two foreground folders.
                    float depth = Mathf.Sin(t * Mathf.PI);
                    card.localScale *= 1f - .24f * depth;
                    card.anchoredPosition += Vector2.down * (35f * depth);
                    if (t >= .5f)
                    {
                        int level = (next + target - 2 + ContractNames.Length) % ContractNames.Length + 1;
                        SetFolderArtwork(i, level);
                    }
                }
            }
            yield return null;
        }
        browsedContractLevel = next;
        folderAnimation = null;
        RestoreFolderLayout();
        RefreshFolderSelection();
    }

    private void RestoreFolderLayout()
    {
        if (animatedFolders == null) return;
        for (int i = 0; i < animatedFolders.Length; i++)
        {
            if (animatedFolders[i] == null) continue;
            animatedFolders[i].anchoredPosition = folderPositions[i];
            animatedFolders[i].localScale = folderScales[i];
            animatedFolders[i].SetSiblingIndex(folderOrder[i]);
        }
        animatedFolders = null;
    }

    private void ResetFolderAnimation()
    {
        if (folderAnimation != null) StopCoroutine(folderAnimation);
        folderAnimation = null;
        RestoreFolderLayout();
    }

    private void OnDisable()
    {
        ResetFolderAnimation();
    }

    private void RefreshFolderSelection()
    {
        if(folderTitles==null || folderTitles.Length!=3)return;
        for(int i=0;i<3;i++)
        {
            int level=(browsedContractLevel+i-2+ContractNames.Length)%ContractNames.Length+1;
            SetFolderArtwork(i, level);
        }
        if(acceptButton!=null)acceptButton.interactable=browsedContractLevel==activeContractLevel;
        if(selectionStatus!=null)selectionStatus.text=browsedContractLevel<activeContractLevel?"COMPLETED":browsedContractLevel>activeContractLevel?"LOCKED — COMPLETE THE CURRENT CONTRACT":"";
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
        // Briefs use ACCEPT or the existing TAB shortcut, without a red X.
        briefAcceptButton=ArtButton(qualificationsPanel.transform,"Accept contract","ACCEPT","blueButton",new Vector2(0,-475),new Vector2(300,70),null);
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
        var closeBrief = qualificationsPanel.transform.Find("Close brief");
        if (closeBrief != null) closeBrief.gameObject.SetActive(false);
        if(briefAcceptButton!=null)briefAcceptButton.gameObject.SetActive(acceptanceBriefPending);
        briefTitle.text=liveTitle;
        var book = qualificationsPanel.transform.Find("Qualifications Book");
        string openedKey = activeContractLevel == 1 ? "contractArtisanOpen" :
            activeContractLevel == 2 ? "contractGokeOpen" :
            activeContractLevel == 3 ? "contractTerrariOpen" :
            activeContractLevel == 4 ? "contractKapeOpen" : "contractHarayaOpen";
        Sprite openedArt = ExportUIArt.Get(openedKey);
        bool uniqueArt = openedArt != null;
        ExportUIArt.Apply(book.GetComponent<Image>(), uniqueArt ? openedKey : "psdOpenFolder");
        // New exports use a full 1920x1080 canvas, with the folder inset in that canvas.
        book.GetComponent<RectTransform>().sizeDelta = uniqueArt ? new Vector2(1920, 1080) : new Vector2(1346, 860);
        foreach (string decoration in new[] { "Project title tape", "Project title", "Reference photo frame", "Product photo", "Product photo label" })
        {
            var child = book.Find(decoration);
            if (child != null) child.gameObject.SetActive(!uniqueArt);
        }
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
            "LIGHT   • Readable subject\n" +
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
            "STAGE: Red backdrop + freely placed Goke     CAMERA: Any thirds intersection\nLIGHT: Readable subject     EDIT: 12s with intro/outro + 2 freely timed overlays",
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

        TextMeshProUGUI lightingTitle = CreateText("Title", lightingCard.transform, "SUBJECT LIGHTING", 34, TextAlignmentOptions.Center);
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
            "Pink backdrop (RGB 255, 140, 175)\n" +
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
