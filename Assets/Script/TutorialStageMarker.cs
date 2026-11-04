using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TutorialStageMarker : MonoBehaviour
{

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= Setup;
        SceneManager.sceneLoaded += Setup;
    }
    private static void Setup(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var target in root.GetComponentsInChildren<Transform>(true))
            {
                string name = target.name.ToLowerInvariant();
                string title = name == "pointa" ? "LIGHT\n45%" : name == "pointb" ? "PRODUCT" : name == "pointc" ? "CAMERA" : null;
                if (title == null || target.GetComponent<TutorialStageMarker>() != null) continue;
                target.gameObject.AddComponent<TutorialStageMarker>().Build(title);
            }
    }
    private void Build(string title)
    {
        SquareGuideMarker.Apply(gameObject);
    }
}
