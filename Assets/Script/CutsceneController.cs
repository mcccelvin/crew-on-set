using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using TMPro; // We need this to talk to the text component!
using UnityEngine.InputSystem;

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
    private bool ending;

    private float cursorIdleTimer;

    private void Start()
    {
        // Hide the mouse cursor so it doesn't distract from the movie!
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (skipPromptText != null)
        {
            var rect = skipPromptText.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-32f, 24f);
            rect.sizeDelta = new Vector2(460f, 48f);
            skipPromptText.enableAutoSizing = false;
            skipPromptText.fontSize = 22f;
            skipPromptText.alignment = TextAlignmentOptions.MidlineRight;
            skipPromptText.raycastTarget = false;
            skipPromptText.alpha = 0f;
            skipPromptText.gameObject.SetActive(true);
        }

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += EndCutscene;
        }
    }

    private void Update()
    {
        // Count up the master timer while the cutscene plays
        playTimer += Time.unscaledDeltaTime;
        // Delta still detects physical mouse movement while the cursor is locked.
        bool cursorMoved = Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > .01f;
        cursorIdleTimer = cursorMoved ? 0f : cursorIdleTimer + Time.unscaledDeltaTime;

        // Unlock the ability to skip after 3 seconds
        if (playTimer >= 3f && !canSkip)
        {
            canSkip = true;
        }

        // A quiet reminder after five idle seconds, without distracting blinking.
        if (skipPromptText != null)
        {
            float targetAlpha = canSkip && cursorIdleTimer >= 5f ? 1f : 0f;
            skipPromptText.alpha = cursorMoved ? 0f : Mathf.MoveTowards(skipPromptText.alpha, targetAlpha, Time.unscaledDeltaTime * 3f);
        }

        // If allowed to skip AND the player presses Spacebar, skip the scene!
        Keyboard keyboard = Keyboard.current;
        if (canSkip && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
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
        if (ending) return;
        ending = true;
        StudioArrivalTour.Queue();
        LoadingScreenController.LoadScene(5);
    }

    private void OnDestroy()
    {
        if (videoPlayer != null) videoPlayer.loopPointReached -= EndCutscene;
    }
}
