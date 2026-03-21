using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class CutsceneController : MonoBehaviour
{
    [Header("Cutscene Settings")]
    public VideoPlayer videoPlayer;

    [Tooltip("Type the exact name of your main game scene here!")]
    public string nextSceneName = "Studio";

    private void Start()
    {
        // Unlock the mouse for the skip button
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += EndCutscene;
        }
    }

    // Link this to your Skip Button's OnClick event!
    public void SkipCutscene()
    {
        EndCutscene(videoPlayer);
    }

    private void EndCutscene(VideoPlayer vp)
    {
        // THE FIX: Dump the video and load the actual game scene!
        SceneManager.LoadScene(nextSceneName);
    }
}