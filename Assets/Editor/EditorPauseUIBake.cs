using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class EditorPauseUIBake
{
    const string Request = "Assets/Editor/EditorPauseUIBake.request";
    static double nextCheck;
    static EditorPauseUIBake() { EditorApplication.update += Check; }
    static void Check()
    {
        if (EditorApplication.timeSinceStartup < nextCheck || !File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextCheck = EditorApplication.timeSinceStartup + 15;
        Bake();
    }
    [MenuItem("Crew-On-Set/UI/Copy Single Player Pause To Editor")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var destination = SceneManager.GetSceneByPath("Assets/Scenes/Editor.unity");
        bool openedDestination = !destination.isLoaded;
        if (!openedDestination && destination.isDirty) { Debug.LogWarning("Save the Editor scene before copying the pause UI."); return; }
        if (openedDestination) destination = EditorSceneManager.OpenScene("Assets/Scenes/Editor.unity", OpenSceneMode.Additive);
        var source = SceneManager.GetSceneByPath("Assets/Scenes/SingleStudio.unity");
        bool openedSource = !source.isLoaded;
        if (openedSource) source = EditorSceneManager.OpenScene("Assets/Scenes/SingleStudio.unity", OpenSceneMode.Additive);
        try
        {
            PauseManager template = null;
            foreach (var root in source.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<PauseManager>(true))
                    if (candidate.pauseMenuCanvas != null) template = candidate;
            if (template == null) throw new System.InvalidOperationException("SingleStudio pause UI not found.");
            PauseManager manager = null;
            foreach (var root in destination.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<PauseManager>(true)) manager = candidate;
            if (manager == null)
            {
                var obj = new GameObject("Editor Pause Manager");
                SceneManager.MoveGameObjectToScene(obj, destination);
                manager = obj.AddComponent<PauseManager>();
            }
            if (manager.pauseMenuCanvas == null)
            {
                var copy = Object.Instantiate(template.pauseMenuCanvas);
                copy.name = "Pause - Single Player Design";
                SceneManager.MoveGameObjectToScene(copy, destination);
                copy.transform.localScale = Vector3.one;
                manager.pauseMenuCanvas = copy;
                var canvas = copy.GetComponent<Canvas>();
                if (canvas != null) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000; }
                foreach (var button in copy.GetComponentsInChildren<Button>(true))
                {
                    button.onClick = new Button.ButtonClickedEvent();
                    if (button.name == "Resume") UnityEventTools.AddPersistentListener(button.onClick, manager.Resume);
                    else if (button.name == "Exit") UnityEventTools.AddPersistentListener(button.onClick, manager.ExitToMain);
                    else if (button.name == "Option" || button.name == "Options") UnityEventTools.AddPersistentListener(button.onClick, manager.OpenOptions);
                }
                copy.SetActive(false);
            }
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(destination);
            if (!EditorSceneManager.SaveScene(destination)) throw new IOException("Could not save Editor scene.");
            if (File.Exists(Request)) File.Delete(Request);
            Debug.Log("Single-player pause UI saved in Editor scene hierarchy.");
        }
        finally
        {
            if (openedSource) EditorSceneManager.CloseScene(source, true);
            if (openedDestination) EditorSceneManager.CloseScene(destination, true);
        }
    }
}
