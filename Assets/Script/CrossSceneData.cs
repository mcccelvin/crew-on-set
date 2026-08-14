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
}
