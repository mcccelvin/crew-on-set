using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Assign actual imported model roots, keeping their asset GUIDs and user placements intact.
public static class StudioSetDressingSetup
{
    private const string CatalogPath = "Assets/Resources/ProductModels.asset";
    private static readonly string[] Models = { "Trash", "suitcase", "Damit", "Aircon1", "WaterDispenser", "Box" };
    private static readonly float[] Heights = { .9f, .75f, 1.5f, .4f, 1.35f, .5f };
    private static readonly Vector2[] Positions = { new Vector2(.08f, .88f), new Vector2(.08f, .67f),
        new Vector2(.08f, .43f), new Vector2(.28f, .96f), new Vector2(.92f, .85f), new Vector2(.92f, .65f) };

    [MenuItem("Crew-On-Set/Studio/Set Up Shared Decorations")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before saving the decoration settings.");
        var catalog = AssetDatabase.LoadAssetAtPath<ProductModelCatalog>(CatalogPath);
        if (catalog == null) throw new InvalidOperationException("Missing ProductModels catalog.");
        var entries = new List<ProductModelCatalog.Decorator>(catalog.decorators ?? Array.Empty<ProductModelCatalog.Decorator>());
        var additions = new List<ProductModelCatalog.Decorator>();
        for (int i = 0; i < Models.Length; i++)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Studio/Kineme/" + Models[i] + ".fbx");
            if (model == null || model.GetComponentInChildren<MeshFilter>(true) == null)
                throw new InvalidOperationException("Missing decoration geometry: " + Models[i]);
            if (entries.Exists(entry => entry != null && entry.model == model)) continue;
            additions.Add(new ProductModelCatalog.Decorator { model = model, height = Heights[i],
                floorPosition = Positions[i], floorClearance = Models[i] == "Aircon1" ? 2.25f : 0f });
        }
        if (additions.Count > 0)
        {
            Undo.RecordObject(catalog, "Add shared studio decorations");
            entries.AddRange(additions);
            catalog.decorators = entries.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }
        Directory.CreateDirectory("Logs/StudioSetDressing");
        File.WriteAllText("Logs/StudioSetDressing/setup.txt", "Assigned all six decoration models. Existing scene placements are reused; missing props spawn near studio walls. The computer desk is raised to 1.05 m on studio entry.");
        Debug.Log("Shared studio decorations ready for every campaign level and MultiStudio.");
    }
}
