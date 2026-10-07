using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Menu panels use continuous edit-suite slides; gameplay retains its authored poses.
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
    private bool moving, captured, productionMenu;
    private Coroutine routine;
    private bool closing;
    private System.Action afterClose;
    private readonly List<ClosingPart> closingParts = new List<ClosingPart>();

    private sealed class ClosingPart
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 position, fromPosition;
        public Vector3 scale, fromScale;
        public float alpha, fromAlpha;
        public bool moving, interactable, blocksRaycasts;

        public void Sample(float progress)
        {
            if (rect == null || group == null) return;
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
            group.alpha = fromAlpha * (1f - eased);
            if (!moving) return;
            rect.anchoredPosition = Vector2.Lerp(fromPosition, position + Vector2.right * 36f, eased);
            rect.localScale = Vector3.Lerp(fromScale, Vector3.Scale(scale, new Vector3(.985f, .985f, 1f)), eased);
        }

        public void Restore()
        {
            if (group != null) { group.alpha = alpha; group.interactable = interactable; group.blocksRaycasts = blocksRaycasts; }
            if (moving && rect != null) { rect.anchoredPosition = position; rect.localScale = scale; }
        }
    }

    public static void SetVisible(GameObject target, bool visible)
    {
        if (target == null) return;
        if (visible) Show(target); else Hide(target);
    }

    public static void Show(GameObject target)
    {
        if (target == null) return;
        if (!Application.isPlaying) { target.SetActive(true); return; }
        if (target.GetComponent<RectTransform>() != null && target.GetComponent<UITransition>() == null)
            target.AddComponent<UITransition>();
        target.SetActive(true);
        var transition = target.GetComponent<UITransition>();
        if (transition != null && transition.closing) transition.Play();
        UIMotionInstaller.DecorateButtons(target);
    }

    public static bool IsClosing(GameObject target)
    {
        if (target == null) return false;
        foreach (var transition in target.GetComponentsInParent<UITransition>(true))
            if (transition.closing) return true;
        return false;
    }

    public static void Hide(GameObject target, System.Action completed = null)
    {
        if (target == null) { completed?.Invoke(); return; }
        if (!Application.isPlaying || !IsProductionMenu(target) || !target.activeInHierarchy || target.GetComponent<RectTransform>() == null)
        {
            target.SetActive(false); completed?.Invoke(); return;
        }
        if (IsClosing(target)) return;
        var transition = target.GetComponent<UITransition>();
        if (transition == null)
        {
            transition = target.AddComponent<UITransition>();
            // Adding an enabled component starts its entrance. Close from the
            // existing visible layout instead of that new entrance's first pose.
            if (transition.routine != null) transition.StopCoroutine(transition.routine);
            transition.routine = null; transition.Restore();
        }
        transition.BeginClose(completed);
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
        bool menu = IsProductionMenu(target);
        transition.duration = menu ? .42f : .48f;
        transition.slideDistance = menu ? 48f : 75f;
        transition.entranceScale = menu ? .985f : .88f;
        if (transition.isActiveAndEnabled) transition.Play();
    }

    internal static bool IsProductionMenu(GameObject target) =>
        target.scene.name == "Main Menu" || target.scene.name == "Account";

    private void OnEnable() { Play(); }

    private void Play()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        CancelClose();
        Restore();
        rect = transform as RectTransform;
        if (rect == null) return;
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        alpha = group.alpha;
        scale = rect.localScale;
        position = rect.anchoredPosition;
        productionMenu = IsProductionMenu(gameObject);
        // Root canvases and layout-controlled rows must retain their authored geometry.
        moving = GetComponent<Canvas>() == null && GetComponent<Animator>() == null &&
            (transform.parent == null || transform.parent.GetComponent<LayoutGroup>() == null);
        captured = true;
        routine = StartCoroutine(Enter());
    }

    private ClosingPart CaptureClosingPart()
    {
        var currentRect = transform as RectTransform;
        var currentGroup = GetComponent<CanvasGroup>();
        if (currentGroup == null) currentGroup = gameObject.AddComponent<CanvasGroup>();
        Vector2 currentPosition = currentRect.anchoredPosition;
        Vector3 currentScale = currentRect.localScale;
        float currentAlpha = currentGroup.alpha;
        if (routine != null) StopCoroutine(routine);
        routine = null;
        CancelClose();
        var part = new ClosingPart
        {
            rect = currentRect, group = currentGroup,
            position = captured ? position : currentRect.anchoredPosition,
            scale = captured ? scale : currentRect.localScale,
            alpha = captured ? alpha : currentGroup.alpha,
            fromPosition = currentPosition, fromScale = currentScale, fromAlpha = currentAlpha,
            moving = captured ? moving : GetComponent<Canvas>() == null && GetComponent<Animator>() == null &&
                (transform.parent == null || transform.parent.GetComponent<LayoutGroup>() == null),
            interactable = currentGroup.interactable, blocksRaycasts = currentGroup.blocksRaycasts
        };
        captured = false;
        return part;
    }

    private void BeginClose(System.Action completed)
    {
        var transitions = GetComponentsInChildren<UITransition>();
        foreach (var transition in transitions)
        {
            if (!transition.isActiveAndEnabled || !(transition.transform is RectTransform)) continue;
            var part = transition.CaptureClosingPart();
            closingParts.Add(part);
        }
        // The window continues catching pointer hits while its controls are locked.
        var rootGroup = GetComponent<CanvasGroup>();
        rootGroup.interactable = false;
        closing = true;
        afterClose = completed;
        routine = StartCoroutine(Exit());
    }

    private IEnumerator Exit()
    {
        float elapsed = 0f;
        const float closingDuration = .24f;
        while (elapsed < closingDuration)
        {
            foreach (var part in closingParts) part.Sample(elapsed / closingDuration);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        var completed = afterClose;
        afterClose = null; closing = false; routine = null;
        gameObject.SetActive(false);
        completed?.Invoke();
    }

    private void CancelClose()
    {
        foreach (var part in closingParts) part.Restore();
        closingParts.Clear();
        closing = false;
        afterClose = null;
    }

    private IEnumerator Enter()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (group == null || rect == null) break;
            ApplyPose(elapsed / Mathf.Max(.01f, duration));
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        Restore();
        routine = null;
    }

    private void ApplyPose(float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (productionMenu)
        {
            // Fast initial travel, a slow clean settle, and a shorter crossfade.
            // No held frames, overshoot or asymmetric scaling of the artwork.
            float remaining = Mathf.Pow(1f - progress, 4f);
            group.alpha = alpha * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / .7f));
            if (moving)
            {
                float size = Mathf.Lerp(Mathf.Max(.985f, entranceScale), 1f, 1f - remaining);
                rect.localScale = Vector3.Scale(scale, new Vector3(size, size, 1f));
                rect.anchoredPosition = position + Vector2.right * (Mathf.Min(slideDistance, 48f) * remaining);
            }
            return;
        }
        int frame = Mathf.Min(Travel.Length - 1, Mathf.FloorToInt(progress * Travel.Length));
        float eased = Travel[frame];
        group.alpha = alpha * Opacity[frame];
        if (moving)
        {
            float size = Mathf.LerpUnclamped(entranceScale, 1f, eased);
            rect.localScale = Vector3.Scale(scale, new Vector3(size * Shape[frame].x, size * Shape[frame].y, 1f));
            rect.anchoredPosition = position + Vector2.down * (slideDistance * (1f - eased));
        }
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
        // A parent/scene may hide this window before its animation finishes.
        // Complete the close once so its owner's input/state cleanup still runs.
        var completed = afterClose;
        CancelClose();
        Restore();
        completed?.Invoke();
    }
}
