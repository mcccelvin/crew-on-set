using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-time scene migration; retains the existing scene objects and their references.
[InitializeOnLoad]
public static class StudioVisualSwap
{
    const string Request = "Assets/Editor/StudioVisualSwap.request";
    const string Source = "Assets/Equipments and Products/STUDIOP.fbx";
    const string Previous = "Assets/Product/STUDIOP.fbx";
    static StudioVisualSwap() { EditorApplication.update += RunPending; }

    static void RunPending()
    {
        if (!File.Exists(Request)) { EditorApplication.update -= RunPending; return; }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= RunPending;
        try
        {
            if (File.ReadAllText(Request).Trim() == "windows") StudioWindowRepair.Apply();
            else if (File.ReadAllText(Request).Trim() == "style-prepare") StudioStyleUpgrade.Prepare();
            else if (File.ReadAllText(Request).Trim() == "style-apply") { ApplySurfaces(); StudioStyleUpgrade.Apply(); }
            else if (File.ReadAllText(Request).Trim() == "material-report")
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
                File.WriteAllLines("Logs/StudioVisualSwap/material-detail.txt", source.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Select(m => m.name + " | " + m.shader.name + " | " + (m.HasProperty("_Color") ? m.color.ToString() : "") + " | " + AssetDatabase.GetAssetPath(m)));
            }
            else if (File.ReadAllText(Request).Trim() == "shader-report")
            {
                var shader = Shader.Find("Crew On Set/Studio Surface");
                File.WriteAllText("Logs/StudioVisualSwap/shader.txt", "Pipeline: " + UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline + "\n" + string.Join("\n", ShaderUtil.GetShaderMessages(shader).Select(m => m.message + " line " + m.line)));
            }
            else if (File.ReadAllText(Request).Trim() == "surfaces")
            {
                ApplySurfaces();
            }
            else if (File.ReadAllText(Request).Trim() == "floor") RestoreFloor();
            else Apply();
            File.Delete(Request);
        }
        catch (Exception error)
        {
            Directory.CreateDirectory("Logs/StudioVisualSwap");
            File.WriteAllText("Logs/StudioVisualSwap/error.txt", error.ToString());
            Debug.LogException(error);
        }
    }

    [MenuItem("Crew-On-Set/Studio/Use Brighter Studio Model")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before replacing the studio.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        if (source == null) throw new InvalidOperationException("Missing studio model: " + Source);
        string backup = "Logs/StudioVisualSwap/" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup);
        var active = SceneManager.GetActiveScene();
        foreach (string name in new[] { "SingleStudio", "MultiStudio" })
        {
            string path = "Assets/Scenes/" + name + ".unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var sceneObjects = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
                var root = sceneObjects.FirstOrDefault(g => PrefabUtility.IsAnyPrefabInstanceRoot(g) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g) == Previous);
                if (root == null)
                {
                    if (sceneObjects.Any(g => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g) == Source)) continue;
                    throw new InvalidOperationException("Existing studio model not found in " + name);
                }
                File.Copy(path, backup + "/" + name + ".unity");
                Undo.RegisterFullObjectHierarchyUndo(root, "Replace studio model");
                // Retain all scene object IDs, transforms, scripts, and enabled states.
                // The newer export merges Floor.011 / Floor.013 into other wall meshes.
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    string relative = AnimationUtility.CalculateTransformPath(filter.transform, root.transform);
                    var replacement = source.transform.Find(relative);
                    var newFilter = replacement != null ? replacement.GetComponent<MeshFilter>() : null;
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (newFilter == null)
                    {
                        if (relative != "Floor.011" && relative != "Floor.013") continue;
                        if (renderer != null) { renderer.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); }
                        foreach (var collider in filter.GetComponents<Collider>())
                        { collider.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(collider); }
                        continue;
                    }
                    filter.sharedMesh = newFilter.sharedMesh;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                    var newRenderer = replacement.GetComponent<MeshRenderer>();
                    if (renderer != null && newRenderer != null)
                    {
                        var floorMaterial = filter.name == "Floor" ? AssetDatabase.LoadAssetAtPath<Material>("Assets/Studio/Equipments/StudioFloorPlanks034B.mat") : null;
                        renderer.sharedMaterials = floorMaterial != null ? newRenderer.sharedMaterials.Select(m => floorMaterial).ToArray() : newRenderer.sharedMaterials;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                    foreach (var collider in filter.GetComponents<MeshCollider>())
                    {
                        collider.sharedMesh = newFilter.sharedMesh;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                    }
                }
                foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
                {
                    string relative = AnimationUtility.CalculateTransformPath(filter.transform, source.transform);
                    if (root.transform.Find(relative) != null) continue;
                    string parentPath = AnimationUtility.CalculateTransformPath(filter.transform.parent, source.transform);
                    var parent = string.IsNullOrEmpty(parentPath) ? root.transform : root.transform.Find(parentPath);
                    if (parent == null) throw new InvalidOperationException("Missing studio parent: " + parentPath);
                    var added = new GameObject(filter.name, typeof(MeshFilter), typeof(MeshRenderer));
                    added.transform.SetParent(parent, false);
                    added.transform.localPosition = filter.transform.localPosition;
                    added.transform.localRotation = filter.transform.localRotation;
                    added.transform.localScale = filter.transform.localScale;
                    added.layer = parent.gameObject.layer;
                    added.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    var sourceRenderer = filter.GetComponent<MeshRenderer>();
                    if (sourceRenderer != null) added.GetComponent<MeshRenderer>().sharedMaterials = sourceRenderer.sharedMaterials;
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { if (!wasOpen) EditorSceneManager.CloseScene(scene, true); }
        }
        if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        File.WriteAllText("Logs/StudioVisualSwap/complete.txt", "Updated both studio scenes to " + Source + ". Backups: " + backup);
    }

    static Material SurfaceMaterial(string name, Color color, float panels)
    {
        string path = "Assets/Studio/" + name + ".mat";
        var shader = Shader.Find("Crew On Set/Studio Surface");
        if (shader == null) throw new InvalidOperationException("Studio surface shader is missing.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader;
        material.SetColor("_Color", color);
        material.SetFloat("_Detail", 0.22f);
        material.SetFloat("_Panels", panels);
        material.SetFloat("_PanelSize", 0.6f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void ApplySurfaces()
    {
        var walls = SurfaceMaterial("Studio Warm Plaster", new Color(0.88f, 0.81f, 0.66f), 0);
        walls.SetFloat("_Finish", 3);
        walls.SetFloat("_Metallic", 0);
        walls.SetFloat("_Smoothness", 0.08f);
        walls.SetFloat("_Detail", 0.2f);
        var stage = SurfaceMaterial("Studio Stage Grip", new Color(0.62f, 0.45f, 0.23f), 0);
        stage.SetFloat("_Finish", 2);
        stage.SetFloat("_Metallic", 0);
        stage.SetFloat("_Smoothness", 0.1f);
        stage.SetFloat("_Detail", 0.15f);
        var floor = SurfaceMaterial("Studio Timber Floor", new Color(0.65f, 0.48f, 0.30f), 0);
        floor.SetFloat("_Finish", 3);
        floor.SetFloat("_Metallic", 0);
        floor.SetFloat("_Smoothness", 0.12f);
        var ceiling = SurfaceMaterial("Studio Acoustic Ceiling", new Color(0.48f, 0.65f, 0.80f), 1);
        ceiling.SetFloat("_Finish", 0);
        ceiling.SetFloat("_PanelSize", 1.2f);
        ceiling.SetFloat("_Metallic", 0);
        ceiling.SetFloat("_Smoothness", 0.12f);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        foreach (string name in new[] { "SingleStudio", "MultiStudio" })
        {
            string path = "Assets/Scenes/" + name + ".unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                // Include retained legacy stage meshes as well as the replacement model.
                foreach (var r in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)))
                {
                    var slots = r.sharedMaterials;
                    if (!slots.Any(m => m != null && (m.name == "Stage" || m.name == "Studio Stage Grip"))) continue;
                    r.sharedMaterials = slots.Select(m => m != null && (m.name == "Stage" || m.name == "Studio Stage Grip") ? stage : m).ToArray();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                }
                var root = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                    .First(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == Previous);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var original = source.transform.Find(AnimationUtility.CalculateTransformPath(renderer.transform, root));
                    var sourceRenderer = original != null ? original.GetComponent<MeshRenderer>() : null;
                    if (sourceRenderer == null) continue;
                    var materials = renderer.sharedMaterials;
                    var originals = sourceRenderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < materials.Length && i < originals.Length; i++)
                    {
                        if (renderer.name == "Roof") { materials[i] = ceiling; changed = true; }
                        else if (renderer.name == "Floor" && originals[i] != null && originals[i].name == "Floor") { materials[i] = floor; changed = true; }
                        else if (originals[i] != null && originals[i].name == "OuterWall") { materials[i] = walls; changed = true; }
                    }
                    if (!changed) continue;
                    Undo.RecordObject(renderer, "Texture studio surfaces");
                    renderer.sharedMaterials = materials;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                StudioCeilingFixtures.Apply(root);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { if (!wasOpen) EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/StudioVisualSwap/surfaces-complete.txt", "Textured plaster walls and acoustic ceiling applied in both scenes.");
    }

    static void RestoreFloor()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Studio/Equipments/StudioFloorPlanks034B.mat");
        if (material == null) throw new InvalidOperationException("Previous floor material is missing.");
        foreach (string name in new[] { "SingleStudio", "MultiStudio" })
        {
            string path = "Assets/Scenes/" + name + ".unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var floor = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true))
                    .First(r => r.name == "Floor" && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r.gameObject) == Previous);
                Undo.RecordObject(floor, "Restore previous studio floor");
                floor.sharedMaterials = floor.sharedMaterials.Select(m => material).ToArray();
                PrefabUtility.RecordPrefabInstancePropertyModifications(floor);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { if (!wasOpen) EditorSceneManager.CloseScene(scene, true); }
        }
        File.WriteAllText("Logs/StudioVisualSwap/floor-complete.txt", "Previous wood floor material restored in both studio scenes.");
    }
}
