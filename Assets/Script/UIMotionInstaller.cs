using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        foreach (var root in scene.GetRootGameObjects())
        {
            DecorateButtons(root);
            if (scene.name == "Main Menu")
                foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rect.name == "main-menu-bg" && rect.GetComponent<MainMenuMotion>() == null)
                        rect.gameObject.AddComponent<MainMenuMotion>();
                    if (rect.GetComponent<Button>() == null && (rect.name == "Play" ||
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
        foreach (var button in FindObjectsOfType<Button>(true)) Decorate(button);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        if (instance == this) instance = null;
    }
}
