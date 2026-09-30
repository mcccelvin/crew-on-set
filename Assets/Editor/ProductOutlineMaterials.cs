using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Only explicit outline slots are remapped; white surfaces and product colors are untouched.
[InitializeOnLoad]
public sealed class ProductOutlineMaterials : AssetPostprocessor
{
    const string InkPath = "Assets/Studio/Studio Black Ink.mat";
    static readonly string[] Roots = { "Assets/Product", "Assets/Studio", "Assets/Resources/EquipmentModels", "Assets/Equipments and Products" };

    static ProductOutlineMaterials() { EditorApplication.delayCall += RepairExisting; }

    static bool IsOutline(string name) => name.IndexOf("outline", StringComparison.OrdinalIgnoreCase) >= 0;
    static bool InScope(string path) => Roots.Any(root => path.StartsWith(root + "/", StringComparison.Ordinal));

    Material OnAssignMaterialModel(Material material, Renderer renderer)
    {
        if (!InScope(assetPath) || !IsOutline(material.name)) return null;
        return AssetDatabase.LoadAssetAtPath<Material>(InkPath);
    }

    [MenuItem("Crew-On-Set/Studio/Repair Product and Camera Outlines")]
    static void RepairExisting()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= AfterPlay;
            EditorApplication.playModeStateChanged += AfterPlay;
            return;
        }
        var ink = AssetDatabase.LoadAssetAtPath<Material>(InkPath);
        if (ink == null) return;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", Roots))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;
            var remaps = importer.GetExternalObjectMap();
            bool changed = false;
            foreach (var material in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
            {
                if (!IsOutline(material.name)) continue;
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name);
                if (remaps.TryGetValue(id, out var current) && current == ink) continue;
                importer.AddRemap(id, ink);
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }
    }

    static void AfterPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= AfterPlay;
        EditorApplication.delayCall += RepairExisting;
    }
}
