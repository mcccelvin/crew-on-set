using UnityEditor;
using UnityEngine;

// Room fixtures only: no changes to purchasable/film lights or their controls.
public static class StudioCeilingFixtures
{
    static Material Material(string name, Color color, bool glow)
    {
        string path = "Assets/Studio/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = color; m.SetFloat("_Glossiness", 0.15f);
        if (glow) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 1.5f); }
        EditorUtility.SetDirty(m);
        return m;
    }
    static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var existing = parent.Find(name);
        var obj = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name; obj.transform.SetParent(parent, false);
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localPosition = position; obj.transform.localScale = scale;
        var collider = obj.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        var meshRenderer = obj.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.allowOcclusionWhenDynamic = false;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }
    public static void Apply(Transform studio)
    {
        var roof = studio.Find("Roof");
        var renderer = roof != null ? roof.GetComponent<MeshRenderer>() : null;
        if (renderer == null) return;
        var bounds = renderer.bounds;
        if (bounds.size.x < 3 || bounds.size.z < 3) return;
        var frame = Material("Ceiling Fixture Frame", new Color(0.09f,0.10f,0.11f), false);
        var diffuser = Material("Ceiling Light Diffuser", new Color(1f,0.95f,0.84f), true);
        var group = studio.Find("Ceiling Room Fixtures");
        if (group == null)
        {
            group = new GameObject("Ceiling Room Fixtures").transform;
            group.position = Vector3.zero; group.SetParent(studio, true);
        }
        // Centres align with the 1.2m world-space ceiling tile grid.
        for (int x = 0; x < 2; x++) for (int z = 0; z < 3; z++)
        {
            float px = Mathf.Lerp(bounds.min.x, bounds.max.x, (x+1f)/3f);
            float pz = Mathf.Lerp(bounds.min.z, bounds.max.z, (z+1f)/4f);
            px = (Mathf.Floor(px/1.2f)+0.5f)*1.2f;
            pz = (Mathf.Floor(pz/1.2f)+0.5f)*1.2f;
            string fixtureName = "Recessed Panel " + x + "-" + z;
            var fixture = group.Find(fixtureName);
            if (fixture == null) fixture = new GameObject(fixtureName).transform;
            fixture.position = new Vector3(px, bounds.min.y-0.06f, pz);
            fixture.rotation = Quaternion.identity;
            fixture.SetParent(group, true);
            // Cancel imported FBX scale so all fixtures remain one square ceiling tile.
            Vector3 parentScale = group.lossyScale;
            fixture.localScale = new Vector3(1f/Mathf.Max(.0001f,Mathf.Abs(parentScale.x)),
                1f/Mathf.Max(.0001f,Mathf.Abs(parentScale.y)),1f/Mathf.Max(.0001f,Mathf.Abs(parentScale.z)));
            Box(fixture, "Frame", Vector3.zero, new Vector3(1.16f,0.045f,1.16f), frame);
            // Keep the entire diffuser below the frame, eliminating intersecting faces.
            Box(fixture, "Diffuser", new Vector3(0,-0.04f,0), new Vector3(1.06f,0.02f,1.06f), diffuser);
            var lamp = fixture.GetComponentInChildren<Light>(true);
            if (lamp == null) lamp = new GameObject("Room Light").AddComponent<Light>();
            lamp.transform.SetParent(fixture, false);
            lamp.transform.localPosition = new Vector3(0,-0.09f,0);
            lamp.transform.localRotation = Quaternion.Euler(90,0,0);
            lamp.type = LightType.Spot; lamp.spotAngle = 110;
            lamp.renderMode = LightRenderMode.ForcePixel;
            lamp.range = 12; lamp.intensity = 0.65f;
            lamp.color = new Color(1f,0.95f,0.86f);
            lamp.shadows = LightShadows.None;
            var appearance = fixture.GetComponent<PanelLightAppearance>();
            if (appearance == null) appearance = fixture.gameObject.AddComponent<PanelLightAppearance>();
            appearance.source = lamp;
            appearance.diffuser = fixture.Find("Diffuser").GetComponent<Renderer>();
        }
    }
}
