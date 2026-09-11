using UnityEngine;
using UnityEngine.InputSystem;

// Session-only: no tutorial completion or grades are written by this switch.
[DefaultExecutionOrder(1000)]
public sealed class DevTutorialBypass : MonoBehaviour
{
    public static bool Disabled { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { Disabled = false; }
    public static void ResetForNewGame() { Disabled = false; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;
        if (FindObjectOfType<DevTutorialBypass>() != null) return;
        var host = new GameObject("Dev Tutorial Bypass");
        DontDestroyOnLoad(host);
        host.AddComponent<DevTutorialBypass>();
    }
    private void Update()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;
        if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
        {
            Disabled = true;
            GameFeedback.Show("CHEAT ACTIVATED\nTutorials disabled for this session");
            Debug.Log("DEV: Tutorials OFF for this session. Restart Play Mode/game to restore tutorials.");
        }
        Keyboard keyboard = Keyboard.current;
        CareerManager.HandleDevCheats(keyboard);
        if (keyboard != null && keyboard.f9Key.wasPressedThisFrame)
        {
            if (TutorialManager.Instance != null) TutorialManager.Instance.SpawnCheatSDCard();
            else GameFeedback.Show("CHEAT UNAVAILABLE\nEnter the studio to spawn a test SD card.", true);
        }
        if (!Disabled) return;
        if (TutorialManager.Instance != null && TutorialManager.Instance.enabled) TutorialManager.Instance.DisableForDevTesting();
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.enabled) GokeLevelManager.Instance.DisableForDevTesting();
        if (Level3Manager.Instance != null && Level3Manager.Instance.enabled) Level3Manager.Instance.DisableForDevTesting();
        if (CampaignLevelManager.Instance != null && CampaignLevelManager.Instance.enabled) CampaignLevelManager.Instance.DisableForDevTesting();
        if (EditorTutorialManager.Instance != null) EditorTutorialManager.Instance.DisableForDevTesting();
    }
}
