using System.Collections.Generic;
using UnityEngine;

// Shared by the lesson and contract so the task checklist matches submission.
public static class GokeSequence
{
    public const float CardSeconds = 2f;
    public const float TargetSeconds = 10f;

    public struct Result
    {
        public bool intro, outro, product, continuous, duration;
        public bool Complete => intro && outro && product && continuous && duration;
    }

    public static Result Evaluate(Transform timeline, float pixelsPerSecond)
    {
        var result = new Result();
        if (timeline == null || pixelsPerSecond <= 0f) return result;
        var clips = new List<DraggableClip>(timeline.GetComponentsInChildren<DraggableClip>());
        clips.RemoveAll(c => !c.isOnTimeline);
        clips.Sort((a, b) => Left(a).CompareTo(Left(b)));
        if (clips.Count == 0) return result;

        int intros = 0, outros = 0;
        float previousEnd = 0f, total = 0f;
        bool continuous = true, product = clips.Count >= 3;
        for (int i = 0; i < clips.Count; i++)
        {
            var clip = clips[i];
            if (clip.providedRole == ProvidedClipRole.GokeIntro) intros++;
            if (clip.providedRole == ProvidedClipRole.GokeOutro) outros++;
            float seconds = (clip.endFrame - clip.startFrame) / TapeSettings.framesPerSecond;
            float start = Left(clip) / pixelsPerSecond;
            if (Mathf.Abs(start - previousEnd) > .05f || seconds <= 0f) continuous = false;
            if (clip.startFrame < 0 || clip.endFrame > clip.totalFrames) continuous = false;
            previousEnd = start + seconds;
            total += seconds;
            if (i > 0 && i < clips.Count - 1)
                product &= clip.providedRole == ProvidedClipRole.None && clip.campaignLevel == 2 && seconds > 0f;
        }
        result.intro = intros == 1 && IsFullCard(clips[0], ProvidedClipRole.GokeIntro)
            && Mathf.Abs(Left(clips[0]) / pixelsPerSecond) <= .05f;
        result.outro = outros == 1 && IsFullCard(clips[clips.Count - 1], ProvidedClipRole.GokeOutro);
        result.product = product;
        result.continuous = continuous;
        result.duration = Mathf.Abs(total - TargetSeconds) <= .75f;
        return result;
    }

    private static bool IsFullCard(DraggableClip clip, ProvidedClipRole role)
    {
        int frames = Mathf.RoundToInt(CardSeconds * TapeSettings.framesPerSecond);
        return clip.providedRole == role && clip.totalFrames == frames
            && clip.startFrame == 0 && clip.endFrame == frames;
    }

    public static float Left(DraggableClip clip)
    {
        var rect = (RectTransform)clip.transform;
        return rect.anchoredPosition.x - rect.rect.width * rect.pivot.x;
    }
}
