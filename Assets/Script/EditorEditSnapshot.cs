using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Edit data only: playback, selection, navigation and tutorial progress are not edits.
internal sealed class EditorEditState
{
    public string key, fingerprint;
    public Action restore, remove;
    public static string Number(float value) => value.ToString("F4", CultureInfo.InvariantCulture);
    public static int Id(UnityEngine.Object value) => value == null ? 0 : value.GetInstanceID();
}

internal sealed class EditorRectState
{
    public readonly Transform parent;
    private readonly int sibling;
    private readonly Vector2 anchorsMin, anchorsMax, pivot, position, size;
    private readonly Vector3 scale;
    private readonly Quaternion rotation;
    private readonly bool timeline;
    private readonly float timeX, duration;
    public static float PixelsPerSecond => Mathf.Max(1f, TimelineManager.Instance != null ? TimelineManager.Instance.pixelsPerSecond : 40f);

    public EditorRectState(RectTransform rect, bool onTimeline, Vector2? settledPosition = null, Vector3? settledScale = null)
    {
        parent = rect.parent; sibling = rect.GetSiblingIndex();
        anchorsMin = rect.anchorMin; anchorsMax = rect.anchorMax; pivot = rect.pivot;
        position = settledPosition ?? rect.anchoredPosition; size = rect.sizeDelta;
        scale = settledScale ?? rect.localScale; rotation = rect.localRotation;
        timeline = onTimeline; timeX = position.x / PixelsPerSecond; duration = rect.rect.width / PixelsPerSecond;
    }
    public string Fingerprint(bool includeGeometry)
    {
        string result = EditorEditState.Id(parent).ToString();
        if (!includeGeometry) return result;
        return result + "/" + EditorEditState.Number(timeline ? timeX : position.x) + "/" + EditorEditState.Number(position.y) +
            "/" + EditorEditState.Number(timeline ? duration : size.x) + "/" + EditorEditState.Number(size.y) +
            "/" + EditorEditState.Number(scale.x) + "/" + EditorEditState.Number(scale.y);
    }
    public void Restore(RectTransform rect)
    {
        if (rect == null || parent == null) return;
        rect.SetParent(parent, false); rect.SetSiblingIndex(sibling);
        rect.anchorMin = anchorsMin; rect.anchorMax = anchorsMax; rect.pivot = pivot;
        rect.sizeDelta = size; rect.localScale = scale; rect.localRotation = rotation;
        rect.anchoredPosition = timeline ? new Vector2(timeX * PixelsPerSecond, position.y) : position;
        if (timeline) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, duration * PixelsPerSecond);
    }
}

internal sealed class EditorEditSnapshot
{
    private readonly Dictionary<string, EditorEditState> states = new Dictionary<string, EditorEditState>();
    public int splitCount;
    public DraggableClip selected;
    private void Add(EditorEditState state) { states[state.key] = state; }

    public static EditorEditSnapshot Capture(EditorManager editor)
    {
        var result = new EditorEditSnapshot { splitCount = DraggableClip.SplitCount, selected = DraggableClip.Selected };
        var clips = new HashSet<DraggableClip>();
        foreach (var root in new[] { editor.clipBankContainer, editor.timelineContainer })
            if (root != null) foreach (var clip in root.GetComponentsInChildren<DraggableClip>(true)) clips.Add(clip);
        foreach (var clip in clips) result.Add(clip.CaptureUndo());
        var overlays = new HashSet<DraggableOverlay>();
        Transform screen = editor.gradingManager != null && editor.gradingManager.computerScreen != null ? editor.gradingManager.computerScreen.transform : null;
        Transform bank = editor.brandingBinPanel != null ? editor.brandingBinPanel.transform : null;
        foreach (var root in new[] { bank, screen })
            if (root != null) foreach (var overlay in root.GetComponentsInChildren<DraggableOverlay>(true)) overlays.Add(overlay);
        foreach (var overlay in overlays) result.Add(overlay.CaptureUndo());
        if (editor.brandingTracks != null) foreach (var track in editor.brandingTracks)
            if (track != null) foreach (var card in track.GetComponentsInChildren<BrandingClip>(true))
                if (card.gameObject.activeSelf && card.linkedOverlay != null) result.Add(card.CaptureUndo(editor));
        if (editor.gradingManager != null) result.Add(editor.gradingManager.CaptureUndo());
        if (PlayerEditTools.Instance != null) result.Add(PlayerEditTools.Instance.CaptureUndo());
        return result;
    }

    public List<Action> ChangesTo(EditorEditSnapshot after)
    {
        var actions = new List<Action>();
        // Remove newly created cards/split pieces before restoring their sources.
        foreach (var pair in after.states)
            if (!states.ContainsKey(pair.Key) && pair.Value.remove != null) actions.Add(pair.Value.remove);
        foreach (var pair in states)
            if (!after.states.TryGetValue(pair.Key, out var later) || later.fingerprint != pair.Value.fingerprint)
                actions.Add(pair.Value.restore);
        return actions;
    }
}
