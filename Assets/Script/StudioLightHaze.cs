using UnityEngine;
using UnityEngine.Rendering;

// A local, depth-clipped approximation of haze inside a studio spotlight.
public sealed class StudioLightHaze : MonoBehaviour
{
    private Light source;
    private MeshRenderer beamRenderer;
    private Material beamMaterial;
    private Mesh beamMesh;
    private bool visible = true;
    private float setupDensity;
    public void Initialize(Light lightSource)
    {
        source = lightSource;
        Shader shader = Resources.Load<Shader>("StudioLightHaze");
        if (shader == null) { enabled = false; return; }
        // Copy the built-in cube mesh without leaving a collider on the beam.
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beamMesh = Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);
        cube.SetActive(false);
        Destroy(cube);
        gameObject.AddComponent<MeshFilter>().sharedMesh = beamMesh;
        beamRenderer = gameObject.AddComponent<MeshRenderer>();
        beamMaterial = new Material(shader);
        beamRenderer.sharedMaterial = beamMaterial;
        beamRenderer.shadowCastingMode = ShadowCastingMode.Off;
        beamRenderer.receiveShadows = false;
        beamRenderer.lightProbeUsage = LightProbeUsage.Off;
        beamRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        LateUpdate();
    }
    public void SetVisible(bool value) { visible = value; }
    private void LateUpdate()
    {
        if (source == null || beamRenderer == null) return;
        beamRenderer.enabled = visible && source.enabled && source.gameObject.activeInHierarchy && source.intensity > .001f;
        if (!beamRenderer.enabled) return;
        float length = Mathf.Min(source.range, 8f);
        float radius = Mathf.Tan(source.spotAngle * Mathf.Deg2Rad * .5f) * length;
        transform.SetPositionAndRotation(source.transform.position + source.transform.forward * length * .5f, source.transform.rotation);
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(2*radius / Mathf.Max(.001f, Mathf.Abs(parentScale.x)),
            2*radius / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), length / Mathf.Max(.001f, Mathf.Abs(parentScale.z)));
        Color tint = source.color;
        if (source.useColorTemperature) tint *= Mathf.CorrelatedColorTemperatureToRGB(source.colorTemperature);
        beamMaterial.SetColor("_BeamColor", tint);
        setupDensity = Mathf.Min(source.intensity, 5f) * .016f;
        beamMaterial.SetFloat("_Density", setupDensity);
    }
    private void OnWillRenderObject()
    {
        Camera camera = Camera.current;
        if (camera == null || beamMaterial == null) return;
        // Set per-camera, including manual recorder Render() calls. Never bake the
        // setup aid into a viewfinder, recording, reflection or export texture.
        bool setupView = IsSetupView(camera);
        beamMaterial.SetFloat("_Density", setupView ? setupDensity : 0f);
        if (setupView) camera.depthTextureMode |= DepthTextureMode.Depth;
    }
    public static bool IsSetupView(Camera camera)
    {
        return camera != null && camera.targetTexture == null &&
            camera.cameraType != CameraType.Reflection &&
            camera.GetComponentInParent<Player.Equipment.FilmCameraItem>() == null &&
            camera.GetComponent<TruePixelRecorder>() == null;
    }
    // Explicit guard also covers manual captures before normal camera callbacks.
    public static System.IDisposable HideForCapture() => new CaptureScope();
    private sealed class CaptureScope : System.IDisposable
    {
        private readonly StudioLightHaze[] guides = FindObjectsOfType<StudioLightHaze>();
        private readonly bool[] previous;
        public CaptureScope()
        {
            previous = new bool[guides.Length];
            for (int i = 0; i < guides.Length; i++)
            {
                var renderer = guides[i].beamRenderer;
                if (renderer == null) continue;
                previous[i] = renderer.forceRenderingOff;
                renderer.forceRenderingOff = true;
            }
        }
        public void Dispose()
        {
            for (int i = 0; i < guides.Length; i++)
                if (guides[i] != null && guides[i].beamRenderer != null)
                    guides[i].beamRenderer.forceRenderingOff = previous[i];
        }
    }
    private void OnDestroy()
    {
        if (beamMaterial != null) Destroy(beamMaterial);
        if (beamMesh != null) Destroy(beamMesh);
    }
}
