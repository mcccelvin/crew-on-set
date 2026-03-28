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

    // This list now carries the scores AND the filenames across scenes
    public List<FootageData> compiledFootage = new List<FootageData>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ClearProject()
    {
        compiledFootage.Clear();
    }
}