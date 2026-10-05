using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    private readonly List<RectTransform> papers = new List<RectTransform>();

    public static void AddLobbyBackdrop(Transform canvas)
    {
        if (canvas.Find("Lobby paper backdrop") != null) return;
        var root = new GameObject("Lobby paper backdrop", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas, false);
        root.transform.SetAsFirstSibling();
        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var background = root.GetComponent<Image>();
        ExportUIArt.Apply(background, "feedbackPaper1");
        background.color = new Color32(151, 108, 69, 255);
        background.raycastTarget = false;
        // Only the decorative papers animate: the lobby's buttons and artwork stay still.
        root.AddComponent<MainMenuMotion>();
    }

    private void BuildPapers()
    {
        if (papers.Count != 0) return;
        var layer = new GameObject("Drifting production papers", typeof(RectTransform), typeof(CanvasGroup), typeof(RectMask2D));
        layer.transform.SetParent(transform, false);
        layer.transform.SetAsFirstSibling();
        var area = layer.GetComponent<RectTransform>();
        area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
        area.offsetMin = area.offsetMax = Vector2.zero;
        var group = layer.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false; group.alpha = .9f;
        for (int i = 0; i < 7; i++)
        {
            var leaf = new GameObject("Script page " + i, typeof(RectTransform), typeof(Image));
            leaf.transform.SetParent(area, false);
            var rect = leaf.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(95 + i * 5, 70 + i * 4);
            var graphic = leaf.GetComponent<Image>(); ExportUIArt.Apply(graphic, "feedbackPaper1");
            graphic.raycastTarget = false;
            graphic.color = new Color32(255, 223, 170, 255);
            var outline = leaf.AddComponent<Outline>();
            outline.effectColor = new Color32(49, 26, 14, 255);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            for (int line = 0; line < 15; line++)
            {
                var mark = new GameObject("Pen stroke", typeof(RectTransform), typeof(Image)); mark.transform.SetParent(rect, false);
                var r = mark.GetComponent<RectTransform>();
                float x = .14f + line % 3 * .25f;
                float y = .78f - line / 3 * .135f;
                r.anchorMin = r.anchorMax = new Vector2(x, y);
                r.sizeDelta = new Vector2(8 + (line * 7 + i * 3) % 15, 1.2f);
                r.localRotation = Quaternion.Euler(0, 0, (line * 13 + i * 7) % 23 - 11);
                var ink = mark.GetComponent<Image>(); ink.color = new Color(.20f,.10f,.045f,.85f); ink.raycastTarget = false;
            }
            papers.Add(rect);
        }
    }

    private void AnimatePapers()
    {
        var area = transform as RectTransform;
        if (area == null) return;
        float width = area.rect.width, height = area.rect.height;
        for (int i = 0; i < papers.Count; i++)
        {
            float t = Mathf.Repeat(elapsed / (26 + i * 1.7f) + i / 7f, 1);
            // Floor-only movement: recycle outside the viewport, never overhead.
            float side = i % 2 == 0 ? -1 : 1;
            var paper = papers[i];
            float floor = -height * .5f + height * (.025f + i % 3 * .008f);
            float x = side * Mathf.Lerp(-width * .5f - paper.sizeDelta.x,
                width * .5f + paper.sizeDelta.x, t);
            float roll = t * Mathf.PI * 12 + i;
            float angle = side * (12 + 9 * Mathf.Sin(roll * .5f));
            float scaleX = .88f;
            // Flattened perspective with small edge lifts, like a sheet on a floor.
            float scaleY = .32f + .18f * Mathf.Abs(Mathf.Sin(roll));
            float radians = angle * Mathf.Deg2Rad;
            float extent = (Mathf.Abs(Mathf.Sin(radians)) * paper.sizeDelta.x * scaleX
                + Mathf.Abs(Mathf.Cos(radians)) * paper.sizeDelta.y * scaleY) * .5f;
            float y = floor + extent + 3 * Mathf.Abs(Mathf.Sin(roll));
            paper.anchoredPosition = new Vector2(x, y);
            paper.localRotation = Quaternion.Euler(0, 0, angle);
            paper.localScale = new Vector3(scaleX, scaleY, 1);
        }
    }
    private static readonly float[] EntranceFrames = { 0f, .18f, .4f, .66f, .86f, 1.06f, 1.025f, .99f, 1f };
    private static readonly float[] TitleFrames = { 0f, 2f, 5f, 8f, 10f, 11f, 10f, 8f, 5f, 2f, 0f, -2f, -4f, -5f, -4f, -2f };

    private void OnEnable()
    {
        parts.Clear();
        elapsed = 0f;
        // Paper decoration removed; retain the authored menu entrance animation.
        var oldPapers = transform.Find("Drifting production papers");
        if (oldPapers != null) oldPapers.gameObject.SetActive(false);
        PaperMenuAudio.Play(false);
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

// Short synthesized paper textures: no external audio files or downloads required.
public static class PaperMenuAudio
{
    private static AudioSource source;
    private static AudioClip rustle, cover;
    private static float lastPlayed = -10;
    public static void Play(bool closing)
    {
        if (!Application.isPlaying || GameOptions.SfxVolume <= 0 || Time.unscaledTime - lastPlayed < .12f) return;
        lastPlayed = Time.unscaledTime;
        if (source == null)
        {
            var host = new GameObject("Paper UI sound"); Object.DontDestroyOnLoad(host);
            source = host.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.spatialBlend = 0; source.ignoreListenerPause = true;
        }
        if (rustle == null) rustle = Create(false);
        if (cover == null) cover = Create(true);
        source.volume = .22f * GameOptions.SfxVolume;
        source.PlayOneShot(closing ? cover : rustle);
    }
    private static AudioClip Create(bool closing)
    {
        const int rate = 22050; int count = (int)(rate * (closing ? .22f : .34f));
        var data = new float[count]; var random = new System.Random(closing ? 43 : 17); float low = 0;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate, p = i / (float)count;
            float noise = (float)random.NextDouble() * 2 - 1; low += (noise - low) * .16f;
            float envelope = Mathf.Sin(Mathf.PI * p); envelope *= envelope;
            data[i] = envelope * ((noise - low) * .20f + low * .65f) * (.65f + .35f * Mathf.Sin(t * 95));
            if (closing) data[i] += Mathf.Sin(t * 2 * Mathf.PI * 110) * Mathf.Exp(-t * 35) * Mathf.Min(1,t * 300) * .3f;
        }
        var clip = AudioClip.Create(closing ? "Book cover tap" : "Paper leaf rustle", count, 1, rate, false); clip.SetData(data, 0); return clip;
    }
}
