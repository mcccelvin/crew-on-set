using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using TMPro; // We need this to talk to the text component!

public class CutsceneController : MonoBehaviour
{
    [Header("Cutscene Settings")]
    public VideoPlayer videoPlayer;     

    [Header("Skip Prompt Settings")]
    [Tooltip("Drag your TextMeshPro text here!")]
    public TextMeshProUGUI skipPromptText;
    public float blinkSpeed = 0.5f;

    private float playTimer = 0f;
    private bool canSkip = false;

    // Blink trackers
    private float blinkTimer = 0f;
    private bool isTextWhite = true;

    private void Start()
    {
        // Hide the mouse cursor so it doesn't distract from the movie!
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Make sure the skip text is hidden when the video starts!
        if (skipPromptText != null) skipPromptText.gameObject.SetActive(false);

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += EndCutscene;
        }
    }

    private void Update()
    {
        // Count up the master timer while the cutscene plays
        playTimer += Time.deltaTime;

        // Unlock the ability to skip after 3 seconds
        if (playTimer >= 3f && !canSkip)
        {
            canSkip = true;
            // Show the text!
            if (skipPromptText != null) skipPromptText.gameObject.SetActive(true);
        }

        // --- NEW: The Blinking Logic ---
        // Only blink if we are allowed to skip and the text exists
        if (canSkip && skipPromptText != null)
        {
            blinkTimer += Time.deltaTime;
            if (blinkTimer >= blinkSpeed)
            {
                blinkTimer = 0f; // Reset the blink timer
                isTextWhite = !isTextWhite; // Flip the color state

                // Apply the color to the text!
                skipPromptText.color = isTextWhite ? Color.white : Color.black;
            }
        }

        // If allowed to skip AND the player presses Spacebar, skip the scene!
        if (canSkip && Input.GetKeyDown(KeyCode.Space))
        {
            EndCutscene(videoPlayer);
        }
    }

    // You can keep this here just in case you ever want a UI button again
    public void SkipCutscene()
    {
        if (canSkip)
        {
            EndCutscene(videoPlayer);
        }
    }

    private void EndCutscene(VideoPlayer vp)
    {

        SceneManager.LoadScene(5);
    }

    private void OnDestroy()
    {
        if (videoPlayer != null) videoPlayer.loopPointReached -= EndCutscene;
    }
}
