using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

[System.Serializable]
public class KnowledgeEntry
{
    public string id; // e.g., "lighting_101"
    public string title;
    [TextArea(3, 5)] public string description;
    public bool isUnlocked = false;
}

[System.Serializable]
public class AchievementEntry
{
    public string id; // e.g., "first_s_grade"
    public string title;
    public string description;
    public int currentProgress;
    public int maxProgress; // e.g., 1000 for "Earn 1000 Coins"
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

    [Header("Knowledge Base UI")]
    public Transform knowledgeListContainer;
    public GameObject knowledgeEntryPrefab; // A UI prefab with Title and Description text
    public List<KnowledgeEntry> database = new List<KnowledgeEntry>();

    [Header("Achievements UI")]
    public Transform achievementListContainer;
    public GameObject achievementEntryPrefab; // A UI prefab with Title, Desc, and Progress text
    public List<AchievementEntry> achievements = new List<AchievementEntry>();

    private bool isAlmanacOpen = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (almanacCanvas != null) almanacCanvas.SetActive(false);
        LoadAlmanacData();

        // Setup Tab Buttons
        if (playerInfoTabBtn) playerInfoTabBtn.onClick.AddListener(() => OpenTab(0));
        if (knowledgeTabBtn) knowledgeTabBtn.onClick.AddListener(() => OpenTab(1));
        if (achievementsTabBtn) achievementsTabBtn.onClick.AddListener(() => OpenTab(2));
    }

    private void Update()
    {
        // --- THE FIX: Changed to P to open the Almanac ---
        if (Input.GetKeyDown(KeyCode.P))
        {
            ToggleAlmanac();
        }
    }

    public void ToggleAlmanac()
    {
        isAlmanacOpen = !isAlmanacOpen;
        if (almanacCanvas != null) almanacCanvas.SetActive(isAlmanacOpen);

        if (isAlmanacOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshAllUI();
            OpenTab(0); // Default to Player Info
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OpenTab(int tabIndex)
    {
        playerInfoPanel.SetActive(tabIndex == 0);
        knowledgePanel.SetActive(tabIndex == 1);
        achievementsPanel.SetActive(tabIndex == 2);
    }

    // ==========================================
    // --- SAVING & LOADING ---
    // ==========================================
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

    // ==========================================
    // --- UNLOCK SYSTEMS (CALL THESE FROM OTHER SCRIPTS!) ---
    // ==========================================

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

    // ==========================================
    // --- UI GENERATION ---
    // ==========================================
    private void RefreshAllUI()
    {
        // 1. Player Info Tab
        if (playerNameText != null) playerNameText.text = "Director: " + PlayerPrefs.GetString("PlayerName", "Guest");

        if (CareerManager.Instance != null && playerMoneyText != null)
            playerMoneyText.text = "Bank: " + CareerManager.Instance.playerMoney + " B-Coins";

        int jobsDone = PlayerPrefs.GetInt("TotalJobsCompleted", 0);
        if (totalJobsText != null) totalJobsText.text = "Commercials Completed: " + jobsDone;

        // 2. Knowledge Tab
        foreach (Transform child in knowledgeListContainer) Destroy(child.gameObject);
        foreach (var entry in database)
        {
            if (entry.isUnlocked && knowledgeEntryPrefab != null)
            {
                GameObject uiObj = Instantiate(knowledgeEntryPrefab, knowledgeListContainer);
                TextMeshProUGUI[] texts = uiObj.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 2)
                {
                    texts[0].text = entry.title;
                    texts[1].text = entry.description;
                }
            }
        }

        // 3. Achievements Tab
        foreach (Transform child in achievementListContainer) Destroy(child.gameObject);
        foreach (var ach in achievements)
        {
            if (achievementEntryPrefab != null)
            {
                GameObject uiObj = Instantiate(achievementEntryPrefab, achievementListContainer);
                TextMeshProUGUI[] texts = uiObj.GetComponentsInChildren<TextMeshProUGUI>();

                // Dim the color if it's not finished
                if (!ach.isUnlocked) uiObj.GetComponent<CanvasGroup>().alpha = 0.5f;

                if (texts.Length >= 3)
                {
                    texts[0].text = ach.title;
                    texts[1].text = ach.description;
                    texts[2].text = ach.isUnlocked ? "COMPLETED" : $"{ach.currentProgress} / {ach.maxProgress}";
                }
            }
        }
    }
}