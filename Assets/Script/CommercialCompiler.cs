using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class CommercialCompiler : MonoBehaviour
{
    public TruePixelPlayer editorPlayer;
    public Transform videoTimelineContainer;
    public Transform[] brandingTracks;
    private readonly List<TMPro.TMP_Text> transportLabels = new List<TMPro.TMP_Text>();
    private readonly List<UnityEngine.UI.Text> legacyTransportLabels = new List<UnityEngine.UI.Text>();
    private string playingTimeline;
    private readonly List<GameObject> redundantTransportButtons = new List<GameObject>();

    private void Start()
    {
        // Keep the authored Play button, including tutorial references and click wiring.
        foreach (var button in FindObjectsOfType<UnityEngine.UI.Button>(true))
        {
            // Legacy scene buttons are wired directly, not assigned to the player's UI fields.
            bool primary = false;
            bool legacyTransport = false;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                var target = button.onClick.GetPersistentTarget(i);
                var method = button.onClick.GetPersistentMethodName(i);
                primary |= target == this && method == nameof(PlayTimelineSequence);
                legacyTransport |= editorPlayer != null && target == editorPlayer && method == nameof(TruePixelPlayer.TogglePlayPause);
            }
            if (legacyTransport && !primary)
            {
                redundantTransportButtons.Add(button.gameObject);
                button.gameObject.SetActive(false);
                continue;
            }
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentTarget(i) == this && button.onClick.GetPersistentMethodName(i) == nameof(PlayTimelineSequence))
                {
                    transportLabels.AddRange(button.GetComponentsInChildren<TMPro.TMP_Text>(true));
                    legacyTransportLabels.AddRange(button.GetComponentsInChildren<UnityEngine.UI.Text>(true));
                    break;
                }
        }
    }

    private void LateUpdate()
    {
        if (editorPlayer == null) return;
        foreach (var button in redundantTransportButtons)
            if (button != null && button.activeSelf) button.SetActive(false);
        editorPlayer.HideSeparateTransport = true;
        if (editorPlayer.playButtonUI != null) editorPlayer.playButtonUI.SetActive(false);
        if (editorPlayer.pauseButtonUI != null) editorPlayer.pauseButtonUI.SetActive(false);
        string caption = editorPlayer.IsLoading ? "Loading…" : editorPlayer.IsPlayingVideo ? "Pause" : "Play";
        foreach (var label in transportLabels) if (label != null) label.text = caption;
        foreach (var label in legacyTransportLabels) if (label != null) label.text = caption;
    }

    private string TimelineSignature()
    {
        if (videoTimelineContainer == null) return "";
        var value = new System.Text.StringBuilder();
        foreach (var clip in videoTimelineContainer.GetComponentsInChildren<DraggableClip>())
            if (clip.isOnTimeline) value.Append(clip.GetInstanceID()).Append(':').Append(clip.clipFilePath)
                .Append(':').Append(clip.startFrame).Append(':').Append(clip.endFrame)
                .Append(':').Append(GokeSequence.Left(clip)).Append(':').Append(clip.GetComponent<RectTransform>().rect.width).Append(';');
        return value.ToString();
    }

    public bool SeekTimeline(float seconds)
    {
        if (editorPlayer == null || videoTimelineContainer == null) return false;
        var clips = new List<DraggableClip>(videoTimelineContainer.GetComponentsInChildren<DraggableClip>());
        clips.Sort((a,b) => GokeSequence.Left(a).CompareTo(GokeSequence.Left(b)));
        var sequence = new List<ClipSegment>();
        foreach (var clip in clips)
        {
            if (!clip.isOnTimeline || clip.endFrame <= clip.startFrame || string.IsNullOrEmpty(clip.clipFilePath) || !File.Exists(clip.clipFilePath)) continue;
            sequence.Add(new ClipSegment { path = clip.clipFilePath, startFrame = clip.startFrame, endFrame = clip.endFrame,
                uiStartX = GokeSequence.Left(clip), uiWidth = clip.GetComponent<RectTransform>().rect.width,
                sourceClip = clip, useClipGrade = true,
                brightness = clip.gradeBrightness, contrast = clip.gradeContrast, saturation = clip.gradeSaturation });
        }
        return editorPlayer.SeekTimeline(sequence, Mathf.Max(0,seconds));
    }

    public void PlayTimelineSequence(bool useFadeIn = false)
    {
        if (editorPlayer != null && editorPlayer.IsLoading) return;
        string signature = TimelineSignature();
        var lesson = EditorTutorialManager.Instance;
        bool startTutorialPreview = lesson != null && lesson.NeedsTutorialPreviewStart;
        if (!startTutorialPreview && editorPlayer != null && editorPlayer.CanResumeTimeline && signature == playingTimeline)
        {
            editorPlayer.TogglePlayPause();
            if (editorPlayer.IsPlayingVideo && lesson != null && lesson.gameObject.activeInHierarchy)
                lesson.OnTimelinePlayed(editorPlayer);
            return;
        }
        if (editorPlayer == null || videoTimelineContainer == null)
        {
            Debug.LogError("Compiler: Missing Player or Timeline references!");
            return;
        }

        List<ClipSegment> sequence = new List<ClipSegment>();
        DraggableClip[] clips = videoTimelineContainer.GetComponentsInChildren<DraggableClip>();

        if (clips.Length == 0)
        {
            Debug.LogWarning("Compiler: No clips found on timeline!");
            return;
        }

        List<DraggableClip> sortedClips = new List<DraggableClip>(clips);
        sortedClips.Sort((a, b) => a.transform.localPosition.x.CompareTo(b.transform.localPosition.x));

        foreach (var clip in sortedClips)
        {
            if (!IsPlayableClip(clip))
            {
                Debug.LogWarning("Compiler: Skipped a missing, damaged, or empty timeline clip.");
                continue;
            }

            RectTransform rt = clip.GetComponent<RectTransform>();
            // Bulletproof math to find the true left edge
            float trueStartX = rt.anchoredPosition.x - (rt.rect.width * rt.pivot.x);

            sequence.Add(new ClipSegment
            {
                path = clip.clipFilePath,
                startFrame = clip.startFrame,
                endFrame = clip.endFrame,
                sourceClip = clip, useClipGrade = true,
                brightness = clip.gradeBrightness, contrast = clip.gradeContrast, saturation = clip.gradeSaturation,

                // --- THE FIX: Send the UI dimensions to the player ---
                uiStartX = trueStartX,
                uiWidth = rt.rect.width
            });
        }

        if (sequence.Count == 0)
        {
            Debug.LogWarning("Compiler: No playable clips found on timeline!");
            if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
            {
                EditorTutorialManager.Instance.ShowWarning("The timeline does not contain a playable recording. Use a valid recorded clip before pressing Play.");
            }
            return;
        }

        editorPlayer.StopTape();

        DraggableOverlay[] overlays = FindObjectsOfType<DraggableOverlay>();
        foreach (var overlay in overlays)
        {
            if (overlay.isOnTimeline)
            {
                CanvasGroup cg = overlay.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
            }
        }

        editorPlayer.PlaySequence(sequence, false);
        playingTimeline = signature;

        Debug.Log("Compiler: Sequence sent to Player. Clips: " + sortedClips.Count);

        if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
        {
            EditorTutorialManager.Instance.OnTimelinePlayed(editorPlayer);
        }
    }

    private bool IsPlayableClip(DraggableClip clip)
    {
        if (clip == null || string.IsNullOrEmpty(clip.clipFilePath) || !File.Exists(clip.clipFilePath)) return false;
        if (clip.endFrame <= clip.startFrame) return false;

        try
        {
            using (BinaryReader reader = new BinaryReader(new FileStream(clip.clipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                if (reader.BaseStream.Length < sizeof(int)) return false;
                int frameCount = reader.ReadInt32();
                if (frameCount <= 0 || clip.startFrame >= frameCount) return false;

                for (int i = 0; i < frameCount; i++)
                {
                    if (reader.BaseStream.Position + sizeof(int) > reader.BaseStream.Length) return false;

                    int frameSize = reader.ReadInt32();
                    if (frameSize <= 0 || reader.BaseStream.Position + frameSize > reader.BaseStream.Length) return false;
                    reader.BaseStream.Seek(frameSize, SeekOrigin.Current);
                }

                return true;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Compiler Tape Error: " + e.Message);
            return false;
        }
    }
}
