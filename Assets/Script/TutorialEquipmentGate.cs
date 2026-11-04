using UnityEngine;
using Player.Equipment;

// Guard player-initiated actions before inventory or equipment state changes.
public static class TutorialEquipmentGate
{
    private static float nextReminder;
    public static bool Allow(string action, Equipment item = null, bool notify = true)
    {
        if (DevTutorialBypass.Disabled || GameSaveManager.IsTrainingRoom) return true;
        bool allowed = true;
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.ShowTutorialProgress)
            allowed = GokeLevelManager.Instance.AllowsEquipmentAction(action, item);
        else if (Level3Manager.Instance != null && (Level3Manager.Instance.ShowTutorialProgress || Level3Manager.Instance.RecordingBlockedByPractice))
            allowed = Level3Manager.Instance.AllowsEquipmentAction(action, item);
        else if (CampaignLevelManager.Instance != null && CampaignLevelManager.Instance.IsContract4PracticeActive)
        {
            var lesson = CampaignLevelManager.Instance;
            allowed = action == "pickup" || action == "switch"
                ? item is ActorMegaphoneItem && lesson.CanUseContract4PracticeAction("megaphone.pickup")
                : action == "shop" ? lesson.CanUseContract4PracticeAction("shop.purchase")
                : action == "director" ? lesson.CanUseContract4PracticeAction("tablet.open")
                : action == "use" && item is ActorMegaphoneItem;
        }
        else if (TutorialManager.Instance != null)
            allowed = CampaignLevelManager.Instance != null && CampaignLevelManager.Instance.RestrictsTutorialEquipment
                ? action == "director" && CampaignLevelManager.Instance.IsActorIntroductionActive()
                : TutorialManager.Instance.AllowsEquipmentAction(action, item);
        if (!allowed && notify) Remind();
        return allowed;
    }

    public static void Remind()
    {
        if (Time.unscaledTime < nextReminder) return;
        nextReminder = Time.unscaledTime + 1.5f;
        // A toast does not replace the boss dialogue or reset lesson progress.
        GameFeedback.ShowBriefWarning("FOCUS ON THE CURRENT TASK\nFollow the boss's instruction and the task checklist before doing something else.");
    }
}
