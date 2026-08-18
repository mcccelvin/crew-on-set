using UnityEngine;
using TMPro;
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
            Instance.moneyTextHUD = this.moneyTextHUD;

            // 2. Tell the surviving manager to pull the new money from the hard drive
            Instance.playerMoney = PlayerPrefs.GetInt("PlayerMoney", 0);

            // 3. Force the screen to update!
            Instance.UpdateMoneyUI();

            // 4. Destroy this duplicate so we don't have clones
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // --- FIX: ALWAYS LOAD MONEY FROM THE HARD DRIVE ON START ---
        playerMoney = PlayerPrefs.GetInt("PlayerMoney", 0);
        UpdateMoneyUI();
    }

    public void AcceptJob(string jobName, int upfrontPayment)
    {
        currentActiveJob = jobName;

        // Save upfront payment to hard drive!
        playerMoney = PlayerPrefs.GetInt("PlayerMoney", 0) + upfrontPayment;
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
        if (amount < 0 || playerMoney < amount) return false;

        playerMoney -= amount;
        PlayerPrefs.SetInt("PlayerMoney", playerMoney);
        PlayerPrefs.Save();

        UpdateMoneyUI();
        return true;
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;

        playerMoney += amount;
        PlayerPrefs.SetInt("PlayerMoney", playerMoney);
        PlayerPrefs.Save();

        UpdateMoneyUI();
    }

    public void UpdateMoneyUI()
    {
        if (moneyTextHUD != null)
        {
            moneyTextHUD.text = $"{playerMoney}";
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // --- SECRET DEVELOPER CHEAT CODES ---

        bool capsLockHeld = keyboard.capsLockKey.isPressed;
        if (capsLockHeld)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) { SwitchLevelCheat(1); return; }
            if (keyboard.digit2Key.wasPressedThisFrame) { SwitchLevelCheat(2); return; }
            if (keyboard.digit3Key.wasPressedThisFrame) { SwitchLevelCheat(3); return; }
        }

        // Press F10 to instantly add 1000 B-Coins
        if (keyboard.f10Key.wasPressedThisFrame)
        {
            AddMoney(1000);

            Debug.Log("[Cheat] Added 1000 B-Coins! Don't tell the boss.");
        }

        // Press F12 to wipe all save data and reset money to 0!
        if (keyboard.f12Key.wasPressedThisFrame)
        {
            Debug.Log("<color=red>DEV COMMAND: WIPING ALL SAVE DATA!</color>");

            // 1. Erase all PlayerPrefs (Tutorial progress, saved money, etc.)
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            // 2. Reset the live money variable
            if (CareerManager.Instance != null)
            {
                CareerManager.Instance.playerMoney = 0;
                CareerManager.Instance.UpdateMoneyUI();
            }

            // 3. (Optional) Reload the scene to start fresh instantly
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }

    private void SwitchLevelCheat(int targetLevel)
    {
        CampaignProgression.SetCheatLevel(targetLevel);

        int minimumMoney = targetLevel == 1 ? 10000 : 20000;
        int savedMoney = PlayerPrefs.GetInt("PlayerMoney", 0);
        if (savedMoney < minimumMoney) PlayerPrefs.SetInt("PlayerMoney", minimumMoney);

        playerMoney = PlayerPrefs.GetInt("PlayerMoney", minimumMoney);
        currentActiveJob = "None";
        UpdateMoneyUI();

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
        UnityEngine.SceneManagement.SceneManager.LoadScene("SingleStudio");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
