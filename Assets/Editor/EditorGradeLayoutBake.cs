using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

[InitializeOnLoad]
public static class EditorGradeLayoutBake
{
    private const string Request = "Assets/Editor/EditorGradeLayoutBake.request";
    private static double nextCheck;
    static EditorGradeLayoutBake() { EditorApplication.update += Check; }
    private static void Check()
    {
        if (EditorApplication.timeSinceStartup < nextCheck || !File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextCheck = EditorApplication.timeSinceStartup + 10;
        Bake();
    }
    [MenuItem("Crew-On-Set/UI/Bake Color Grading Layout")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        const string path = "Assets/Scenes/Editor.unity";
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        // Never auto-save unrelated unsaved user edits.
        if (!opened && scene.isDirty) { Debug.LogWarning("Save the Editor scene, then bake the color layout."); return; }
        foreach (var root in scene.GetRootGameObjects())
            foreach (var grading in root.GetComponentsInChildren<ColorGradingManager>(true))
            {
                grading.BakeHierarchyUI();
                EditorUtility.SetDirty(grading);
            }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        if (File.Exists(Request)) File.Delete(Request);
        Debug.Log("Color grading layout saved in Editor scene hierarchy.");
    }
}
