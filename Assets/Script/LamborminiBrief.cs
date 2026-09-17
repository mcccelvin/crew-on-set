using UnityEngine;

public static class LamborminiBrief
{
    public const float TargetSeconds=25f, MinimumSeconds=24.5f, MaximumSeconds=25.5f;
    public const float CardSeconds=2f;
    public const float BrightnessMin=.85f, BrightnessMax=1.15f;
    public const float ContrastMin=1.05f, ContrastMax=1.45f;
    public const float SaturationMin=.95f, SaturationMax=1.3f;
    public static bool InRange(float value,float min,float max)=>value>=min-.0001f && value<=max+.0001f;
    public const int RequiredTakes = 3;
    // Compatibility entry point for older callers; follows the current contract.
    public static bool HasTwoRecordedTakes(Transform timeline, float pixelsPerSecond) => HasRequiredRecordedTakes(timeline, pixelsPerSecond);
    public static bool HasRequiredRecordedTakes(Transform timeline, float pixelsPerSecond)
    {
        if (timeline == null || pixelsPerSecond <= 0) return false;
        var clips = new System.Collections.Generic.List<DraggableClip>(timeline.GetComponentsInChildren<DraggableClip>());
        clips.RemoveAll(c => !c.isOnTimeline || !c.gameObject.activeInHierarchy);
        if (clips.Count != RequiredTakes + 2) return false;
        clips.Sort((a,b) => GokeSequence.Left(a).CompareTo(GokeSequence.Left(b)));
        if (!IsFullCard(clips[0], ProvidedClipRole.TerrariIntro) ||
            !IsFullCard(clips[clips.Count - 1], ProvidedClipRole.TerrariOutro)) return false;
        var sources = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        float end = 0;
        int recordedTakes = 0;
        foreach (var clip in clips)
        {
            if (clip.startFrame < 0 || clip.endFrame > clip.totalFrames || clip.endFrame <= clip.startFrame ||
                string.IsNullOrWhiteSpace(clip.clipFilePath)) return false;
            if (clip.providedRole == ProvidedClipRole.None)
            {
                if (clip.campaignLevel != 3) return false;
                string source = System.IO.Path.GetFileName(clip.clipFilePath.Replace('\\','/'));
                if (!sources.Add(source)) return false;
                recordedTakes++;
            }
            float start = GokeSequence.Left(clip) / pixelsPerSecond;
            if (Mathf.Abs(start - end) > .05f) return false;
            end = start + (clip.endFrame - clip.startFrame) / TapeSettings.framesPerSecond;
        }
        return recordedTakes == RequiredTakes && InRange(end, MinimumSeconds, MaximumSeconds);
    }

    private static bool IsFullCard(DraggableClip clip, ProvidedClipRole role)
    {
        int frames = Mathf.RoundToInt(CardSeconds * TapeSettings.framesPerSecond);
        return clip.providedRole == role && clip.totalFrames == frames && clip.startFrame == 0 && clip.endFrame == frames;
    }

    public static float Composition(Vector4 bounds)
    {
        float w=bounds.z-bounds.x,h=bounds.w-bounds.y;
        if(w<=0 || h<=0)return 0;
        float visibleW=Mathf.Max(0,Mathf.Min(1,bounds.z)-Mathf.Max(0,bounds.x));
        float visibleH=Mathf.Max(0,Mathf.Min(1,bounds.w)-Mathf.Max(0,bounds.y));
        float screenArea=visibleW*visibleH;
        if(screenArea<.035f)return 0;
        // Detail shots may crop the silhouette, but need substantial visible vehicle area.
        float visibleFraction=screenArea/(w*h);
        bool detail=Mathf.Max(w,h)>.9f && screenArea>=.22f && visibleFraction>=.18f;
        if(detail)return 70f;
        float visibility=Mathf.Clamp01(visibleFraction/.98f);
        float size=Mathf.Clamp01(Mathf.Max(w,h)/.45f);
        Vector2 center=new Vector2((bounds.x+bounds.z)*.5f,(bounds.y+bounds.w)*.5f);
        float composition=Mathf.Clamp01(1-Mathf.Max(0,Mathf.Abs(center.x-.5f)-.19f)/.3f)
            *Mathf.Clamp01(1-Mathf.Max(0,Mathf.Abs(center.y-.5f)-.17f)/.3f);
        return 30*visibility+25*size+15*composition;
    }
}
