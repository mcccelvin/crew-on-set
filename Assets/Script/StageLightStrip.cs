using UnityEngine;

// An accent-light prop. It supplements the graded Soft Light and is not a camera subject.
public sealed class StageLightStrip : MonoBehaviour
{
    private Transform bar;
    private Material glow;
    private Material housing;
    private BoxCollider selection;
    private int tilt;
    private readonly Light[] emitters = new Light[3];
    private static readonly float[] output = { 0f, .55f, 1.1f, 1.8f };
    private int power = 2;
    private Color lightColor = Color.white;
    public string PowerLabel => new[] { "OFF", "LOW", "MEDIUM", "HIGH" }[power];

    public static GameObject Create()
    {
        var root = new GameObject("LIGHT STRIP_Wrapper");
        root.AddComponent<StageLightStrip>().Build();
        return root;
    }

    private void Build()
    {
        glow = new Material(Shader.Find("Standard")) { name = "LED strip" };
        glow.EnableKeyword("_EMISSION");
        housing = new Material(Shader.Find("Standard")) { name = "LED frame", color = new Color(.035f,.035f,.04f) };
        housing.SetFloat("_Metallic", .6f);
        bar = new GameObject("LED bar pivot").transform;
        bar.SetParent(transform, false); bar.localPosition = new Vector3(0,1.35f,0);
        Part("LED diffuser", bar, Vector3.zero, new Vector3(.075f,2.5f,.075f), glow);
        for (int i = 0; i < emitters.Length; i++)
        {
            var source = new GameObject("LED illumination " + (i + 1));
            source.transform.SetParent(bar, false);
            source.transform.localPosition = new Vector3(0, (i - 1) * .85f, 0);
            Light light = source.AddComponent<Light>();
            light.type = LightType.Point; light.range = 5f;
            light.renderMode = LightRenderMode.ForcePixel;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.shadows = LightShadows.Soft;
            light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Low;
            light.shadowBias = .02f; light.shadowNormalBias = .1f;
            emitters[i] = light;
        }
        // The diffuser must not shadow its own light sources.
        bar.GetComponentInChildren<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Part("Strip base", transform, new Vector3(0,.035f,0), new Vector3(.45f,.07f,.35f), housing);
        Part("Strip stem", transform, new Vector3(0,.68f,0), new Vector3(.04f,1.3f,.04f), housing);
        selection = gameObject.AddComponent<BoxCollider>();
        SetColor(Color.white); UpdateBounds();
    }

    private void Part(string name, Transform parent, Vector3 position, Vector3 size, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name; part.transform.SetParent(parent, false);
        part.transform.localPosition = position; part.transform.localScale = size;
        part.GetComponent<Renderer>().sharedMaterial = material;
        // Keep these disabled, avoiding stale references from deferred Destroy during drag.
        part.GetComponent<Collider>().enabled = false;
    }

    public void CycleTilt()
    {
        tilt = (tilt + 1) % 3;
        bar.localRotation = Quaternion.Euler(0,0,tilt * 45f);
        UpdateBounds();
    }

    public void SetColor(Color color)
    {
        lightColor = color;
        RefreshLight();
    }

    public void CyclePower()
    {
        power = (power + 1) % output.Length;
        RefreshLight();
    }

    private void RefreshLight()
    {
        glow.color = power == 0 ? lightColor * .2f : lightColor;
        glow.SetColor("_EmissionColor", lightColor * output[power] * 3f);
        foreach (Light light in emitters)
        {
            if (light == null) continue;
            light.color = lightColor; light.intensity = output[power];
            light.enabled = power != 0;
        }
    }

    private void UpdateBounds()
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        foreach (var filter in GetComponentsInChildren<MeshFilter>())
        {
            Bounds mesh = filter.sharedMesh.bounds;
            for (int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2) for(int z=-1;z<=1;z+=2)
                bounds.Encapsulate(transform.InverseTransformPoint(filter.transform.TransformPoint(mesh.center + Vector3.Scale(mesh.extents,new Vector3(x,y,z)))));
        }
        selection.center = bounds.center; selection.size = bounds.size;
    }

    private void OnDestroy()
    {
        if (glow != null) Destroy(glow);
        if (housing != null) Destroy(housing);
    }
}
