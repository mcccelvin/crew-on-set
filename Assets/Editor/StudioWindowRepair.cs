using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class StudioWindowRepair
{
    public static void Apply()
    {
        const string modelPath = "Assets/Product/STUDIOP.fbx";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var shader = Shader.Find("Crew On Set/Window Glass");
        if (model == null || shader == null) throw new System.InvalidOperationException("Window source or shader missing.");
        const string materialPath = "Assets/Studio/Studio Window Glass.mat";
        var glass = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (glass == null) { glass = new Material(shader); AssetDatabase.CreateAsset(glass, materialPath); }
        glass.shader = shader;
        glass.color = new Color(0.65f,0.82f,0.86f,0.28f);
        EditorUtility.SetDirty(glass);
        foreach (string name in new[] { "SingleStudio", "MultiStudio" })
        {
            var scene = SceneManager.GetSceneByPath("Assets/Scenes/" + name + ".unity");
            bool open = scene.IsValid() && scene.isLoaded;
            if (!open) scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Additive);
            try
            {
                var root = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                    .First(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == modelPath);
                foreach (var original in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var slots = original.sharedMaterials;
                    if (!slots.Any(m => m != null && m.name == "glass")) continue;
                    string relative = AnimationUtility.CalculateTransformPath(original.transform, model.transform);
                    var target = root.Find(relative);
                    if (target == null) continue;
                    var renderer = target.GetComponent<MeshRenderer>();
                    // The framed reception window remains in the replacement model.
                    if (original.name == "Cube.001" && renderer != null)
                    {
                        target.gameObject.SetActive(true); renderer.enabled = true;
                        var materials = renderer.sharedMaterials;
                        for (int i=0; i<materials.Length && i<slots.Length; i++)
                            if (slots[i] != null && slots[i].name == "glass") materials[i] = glass;
                        renderer.sharedMaterials = materials;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(target.gameObject);
                        continue;
                    }
                    if (target.Find("Restored Window Pane") != null) continue;
                    var sourceMesh = original.GetComponent<MeshFilter>().sharedMesh;
                    var triangles = Enumerable.Range(0, Mathf.Min(slots.Length,sourceMesh.subMeshCount))
                        .Where(i => slots[i] != null && slots[i].name == "glass")
                        .SelectMany(i => sourceMesh.GetTriangles(i)).ToArray();
                    if (triangles.Length == 0) continue;
                    string meshPath = "Assets/Studio/Window Pane " + original.name + ".asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (mesh == null)
                    {
                        mesh = Object.Instantiate(sourceMesh); mesh.name = "Window Pane " + original.name;
                        mesh.subMeshCount = 1; mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();
                        AssetDatabase.CreateAsset(mesh,meshPath);
                    }
                    var pane = new GameObject("Restored Window Pane",typeof(MeshFilter),typeof(MeshRenderer));
                    pane.transform.SetParent(target,false);
                    pane.GetComponent<MeshFilter>().sharedMesh = mesh;
                    pane.GetComponent<MeshRenderer>().sharedMaterial = glass;
                    pane.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { if (!open) EditorSceneManager.CloseScene(scene,true); }
        }
        AssetDatabase.SaveAssets();
    }
}
