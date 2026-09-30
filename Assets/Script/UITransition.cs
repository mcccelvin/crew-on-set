using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Short entrance transitions; closing stays immediate so existing input gates remain accurate.
[DisallowMultipleComponent]
public sealed class UITransition : MonoBehaviour
{
    [Range(.15f, .8f)] public float duration = .44f;
    public float slideDistance = 55f;
    [Range(.8f, 1f)] public float entranceScale = .9f;
    // Eight held poses: approach, overshoot, squash and settle.
    private static readonly float[] Travel = { 0f, .24f, .55f, .83f, 1.07f, 1.025f, .99f, 1f };
    private static readonly float[] Opacity = { 0f, .3f, .65f, .9f, 1f, 1f, 1f, 1f };
    private static readonly Vector2[] Shape = {
        new Vector2(1.03f,.94f), new Vector2(.98f,1.03f), new Vector2(.98f,1.03f),
        new Vector2(1f,1f), new Vector2(1.025f,.98f), new Vector2(.99f,1.015f),
        new Vector2(1.005f,.997f), Vector2.one };
    private CanvasGroup group;
    private RectTransform rect;
    private Vector3 scale;
    private Vector2 position;
    private float alpha;
    private bool moving, captured;
    private Coroutine routine;

    public static void SetVisible(GameObject target, bool visible)
    {
        if (target == null) return;
        if (visible) Show(target); else target.SetActive(false);
    }

    public static void Show(GameObject target)
    {
        if (target == null) return;
        if (!Application.isPlaying) { target.SetActive(true); return; }
        if (target.GetComponent<RectTransform>() != null && target.GetComponent<UITransition>() == null)
            target.AddComponent<UITransition>();
        target.SetActive(true);
        UIMotionInstaller.DecorateButtons(target);
    }

    public static void Replay(GameObject target)
    {
        if (target == null) return;
        var transition = target.GetComponent<UITransition>();
        if (transition == null) { Show(target); return; }
        if (transition.isActiveAndEnabled) transition.Play();
    }

    public static void ConfigurePanel(GameObject target)
    {
        if (target == null || !Application.isPlaying) return;
        var transition = target.GetComponent<UITransition>();
        if (transition == null) transition = target.AddComponent<UITransition>();
        transition.duration = .48f;
        transition.slideDistance = 75f;
        transition.entranceScale = .88f;
        if (transition.isActiveAndEnabled) transition.Play();
    }

    private void OnEnable() { Play(); }

    private void Play()
    {
        if (routine != null) StopCoroutine(routine);
        Restore();
        rect = transform as RectTransform;
        if (rect == null) return;
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        alpha = group.alpha;
        scale = rect.localScale;
        position = rect.anchoredPosition;
        // Root canvases and layout-controlled rows must retain their authored geometry.
        moving = GetComponent<Canvas>() == null && GetComponent<Animator>() == null &&
            (transform.parent == null || transform.parent.GetComponent<LayoutGroup>() == null);
        captured = true;
        routine = StartCoroutine(Enter());
    }

    private IEnumerator Enter()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (group == null || rect == null) break;
            int frame = Mathf.Min(Travel.Length - 1, Mathf.FloorToInt(elapsed / Mathf.Max(.01f, duration) * Travel.Length));
            float eased = Travel[frame];
            group.alpha = alpha * Opacity[frame];
            if (moving)
            {
                float size = Mathf.LerpUnclamped(entranceScale, 1f, eased);
                rect.localScale = Vector3.Scale(scale, new Vector3(size * Shape[frame].x, size * Shape[frame].y, 1f));
                rect.anchoredPosition = position + Vector2.down * (slideDistance * (1f - eased));
            }
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        Restore();
        routine = null;
    }

    private void Restore()
    {
        if (!captured) return;
        if (group != null) group.alpha = alpha;
        if (moving && rect != null) { rect.localScale = scale; rect.anchoredPosition = position; }
        captured = false;
    }

    private void OnDisable()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        Restore();
    }
}
