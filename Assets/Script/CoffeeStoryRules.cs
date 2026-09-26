using System.Collections.Generic;
using UnityEngine;

// Level 4 only: cut between three different moments instead of repeating a pose.
public static class CoffeeStoryRules
{
    public static int Beat(string pose) => pose == "Wave" ? 1 :
        pose == "Action" || pose == "Using Machine" ? 2 : pose == "Sitting" ? 3 : 0;

    public struct Result { public bool beats, continuous, duration, brand; }

    public static Result Evaluate(Transform timeline, float pixelsPerSecond)
    {
        var result = new Result();
        if (timeline == null || pixelsPerSecond <= 0f) return result;
        var clips = new List<DraggableClip>(timeline.GetComponentsInChildren<DraggableClip>(true));
        clips.RemoveAll(c => !c.isOnTimeline);
        clips.Sort((a, b) => GokeSequence.Left(a).CompareTo(GokeSequence.Left(b)));
        result.beats = clips.Count == 3;
        result.continuous = clips.Count > 0;
        var sources = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        float end = 0f;
        for (int i = 0; i < clips.Count; i++)
        {
            var clip = clips[i];
            float start = GokeSequence.Left(clip) / pixelsPerSecond;
            float seconds = (clip.endFrame - clip.startFrame) / TapeSettings.framesPerSecond;
            result.beats &= clip.campaignLevel == 4 && Beat(clip.actorPose) == i + 1 &&
                clip.requiredSubjectsVisible && !string.IsNullOrEmpty(clip.clipFilePath) && sources.Add(clip.clipFilePath);
            result.continuous &= Mathf.Abs(start - end) <= .05f && seconds >= 2f &&
                clip.startFrame >= 0 && clip.endFrame <= clip.totalFrames;
            end = start + seconds;
        }
        result.duration = Mathf.Abs(end - 15f) <= .5f;
        foreach (var graphic in Object.FindObjectsOfType<BrandingClip>(true))
        {
            var overlay = graphic.linkedOverlay;
            if (overlay != null && overlay.isOnTimeline &&
                overlay.startFrame / TapeSettings.framesPerSecond <= end - 2f + .05f &&
                overlay.endFrame / TapeSettings.framesPerSecond >= end - .05f)
                result.brand = true;
        }
        return result;
    }
}

