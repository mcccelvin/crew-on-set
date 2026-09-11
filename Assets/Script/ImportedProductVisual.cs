using System.Collections.Generic;
using UnityEngine;

// Paint the vehicle body without painting its tyres, windows, trim, or product labels.
public sealed class ImportedProductVisual : MonoBehaviour
{
    private readonly List<Material> bodyMaterials = new List<Material>();
    public bool CanRecolor => bodyMaterials.Count > 0;

    public void Initialize(bool isVehicle)
    {
        if (!isVehicle) return;
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || materials[i].name != "CarColor") continue;
                materials[i] = new Material(materials[i]);
                bodyMaterials.Add(materials[i]);
            }
            renderer.sharedMaterials = materials;
        }
        SetBodyColor(new Color(1f, 0.23f, 0.025f));
    }

    public void SetBodyColor(Color color)
    {
        foreach (var material in bodyMaterials) material.color = color;
    }

    private void OnDestroy()
    {
        foreach (var material in bodyMaterials) if (material != null) Destroy(material);
    }
}
