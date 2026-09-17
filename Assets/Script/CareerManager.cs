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

    private TextMeshProUGUI dayTextHUD;
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
        if (dayTextHUD != null) dayTextHUD.text = "DAY " + CampaignProgression.GetCurrentLevel();
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
        coins.anchorMin = coins.anchorMax = Vector2.one;
        coins.pivot = new Vector2(1,1);
        coins.anchoredPosition = new Vector2(-28,-40);
        if (coins.parent.Find("Day HUD") == null)
        {
            dayTextHUD = Instantiate(moneyTextHUD, coins.parent);
            dayTextHUD.name = "Day HUD";
            dayTextHUD.text = "DAY " + CampaignProgression.GetCurrentLevel();
            dayTextHUD.fontSize = 36;
            dayTextHUD.fontStyle = FontStyles.Bold;
            dayTextHUD.alignment = TextAlignmentOptions.Center;
            dayTextHUD.raycastTarget = false;
            var dayRect = dayTextHUD.rectTransform;
            dayRect.anchorMin = dayRect.anchorMax = new Vector2(.5f,1);
            dayRect.pivot = new Vector2(.5f,1);
            dayRect.anchoredPosition = new Vector2(0,-32);
            dayRect.sizeDelta = new Vector2(260,60);
        }
        else dayTextHUD = coins.parent.Find("Day HUD").GetComponent<TextMeshProUGUI>();
        var existingBook = coins.parent.Find("Almanac HUD");
        if (existingBook != null)
        {
            almanacHudImage = existingBook.GetComponent<Image>();
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
        RefreshAlmanacAppearance();
        root.GetComponent<Button>().onClick.AddListener(() =>
        {
            var almanac = FindObjectOfType<AlmanacManager>();
            if (almanac != null) almanac.ToggleAlmanac();
        });
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

    public void AcceptJob(string jobName, int upfrontPayment)
    {
        currentActiveJob = jobName;
        string acceptedKey = CampaignProgression.GetAcceptedKey(CampaignProgression.GetCurrentLevel());
        if (PlayerPrefs.GetInt(acceptedKey, 0) == 1) return;
        PlayerPrefs.SetInt(acceptedKey, 1);

        // Save upfront payment to hard drive!
        playerMoney = (int)System.Math.Min(int.MaxValue, (long)Mathf.Max(0, PlayerPrefs.GetInt("PlayerMoney", 0)) + Mathf.Max(0, upfrontPayment));
        PlayerPrefs.SetInt("PlayerMoney", playerMoney);
        PlayerPrefs.Save();

        UpdateMoneyUI();
        Debug.Log($"Accepted {jobName}. Received {upfrontPayment} B coins upfront!");
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
        playerMoney = Mathf.Max(0, PlayerPrefs.GetInt("PlayerMoney", 0));
        if (amount < 0) return false;
        if (playerMoney < amount)
        {
            GameFeedback.Show($"INSUFFICIENT BALANCE\nNeed {amount:N0} B-Coins | Balance {playerMoney:N0} | Short {amount - playerMoney:N0}", true);
            UpdateMoneyUI();
            return false;
        }

        playerMoney -= amount;
        PlayerPrefs.SetInt("PlayerMoney", playerMoney);
        PlayerPrefs.Save();

        UpdateMoneyUI();
        GameFeedback.Show($"PURCHASE CONFIRMED  -{amount:N0} B-Coins\nBalance: {playerMoney:N0} B-Coins");
        return true;
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;

        playerMoney = (int)System.Math.Min(int.MaxValue, (long)Mathf.Max(0, PlayerPrefs.GetInt("PlayerMoney", 0)) + amount);
        PlayerPrefs.SetInt("PlayerMoney", playerMoney);
        PlayerPrefs.Save();

        UpdateMoneyUI();
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

    private static int lastCheatInputFrame = -1;

    public static void HandleDevCheats(Keyboard keyboard)
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;
        if (keyboard == null || !Application.isFocused || lastCheatInputFrame == Time.frameCount) return;
        lastCheatInputFrame = Time.frameCount;

        // --- SECRET DEVELOPER CHEAT CODES ---

        bool capsLockHeld = keyboard.capsLockKey.isPressed;
        if (capsLockHeld)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) { SwitchLevelCheat(1); return; }
            if (keyboard.digit2Key.wasPressedThisFrame) { SwitchLevelCheat(2); return; }
            if (keyboard.digit3Key.wasPressedThisFrame) { SwitchLevelCheat(3); return; }
            if (keyboard.digit4Key.wasPressedThisFrame) { SwitchLevelCheat(4); return; }
        }

        // Press F10 to instantly add 1000 B-Coins
        if (keyboard.f10Key.wasPressedThisFrame)
        {
            if (Instance == null) Instance = FindObjectOfType<CareerManager>(true);
            if (Instance != null) Instance.AddMoney(1000);
            else
            {
                int balance = (int)System.Math.Min(int.MaxValue, (long)Mathf.Max(0, PlayerPrefs.GetInt("PlayerMoney", 0)) + 1000);
                PlayerPrefs.SetInt("PlayerMoney", balance);
                PlayerPrefs.Save();
            }

            Debug.Log("DEV F10: Added 1,000 B-Coins. Balance: " + PlayerPrefs.GetInt("PlayerMoney", 0).ToString("N0"));
            GameFeedback.Show("CHEAT ACTIVATED: +1,000 B-Coins\nBalance: " + PlayerPrefs.GetInt("PlayerMoney", 0).ToString("N0") + " B-Coins");
        }

        // F12 resets only the active career; account identity and other saves are kept.
        if (keyboard.f12Key.wasPressedThisFrame)
        {
            ResetCareerForTesting();
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            // Editor/review scenes require an existing take and cannot start an empty career.
            if (scene == "Editor" || scene == "ReviewScene") scene = "SingleStudio";
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
        }
    }

    public static void ResetCareerForTesting()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;
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
        Debug.Log("DEV F12: Current career reset; fast dialogue " + (DevTutorialBypass.FastBossDialogue ? "ON" : "OFF"));
        GameFeedback.Show("DEV RESET ACTIVATED\nCareer restarted | Fast dialogue " + (DevTutorialBypass.FastBossDialogue ? "ON" : "OFF"));
    }

    private static void SwitchLevelCheat(int targetLevel)
    {
        CampaignProgression.SetCheatLevel(targetLevel);

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
        UnityEngine.SceneManagement.SceneManager.LoadScene("SingleStudio");
    }

    private void OnDestroy()
    {
        if (lockedAlmanacMaterial != null) Destroy(lockedAlmanacMaterial);
        if (Instance == this) Instance = null;
    }
}
