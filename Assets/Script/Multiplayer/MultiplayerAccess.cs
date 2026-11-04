public static class MultiplayerAccess
{
    public static bool RequireTutorial()
    {
        var saves=GameSaveManager.Ensure();
        if(saves.HasCompletedSoloTutorial())return true;
        GameFeedback.Show(saves.Syncing ? "Your saved tutorial progress is syncing. Try multiplayer again shortly." :
            "Finish the singleplayer tutorial contract first to unlock multiplayer.",GameFeedback.NoticeType.Caution);
        return false;
    }
}
