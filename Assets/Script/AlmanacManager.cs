using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

[System.Serializable]
public class KnowledgeEntry
{
    public string id;
    public string title;
    [TextArea(3, 5)] public string description;
    public string category = "Equipment";
    public int level = 1;
    public int sortOrder = 0;
    public bool isUnlocked = false;
}

[System.Serializable]
public class AchievementEntry
{
    public string id;
    public string title;
    public string description;
    public int currentProgress;
    public int maxProgress;
    public bool isUnlocked;
}

public class AlmanacManager : MonoBehaviour
{
    public static AlmanacManager Instance;

    [Header("Main UI")]
    public GameObject almanacCanvas;

    [Header("Tab Buttons")]
    public Button playerInfoTabBtn;
    public Button knowledgeTabBtn;
    public Button achievementsTabBtn;

    [Header("Panels")]
    public GameObject playerInfoPanel;
    public GameObject knowledgePanel;
    public GameObject achievementsPanel;

    [Header("Player Info UI")]
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI playerMoneyText;
    public TextMeshProUGUI totalJobsText;
    public TextMeshProUGUI currentLevelText;
    public TextMeshProUGUI activeContractText;

    [Header("Knowledge Base UI")]
    public Transform knowledgeListContainer;
    public GameObject knowledgeEntryPrefab;
    public List<KnowledgeEntry> database = new List<KnowledgeEntry>();

    [Header("Achievements UI")]
    public Transform achievementListContainer;
    public GameObject achievementEntryPrefab;
    public List<AchievementEntry> achievements = new List<AchievementEntry>();

    private bool isAlmanacOpen = false;
    private Button closeButton;
    private Button allKnowledgeButton;
    private Button equipmentKnowledgeButton;
    private Button techniquesKnowledgeButton;
    private int knowledgeCategoryFilter = 0;
    private Player.PlayerController.PlayerController playerController;
    private bool playerCouldMove = true;
    private bool playerCouldLook = true;
    private bool hasPlayerStateSnapshot = false;
    private CursorLockMode previousCursorLockState = CursorLockMode.Locked;
    private bool previousCursorVisible = false;
    private bool hasCursorStateSnapshot = false;

    private readonly Color backgroundColor = new Color(0.035f, 0.05f, 0.075f, 0.98f);
    private readonly Color panelColor = new Color(0.09f, 0.12f, 0.16f, 1f);
    private readonly Color headerColor = new Color(0.08f, 0.27f, 0.42f, 1f);
    private readonly Color buttonColor = new Color(0.16f, 0.22f, 0.29f, 1f);
    private readonly Color entryColor = new Color(0.13f, 0.17f, 0.22f, 1f);

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(this);
            return;
        }
    }

    private void Start()
    {
        InitializeAlmanacUI();
    }

    private void InitializeAlmanacUI()
    {
        EnsureEquipmentAndTechniqueEntries();
        RemoveLegacyKnowledgeEntries();
        BuildAlmanacUI();
        BuildKnowledgeFilters();
        LoadAlmanacData();
        RestoreKnowledgeProgress();

        if (almanacCanvas != null) almanacCanvas.SetActive(false);

        if (playerInfoTabBtn) playerInfoTabBtn.onClick.AddListener(OpenPlayerInfoTab);
        if (knowledgeTabBtn) knowledgeTabBtn.onClick.AddListener(OpenKnowledgeTab);
        if (achievementsTabBtn) achievementsTabBtn.onClick.AddListener(OpenAchievementsTab);
        if (closeButton) closeButton.onClick.AddListener(ToggleAlmanac);
        if (allKnowledgeButton) allKnowledgeButton.onClick.AddListener(ShowAllKnowledge);
        if (equipmentKnowledgeButton) equipmentKnowledgeButton.onClick.AddListener(ShowEquipmentKnowledge);
        if (techniquesKnowledgeButton) techniquesKnowledgeButton.onClick.AddListener(ShowTechniqueKnowledge);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindSceneCanvas(scene);
    }

    private void RebindSceneCanvas(Scene scene)
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        GameObject sceneAlmanacCanvas = null;

        foreach (Canvas canvas in canvases)
        {
            if (canvas.gameObject.scene == scene && canvas.gameObject.name == "Almanac")
            {
                sceneAlmanacCanvas = canvas.gameObject;
                break;
            }
        }

        if (sceneAlmanacCanvas == null)
        {
            if (isAlmanacOpen)
            {
                if (almanacCanvas != null) almanacCanvas.SetActive(false);
                RestoreInputState();
                isAlmanacOpen = false;
            }

            almanacCanvas = null;
            return;
        }

        if (sceneAlmanacCanvas == almanacCanvas) return;

        if (isAlmanacOpen) RestoreInputState();
        RemoveUIListeners();

        almanacCanvas = sceneAlmanacCanvas;
        playerInfoTabBtn = null;
        knowledgeTabBtn = null;
        achievementsTabBtn = null;
        playerInfoPanel = null;
        knowledgePanel = null;
        achievementsPanel = null;
        playerNameText = null;
        playerMoneyText = null;
        totalJobsText = null;
        currentLevelText = null;
        activeContractText = null;
        knowledgeListContainer = null;
        achievementListContainer = null;
        closeButton = null;
        allKnowledgeButton = null;
        equipmentKnowledgeButton = null;
        techniquesKnowledgeButton = null;
        knowledgeCategoryFilter = 0;
        playerController = null;
        hasPlayerStateSnapshot = false;
        hasCursorStateSnapshot = false;
        isAlmanacOpen = false;

        InitializeAlmanacUI();
    }

    private void RemoveUIListeners()
    {
        if (playerInfoTabBtn) playerInfoTabBtn.onClick.RemoveListener(OpenPlayerInfoTab);
        if (knowledgeTabBtn) knowledgeTabBtn.onClick.RemoveListener(OpenKnowledgeTab);
        if (achievementsTabBtn) achievementsTabBtn.onClick.RemoveListener(OpenAchievementsTab);
        if (closeButton) closeButton.onClick.RemoveListener(ToggleAlmanac);
        if (allKnowledgeButton) allKnowledgeButton.onClick.RemoveListener(ShowAllKnowledge);
        if (equipmentKnowledgeButton) equipmentKnowledgeButton.onClick.RemoveListener(ShowEquipmentKnowledge);
        if (techniquesKnowledgeButton) techniquesKnowledgeButton.onClick.RemoveListener(ShowTechniqueKnowledge);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            ToggleAlmanac();
        }
    }

    private void LateUpdate()
    {
        if (!isAlmanacOpen) return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (playerController != null)
        {
            playerController.canMove = false;
            playerController.canLook = false;
        }
    }

    public void ToggleAlmanac()
    {
        if (almanacCanvas == null) RebindSceneCanvas(SceneManager.GetActiveScene());
        if (!isAlmanacOpen && PlayerPrefs.GetInt("AlmanacUnlocked", 0) == 0) return;
        if (!isAlmanacOpen && PauseManager.isPaused) return;
        if (!isAlmanacOpen && (Cursor.visible || Cursor.lockState != CursorLockMode.Locked)) return;
        if (!isAlmanacOpen && ContractUIManager.Instance != null && ContractUIManager.Instance.IsQualificationsOpen()) return;
        if (!isAlmanacOpen && GokeLevelManager.Instance != null && !GokeLevelManager.Instance.CanOpenAlmanac()) return;
        if (!isAlmanacOpen && Level3Manager.Instance != null && !Level3Manager.Instance.CanOpenAlmanac()) return;
        if (!isAlmanacOpen && CampaignLevelManager.Instance != null && !CampaignLevelManager.Instance.CanOpenAlmanac()) return;
        if (almanacCanvas == null) return;

        isAlmanacOpen = !isAlmanacOpen;
        almanacCanvas.SetActive(isAlmanacOpen);

        if (isAlmanacOpen)
        {
            CaptureInputState();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshAllUI();
            OpenTab(1);

            if (GokeLevelManager.Instance != null) GokeLevelManager.Instance.OnAlmanacOpened();
            if (Level3Manager.Instance != null) Level3Manager.Instance.OnAlmanacOpened();
            if (CampaignLevelManager.Instance != null) CampaignLevelManager.Instance.OnAlmanacOpened();
        }
        else
        {
            RestoreInputState();

            if (GokeLevelManager.Instance != null) GokeLevelManager.Instance.OnAlmanacClosed();
            if (Level3Manager.Instance != null) Level3Manager.Instance.OnAlmanacClosed();
            if (CampaignLevelManager.Instance != null) CampaignLevelManager.Instance.OnAlmanacClosed();
        }
    }

    public bool IsOpen()
    {
        return isAlmanacOpen;
    }

    public void UnlockTutorialEquipment()
    {
        UnlockLevel1Knowledge();
    }

    public void UnlockLevel1Knowledge()
    {
        PlayerPrefs.SetInt("AlmanacUnlocked", 1);
        EnsureEquipmentAndTechniqueEntries();
        RemoveLegacyKnowledgeEntries();
        UnlockKnowledge("director_tablet");
        UnlockKnowledge("led_panel");
        UnlockKnowledge("nony_fx_camera");
        UnlockKnowledge("sd_card");
        UnlockKnowledge("set_building_technique");
        UnlockKnowledge("center_framing");
        UnlockKnowledge("basic_product_lighting");
        UnlockKnowledge("recording_technique");
        UnlockKnowledge("post_production_technique");
        PlayerPrefs.Save();
    }

    public void UnlockProductionTechniques()
    {
        EnsureEquipmentAndTechniqueEntries();
        RemoveLegacyKnowledgeEntries();
        UnlockKnowledge("rule_of_thirds");
        UnlockKnowledge("three_point_lighting");
        UnlockKnowledge("product_separation");
        UnlockKnowledge("commercial_color_grading");
        PlayerPrefs.Save();
    }

    public void UnlockLevel3Equipment()
    {
        PlayerPrefs.SetInt("AlmanacUnlocked", 1);
        EnsureEquipmentAndTechniqueEntries();
        RemoveLegacyKnowledgeEntries();
        UnlockKnowledge("level_3_soft_light");
        UnlockKnowledge("hiring_and_posing_actors");
        UnlockKnowledge("automotive_staging");
        UnlockKnowledge("soft_light_technique");
        PlayerPrefs.Save();
    }

    public void UnlockLevel4Knowledge()
    {
        PlayerPrefs.SetInt("AlmanacUnlocked", 1);
        EnsureEquipmentAndTechniqueEntries();
        RemoveLegacyKnowledgeEntries();
        UnlockKnowledge("shot_coverage");
        UnlockKnowledge("screen_continuity");
        UnlockKnowledge("motivated_lighting");
        UnlockKnowledge("lifestyle_staging");
        UnlockKnowledge("warm_commercial_grade");
        PlayerPrefs.Save();
    }

    public void UnlockLevel5Knowledge()
    {
        PlayerPrefs.SetInt("AlmanacUnlocked", 1);
        EnsureEquipmentAndTechniqueEntries();
        RemoveLegacyKnowledgeEntries();
        UnlockKnowledge("creative_brief");
        UnlockKnowledge("visual_hierarchy");
        UnlockKnowledge("quality_control");
        PlayerPrefs.Save();
    }

    private void RestoreKnowledgeProgress()
    {
        bool hasCompletedContract = PlayerPrefs.GetInt("FlowerContractGraded", 0) == 1 ||
                                    PlayerPrefs.GetInt("GokeContractGraded", 0) == 1 ||
                                    PlayerPrefs.GetInt("LamborminiContractGraded", 0) == 1 ||
                                    PlayerPrefs.GetInt("KapeKulturaContractGraded", 0) == 1 ||
                                    PlayerPrefs.GetInt("HarayaContractGraded", 0) == 1;
        if (CampaignProgression.GetCurrentLevel() >= 2 || hasCompletedContract || PlayerPrefs.GetInt("CampaignCompleted", 0) == 1)
        {
            PlayerPrefs.SetInt("AlmanacUnlocked", 1);
        }

        if (PlayerPrefs.GetInt("AlmanacUnlocked", 0) == 1)
        {
            UnlockLevel1Knowledge();
        }

        if (PlayerPrefs.GetInt("GokeContractAccepted", 0) == 1 || PlayerPrefs.GetInt("GokeContractGraded", 0) == 1 || PlayerPrefs.GetInt("Knowledge_rule_of_thirds", 0) == 1)
        {
            UnlockKnowledge("level_2_camera");
            UnlockProductionTechniques();
        }

        if (PlayerPrefs.GetInt("LamborminiContractAccepted", 0) == 1 || PlayerPrefs.GetInt("LamborminiContractGraded", 0) == 1 || PlayerPrefs.GetInt("Knowledge_level_3_soft_light", 0) == 1)
        {
            UnlockLevel3Equipment();
        }

        if (PlayerPrefs.GetInt("KapeKulturaContractAccepted", 0) == 1 || PlayerPrefs.GetInt("KapeKulturaContractGraded", 0) == 1 || PlayerPrefs.GetInt("Knowledge_shot_coverage", 0) == 1)
        {
            UnlockLevel4Knowledge();
        }

        if (PlayerPrefs.GetInt("HarayaContractAccepted", 0) == 1 || PlayerPrefs.GetInt("HarayaContractGraded", 0) == 1 || PlayerPrefs.GetInt("CampaignCompleted", 0) == 1 || PlayerPrefs.GetInt("Knowledge_creative_brief", 0) == 1)
        {
            UnlockLevel5Knowledge();
        }
    }

    private void CaptureInputState()
    {
        previousCursorLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        hasCursorStateSnapshot = true;

        playerController = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (playerController == null) return;

        playerCouldMove = playerController.canMove;
        playerCouldLook = playerController.canLook;
        hasPlayerStateSnapshot = true;
        playerController.canMove = false;
        playerController.canLook = false;
    }

    private void RestoreInputState()
    {
        if (hasPlayerStateSnapshot)
        {
            if (playerController != null)
            {
                playerController.canMove = playerCouldMove;
                playerController.canLook = playerCouldLook;
            }
        }

        playerController = null;
        hasPlayerStateSnapshot = false;

        if (hasCursorStateSnapshot)
        {
            Cursor.lockState = previousCursorLockState;
            Cursor.visible = previousCursorVisible;
            hasCursorStateSnapshot = false;
        }
    }

    private void OpenTab(int tabIndex)
    {
        if (playerInfoPanel != null) playerInfoPanel.SetActive(tabIndex == 0);
        if (knowledgePanel != null) knowledgePanel.SetActive(tabIndex == 1);
        if (achievementsPanel != null) achievementsPanel.SetActive(tabIndex == 2);
    }

    private void OpenPlayerInfoTab() { OpenTab(0); }
    private void OpenKnowledgeTab() { OpenTab(1); RefreshKnowledgeUI(); }
    private void OpenAchievementsTab() { OpenTab(2); }

    private void ShowAllKnowledge() { SetKnowledgeCategoryFilter(0); }
    private void ShowEquipmentKnowledge() { SetKnowledgeCategoryFilter(1); }
    private void ShowTechniqueKnowledge() { SetKnowledgeCategoryFilter(2); }

    private void SetKnowledgeCategoryFilter(int category)
    {
        knowledgeCategoryFilter = category;
        RefreshKnowledgeUI();
        UpdateKnowledgeFilterButtons();
    }

    private void UpdateKnowledgeFilterButtons()
    {
        if (allKnowledgeButton) allKnowledgeButton.interactable = knowledgeCategoryFilter != 0;
        if (equipmentKnowledgeButton) equipmentKnowledgeButton.interactable = knowledgeCategoryFilter != 1;
        if (techniquesKnowledgeButton) techniquesKnowledgeButton.interactable = knowledgeCategoryFilter != 2;
    }

    private void EnsureDefaultKnowledgeEntries()
    {
        AddKnowledgeEntry("rule_of_thirds", "COMPOSITION: RULE OF THIRDS", "A composition method that divides the frame into a 3 × 3 grid.\n\n• Place the subject near a grid intersection instead of automatically centering it.\n• Keep the eyes or main point of interest close to the upper horizontal line.\n• Leave open space in front of the direction the subject faces.\n• Use intentional center framing only when the brief specifically calls for it.");
        AddKnowledgeEntry("three_point_lighting", "LIGHTING: 3-POINT LIGHTING", "A professional setup using three lights with different jobs.\n\n• Key Light: the strongest light, placed about 45° from the subject.\n• Fill Light: a softer light on the opposite side that controls shadow depth.\n• Back Light: placed behind the subject to separate it from the background.\n• Balance intensity and tilt so every light supports the key instead of flattening the image.");
        AddKnowledgeEntry("director_tablet", "DIRECTOR TABLET", "Stage-building control center.\n\n• Add and paint backdrop walls with the RGB controls.\n• Spawn approved props, select them, and press [T] to reposition them.\n• Use Clear Stage when you need to rebuild the set.");
        AddKnowledgeEntry("led_panel", "160 LED PANEL", "Portable light used as a key, fill, or back light.\n\n• [LMB] toggles power.\n• [Scroll] changes intensity from 0–100%.\n• [Up/Down Arrows] adjust tilt in 5-degree steps.\n• [G] drops the light in position.");
        AddKnowledgeEntry("nony_fx_camera", "NONY FX CAMERA", "Your first production camera, designed for stable center framing.\n\n• [C] inserts a blank SD Card.\n• [LMB] opens the viewfinder and [R] records.\n• [Scroll] controls zoom and [Q/E] changes pedestal height.\n• The HUD displays focus, recording time, and subject position.");
        AddKnowledgeEntry("sd_card", "SD CARD", "Removable storage used by every production camera.\n\n• Carry a blank card and press [C] while holding the camera to load it.\n• Completing a recording ejects a used card containing the footage and grading data.\n• Carry the used card to the computer tower and press [F] to ingest it.");
        AddKnowledgeEntry("level_2_camera", "LEVEL 2 CAMERA", "Advanced camera built for professional client work.\n\n• Continuous autofocus keeps the selected subject sharp.\n• The tracking frame warns when the subject leaves the safe center area.\n• Zoom ranges from a wide 60° view to a tight 15° view.\n• Live focus distance, REC timer, camera score, and lighting score are saved with the footage.\n• Controls: [C] SD Card, [LMB] Viewfinder, [R] Record, [Scroll] Zoom, [Q/E] Height.");
        AddKnowledgeEntry("level_3_soft_light", "LEVEL 3 SOFT LIGHT", "Higher-output light designed for cleaner subject lighting.\n\n• Produces up to 40 lux, twice the output of the 160 LED Panel.\n• Soft shadows create smoother transitions across the subject.\n• Use it as a strong Key Light or move it farther away for wider coverage.\n• Controls: [LMB] Power, [Scroll] Intensity, [Up/Down Arrows] Tilt, [G] Drop.");
    }

    private void AddKnowledgeEntry(string id, string title, string description)
    {
        AddKnowledgeEntry(id, title, description, "Equipment", 1, 999);
    }

    private void AddKnowledgeEntry(string id, string title, string description, int level, int sortOrder)
    {
        AddKnowledgeEntry(id, title, description, "Equipment", level, sortOrder);
    }

    private void AddKnowledgeEntry(string id, string title, string description, string category, int level, int sortOrder)
    {
        foreach (KnowledgeEntry entry in database)
        {
            if (entry.id == id)
            {
                entry.title = title;
                entry.description = description;
                entry.category = category;
                entry.level = level;
                entry.sortOrder = sortOrder;
                return;
            }
        }

        KnowledgeEntry newEntry = new KnowledgeEntry();
        newEntry.id = id;
        newEntry.title = title;
        newEntry.description = description;
        newEntry.category = category;
        newEntry.level = level;
        newEntry.sortOrder = sortOrder;
        database.Add(newEntry);
    }

    private void EnsureLevel1KnowledgeEntries()
    {
        AddKnowledgeEntry("level_1_workflow", "LEVEL 1 - COMPLETE PRODUCTION WORKFLOW", "Use this order for every commercial.\n\n1. CONTRACT: Read the client brief before spending B-Coins.\n2. PRE-PRODUCTION: Build the backdrop, choose its color, and stage the approved props.\n3. LIGHTING: Place, power, aim, and balance your lights.\n4. CAMERA: Insert a blank SD Card, frame the subject, and record the required duration.\n5. INGEST: Pick up the used SD Card and press [F] at the computer tower.\n6. POST-PRODUCTION: Trim, add branding, color grade, export, review, and submit.", 1, 0);
        AddKnowledgeEntry("contracts_and_guides", "LEVEL 1 - CONTRACTS, ALMANAC & QUALIFICATIONS", "Use the correct reference for the job.\n\n- The contract board tells you the client, payment, and required deliverables before acceptance.\n- Press [P] to open this Production Almanac for permanent equipment, controls, and technique guides.\n- Press [TAB] after accepting a supported contract to open its exact qualification sheet.\n- The on-screen task list shows your current objective, but it does not replace the full contract.\n- Check the brief before buying, building, recording, and exporting.", 1, 10);
        AddKnowledgeEntry("director_tablet", "LEVEL 1 - DIRECTOR TABLET CONTROLS", "The Director Tablet is the stage-building control center.\n\n- Press [E] at the terminal to open or close it.\n- ADD WALL creates the stage backdrop. Select the wall before using the RGB controls.\n- Drag an approved prop card onto the stage to spawn and position it.\n- Select a placed prop and press [T] to reposition it.\n- Props and walls cost B-Coins, so avoid unnecessary duplicates.\n- CLEAR STAGE removes the current setup when you need to rebuild.", 1, 20);
        AddKnowledgeEntry("set_building", "LEVEL 1 - SET BUILDING & PRODUCT STAGING", "Build for the camera, not only for the Scene view.\n\n- Match the backdrop color requested by the client.\n- Pull the product away from the wall to create separation and reduce flat shadows.\n- Use cubes or approved props as supports when the product needs height.\n- Keep the main product visible and avoid placing graphics, actors, or props directly in front of it.\n- Open the camera viewfinder before recording and correct any overlap or empty framing.", 1, 30);
        AddKnowledgeEntry("led_panel", "LEVEL 1 - 160 LED PANEL", "Portable light used as a key, fill, or back light.\n\n- [LMB] toggles power while the light is held.\n- [Scroll] changes intensity in 5% steps from 0-100%.\n- [Up/Down Arrows] adjust tilt in 5-degree steps.\n- [G] drops the light in its current position.\n- Aim the light at the subject before dropping it. One light can illuminate a basic shot; multiple lights create depth and separation.", 1, 40);
        AddKnowledgeEntry("nony_fx_camera", "LEVEL 1 - NONY FX CAMERA", "Your first production camera for stable center framing.\n\n- [C] inserts a blank SD Card from the hotbar.\n- [LMB] opens or closes the viewfinder.\n- [Scroll] changes zoom and [Q/E] changes pedestal height.\n- [R] starts or stops recording. Camera adjustments are locked while recording.\n- For the Flower Vase training contract, keep the subject centered and hold the shot for 10 seconds.", 1, 50);
        AddKnowledgeEntry("sd_card", "LEVEL 1 - SD CARD & FOOTAGE INGEST", "Every recording requires a blank SD Card.\n\n- Pick up a blank card before preparing the camera.\n- Hold the camera and press [C] to consume and insert the blank card.\n- Stopping a valid recording ejects a used card containing the footage and production scores.\n- Press [E] to pick up the used card.\n- Hold it at the computer tower and press [F] to ingest the footage.\n- Open the monitor with [E], then review the clip in RECORDINGS before entering the Editor.", 1, 60);
        AddKnowledgeEntry("recording_workflow", "LEVEL 1 - RECORDING CHECKLIST", "Check these items before pressing [R].\n\n- The correct product and backdrop are visible.\n- Every required light is powered, aimed, and set to the intended intensity.\n- A blank SD Card is inserted.\n- The subject is detected and framed for the requested composition.\n- Zoom and camera height are final because adjustments lock during recording.\n- Record at least the required duration, then stop with [R] and collect the ejected card.", 1, 70);
        AddKnowledgeEntry("post_production_workflow", "LEVEL 1 - POST-PRODUCTION CHECKLIST", "Turn the recorded take into the final commercial.\n\n- Drag the recorded clip from the media bin to the Video Track.\n- Preview it, double-click it, and trim the handles to the required duration.\n- Move the clip to 0.0 seconds so the sequence has no empty opening.\n- Add the required branding graphics without blocking the product.\n- Time each branding clip on its timeline track.\n- Adjust brightness, contrast, and saturation to match the client brief.\n- Export, watch the final render, then submit it for grading.", 1, 80);
    }

    private void EnsureLevel2KnowledgeEntries()
    {
        AddKnowledgeEntry("level_2_camera", "LEVEL 2 - ADVANCED CAMERA", "Advanced camera built for professional client work.\n\n- The 3 x 3 grid supports Rule of Thirds composition.\n- Continuous autofocus and the tracking frame help keep the product readable.\n- The HUD displays focus distance, recording time, camera score, and lighting score.\n- [C] inserts an SD Card, [LMB] opens the viewfinder, [R] records, [Scroll] zooms, and [Q/E] changes height.\n- Finalize the framing before recording because look, zoom, and height adjustments lock during the take.", 2, 0);
        AddKnowledgeEntry("rule_of_thirds", "LEVEL 2 - RULE OF THIRDS", "Use the camera's 3 x 3 grid to create deliberate off-center composition.\n\n- Place the product near the left or right vertical grid line.\n- Put the most important detail close to a grid intersection.\n- Leave open space in the direction a subject faces or a vehicle points.\n- Do not center the product when the client specifically requests Rule of Thirds.\n- Check the tracking frame and product visibility before recording.", 2, 10);
        AddKnowledgeEntry("three_point_lighting", "LEVEL 2 - 3-POINT LIGHTING", "Three lights create shape and separation by performing different jobs.\n\n- KEY: strongest light, placed about 45 degrees to one side. Start near 75% intensity.\n- FILL: softer light on the opposite side. Start near 40% intensity.\n- BACK: behind the subject for separation. Start near 60% intensity.\n- Aim every beam at the product and adjust tilt until it reaches the subject.\n- Inspect the camera view for depth, readable highlights, and controlled shadows.", 2, 20);
        AddKnowledgeEntry("level_2_workflow", "LEVEL 2 - PRODUCT COMMERCIAL WORKFLOW", "Use this plan for contracts such as Goke Cola.\n\n1. Build and color the requested backdrop.\n2. Place the approved product away from the wall.\n3. Build a Key, Fill, and Back Light arrangement.\n4. Use the Level 2 Camera grid to place the product on the requested third.\n5. Record a clean take and ingest its SD Card.\n6. In the Editor, match the requested duration, graphic count, contrast, and saturation.\n7. Press [TAB] whenever you need the active contract's exact qualifications.", 2, 30);
        AddKnowledgeEntry("grading_and_feedback", "LEVEL 2 - GRADING & CLIENT FEEDBACK", "Your final grade combines three production areas.\n\n- PRE-PRODUCTION checks the stage, backdrop, approved props, and placement.\n- PRODUCTION checks composition and equipment settings throughout the recorded take.\n- POST-PRODUCTION checks duration, branding count, and color grade.\n- S requires 90+ overall, Camera 60/70, and Lighting 25/30.\n- A requires 80+ overall, Camera 50/70, and Lighting 20/30. B and C also require both departments to pass.\n- Required lighting roles and equipment must be present; editing cannot replace missing production work.", 2, 40);
    }

    private void EnsureLevel3KnowledgeEntries()
    {
        AddKnowledgeEntry("level_3_soft_light", "LEVEL 3 - SOFT LIGHT", "Higher-output light designed for cleaner subject and vehicle lighting.\n\n- Produces up to 40 lux, twice the output of the 160 LED Panel.\n- Softer shadows create smoother transitions across faces and reflective body panels.\n- For Lambormini, start near 75% intensity and -10 degrees tilt.\n- [LMB] toggles power, [Scroll] changes intensity, [Up/Down Arrows] adjust tilt, and [G] drops it.\n- Aim it across the actor and vehicle, then check that both remain readable through the camera.", 3, 0);
        AddKnowledgeEntry("hiring_and_posing_actors", "LEVEL 3 - HIRING & POSING ACTORS", "Actors are hired and staged through the Director Terminal.\n\n- Open the Director Terminal and drag an Actor card onto the stage like a prop.\n- Each actor hire costs 500 B-Coins.\n- Select the placed actor to enable the POSE ACTOR button.\n- The button cycles between Neutral, Wave, and Action poses.\n- Press [T] while the actor is selected to reposition them.\n- Keep the actor clear of the main product or vehicle so both remain readable.", 3, 10);
        AddKnowledgeEntry("automotive_staging", "LEVEL 3 - AUTOMOTIVE STAGING", "Vehicle commercials require a readable silhouette, controlled reflections, and deliberate actor placement.\n\n- Drag the approved car from the Director Terminal onto the stage.\n- Leave open space around the vehicle and show its important front or side shape.\n- Position the actor beside the vehicle instead of directly in front of it.\n- Use the Soft Light across body panels to reveal their form.\n- Use the Level 2 Camera grid to balance the actor and vehicle.\n- Press [TAB] during the active contract to review its exact qualifications.", 3, 20);
        AddKnowledgeEntry("level_3_workflow", "LEVEL 3 - ACTOR & VEHICLE WORKFLOW", "Use this plan for the Lambormini production.\n\n1. Get the Level 3 Soft Light from the delivery area.\n2. Open the Director Terminal and place the approved vehicle.\n3. Hire an actor, position them beside the vehicle, and choose a suitable pose.\n4. Light the actor and vehicle while preserving shape and separation.\n5. Frame both subjects clearly with the Level 2 Camera grid.\n6. Confirm that the actor does not block the vehicle, then record and complete post-production.\n7. Use [P] for techniques and [TAB] for exact contract qualifications.", 3, 30);
    }

    private void EnsureEquipmentAndTechniqueEntries()
    {
        AddKnowledgeEntry("director_tablet", "EQUIPMENT - DIRECTOR TABLET", "LEVEL 1 PRODUCTION STATION\n\nFEATURES\n- Builds and colors backdrop walls with RGB controls.\n- Displays the props approved for the active contract.\n- Selects, moves, poses, and clears objects placed on the stage.\n\nHOW TO USE\n- Press [E] at the Director Terminal to open it.\n- Use ADD WALL, select the wall, then adjust the RGB sliders.\n- Drag an approved prop, vehicle, or actor card onto the stage.\n- Select an object and press [T] to reposition it.\n- Select an actor and use POSE ACTOR to change pose.\n- Use CLEAR STAGE when you need to rebuild the set.", "Equipment", 1, 0);
        AddKnowledgeEntry("led_panel", "EQUIPMENT - 160 LED PANEL", "LEVEL 1 EQUIPMENT - 100 B-COINS\n\nFEATURES\n- Portable light with a maximum output of 20 lux.\n- Intensity range: 0-100% in 5% steps.\n- Tilt range: -45 to +45 degrees in 5-degree steps.\n\nHOW TO USE\n- Press [LMB] to turn it on or off while holding it.\n- While powered, use [Scroll] to change intensity.\n- Use [Up/Down Arrows] to change tilt.\n- Aim it at the subject, then press [G] to drop it in position.", "Equipment", 1, 10);
        AddKnowledgeEntry("nony_fx_camera", "EQUIPMENT - NONY FX CAMERA", "LEVEL 1 EQUIPMENT - 4,000 B-COINS\n\nFEATURES\n- Production camera with a 15-60 degree zoom range.\n- Continuous autofocus and a subject-tracking viewfinder HUD.\n- Displays focus distance, REC status, recording time, and subject position.\n- Supports zoom and pedestal-height adjustment.\n\nHOW TO USE\n- Pick it up with [E] and press [C] to insert a blank SD Card.\n- Press [LMB] to open or close the viewfinder.\n- Use [Scroll] to zoom and [Q/E] to change camera height.\n- Press [R] to start or stop recording.\n- Press [G] to drop it. Camera adjustments lock during recording.", "Equipment", 1, 20);
        AddKnowledgeEntry("sd_card", "EQUIPMENT - SD CARD", "LEVEL 1 EQUIPMENT - 50 B-COINS\n\nFEATURES\n- Blank cards provide recording storage for every camera.\n- Used cards store the footage filename, duration, camera score, lighting score, and total score.\n\nHOW TO USE\n- Keep a blank card in the hotbar and press [C] while holding a camera.\n- Stop the recording to eject the used card.\n- Pick it up with [E].\n- Hold it at the computer tower and press [F] to ingest the footage.\n- Open the monitor with [E] to review the recording.", "Equipment", 1, 30);
        AddKnowledgeEntry("level_2_camera", "EQUIPMENT - LEVEL 2 CAMERA", "LEVEL 2 EQUIPMENT - 10,000 B-COINS\n\nFEATURES\n- Adds a 3 x 3 composition grid for Rule of Thirds framing.\n- Uses the same autofocus, tracking HUD, focus display, and 15-60 degree zoom range as the NONY FX Camera.\n- Saves camera and lighting scores with the recorded footage.\n\nHOW TO USE\n- Pick it up with [E] and press [C] to insert an SD Card.\n- Press [LMB] to open the viewfinder and display the grid.\n- Use [Scroll] to zoom and [Q/E] to change height.\n- Place the subject on a grid third, then press [R] to record.\n- Press [G] to drop it.", "Equipment", 2, 0);
        AddKnowledgeEntry("level_3_soft_light", "EQUIPMENT - LEVEL 3 SOFT LIGHT", "LEVEL 3 EQUIPMENT - 5,000 B-COINS\n\nFEATURES\n- Produces up to 40 lux, twice the output of the 160 LED Panel.\n- Creates softer shadow transitions on faces and reflective surfaces.\n- Strong enough for a powerful Key Light or wider coverage from farther away.\n\nHOW TO USE\n- Press [LMB] to toggle power.\n- Use [Scroll] to change intensity.\n- Use [Up/Down Arrows] to adjust tilt.\n- Aim it across the subject or vehicle, then press [G] to drop it.", "Equipment", 3, 0);

        AddKnowledgeEntry("set_building_technique", "TECHNIQUE - SET BUILDING & PRODUCT STAGING", "LEVEL 1 TECHNIQUE\n\n- Match the backdrop color and approved props to the contract brief.\n- Use a support cube when the product needs height.\n- Keep the product visible and remove anything blocking its silhouette.\n- In the Flower Vase contract, use a pink backdrop, place the cube near center, then place the flower on top.\n- Confirm the final placement through the camera viewfinder, not only from the player view.", "Technique", 1, 0);
        AddKnowledgeEntry("center_framing", "TECHNIQUE - CENTER FRAMING", "LEVEL 1 TECHNIQUE\n\n- Place the main subject in the middle of the frame.\n- Keep it inside the center area for the entire take.\n- Use zoom to reduce distracting background and [Q/E] to correct camera height before recording.\n- Center framing creates a simple, direct product image and is required by the Flower Vase contract.\n- Do not move the camera during the 10-second recording.", "Technique", 1, 10);
        AddKnowledgeEntry("basic_product_lighting", "TECHNIQUE - BASIC PRODUCT LIGHTING", "LEVEL 1 TECHNIQUE\n\n- Use one powered light to illuminate the front or side of the product.\n- Aim the light before dropping it and confirm the beam reaches the subject.\n- For the Flower Vase contract, set intensity to 45% and tilt to -5 degrees.\n- Increase intensity when the subject is too dark; reduce it when highlights lose detail.\n- Check the result through the camera viewfinder before recording.", "Technique", 1, 20);
        AddKnowledgeEntry("recording_technique", "TECHNIQUE - STABLE 10-SECOND RECORDING", "LEVEL 1 TECHNIQUE\n\n- Insert a blank SD Card and finish the composition before pressing [R].\n- Keep the camera stable and the subject correctly framed throughout the take.\n- Record for the duration requested by the contract; the training and Goke contracts use 10 seconds.\n- Press [R] again to stop and generate the used SD Card.\n- Review the ingested clip before opening the Editor.", "Technique", 1, 30);
        AddKnowledgeEntry("post_production_technique", "TECHNIQUE - TRIMMING, BRANDING & COLOR", "LEVEL 1 TECHNIQUE\n\n- Drag the clip to the Video Track, trim it to 10.0 seconds, and move it to 0.0 seconds.\n- Keep Logo 1 on screen from 0-5 seconds and Logo 2 from 5-10 seconds without blocking the product.\n- Use brightness for exposure, contrast for separation, and saturation for color strength.\n- The Flower Vase target grade is Brightness 0.95, Contrast 1.15, and Saturation 1.10.\n- Export, review the final render, then submit it.", "Technique", 1, 40);
        AddKnowledgeEntry("rule_of_thirds", "TECHNIQUE - RULE OF THIRDS", "LEVEL 2 TECHNIQUE\n\n- Divide the frame with the Level 2 Camera's 3 x 3 grid.\n- Place the product near the left or right vertical line instead of the center.\n- Put the most important detail close to a grid intersection.\n- Leave visual space in front of the direction a person faces or a vehicle points.\n- For Goke Cola, the product must sit clearly on the left or right third.", "Technique", 2, 0);
        AddKnowledgeEntry("three_point_lighting", "TECHNIQUE - 3-POINT LIGHTING", "LEVEL 2 TECHNIQUE\n\n- KEY LIGHT: strongest, about 45 degrees to one side. Start near 75% intensity.\n- FILL LIGHT: opposite side controlling shadow depth. Start near 40%.\n- BACK LIGHT: behind the product for separation. Start near 60%.\n- Aim every beam at the product and adjust tilt until the light reaches it.\n- Check the camera image for depth, readable highlights, and controlled shadows.", "Technique", 2, 10);
        AddKnowledgeEntry("product_separation", "TECHNIQUE - PRODUCT & BACKDROP SEPARATION", "LEVEL 2 TECHNIQUE\n\n- Pull the product forward instead of leaving it against the backdrop.\n- Physical distance creates depth and gives the Back Light room to work.\n- Keep the product silhouette clear from props with similar colors.\n- For Goke Cola, use a red backdrop and keep the can at least 1.5 units away from the wall.\n- Use lighting and color contrast to guide attention toward the product.", "Technique", 2, 20);
        AddKnowledgeEntry("commercial_color_grading", "TECHNIQUE - COMMERCIAL COLOR GRADING", "LEVEL 2 TECHNIQUE\n\n- Brightness controls overall exposure, contrast separates light and dark areas, and saturation controls color strength.\n- Avoid excessive contrast that removes shadow detail.\n- For Goke Cola, trim the edit to 9-11 seconds and use exactly 3 graphics.\n- Use Brightness 0.85-1.15, Contrast 1.15-1.70, and Saturation 1.20-1.60.\n- Match the grade to the contract instead of applying the same settings to every commercial.", "Technique", 2, 30);
        AddKnowledgeEntry("hiring_and_posing_actors", "TECHNIQUE - HIRING, BLOCKING & POSING ACTORS", "LEVEL 3 TECHNIQUE\n\n- Drag an Actor card from the Director Terminal onto the stage. Each hire costs 500 B-Coins.\n- Select the actor to enable POSE ACTOR.\n- Cycle between Neutral, Wave, and Action poses to match the commercial.\n- Select the actor and press [T] to reposition them.\n- Block the actor beside the product or vehicle without hiding its important shape.", "Technique", 3, 0);
        AddKnowledgeEntry("automotive_staging", "TECHNIQUE - AUTOMOTIVE STAGING & COMPOSITION", "LEVEL 3 TECHNIQUE\n\n- Show a readable front or side silhouette of the vehicle.\n- Leave open space around the body instead of crowding it with props.\n- Place the actor beside the car rather than directly in front of it.\n- Use the Rule of Thirds grid to balance the actor and vehicle as two subjects.\n- Check that the pose, vehicle direction, and empty space guide the viewer through the frame.", "Technique", 3, 10);
        AddKnowledgeEntry("soft_light_technique", "TECHNIQUE - SOFT LIGHTING FOR REFLECTIVE SURFACES", "LEVEL 3 TECHNIQUE\n\n- Move the Level 3 Soft Light across the front or side of the vehicle to reveal body shape.\n- Start near 75% intensity and -10 degrees tilt, then aim the beam at the actor and car.\n- Change distance and intensity together: farther placement widens coverage but reduces brightness.\n- Keep enough shadow to preserve depth instead of lighting every surface equally.\n- In post use Contrast 1.15-1.45, Saturation 0.95-1.20, and Brightness 0.90-1.10.", "Technique", 3, 20);
        AddKnowledgeEntry("shot_coverage", "TECHNIQUE - SHOT COVERAGE", "LEVEL 4 TECHNIQUE\n\n- Coverage records the same action at useful shot sizes so the editor can build a clear sequence.\n- WIDE establishes the actor, product, and setting.\n- MEDIUM shows the actor using or presenting the product.\n- CLOSE-UP emphasizes the product or a meaningful detail.\n- For Kape Kultura, record all three sizes and keep every required subject visible before moving to the next setup.", "Technique", 4, 0);
        AddKnowledgeEntry("screen_continuity", "TECHNIQUE - SCREEN DIRECTION & CONTINUITY", "LEVEL 4 TECHNIQUE\n\n- Keep the camera on one side of the actor-product axis so screen direction remains consistent.\n- Preserve the actor's pose and the positions of important props between matching shots.\n- A sudden side reversal can make the actor appear to face or move in the opposite direction.\n- Check each recording before ingesting it: the wide, medium, and close-up should feel like one continuous moment.", "Technique", 4, 10);
        AddKnowledgeEntry("motivated_lighting", "TECHNIQUE - MOTIVATED SOFT LIGHT", "LEVEL 4 TECHNIQUE\n\n- Motivated lighting appears to come from a believable source such as a window or practical lamp.\n- Use the Level 3 Soft Light as a natural-looking key and keep its direction consistent across every shot.\n- Protect highlight detail on the cup and readable light on the actor's face.\n- Avoid changing intensity, tilt, or light direction between coverage unless the story motivates the change.", "Technique", 4, 20);
        AddKnowledgeEntry("lifestyle_staging", "TECHNIQUE - LIFESTYLE PRODUCT STAGING", "LEVEL 4 TECHNIQUE\n\n- Show how the product belongs in a person's daily routine instead of presenting it alone.\n- Keep the actor close enough to establish a relationship with the product without hiding it.\n- Use warm backdrop color, balanced negative space, and an uncluttered silhouette to support a welcoming mood.\n- The product must remain the clearest visual priority in every required shot.", "Technique", 4, 30);
        AddKnowledgeEntry("warm_commercial_grade", "TECHNIQUE - WARM COMMERCIAL COLOR GRADE", "LEVEL 4 TECHNIQUE\n\n- Correct exposure and shot matching before creating the warm look.\n- Keep skin and product color believable while using moderate saturation for warmth.\n- Match every clip so cuts do not create brightness or color jumps.\n- For Kape Kultura use Brightness 0.95-1.15, Contrast 1.05-1.30, and Saturation 1.05-1.30.\n- Finish a 15-second sequence with exactly 2 readable graphics.", "Technique", 4, 40);
        AddKnowledgeEntry("creative_brief", "TECHNIQUE - INTERPRETING A CREATIVE BRIEF", "LEVEL 5 TECHNIQUE\n\n- Identify the audience, communication goal, required subjects, mood, and deliverables before building the set.\n- Translate each written requirement into a visible production decision.\n- For Haraya, the teal campaign world, actor, product, and vehicle must feel like one intentional brand story.\n- A creative choice can break a composition convention only when it still serves the brief and remains readable.", "Technique", 5, 0);
        AddKnowledgeEntry("visual_hierarchy", "TECHNIQUE - INTEGRATED CAMPAIGN & VISUAL HIERARCHY", "LEVEL 5 TECHNIQUE\n\n- Visual hierarchy controls what the viewer notices first, second, and third.\n- Use scale, contrast, placement, light, and negative space to make the product dominant while the actor and vehicle provide context.\n- Carry one brand idea through production design, performance, composition, lighting, graphics, and color.\n- Prevent tangencies, overlaps, and background clutter from weakening silhouettes.\n- Record at least four purposeful shots using three shot sizes so the final edit has progression and variety.", "Technique", 5, 10);
        AddKnowledgeEntry("quality_control", "TECHNIQUE - COMMERCIAL QUALITY CONTROL", "LEVEL 5 TECHNIQUE\n\n- Review the brief before recording, before export, and before submission.\n- Confirm required subjects, shot variety, lighting roles, duration, graphic count, and color ranges.\n- Watch the finished sequence for empty frames, accidental reversals, obstructed products, mismatched shots, or unreadable graphics.\n- Haraya requires a 20-second edit, three graphics, three-point lighting, and a polished grade within the contract qualifications.", "Technique", 5, 20);
    }

    private void RemoveLegacyKnowledgeEntries()
    {
        string[] legacyEntryIds = new string[]
        {
            "level_1_workflow",
            "contracts_and_guides",
            "set_building",
            "recording_workflow",
            "post_production_workflow",
            "level_2_workflow",
            "grading_and_feedback",
            "level_3_workflow"
        };

        for (int databaseIndex = database.Count - 1; databaseIndex >= 0; databaseIndex--)
        {
            foreach (string legacyEntryId in legacyEntryIds)
            {
                if (database[databaseIndex].id != legacyEntryId) continue;
                database.RemoveAt(databaseIndex);
                break;
            }
        }
    }

    private void LoadAlmanacData()
    {
        foreach (var entry in database)
        {
            entry.isUnlocked = PlayerPrefs.GetInt("Knowledge_" + entry.id, 0) == 1;
        }

        foreach (var ach in achievements)
        {
            ach.currentProgress = PlayerPrefs.GetInt("AchivProg_" + ach.id, 0);
            ach.isUnlocked = PlayerPrefs.GetInt("AchivDone_" + ach.id, 0) == 1;
        }
    }

    public void SaveAlmanacData()
    {
        foreach (var entry in database)
        {
            PlayerPrefs.SetInt("Knowledge_" + entry.id, entry.isUnlocked ? 1 : 0);
        }

        foreach (var ach in achievements)
        {
            PlayerPrefs.SetInt("AchivProg_" + ach.id, ach.currentProgress);
            PlayerPrefs.SetInt("AchivDone_" + ach.id, ach.isUnlocked ? 1 : 0);
        }
        PlayerPrefs.Save();
    }

    public void UnlockKnowledge(string id)
    {
        foreach (var entry in database)
        {
            if (entry.id == id && !entry.isUnlocked)
            {
                entry.isUnlocked = true;
                Debug.Log($"<color=cyan>Knowledge Unlocked: {entry.title}</color>");
                SaveAlmanacData();
                return;
            }
        }
    }

    public void AddAchievementProgress(string id, int amount)
    {
        foreach (var ach in achievements)
        {
            if (ach.id == id && !ach.isUnlocked)
            {
                ach.currentProgress += amount;
                if (ach.currentProgress >= ach.maxProgress)
                {
                    ach.currentProgress = ach.maxProgress;
                    ach.isUnlocked = true;
                    Debug.Log($"<color=yellow>Achievement Unlocked: {ach.title}!</color>");
                }
                SaveAlmanacData();
                return;
            }
        }
    }

    private void RefreshAllUI()
    {
        if (playerNameText != null) playerNameText.text = "Director: " + PlayerPrefs.GetString("PlayerName", "Guest");

        if (CareerManager.Instance != null && playerMoneyText != null)
            playerMoneyText.text = "Bank: " + CareerManager.Instance.playerMoney + " B-Coins";

        int jobsDone = PlayerPrefs.GetInt("TotalJobsCompleted", 0);
        if (totalJobsText != null) totalJobsText.text = "Commercials Completed: " + jobsDone;

        int currentLevel = CampaignProgression.GetCurrentLevel();
        if (currentLevelText != null) currentLevelText.text = "Current Production Level: " + currentLevel;

        if (activeContractText != null)
        {
            string activeContract = CareerManager.Instance != null ? CareerManager.Instance.currentActiveJob : "None";
            activeContractText.text = "Active Contract: " + activeContract;
        }

        RefreshKnowledgeUI();
        RefreshAchievementsUI();
    }

    private void RefreshKnowledgeUI()
    {
        if (knowledgeListContainer == null) return;

        foreach (Transform child in knowledgeListContainer)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        List<KnowledgeEntry> visibleEntries = new List<KnowledgeEntry>();
        foreach (var entry in database)
        {
            if (!entry.isUnlocked) continue;
            if (knowledgeCategoryFilter == 1 && entry.category != "Equipment") continue;
            if (knowledgeCategoryFilter == 2 && entry.category != "Technique") continue;

            visibleEntries.Add(entry);
        }

        visibleEntries.Sort(CompareKnowledgeEntries);

        foreach (KnowledgeEntry entry in visibleEntries)
        {
            CreateKnowledgeEntryUI(entry);
        }

        if (visibleEntries.Count == 0)
        {
            string message = knowledgeCategoryFilter == 0 ?
                "Complete lessons to unlock Almanac entries." :
                "No " + (knowledgeCategoryFilter == 1 ? "equipment" : "techniques") + " have been unlocked yet.";
            CreateEmptyMessage(knowledgeListContainer, message);
        }

        RebuildKnowledgeLayout();
        UpdateKnowledgeFilterButtons();
    }

    private void RebuildKnowledgeLayout()
    {
        if (knowledgeListContainer == null) return;

        RectTransform contentRect = knowledgeListContainer as RectTransform;
        if (contentRect == null) return;

        VerticalLayoutGroup layoutGroup = contentRect.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup != null) layoutGroup.enabled = false;

        ContentSizeFitter contentSizeFitter = contentRect.GetComponent<ContentSizeFitter>();
        if (contentSizeFitter != null) contentSizeFitter.enabled = false;

        float currentY = 10f;
        foreach (Transform child in knowledgeListContainer)
        {
            if (!child.gameObject.activeSelf) continue;

            RectTransform childRect = child as RectTransform;
            if (childRect == null) continue;

            LayoutElement layoutElement = child.GetComponent<LayoutElement>();
            float childHeight = layoutElement != null ? layoutElement.preferredHeight : 100f;

            childRect.anchorMin = new Vector2(0f, 1f);
            childRect.anchorMax = new Vector2(1f, 1f);
            childRect.pivot = new Vector2(0.5f, 1f);
            childRect.anchoredPosition = new Vector2(0f, -currentY);
            childRect.sizeDelta = new Vector2(-20f, childHeight);

            currentY += childHeight + 14f;
        }

        contentRect.sizeDelta = new Vector2(0f, currentY + 10f);
        contentRect.anchoredPosition = Vector2.zero;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        if (contentRect.parent != null)
        {
            RectTransform viewportRect = contentRect.parent as RectTransform;
            if (viewportRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(viewportRect);
        }
    }

    private int CompareKnowledgeEntries(KnowledgeEntry firstEntry, KnowledgeEntry secondEntry)
    {
        int firstCategoryOrder = firstEntry.category == "Equipment" ? 0 : 1;
        int secondCategoryOrder = secondEntry.category == "Equipment" ? 0 : 1;
        int categoryComparison = firstCategoryOrder.CompareTo(secondCategoryOrder);
        if (categoryComparison != 0) return categoryComparison;

        int levelComparison = firstEntry.level.CompareTo(secondEntry.level);
        if (levelComparison != 0) return levelComparison;
        return firstEntry.sortOrder.CompareTo(secondEntry.sortOrder);
    }

    private void RefreshAchievementsUI()
    {
        if (achievementListContainer == null) return;

        foreach (Transform child in achievementListContainer) Destroy(child.gameObject);

        if (achievements.Count == 0)
        {
            CreateEmptyMessage(achievementListContainer, "Achievements will be added as your directing career expands.");
            return;
        }

        foreach (var ach in achievements)
        {
            CreateAchievementEntryUI(ach);
        }
    }

    private void CreateKnowledgeEntryUI(KnowledgeEntry entry)
    {
        GameObject entryObject = CreatePanel("Knowledge Entry", knowledgeListContainer, entryColor);
        LayoutElement layoutElement = entryObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = Mathf.Clamp(190f + entry.description.Length * 0.3f, 250f, 390f);

        TextMeshProUGUI titleText = CreateText("Title", entryObject.transform, entry.title, 30, TextAlignmentOptions.Left);
        SetStretchRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25f, -75f), new Vector2(-25f, -25f));
        titleText.fontStyle = FontStyles.Bold;
        if (entry.category == "Equipment") titleText.color = new Color(0.35f, 0.8f, 1f);
        else titleText.color = new Color(1f, 0.7f, 0.3f);

        TextMeshProUGUI descriptionText = CreateText("Description", entryObject.transform, entry.description, 20, TextAlignmentOptions.TopLeft);
        SetStretchRect(descriptionText.rectTransform, Vector2.zero, Vector2.one, new Vector2(25f, 20f), new Vector2(-25f, -80f));
    }

    private void CreateAchievementEntryUI(AchievementEntry achievement)
    {
        GameObject entryObject = CreatePanel("Achievement Entry", achievementListContainer, entryColor);
        LayoutElement layoutElement = entryObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 145f;

        CanvasGroup canvasGroup = entryObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = achievement.isUnlocked ? 1f : 0.5f;

        TextMeshProUGUI titleText = CreateText("Title", entryObject.transform, achievement.title, 28, TextAlignmentOptions.Left);
        SetStretchRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25f, -65f), new Vector2(-250f, -20f));
        titleText.fontStyle = FontStyles.Bold;

        TextMeshProUGUI descriptionText = CreateText("Description", entryObject.transform, achievement.description, 19, TextAlignmentOptions.TopLeft);
        SetStretchRect(descriptionText.rectTransform, Vector2.zero, Vector2.one, new Vector2(25f, 18f), new Vector2(-250f, -70f));

        string progress = achievement.isUnlocked ? "COMPLETED" : achievement.currentProgress + " / " + achievement.maxProgress;
        TextMeshProUGUI progressText = CreateText("Progress", entryObject.transform, progress, 20, TextAlignmentOptions.Center);
        SetStretchRect(progressText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-220f, 20f), new Vector2(-30f, -20f));
    }

    private void CreateEmptyMessage(Transform parent, string message)
    {
        GameObject messageObject = new GameObject("Empty Message", typeof(RectTransform), typeof(LayoutElement));
        messageObject.transform.SetParent(parent, false);
        messageObject.GetComponent<LayoutElement>().preferredHeight = 100f;

        TextMeshProUGUI messageText = CreateText("Text", messageObject.transform, message, 22, TextAlignmentOptions.Center);
        SetStretchRect(messageText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        messageText.color = new Color(0.65f, 0.7f, 0.75f);
    }

    private void BuildAlmanacUI()
    {
        if (almanacCanvas == null || playerInfoPanel != null) return;

        Canvas canvas = almanacCanvas.GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = 60;

        RectTransform canvasRect = almanacCanvas.GetComponent<RectTransform>();
        canvasRect.localScale = Vector3.one;
        SetStretchRect(canvasRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject background = CreatePanel("Almanac Background", almanacCanvas.transform, backgroundColor);
        SetStretchRect(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject mainPanel = CreatePanel("Almanac Book", background.transform, panelColor);
        SetRect(mainPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 860f));

        GameObject header = CreatePanel("Header", mainPanel.transform, headerColor);
        SetStretchRect(header.GetComponent<RectTransform>(), new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -100f), Vector2.zero);

        TextMeshProUGUI titleText = CreateText("Title", header.transform, "PRODUCTION ALMANAC", 46, TextAlignmentOptions.Left);
        SetStretchRect(titleText.rectTransform, Vector2.zero, Vector2.one, new Vector2(40f, 0f), new Vector2(-280f, 0f));
        titleText.fontStyle = FontStyles.Bold;

        closeButton = CreateButton("Close Button", header.transform, "CLOSE  [P]");
        SetRect(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-145f, 0f), new Vector2(230f, 58f));

        GameObject sidePanel = CreatePanel("Tabs", mainPanel.transform, new Color(0.065f, 0.085f, 0.11f, 1f));
        SetStretchRect(sidePanel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(300f, -100f));

        playerInfoTabBtn = CreateButton("Director Tab", sidePanel.transform, "DIRECTOR");
        SetRect(playerInfoTabBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(250f, 64f));

        knowledgeTabBtn = CreateButton("Knowledge Tab", sidePanel.transform, "KNOWLEDGE");
        SetRect(knowledgeTabBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(250f, 64f));

        achievementsTabBtn = CreateButton("Achievements Tab", sidePanel.transform, "ACHIEVEMENTS");
        SetRect(achievementsTabBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(250f, 64f));

        GameObject contentArea = new GameObject("Content Area", typeof(RectTransform));
        contentArea.transform.SetParent(mainPanel.transform, false);
        SetStretchRect(contentArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(330f, 35f), new Vector2(-35f, -125f));

        BuildPlayerInfoPanel(contentArea.transform);
        BuildKnowledgePanel(contentArea.transform);
        BuildAchievementsPanel(contentArea.transform);
    }

    private void BuildPlayerInfoPanel(Transform parent)
    {
        playerInfoPanel = CreatePanel("Director Panel", parent, new Color(0f, 0f, 0f, 0f));
        SetStretchRect(playerInfoPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        TextMeshProUGUI sectionTitle = CreateText("Section Title", playerInfoPanel.transform, "DIRECTOR PROFILE", 38, TextAlignmentOptions.Left);
        SetStretchRect(sectionTitle.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(20f, -70f), new Vector2(-40f, -10f));
        sectionTitle.fontStyle = FontStyles.Bold;

        playerNameText = CreateText("Player Name", playerInfoPanel.transform, "Director: Guest", 30, TextAlignmentOptions.Left);
        SetStretchRect(playerNameText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -190f), new Vector2(-40f, -130f));

        playerMoneyText = CreateText("Player Money", playerInfoPanel.transform, "Bank: 0 B-Coins", 30, TextAlignmentOptions.Left);
        SetStretchRect(playerMoneyText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -270f), new Vector2(-40f, -210f));

        totalJobsText = CreateText("Jobs Completed", playerInfoPanel.transform, "Commercials Completed: 0", 30, TextAlignmentOptions.Left);
        SetStretchRect(totalJobsText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -350f), new Vector2(-40f, -290f));

        currentLevelText = CreateText("Current Level", playerInfoPanel.transform, "Current Production Level: 1", 30, TextAlignmentOptions.Left);
        SetStretchRect(currentLevelText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -430f), new Vector2(-40f, -370f));

        activeContractText = CreateText("Active Contract", playerInfoPanel.transform, "Active Contract: None", 30, TextAlignmentOptions.Left);
        SetStretchRect(activeContractText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -510f), new Vector2(-40f, -450f));
    }

    private void BuildKnowledgePanel(Transform parent)
    {
        knowledgePanel = CreatePanel("Equipment Panel", parent, new Color(0f, 0f, 0f, 0f));
        SetStretchRect(knowledgePanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        TextMeshProUGUI sectionTitle = CreateText("Section Title", knowledgePanel.transform, "EQUIPMENT & TECHNIQUES", 38, TextAlignmentOptions.Left);
        SetStretchRect(sectionTitle.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(20f, -70f), new Vector2(-40f, -10f));
        sectionTitle.fontStyle = FontStyles.Bold;

        knowledgeListContainer = CreateScrollList("Equipment List", knowledgePanel.transform);
        BuildKnowledgeFilters();
    }

    private void BuildKnowledgeFilters()
    {
        if (knowledgePanel == null || allKnowledgeButton != null) return;

        allKnowledgeButton = CreateButton("All Guides Button", knowledgePanel.transform, "ALL GUIDES");
        SetRect(allKnowledgeButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(125f, -112f), new Vector2(220f, 50f));

        equipmentKnowledgeButton = CreateButton("Equipment Button", knowledgePanel.transform, "EQUIPMENT");
        SetRect(equipmentKnowledgeButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(365f, -112f), new Vector2(220f, 50f));

        techniquesKnowledgeButton = CreateButton("Techniques Button", knowledgePanel.transform, "TECHNIQUES");
        SetRect(techniquesKnowledgeButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(605f, -112f), new Vector2(220f, 50f));

        if (knowledgeListContainer != null && knowledgeListContainer.parent != null && knowledgeListContainer.parent.parent != null)
        {
            RectTransform scrollRect = knowledgeListContainer.parent.parent.GetComponent<RectTransform>();
            SetStretchRect(scrollRect, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -150f));
        }
    }

    private void BuildAchievementsPanel(Transform parent)
    {
        achievementsPanel = CreatePanel("Achievements Panel", parent, new Color(0f, 0f, 0f, 0f));
        SetStretchRect(achievementsPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        TextMeshProUGUI sectionTitle = CreateText("Section Title", achievementsPanel.transform, "ACHIEVEMENTS", 38, TextAlignmentOptions.Left);
        SetStretchRect(sectionTitle.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(20f, -70f), new Vector2(-40f, -10f));
        sectionTitle.fontStyle = FontStyles.Bold;

        achievementListContainer = CreateScrollList("Achievement List", achievementsPanel.transform);
    }

    private Transform CreateScrollList(string objectName, Transform parent)
    {
        GameObject scrollObject = CreatePanel(objectName, parent, new Color(0.04f, 0.055f, 0.075f, 1f));
        SetStretchRect(scrollObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -90f));

        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 35f;

        GameObject viewport = CreatePanel("Viewport", scrollObject.transform, new Color(0f, 0f, 0f, 0f));
        SetStretchRect(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));
        viewport.AddComponent<RectMask2D>();

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layoutGroup = content.GetComponent<VerticalLayoutGroup>();
        layoutGroup.padding = new RectOffset(10, 10, 10, 10);
        layoutGroup.spacing = 14f;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;

        ContentSizeFitter contentSizeFitter = content.GetComponent<ContentSizeFitter>();
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRect;

        return content.transform;
    }

    private GameObject CreatePanel(string objectName, Transform parent, Color color)
    {
        GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        panelObject.GetComponent<Image>().color = color;
        return panelObject;
    }

    private Button CreateButton(string objectName, Transform parent, string buttonLabel)
    {
        GameObject buttonObject = CreatePanel(objectName, parent, buttonColor);
        Button button = buttonObject.AddComponent<Button>();

        ColorBlock colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = new Color(0.22f, 0.36f, 0.48f, 1f);
        colors.pressedColor = new Color(0.08f, 0.18f, 0.27f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        TextMeshProUGUI buttonText = CreateText("Text", buttonObject.transform, buttonLabel, 22, TextAlignmentOptions.Center);
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
        if (isAlmanacOpen) RestoreInputState();
        RemoveUIListeners();
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }
}
