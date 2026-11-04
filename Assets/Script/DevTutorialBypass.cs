using UnityEngine;
using UnityEngine.InputSystem;

// Session-only: no tutorial completion or grades are written by this switch.
[DefaultExecutionOrder(1000)]
public sealed class DevTutorialBypass : MonoBehaviour
{
    private static bool developerDisabled;
    public static bool DeveloperDisabled=>developerDisabled;
    public static bool Disabled { get=>developerDisabled || ContractTutorialChoice.SkipThisContract; private set=>developerDisabled=value; }
    public static bool FastBossDialogue { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { Disabled = false; FastBossDialogue = false; ContractTutorialChoice.Reset(); }
    public static void ResetForNewGame() { ResetSession(); }

    // A test restart restores tutorial tasks, but keeps the developer's pacing preference.
    public static void ResetForCareerTesting() { Disabled = false; ContractTutorialChoice.Reset(); }

    public static bool PracticeDelayComplete(float elapsed, float duration)
    {
        return FastBossDialogue || elapsed >= duration;
    }

    public static System.Collections.IEnumerator WaitForPractice(float duration)
    {
        float elapsed = 0f;
        // Poll the flag so switching F4 on also releases a wait already in progress.
        while (!PracticeDelayComplete(elapsed, duration))
        {
            yield return null;
            elapsed += Time.deltaTime;
        }
    }

    public static void ToggleFastBossDialogue()
    {
        if (!DevCommandsPanel.CommandsAllowed) return;
        FastBossDialogue = !FastBossDialogue;
        if (FastBossDialogue && TutorialUIManager.Instance != null)
            TutorialUIManager.Instance.CompleteBossRevealForTesting();
        GameFeedback.Show(FastBossDialogue ? "FAST DIALOGUE ON\nInstant dialogue and practice waits; tasks remain active" :
            "FAST DIALOGUE OFF\nNormal Boss dialogue restored");
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (!DevCommandsPanel.Available) return;
        if (FindObjectOfType<DevTutorialBypass>() != null) return;
        var host = new GameObject("Dev Tutorial Bypass");
        DontDestroyOnLoad(host);
        host.AddComponent<DevTutorialBypass>();
    }
    private void Update()
    {
        if (!Application.isFocused) return;
        if (!DevCommandsPanel.CommandsAllowed) return;
        if (!developerDisabled) return;
        if (TutorialManager.Instance != null && TutorialManager.Instance.enabled) TutorialManager.Instance.DisableForDevTesting();
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.enabled) GokeLevelManager.Instance.DisableForDevTesting();
        if (Level3Manager.Instance != null && Level3Manager.Instance.enabled) Level3Manager.Instance.DisableForDevTesting();
        if (CampaignLevelManager.Instance != null && CampaignLevelManager.Instance.enabled) CampaignLevelManager.Instance.DisableForDevTesting();
        if (EditorTutorialManager.Instance != null) EditorTutorialManager.Instance.DisableForDevTesting();
    }
    public static void DisableTutorials()
    {
        if (!DevCommandsPanel.CommandsAllowed) return;
        Disabled=true;
        GameFeedback.Show("DEV: Tutorials disabled for this session");
    }

    public static void EnableTutorials()
    {
        if(!DevCommandsPanel.CommandsAllowed)return;
        Disabled=false;ContractTutorialChoice.Reset();
        int level=CampaignProgression.GetCurrentLevel();
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(GameSaveManager.IsTrainingRoom)
        {GameFeedback.Show("DEV: Tutorials enabled for contracts. Training remains free practice.");return;}
        if(scene.name=="SingleStudio" || scene.name=="MultiStudio")
        {
            // Recreate disabled lesson owners without resetting career data,
            // adding cheat funds, clearing the project or changing contracts.
            ContractTutorialChoice.PlayAfterReload(level);
            PauseManager.isPaused=false;Time.timeScale=1;AudioListener.pause=false;
            GameFeedback.Show("DEV: Restarting Contract "+level+" tutorial");
            LoadingScreenController.LoadScene("SingleStudio");
            return;
        }
        if(scene.name=="Editor")
        {
            ContractTutorialChoice.PlayInCurrentScene(level);
            var editor=EditorManager.Instance;
            if(level==1)EditorTutorialManager.RestartForDevTesting();
            else if(level==2 && editor!=null && editor.GetComponent<GokeIntroOutroLesson>()==null)editor.gameObject.AddComponent<GokeIntroOutroLesson>();
            else if(level==4 && editor!=null && editor.GetComponent<CoffeeStoryEditLesson>()==null)editor.gameObject.AddComponent<CoffeeStoryEditLesson>();
        }
        GameFeedback.Show("DEV: Tutorial guidance enabled");
    }
}
