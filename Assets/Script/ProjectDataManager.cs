using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class FootageData
{
    public string fileName;
    public float camScore;
    public float lightScore;
}

public class ProjectDataManager : MonoBehaviour
{
    public static ProjectDataManager Instance;

    public List<FootageData> compiledFootage = new List<FootageData>();

    // --- NEW: Smuggle the Stage Data ---
    public float savedPreProdScore = 100f;
    public string savedPreProdFeedback = "";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    public void ClearProject()
    {
        compiledFootage.Clear();
        savedPreProdScore = 100f;
        savedPreProdFeedback = "";
    }
}