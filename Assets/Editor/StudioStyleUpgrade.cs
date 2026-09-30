using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Visual-only copies: source FBX files, colliders and station references remain untouched.
public static class StudioStyleUpgrade
{
    const string Source = "Assets/Equipments and Products/STUDIOP.fbx";
    const string Previous = "Assets/Product/STUDIOP.fbx";
    const string Work = "Logs/StudioStyle";
    static readonly string[] Tables = { "director table", "editor table.001", "sound table.002", "kiosk table.001" };
    [Serializable] public class FaceSet { public int[] triangles; }
    [Serializable] public class Geometry
    {
        public Vector3[] vertices;
        public Vector3[] normals;
        public Vector2[] uv;
        public FaceSet[] submeshes;
        public float width;
    }
    public static void Prepare()
    {
        Directory.CreateDirectory(Work);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        foreach (string name in Tables)
        {
            var filter = source.transform.Find(name).GetComponent<MeshFilter>();
            var mesh = filter.sharedMesh;
            var data = new Geometry { vertices = mesh.vertices, uv = mesh.uv,
                width = 0.012f / Mathf.Max(0.001f, filter.transform.lossyScale.x),
                submeshes = Enumerable.Range(0, mesh.subMeshCount).Select(i => new FaceSet { triangles = mesh.GetTriangles(i) }).ToArray() };
            File.WriteAllText(Work + "/" + name + ".json", JsonUtility.ToJson(data));
        }
        var materials = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true))
            .SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct();
        File.WriteAllLines(Work + "/materials.txt", materials.Select(m => m.name + " | " + m.shader.name + " | " + (m.HasProperty("_Color") ? m.color.ToString() : "") + " | " + AssetDatabase.GetAssetPath(m)));
    }
    static Material Paint(string name, Color color, bool ink = false)
    {
        string path = "Assets/Studio/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
        material.color = color;
        material.SetFloat("_Glossiness", ink ? 0 : 0.2f);
        material.SetFloat("_Metallic", 0);
        material.SetFloat("_SpecularHighlights", ink ? 0 : 1);
        material.SetFloat("_GlossyReflections", ink ? 0 : 1);
        if (ink) { material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); material.EnableKeyword("_GLOSSYREFLECTIONS_OFF"); }
        EditorUtility.SetDirty(material);
        return material;
    }
    public static void Apply()
    {
        var blue = Paint("Studio UI Blue", new Color(0.12f, 0.34f, 0.53f));
        var gold = Paint("Studio UI Gold", new Color(0.85f, 0.59f, 0.22f));
        var ink = Paint("Studio Black Ink", new Color(0.012f, 0.015f, 0.022f), true);
        // These are the imported outline-shell slots, not the white equipment surfaces.
        var outlineSlots = new System.Collections.Generic.Dictionary<string, string[]>
        {
            { "Assets/Studio/Equipments/LowDirectorTablet.fbx", new[] { "Material", "Material.002" } },
            { "Assets/Studio/Equipments/LowDirectorMegaPhone.fbx", new[] { "Material.001" } },
            { "Assets/Studio/Equipments/LowDirectorChair.fbx", new[] { "Material" } },
            { "Assets/Studio/Equipments/LowAudio.fbx", new[] { "Material.1" } },
            { "Assets/Studio/Equipments/EditorPlayground.fbx", new[] { "Material" } },
            { Source, new[] { "Material.001" } }
        };
        // Remaps cover spawned instances too. Explicit scene overrides are repaired below.
        var oldInk = new System.Collections.Generic.HashSet<Material>();
        foreach (var entry in outlineSlots)
        {
            foreach (var material in AssetDatabase.LoadAllAssetsAtPath(entry.Key).OfType<Material>())
                if (entry.Value.Contains(material.name)) oldInk.Add(material);
        }
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        Directory.CreateDirectory("Assets/Studio/Beveled");
        var meshes = new System.Collections.Generic.Dictionary<string, Mesh>();
        foreach (var name in Tables)
        {
            var data = JsonUtility.FromJson<Geometry>(File.ReadAllText(Work + "/" + name + ".beveled.json"));
            string path = "Assets/Studio/Beveled/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear(); mesh.name = name + " Beveled";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = data.vertices; mesh.normals = data.normals; mesh.uv = data.uv;
            mesh.subMeshCount = data.submeshes.Length;
            for (int i = 0; i < data.submeshes.Length; i++) mesh.SetTriangles(data.submeshes[i].triangles, i);
            mesh.RecalculateBounds(); mesh.RecalculateTangents(); EditorUtility.SetDirty(mesh);
            meshes[name] = mesh;
        }
        foreach (string name in new[] { "SingleStudio", "MultiStudio" })
        {
            string path = "Assets/Scenes/" + name + ".unity";
            File.Copy(path, Work + "/" + name + "-before-style.unity", true);
            var scene = SceneManager.GetSceneByPath(path);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                foreach (var renderer in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)))
                {
                    var materials = renderer.sharedMaterials;
                    if (!materials.Any(m => m != null && oldInk.Contains(m))) continue;
                    renderer.sharedMaterials = materials.Select(m => m != null && oldInk.Contains(m) ? ink : m).ToArray();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                var root = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                    .First(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == Previous);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var original = source.transform.Find(AnimationUtility.CalculateTransformPath(renderer.transform, root));
                    if (original == null) continue;
                    var reference = original.GetComponent<MeshRenderer>();
                    if (reference == null) continue;
                    var materials = renderer.sharedMaterials;
                    var originals = reference.sharedMaterials;
                    for (int i = 0; i < materials.Length && i < originals.Length; i++)
                    {
                        string key = originals[i] != null ? originals[i].name : "";
                        if (key == "Doorframe" || key == "KioskFrame") materials[i] = blue;
                        if (key == "DirectorTable" || key == "EditorTable" || key == "SoundTable" || key == "KioskTable" || key == "WallHook") materials[i] = gold;
                        if (key == "TableStand" || key == "Ladder" || (renderer.name == "Plane.002" && key == "Material.001")) materials[i] = ink;
                    }
                    // Roof has its own acoustic panel material, not the frame color.
                    if (renderer.name != "Roof") { renderer.sharedMaterials = materials; PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); }
                    if (meshes.TryGetValue(renderer.name, out var mesh))
                    {
                        var filter = renderer.GetComponent<MeshFilter>();
                        filter.sharedMesh = mesh; PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { if (!wasOpen) EditorSceneManager.CloseScene(scene, true); }
        }
        foreach (var entry in outlineSlots)
        {
            var importer = AssetImporter.GetAtPath(entry.Key) as ModelImporter;
            foreach (var slot in entry.Value)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot), ink);
            importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText(Work + "/complete.txt", "Palette and four beveled station meshes applied to both studios.");
    }
}
