using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Gate actual raycast targets as well as keyboard/controller-selectable controls.
[DefaultExecutionOrder(-10000)]
public sealed class EditorTutorialInputGate : MonoBehaviour
{
    private readonly Dictionary<Graphic, bool> graphics = new Dictionary<Graphic, bool>();
    private readonly Dictionary<Selectable, bool> controls = new Dictionary<Selectable, bool>();
    private readonly Dictionary<Button, Button.ButtonClickedEvent> buttonActions = new Dictionary<Button, Button.ButtonClickedEvent>();
    private static bool Within(Transform item, Transform root) => root != null && (item == root || item.IsChildOf(root));

    public static bool Allowed(Transform item)
    {
        var t = EditorTutorialManager.Instance;
        if (t == null || !t.RestrictsEditor || PauseManager.isPaused) return true;
        var ui = TutorialUIManager.Instance;
        if (ui != null && ui.bossHUDCanvas != null && Within(item, ui.bossHUDCanvas.transform)) return true;
        var editor = EditorManager.Instance;
        if (editor != null && editor.IsReviewBackControl(item)) return true;
        if (!t.AcceptsTaskInput) return false;
        if (editor != null && editor.gradingManager != null && editor.gradingManager.IsComparisonControl(item))
        {
            switch (t.currentStep)
            {
                case EditorTutorialManager.EditorStep.AdjustBrightness:
                case EditorTutorialManager.EditorStep.AdjustContrast:
                case EditorTutorialManager.EditorStep.AdjustSaturation:
                case EditorTutorialManager.EditorStep.ClickExport:
                    return true;
            }
        }
        var button = item.GetComponentInParent<Button>();
        if (button != null && (t.currentStep == EditorTutorialManager.EditorStep.TrimTo10Seconds || t.currentStep == EditorTutorialManager.EditorStep.CloseTrimWindow))
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentMethodName(i) == "CloseWindow") return true;
        var tools = PlayerEditTools.Instance;
        switch (t.currentStep)
        {
            case EditorTutorialManager.EditorStep.DragVideoToTimeline:
                return Within(item, t.videoBinClipRect) || Within(item, t.timelineVideoTrackRect);
            case EditorTutorialManager.EditorStep.PlayPreview:
            case EditorTutorialManager.EditorStep.PlayBrandingPreview:
            case EditorTutorialManager.EditorStep.PreviewCommercialFinish:
                return Within(item, t.playButtonRect);
            case EditorTutorialManager.EditorStep.DoubleClickToTrim:
            case EditorTutorialManager.EditorStep.PositionVideoAtStart:
            case EditorTutorialManager.EditorStep.PracticeUndo:
                return Within(item, t.timelineVideoTrackRect);
            case EditorTutorialManager.EditorStep.SeekTimeline:
                return TimelineManager.Instance != null && Within(item, TimelineManager.Instance.timestampContainer);
            case EditorTutorialManager.EditorStep.TrimLeftHandle: return Within(item, t.leftTrimHandleRect);
            case EditorTutorialManager.EditorStep.TrimRightHandle: return Within(item, t.rightTrimHandleRect);
            case EditorTutorialManager.EditorStep.TrimTo10Seconds:
                return Within(item, t.leftTrimHandleRect) || Within(item, t.rightTrimHandleRect) || Within(item, t.closeTrimWindowBtnRect);
            case EditorTutorialManager.EditorStep.CloseTrimWindow: return Within(item, t.closeTrimWindowBtnRect);
            case EditorTutorialManager.EditorStep.GoToBrandingPhase: return Within(item, t.brandingTabBtnRect);
            case EditorTutorialManager.EditorStep.DragLogoToScreen:
            case EditorTutorialManager.EditorStep.DragToOtherTimeline:
                int index = t.currentStep == EditorTutorialManager.EditorStep.DragLogoToScreen ? 0 : 1;
                Transform source = t.brandingBinClipRect;
                if (source != null && source.childCount > index) source = source.GetChild(index);
                return Within(item, source) || Within(item, t.previewScreenRect);
            case EditorTutorialManager.EditorStep.TrimBranding: return Within(item, t.brandingTimelineClipRect);
            case EditorTutorialManager.EditorStep.PositionSecondBranding: return Within(item, t.otherBrandingTrackRect);
            case EditorTutorialManager.EditorStep.ChooseCameraMotion: return tools != null && Within(item, tools.GetCameraMotionButtonRect());
            case EditorTutorialManager.EditorStep.ChooseGraphicAnimation: return tools != null && Within(item, tools.GetGraphicAnimationButtonRect());
            case EditorTutorialManager.EditorStep.ChooseTransition: return tools != null && Within(item, tools.GetTransitionButtonRect());
            case EditorTutorialManager.EditorStep.ChooseMusic: return tools != null && Within(item, tools.GetMusicButtonRect());
            case EditorTutorialManager.EditorStep.PrepareForColorGrade: return Within(item, t.colorGradeTabBtnRect);
            case EditorTutorialManager.EditorStep.AdjustBrightness: return Within(item, t.brightnessSliderRect);
            case EditorTutorialManager.EditorStep.AdjustContrast: return Within(item, t.contrastSliderRect);
            case EditorTutorialManager.EditorStep.AdjustSaturation: return Within(item, t.saturationSliderRect);
            case EditorTutorialManager.EditorStep.ClickExport: return Within(item, t.exportButtonRect);
            case EditorTutorialManager.EditorStep.ReviewAndSubmit: return Within(item, t.submitButtonRect);
            default: return false;
        }
    }

    private void Update()
    {
        Restore();
        var tutorial = EditorTutorialManager.Instance;
        if (tutorial == null || !tutorial.RestrictsEditor || PauseManager.isPaused) return;
        foreach (var button in FindObjectsOfType<Button>())
        {
            if (Allowed(button.transform)) continue;
            var original = button.onClick;
            bool closeInspector = false;
            for (int i = 0; i < original.GetPersistentEventCount(); i++)
                if (original.GetPersistentMethodName(i) == "CloseWindow" && original.GetPersistentTarget(i) is ClipInspector)
                    closeInspector = tutorial.AcceptsTaskInput && (tutorial.currentStep == EditorTutorialManager.EditorStep.TrimTo10Seconds || tutorial.currentStep == EditorTutorialManager.EditorStep.CloseTrimWindow);
            if (closeInspector) continue;
            buttonActions.Add(button, original);
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() =>
            {
                if (tutorial == null || !tutorial.RestrictsEditor || PauseManager.isPaused || Allowed(button.transform)) original.Invoke();
                else if (tutorial.AcceptsTaskInput) tutorial.ShowCurrentTaskReminder();
            });
        }
    }

    private void Restore()
    {
        foreach (var entry in buttonActions) if (entry.Key != null) entry.Key.onClick = entry.Value;
        buttonActions.Clear();
        foreach (var entry in graphics) if (entry.Key != null) entry.Key.raycastTarget = entry.Value;
        foreach (var entry in controls) if (entry.Key != null) entry.Key.interactable = entry.Value;
        graphics.Clear(); controls.Clear();
    }
    private void OnDisable() { Restore(); }
    private void OnDestroy() { Restore(); }
}
