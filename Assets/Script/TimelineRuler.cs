using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class TimelineRuler : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler
{
    public TimelineManager timeline;
    public static bool CanSeek
    {
        get
        {
            var editor = EditorManager.Instance;
            if (editor == null || !editor.gameObject.activeInHierarchy || editor.timelineContainer == null ||
                !editor.timelineContainer.gameObject.activeInHierarchy || PauseManager.isPaused || editor.ReviewIsOpen) return false;
            if (ContractUIManager.Instance != null && ContractUIManager.Instance.IsQualificationsOpen()) return false;
            if (TutorialUIManager.Instance != null && TutorialUIManager.Instance.IsBossDialogueOpen()) return false;
            var lesson = EditorTutorialManager.Instance;
            return lesson == null || !lesson.RestrictsEditor || lesson.AcceptsTaskInput && lesson.currentStep == EditorTutorialManager.EditorStep.SeekTimeline;
        }
    }
    public static TimelineRuler Install(TimelineManager manager)
    {
        if (manager.timestampContainer == null) return null;
        var ruler = manager.timestampContainer.GetComponent<TimelineRuler>();
        if (ruler == null) ruler = manager.timestampContainer.gameObject.AddComponent<TimelineRuler>();
        ruler.timeline = manager;
        var background = manager.timestampContainer.GetComponent<Graphic>();
        if (background == null) { var image = manager.timestampContainer.gameObject.AddComponent<Image>(); image.color = Color.clear; background = image; }
        background.raycastTarget = true;
        return ruler;
    }
    public void Seek(float seconds)
    {
        if (!CanSeek) return;
        var compiler = FindObjectOfType<CommercialCompiler>();
        if (compiler != null && compiler.SeekTimeline(seconds)) EditorTutorialManager.Instance?.OnTimelineSeeked();
    }
    private void SeekPointer(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left || timeline == null) return;
        var rect = timeline.timestampContainer;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, data.pressEventCamera, out var point))
            Seek(TimelineSeekMath.SecondsAt(point.x, rect.rect.xMin, rect.rect.width, timeline.pixelsPerSecond));
    }
    public void OnPointerDown(PointerEventData data) { SeekPointer(data); }
    public void OnBeginDrag(PointerEventData data) { SeekPointer(data); }
    public void OnDrag(PointerEventData data) { SeekPointer(data); }
}
