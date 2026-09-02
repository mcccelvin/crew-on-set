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
    private Rect originalScreenUVRect;
    private bool hasOriginalScreenUVRect = false;
    private CanvasGroup editorialCanvasGroup;
    private AudioSource editorialAudioSource;
    private AudioClip editorialAudioClip;
    private PlayerEditTools.MusicMode preparedMusicMode = PlayerEditTools.MusicMode.None;
    private int preparedMusicFrameCount = 0;
    private bool editorialAudioStarted = false;
    private bool editorialAudioPaused = false;

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
            int framesToAdvance = Mathf.Max(1, Mathf.FloorToInt(playbackTimer / frameInterval));
            playbackTimer -= framesToAdvance * frameInterval;
            currentFrameIndex += framesToAdvance;

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

        currentFrameIndex = Mathf.Max(0, preloadedFrames.Count - 1);
        RenderCurrentFrame();
        UpdatePlayPauseUI();

        if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
        {
            EditorTutorialManager.Instance.OnPlaybackFinished();
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

            PreparePlayerCreatedEffects();

            RefreshOverlays();
            SetupScrubBar();
            RenderCurrentFrame();
            UpdatePlayPauseUI();

            if (preloadedFrames.Count == 0 && EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
            {
                EditorTutorialManager.Instance.ShowWarning("This timeline has no readable video frames. Return to the studio and record a new clip.");
            }
        }
    }

    public void TogglePlayPause()
    {
        if (preloadedFrames.Count == 0) return;

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

        SyncPlayerCreatedAudio();
        UpdatePlayPauseUI();
    }

    public void StopTape()
    {
        StopAllCoroutines();
        HideLoading();
        isPaused = true;
        isFinished = true;
        if (TimelinePlayhead.Instance != null) TimelinePlayhead.Instance.StopPlayback();
        StopPlayerCreatedEffects();
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
        screenColor.a = 1f;
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

        ApplyPlayerCreatedMotion();
        ApplyPlayerCreatedTransition();
        SyncPlayerCreatedAudio();
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

        StopPlayerCreatedEffects();

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

    public void RefreshPlayerCreatedEffects()
    {
        if (currentSequence == null || currentSequence.Count == 0 || preloadedFrames.Count == 0) return;

        PreparePlayerCreatedEffects();
        RenderCurrentFrame();
    }

    private void PreparePlayerCreatedEffects()
    {
        CacheOriginalScreenUVRect();
        EnsureEditorialCanvasGroup();

        PlayerEditTools tools = PlayerEditTools.Instance;
        PlayerEditTools.MusicMode musicMode = tools != null ? tools.selectedMusic : PlayerEditTools.MusicMode.None;

        if (musicMode != preparedMusicMode || editorialAudioClip == null || preparedMusicFrameCount != preloadedFrames.Count)
        {
            BuildPlayerSelectedMusic(musicMode);
        }
    }

    private void ApplyPlayerCreatedMotion()
    {
        if (computerScreen == null) return;
        CacheOriginalScreenUVRect();

        if (currentSequence == null || currentSequence.Count == 0)
        {
            computerScreen.uvRect = originalScreenUVRect;
            return;
        }

        PlayerEditTools tools = PlayerEditTools.Instance;
        PlayerEditTools.CameraMotionMode motionMode = tools != null ? tools.selectedCameraMotion : PlayerEditTools.CameraMotionMode.None;

        if (motionMode == PlayerEditTools.CameraMotionMode.None || preloadedFrames.Count <= 1)
        {
            computerScreen.uvRect = originalScreenUVRect;
            return;
        }

        float progress = Mathf.Clamp01((float)currentFrameIndex / (preloadedFrames.Count - 1));
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        float crop = 0.075f;
        float pan = 0f;

        if (motionMode == PlayerEditTools.CameraMotionMode.SlowPushIn)
        {
            crop *= easedProgress;
        }
        else if (motionMode == PlayerEditTools.CameraMotionMode.SlowPullOut)
        {
            crop *= 1f - easedProgress;
        }
        else
        {
            crop = 0.045f;
            float direction = motionMode == PlayerEditTools.CameraMotionMode.PanLeft ? -1f : 1f;
            pan = direction * Mathf.Lerp(-crop, crop, easedProgress);
        }

        computerScreen.uvRect = new Rect(originalScreenUVRect.x + crop + pan,
                                         originalScreenUVRect.y + crop,
                                         originalScreenUVRect.width - crop * 2f,
                                         originalScreenUVRect.height - crop * 2f);
    }

    private void ApplyPlayerCreatedTransition()
    {
        EnsureEditorialCanvasGroup();
        if (editorialCanvasGroup == null) return;

        if (currentSequence == null || currentSequence.Count == 0 || isPaused || isFinished)
        {
            editorialCanvasGroup.alpha = 1f;
            return;
        }

        PlayerEditTools tools = PlayerEditTools.Instance;
        PlayerEditTools.TransitionMode transitionMode = tools != null ? tools.selectedTransition : PlayerEditTools.TransitionMode.Cut;

        if (transitionMode == PlayerEditTools.TransitionMode.Cut)
        {
            if (isFadingIn)
            {
                int legacyFadeFrames = Mathf.Max(1, Mathf.RoundToInt(framesPerSecond));
                editorialCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((float)currentFrameIndex / legacyFadeFrames));
            }
            else
            {
                editorialCanvasGroup.alpha = 1f;
            }
            return;
        }

        float edgeSeconds = transitionMode == PlayerEditTools.TransitionMode.DipToBlack ? 0.28f : 0.5f;
        int edgeFrames = Mathf.Max(2, Mathf.RoundToInt(framesPerSecond * edgeSeconds));
        int framesFromEnd = preloadedFrames.Count - 1 - currentFrameIndex;
        float transitionAlpha = Mathf.Min(Mathf.Clamp01((float)currentFrameIndex / edgeFrames),
                                          Mathf.Clamp01((float)framesFromEnd / edgeFrames));

        if (transitionMode == PlayerEditTools.TransitionMode.DipToBlack && currentSequence.Count > 1)
        {
            int dipFrames = Mathf.Max(2, Mathf.RoundToInt(framesPerSecond * 0.18f));
            for (int i = 1; i < currentSequence.Count; i++)
            {
                int distanceFromEdit = Mathf.Abs(currentFrameIndex - currentSequence[i].globalStartFrame);
                if (distanceFromEdit <= dipFrames)
                {
                    transitionAlpha = Mathf.Min(transitionAlpha, Mathf.Clamp01((float)distanceFromEdit / dipFrames));
                }
            }
        }

        editorialCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, transitionAlpha);
    }

    private void SyncPlayerCreatedAudio()
    {
        if (editorialAudioSource == null || editorialAudioSource.clip == null) return;

        if (currentSequence == null || currentSequence.Count == 0)
        {
            editorialAudioSource.Stop();
            editorialAudioStarted = false;
            editorialAudioPaused = false;
            return;
        }

        float targetTime = currentFrameIndex / Mathf.Max(1f, framesPerSecond);
        targetTime = Mathf.Clamp(targetTime, 0f, Mathf.Max(0f, editorialAudioSource.clip.length - 0.01f));
        if (Mathf.Abs(editorialAudioSource.time - targetTime) > 0.2f) editorialAudioSource.time = targetTime;

        bool shouldPlay = !isPaused && !isFinished;
        if (shouldPlay)
        {
            if (editorialAudioPaused && editorialAudioStarted)
            {
                editorialAudioSource.UnPause();
            }
            else if (!editorialAudioSource.isPlaying)
            {
                editorialAudioSource.Play();
                editorialAudioStarted = true;
            }

            editorialAudioPaused = false;
        }
        else if (editorialAudioSource.isPlaying)
        {
            editorialAudioSource.Pause();
            editorialAudioPaused = true;
        }
    }

    private void BuildPlayerSelectedMusic(PlayerEditTools.MusicMode musicMode)
    {
        if (editorialAudioSource == null) editorialAudioSource = gameObject.GetComponent<AudioSource>();
        if (editorialAudioSource == null) editorialAudioSource = gameObject.AddComponent<AudioSource>();

        editorialAudioSource.Stop();
        editorialAudioSource.playOnAwake = false;
        editorialAudioSource.loop = false;
        editorialAudioSource.spatialBlend = 0f;
        editorialAudioSource.volume = 0.5f;
        editorialAudioStarted = false;
        editorialAudioPaused = false;
        preparedMusicMode = musicMode;
        preparedMusicFrameCount = preloadedFrames.Count;

        if (editorialAudioClip != null)
        {
            Destroy(editorialAudioClip);
            editorialAudioClip = null;
        }

        if (musicMode == PlayerEditTools.MusicMode.None)
        {
            editorialAudioSource.clip = null;
            return;
        }

        float duration = Mathf.Max(1f, preloadedFrames.Count / Mathf.Max(1f, framesPerSecond));
        int sampleRate = 22050;
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
        float[] samples = new float[sampleCount];
        float beatsPerMinute = musicMode == PlayerEditTools.MusicMode.Clean ? 92f : musicMode == PlayerEditTools.MusicMode.Energy ? 126f : 76f;
        float beatLength = 60f / beatsPerMinute;

        for (int i = 0; i < sampleCount; i++)
        {
            float time = (float)i / sampleRate;
            float progress = time / duration;
            float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.08f, progress));
            float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, 1f, progress));
            float musicSample = BuildMusicSample(musicMode, time, beatLength, i);
            samples[i] = Mathf.Clamp(musicSample * fadeIn * fadeOut, -0.7f, 0.7f);
        }

        editorialAudioClip = AudioClip.Create("Player Selected " + musicMode + " Music", sampleCount, 1, sampleRate, false);
        editorialAudioClip.SetData(samples, 0);
        editorialAudioSource.clip = editorialAudioClip;
        editorialAudioSource.time = 0f;
    }

    private float BuildMusicSample(PlayerEditTools.MusicMode musicMode, float time, float beatLength, int sampleIndex)
    {
        float beatPosition = time / beatLength;
        int beatIndex = Mathf.FloorToInt(beatPosition);
        int beatInBar = beatIndex % 4;
        int barIndex = beatIndex / 4;
        float beatPhase = Mathf.Repeat(beatPosition, 1f);
        float eighthPosition = time / (beatLength * 0.5f);
        int eighthIndex = Mathf.FloorToInt(eighthPosition);
        float eighthPhase = Mathf.Repeat(eighthPosition, 1f);

        float root = GetMusicRoot(musicMode, barIndex);
        bool minorChord = musicMode != PlayerEditTools.MusicMode.Clean;
        float thirdRatio = minorChord ? 1.189207f : 1.259921f;
        float fifthRatio = 1.498307f;

        float pad = Mathf.Sin(time * root * Mathf.PI * 2f) * 0.035f;
        pad += Mathf.Sin(time * root * thirdRatio * Mathf.PI * 2f) * 0.022f;
        pad += Mathf.Sin(time * root * fifthRatio * Mathf.PI * 2f) * 0.019f;

        float bassEnvelope = Mathf.Exp(-beatPhase * (musicMode == PlayerEditTools.MusicMode.Cinematic ? 3f : 6f));
        float bass = Mathf.Sin(time * root * 0.5f * Mathf.PI * 2f) * bassEnvelope * 0.055f;

        float noteRatio = GetMelodyRatio(eighthIndex);
        float pluckEnvelope = Mathf.Exp(-eighthPhase * (musicMode == PlayerEditTools.MusicMode.Energy ? 8f : 5f));
        float pluckLevel = musicMode == PlayerEditTools.MusicMode.Cinematic ? 0.018f : musicMode == PlayerEditTools.MusicMode.Energy ? 0.045f : 0.03f;
        float pluck = Mathf.Sin(time * root * 2f * noteRatio * Mathf.PI * 2f) * pluckEnvelope * pluckLevel;

        float kickPitch = 1.7f * beatPhase - 0.55f * beatPhase * beatPhase;
        float kick = Mathf.Sin(kickPitch * Mathf.PI * 2f) * Mathf.Exp(-beatPhase * 12f) * 0.12f;

        float noise = GetDeterministicNoise(sampleIndex);
        float snare = (beatInBar == 1 || beatInBar == 3) ? noise * Mathf.Exp(-beatPhase * 18f) * 0.055f : 0f;
        float hatLevel = musicMode == PlayerEditTools.MusicMode.Energy ? 0.035f : 0.015f;
        float hat = noise * Mathf.Exp(-eighthPhase * 30f) * hatLevel;

        if (musicMode == PlayerEditTools.MusicMode.Cinematic)
        {
            kick *= beatInBar == 0 ? 1.25f : 0.35f;
            snare *= 0.2f;
            hat *= 0.25f;
        }
        else if (musicMode == PlayerEditTools.MusicMode.Clean)
        {
            kick *= beatInBar == 0 || beatInBar == 2 ? 0.8f : 0.25f;
            snare *= 0.45f;
            hat *= 0.55f;
        }

        return pad + bass + pluck + kick + snare + hat;
    }

    private float GetMusicRoot(PlayerEditTools.MusicMode musicMode, int barIndex)
    {
        int chord = Mathf.Abs(barIndex) % 4;

        if (musicMode == PlayerEditTools.MusicMode.Clean)
        {
            if (chord == 1) return 110f;
            if (chord == 2) return 87.31f;
            if (chord == 3) return 98f;
            return 130.81f;
        }

        if (musicMode == PlayerEditTools.MusicMode.Energy)
        {
            if (chord == 1) return 65.41f;
            if (chord == 2) return 58.27f;
            if (chord == 3) return 65.41f;
            return 73.42f;
        }

        if (chord == 1) return 51.91f;
        if (chord == 2) return 58.27f;
        if (chord == 3) return 49f;
        return 65.41f;
    }

    private float GetMelodyRatio(int noteIndex)
    {
        int note = Mathf.Abs(noteIndex) % 8;
        if (note == 1 || note == 6) return 1.259921f;
        if (note == 2 || note == 5) return 1.498307f;
        if (note == 3) return 2f;
        if (note == 7) return 1.681793f;
        return 1f;
    }

    private float GetDeterministicNoise(int sampleIndex)
    {
        unchecked
        {
            int value = sampleIndex;
            value = (value << 13) ^ value;
            int hashed = value * (value * value * 15731 + 789221) + 1376312589;
            return 1f - ((hashed & 0x7fffffff) / 1073741824f);
        }
    }

    private void StopPlayerCreatedEffects()
    {
        if (editorialAudioSource != null) editorialAudioSource.Stop();
        if (computerScreen != null && hasOriginalScreenUVRect) computerScreen.uvRect = originalScreenUVRect;
        if (editorialCanvasGroup != null) editorialCanvasGroup.alpha = 1f;
        editorialAudioStarted = false;
        editorialAudioPaused = false;
    }

    private void EnsureEditorialCanvasGroup()
    {
        if (computerScreen == null || editorialCanvasGroup != null) return;

        editorialCanvasGroup = computerScreen.GetComponent<CanvasGroup>();
        if (editorialCanvasGroup == null) editorialCanvasGroup = computerScreen.gameObject.AddComponent<CanvasGroup>();
        editorialCanvasGroup.alpha = 1f;
    }

    private void CacheOriginalScreenUVRect()
    {
        if (computerScreen == null || hasOriginalScreenUVRect) return;

        originalScreenUVRect = computerScreen.uvRect;
        hasOriginalScreenUVRect = true;
    }

    private void OnDestroy()
    {
        if (exportProgressBar != null)
            exportProgressBar.onValueChanged.RemoveListener(OnScrub);

        if (playbackTexture != null) Destroy(playbackTexture);
        if (thumb != null) Destroy(thumb);
        if (editorialAudioClip != null) Destroy(editorialAudioClip);
    }
}

#if false
public class CommercialPresentation : MonoBehaviour
{
    private RawImage screen;
    private RectTransform presentationRoot;
    private CanvasGroup introGroup;
    private RectTransform introRect;
    private TextMeshProUGUI introTitle;
    private TextMeshProUGUI introTagline;
    private CanvasGroup middleGroup;
    private RectTransform middleRect;
    private TextMeshProUGUI middleCopy;
    private CanvasGroup endGroup;
    private RectTransform endRect;
    private Image endBackground;
    private TextMeshProUGUI endTitle;
    private TextMeshProUGUI endTagline;
    private Image lightSweep;
    private RectTransform lightSweepRect;
    private Image fadeImage;
    private Image cutFlashImage;
    private Image colorWash;
    private readonly List<RectTransform> particles = new List<RectTransform>();
    private readonly List<Image> particleImages = new List<Image>();
    private readonly List<float> particleSeeds = new List<float>();
    private List<ClipSegment> sequence = new List<ClipSegment>();
    private AudioSource audioSource;
    private AudioClip commercialAudio;
    private Texture2D sweepTexture;
    private Texture2D circleTexture;
    private Sprite sweepSprite;
    private Sprite circleSprite;
    private Rect originalUVRect;
    private int totalFrames;
    private int currentLevel;
    private int lastClipIndex = -1;
    private int lastRenderedFrame = -1;
    private float framesPerSecond = 12f;
    private float productionQuality = 0.7f;
    private float cutFlash = 0f;
    private bool isPresentationActive = false;
    private bool audioHasStarted = false;
    private bool audioWasPaused = false;

    public void Initialize(RawImage targetScreen)
    {
        if (targetScreen == null) return;
        if (screen == targetScreen && presentationRoot != null) return;

        screen = targetScreen;
        originalUVRect = screen.uvRect;
        BuildPresentationUI();
    }

    public void BeginPresentation(List<ClipSegment> clipSequence, int frameCount, float playbackFramesPerSecond)
    {
        if (screen == null) return;
        if (presentationRoot == null) BuildPresentationUI();

        sequence = clipSequence != null ? clipSequence : new List<ClipSegment>();
        totalFrames = Mathf.Max(1, frameCount);
        framesPerSecond = Mathf.Max(1f, playbackFramesPerSecond);
        currentLevel = FindCampaignLevel();
        productionQuality = CalculateProductionQuality();
        lastClipIndex = -1;
        lastRenderedFrame = -1;
        cutFlash = 0f;
        isPresentationActive = true;

        ConfigureCampaignLook();
        BuildCommercialAudio();

        presentationRoot.gameObject.SetActive(true);
        presentationRoot.SetAsFirstSibling();
        screen.uvRect = originalUVRect;
    }

    public void RenderFrame(int frameIndex, bool isPlaying)
    {
        if (!isPresentationActive || presentationRoot == null || screen == null) return;

        int safeFrame = Mathf.Clamp(frameIndex, 0, totalFrames - 1);
        float progress = totalFrames <= 1 ? 0f : (float)safeFrame / (totalFrames - 1);
        int activeClipIndex = FindActiveClipIndex(safeFrame);

        if (lastClipIndex >= 0 && activeClipIndex != lastClipIndex) cutFlash = 1f;
        lastClipIndex = activeClipIndex;

        int frameDifference = lastRenderedFrame < 0 ? 1 : Mathf.Max(1, Mathf.Abs(safeFrame - lastRenderedFrame));
        lastRenderedFrame = safeFrame;
        cutFlash = Mathf.MoveTowards(cutFlash, 0f, frameDifference * 0.16f);

        AnimateEditorialMotion(progress);
        AnimateGraphics(progress);
        AnimateParticles(progress);
        SetPlaybackState(isPlaying, safeFrame, framesPerSecond);
    }

    public void SetPlaybackState(bool isPlaying, int frameIndex, float playbackFramesPerSecond)
    {
        if (audioSource == null || audioSource.clip == null) return;

        float targetTime = frameIndex / Mathf.Max(1f, playbackFramesPerSecond);
        targetTime = Mathf.Clamp(targetTime, 0f, Mathf.Max(0f, audioSource.clip.length - 0.01f));

        if (Mathf.Abs(audioSource.time - targetTime) > 0.2f) audioSource.time = targetTime;

        if (isPlaying)
        {
            if (audioWasPaused && audioHasStarted)
            {
                audioSource.UnPause();
            }
            else if (!audioSource.isPlaying)
            {
                audioSource.Play();
                audioHasStarted = true;
            }

            audioWasPaused = false;
        }
        else if (audioSource.isPlaying)
        {
            audioSource.Pause();
            audioWasPaused = true;
        }
    }

    public void EndPresentation()
    {
        isPresentationActive = false;
        if (presentationRoot != null) presentationRoot.gameObject.SetActive(false);
        if (audioSource != null) audioSource.Stop();
        if (screen != null) screen.uvRect = originalUVRect;
        audioHasStarted = false;
        audioWasPaused = false;
    }

    private void BuildPresentationUI()
    {
        if (screen == null || presentationRoot != null) return;

        GameObject rootObject = new GameObject("Commercial Presentation", typeof(RectTransform));
        rootObject.layer = screen.gameObject.layer;
        rootObject.transform.SetParent(screen.transform, false);
        presentationRoot = rootObject.GetComponent<RectTransform>();
        StretchRect(presentationRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        colorWash = CreateImage("Campaign Color Wash", presentationRoot, Color.clear);
        StretchRect(colorWash.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        CreateCinematicBar("Top Letterbox", new Vector2(0f, 0.94f), new Vector2(1f, 1f));
        CreateCinematicBar("Bottom Letterbox", new Vector2(0f, 0f), new Vector2(1f, 0.06f));

        CreateParticleGraphics();

        lightSweep = CreateImage("Animated Light Sweep", presentationRoot, Color.white);
        lightSweepRect = lightSweep.rectTransform;
        lightSweepRect.anchorMin = new Vector2(0.5f, 0.5f);
        lightSweepRect.anchorMax = new Vector2(0.5f, 0.5f);
        lightSweepRect.pivot = new Vector2(0.5f, 0.5f);
        lightSweepRect.sizeDelta = new Vector2(260f, 1200f);
        lightSweepRect.localEulerAngles = new Vector3(0f, 0f, -18f);

        BuildIntroGraphics();
        BuildMiddleGraphics();
        BuildEndCard();

        cutFlashImage = CreateImage("Editorial Cut Flash", presentationRoot, Color.clear);
        StretchRect(cutFlashImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        fadeImage = CreateImage("Commercial Fade", presentationRoot, Color.black);
        StretchRect(fadeImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.32f;

        presentationRoot.gameObject.SetActive(false);
    }

    private void BuildIntroGraphics()
    {
        GameObject introObject = new GameObject("Campaign Intro", typeof(RectTransform), typeof(CanvasGroup));
        introObject.layer = screen.gameObject.layer;
        introObject.transform.SetParent(presentationRoot, false);
        introRect = introObject.GetComponent<RectTransform>();
        introRect.anchorMin = new Vector2(0.07f, 0.13f);
        introRect.anchorMax = new Vector2(0.64f, 0.34f);
        introRect.offsetMin = Vector2.zero;
        introRect.offsetMax = Vector2.zero;
        introGroup = introObject.GetComponent<CanvasGroup>();
        introGroup.blocksRaycasts = false;

        Image accentLine = CreateImage("Intro Accent", introRect, Color.white);
        RectTransform accentRect = accentLine.rectTransform;
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(7f, 0f);

        introTitle = CreateText("Campaign Title", introRect, 54f, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
        StretchRect(introTitle.rectTransform, Vector2.zero, Vector2.one, new Vector2(25f, 38f), Vector2.zero);

        introTagline = CreateText("Campaign Tagline", introRect, 22f, FontStyles.Normal, TextAlignmentOptions.BottomLeft);
        StretchRect(introTagline.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 4f), new Vector2(0f, -58f));
    }

    private void BuildMiddleGraphics()
    {
        GameObject middleObject = new GameObject("Campaign Message", typeof(RectTransform), typeof(CanvasGroup));
        middleObject.layer = screen.gameObject.layer;
        middleObject.transform.SetParent(presentationRoot, false);
        middleRect = middleObject.GetComponent<RectTransform>();
        middleRect.anchorMin = new Vector2(0.5f, 0.12f);
        middleRect.anchorMax = new Vector2(0.5f, 0.12f);
        middleRect.pivot = new Vector2(0.5f, 0f);
        middleRect.anchoredPosition = Vector2.zero;
        middleRect.sizeDelta = new Vector2(940f, 64f);
        middleGroup = middleObject.GetComponent<CanvasGroup>();
        middleGroup.blocksRaycasts = false;

        middleCopy = CreateText("Campaign Copy", middleRect, 26f, FontStyles.Bold, TextAlignmentOptions.Center);
        StretchRect(middleCopy.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private void BuildEndCard()
    {
        GameObject endObject = new GameObject("Commercial End Card", typeof(RectTransform), typeof(CanvasGroup));
        endObject.layer = screen.gameObject.layer;
        endObject.transform.SetParent(presentationRoot, false);
        endRect = endObject.GetComponent<RectTransform>();
        StretchRect(endRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        endGroup = endObject.GetComponent<CanvasGroup>();
        endGroup.blocksRaycasts = false;

        endBackground = CreateImage("End Card Background", endRect, Color.black);
        StretchRect(endBackground.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        endTitle = CreateText("End Card Title", endRect, 88f, FontStyles.Bold, TextAlignmentOptions.Center);
        endTitle.characterSpacing = 4f;
        StretchRect(endTitle.rectTransform, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.72f), Vector2.zero, Vector2.zero);

        endTagline = CreateText("End Card Tagline", endRect, 25f, FontStyles.Bold, TextAlignmentOptions.Center);
        endTagline.characterSpacing = 7f;
        StretchRect(endTagline.rectTransform, new Vector2(0.1f, 0.22f), new Vector2(0.9f, 0.38f), Vector2.zero, Vector2.zero);
    }

    private void CreateParticleGraphics()
    {
        CreateCircleSprite();

        for (int i = 0; i < 12; i++)
        {
            Image particle = CreateImage("Commercial Particle " + (i + 1), presentationRoot, Color.white);
            particle.sprite = circleSprite;
            particle.preserveAspect = true;
            RectTransform particleRect = particle.rectTransform;
            particleRect.anchorMin = new Vector2(0.5f, 0.5f);
            particleRect.anchorMax = new Vector2(0.5f, 0.5f);
            particleRect.pivot = new Vector2(0.5f, 0.5f);
            particles.Add(particleRect);
            particleImages.Add(particle);
            particleSeeds.Add(Mathf.Repeat(i * 0.381966f, 1f));
        }

        CreateSweepSprite();
    }

    private void ConfigureCampaignLook()
    {
        string title = "CRYSTAL BLOOMS";
        string tagline = "CRAFTED TO BE REMEMBERED";
        string copy = "FORM  •  COLOR  •  CRAFT";
        Color accent = new Color(1f, 0.46f, 0.72f);
        Color background = new Color(0.12f, 0.025f, 0.08f, 0.94f);

        if (currentLevel == 2)
        {
            title = "GOKE";
            tagline = "OPEN THE ENERGY";
            copy = "ICE COLD  •  FULL ENERGY";
            accent = new Color(1f, 0.12f, 0.1f);
            background = new Color(0.18f, 0.015f, 0.02f, 0.95f);
        }
        else if (currentLevel == 3)
        {
            title = "LAMBORMINI";
            tagline = "DESIGNED TO MOVE";
            copy = "FORM  •  LIGHT  •  MOTION";
            accent = new Color(1f, 0.66f, 0.12f);
            background = new Color(0.025f, 0.035f, 0.055f, 0.96f);
        }
        else if (currentLevel == 4)
        {
            title = "KAPE KULTURA";
            tagline = "EVERY STORY STARTS HERE";
            copy = "MOMENTS  •  PEOPLE  •  FLAVOR";
            accent = new Color(0.92f, 0.58f, 0.25f);
            background = new Color(0.1f, 0.045f, 0.02f, 0.95f);
        }
        else if (currentLevel >= 5)
        {
            title = "HARAYA";
            tagline = "IMAGINE THE IMPOSSIBLE";
            copy = "VISION  •  CRAFT  •  STORY";
            accent = new Color(0.28f, 0.82f, 1f);
            background = new Color(0.015f, 0.04f, 0.09f, 0.96f);
        }

        introTitle.text = title;
        introTagline.text = tagline;
        middleCopy.text = copy;
        endTitle.text = title;
        endTagline.text = tagline;

        introTitle.color = Color.white;
        introTagline.color = accent;
        middleCopy.color = Color.Lerp(accent, Color.white, 0.18f);
        endTitle.color = Color.white;
        endTagline.color = accent;
        endBackground.color = background;
        colorWash.color = new Color(accent.r, accent.g, accent.b, Mathf.Lerp(0.025f, 0.07f, productionQuality));
        lightSweep.sprite = sweepSprite;
        lightSweep.color = new Color(accent.r, accent.g, accent.b, 0f);

        for (int i = 0; i < particleImages.Count; i++)
        {
            float alpha = currentLevel == 2 ? 0.42f : 0.2f;
            particleImages[i].color = new Color(accent.r, accent.g, accent.b, alpha);

            if (currentLevel == 3 || currentLevel >= 5)
            {
                particles[i].sizeDelta = new Vector2(90f + (i % 3) * 25f, 3f);
            }
            else
            {
                float size = 10f + (i % 4) * 7f;
                particles[i].sizeDelta = new Vector2(size, size);
            }
        }
    }

    private void AnimateEditorialMotion(float progress)
    {
        int act = Mathf.Min(2, Mathf.FloorToInt(progress * 3f));
        float actProgress = Mathf.Repeat(progress * 3f, 1f);
        if (progress >= 0.999f) actProgress = 1f;

        float baseCrop = currentLevel == 1 ? 0.018f : currentLevel == 2 ? 0.026f : 0.032f;
        float actCrop = act == 1 ? baseCrop * 1.65f : act == 2 ? baseCrop * 0.7f : baseCrop;
        float ease = Mathf.SmoothStep(0f, 1f, actProgress);
        float crop = Mathf.Lerp(actCrop * 0.35f, actCrop, ease);

        float horizontalDirection = act == 0 ? -1f : act == 1 ? 1f : 0f;
        float verticalDirection = currentLevel == 3 ? 0.35f : -0.15f;
        float panX = horizontalDirection * crop * (ease - 0.5f) * 0.7f;
        float panY = verticalDirection * crop * (ease - 0.5f);
        screen.uvRect = new Rect(originalUVRect.x + crop + panX,
                                 originalUVRect.y + crop + panY,
                                 originalUVRect.width - crop * 2f,
                                 originalUVRect.height - crop * 2f);
    }

    private void AnimateGraphics(float progress)
    {
        float introAlpha = FadeWindow(progress, 0.045f, 0.1f, 0.27f, 0.34f);
        introGroup.alpha = introAlpha;
        introRect.anchoredPosition = new Vector2(Mathf.Lerp(-55f, 0f, SmoothRange(0.04f, 0.14f, progress)), 0f);

        float middleAlpha = FadeWindow(progress, 0.42f, 0.49f, 0.67f, 0.74f);
        middleGroup.alpha = middleAlpha;
        middleRect.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, SmoothRange(0.42f, 0.54f, progress));

        float endAlpha = SmoothRange(0.79f, 0.88f, progress);
        endGroup.alpha = endAlpha;
        endRect.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, endAlpha);

        Color fadeColor = fadeImage.color;
        fadeColor.a = 1f - SmoothRange(0f, 0.075f, progress);
        fadeImage.color = fadeColor;

        float sweepWindow = FadeWindow(progress, 0.16f, 0.24f, 0.7f, 0.78f);
        float screenWidth = Mathf.Max(1280f, screen.rectTransform.rect.width);
        lightSweepRect.anchoredPosition = new Vector2(Mathf.Lerp(-screenWidth * 0.72f, screenWidth * 0.72f, SmoothRange(0.18f, 0.76f, progress)), 0f);
        Color sweepColor = lightSweep.color;
        sweepColor.a = sweepWindow * Mathf.Lerp(0.08f, 0.23f, productionQuality);
        lightSweep.color = sweepColor;

        cutFlashImage.color = new Color(1f, 1f, 1f, cutFlash * 0.24f);
    }

    private void AnimateParticles(float progress)
    {
        if (screen == null) return;

        float width = Mathf.Max(1280f, screen.rectTransform.rect.width);
        float height = Mathf.Max(720f, screen.rectTransform.rect.height);
        float visibleWindow = FadeWindow(progress, 0.12f, 0.22f, 0.72f, 0.8f);

        for (int i = 0; i < particles.Count; i++)
        {
            float seed = particleSeeds[i];
            float travel = Mathf.Repeat(progress * (currentLevel == 2 ? 1.4f : 0.75f) + seed, 1f);
            float x;
            float y;

            if (currentLevel == 3 || currentLevel >= 5)
            {
                x = Mathf.Lerp(-width * 0.58f, width * 0.58f, travel);
                y = Mathf.Lerp(-height * 0.34f, height * 0.34f, Mathf.Repeat(seed * 2.7f, 1f));
            }
            else
            {
                x = Mathf.Lerp(-width * 0.46f, width * 0.46f, Mathf.Repeat(seed * 3.1f, 1f));
                x += Mathf.Sin((progress + seed) * 8f) * 35f;
                y = Mathf.Lerp(-height * 0.5f, height * 0.48f, travel);
            }

            particles[i].anchoredPosition = new Vector2(x, y);
            float pulse = 0.75f + Mathf.Sin((progress * 12f) + i) * 0.2f;
            particles[i].localScale = Vector3.one * pulse;

            Color particleColor = particleImages[i].color;
            float baseAlpha = currentLevel == 2 ? 0.34f : 0.16f;
            particleColor.a = visibleWindow * baseAlpha * Mathf.Lerp(0.6f, 1f, productionQuality);
            particleImages[i].color = particleColor;
        }
    }

    private int FindCampaignLevel()
    {
        foreach (ClipSegment clip in sequence)
        {
            if (clip != null && clip.campaignLevel >= CampaignProgression.MinimumLevel)
            {
                return Mathf.Clamp(clip.campaignLevel, CampaignProgression.MinimumLevel, CampaignProgression.MaximumLevel);
            }
        }

        return CampaignProgression.GetCurrentLevel();
    }

    private float CalculateProductionQuality()
    {
        float weightedScore = 0f;
        float totalWeight = 0f;

        foreach (ClipSegment clip in sequence)
        {
            if (clip == null) continue;
            float weight = Mathf.Max(1f, clip.endFrame - clip.startFrame);
            float clipScore = Mathf.Clamp01((clip.cameraScore + clip.lightScore) / 100f);
            weightedScore += clipScore * weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0f || weightedScore <= 0f) return 0.7f;
        return Mathf.Clamp01(weightedScore / totalWeight);
    }

    private int FindActiveClipIndex(int frameIndex)
    {
        for (int i = 0; i < sequence.Count; i++)
        {
            ClipSegment clip = sequence[i];
            if (clip != null && frameIndex >= clip.globalStartFrame && frameIndex < clip.globalEndFrame) return i;
        }

        return sequence.Count > 0 ? sequence.Count - 1 : 0;
    }

    private float FadeWindow(float value, float fadeInStart, float fadeInEnd, float fadeOutStart, float fadeOutEnd)
    {
        float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeInStart, fadeInEnd, value));
        float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeOutStart, fadeOutEnd, value));
        return Mathf.Clamp01(fadeIn * fadeOut);
    }

    private float SmoothRange(float minimum, float maximum, float value)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minimum, maximum, value));
    }

    private void BuildCommercialAudio()
    {
        if (commercialAudio != null) Destroy(commercialAudio);

        float duration = Mathf.Max(1f, totalFrames / framesPerSecond);
        int sampleRate = 22050;
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
        float[] samples = new float[sampleCount];
        float baseFrequency = currentLevel == 1 ? 110f : currentLevel == 2 ? 146f : currentLevel == 3 ? 82f : 98f;
        float beatLength = currentLevel == 2 ? 0.48f : currentLevel == 3 ? 0.72f : 0.82f;

        for (int i = 0; i < sampleCount; i++)
        {
            float time = (float)i / sampleRate;
            float normalizedTime = time / duration;
            float masterEnvelope = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.08f, normalizedTime));
            masterEnvelope *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, 1f, normalizedTime));

            float bed = Mathf.Sin(time * baseFrequency * Mathf.PI * 2f) * 0.035f;
            bed += Mathf.Sin(time * baseFrequency * 1.5f * Mathf.PI * 2f) * 0.018f;

            float beatPhase = Mathf.Repeat(time, beatLength) / beatLength;
            float beat = Mathf.Sin(time * baseFrequency * 0.5f * Mathf.PI * 2f) * Mathf.Exp(-beatPhase * 8f) * 0.09f;

            float whooshEnvelope = FadeWindow(normalizedTime, 0f, 0.02f, 0.08f, 0.14f);
            float whoosh = Mathf.Sin(i * 0.017f) * Mathf.Sin(i * 0.0061f) * whooshEnvelope * 0.11f;

            float endHitDistance = Mathf.Abs(normalizedTime - 0.82f);
            float endHitEnvelope = Mathf.Clamp01(1f - endHitDistance / 0.035f);
            float endHit = Mathf.Sin(time * baseFrequency * 0.75f * Mathf.PI * 2f) * endHitEnvelope * 0.13f;

            samples[i] = Mathf.Clamp((bed + beat + whoosh + endHit) * masterEnvelope, -0.4f, 0.4f);
        }

        commercialAudio = AudioClip.Create("Level " + currentLevel + " Commercial Mix", sampleCount, 1, sampleRate, false);
        commercialAudio.SetData(samples, 0);
        audioSource.clip = commercialAudio;
        audioSource.time = 0f;
        audioHasStarted = false;
        audioWasPaused = false;
    }

    private void CreateCinematicBar(string objectName, Vector2 anchorMin, Vector2 anchorMax)
    {
        Image bar = CreateImage(objectName, presentationRoot, new Color(0f, 0f, 0f, 0.86f));
        StretchRect(bar.rectTransform, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.layer = parent.gameObject.layer;
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = parent.gameObject.layer;
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private void StretchRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    private void CreateSweepSprite()
    {
        if (sweepSprite != null) return;

        sweepTexture = new Texture2D(64, 4, TextureFormat.RGBA32, false);
        sweepTexture.wrapMode = TextureWrapMode.Clamp;

        for (int x = 0; x < sweepTexture.width; x++)
        {
            float normalizedX = (float)x / (sweepTexture.width - 1);
            float alpha = Mathf.Pow(Mathf.Sin(normalizedX * Mathf.PI), 4f);

            for (int y = 0; y < sweepTexture.height; y++)
            {
                sweepTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        sweepTexture.Apply();
        sweepSprite = Sprite.Create(sweepTexture, new Rect(0f, 0f, sweepTexture.width, sweepTexture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private void CreateCircleSprite()
    {
        if (circleSprite != null) return;

        circleTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        circleTexture.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2(15.5f, 15.5f);

        for (int x = 0; x < circleTexture.width; x++)
        {
            for (int y = 0; y < circleTexture.height; y++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / 15.5f;
                float alpha = 1f - Mathf.SmoothStep(0.72f, 1f, distance);
                circleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        circleTexture.Apply();
        circleSprite = Sprite.Create(circleTexture, new Rect(0f, 0f, circleTexture.width, circleTexture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private void OnDestroy()
    {
        if (commercialAudio != null) Destroy(commercialAudio);
        if (sweepSprite != null) Destroy(sweepSprite);
        if (circleSprite != null) Destroy(circleSprite);
        if (sweepTexture != null) Destroy(sweepTexture);
        if (circleTexture != null) Destroy(circleTexture);
    }
}
#endif
