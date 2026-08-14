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
    public float framesPerSecond = TapeSettings.framesPerSecond;

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

    private List<byte[]> preloadedFrames = new List<byte[]>();
    private Texture2D playbackTexture;
    private DraggableOverlay[] timelineOverlays;
    private bool isPaused = true;
    public bool isFinished = false;

    private List<ClipSegment> currentSequence = new List<ClipSegment>();
    private int currentFrameIndex = 0;
    private bool isFadingIn = false;
    private Texture2D thumb = null;

    private float playbackTimer = 0f;

    private void Update()
    {
        if (preloadedFrames.Count == 0) return;

        float frameInterval = 1f / Mathf.Max(1f, framesPerSecond);

        if (currentFrameIndex >= preloadedFrames.Count)
        {
            FinishPlayback();
            return;
        }

        if (isPaused || isFinished) return;

        playbackTimer += Time.deltaTime;
        if (playbackTimer >= frameInterval)
        {
            playbackTimer -= frameInterval;
            currentFrameIndex++;

            if (currentFrameIndex >= preloadedFrames.Count)
            {
                FinishPlayback();
            }
            else
            {
                RenderCurrentFrame();
                if (exportProgressBar != null) exportProgressBar.SetValueWithoutNotify(currentFrameIndex);
            }
        }
    }

    private void FinishPlayback()
    {
        isFinished = true;
        isPaused = true;
        if (TimelinePlayhead.Instance != null) TimelinePlayhead.Instance.StopPlayback();

        currentFrameIndex = 0;
        RenderCurrentFrame();
        UpdatePlayPauseUI();

        if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
        {
            EditorTutorialManager.Instance.OnPlaybackFinished();
            EditorTutorialManager.Instance.OnTimelinePlayed();
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
        isFadingIn = false;
        StartCoroutine(LoadSingleTapeCoroutine(tapeFilePath));
    }

    private IEnumerator LoadSingleTapeCoroutine(string tapeFilePath)
    {
        ShowLoading("Loading Tape...");
        yield return null;

        try
        {
            preloadedFrames.Clear();

            if (File.Exists(tapeFilePath))
            {
                using (BinaryReader reader = new BinaryReader(new FileStream(tapeFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    int frameCount = reader.ReadInt32();
                    for (int i = 0; i < frameCount; i++)
                    {
                        int frameSize = reader.ReadInt32();
                        byte[] frameBytes = reader.ReadBytes(frameSize);

                        preloadedFrames.Add(frameBytes);

                        if (preloadedFrames.Count % 10 == 0) yield return null;
                    }
                }
            }
        }
        finally
        {
            HideLoading();
            isPaused = preloadedFrames.Count == 0;
            isFinished = preloadedFrames.Count == 0;
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
        isFadingIn = useFadeIn;
        StartCoroutine(LoadSequenceCoroutine());
    }

    private IEnumerator LoadSequenceCoroutine()
    {
        ShowLoading("Compiling Timeline...");
        yield return null;

        try
        {
            preloadedFrames.Clear();

            foreach (var clip in currentSequence)
            {
                if (File.Exists(clip.path))
                {
                    using (BinaryReader reader = new BinaryReader(new FileStream(clip.path, FileMode.Open, FileAccess.Read, FileShare.Read)))
                    {
                        int frameCount = reader.ReadInt32();
                        if (frameCount <= 0) continue;

                        int start = Mathf.Clamp(clip.startFrame, 0, frameCount - 1);
                        int end = Mathf.Clamp(clip.endFrame, start + 1, frameCount);

                        clip.globalStartFrame = preloadedFrames.Count;

                        for (int i = 0; i < start; i++) reader.ReadBytes(reader.ReadInt32());

                        for (int i = start; i < end; i++)
                        {
                            byte[] data = reader.ReadBytes(reader.ReadInt32());
                            preloadedFrames.Add(data);

                            if (preloadedFrames.Count % 10 == 0)
                            {
                                if (loadingText != null) loadingText.text = $"Compiling Frame {preloadedFrames.Count}...";
                                yield return null;
                            }
                        }
                        clip.globalEndFrame = preloadedFrames.Count;
                    }
                }
            }
        }
        finally
        {
            HideLoading();
            isPaused = preloadedFrames.Count == 0;
            isFinished = preloadedFrames.Count == 0;
            currentFrameIndex = 0;
            playbackTimer = 0f;

            if (TimelinePlayhead.Instance != null && preloadedFrames.Count > 0) TimelinePlayhead.Instance.StartPlayback();

            RefreshOverlays();
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
        HideLoading();
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
        if (computerScreen == null || preloadedFrames.Count == 0 || currentFrameIndex >= preloadedFrames.Count) return;

        if (playbackTexture == null) playbackTexture = new Texture2D(2, 2);
        playbackTexture.LoadImage(preloadedFrames[currentFrameIndex]);
        computerScreen.texture = playbackTexture;

        Color screenColor = computerScreen.color;
        if (isFadingIn)
        {
            int fadeFrameCount = Mathf.Max(1, Mathf.RoundToInt(framesPerSecond));
            screenColor.a = Mathf.Clamp01((float)currentFrameIndex / fadeFrameCount);
        }
        else screenColor.a = 1f;
        computerScreen.color = screenColor;

        float newX = 0f;

        if (currentSequence != null && currentSequence.Count > 0)
        {
            ClipSegment activeClip = null;

            foreach (var clip in currentSequence)
            {
                if (currentFrameIndex >= clip.globalStartFrame && currentFrameIndex < clip.globalEndFrame)
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

            float pixelsPerFrame = pps / Mathf.Max(1f, framesPerSecond);
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
        if (exportProgressBar != null && preloadedFrames.Count > 0)
        {
            exportProgressBar.minValue = 0;
            exportProgressBar.maxValue = preloadedFrames.Count - 1;
            exportProgressBar.value = 0;
            exportProgressBar.onValueChanged.RemoveListener(OnScrub);
            exportProgressBar.onValueChanged.AddListener(OnScrub);
        }
    }

    public void OnScrub(float value)
    {
        if (preloadedFrames.Count == 0) return;

        isPaused = true;
        if (TimelinePlayhead.Instance != null) TimelinePlayhead.Instance.PausePlayback();
        UpdatePlayPauseUI();

        currentFrameIndex = Mathf.Clamp(Mathf.RoundToInt(value), 0, preloadedFrames.Count - 1);
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
            float pixelsPerFrame = TimelineManager.Instance.pixelsPerSecond / Mathf.Max(1f, framesPerSecond);
            if (pixelsPerFrame > 0) currentTimelineFrame = Mathf.RoundToInt(timelinePixelX / pixelsPerFrame);
        }

        if (timelineOverlays == null) return;

        foreach (DraggableOverlay overlay in timelineOverlays)
        {
            if (overlay != null && overlay.isOnTimeline)
            {
                overlay.EvaluateVisibility(currentTimelineFrame, !isPaused);
            }
        }
    }

    public void RefreshOverlays()
    {
        timelineOverlays = FindObjectsOfType<DraggableOverlay>();
    }

    public void ShowPreviewFrame(string tapeFilePath)
    {
        if (computerScreen == null || !File.Exists(tapeFilePath)) return;

        using (BinaryReader reader = new BinaryReader(new FileStream(tapeFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
        {
            int frameCount = reader.ReadInt32();
            if (frameCount <= 0) return;

            int frameSize = reader.ReadInt32();
            byte[] frameBytes = reader.ReadBytes(frameSize);
            if (thumb != null) Destroy(thumb);
            thumb = new Texture2D(2, 2);
            thumb.LoadImage(frameBytes);
            computerScreen.texture = thumb;
        }
    }

    private void OnDestroy()
    {
        if (exportProgressBar != null)
            exportProgressBar.onValueChanged.RemoveListener(OnScrub);

        if (playbackTexture != null) Destroy(playbackTexture);
        if (thumb != null) Destroy(thumb);
    }
}
