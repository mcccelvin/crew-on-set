using UnityEngine;
using UnityEngine.UI;

public class TimelinePlayhead : MonoBehaviour
{
    public static TimelinePlayhead Instance;
    private RectTransform myRect;
    private float pixelsPerFrame = 40f / 24f; // Matches your Premiere settings

    public bool isPlaying = false;
    public int currentFrame = 0;
    private float frameTimer = 0f;

    void Awake()
    {
        Instance = this;
        myRect = GetComponent<RectTransform>();

        // Force the playhead to start at the absolute left edge!
        myRect.anchorMin = new Vector2(0, 0.5f);
        myRect.anchorMax = new Vector2(0, 0.5f);
        myRect.pivot = new Vector2(0.5f, 0.5f);
        myRect.anchoredPosition = new Vector2(0, 0);
    }

    // Call this from your UI Play Button!
    public void StartPlayback()
    {
        currentFrame = 0;
        isPlaying = true;
        frameTimer = 0f;
        UpdatePlayheadAndLogos();
    }

    public void StopPlayback()
    {
        isPlaying = false;
        currentFrame = 0;
        myRect.anchoredPosition = new Vector2(0, 0); // Snap back to start
        UpdatePlayheadAndLogos();
    }
    public void PausePlayback()
    {
        isPlaying = false;
        // Notice we don't reset the position here, so it freezes exactly at the end!
    }

    void Update()
    {
        if (!isPlaying) return;

        // Move forward at exactly 24 FPS
        frameTimer += Time.deltaTime;
        if (frameTimer >= (1f / 24f))
        {
            frameTimer -= (1f / 24f);
            currentFrame++;
            UpdatePlayheadAndLogos();
        }
    }

    private void UpdatePlayheadAndLogos()
    {
        // 1. Physically slide the red line across the timeline
        myRect.anchoredPosition = new Vector2(currentFrame * pixelsPerFrame, myRect.anchoredPosition.y);

        // 2. Tell every logo on the TV to turn ON or OFF!
        DraggableOverlay[] allLogos = FindObjectsOfType<DraggableOverlay>();
        foreach (var logo in allLogos)
        {
            logo.EvaluateVisibility(currentFrame, isPlaying);
        }
    }
}