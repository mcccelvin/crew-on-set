using UnityEngine;

// Derived from the existing career checkpoint, including direct level/retry entry.
public static class CameraFeatureUnlocks
{
    public static int Level => CampaignProgression.GetCurrentLevel();
    public static bool ManualFocus => Level >= 2;
    public static bool WhiteBalance => Level >= 3;
    public static bool Exposure => Level >= 4;
    public static bool IsCamera(string name) => !string.IsNullOrEmpty(name) &&
        (name.ToUpperInvariant().Contains("CAMERA") || name.ToUpperInvariant().Contains("NONY FX"));
}
