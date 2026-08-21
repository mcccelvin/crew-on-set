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
        qualificationsUnlocked = true;

        if (offerPanel != null) offerPanel.SetActive(false);
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
                declineMessageText.text = "You can review the requirements, but the Lambormini contract must be accepted to continue Level 3.";
            else
                declineMessageText.text = "You can review the requirements, but this contract must be accepted to continue Level 2.";
        }
    }

    private void ToggleQualifications()
    {
        isQualificationsOpen = !isQualificationsOpen;

        if (contractCanvas != null) contractCanvas.SetActive(isQualificationsOpen);
        if (offerPanel != null) offerPanel.SetActive(false);
        if (qualificationsPanel != null) qualificationsPanel.SetActive(isQualificationsOpen);

        if (isQualificationsOpen)
        {
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
            "STAGE   - RED backdrop and Cola away from the wall\n" +
            "CAMERA  - Rule of Thirds composition\n" +
            "LIGHT   - 3-Point Lighting\n" +
            "EDIT    - 10 seconds, 3 title-safe graphics, balanced red commercial grade\n" +
            "GRADE   - Brightness 0.94-1.02, Contrast 1.14-1.26, Saturation 1.04-1.16\n\n" +
            "UPFRONT PAYMENT: 60,000 B-COINS");

        SetQualificationSummary("STAGE: Red backdrop + product away from wall     CAMERA: Rule of Thirds\nLIGHT: Key, Fill & Back     EDIT: 10s + 3 title-safe graphics + balanced color");

        SetQualificationText("GOKE COLA - SELECTED CONTRACT",
            "RULE OF THIRDS",
            "Divide the frame into a 3 x 3 grid.\n\n" +
            "- Place the Cola near a grid intersection.\n" +
            "- Keep the main visual interest near the upper line.\n" +
            "- Leave intentional negative space.\n" +
            "- Do not use the tutorial's default center framing.",
            "3-POINT LIGHTING",
            "Build the shot using three lighting roles.\n\n" +
            "- KEY: strongest light, about 45 degrees from the subject.\n" +
            "- FILL: softer opposite light controlling shadows.\n" +
            "- BACK: light behind the subject for separation.\n" +
            "- Recommended starting point: Key 75%, Fill 40%, Back 60%.\n" +
            "- Aim every beam at the product and keep the key dominant.\n\n" +
            "POST-PRODUCTION\n" +
            "Keep graphics inside title safe. Protect the red brand palette with B 0.94-1.02, C 1.14-1.26, and S 1.04-1.16.");

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
        SetContractText("LAMBORMINI",
            "CLIENT QUALIFICATIONS\n\n" +
            "VEHICLE - Place the Lambormini car on the stage\n" +
            "CAST    - Hire and pose one actor beside the car\n" +
            "LIGHT   - Use the Level 3 Soft Light for clean reflections\n" +
            "CAMERA  - Create a premium automotive composition\n" +
            "EDIT    - 10-second premium automotive color grade\n\n" +
            "UPFRONT PAYMENT: 80,000 B-COINS");

        SetQualificationSummary("STAGE: Lambormini car + one posed actor     CAMERA: Premium automotive frame\nLIGHT: Level 3 Soft Light     EDIT: 10-second premium grade");

        SetQualificationText("LAMBORMINI - SELECTED CONTRACT",
            "ACTOR DIRECTION",
            "The actor must support the vehicle instead of hiding it.\n\n" +
            "- Hire an actor from the Director Terminal.\n" +
            "- Place the actor beside the car.\n" +
            "- Select the actor and choose a clear pose.\n" +
            "- Keep the actor from blocking the car body.",
            "AUTOMOTIVE LIGHTING",
            "Use the Level 3 Soft Light to shape the vehicle.\n\n" +
            "- Light the side and front of the car.\n" +
            "- Keep highlights clean across the body.\n" +
            "- Start near 75% intensity and -10 degrees tilt.\n" +
            "- Aim the beam so the actor and vehicle stay readable.\n" +
            "- In the Editor use Contrast 1.15-1.45, Saturation 0.95-1.20, and Brightness 0.90-1.10.");

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
            "EDIT    - 15 seconds, 2 graphics, warm color grade\n\n" +
            "UPFRONT PAYMENT: 100,000 B-COINS");

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
            "- Use exactly 2 graphics.\n" +
            "- Grade within Brightness 0.95-1.15, Contrast 1.05-1.30, and Saturation 1.05-1.30.");

        SetPreviousContractText("LAMBORMINI",
            "PREVIOUS CONTRACT\n\n" +
            "Actor and vehicle staging\n" +
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
            "UPFRONT PAYMENT: 150,000 B-COINS");

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

        Transform titleTransform = offerPanel.transform.Find("Goke Cola Contract/Contract Details/Contract Title");
        Transform descriptionTransform = offerPanel.transform.Find("Goke Cola Contract/Contract Details/Contract Description");

        if (titleTransform != null) titleTransform.GetComponent<TextMeshProUGUI>().text = title;
        if (descriptionTransform != null) descriptionTransform.GetComponent<TextMeshProUGUI>().text = description;
    }

    private void SetQualificationText(string heading, string leftTitle, string leftDescription, string rightTitle, string rightDescription)
    {
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

    private void BuildOfferPanel()
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
        contractTitle.color = new Color(1f, 0.78f, 0.2f);

        TextMeshProUGUI contractDescription = CreateText("Contract Description", contractInner.transform,
            "CLIENT QUALIFICATIONS\n\n" +
            "STAGE   • RED backdrop and Cola away from the wall\n" +
            "CAMERA  • Rule of Thirds composition\n" +
            "LIGHT   • 3-Point Lighting\n" +
            "EDIT    • 10 seconds, three title-safe graphics, balanced color\n\n" +
            "UPFRONT PAYMENT: 60,000 B-COINS",
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

    private void BuildQualificationsPanel()
    {
        qualificationsPanel = CreatePanel("Contract Qualifications", contractCanvas.transform, backgroundColor);
        SetStretchRect(qualificationsPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject mainPanel = CreatePanel("Qualifications Book", qualificationsPanel.transform, new Color(0.07f, 0.09f, 0.12f, 1f));
        SetRect(mainPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1450f, 850f));

        TextMeshProUGUI headingText = CreateText("Heading", mainPanel.transform, "GOKE COLA - SELECTED CONTRACT", 42, TextAlignmentOptions.Center);
        SetRect(headingText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(1300f, 60f));
        headingText.fontStyle = FontStyles.Bold;

        TextMeshProUGUI contractSummary = CreateText("Contract Summary", mainPanel.transform,
            "STAGE: Red backdrop + product away from wall     CAMERA: Rule of Thirds\nLIGHT: Key, Fill & Back     EDIT: 10s + 3 title-safe graphics + balanced color",
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
            "• Keep the main visual interest near the upper line.\n" +
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
