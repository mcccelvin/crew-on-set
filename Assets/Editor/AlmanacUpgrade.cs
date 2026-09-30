using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AlmanacUpgrade
{
    static AlmanacUpgrade() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string request = "Temp/AlmanacUpgrade.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { Apply(); }
        catch (Exception e) { File.WriteAllText("Logs/AlmanacUpgrade-error.txt", e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Crew-On-Set/Almanac/Apply Prepared Illustrations")]
    public static void Apply()
    {
        var original = SceneManager.GetActiveScene();
        int count = 0;
        try
        {
            foreach (string name in new[] { "SingleStudio", "MultiStudio" })
            {
                string path = "Assets/Scenes/" + name + ".unity";
                var scene = SceneManager.GetSceneByPath(path);
                bool loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var manager in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AlmanacManager>(true)))
                    {
                        typeof(AlmanacManager).GetMethod("EnsureIllustrationNote", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
                        if (manager.almanacCanvas != null)
                        {
                            foreach (var rect in manager.almanacCanvas.GetComponentsInChildren<RectTransform>(true))
                                if (rect != null && rect.name == "Watch guide") UnityEngine.Object.DestroyImmediate(rect.gameObject);
                        }
                        EditorUtility.SetDirty(manager);
                        count++;
                    }
                    foreach (var rect in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RectTransform>(true)).ToArray())
                        if (rect != null && rect.name == "Watch guide" && rect.parent != null && rect.parent.name == "Book pages")
                            UnityEngine.Object.DestroyImmediate(rect.gameObject);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save " + path);
                }
                finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
            }
            AlmanacIllustrationPreview.Render();
            File.WriteAllText("Logs/AlmanacUpgrade-complete.txt", "Saved editable illustration notes for " + count + " managers; rendered seven pages. " + DateTime.Now);
        }
        finally { if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original); }
    }
}

