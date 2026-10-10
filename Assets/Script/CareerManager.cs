using PlayerPrefs = GameSavePrefs;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class CareerManager : MonoBehaviour
{
    public static CareerManager Instance;

    [Header("Economy")]
    public int playerMoney = 0;
    public string currentActiveJob = "None";

    [Header("UI")]
    [Tooltip("Drag your Money Text UI element here")]
    public TextMeshProUGUI moneyTextHUD;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            // We just returned to the Studio! 
            // 1. Give the surviving manager the fresh UI connection from this new scene
            if (moneyTextHUD != null) { Instance.moneyTextHUD = moneyTextHUD; Instance.ConfigureGameplayHUD(); }

            // 2. Tell the surviving manager to pull the new money from the hard drive
            Instance.playerMoney = PlayerPrefs.GetInt("PlayerMoney", 0);

            // 3. Force the screen to update!
            Instance.UpdateMoneyUI();

            // 4. Destroy this duplicate so we don't have clones
            // This object may also host the pause/tutorial managers.
            Destroy(this);
        }
    }

    private void OnEnable()
    {
        // Awake is not called again after a Play Mode script/domain reload.
        if (Instance == null) Instance = this;
        if (Instance == this) UpdateMoneyUI();
    }

    private Image almanacHudImage;
    private Material lockedAlmanacMaterial;

    private void RefreshAlmanacAppearance()
    {
        if (almanacHudImage == null) return;
        bool locked = CampaignProgression.GetCurrentLevel() == 1 && PlayerPrefs.GetInt("AlmanacUnlocked", 0) == 0;
        if (locked && lockedAlmanacMaterial == null)
        {
            var shader = Resources.Load<Shader>("AlmanacLocked");
            if (shader != null) lockedAlmanacMaterial = new Material(shader);
        }
        almanacHudImage.material = locked ? lockedAlmanacMaterial : null;
    }




    private void Update()
    {
        if (Instance != this) return;
        RefreshAlmanacAppearance();
        if (playerMoney != Mathf.Max(0, PlayerPrefs.GetInt("PlayerMoney", 0))) UpdateMoneyUI();
    }

    private void Start()
    {
        ConfigureGameplayHUD();
        // --- FIX: ALWAYS LOAD MONEY FROM THE HARD DRIVE ON START ---
        playerMoney = PlayerPrefs.GetInt("PlayerMoney", 0);
        UpdateMoneyUI();
    }

    // Keep the book shortcut and balance in the same gameplay canvas so menus hide both.
    public void ConfigureGameplayHUD()
    {
        if (moneyTextHUD == null) return;
        var coins = moneyTextHUD.transform.parent as RectTransform;
        if (coins == null || coins.parent == null) return;
        if (profileHudButton == null && coins.parent.Find("Profile HUD") == null)
        {
        coins.anchorMin = coins.anchorMax = Vector2.one;
        coins.pivot = new Vector2(1,1);
        coins.anchoredPosition = new Vector2(-28,-144);
        }
        ConfigureProfileShortcut(coins.parent);
        // Retire any existing counter without changing campaign/day progression.
        var oldDayHud = coins.parent.Find("Day HUD");
        if (oldDayHud != null) oldDayHud.gameObject.SetActive(false);
        var existingBook = coins.parent.Find("Almanac HUD");
        if (existingBook != null)
        {
            almanacHudImage = existingBook.GetComponent<Image>();
            BindAlmanacShortcut(existingBook.GetComponent<Button>());
            RefreshAlmanacAppearance();
            return;
        }
        var root = new GameObject("Almanac HUD", typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(coins.parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0,1);
        rect.pivot = new Vector2(0,1);
        rect.anchoredPosition = new Vector2(28,-24);
        rect.sizeDelta = new Vector2(130,172);
        ExportUIArt.Apply(root.GetComponent<Image>(), "almanacHud");
        root.GetComponent<Image>().preserveAspect = true;
        almanacHudImage = root.GetComponent<Image>();
        if (Application.isPlaying) RefreshAlmanacAppearance();
        BindAlmanacShortcut(root.GetComponent<Button>());
        var caption = Instantiate(moneyTextHUD, rect);
        caption.name = "Almanac shortcut";
        caption.text = "P";
        caption.fontSize = 34;
        caption.fontStyle = FontStyles.Bold;
        caption.color = Color.white;
        caption.alignment = TextAlignmentOptions.Center;
        caption.raycastTarget = false;
        caption.rectTransform.anchorMin = caption.rectTransform.anchorMax = new Vector2(.5f,0);
        caption.rectTransform.pivot = new Vector2(.5f,0);
        caption.rectTransform.anchoredPosition = new Vector2(0,14);
        caption.rectTransform.sizeDelta = new Vector2(60,44);
    }

    [SerializeField] private Button profileHudButton;

    private void ConfigureProfileShortcut(Transform parent)
    {
        var existing = profileHudButton != null ? profileHudButton.transform : parent.Find("Profile HUD");
        if (existing != null)
        {
            profileHudButton = existing.GetComponent<Button>();
            AttachAvatarPortrait(existing);
            BindProfileShortcut();
            return;
        }
        var root = existing != null ? existing.gameObject : new GameObject("Profile HUD", typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-28, -24);
        rect.sizeDelta = new Vector2(94, 94);
        ExportUIArt.Apply(root.GetComponent<Image>(), "profileIcon");
        root.GetComponent<Image>().preserveAspect = true;
        AttachAvatarPortrait(root.transform);
        var button = root.GetComponent<Button>();
        profileHudButton = button;
        BindProfileShortcut();
        if (rect.Find("Profile shortcut") == null)
        {
            var caption = Instantiate(moneyTextHUD, rect);
            caption.name = "Profile shortcut";
            caption.text = "PROFILE [I]";
            caption.fontSize = 18;
            caption.enableAutoSizing = false;
            caption.alignment = TextAlignmentOptions.Center;
            caption.raycastTarget = false;
            ExportUIArt.OutlineText(caption);
            caption.rectTransform.anchorMin = caption.rectTransform.anchorMax = new Vector2(.5f, 0);
            caption.rectTransform.pivot = new Vector2(.5f, 1);
            caption.rectTransform.anchoredPosition = new Vector2(-8, -2);
            caption.rectTransform.sizeDelta = new Vector2(124, 22);
        }
    }

    private static void AttachAvatarPortrait(Transform shortcut) => AccountAvatarPortrait.Attach(shortcut);

    private void BindProfileShortcut()
    {
        if (profileHudButton == null || profileHudButton.onClick.GetPersistentEventCount() > 0) return;
        profileHudButton.onClick.RemoveListener(OpenProfile);
        profileHudButton.onClick.AddListener(OpenProfile);
    }

    public void OpenProfile()
    {
        if (AlmanacManager.Instance != null) AlmanacManager.Instance.OpenPlayerProfile();
    }
    private void BindAlmanacShortcut(Button button)
    {
        if (button == null) return;
        button.onClick.RemoveListener(OpenAlmanac);
        button.onClick.AddListener(OpenAlmanac);
    }
    private void OpenAlmanac()
    {
        var almanac = FindObjectOfType<AlmanacManager>();
        if (almanac != null) almanac.ToggleAlmanac();
    }
#if UNITY_EDITOR
    public void BakeHierarchyUI() { ConfigureGameplayHUD(); }
#endif

    public void AcceptJob(string jobName, int upfrontPayment)
    {
        currentActiveJob = jobName;
        string acceptedKey = CampaignProgression.GetAcceptedKey(CampaignProgression.GetCurrentLevel());
        if (PlayerPrefs.GetInt(acceptedKey, 0) == 1) return;
        PlayerAnalytics.Begin(CampaignProgression.GetCurrentLevel());
        PlayerAnalytics.TransactionMade(Mathf.Max(0, upfrontPayment), "Contract advance", jobName);
        PlayerPrefs.SetInt(acceptedKey, 1);

        // Save upfront payment to hard drive!
        playerMoney = (int)System.Math.Min(int.MaxValue, (long)Mathf.Max(0, PlayerPrefs.GetInt("PlayerMoney", 0)) + Mathf.Max(0, upfrontPayment));
        PlayerPrefs.SetInt("PlayerMoney", playerMoney);
        PlayerPrefs.Save();

        UpdateMoneyUI();
        Debug.Log($"Accepted {jobName}. Received {upfrontPayment} B coins upfront!");
        BudgetRetryPrompt.CaptureStart(true);
        GameSaveManager.Instance?.SaveBudgetCheckpoint();
    }

    public void CompleteActiveJob(int finalPayment)
    {
        // The ContractGrader already saved the money. We just need to sync up!
        playerMoney = PlayerPrefs.GetInt("PlayerMoney", 0);
        currentActiveJob = "None";

        UpdateMoneyUI();
    }

    public bool TrySpendMoney(int amount)
    {
        return TrySpendMoney(amount, "Other purchases", "Purchase");
    }

    public bool TrySpendMoney(int amount, string category, string item)
    {
        playerMoney = Mathf.Max(0, PlayerPrefs.GetInt("PlayerMoney", 0));
        if (amount < 0) return false;
        BudgetRetryPrompt.CaptureStart();
        if (playerMoney < amount)
        {
            PlayerAnalytics.PurchaseRejected();
            GameFeedback.Show($"INSUFFICIENT BALANCE\nNeed {amount:N0} B-Coins | Balance {playerMoney:N0} | Short {amount - playerMoney:N0}", true);
            UpdateMoneyUI();
            BudgetRetryPrompt.Show(amount, playerMoney);
            return false;
        }

        // A cart records its lines separately after the whole purchase is approved.
        if (category != "Cart") PlayerAnalytics.TransactionMade(-amount, category, item);
        else PlayerAnalytics.EnsureTracking();
        playerMoney -= amount;
        PlayerPrefs.SetInt("PlayerMoney", playerMoney);
        PlayerPrefs.Save();

        UpdateMoneyUI();
        GameFeedback.Show($"PURCHASE CONFIRMED  -{amount:N0} B-Coins\nBalance: {playerMoney:N0} B-Coins");
        GameSaveManager.Instance?.SaveCheckpoint();
        return true;
    }

    public void AddMoney(int amount)
    {
        AddMoney(amount, "Other income");
    }

    public void AddMoney(int amount, string source)
    {
        if (amount <= 0) return;
        if (source == "Training allowance") PlayerAnalytics.Begin(1);
        PlayerAnalytics.TransactionMade(amount, source, source);

        playerMoney = (int)System.Math.Min(int.MaxValue, (long)Mathf.Max(0, PlayerPrefs.GetInt("PlayerMoney", 0)) + amount);
        PlayerPrefs.SetInt("PlayerMoney", playerMoney);
        PlayerPrefs.Save();

        UpdateMoneyUI();
        GameSaveManager.Instance?.SaveCheckpoint();
    }

    public void UpdateMoneyUI()
    {
        playerMoney = Mathf.Max(0, PlayerPrefs.GetInt("PlayerMoney", 0));
        GameFeedback.RefreshBalance();
        if (moneyTextHUD != null)
        {
            moneyTextHUD.text = playerMoney.ToString("N0");
        }
    }

    // Compatibility entry point; old shortcut polling has moved to the F12 menu.
    public static void HandleDevCheats(Keyboard keyboard)
    {
    }
    public static void DevAddBudget()
    {
        if (DevCommandsPanel.CommandsAllowed)
        {
            if (Instance == null) Instance = FindObjectOfType<CareerManager>(true);
            if (Instance != null) Instance.AddMoney(1000, "Developer funds");
            else
            {
                int balance = (int)System.Math.Min(int.MaxValue, (long)Mathf.Max(0, PlayerPrefs.GetInt("PlayerMoney", 0)) + 1000);
                PlayerPrefs.SetInt("PlayerMoney", balance);
                PlayerPrefs.Save();
            }

            Debug.Log("DEV: Added 1,000 B-Coins. Balance: " + PlayerPrefs.GetInt("PlayerMoney", 0).ToString("N0"));
            GameFeedback.Show("CHEAT ACTIVATED: +1,000 B-Coins\nBalance: " + PlayerPrefs.GetInt("PlayerMoney", 0).ToString("N0") + " B-Coins");
        }

    }
    public static void DevRemoveBudget()
    {
        if (DevCommandsPanel.CommandsAllowed)
        {
            int previousBalance = Mathf.Max(0, PlayerPrefs.GetInt("PlayerMoney", 0));
            int removed = Mathf.Min(1000, previousBalance);
            int balance = previousBalance - removed;
            if (removed > 0) PlayerAnalytics.TransactionMade(-removed, "Developer funds", "Developer deduction");
            PlayerPrefs.SetInt("PlayerMoney", balance);
            PlayerPrefs.Save();
            if (Instance == null) Instance = FindObjectOfType<CareerManager>(true);
            if (Instance != null) Instance.UpdateMoneyUI();
            else GameFeedback.RefreshBalance();
            GameSaveManager.Instance?.SaveCheckpoint();
            Debug.Log("DEV: Removed " + removed + " B-Coins. Balance: " + balance.ToString("N0"));
            GameFeedback.Show("CHEAT ACTIVATED: -" + removed.ToString("N0") + " B-Coins\nBalance: " + balance.ToString("N0") + " B-Coins");
        }

    }
    public static void DevRestartCareer()
    {
        if (DevCommandsPanel.CommandsAllowed)
        {
            ResetCareerForTesting();
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            // Editor/review scenes require an existing take and cannot start an empty career.
            if (scene == "Editor" || scene == "ReviewScene") scene = "SingleStudio";
            if (Photon.Pun.PhotonNetwork.InRoom)
            {
                // The multiplayer leave callback returns to the menu after disconnecting the room.
                Photon.Pun.PhotonNetwork.LeaveRoom();
                return;
            }
            Cursor.lockState = scene == "SingleStudio" ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = scene != "SingleStudio";
            LoadingScreenController.LoadScene(scene);
        }
    }

    public static void ResetCareerForTesting()
    {
        if (!DevCommandsPanel.CommandsAllowed) return;
        foreach (TruePixelPlayer player in FindObjectsOfType<TruePixelPlayer>(true)) player.StopTape();
        if (ProjectDataManager.Instance != null) ProjectDataManager.Instance.ClearProject();
        CrossSceneData.finalGrades = default;
        CrossSceneData.submittedLevel = 0;
        CrossSceneData.resultApplied = false;
        DevTutorialBypass.ResetForCareerTesting();
        PauseManager.isPaused = false;
        Time.timeScale = 1f;
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        if (Instance != null)
        {
            Instance.playerMoney = 0;
            Instance.currentActiveJob = "None";
            Instance.UpdateMoneyUI();
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("DEV: Current career reset; fast dialogue " + (DevTutorialBypass.FastBossDialogue ? "ON" : "OFF"));
        GameFeedback.Show("DEV RESET ACTIVATED\nCareer restarted | Fast dialogue " + (DevTutorialBypass.FastBossDialogue ? "ON" : "OFF"));
    }

    public static void SwitchLevelCheat(int targetLevel)
    {
        if (!DevCommandsPanel.CommandsAllowed || targetLevel<1 || targetLevel>4) return;
        CampaignProgression.SetCheatLevel(targetLevel);
        // Restart the flower setup only for an explicit level-one cheat.
        if (targetLevel == 1)
        {
            PlayerPrefs.SetInt("Studio.StageCleared", 1);
            PlayerPrefs.SetInt("Studio.SelectedInterior", 0);
            PlayerPrefs.DeleteKey("Studio.WallColor");
        }
        else if (targetLevel <= 3)
        {
            // Coffee interiors are unavailable in levels 1-3; retain ownership for later.
            PlayerPrefs.SetInt("Studio.SelectedInterior", 0);
            PlayerPrefs.SetInt("OwnedInterior.0", 1);
            PlayerPrefs.SetInt("Studio.StageCleared", 0);
        }

        int minimumMoney = targetLevel == 1 ? 10000 : 20000;
        int savedMoney = PlayerPrefs.GetInt("PlayerMoney", 0);
        if (savedMoney < minimumMoney) PlayerPrefs.SetInt("PlayerMoney", minimumMoney);

        if (Instance != null)
        {
            Instance.playerMoney = PlayerPrefs.GetInt("PlayerMoney", minimumMoney);
            Instance.currentActiveJob = "None";
            Instance.UpdateMoneyUI();
        }

        if (ProjectDataManager.Instance != null) ProjectDataManager.Instance.ClearProject();

        CrossSceneData.finalGrades = new ProductionGrades();
        CrossSceneData.submittedLevel = 0;
        CrossSceneData.resultApplied = false;

        PauseManager.isPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        PlayerPrefs.Save();
        Debug.Log("<color=yellow>DEV LEVEL CHEAT: Loading Level " + targetLevel + "</color>");
        GameFeedback.Show("CHEAT ACTIVATED\nLoading Level " + targetLevel);
        if (targetLevel == 1) StudioArrivalTour.Queue();
        LoadingScreenController.LoadScene("SingleStudio");
    }

    private void OnDestroy()
    {
        if (lockedAlmanacMaterial != null) Destroy(lockedAlmanacMaterial);
        if (Instance == this) Instance = null;
    }
}


