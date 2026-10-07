using UnityEngine;
using UnityEngine.UI;

/// <summary>Renders the currently synchronized outfit into a small in-game profile portrait.</summary>
public sealed class AccountAvatarPortrait : MonoBehaviour
{
    private const int AvatarLayer = 31;
    private RawImage target;
    private CCoinService service;
    private GameObject portraitRoot;
    private RenderTexture texture;
    private Camera portraitCamera;
    private Light portraitLight;

    public static void Attach(Transform shortcut)
    {
        if (shortcut == null || shortcut.Find("Live account avatar") != null) return;
        var portrait = new GameObject("Live account avatar", typeof(RectTransform), typeof(RawImage));
        portrait.transform.SetParent(shortcut, false);
        var rect = portrait.GetComponent<RectTransform>();
        // Fractional insets respect the authored main-menu button's nonuniform scale.
        rect.anchorMin = new Vector2(.19f, .19f);
        rect.anchorMax = new Vector2(.81f, .81f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        portrait.GetComponent<RawImage>().raycastTarget = false;
        portrait.AddComponent<AccountAvatarPortrait>();
    }

    private void Awake()
    {
        target = GetComponent<RawImage>();
    }

    private void OnEnable()
    {
        if (service != null) service.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (service != null) service.Changed -= Refresh;
        Release();
    }

    private void Bind()
    {
        if (service != null) return;
        service = CCoinService.Ensure();
        service.Changed += Refresh;
    }

    private void Refresh()
    {
        if (target == null) target = GetComponent<RawImage>();
        if (target == null || !isActiveAndEnabled) return;
        Bind();
        RenderOutfit(service.EquippedParts);
    }

    private void RenderOutfit(string[] parts)
    {
        if (target == null) target = GetComponent<RawImage>();
        Release();

        var catalog = Resources.Load<ProductModelCatalog>("ProductModels");
        GameObject source = null;
        if (catalog != null && catalog.coffeeActorModels != null)
            foreach (var model in catalog.coffeeActorModels)
                if (model != null && model.name == "DefaultCharacGirlRig") { source = model; break; }
        if (source == null) return;

        portraitRoot = new GameObject("Account avatar portrait");
        portraitRoot.transform.position = new Vector3(20000, 20000, 20000);
        var modelInstance = Instantiate(source);
        modelInstance.name = "Current account avatar";
        modelInstance.transform.SetPositionAndRotation(portraitRoot.transform.position, Quaternion.identity);
        modelInstance.transform.SetParent(portraitRoot.transform, true);
        CharacterCosmeticRig.Load()?.Apply(modelInstance, parts);
        foreach (var animator in modelInstance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var component in modelInstance.GetComponentsInChildren<MonoBehaviour>(true)) component.enabled = false;
        foreach (var collider in modelInstance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var otherCamera in modelInstance.GetComponentsInChildren<Camera>(true)) otherCamera.enabled = false;
        foreach (var otherLight in modelInstance.GetComponentsInChildren<Light>(true)) otherLight.enabled = false;

        var renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = default;
        bool found = false;
        foreach (var renderer in renderers)
        {
            if (!renderer.enabled) continue;
            renderer.gameObject.layer = AvatarLayer;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!found) { Release(); return; }

        portraitRoot.transform.localScale = Vector3.one * (1.7f / Mathf.Max(.01f, bounds.size.y));
        bounds = BoundsOf(modelInstance);
        var cameraObject = new GameObject("Account avatar camera", typeof(Camera));
        cameraObject.transform.SetParent(portraitRoot.transform, false);
        portraitCamera = cameraObject.GetComponent<Camera>();
        portraitCamera.enabled = false;
        portraitCamera.cullingMask = 1 << AvatarLayer;
        portraitCamera.clearFlags = CameraClearFlags.SolidColor;
        portraitCamera.backgroundColor = new Color32(245, 239, 218, 255);
        portraitCamera.orthographic = true;
        portraitCamera.aspect = 1f;
        portraitCamera.orthographicSize = Mathf.Max(bounds.extents.y * .57f, bounds.extents.x * 1.1f);
        portraitCamera.nearClipPlane = .01f;
        portraitCamera.farClipPlane = 20f;
        var lookAt = bounds.center + Vector3.up * (bounds.size.y * .22f);
        portraitCamera.transform.position = lookAt + Vector3.forward * (bounds.extents.z + 4f);
        portraitCamera.transform.LookAt(lookAt);

        var lightObject = new GameObject("Account avatar light", typeof(Light));
        lightObject.transform.SetParent(portraitRoot.transform, false);
        lightObject.transform.position = lookAt + new Vector3(-2f, 2f, 3f);
        lightObject.transform.LookAt(lookAt);
        portraitLight = lightObject.GetComponent<Light>();
        portraitLight.type = LightType.Directional;
        portraitLight.intensity = 1.3f;
        portraitLight.cullingMask = 1 << AvatarLayer;
        portraitLight.enabled = true;

        texture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
        texture.Create();
        target.texture = texture;
        portraitCamera.targetTexture = texture;
        portraitCamera.Render();
        portraitCamera.targetTexture = null;
        portraitLight.enabled = false;
    }

    private static Bounds BoundsOf(GameObject model)
    {
        Bounds bounds = default;
        bool found = false;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    private void Release()
    {
        if (target != null) target.texture = null;
        if (portraitRoot != null) { portraitRoot.SetActive(false); Dispose(portraitRoot); }
        if (texture != null)
        {
            texture.Release();
            Dispose(texture);
        }
        portraitRoot = null;
        texture = null;
        portraitCamera = null;
        portraitLight = null;
    }

    private static void Dispose(Object value)
    {
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}
