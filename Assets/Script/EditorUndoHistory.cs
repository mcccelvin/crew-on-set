using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Capture before the EventSystem edits UI; commit once a complete gesture finishes.
[DefaultExecutionOrder(-9000)]
public sealed class EditorUndoHistory : MonoBehaviour
{
    private sealed class Entry
    {
        public List<Action> actions;
        public bool split, tutorial;
        public EditorTutorialManager.EditorStep step;
        public DraggableClip selected;
    }
    private const int Capacity = 64;
    private readonly List<Entry> history = new List<Entry>();
    private EditorManager editor;
    private EditorEditSnapshot pending;
    private Entry pendingEntry;
    private bool suppressFrame;
    private void Awake() { editor = GetComponent<EditorManager>(); }

    private static bool TextFocused()
    {
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null) return false;
        var tmp = selected.GetComponentInParent<TMPro.TMP_InputField>();
        var legacy = selected.GetComponentInParent<InputField>();
        return tmp != null && tmp.isFocused || legacy != null && legacy.isFocused;
    }
    private bool Available()
    {
        if (editor == null || !editor.gameObject.activeInHierarchy || editor.timelineContainer == null ||
            !editor.timelineContainer.gameObject.activeInHierarchy || !Application.isFocused || PauseManager.isPaused || editor.ReviewIsOpen) return false;
        if (TutorialUIManager.Instance != null && TutorialUIManager.Instance.IsBossDialogueOpen()) return false;
        if (ContractUIManager.Instance != null && ContractUIManager.Instance.IsQualificationsOpen()) return false;
        var lesson = EditorTutorialManager.Instance;
        return lesson == null || !lesson.RestrictsEditor || lesson.AcceptsTaskInput;
    }
    private void Update()
    {
        suppressFrame = false;
        var mouse = Mouse.current; var keyboard = Keyboard.current;
        bool held = mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed);
        bool released = mouse != null && (mouse.leftButton.wasReleasedThisFrame || mouse.rightButton.wasReleasedThisFrame);
        bool undo = keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) && keyboard.zKey.wasPressedThisFrame;
        if (undo && Available() && !TextFocused() && !held && !released)
        {
            Commit(); Undo(); suppressFrame = true; return;
        }
        if (pending != null || !Available()) return;
        bool pressed = mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
        pressed |= keyboard != null && keyboard.anyKey.wasPressedThisFrame && !undo;
        if (!pressed) return;
        pending = EditorEditSnapshot.Capture(editor);
        var lesson = EditorTutorialManager.Instance;
        pendingEntry = new Entry { tutorial = lesson != null && lesson.RestrictsEditor,
            step = lesson != null ? lesson.currentStep : default, selected = pending.selected };
    }
    private void LateUpdate()
    {
        if (suppressFrame || pending == null || TextFocused()) return;
        var mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed)) return;
        Commit();
    }
    private void Commit()
    {
        if (pending == null || editor == null) return;
        var after = EditorEditSnapshot.Capture(editor);
        pendingEntry.actions = pending.ChangesTo(after);
        pendingEntry.split = after.splitCount > pending.splitCount;
        pending = null;
        if (pendingEntry.actions.Count != 0)
        {
            history.Add(pendingEntry);
            if (history.Count > Capacity) history.RemoveAt(0);
            EditorTutorialManager.Instance?.OnEditorEditRecorded();
        }
        pendingEntry = null;
    }
    private void Undo()
    {
        if (history.Count == 0) { GameFeedback.Show("NOTHING TO UNDO"); return; }
        Entry entry = history[history.Count - 1];
        var lesson = EditorTutorialManager.Instance;
        // Don't undo a completed required task and leave the lesson stranded.
        if (lesson != null && lesson.RestrictsEditor && (!entry.tutorial || entry.step != lesson.currentStep))
        { GameFeedback.Show("TRY UNDO DURING THE CURRENT EDITING TASK"); return; }
        history.RemoveAt(history.Count - 1);
        var compiler = FindObjectOfType<CommercialCompiler>();
        var player = compiler != null ? compiler.editorPlayer : null;
        float seconds = player != null && player.playheadLine != null ? player.playheadLine.anchoredPosition.x / EditorRectState.PixelsPerSecond : 0f;
        player?.StopTape();
        foreach (var action in entry.actions) action?.Invoke();
        if (entry.split) DraggableClip.NotifySplitUndone(entry.selected);
        editor.gradingManager?.RefreshAfterUndo();
        PlayerEditTools.Instance?.RefreshAfterUndo();
        ClipInspector.Instance?.RefreshAfterUndo();
        TimelineManager.Instance?.RefreshTimeline();
        player?.RefreshOverlays();
        compiler?.SeekTimeline(seconds);
        GameFeedback.Show(entry.split ? "SPLIT UNDONE" : "EDIT UNDONE");
        lesson?.OnEditorUndo();
    }
    private void OnDisable() { pending = null; pendingEntry = null; }
}
