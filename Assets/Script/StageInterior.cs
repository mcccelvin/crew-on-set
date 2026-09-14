using UnityEngine;
using System.Collections.Generic;

// Furnish the existing backdrop without adding graded actors/products or changing its paint surfaces.
public sealed class StageInterior : MonoBehaviour
{
    private readonly List<Material> materials = new List<Material>();
    public static string Title(int index) => index == 1 ? "CAFE CORNER" : index == 2 ? "LIVING ROOM" : "PLAIN BACKDROP";
    public static int Cost(int index) => index == 1 ? 2000 : index == 2 ? 2750 : ProductionEconomy.Wall;

    public static void Furnish(GameObject wall, int style)
    {
        if (style == 0) return;
        Renderer screen = null;
        foreach (var renderer in wall.GetComponentsInChildren<Renderer>())
            if (renderer.name.StartsWith("Screen", System.StringComparison.OrdinalIgnoreCase)) { screen = renderer; break; }
        if (screen == null) return;
        Bounds bounds = screen.bounds;
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
        if (style == 1)
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
        else
        {
            interior.Part("Sofa base",new Vector3(-1.1f,.27f,1),new Vector3(2.35f,.4f,.83f),wood);
            interior.Part("Sofa cushion",new Vector3(-1.1f,.51f,.92f),new Vector3(2.15f,.18f,.7f),cream);
            interior.Part("Sofa back",new Vector3(-1.1f,.8f,1.4f),new Vector3(2.35f,.82f,.18f),cream);
            for(int i=-1;i<=1;i+=2)
                interior.Part("Sofa arm",new Vector3(-1.1f+i*1.18f,.58f,1),new Vector3(.15f,.55f,.86f),wood);
            interior.Part("Side table",new Vector3(1.05f,.58f,1.1f),new Vector3(.6f,.08f,.6f),wood);
            interior.Part("Table pedestal",new Vector3(1.05f,.27f,1.1f),new Vector3(.15f,.54f,.15f),dark);
            interior.Part("Lamp base",new Vector3(1.05f,.67f,1.1f),new Vector3(.18f,.1f,.18f),dark);
            interior.Part("Lamp stem",new Vector3(1.05f,.88f,1.1f),new Vector3(.04f,.35f,.04f),dark);
            interior.Part("Lamp shade",new Vector3(1.05f,1.12f,1.1f),new Vector3(.4f,.3f,.4f),cream);
        }
        interior.Part("Plant pot",new Vector3(2.3f,.2f,1.15f),new Vector3(.4f,.4f,.4f),wood);
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
