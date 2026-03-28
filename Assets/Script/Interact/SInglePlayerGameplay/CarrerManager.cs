using UnityEngine;
using TMPro;

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

    public void UpdateMoneyUI()
    {
        if (moneyTextHUD != null)
        {
            moneyTextHUD.text = $"{playerMoney}";
        }
    }
}