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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateInstance()
    {
        if (Instance != null) return;

        GameObject projectDataObject = new GameObject("ProjectDataManager");
        projectDataObject.AddComponent<ProjectDataManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        Debug.Log("ProjectDataManager instance is set up and ready to go!");
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
