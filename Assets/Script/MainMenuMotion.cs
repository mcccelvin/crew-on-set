using System.Collections.Generic;
using UnityEngine;

// Animates the authored menu containers, leaving child button feedback independent.
[DisallowMultipleComponent]
public sealed class MainMenuMotion : MonoBehaviour
{
    private sealed class Part
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 position;
        public float alpha, delay;
        public bool title;
    }
    private readonly List<Part> parts = new List<Part>();
    private float elapsed;
    private static readonly float[] EntranceFrames = { 0f, .18f, .4f, .66f, .86f, 1.06f, 1.025f, .99f, 1f };
    private static readonly float[] TitleFrames = { 0f, 2f, 5f, 8f, 10f, 11f, 10f, 8f, 5f, 2f, 0f, -2f, -4f, -5f, -4f, -2f };

    private void OnEnable()
    {
        parts.Clear();
        elapsed = 0f;
        Add("design", 0f, true);
        Add("play", .1f, false);
        Add("option", .17f, false);
        Add("credits", .24f, false);
        Add("exit", .31f, false);
        Add("account", .38f, false);
        Animate();
    }

    private void Add(string path, float delay, bool title)
    {
        var rect = transform.Find(path) as RectTransform;
        if (rect == null) return;
        var group = EnsureGroup(rect);
        if (group == null) return;
        parts.Add(new Part { rect = rect, group = group, position = rect.anchoredPosition,
            alpha = group.alpha, delay = delay, title = title });
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        Animate();
    }

    private static CanvasGroup EnsureGroup(RectTransform rect)
    {
        if (rect == null) return null;
        // Unity's missing-component wrappers require its overloaded null check, not ??.
        var group = rect.GetComponent<CanvasGroup>();
        if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
        return group;
    }

    private void Animate()
    {
        foreach (var part in parts)
        {
            if (part.rect == null) continue;
            if (part.group == null) part.group = EnsureGroup(part.rect);
            if (part.group == null) continue;
            int frame = Mathf.Clamp(Mathf.FloorToInt((elapsed - part.delay) * 18f), 0, EntranceFrames.Length - 1);
            float ease = EntranceFrames[frame];
            part.group.alpha = part.alpha * Mathf.Clamp01(ease);
            Vector2 entrance = part.title ? new Vector2(-80f, -30f) : new Vector2(90f, 0f);
            int idleFrame = Mathf.FloorToInt(Mathf.Max(0f, elapsed - .75f) * 8f) % TitleFrames.Length;
            Vector2 drift = part.title ? Vector2.up * TitleFrames[idleFrame] : Vector2.zero;
            part.rect.anchoredPosition = part.position + entrance * (1f - ease) + drift;
        }
    }

    private void OnDisable()
    {
        foreach (var part in parts)
        {
            if (part.rect != null) part.rect.anchoredPosition = part.position;
            if (part.group != null) part.group.alpha = part.alpha;
        }
        parts.Clear();
    }
}
