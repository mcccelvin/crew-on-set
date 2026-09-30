using UnityEngine;

public sealed class StudioShowcaseRig : MonoBehaviour
{
    [Tooltip("Optional camera angles, played in order. Leave empty for automatic stage and shop views.")]
    public Camera[] shots;
    [Min(1f)] public float secondsPerShot = 3f;
    private void Awake()
    {
        if (shots == null) return;
        foreach (var shot in shots) if (shot != null) shot.enabled = false;
    }
}
