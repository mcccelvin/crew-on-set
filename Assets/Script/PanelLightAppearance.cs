using UnityEngine;

// Shared square beam profile and visible diffuser state; does not alter light power.
public sealed class PanelLightAppearance : MonoBehaviour
{
    public Light source;
    public Renderer diffuser;
    private Material material;
    private Color offColor;
    private MaterialPropertyBlock properties;
    private static Texture2D squareCookie;

    public static Texture2D SquareCookie
    {
        get
        {
            if (squareCookie != null) return squareCookie;
            const int size = 64;
            squareCookie = new Texture2D(size,size,TextureFormat.RGBA32,false,true);
            squareCookie.name = "Soft Square Panel Beam";
            squareCookie.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size*size];
            for (int y=0;y<size;y++) for (int x=0;x<size;x++)
            {
                float edge = Mathf.Max(Mathf.Abs((x+0.5f)/size*2-1),Mathf.Abs((y+0.5f)/size*2-1));
                float a = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.56f,0.68f,edge));
                pixels[y*size+x] = new Color(a,a,a,a);
            }
            squareCookie.SetPixels(pixels); squareCookie.Apply(false,true);
            return squareCookie;
        }
    }
    void Start()
    {
        if (source != null)
        {
            source.cookie = SquareCookie;
            // Auto-mode lights can switch to vertex lighting as the camera moves.
            source.renderMode = LightRenderMode.ForcePixel;
        }
        foreach (var part in GetComponentsInChildren<MeshRenderer>(true))
        {
            // These fixtures were added after the room's baked occlusion data.
            part.allowOcclusionWhenDynamic = false;
            part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            part.receiveShadows = false;
        }
        if (diffuser == null || diffuser.sharedMaterial == null) return;
        material = new Material(diffuser.sharedMaterial);
        material.EnableKeyword("_EMISSION");
        offColor = new Color(0.65f,0.65f,0.62f);
        diffuser.sharedMaterial = material;
        properties = new MaterialPropertyBlock();
    }
    void LateUpdate()
    {
        if (properties == null || diffuser == null) return;
        bool on = source != null && source.isActiveAndEnabled && source.intensity > 0;
        diffuser.GetPropertyBlock(properties);
        properties.SetColor("_Color", on ? Color.white : offColor);
        properties.SetColor("_EmissionColor", on ? source.color*Mathf.Clamp(source.intensity*3,1.5f,5) : Color.black);
        diffuser.SetPropertyBlock(properties);
    }
    void OnDestroy() { if (material != null) Destroy(material); }
}
