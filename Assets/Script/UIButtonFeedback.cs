using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class UIButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    private Button button;
    private Vector3 originalScale;
    private float releaseAt, poseStarted;
    private int pose;
    private Vector2 from = Vector2.one, current = Vector2.one;
    private static readonly float[] Frames = { 0f, .5f, 1.18f, .95f, 1f };
    private bool hovered, selected, pressed, captured;

    private void OnEnable()
    {
        button = GetComponent<Button>();
        originalScale = transform.localScale;
        captured = true;
        pose = 0;
        from = current = Vector2.one;
        poseStarted = Time.unscaledTime;
        hovered = selected = pressed = false;
        releaseAt = 0f;
    }

    private void Update()
    {
        if (releaseAt > 0f && Time.unscaledTime >= releaseAt) { pressed = false; releaseAt = 0f; }
        int next = button.IsInteractable() ? pressed ? 2 : hovered || selected ? 1 : 0 : 0;
        if (next != pose) { pose = next; from = current; poseStarted = Time.unscaledTime; }
        int frame = Mathf.Min(Frames.Length - 1, Mathf.FloorToInt((Time.unscaledTime - poseStarted) * 24f));
        Vector2 target = pose == 2 ? new Vector2(1.04f, .9f) : pose == 1 ? new Vector2(1.045f, 1.045f) : Vector2.one;
        current = Vector2.LerpUnclamped(from, target, Frames[frame]);
        transform.localScale = Vector3.Scale(originalScale, new Vector3(current.x, current.y, 1f));
    }

    public void OnPointerEnter(PointerEventData e) { hovered = true; }
    public void OnPointerExit(PointerEventData e) { hovered = pressed = false; }
    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) pressed = true; }
    public void OnPointerUp(PointerEventData e) { releaseAt = Time.unscaledTime + .06f; }
    public void OnSelect(BaseEventData e) { selected = true; }
    public void OnDeselect(BaseEventData e) { selected = pressed = false; }
    public void OnSubmit(BaseEventData e) { pressed = true; releaseAt = Time.unscaledTime + .09f; }
    private void OnDisable()
    {
        if (captured) transform.localScale = originalScale;
        captured = false;
        hovered = selected = pressed = false;
    }
}
