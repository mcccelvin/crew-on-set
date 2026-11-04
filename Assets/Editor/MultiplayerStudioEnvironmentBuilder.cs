#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Keep the campaign and multiplayer room geometry sourced from one authored scene.
public static class MultiplayerStudioEnvironmentBuilder
{
    private const string Source = "Assets/Scenes/SingleStudio.unity";
    private const string Target = "Assets/Resources/CrewUI/StudioEnvironment.prefab";
    private static bool IsClone()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ParrelSync.ClonesManager")).FirstOrDefault(t => t != null);
        return type?.GetMethod("IsClone",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)?.Invoke(null,null) is bool clone && clone;
    }
    private static void Ensure()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || IsClone()) return;
        var importer = AssetImporter.GetAtPath(Target);
        string signature = AssetDatabase.GetAssetDependencyHash(Source).ToString();
        if (importer == null || importer.userData != signature) Rebuild();
    }
    [MenuItem("Crew-On-Set/Refresh Multiplayer Studio Environment")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlaying || IsClone()) return;
        var source = SceneManager.GetSceneByPath(Source);
        bool opened = !source.IsValid() || !source.isLoaded;
        if (opened) source = EditorSceneManager.OpenScene(Source,OpenSceneMode.Additive);
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var roots = source.GetRootGameObjects();
            var authored = roots.Where(root => root.name == "StudioSetup" || root.name == "Studio" || root.name == "Stage").ToArray();
            if (!authored.Any(root => root.name == "Stage") || authored.Length < 2)
                throw new InvalidOperationException("SingleStudio's authored room and stage were not found.");
            var holder = new GameObject("Shared Singleplayer Studio"); holder.SetActive(false);
            SceneManager.MoveGameObjectToScene(holder,preview);
            foreach (var root in authored) { var copy = Object.Instantiate(root,holder.transform,false); copy.name = root.name; }
            foreach (var script in holder.GetComponentsInChildren<MonoBehaviour>(true))
                if (script != null) Object.DestroyImmediate(script);
            foreach (var component in holder.GetComponentsInChildren<Component>(true))
                if (component != null && !(component is Transform) && !(component is MeshFilter) && !(component is Renderer) &&
                    !(component is Collider) && !(component is Light) && !(component is LODGroup)) Object.DestroyImmediate(component);
            holder.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(holder,Target);
            var importer = AssetImporter.GetAtPath(Target);
            if (importer != null) { importer.userData = AssetDatabase.GetAssetDependencyHash(Source).ToString(); importer.SaveAndReimport(); }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            if (opened) EditorSceneManager.CloseScene(source,true);
        }
    }
}
#endif
