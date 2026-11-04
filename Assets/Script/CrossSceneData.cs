using UnityEngine;

[System.Serializable]
public struct ProductionGrades
{
    public float preProductionScore;
    public float productionScore;
    public float postProductionScore;
    public string letterGrade;
    public string feedback;
    public int earnedBCoins;
}

public static class CrossSceneData
{
    // Holds the final calculated grades while changing scenes
    public static ProductionGrades finalGrades;
    public static int submittedLevel;
    public static bool resultApplied;
    public static bool submittedWithTutorial;
}

[System.Serializable]
public sealed class CommercialArchiveEntry
{
    public string id, submissionKey, date, feedback;
    public int level;
    public string grade;

    public static string DirectoryPath
    {
        get
        {
            var manager = GameSaveManager.Ensure();
            string owner = manager.Repository?.Owner ?? "guest";
            string key = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(owner)).Replace('/', '_').Replace('+','-');
            return System.IO.Path.Combine(Application.persistentDataPath, "FinishedCommercials", key);
        }
    }

    public static CommercialArchiveEntry pending;
    public void Save()
    {
        string dir = DirectoryPath;
        System.IO.Directory.CreateDirectory(dir);
        string path = System.IO.Path.Combine(dir,id+".json");
        System.IO.File.WriteAllText(path+".tmp", JsonUtility.ToJson(this));
        if (System.IO.File.Exists(path)) System.IO.File.Replace(path+".tmp",path,null);
        else System.IO.File.Move(path+".tmp",path);
    }
}
