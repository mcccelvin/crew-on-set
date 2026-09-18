using PlayerPrefs = GameSavePrefs;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

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

public partial class AlmanacManager : MonoBehaviour
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
    [SerializeField] private Button closeButton;
    [SerializeField] private Button allKnowledgeButton;
    [SerializeField] private Button equipmentKnowledgeButton;
    [SerializeField] private Button techniquesKnowledgeButton;
    [SerializeField] private GameObject techniqueGuidePanel;
    [SerializeField] private AlmanacGuidePlayer ruleOfThirdsGuidePlayer;
    private int knowledgeCategoryFilter = 0;
    private HashSet<string> stagedHiddenKnowledge = new HashSet<string>();
    private Player.Manager.InputManager inputManager;
    private Player.PlayerController.PlayerController playerController;
    private bool playerCouldMove = true;
    private bool playerCouldLook = true;
    private bool hasPlayerStateSnapshot = false;
    private CursorLockMode previousCursorLockState = CursorLockMode.Locked;
    private bool previousCursorVisible = false;
    private bool hasCursorStateSnapshot = false;

    private readonly Color backgroundColor = new Color(0.12f, 0.085f, 0.055f, 0.92f);
    private readonly Color panelColor = new Color(0.94f, 0.87f, 0.71f, 1f);
    private readonly Color headerColor = new Color32(88, 57, 36, 255);
    private readonly Color buttonColor = new Color32(88, 57, 36, 255);
    private readonly Color entryColor = new Color32(248, 240, 220, 255);

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
        BindBookButtons();
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
        bookEntryTitle=null;
        bookPage=0;
        bookCategory=0;
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
        techniqueGuidePanel = null;
        ruleOfThirdsGuidePlayer = null;
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
        UpdateNavigationLesson();
        if (inputManager == null) inputManager = FindObjectOfType<Player.Manager.InputManager>();

        Keyboard keyboard = Keyboard.current;
        bool actionPressed = inputManager != null && inputManager.ConsumeAlmanac();
        bool keyPressed = keyboard != null && keyboard.pKey.wasPressedThisFrame;
        // Keep the global book shortcut available after menu/input-map transitions.
        // A single OR prevents the action and keyboard paths toggling twice.
        bool almanacPressed = Application.isFocused && (actionPressed || keyPressed);

        if (almanacPressed)
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
        if (!DevTutorialBypass.Disabled && !isAlmanacOpen && PlayerPrefs.GetInt("AlmanacUnlocked", 0) == 0) return;
        if (!isAlmanacOpen && PauseManager.isPaused) return;
        if (!isAlmanacOpen)
        {
            // Cursor state is not menu state: Resume/focus can leave it unlocked.
            if (TutorialUIManager.Instance != null && TutorialUIManager.Instance.IsBossDialogueOpen()) return;
            if (ContractUIManager.Instance != null &&
                (ContractUIManager.Instance.IsQualificationsOpen() || ContractUIManager.Instance.IsContractUIOpen())) return;
            DirectorTerminal director = FindObjectOfType<DirectorTerminal>();
            if (director != null && director.IsTerminalActive()) return;
            ShopTerminal shop = FindObjectOfType<ShopTerminal>();
            if (shop != null && shop.IsTerminalActive()) return;
            ComputerStation computer = FindObjectOfType<ComputerStation>();
            if (computer != null && computer.computerUICanvas != null && computer.computerUICanvas.activeInHierarchy) return;

            int level = CampaignProgression.GetCurrentLevel();
            if (level == 2 && GokeLevelManager.Instance != null && !GokeLevelManager.Instance.CanOpenAlmanac()) return;
            if (level == 3 && Level3Manager.Instance != null && !Level3Manager.Instance.CanOpenAlmanac()) return;
            if (level >= 4 && CampaignLevelManager.Instance != null && !CampaignLevelManager.Instance.CanOpenAlmanac()) return;
        }
        if (almanacCanvas == null) return;

        if (isAlmanacOpen) EndNavigationLesson();
        isAlmanacOpen = !isAlmanacOpen;
        almanacCanvas.SetActive(isAlmanacOpen);

        if (isAlmanacOpen)
        {
            CaptureInputState();
            // Opening from gameplay must return to gameplay, not a stale Resume cursor.
            previousCursorLockState = CursorLockMode.Locked;
            previousCursorVisible = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshAllUI();
            OpenTab(1);


            if (GokeLevelManager.Instance != null) GokeLevelManager.Instance.OnAlmanacOpened();
            if (Level3Manager.Instance != null) Level3Manager.Instance.OnAlmanacOpened();
            if (CampaignLevelManager.Instance != null) CampaignLevelManager.Instance.OnAlmanacOpened();
            BeginNavigationLesson();
        }
        else
        {
            CloseTechniqueGuide();
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

    public void PrepareLevelIntroduction(int level)
    {
        int introductionLevel = Mathf.Clamp(level, CampaignProgression.MinimumLevel, CampaignProgression.MaximumLevel);
        EnsureEquipmentAndTechniqueEntries();
        RemoveLegacyKnowledgeEntries();
        stagedHiddenKnowledge.Clear();

        foreach (KnowledgeEntry entry in database)
        {
            if (entry.level >= introductionLevel) stagedHiddenKnowledge.Add(entry.id);
        }

        if (isAlmanacOpen) RefreshKnowledgeUI();
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
        UnlockKnowledge("advertising_post_production");
        PlayerPrefs.Save();
    }

    public void UnlockLevel3Equipment()
    {
        PlayerPrefs.SetInt("AlmanacUnlocked", 1);
        EnsureEquipmentAndTechniqueEntries();
        RemoveLegacyKnowledgeEntries();
        UnlockKnowledge("level_3_soft_light");
        UnlockKnowledge("automotive_staging");
        UnlockKnowledge("soft_light_technique");
        UnlockKnowledge("vehicle_rim_lighting");
        PlayerPrefs.Save();
    }

    public void UnlockLevel4Knowledge()
    {
        PlayerPrefs.SetInt("AlmanacUnlocked", 1);
        EnsureEquipmentAndTechniqueEntries();
        RemoveLegacyKnowledgeEntries();
        UnlockKnowledge("hiring_and_posing_actors");
        UnlockKnowledge("shot_coverage");
        UnlockKnowledge("screen_continuity");
        UnlockKnowledge("motivated_lighting");
        UnlockKnowledge("lifestyle_staging");
        UnlockKnowledge("warm_commercial_grade");
        UnlockKnowledge("coffee_story_workflow");
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

        if (!HasStagedKnowledgeForLevel(2) &&
            (PlayerPrefs.GetInt("GokeContractAccepted", 0) == 1 || PlayerPrefs.GetInt("GokeContractGraded", 0) == 1 || PlayerPrefs.GetInt("Knowledge_rule_of_thirds", 0) == 1))
        {
            UnlockKnowledge("level_2_camera");
            UnlockProductionTechniques();
        }

        if (!HasStagedKnowledgeForLevel(3) &&
            (PlayerPrefs.GetInt("LamborminiContractAccepted", 0) == 1 || PlayerPrefs.GetInt("LamborminiContractGraded", 0) == 1 || PlayerPrefs.GetInt("Knowledge_level_3_soft_light", 0) == 1))
        {
            UnlockLevel3Equipment();
        }

        if (!HasStagedKnowledgeForLevel(4) &&
            (PlayerPrefs.GetInt("KapeKulturaContractAccepted", 0) == 1 || PlayerPrefs.GetInt("KapeKulturaContractGraded", 0) == 1 || PlayerPrefs.GetInt("Knowledge_shot_coverage", 0) == 1))
        {
            UnlockLevel4Knowledge();
        }

        if (!HasStagedKnowledgeForLevel(5) &&
            (PlayerPrefs.GetInt("HarayaContractAccepted", 0) == 1 || PlayerPrefs.GetInt("HarayaContractGraded", 0) == 1 || PlayerPrefs.GetInt("CampaignCompleted", 0) == 1 || PlayerPrefs.GetInt("Knowledge_creative_brief", 0) == 1))
        {
            UnlockLevel5Knowledge();
        }
    }

    private bool HasStagedKnowledgeForLevel(int level)
    {
        foreach (KnowledgeEntry entry in database)
        {
            if (entry.level == level && stagedHiddenKnowledge.Contains(entry.id)) return true;
        }

        return false;
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
        if (tabIndex != 1) CloseTechniqueGuide();
        if (playerInfoPanel != null) playerInfoPanel.SetActive(tabIndex == 0);
        if (knowledgePanel != null) knowledgePanel.SetActive(tabIndex == 1);
        if (achievementsPanel != null) achievementsPanel.SetActive(tabIndex == 2);
        if (playerInfoTabBtn != null) playerInfoTabBtn.interactable = tabIndex != 0;
        if (knowledgeTabBtn != null) knowledgeTabBtn.interactable = tabIndex != 1;
        if (achievementsTabBtn != null) achievementsTabBtn.interactable = tabIndex != 2;
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
        if(bookEntryTitle!=null)
        {
            // These tabs also return from the director record and milestones pages.
            foreach(var button in new[]{equipmentKnowledgeButton,techniquesKnowledgeButton})
            {
                if(button==null)continue;
                button.interactable=true;
                bool selected=button==equipmentKnowledgeButton?knowledgeCategoryFilter==1:knowledgeCategoryFilter==2;
                var colors=button.colors;colors.normalColor=selected?Color.white:new Color(.72f,.62f,.53f);button.colors=colors;
            }
            return;
        }
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
        AddKnowledgeEntry("director_tablet", "LEVEL 1 - DIRECTOR TABLET CONTROLS", "The Director Tablet is the stage-building control center.\n\n- Press [E] at the terminal to open or close it.\n- ADD WALL creates the stage backdrop. Select the wall before using the RGB controls.\n- Click an approved prop card to attach it to the cursor, then click the stage to place it.\n- Select a placed prop and press [T] to reposition it.\n- Props and walls cost B-Coins, so avoid unnecessary duplicates.\n- CLEAR STAGE removes the current setup when you need to rebuild.", 1, 20);
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
        AddKnowledgeEntry("three_point_lighting", "LEVEL 2 - 3-POINT LIGHTING", "Three lights create shape and separation by performing different jobs.\n\n- KEY: strongest light, placed about 45 degrees to one side. Start near 75% intensity.\n- FILL: softer light on the opposite side. Start near 40% intensity.\n- BACK: behind the subject for separation. Start near 60% intensity; these percentages are examples, not Goke grading targets.\n- Aim every beam at the product and adjust tilt until it reaches the subject.\n- Inspect the camera view for depth, readable highlights, and controlled shadows.", 2, 20);
        AddKnowledgeEntry("level_2_workflow", "LEVEL 2 - PRODUCT COMMERCIAL WORKFLOW", "Use this plan for contracts such as Goke Cola.\n\n1. Build and color the requested backdrop.\n2. Place the approved product away from the wall.\n3. Build a Key, Fill, and Back Light arrangement.\n4. Use the Level 2 Camera grid to place the product on the requested third.\n5. Record a clean take and ingest its SD Card.\n6. In CLIPS, find the supplied GOKE INTRO and GOKE OUTRO. Build INTRO 2s > your footage 6s > OUTRO 2s with no gaps. Add the logo and tagline overlays at any times, together or separately.\n7. Press [TAB] whenever you need the active contract's exact qualifications.", 2, 30);
        AddKnowledgeEntry("grading_and_feedback", "LEVEL 2 - GRADING & CLIENT FEEDBACK", "Your final grade combines three production areas.\n\n- PRE-PRODUCTION checks the stage, backdrop, approved props, and placement.\n- PRODUCTION checks composition and equipment settings throughout the recorded take.\n- POST-PRODUCTION checks duration, branding count, and color grade.\n- S requires 90+ overall, Camera 60/70, and Lighting 25/30.\n- A requires 80+ overall, Camera 50/70, and Lighting 20/30. B and C also require both departments to pass.\n- Required lighting roles and equipment must be present; editing cannot replace missing production work.", 2, 40);
    }

    private void EnsureLevel3KnowledgeEntries()
    {
        AddKnowledgeEntry("level_3_soft_light", "LEVEL 3 - SOFT LIGHT", "Higher-output light designed for cleaner subject and vehicle lighting.\n\n- Produces up to 40 lux, twice the output of the 160 LED Panel.\n- Softer shadows create smoother transitions across reflective body panels.\n- For Terrari, start near 75% intensity and -10 degrees tilt.\n- [LMB] toggles power, [Scroll] changes intensity, [Up/Down Arrows] adjust tilt, and [G] drops it.\n- Aim it across the vehicle, then check that the silhouette and highlight detail remain readable through the camera.", 3, 0);
        AddKnowledgeEntry("hiring_and_posing_actors", "LEVEL 4 - HIRING & POSING ACTORS", "Actors are hired and staged through the Director Terminal.\n\n- Click an Actor card to attach the actor to the cursor, then click the stage to place them.\n- Default hire rates: Rookie 750, Trained 2,250, Expert 4,500 B-Coins. Higher tiers have smoother, more expressive gestures.\n- Select the placed actor to enable the POSE ACTOR button.\n- The button cycles between breathing idle (Neutral), animated greeting (Wave), and product presentation (Action).\n- Press [T] while the actor is selected to reposition them; [R] turns the bot in 15-degree steps.\n- Keep the actor clear of the main product so both remain readable.", 4, 0);
        AddKnowledgeEntry("automotive_staging", "LEVEL 3 - AUTOMOTIVE STAGING", "Vehicle commercials require a readable silhouette, controlled reflections, and deliberate negative space.\n\n- Click the approved car card, move it over the stage, then click to place it.\n- Leave open space around the vehicle and show its important front or side shape.\n- Use the Soft Light across body panels to reveal their form.\n- Use the Level 2 Camera grid to place the vehicle deliberately instead of crowding the frame.\n- Press [TAB] during the active contract to review its exact qualifications.", 3, 10);
        AddKnowledgeEntry("vehicle_rim_lighting", "TECHNIQUE - SMOOTH VEHICLE MOVEMENT", "Use the warm Soft Light across the car body to reveal reflections and shape. Equip the camera, then hold [Ctrl] while using WASD and the mouse. Precision movement slows walking and looking so the shot feels controlled. Begin holding Ctrl before moving, keep the car framed, release WASD first, let the camera settle, then stop recording. The movement itself does not change the score; the recorded framing and lighting do.", "Technique", 3, 35);
        AddKnowledgeEntry("level_3_workflow", "LEVEL 3 - VEHICLE LIGHTING WORKFLOW", "Use this plan for the Terrari production.\n\n1. Use ADD WALL in the Director Tablet, choose a dark backdrop color, then place one orange Terrari.\n2. Aim the Soft Light across the body. Start at 75% output, -10 degrees, 4300K and 75% diffusion. Hold [Q/E] while carrying it to raise or lower the real stand, then [G] to place it. The beam guide is hidden from recordings.\n3. Buy three blank SD cards. Record back, side and overall views on different cards, about 7 seconds each. Hold [Ctrl] with WASD and the mouse for a smooth moving shot.\n4. Ingest all three cards. Build: TERRARI INTRO 2s + BACK 7s + SIDE 7s + OVERALL 7s + TERRARI OUTRO 2s. Join everything from 0s without gaps or overlaps for exactly 25 seconds.\n5. Keep overlays inside title safe and away from the car. Upper-left is a good starting position. Brightness 0.85-1.15, Contrast 1.05-1.45, Saturation 0.95-1.30. Use [TAB] for the brief.", 3, 20);
    }

    private void EnsureEquipmentAndTechniqueEntries()
    {
        AddKnowledgeEntry("actor_megaphone", "EQUIPMENT - DIRECTOR MEGAPHONE", "LEVEL 4 EQUIPMENT - 900 B-COINS\n\nA handheld cue tool for directing a hired Actor from the studio floor.\n\nHOW TO USE\n- Buy it from the Equipment Shop and pick it up with [E].\n- Aim at an Actor and press [LMB] to select them.\n- Press [Z] Neutral, [X] Wave, or [C] Action.\n- Arrow keys nudge the actor; [R] turns them.\n- [B/N] save walk marks, [K] rehearses, [J] returns to START, and [H] clears the route.\n- The megaphone controls the selected Actor without reopening the Director Tablet.", "Equipment", 4, 5);
        AddKnowledgeEntry("director_tablet", "EQUIPMENT - DIRECTOR TABLET", "LEVEL 1 PRODUCTION STATION\n\nFEATURES\n- Builds and colors backdrop walls with RGB controls.\n- Displays the props approved for the active contract.\n- Selects, moves, poses, and clears objects placed on the stage.\n\nHOW TO USE\n- Press [E] at the Director Terminal to open it.\n- From Level 4 use CHOOSE SET for a plain backdrop, Cafe Corner or Coffee Interior. Earlier levels use ADD WALL. Select the wall, then adjust the RGB sliders, type 0-255 in the number fields, or enter a HEX color such as #FF6600. Press Enter to apply.\n- Click an approved prop, vehicle, or actor card to attach it to the cursor.\n- Move the cursor over the stage and click again to place it.\n- Select an object and press [T] to reposition it.\n- Select an actor and use POSE ACTOR to change pose.\n- Use CLEAR STAGE when you need to rebuild the set.", "Equipment", 1, 0);
        AddKnowledgeEntry("led_panel", "EQUIPMENT - 160 LED PANEL", "LEVEL 1 EQUIPMENT - 1,200 B-COINS\n\nFEATURES\n- Portable light with a maximum output of 20 lux.\n- Intensity range: 0-100% in 5% steps.\n- Tilt range: -45 to +45 degrees in 5-degree steps.\n\nHOW TO USE\n- Press [LMB] to turn it on or off while holding it.\n- While powered, use [Scroll] to change intensity.\n- Use [Up/Down Arrows] to change tilt.\n- Hold [PgUp/PgDn] while carrying it to raise/lower the existing stand.\n- The always-on guide shows visible-beam haze; setup view only, never in the recording.\n- Aim it at the subject, then press [G] to drop it in position.", "Equipment", 1, 10);
        AddKnowledgeEntry("nony_fx_camera", "EQUIPMENT - NONY FX CAMERA", "LEVEL 1 EQUIPMENT - 4,000 B-COINS\n\nFEATURES\n- Production camera with a 15-60 degree zoom range.\n- Continuous autofocus and a subject-tracking viewfinder HUD.\n- Displays focus distance, REC status, recording time, and subject position.\n- Supports zoom and pedestal-height adjustment.\n\nHOW TO USE\n- Pick it up with [E] and press [C] to insert a blank SD Card.\n- Press [LMB] to open or close the viewfinder.\n- Use [Scroll] to zoom and [Q/E] to change camera height.\n- Press [R] to start or stop recording.\n- Hold [Ctrl] with WASD for slow, eased camera movement; mouse look becomes gentler. Release WASD before releasing Ctrl for a smooth stop.\n- Press [G] to drop it. Zoom and height lock during recording; the first tutorial still requires a stationary take.", "Equipment", 1, 20);
        AddKnowledgeEntry("sd_card", "EQUIPMENT - SD CARD", "LEVEL 1 EQUIPMENT - 150 B-COINS\n\nFEATURES\n- Blank cards provide recording storage for every camera.\n- Used cards store the footage filename, duration, camera score, lighting score, and total score.\n\nHOW TO USE\n- Keep a blank card in the hotbar and press [C] while holding a camera.\n- Stop the recording to eject the used card.\n- Pick it up with [E].\n- Hold it at the computer tower and press [F] to ingest the footage.\n- Open the monitor with [E] to review the recording.", "Equipment", 1, 30);
        AddKnowledgeEntry("level_2_camera", "EQUIPMENT - LEVEL 2 CAMERA", "LEVEL 2 EQUIPMENT - 6,000 B-COINS\n\nFEATURES\n- Adds a 3 x 3 composition grid for Rule of Thirds framing.\n- Uses the same autofocus, tracking HUD, focus display, and 15-60 degree zoom range as the NONY FX Camera.\n- Saves camera and lighting scores with the recorded footage.\n\nHOW TO USE\n- Pick it up with [E] and press [C] to insert an SD Card.\n- Press [LMB] to open the viewfinder and display the grid.\n- Use [Scroll] to zoom and [Q/E] to change height.\n- Hold [Ctrl] with WASD to move slowly with eased starts and stops; mouse look becomes gentler. Keep Ctrl held while releasing WASD to settle before stopping the take.\n- Place the subject on a grid third, then press [R] to record.\n- Press [G] to drop it.", "Equipment", 2, 0);
        AddKnowledgeEntry("level_3_soft_light", "EQUIPMENT - LEVEL 3 SOFT LIGHT", "LEVEL 3 EQUIPMENT - 4,500 B-COINS\n\nFEATURES\n- Produces up to 40 lux, twice the output of the 160 LED Panel.\n- Creates softer shadow transitions on faces and reflective surfaces.\n- Strong enough for a powerful Key Light or wider coverage from farther away.\n\nHOW TO USE\n- Press [LMB] to toggle power.\n- Use [Scroll] to change intensity.\n- Use [Up/Down Arrows] to adjust tilt.\n- Hold [PgUp/PgDn] to raise/lower the head (up to +1.5m); [G] places the stand.\n- The always-on guide shows a setup-only beam guide; it is hidden in the viewfinder and recordings.\n- Aim it across the subject or vehicle, then press [G] to drop it.", "Equipment", 3, 0);

        AddKnowledgeEntry("set_building_technique", "TECHNIQUE - SET BUILDING & PRODUCT STAGING", "LEVEL 1 TECHNIQUE\n\n- Match the backdrop color and approved props to the contract brief.\n- Use a support cube when the product needs height.\n- Keep the product visible and remove anything blocking its silhouette.\n- In the Flower Vase contract, use a pink backdrop, place the cube near center, then place the flower on top.\n- Confirm the final placement through the camera viewfinder, not only from the player view.", "Technique", 1, 0);
        AddKnowledgeEntry("center_framing", "TECHNIQUE - CENTER FRAMING", "LEVEL 1 TECHNIQUE\n\n- Place the main subject in the middle of the frame.\n- Keep it inside the center area for the entire take.\n- Use zoom to reduce distracting background and [Q/E] to correct camera height before recording.\n- Center framing creates a simple, direct product image and is required by the Flower Vase contract.\n- Do not move the camera during the 10-second recording.", "Technique", 1, 10);
        AddKnowledgeEntry("basic_product_lighting", "TECHNIQUE - BASIC PRODUCT LIGHTING", "LEVEL 1 TECHNIQUE\n\n- Use one powered light to illuminate the front or side of the product.\n- Aim the light before dropping it and confirm the beam reaches the subject.\n- For the Flower Vase contract, set intensity to 45% and tilt to -5 degrees.\n- Increase intensity when the subject is too dark; reduce it when highlights lose detail.\n- Check the result through the camera viewfinder before recording.", "Technique", 1, 20);
        AddKnowledgeEntry("recording_technique", "TECHNIQUE - STABLE 10-SECOND RECORDING", "LEVEL 1 TECHNIQUE\n\n- Insert a blank SD Card and finish the composition before pressing [R].\n- Keep the camera stable and the subject correctly framed throughout the take.\n- Record for the duration requested by the contract; the training and Goke contracts use 10 seconds.\n- Press [R] again to stop and generate the used SD Card.\n- Review the ingested clip before opening the Editor.", "Technique", 1, 30);
        AddKnowledgeEntry("post_production_technique", "TECHNIQUE - TRIMMING, BRANDING & COLOR", "LEVEL 1 TECHNIQUE\n\n- Drag the clip to the Video Track, trim it to 10.0 seconds, and move it to 0.0 seconds.\n- Keep Logo 1 on screen from 0-5 seconds and Logo 2 from 5-10 seconds without blocking the product.\n- Use brightness for exposure, contrast for separation, and saturation for color strength.\n- Choose your own Flower Vase look. All available values earn full color credit: Brightness 0.75-1.25, Contrast 0.75-1.50, Saturation 0.65-1.40. Compare Before / After and keep product detail readable.\n- Export, review the final render, then submit it.", "Technique", 1, 40);
        AddKnowledgeEntry("rule_of_thirds", "TECHNIQUE - RULE OF THIRDS", "LEVEL 2 TECHNIQUE\n\n- Divide the frame with the Level 2 Camera's 3 x 3 grid.\n- Place the product near the left or right vertical line instead of the center.\n- Put the most important detail close to a grid intersection.\n- Leave visual space in front of the direction a person faces or a vehicle points.\n- For Goke Cola, the product must sit clearly on the left or right third.", "Technique", 2, 0);
        AddKnowledgeEntry("three_point_lighting", "TECHNIQUE - 3-POINT LIGHTING", "LEVEL 2 TECHNIQUE\n\n- KEY LIGHT: strongest, about 45 degrees to one side. Start near 75% intensity.\n- FILL LIGHT: opposite side controlling shadow depth. Start near 40%.\n- BACK LIGHT: behind the product for separation. Start near 60%; these percentages are examples, not Goke grading targets.\n- Aim every beam at the product and adjust tilt until the light reaches it.\n- Check the camera image for depth, readable highlights, and controlled shadows.", "Technique", 2, 10);
        AddKnowledgeEntry("product_separation", "TECHNIQUE - PRODUCT & BACKDROP SEPARATION", "LEVEL 2 TECHNIQUE\n\n- Pull the product forward instead of leaving it against the backdrop.\n- Physical distance creates depth and gives the Back Light room to work.\n- Keep the product silhouette clear from props with similar colors.\n- For Goke Cola, use a red backdrop and place the can wherever you choose; no minimum wall distance is required.\n- Use lighting and color contrast to guide attention toward the product.", "Technique", 2, 20);
        AddKnowledgeEntry("commercial_color_grading", "TECHNIQUE - COMMERCIAL COLOR GRADING", "OPTIONAL GOKE FINISH\n\nBrightness controls exposure, contrast separates light and dark areas, and saturation controls color strength. Preserve highlight and shadow detail. For this contract these controls are optional; the lesson focuses on intro/outro placement. The contract also requires two overlays, with timing and duration chosen by you.", "Technique", 2, 30);
        AddKnowledgeEntry("advertising_post_production", "TECHNIQUE - INTRO & OUTRO", "GOKE POST-PRODUCTION\n\nINTRO: Introduces the brand and sets the tone, helping viewers understand whose commercial they are watching.\n\nOUTRO: Reinforces the brand and leaves a memorable closing message. 'Make it a Goke' invites the viewer to choose the product.\n\nBoth clips are supplied in CLIPS. Drag the full 2-second intro to 0s, place 8 seconds of your recorded product footage after it, and finish with the full 2-second outro at 10s. Join all clips without gaps or overlaps. Double-click footage to trim; right-click a clip to return it to the bank. Use both the Goke logo and tagline overlays. Choose when and how long they appear; no fixed order or minimum hold is required. Preview the complete 12-second commercial before export.", "Technique", 2, 40);
        AddKnowledgeEntry("hiring_and_posing_actors", "TECHNIQUE - HIRING, BLOCKING & POSING ACTORS", "LEVEL 4 TECHNIQUE\n\n- Click an Actor card, move the actor over the stage, then click again to place them. Hire prices reflect acting polish: Rookie 750, Trained 2,250, Expert 4,500 B-Coins (default rates).\n- Select the actor to enable POSE ACTOR.\n- Cycle between breathing idle (Neutral), animated greeting (Wave), and product presentation (Action). Bots perform automatically on their mark; they restart the action for every take. Rookie gestures are smaller with slower cues; Trained and Expert actors deliver progressively smoother, more expressive gestures. All tiers can meet the contract.\n- Select the actor and press [T] to reposition them; [R] turns the bot in 15-degree steps.\n- Block the actor beside the product without hiding its important shape.\n- Preserve the same pose and screen side across matching shots.", "Technique", 4, 0);
        AddKnowledgeEntry("automotive_staging", "TECHNIQUE - AUTOMOTIVE STAGING & COMPOSITION", "LEVEL 3 TECHNIQUE\n\n- Show a readable front or side silhouette of the vehicle.\n- Leave open space around the body instead of crowding it with props.\n- Use the Rule of Thirds grid to balance the vehicle with intentional negative space.\n- Aim the Soft Light across the body to reveal form without clipping reflections.\n- Check that the vehicle direction and empty space guide the viewer through the frame.", "Technique", 3, 10);
        AddKnowledgeEntry("soft_light_technique", "TECHNIQUE - SOFT LIGHTING FOR REFLECTIVE SURFACES", "LEVEL 3 TECHNIQUE\n\n- Move the Level 3 Soft Light across the front or side of the vehicle to reveal body shape.\n- Start near 75% intensity and -10 degrees tilt, then aim the beam across the car.\n- Change distance and intensity together: farther placement widens coverage but reduces brightness.\n- Keep enough shadow to preserve depth instead of lighting every surface equally.\n- In post use Contrast 1.15-1.45, Saturation 0.95-1.20, and Brightness 0.90-1.10.", "Technique", 3, 20);
        AddKnowledgeEntry("coffee_story_workflow", "LEVEL 4 - THE COFFEE STORY", "Build a welcoming morning moment, then tell it with three shot sizes.\n\n1. CHOOSE SET in the Director Tablet: Plain Backdrop (500 B), Cafe Corner (2,000 B), or Coffee Interior (2,750 B). Preview, BUY SET once, then USE SET. Switch owned sets for free; only one is active. Furnished sets start warm brown; wall/floor paint still works, keeping furniture colors intact. Try #80502E. Place exactly one Kape Kultura product and one Actor; use Wave or Action.\n2. Suggest a window with the Soft Light in front and to one side. Power it on and aim between the Actor and coffee. Try 75% output and at least 50% diffusion.\n3. Record a WIDE to introduce the setting, a MEDIUM to connect the Actor and product, and a CLOSE-UP to give the product emphasis. The camera focus readout names the current size. Even the close shot must show both subjects fully.\n4. Keep the same pose, prop positions and screen side throughout. Hold each take steady for about 6 seconds. Collect each recorded SD card and insert the three matching takes into the computer.\n5. Try Wide -> Medium -> Close-Up, trimmed to 5 seconds each and joined from 0. Total: 15 seconds. Use the bin labels to identify the shots.\n6. Add exactly 2 readable graphics, choose their entrance animation, camera motion, a transition and music in Branding. Try Brightness 1.05, Contrast 1.15, Saturation 1.15. Preview before export.\n\nThese shot sizes give the viewer context, connection and product emphasis. Continuity makes the cuts feel like one moment.", "Technique", 4, 50);
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
        bool wasStagedKnowledge = stagedHiddenKnowledge.Remove(id);

        foreach (var entry in database)
        {
            if (entry.id != id) continue;

            if (!entry.isUnlocked)
            {
                entry.isUnlocked = true;
                Debug.Log($"<color=cyan>Knowledge Unlocked: {entry.title}</color>");
                SaveAlmanacData();
            }

            if (wasStagedKnowledge && isAlmanacOpen) RefreshKnowledgeUI();
            return;
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

    private void RefreshDirectorName()
    {
        if (playerNameText == null) return;
        string playerName = PlayerPrefs.GetString("PlayerName", "").Trim();
        playerNameText.richText = false;
        playerNameText.text = "Director: " + (string.IsNullOrEmpty(playerName) ? "Guest" : playerName);
    }

    private void RefreshAllUI()
    {
        RefreshDirectorName();

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
        if(bookEntryTitle!=null){RefreshBookPage();return;}
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
            if (stagedHiddenKnowledge.Contains(entry.id)) continue;
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
            Transform description = child.Find("Description");
            if (description != null)
            {
                TMP_Text article = description.GetComponent<TMP_Text>();
                float width = Mathf.Max(240f, contentRect.rect.width - 100f);
                bool hasGuide = child.Find("Watch Rule of Thirds Guide") != null;
                childHeight = Mathf.Max(240f, article.GetPreferredValues(article.text, width, Mathf.Infinity).y + (hasGuide ? 190f : 130f));
                if (layoutElement != null) layoutElement.preferredHeight = childHeight;
            }

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
        bool hasVideoGuide = entry.id == "rule_of_thirds" && entry.category == "Technique";
        layoutElement.preferredHeight = Mathf.Clamp(190f + entry.description.Length * 0.3f, 250f, hasVideoGuide ? 450f : 390f);

        GameObject rule = CreatePanel("Chapter Rule", entryObject.transform, new Color32(88, 57, 36, 255));
        SetStretchRect(rule.GetComponent<RectTransform>(), new Vector2(0, 1), Vector2.one, new Vector2(25, -8), new Vector2(-25, -5));
        rule.GetComponent<Image>().raycastTarget = false;
        TextMeshProUGUI titleText = CreateText("Title", entryObject.transform, entry.title.Replace("TECHNIQUE - ", "").Replace("EQUIPMENT - ", ""), 28, TextAlignmentOptions.Left);
        SetStretchRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25f, -75f), new Vector2(-25f, -25f));
        titleText.fontStyle = FontStyles.Bold;
        if (entry.category == "Equipment") titleText.color = EditorWorkspaceUI.Ink;
        else titleText.color = EditorWorkspaceUI.Ink;

        TextMeshProUGUI descriptionText = CreateText("Description", entryObject.transform, entry.description, 24, TextAlignmentOptions.TopLeft);
        descriptionText.lineSpacing = 6;
        SetStretchRect(descriptionText.rectTransform, Vector2.zero, Vector2.one, new Vector2(25f, hasVideoGuide ? 85f : 20f), new Vector2(-25f, -80f));

        if (hasVideoGuide)
        {
            Button watchGuideButton = CreateButton("Watch Rule of Thirds Guide", entryObject.transform, "WATCH IN-GAME VIDEO GUIDE");
            SetRect(watchGuideButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(190f, 43f), new Vector2(330f, 56f));
            watchGuideButton.onClick.AddListener(ShowRuleOfThirdsGuide);
        }
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
        messageText.color = new Color(0.40f, 0.32f, 0.23f);
    }

    private void BuildAlmanacUI()
    {
        BuildIllustratedBook();
    }

    private void BuildLegacyAlmanacUI()
    {
        if (almanacCanvas == null || playerInfoPanel != null) return;

        Canvas canvas = almanacCanvas.GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = 60;
        CanvasScaler bookScaler = almanacCanvas.GetComponent<CanvasScaler>();
        if (bookScaler != null)
        {
            bookScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            bookScaler.referenceResolution = new Vector2(1920, 1080);
            bookScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        RectTransform canvasRect = almanacCanvas.GetComponent<RectTransform>();
        canvasRect.localScale = Vector3.one;
        SetStretchRect(canvasRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject background = CreatePanel("Almanac Background", almanacCanvas.transform, backgroundColor);
        SetStretchRect(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject pageStack = CreatePanel("Page edges", background.transform, new Color32(195, 172, 132, 255));
        SetRect(pageStack.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(9, -10), new Vector2(1510, 870));
        pageStack.GetComponent<Image>().raycastTarget = false;
        GameObject mainPanel = CreatePanel("Almanac Book", background.transform, new Color32(248, 240, 220, 255));
        SetRect(mainPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 860f));

        GameObject header = CreatePanel("Header", mainPanel.transform, headerColor);
        SetStretchRect(header.GetComponent<RectTransform>(), new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -100f), Vector2.zero);

        TextMeshProUGUI titleText = CreateText("Title", header.transform, "THE PRODUCTION HANDBOOK", 36, TextAlignmentOptions.Left);
        SetStretchRect(titleText.rectTransform, Vector2.zero, Vector2.one, new Vector2(40f, 0f), new Vector2(-280f, 0f));
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;

        closeButton = CreateButton("Close Button", header.transform, "CLOSE  [P]");
        SetRect(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-145f, 0f), new Vector2(230f, 58f));
        ExportUIArt.Apply(closeButton.GetComponent<Image>(),"close");
        closeButton.GetComponent<RectTransform>().sizeDelta=new Vector2(58,58);
        closeButton.GetComponentInChildren<TextMeshProUGUI>().text="";
        var closeColors=closeButton.colors;closeColors.normalColor=Color.white;closeButton.colors=closeColors;

        GameObject sidePanel = CreatePanel("Tabs", mainPanel.transform, new Color32(228, 210, 178, 255));
        SetStretchRect(sidePanel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(300f, -100f));

        playerInfoTabBtn = CreateButton("Director Tab", sidePanel.transform, "01   DIRECTOR");
        SetRect(playerInfoTabBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(250f, 64f));

        knowledgeTabBtn = CreateButton("Knowledge Tab", sidePanel.transform, "02   FIELD GUIDE");
        SetRect(knowledgeTabBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(250f, 64f));

        achievementsTabBtn = CreateButton("Achievements Tab", sidePanel.transform, "03   MILESTONES");
        SetRect(achievementsTabBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(250f, 64f));

        GameObject spine = CreatePanel("Book binding", mainPanel.transform, new Color(0.43f, 0.28f, 0.15f));
        SetStretchRect(spine.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0f, 1f), new Vector2(302f, 0f), new Vector2(312f, -100f));
        spine.GetComponent<Image>().raycastTarget = false;
        var cover = mainPanel.AddComponent<Outline>();
        cover.effectColor = new Color(0.25f, 0.13f, 0.06f);
        cover.effectDistance = new Vector2(8f, -8f);
        TextMeshProUGUI note = CreateText("Handbook note", sidePanel.transform,
            "ON SET\n<size=75%>A director's reference</size>\n\n<size=80%>01  Plan the scene\n02  Shape the light\n03  Frame the story\n04  Refine the edit\n05  Deliver the film</size>", 28, TextAlignmentOptions.Left);
        SetStretchRect(note.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 40f), new Vector2(-20f, -360f));
        GameObject contentArea = new GameObject("Content Area", typeof(RectTransform));
        contentArea.transform.SetParent(mainPanel.transform, false);
        SetStretchRect(contentArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(330f, 35f), new Vector2(-35f, -125f));

        BuildPlayerInfoPanel(contentArea.transform);
        BuildKnowledgePanel(contentArea.transform);
        BuildAchievementsPanel(contentArea.transform);
        TextMeshProUGUI footer = CreateText("Book Footer", mainPanel.transform, "CREW ON SET     /     VIDEO PRODUCTION                                       FIELD EDITION    ·    SCROLL TO READ", 16, TextAlignmentOptions.Left);
        SetStretchRect(footer.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(345, 6), new Vector2(-35, 30));
    }

    private void BuildPlayerInfoPanel(Transform parent)
    {
        playerInfoPanel = CreatePanel("Director Panel", parent, new Color(0f, 0f, 0f, 0f));
        SetStretchRect(playerInfoPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        TextMeshProUGUI sectionTitle = CreateText("Section Title", playerInfoPanel.transform, "DIRECTOR'S RECORD", 36, TextAlignmentOptions.Left);
        SetStretchRect(sectionTitle.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(20f, -70f), new Vector2(-40f, -10f));
        sectionTitle.fontStyle = FontStyles.Bold;

        playerNameText = CreateText("Player Name", playerInfoPanel.transform, "", 30, TextAlignmentOptions.Left);
        RefreshDirectorName();
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
        BuildTechniqueGuidePanel();
    }

    private void BuildKnowledgeFilters()
    {
        if(bookEntryTitle!=null)return;
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

    private void BuildTechniqueGuidePanel()
    {
        if (knowledgePanel == null || techniqueGuidePanel != null) return;

        techniqueGuidePanel = CreatePanel("Technique Video Guide", knowledgePanel.transform, new Color(0.94f, 0.87f, 0.71f, 1f));
        SetStretchRect(techniqueGuidePanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        TextMeshProUGUI guideTitle = CreateText("Guide Title", techniqueGuidePanel.transform, "RULE OF THIRDS - IN-GAME VIDEO GUIDE", 32, TextAlignmentOptions.Left);
        SetStretchRect(guideTitle.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(25f, -70f), new Vector2(-220f, -15f));
        guideTitle.fontStyle = FontStyles.Bold;
        guideTitle.color = EditorWorkspaceUI.Ink;

        Button closeGuideButton = CreateButton("Close Guide Button", techniqueGuidePanel.transform, "BACK TO GUIDES");
        SetRect(closeGuideButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-125f, -42f), new Vector2(220f, 52f));


        GameObject videoFrame = CreatePanel("Video Frame", techniqueGuidePanel.transform, Color.black);
        SetRect(videoFrame.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -335f), new Vector2(930f, 520f));

        GameObject previewObject = new GameObject("In-Game Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        previewObject.transform.SetParent(videoFrame.transform, false);
        RawImage previewImage = previewObject.GetComponent<RawImage>();
        previewImage.color = Color.white;
        SetStretchRect(previewImage.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));

        CreateGuideGridLine("Left Vertical Third", previewImage.transform, new Vector2(1f / 3f, 0f), new Vector2(1f / 3f, 1f), new Vector2(3f, 0f));
        CreateGuideGridLine("Right Vertical Third", previewImage.transform, new Vector2(2f / 3f, 0f), new Vector2(2f / 3f, 1f), new Vector2(3f, 0f));
        CreateGuideGridLine("Upper Horizontal Third", previewImage.transform, new Vector2(0f, 2f / 3f), new Vector2(1f, 2f / 3f), new Vector2(0f, 3f));
        CreateGuideGridLine("Lower Horizontal Third", previewImage.transform, new Vector2(0f, 1f / 3f), new Vector2(1f, 1f / 3f), new Vector2(0f, 3f));

        GameObject captionPanel = CreatePanel("Guide Caption", videoFrame.transform, new Color(0f, 0f, 0f, 0.78f));
        SetStretchRect(captionPanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 8f), new Vector2(-8f, 92f));

        TextMeshProUGUI captionText = CreateText("Caption", captionPanel.transform, "", 21, TextAlignmentOptions.Center);
        captionText.color = Color.white;
        SetStretchRect(captionText.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 6f), new Vector2(-20f, -6f));

        TextMeshProUGUI videoLabel = CreateText("Video Label", videoFrame.transform, "IN-GAME CAMERA DEMONSTRATION", 17, TextAlignmentOptions.Left);
        videoLabel.color = Color.white;
        SetStretchRect(videoLabel.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(20f, -38f), new Vector2(-20f, -10f));
        videoLabel.fontStyle = FontStyles.Bold;

        Button playPauseButton = CreateButton("Play Pause Button", techniqueGuidePanel.transform, "PAUSE");
        SetRect(playPauseButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-130f, 42f), new Vector2(220f, 56f));

        Button replayButton = CreateButton("Replay Button", techniqueGuidePanel.transform, "REPLAY");
        SetRect(replayButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(130f, 42f), new Vector2(220f, 56f));

        ruleOfThirdsGuidePlayer = techniqueGuidePanel.AddComponent<AlmanacGuidePlayer>();
        ruleOfThirdsGuidePlayer.Initialize(previewImage, captionText, playPauseButton.GetComponentInChildren<TextMeshProUGUI>());


        techniqueGuidePanel.SetActive(false);
    }

    private void CreateGuideGridLine(string lineName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
    {
        GameObject lineObject = CreatePanel(lineName, parent, new Color(1f, 1f, 1f, 0.72f));
        RectTransform lineRect = lineObject.GetComponent<RectTransform>();
        lineRect.anchorMin = anchorMin;
        lineRect.anchorMax = anchorMax;
        lineRect.anchoredPosition = Vector2.zero;
        lineRect.sizeDelta = sizeDelta;
        lineObject.GetComponent<Image>().raycastTarget = false;
    }

    private void ShowRuleOfThirdsGuide()
    {
        if (techniqueGuidePanel == null || ruleOfThirdsGuidePlayer == null) return;

        techniqueGuidePanel.SetActive(true);
        techniqueGuidePanel.transform.SetAsLastSibling();
        ruleOfThirdsGuidePlayer.OpenGuide(FindRuleOfThirdsSubjectPrefab());
    }

    private void CloseTechniqueGuide()
    {
        if (ruleOfThirdsGuidePlayer != null) ruleOfThirdsGuidePlayer.CloseGuide();
        if (techniqueGuidePanel != null) techniqueGuidePanel.SetActive(false);
    }

    private GameObject FindRuleOfThirdsSubjectPrefab()
    {
        DirectorTerminal directorTerminal = FindObjectOfType<DirectorTerminal>(true);
        if (directorTerminal == null) return null;

        foreach (LevelPropBank propBank in directorTerminal.propDatabase)
        {
            foreach (GameObject propPrefab in propBank.allowedProps)
            {
                if (propPrefab != null && propPrefab.name.ToLower().Contains("goke")) return propPrefab;
            }
        }

        return null;
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
        GameObject scrollObject = CreatePanel(objectName, parent, new Color32(248, 240, 220, 255));
        SetStretchRect(scrollObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -90f));

        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 35f;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

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
        if(objectName=="Almanac Book"||objectName=="Tabs")ExportUIArt.Apply(panelObject.GetComponent<Image>(),"paper");
        if(objectName=="Header")ExportUIArt.Apply(panelObject.GetComponent<Image>(),"tab");
        return panelObject;
    }

    private Button CreateButton(string objectName, Transform parent, string buttonLabel)
    {
        GameObject buttonObject = CreatePanel(objectName, parent, buttonColor);
        buttonObject.GetComponent<Image>().color = Color.white;
        Button button = buttonObject.AddComponent<Button>();

        ColorBlock colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = new Color32(122, 84, 51, 255);
        colors.pressedColor = new Color32(88, 57, 36, 255);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color32(156, 112, 69, 255);
        colors.fadeDuration = .15f;
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.colors = colors;
        ExportUIArt.Apply(buttonObject.GetComponent<Image>(),"tab");

        TextMeshProUGUI buttonText = CreateText("Text", buttonObject.transform, buttonLabel, 22, TextAlignmentOptions.Center);
        SetStretchRect(buttonText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        buttonText.fontStyle = FontStyles.Bold;
        buttonText.color = Color.white;

        return button;
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI textComponent = textObject.GetComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.font = TMP_Settings.defaultFontAsset;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = EditorWorkspaceUI.Ink;
        textComponent.raycastTarget = false;
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





