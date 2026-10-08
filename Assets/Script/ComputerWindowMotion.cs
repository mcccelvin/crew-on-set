using System.Collections;
using UnityEngine;

// One continuous desktop-window expansion. Child cards keep their settled layout.
[DisallowMultipleComponent]
public sealed class ComputerWindowMotion : MonoBehaviour
{
    private RectTransform rect;
    private CanvasGroup group;
    private Vector2 settledPosition, startPosition;
    private Vector3 settledScale;
    private float settledAlpha;
    private bool settledInteractable, settledRaycasts, captured;
    private Coroutine routine;

    public static void Show(GameObject panel, RectTransform desktopIcon = null)
    {
        if (panel == null) return;
        foreach (var transition in panel.GetComponentsInChildren<UITransition>(true))
            UITransition.StopMotion(transition.gameObject);
        UITransition.ShowImmediately(panel);
        var motion = panel.GetComponent<ComputerWindowMotion>();
        if (motion == null) motion = panel.AddComponent<ComputerWindowMotion>();
        motion.Play(desktopIcon);
    }

    private void Play(RectTransform icon)
    {
        if (routine != null) StopCoroutine(routine);
        Restore();
        rect = transform as RectTransform;
        if (rect == null) return;
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        settledPosition = rect.anchoredPosition;
        settledScale = rect.localScale;
        settledAlpha = group.alpha;
        settledInteractable = group.interactable;
        settledRaycasts = group.blocksRaycasts;
        captured = true;
        Vector2 direction = new Vector2(-45, -20);
        if (icon != null && rect.parent != null)
            direction = Vector2.ClampMagnitude((Vector2)rect.parent.InverseTransformPoint(icon.TransformPoint(icon.rect.center)) - (Vector2)rect.localPosition, 600) * .1f;
        startPosition = settledPosition + direction;
        if (!Application.isPlaying) { Restore(); return; }
        group.interactable = false;
        // The window still blocks clicks on the desktop while it opens.
        group.blocksRaycasts = true;
        Sample(0);
        routine = StartCoroutine(Open());
    }

    private IEnumerator Open()
    {
        float elapsed = 0;
        while (elapsed < .28f)
        {
            elapsed += Time.unscaledDeltaTime;
            Sample(elapsed / .28f);
            yield return null;
        }
        Restore();
        routine = null;
    }

    private void Sample(float progress)
    {
        float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(progress), 3);
        rect.anchoredPosition = Vector2.Lerp(startPosition, settledPosition, t);
        rect.localScale = settledScale * Mathf.Lerp(.86f, 1f, t);
        group.alpha = settledAlpha * Mathf.Clamp01(progress * 2.5f);
    }

    private void Restore()
    {
        if (!captured) return;
        if (rect != null) { rect.anchoredPosition = settledPosition; rect.localScale = settledScale; }
        if (group != null) { group.alpha = settledAlpha; group.interactable = settledInteractable; group.blocksRaycasts = settledRaycasts; }
        captured = false;
    }

    private void OnDisable()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        Restore();
    }
}
