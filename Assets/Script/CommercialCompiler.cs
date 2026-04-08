using UnityEngine;
using System.Collections.Generic;

public class CommercialCompiler : MonoBehaviour
{
    public TruePixelPlayer editorPlayer;
    public Transform videoTimelineContainer;
    public Transform[] brandingTracks;

    public void PlayTimelineSequence(bool useFadeIn = false)
    {
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
            RectTransform rt = clip.GetComponent<RectTransform>();
            // Bulletproof math to find the true left edge
            float trueStartX = rt.anchoredPosition.x - (rt.rect.width * rt.pivot.x);

            sequence.Add(new ClipSegment
            {
                path = clip.clipFilePath,
                startFrame = clip.startFrame,
                endFrame = clip.endFrame,

                // --- THE FIX: Send the UI dimensions to the player ---
                uiStartX = trueStartX,
                uiWidth = rt.rect.width
            });
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

        editorPlayer.PlaySequence(sequence, useFadeIn);

        Debug.Log("Compiler: Sequence sent to Player. Clips: " + sortedClips.Count);

        // --- NEW TUTORIAL PING ---
        if (EditorTutorialManager.Instance != null) EditorTutorialManager.Instance.OnTimelinePlayed();
    }
}