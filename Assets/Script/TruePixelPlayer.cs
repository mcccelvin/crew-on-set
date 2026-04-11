using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;

[System.Serializable]
public class ClipSegment
{
    public string path;
    public int startFrame;
    public int endFrame;
    public float uiStartX;
    public float uiWidth;
    [HideInInspector] public int globalStartFrame;
    [HideInInspector] public int globalEndFrame;
}

public class TruePixelPlayer : MonoBehaviour
{
    public RawImage computerScreen;
    public float framesPerSecond = 24f;

    [Header("UI Buttons")]
    public GameObject playButtonUI;
    public GameObject pauseButtonUI;

    [Header("Premiere Timeline (Small Editor)")]
    public RectTransform playheadLine;
    public RectTransform timelineArea;

    [Header("Export Playback UI (Big Screen)")]
    public Slider exportProgressBar;

    [Header("Loading Screen")]
    public GameObject loadingPanel;
    public TextMeshProUGUI loadingText;

    private List<Texture2D> preloadedTextures = new List<Texture2D>();
    private bool isPaused = true;
    public bool isFinished = false;

    private List<ClipSegment> currentSequence = new List<ClipSegment>();
    private int currentFrameIndex = 0;
    private bool isFadingIn = false;
    private Texture2D thumb = null;

    private float playbackTimer = 0f;

    private void Update()
    {   if (currentFrameIndex >= preloadedTextures.Count)
            {
                isFinished = true;
                isPaused = true;
                if (TimelinePlayhead.Instance != null) TimelinePlayhead.Instance.StopPlayback();

                // Instantly rewind the playhead to 0.00 when finished
                currentFrameIndex = 0;
                RenderCurrentFrame();
                UpdatePlayPauseUI();

                if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
                {
                    // Keep this one (your original fix)
                    EditorTutorialManager.Instance.OnPlaybackFinished();
                    
                    // --- ADD THIS LINE --- 
                    // This finally tells the tutorial: "The playback task is fully complete!"
                    EditorTutorialManager.Instance.OnTimelinePlayed(); 
                }
            }
        if (isPaused || isFinished || preloadedTextures.Count == 0) return;

        playbackTimer += Time.deltaTime;
        if (playbackTimer >= (1f / framesPerSecond))
        {
            playbackTimer -= (1f / framesPerSecond);
            currentFrameIndex++;

            if (currentFrameIndex >= preloadedTextures.Count)
            {
                isFinished = true;
                isPaused = true;
                if (TimelinePlayhead.Instance != null) TimelinePlayhead.Instance.StopPlayback();

                // Instantly rewind the playhead to 0.00 when finished
                currentFrameIndex = 0;
                RenderCurrentFrame();
                UpdatePlayPauseUI();

                if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
                {
                    // Keep this one (your original fix)
                    EditorTutorialManager.Instance.OnPlaybackFinished();

                    // --- ADD THIS LINE --- 
                    // This finally tells the tutorial: "The playback task is fully complete!"
                    EditorTutorialManager.Instance.OnTimelinePlayed();
                }
            }
            else
            {
                RenderCurrentFrame();
                if (exportProgressBar != null) exportProgressBar.SetValueWithoutNotify(currentFrameIndex);
            }
        }
    }

    private void ShowLoading(string message)
    {
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (loadingText != null) loadingText.text = message;
    }

    private void HideLoading()
    {
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }

    public void PlayTape(string tapeFilePath)
    {
        StopTape();
        currentSequence.Clear();
        StartCoroutine(LoadSingleTapeCoroutine(tapeFilePath));
    }

    private IEnumerator LoadSingleTapeCoroutine(string tapeFilePath)
    {
        ShowLoading("Loading Tape...");
        yield return null;

        try
        {
            foreach (var tex in preloadedTextures) if (tex != null) Destroy(tex);
            preloadedTextures.Clear();

            if (File.Exists(tapeFilePath))
            {
                using (BinaryReader reader = new BinaryReader(new FileStream(tapeFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    int frameCount = reader.ReadInt32();
                    for (int i = 0; i < frameCount; i++)
                    {
                        int frameSize = reader.ReadInt32();
                        byte[] frameBytes = reader.ReadBytes(frameSize);

                        Texture2D tex = new Texture2D(2, 2);
                        tex.LoadImage(frameBytes);
                        tex.Apply();
                        preloadedTextures.Add(tex);

                        if (preloadedTextures.Count % 10 == 0) yield return null;
                    }
                }
            }
        }
        finally
        {
            HideLoading();
            isPaused = false;
            isFinished = false;
            currentFrameIndex = 0;
            playbackTimer = 0f;

            SetupScrubBar();
            RenderCurrentFrame();
            UpdatePlayPauseUI();
        }
    }

    public void PlaySequence(List<ClipSegment> sequence, bool useFadeIn)
    {
        StopTape();
        currentSequence = sequence;
        isFadingIn = false;
        StartCoroutine(LoadSequenceCoroutine());
    }

    private IEnumerator LoadSequenceCoroutine()
    {
        ShowLoading("Compiling Timeline...");
        yield return null;

        try
        {
            foreach (var tex in preloadedTextures) if (tex != null) Destroy(tex);
            preloadedTextures.Clear();

            foreach (var clip in currentSequence)
            {
                if (File.Exists(clip.path))
                {
                    using (BinaryReader reader = new BinaryReader(new FileStream(clip.path, FileMode.Open, FileAccess.Read, FileShare.Read)))
                    {
                        int frameCount = reader.ReadInt32();
                        int start = Mathf.Clamp(clip.startFrame, 0, frameCount - 1);
                        int end = Mathf.Clamp(clip.endFrame, start + 1, frameCount);

                        clip.globalStartFrame = preloadedTextures.Count;

                        for (int i = 0; i < start; i++) reader.ReadBytes(reader.ReadInt32());

                        for (int i = start; i < end; i++)
                        {
                            byte[] data = reader.ReadBytes(reader.ReadInt32());
                            Texture2D tex = new Texture2D(2, 2);
                            tex.LoadImage(data);
                            tex.Apply();
                            preloadedTextures.Add(tex);

                            if (preloadedTextures.Count % 10 == 0)
                            {
                                if (loadingText != null) loadingText.text = $"Compiling Frame {preloadedTextures.Count}...";
                                yield return null;
                            }
                        }
                        clip.globalEndFrame = preloadedTextures.Count;
                    }
                }
            }
        }
        finally
        {
            HideLoading();
            isPaused = false;
            isFinished = false;
            currentFrameIndex = 0;
            playbackTimer = 0f;

            if (TimelinePlayhead.Instance != null) TimelinePlayhead.Instance.StartPlayback();

            SetupScrubBar();
            RenderCurrentFrame();
            UpdatePlayPauseUI();
        }
    }

    public void TogglePlayPause()
    {
        // --- THE FIX: Tell the Tutorial Manager that we clicked Play! ---
        if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
        {
            EditorTutorialManager.Instance.OnTimelinePlayed();
        }

        if (isFinished)
        {
            isFinished = false;
            currentFrameIndex = 0;
            playbackTimer = 0f;
            isPaused = false;
            if (TimelinePlayhead.Instance != null) TimelinePlayhead.Instance.StartPlayback();
        }
        else
        {
            isPaused = !isPaused;
            if (isPaused && TimelinePlayhead.Instance != null) TimelinePlayhead.Instance.PausePlayback();
            if (!isPaused && TimelinePlayhead.Instance != null) TimelinePlayhead.Instance.isPlaying = true;
        }

        UpdatePlayPauseUI();
    }

    public void StopTape()
    {
        StopAllCoroutines();
        isPaused = true;
        isFinished = true;
        if (TimelinePlayhead.Instance != null) TimelinePlayhead.Instance.StopPlayback();
        UpdatePlayPauseUI();
    }

    private void UpdatePlayPauseUI()
    {
        if (playButtonUI != null) playButtonUI.SetActive(isPaused || isFinished);
        if (pauseButtonUI != null) pauseButtonUI.SetActive(!isPaused && !isFinished);
    }

    private void RenderCurrentFrame()
    {
        if (computerScreen == null || preloadedTextures.Count == 0 || currentFrameIndex >= preloadedTextures.Count) return;

        computerScreen.texture = preloadedTextures[currentFrameIndex];

        float newX = 0f;

        if (currentSequence != null && currentSequence.Count > 0)
        {
            ClipSegment activeClip = null;

            foreach (var clip in currentSequence)
            {
                if (currentFrameIndex >= clip.globalStartFrame && currentFrameIndex <= clip.globalEndFrame)
                {
                    activeClip = clip;
                    break;
                }
            }

            if (activeClip != null)
            {
                int framesInClip = activeClip.globalEndFrame - activeClip.globalStartFrame;

                if (framesInClip > 1)
                {
                    float clipProgress = (float)(currentFrameIndex - activeClip.globalStartFrame) / (framesInClip - 1);
                    newX = activeClip.uiStartX + (clipProgress * activeClip.uiWidth);
                }
                else newX = activeClip.uiStartX;
            }
        }
        else
        {
            float pps = 40f;
            if (TimelineManager.Instance != null && TimelineManager.Instance.pixelsPerSecond > 0)
            {
                pps = TimelineManager.Instance.pixelsPerSecond;
            }

            float pixelsPerFrame = pps / framesPerSecond;
            newX = currentFrameIndex * pixelsPerFrame;
        }

        if (playheadLine != null)
        {
            playheadLine.anchoredPosition = new Vector2(newX, playheadLine.anchoredPosition.y);
        }

        UpdateOverlays(currentFrameIndex, newX);
    }

    public void SetupScrubBar()
    {
        if (exportProgressBar != null && preloadedTextures.Count > 0)
        {
            exportProgressBar.minValue = 0;
            exportProgressBar.maxValue = preloadedTextures.Count - 1;
            exportProgressBar.value = 0;
            exportProgressBar.onValueChanged.RemoveAllListeners();
            exportProgressBar.onValueChanged.AddListener(OnScrub);
        }
    }

    public void OnScrub(float value)
    {
        if (preloadedTextures.Count == 0) return;

        isPaused = true;
        if (TimelinePlayhead.Instance != null) TimelinePlayhead.Instance.PausePlayback();
        UpdatePlayPauseUI();

        currentFrameIndex = Mathf.Clamp(Mathf.RoundToInt(value), 0, preloadedTextures.Count - 1);
        RenderCurrentFrame();

        if (isFinished)
        {
            isFinished = false;
            UpdatePlayPauseUI();
        }
    }

    private void UpdateOverlays(int compiledFrameIndex, float timelinePixelX)
    {
        int currentTimelineFrame = compiledFrameIndex;

        if (TimelineManager.Instance != null && TimelineManager.Instance.pixelsPerSecond > 0)
        {
            float pixelsPerFrame = TimelineManager.Instance.pixelsPerSecond / framesPerSecond;
            if (pixelsPerFrame > 0) currentTimelineFrame = Mathf.RoundToInt(timelinePixelX / pixelsPerFrame);
        }

        DraggableOverlay[] overlays = FindObjectsOfType<DraggableOverlay>();
        foreach (var o in overlays)
        {
            if (o.isOnTimeline)
            {
                o.EvaluateVisibility(currentTimelineFrame, !isPaused);
            }
        }
    }

    public void ShowPreviewFrame(string tapeFilePath)
    {
        if (computerScreen == null || !File.Exists(tapeFilePath)) return;

        using (BinaryReader reader = new BinaryReader(new FileStream(tapeFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
        {
            reader.ReadInt32();
            int frameSize = reader.ReadInt32();
            byte[] frameBytes = reader.ReadBytes(frameSize);
            if (thumb != null) Destroy(thumb);
            thumb = new Texture2D(2, 2);
            thumb.LoadImage(frameBytes);
            thumb.Apply();
            computerScreen.texture = thumb;
        }
    }
}