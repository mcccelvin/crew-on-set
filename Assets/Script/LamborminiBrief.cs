using UnityEngine;

public static class LamborminiBrief
{
    public const float MinimumSeconds=8f, MaximumSeconds=12f;
    public const float BrightnessMin=.85f, BrightnessMax=1.15f;
    public const float ContrastMin=1.05f, ContrastMax=1.45f;
    public const float SaturationMin=.95f, SaturationMax=1.3f;
    public static bool InRange(float value,float min,float max)=>value>=min-.0001f && value<=max+.0001f;
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
