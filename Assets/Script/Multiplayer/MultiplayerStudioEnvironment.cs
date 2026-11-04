using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MultiplayerStudioEnvironment
{
    public const string RetiredName = "Retired multiplayer environment";
    private const string SharedName = "Shared singleplayer studio environment";

    public static void Apply(Scene scene)
    {
        if (scene.name != "MultiStudio" || !scene.isLoaded) return;
        var roots = scene.GetRootGameObjects();
        if (roots.Any(root => root.name == SharedName)) return;
        var source = Resources.Load<GameObject>("CrewUI/StudioEnvironment");
        if (source == null) { Debug.LogError("The shared singleplayer studio environment is missing."); return; }
        var retired = new GameObject(RetiredName); retired.SetActive(false);
        SceneManager.MoveGameObjectToScene(retired, scene);
        // Retain serialized legacy references, while removing the outdated room
        // shell and stage from rendering, collision and station discovery.
        foreach (var root in roots)
            if (root.name == "Studio" || root.name == "StudioSetup" || root.name == "Product Studio" || root.name == "Stage")
                root.transform.SetParent(retired.transform, true);
        var environment = Object.Instantiate(source);
        environment.name = SharedName;
        SceneManager.MoveGameObjectToScene(environment, scene);
        environment.SetActive(true);
        Physics.SyncTransforms();
    }
}
