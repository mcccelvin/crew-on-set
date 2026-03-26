using UnityEngine;
using System.Collections.Generic;

public class CommercialCompiler : MonoBehaviour
{
    [Header("References")]
    public Transform timelineDropZone;
    public TruePixelPlayer editorPlayer;

    public void PlayTimelineSequence()
    {
        Debug.Log("COMPILER: Play button was clicked!"); // ADDED THIS

        if (timelineDropZone == null)
        {
            Debug.LogError("COMPILER ERROR: Timeline Drop Zone is missing!");
            return;
        }

        DraggableClip[] clipsInTimeline = timelineDropZone.GetComponentsInChildren<DraggableClip>();
        if (clipsInTimeline.Length == 0)
        {
            Debug.LogWarning("COMPILER: Timeline is empty! Cannot play.");
            return;
        }

        List<ClipSegment> sequence = new List<ClipSegment>();
        foreach (DraggableClip clip in clipsInTimeline)
        {
            sequence.Add(new ClipSegment
            {
                path = clip.clipFilePath,
                startFrame = clip.startFrame,
                endFrame = clip.endFrame
            });
        }

        Debug.Log($"COMPILER: Sending {sequence.Count} clips to the Video Player!");

        if (editorPlayer != null) editorPlayer.PlaySequence(sequence);
        else Debug.LogError("COMPILER ERROR: Editor Player slot is empty!");
    }
}