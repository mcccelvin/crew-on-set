using System;
using System.Collections.Generic;

// Timeline time and compiled tape time differ when clips have trims or gaps.
public static class TimelineSeekMath
{
    public static float SecondsAt(float localX, float left, float width, float pixelsPerSecond)
        => (float)(Math.Max(0, Math.Min(width, localX - left)) / Math.Max(1, pixelsPerSecond));

    public static int FrameAt(IList<ClipSegment> sequence, float seconds, float pixelsPerSecond, float fps, out bool hasPicture)
    {
        hasPicture = false;
        float x = Math.Max(0, seconds) * Math.Max(1, pixelsPerSecond);
        if (sequence == null) return 0;
        foreach (var clip in sequence)
        {
            int count = clip.globalEndFrame - clip.globalStartFrame;
            if (count <= 0 || clip.uiWidth <= 0) continue;
            if (x >= clip.uiStartX && x < clip.uiStartX + clip.uiWidth)
            {
                hasPicture = true;
                int offset = (int)Math.Round((x - clip.uiStartX) * Math.Max(1, fps) / Math.Max(1, pixelsPerSecond));
                return clip.globalStartFrame + Math.Max(0, Math.Min(count - 1, offset));
            }
            if (x < clip.uiStartX) return clip.globalStartFrame; // Resume at the next available shot in a gap.
        }
        return sequence.Count == 0 ? 0 : Math.Max(0, sequence[sequence.Count - 1].globalEndFrame - 1);
    }
}
