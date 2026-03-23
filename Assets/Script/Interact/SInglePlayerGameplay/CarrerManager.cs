using UnityEngine;
using TMPro; // --- NEW: Needed to talk to the UI Text! ---

public class CareerManager : MonoBehaviour
{
    public static CareerManager Instance;

    [Header("Economy")]
    public int playerMoney = 0; // In B coins
    public string currentActiveJob = "None";

    [Header("UI")]
    [Tooltip("Drag your Money Text UI element here")]
    public TextMeshProUGUI moneyTextHUD; // --- NEW: The link to your screen ---

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Update the screen the moment the game starts so it doesn't say "New Text"
        UpdateMoneyUI();
    }

    public void AcceptJob(string jobName, int upfrontPayment)
    {
        currentActiveJob = jobName;
        playerMoney += upfrontPayment;
        UpdateMoneyUI(); // --- NEW: Refresh the screen! ---
        Debug.Log($"Accepted {jobName}. Received {upfrontPayment} B coins upfront!");
    }

    public void CompleteActiveJob(int finalPayment)
    {
        playerMoney += finalPayment;
        currentActiveJob = "None";
        UpdateMoneyUI(); // --- NEW: Refresh the screen! ---
        Debug.Log($"Job Complete! Received final {finalPayment} B coins.");
    }

    // --- NEW: The method that actually changes the text on screen ---
    public void UpdateMoneyUI()
    {
        if (moneyTextHUD != null)
        {
            // You can format this however you like! 
            moneyTextHUD.text = $"{playerMoney} B";
        }
    }
}