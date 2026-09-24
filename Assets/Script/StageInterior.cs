using UnityEngine;
using System.Collections.Generic;

// Furnish the existing backdrop without adding graded actors/products or changing its paint surfaces.
public sealed class StageInterior : MonoBehaviour
{
    private readonly List<Material> materials = new List<Material>();
    public static string Title(int index) => index == 1 ? "CAFE CORNER" : index == 2 ? "COFFEE INTERIOR" : "PLAIN BACKDROP";
    public static int Cost(int index) => index == 1 ? 2000 : index == 2 ? 2750 : ProductionEconomy.Wall;

    public static Renderer FindStagePlatform()
    {
        var stage = GameObject.Find("Stage");
        if (stage == null) return null;
        foreach (var renderer in stage.GetComponentsInChildren<Renderer>())
            if (renderer.name.Equals("stage", System.StringComparison.OrdinalIgnoreCase)) return renderer;
        return null;
    }

    // Backdrops fit their white screen; furniture fits its complete visible model.
    public static void FitInsideStage(GameObject root, Bounds stage, Vector3 offset, bool screenOnly = false)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds();
        bool found = false;
        foreach (var renderer in renderers)
        {
            if (!renderer.enabled) continue;
            if (screenOnly && !renderer.name.StartsWith("Screen", System.StringComparison.OrdinalIgnoreCase)) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!found) return;
        float width = Mathf.Max(.001f, stage.size.x - (screenOnly ? .01f : .1f));
        float depth = Mathf.Max(.001f, stage.size.z - (screenOnly ? .01f : .1f));
        if (screenOnly)
        {
            // Fill both stage dimensions with the white surface and retain its saved height.
            // Backdrop placement roots have world-aligned axes.
            root.transform.localScale = Vector3.Scale(root.transform.localScale,
                new Vector3(width / Mathf.Max(.001f, bounds.size.x), 1f,
                    depth / Mathf.Max(.001f, bounds.size.z)));
        }
        else
        {
            float scale = Mathf.Min(1f, Mathf.Min(width / Mathf.Max(.001f, bounds.size.x),
                depth / Mathf.Max(.001f, bounds.size.z)));
            root.transform.localScale *= scale;
        }
        found = false;
        foreach (var renderer in renderers)
        {
            if (!renderer.enabled) continue;
            if (screenOnly && !renderer.name.StartsWith("Screen", System.StringComparison.OrdinalIgnoreCase)) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        float x = stage.center.x + Mathf.Clamp(offset.x, -Mathf.Max(0f, (width - bounds.size.x) * .5f), Mathf.Max(0f, (width - bounds.size.x) * .5f));
        float z = stage.center.z + Mathf.Clamp(offset.z, -Mathf.Max(0f, (depth - bounds.size.z) * .5f), Mathf.Max(0f, (depth - bounds.size.z) * .5f));
        root.transform.position += new Vector3(x - bounds.center.x,
            stage.max.y + .025f + Mathf.Max(0f, offset.y) - bounds.min.y, z - bounds.center.z);
    }

    public static void Furnish(GameObject wall, int style)
    {
        // Retain saved style IDs, but Cafe Corner no longer generates practice furniture.
        if (style == 0 || style == 1) return;
        Renderer screen = null;
        foreach (var renderer in wall.GetComponentsInChildren<Renderer>())
            if (renderer.name.StartsWith("Screen", System.StringComparison.OrdinalIgnoreCase)) { screen = renderer; break; }
        if (screen == null) return;
        Bounds bounds = screen.bounds;
        // Replace the entire backdrop visually, while retaining its ownership/selection root.
        if (style == 2)
        {
            var platform = FindStagePlatform();
            Bounds fitBounds = platform != null ? platform.bounds : bounds;
            var imported = ProductModelCatalog.CreateFurniture(false,
                fitBounds.size);
            if (imported != null)
            {
                foreach (var renderer in wall.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                foreach (var collider in wall.GetComponentsInChildren<Collider>()) collider.enabled = false;
                imported.transform.position = new Vector3(bounds.center.x, bounds.min.y + .03f, bounds.center.z);
                var entry = ProductModelCatalog.GetCoffeeInteriorEntry();
                if (entry != null) imported.transform.position += entry.position;
                imported.transform.SetParent(wall.transform, true);
                if (platform != null)
                    FitInsideStage(imported, fitBounds, entry != null ? entry.position : Vector3.zero);
                Contract4Interactable.BindFurniture(imported);
                return;
            }
        }
        var host = new GameObject(Title(style) + " Furniture");
        host.transform.SetParent(wall.transform, true);
        host.transform.position = new Vector3(bounds.center.x, bounds.min.y + .03f, bounds.center.z);
        host.transform.rotation = Quaternion.identity;
        // Compensate for FBX scaling: the furniture uses world-sized metres.
        host.transform.localScale = Vector3.one;
        Vector3 inherited = host.transform.lossyScale;
        host.transform.localScale = new Vector3(1 / Mathf.Max(.001f, Mathf.Abs(inherited.x)), 1 / Mathf.Max(.001f, Mathf.Abs(inherited.y)), 1 / Mathf.Max(.001f, Mathf.Abs(inherited.z)));
        var interior = host.AddComponent<StageInterior>();
        float width = Mathf.Min(5.5f, bounds.size.x * .8f);
        float depth = Mathf.Min(3.5f, bounds.size.z * .7f);
        // Shrink only for unusually small authored stages; leave the front and centre clear for filming.
        float scale = Mathf.Min(1f, width / 5.5f, depth / 3.5f);
        host.transform.localScale *= Mathf.Max(.15f, scale);
        var wood = interior.Material(new Color(.25f,.12f,.055f));
        var cream = interior.Material(new Color(.85f,.76f,.6f));
        var dark = interior.Material(new Color(.08f,.085f,.09f));
        var green = interior.Material(new Color(.15f,.34f,.17f));
        if (style == 1 || style == 2)
        {
            interior.Part("Cafe counter", new Vector3(-1.5f,.48f,1.05f), new Vector3(2f,.96f,.65f), wood);
            interior.Part("Countertop", new Vector3(-1.5f,1f,1.05f), new Vector3(2.12f,.08f,.76f), cream);
            interior.Part("Coffee machine", new Vector3(-1.9f,1.3f,1.07f), new Vector3(.42f,.52f,.38f), dark);
            interior.Part("Machine front", new Vector3(-1.9f,1.3f,.867f), new Vector3(.31f,.28f,.03f), cream);
            for (int i=0;i<2;i++)
            {
                float x = .8f + i * .95f;
                interior.Part("Cafe stool seat",new Vector3(x,.67f,1.1f),new Vector3(.5f,.09f,.5f),wood);
                for(int j=-1;j<=1;j+=2) for(int k=-1;k<=1;k+=2)
                    interior.Part("Stool leg",new Vector3(x+j*.17f,.31f,1.1f+k*.17f),new Vector3(.06f,.62f,.06f),dark);
            }
        }
        interior.Part("Plant pot",new Vector3(2.3f,.2f,1.15f),new Vector3(.4f,.4f,.4f),wood);
        Contract4Interactable.BindFurniture(host);
        interior.Part("Plant stem",new Vector3(2.3f,.68f,1.15f),new Vector3(.06f,.62f,.06f),wood);
        for(int i=0;i<3;i++)
            interior.Part("Plant leaves",new Vector3(2.3f+(i-1)*.14f,.75f+i*.15f,1.15f),new Vector3(.38f,.25f,.35f),green);
    }

    private Material Material(Color color)
    {
        var shader = Shader.Find(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? "Universal Render Pipeline/Lit" : "Standard");
        if (shader == null) shader = Shader.Find("Standard");
        var material = new Material(shader) { color = color };
        materials.Add(material);
        return material;
    }
    private void Part(string title, Vector3 position, Vector3 size, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = title;
        part.transform.SetParent(transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        part.GetComponent<Renderer>().sharedMaterial = material;
    }
    private void OnDestroy() { foreach (var material in materials) if (material != null) Destroy(material); }
}
