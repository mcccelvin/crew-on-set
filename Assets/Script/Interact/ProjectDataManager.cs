using UnityEngine;
using System.Collections.Generic;

public class ProjectDataManager : MonoBehaviour
{
    public static ProjectDataManager Instance;

    [Header("Studio Data (Brought to Editor)")]
    // Just the file paths! Grading will be handled later.
    public List<string> rawFootagePaths = new List<string>();

    [Header("Editor Data (The Final Commercial)")]
    public List<string> timelineClips = new List<string>();
    public string commercialOverlayText = "";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Survives the scene load!
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ClearProject()
    {
        rawFootagePaths.Clear();
        timelineClips.Clear();
        commercialOverlayText = "";
    }
}