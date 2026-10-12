using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

// Applies to authored scene UI and dynamically created shop/save/multiplayer buttons.
public sealed class UIMotionInstaller : MonoBehaviour
{
    private static UIMotionInstaller instance;
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        instance = new GameObject("UI Motion").AddComponent<UIMotionInstaller>();
        DontDestroyOnLoad(instance.gameObject);
        SceneManager.sceneLoaded += instance.SceneLoaded;
    }

    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ConfigureScreenUI();
        foreach (var root in scene.GetRootGameObjects())
        {
            DecorateButtons(root);
            if (scene.name == "Main Menu")
                foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rect.name == "main-menu-bg" && rect.GetComponent<MainMenuMotion>() == null)
                        rect.gameObject.AddComponent<MainMenuMotion>();
                    if (rect.GetComponent<Button>() == null && (rect.name == "Play" || rect.name == "Options" ||
                        rect.name == "createpanel" || (rect.parent != null && rect.parent.name == "Play" &&
                        (rect.name == "join" || rect.name == "load"))))
                        UITransition.ConfigurePanel(rect.gameObject);
                }
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace ||
                    canvas.GetComponent<CanvasGroup>() != null || canvas.GetComponent<Animator>() != null ||
                    canvas.GetComponent<UITransition>() != null) continue;
                // Gameplay controllers register their panels directly; menu scenes use their root canvas.
                if (scene.name == "SingleStudio" || scene.name == "MultiStudio") continue;
                canvas.gameObject.AddComponent<UITransition>();
            }
            if (scene.name == "Main Menu") BindMenuCloseButtons(root);
        }
    }

    private static void BindMenuCloseButtons(GameObject root)
    {
        foreach (var button in root.GetComponentsInChildren<Button>(true))
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentMethodName(i) != "SetActive") continue;
                var target = button.onClick.GetPersistentTarget(i) as GameObject;
                // Authored modal Close/Cancel events hide an ancestor. Sibling
                // activation/tab-selection events keep their original behavior.
                if (target == null || target.GetComponent<Button>() != null ||
                    target.GetComponent<UITransition>() == null || !button.transform.IsChildOf(target.transform)) continue;
                button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
                button.onClick.AddListener(() => UITransition.Hide(target));
            }
    }

    public static void DecorateButtons(GameObject root)
    {
        if (root == null) return;
        foreach (var button in root.GetComponentsInChildren<Button>(true)) Decorate(button);
    }

    private static void Decorate(Button button)
    {
        if (button == null || !button.gameObject.scene.IsValid() ||
            button.GetComponent<Animator>() != null || button.GetComponent<UIButtonFeedback>() != null) return;
        var canvas = button.GetComponentInParent<Canvas>(true);
        if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.WorldSpace) return;
        button.gameObject.AddComponent<UIButtonFeedback>();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 1f;
        ConfigureScreenUI();
        foreach (var button in FindObjectsOfType<Button>(true)) Decorate(button);
    }

    private static void ConfigureScreenUI()
    {
        foreach (var scroll in FindObjectsOfType<ScrollRect>(true)) EnsureScrollHandles(scroll);
        // Fit the authored design on both axes, including runtime-created panels.
        // World-space terminals keep their physical size and camera raycasts.
        foreach (var scaler in FindObjectsOfType<CanvasScaler>(true))
        {
            var canvas = scaler.GetComponent<Canvas>();
            if (canvas == null || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }
        var events = EventSystem.current;
        if (events == null) return;
        var input = events.GetComponent<InputSystemUIInputModule>();
        var legacy = events.GetComponent<StandaloneInputModule>();
        if (input == null && legacy != null && legacy.enabled)
        {
            legacy.enabled = false;
            input = events.gameObject.AddComponent<InputSystemUIInputModule>();
        }
        if (input != null && input.enabled && input.actionsAsset == null)
            input.AssignDefaultActions();
    }

    public static void EnsureScrollHandles(ScrollRect scroll)
    {
        if (scroll == null || scroll.content == null || !scroll.gameObject.scene.IsValid()) return;
        // Color grading keeps wheel/drag scrolling, without the added gold rail.
        if (scroll.name == "Grade Scroll View" || scroll.content.name == "Grade Scroll Content")
        {
            var bar = scroll.verticalScrollbar;
            if (bar != null && bar.name == "Auto vertical scroll handle")
            {
                scroll.verticalScrollbar = null;
                bar.gameObject.SetActive(false);
                Destroy(bar.gameObject);
            }
            return;
        }
        if (scroll.vertical && scroll.verticalScrollbar == null)
        {
            scroll.verticalScrollbar = CreateScrollHandle(scroll, true);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }
        if (scroll.horizontal && scroll.horizontalScrollbar == null)
        {
            scroll.horizontalScrollbar = CreateScrollHandle(scroll, false);
            scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }
    }

    private static Scrollbar CreateScrollHandle(ScrollRect scroll, bool vertical)
    {
        var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        var rail = new GameObject(vertical ? "Auto vertical scroll handle" : "Auto horizontal scroll handle",
            typeof(RectTransform), typeof(Image), typeof(Scrollbar), typeof(LayoutElement));
        rail.layer = scroll.gameObject.layer;
        rail.transform.SetParent(viewport, false);
        rail.GetComponent<LayoutElement>().ignoreLayout = true;
        var rect = (RectTransform)rail.transform;
        rect.anchorMin = vertical ? new Vector2(1, 0) : Vector2.zero;
        rect.anchorMax = vertical ? Vector2.one : new Vector2(1, 0);
        rect.pivot = vertical ? new Vector2(1, .5f) : new Vector2(.5f, 0);
        rect.offsetMin = vertical ? new Vector2(-24, scroll.horizontal ? 28 : 4) : new Vector2(4, 4);
        rect.offsetMax = vertical ? new Vector2(-4, -4) : new Vector2(scroll.vertical ? -28 : -4, 24);
        var railImage = rail.GetComponent<Image>();
        CrewPaperStyle.Round(railImage);
        railImage.color = new Color32(29, 38, 50, 180);
        railImage.raycastTarget = true;
        var handle = new GameObject("Drag handle", typeof(RectTransform), typeof(Image));
        handle.layer = rail.layer; handle.transform.SetParent(rect, false);
        var handleRect = (RectTransform)handle.transform;
        handleRect.anchorMin = Vector2.zero; handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = new Vector2(3, 3); handleRect.offsetMax = new Vector2(-3, -3);
        var image = handle.GetComponent<Image>();
        CrewPaperStyle.Round(image); image.color = new Color32(255, 198, 76, 255);
        image.raycastTarget = true;
        var bar = rail.GetComponent<Scrollbar>();
        bar.direction = vertical ? Scrollbar.Direction.BottomToTop : Scrollbar.Direction.LeftToRight;
        bar.handleRect = handleRect; bar.targetGraphic = image;
        var colors = bar.colors;
        colors.highlightedColor = new Color(1f, 1f, .85f);
        colors.pressedColor = new Color(.8f, .7f, .5f);
        bar.colors = colors;
        return bar;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        if (instance == this) instance = null;
    }
}
