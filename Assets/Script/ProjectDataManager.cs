using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class FootageData
{
    public string fileName;
    public float camScore;
    public float lightScore;
    public int campaignLevel;
    public int shotType;
    public float screenDirection;
    public string actorPose;
    public bool requiredSubjectsVisible;
    public bool usedSoftLight;
    public bool hasThreePointRoles;
}

public class ProjectDataManager : MonoBehaviour
{
    public static ProjectDataManager Instance;

    public List<FootageData> compiledFootage = new List<FootageData>();

    // --- NEW: Smuggle the Stage Data ---
    public float savedPreProdScore = 100f;
    public string savedPreProdFeedback = "";
    public bool savedRequiredSetupMet = true;

    private void Awake()
    {
        // 1. Check if a different instance already exists
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Found a duplicate ProjectDataManager! Destroying the clone.");
            Destroy(gameObject);
            return; // Stop running code for this duplicate
        }
        Debug.Log("ProjectDataManager instance is set up and ready to go!");
        // 2. Claim the instance
        Instance = this;

        // 3. FORCE this object to be a root object so DontDestroyOnLoad doesn't break
        transform.SetParent(null);

        // 4. Protect it from scene loads
        DontDestroyOnLoad(gameObject);
    }

    public void ClearProject()
    {
        compiledFootage.Clear();
        savedPreProdScore = 100f;
        savedPreProdFeedback = "";
        savedRequiredSetupMet = true;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
