using UnityEngine;

public static class CampaignProgression
{
    public const int MinimumLevel = 1;
    public const int MaximumLevel = 5;
    private const string levelCheatOverrideKey = "CampaignLevelCheatOverride";
    private const string levelCheatIntroductionKey = "CampaignLevelCheatIntroduction";

    public static int GetCurrentLevel()
    {
        int cheatLevel = PlayerPrefs.GetInt(levelCheatOverrideKey, 0);
        if (cheatLevel >= MinimumLevel && cheatLevel <= MaximumLevel)
        {
            PlayerPrefs.SetInt("CurrentLevel", cheatLevel);
            return cheatLevel;
        }

        int currentLevel = PlayerPrefs.GetInt("CurrentLevel", 0);
        int tutorialProgress = PlayerPrefs.GetInt("TutorialProgress", 0);

        if (currentLevel <= 0)
        {
            currentLevel = tutorialProgress >= 2 ? Mathf.Clamp(tutorialProgress, 2, MaximumLevel) : MinimumLevel;
        }

        if (tutorialProgress >= 2 && tutorialProgress > currentLevel)
        {
            currentLevel = Mathf.Clamp(tutorialProgress, 2, MaximumLevel);
        }

        if (currentLevel == 2 && PlayerPrefs.GetInt("GokeContractGraded", 0) == 1) currentLevel = 3;
        if (currentLevel == 3 && PlayerPrefs.GetInt("LamborminiContractGraded", 0) == 1) currentLevel = 4;
        if (currentLevel == 4 && PlayerPrefs.GetInt("KapeKulturaContractGraded", 0) == 1) currentLevel = 5;

        currentLevel = Mathf.Clamp(currentLevel, MinimumLevel, MaximumLevel);
        PlayerPrefs.SetInt("CurrentLevel", currentLevel);
        return currentLevel;
    }

    public static void SetCurrentLevel(int level)
    {
        int currentLevel = Mathf.Clamp(level, MinimumLevel, MaximumLevel);
        PlayerPrefs.SetInt("CurrentLevel", currentLevel);

        if (currentLevel >= 2) PlayerPrefs.SetInt("TutorialProgress", currentLevel);
        PlayerPrefs.Save();
    }

    public static void SetCheatLevel(int level)
    {
        int cheatLevel = Mathf.Clamp(level, MinimumLevel, MaximumLevel);
        PlayerPrefs.SetInt(levelCheatOverrideKey, cheatLevel);
        PlayerPrefs.SetInt("CurrentLevel", cheatLevel);
        PlayerPrefs.SetInt("TutorialProgress", cheatLevel >= 2 ? cheatLevel : 0);
        PlayerPrefs.DeleteKey("Level1RetryActive");

        if (cheatLevel >= 2) PlayerPrefs.SetInt(levelCheatIntroductionKey, cheatLevel);
        else PlayerPrefs.DeleteKey(levelCheatIntroductionKey);

        PlayerPrefs.Save();
    }

    public static bool ConsumeCheatIntroduction(int level)
    {
        int currentLevel = Mathf.Clamp(level, MinimumLevel, MaximumLevel);
        if (PlayerPrefs.GetInt(levelCheatIntroductionKey, 0) != currentLevel) return false;

        PlayerPrefs.DeleteKey(levelCheatIntroductionKey);
        PlayerPrefs.Save();
        return true;
    }

    public static string GetContractName(int level)
    {
        if (level == 1) return "Crystal Blooms - Artisan Flower Vase";
        if (level == 2) return "Goke Cola";
        if (level == 3) return "Lambormini";
        if (level == 4) return "Kape Kultura";
        return "Haraya Campaign";
    }

    public static string GetAcceptedKey(int level)
    {
        if (level == 1) return "FlowerContractAccepted";
        if (level == 2) return "GokeContractAccepted";
        if (level == 3) return "LamborminiContractAccepted";
        if (level == 4) return "KapeKulturaContractAccepted";
        return "HarayaContractAccepted";
    }

    public static string GetGradedKey(int level)
    {
        if (level == 1) return "FlowerContractGraded";
        if (level == 2) return "GokeContractGraded";
        if (level == 3) return "LamborminiContractGraded";
        if (level == 4) return "KapeKulturaContractGraded";
        return "HarayaContractGraded";
    }

    public static string GetRewardKey(int level)
    {
        return "ContractRewardClaimed_Level" + level;
    }

    public static void CompleteLevel(int completedLevel)
    {
        int level = Mathf.Clamp(completedLevel, MinimumLevel, MaximumLevel);
        PlayerPrefs.DeleteKey(levelCheatOverrideKey);
        PlayerPrefs.DeleteKey(levelCheatIntroductionKey);
        string gradedKey = GetGradedKey(level);
        bool isFirstCompletion = PlayerPrefs.GetInt(gradedKey, 0) == 0;

        PlayerPrefs.SetInt(gradedKey, 1);

        if (isFirstCompletion)
        {
            int completedJobs = PlayerPrefs.GetInt("TotalJobsCompleted", 0);
            PlayerPrefs.SetInt("TotalJobsCompleted", completedJobs + 1);
        }

        if (level == 1)
        {
            PlayerPrefs.DeleteKey("Level1RetryActive");
            PlayerPrefs.SetInt("TutorialProgress", 1);
            PlayerPrefs.SetInt("CurrentLevel", 1);
        }
        else if (level < MaximumLevel)
        {
            PlayerPrefs.SetInt("CurrentLevel", level + 1);
            PlayerPrefs.SetInt("TutorialProgress", level + 1);
        }
        else
        {
            PlayerPrefs.SetInt("CampaignCompleted", 1);
            PlayerPrefs.SetInt("CurrentLevel", MaximumLevel);
            PlayerPrefs.SetInt("TutorialProgress", MaximumLevel);
        }

        PlayerPrefs.Save();
    }
}
