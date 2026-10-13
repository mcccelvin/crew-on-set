using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Animations;
using UnityEngine.Playables;

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
    private Image frameImage;
    private readonly System.Collections.Generic.List<Mesh> portraitMeshes = new System.Collections.Generic.List<Mesh>();

    public static void Attach(Transform shortcut)
    {
        if (shortcut == null || shortcut.Find("Live account avatar") != null) return;
        var portrait = new GameObject("Live account avatar", typeof(RectTransform), typeof(RoundedAvatarImage));
        portrait.transform.SetParent(shortcut, false);
        var rect = portrait.GetComponent<RectTransform>();
        // Stay inside the authored gold frame on both axes. WidthControlsHeight
        // overrides the vertical inset on non-square buttons and covers the rim.
        rect.anchorMin = new Vector2(.16f, .16f);
        rect.anchorMax = new Vector2(.84f, .84f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        portrait.GetComponent<RawImage>().raycastTarget = false;
        portrait.AddComponent<AccountAvatarPortrait>();
    }

    private void Awake()
    {
        target = GetComponent<RawImage>();
        frameImage = transform.parent != null ? transform.parent.GetComponent<Image>() : null;
    }

    private void LateUpdate()
    {
        // HUD frames can preserve the sprite aspect inside a wider RectTransform.
        // Fit to the visible artwork, not the invisible button bounds.
        if (frameImage == null || frameImage.name == "account") return;
        Rect frame = frameImage.rectTransform.rect;
        Sprite sprite = frameImage.overrideSprite;
        if (frameImage.preserveAspect && sprite != null && sprite.rect.height > 0)
        {
            float aspect = sprite.rect.width / sprite.rect.height;
            Vector2 size = frame.size;
            if (size.x > size.y * aspect) size.x = size.y * aspect;
            else size.y = size.x / aspect;
            Vector2 pivot = frameImage.rectTransform.pivot;
            frame.position += Vector2.Scale(frame.size - size, pivot);
            frame.size = size;
        }
        var rect = target.rectTransform;
        rect.anchorMin = rect.anchorMax = frameImage.rectTransform.pivot;
        rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = frame.center;
        rect.sizeDelta = frame.size * .64f;
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
        target.enabled = parts != null && System.Array.Exists(parts, id => CharacterCosmetics.Find(id)?.kind == "body");
        if (!target.enabled) return;

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
        PosePortrait(modelInstance);

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
        // Frame the head and shoulders; outstretched arms should not shrink the face.
        bool menuPortrait = transform.parent != null && transform.parent.name == "account";
        portraitCamera.orthographicSize = bounds.size.y * (menuPortrait ? .20f : .23f);
        portraitCamera.nearClipPlane = .01f;
        portraitCamera.farClipPlane = 20f;
        var lookAt = bounds.center + Vector3.up * (bounds.size.y * .29f);
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

    private void PosePortrait(GameObject model)
    {
        Animator rig = null;
        foreach (var candidate in model.GetComponentsInChildren<Animator>(true))
            if (candidate.avatar != null && candidate.avatar.isValid && candidate.avatar.isHuman) { rig = candidate; break; }
        if (rig == null) return;
        PlayableGraph graph = default;
        try
        {
            rig.enabled = true;
            rig.applyRootMotion = false;
            rig.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var clip in Resources.LoadAll<AnimationClip>("Character/Animations/Stand--Idle.anim"))
            {
                if (clip.name.StartsWith("__preview__") || !clip.humanMotion) continue;
                graph = PlayableGraph.Create("HUD portrait idle");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var pose = AnimationClipPlayable.Create(graph, clip);
                AnimationPlayableOutput.Create(graph, "Portrait", rig).SetSourcePlayable(pose);
                graph.Play(); pose.SetTime(Mathf.Min(.5f, clip.length * .5f)); graph.Evaluate(0);
                break;
            }
            if (!graph.IsValid())
            {
                // Arms-down fallback for a build without the idle clip.
                foreach (var bone in new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm })
                {
                    var arm = rig.GetBoneTransform(bone);
                    var elbow = rig.GetBoneTransform(bone == HumanBodyBones.LeftUpperArm ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                    if (arm != null && elbow != null)
                        arm.rotation = Quaternion.FromToRotation(elbow.position - arm.position, Vector3.down) * arm.rotation;
                }
            }
            // Freeze the evaluated skin before the manual camera renders it.
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.enabled || skin.sharedMesh == null) continue;
                var mesh = new Mesh { name = "HUD idle portrait mesh" };
                skin.BakeMesh(mesh, true); portraitMeshes.Add(mesh);
                var display = new GameObject("HUD posed outfit", typeof(MeshFilter), typeof(MeshRenderer));
                display.transform.SetParent(skin.transform, false);
                display.GetComponent<MeshFilter>().sharedMesh = mesh;
                display.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                skin.enabled = false;
            }
        }
        finally
        {
            if (graph.IsValid()) graph.Destroy();
            rig.enabled = false;
        }
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
        foreach (var mesh in portraitMeshes) if (mesh != null) Dispose(mesh);
        portraitMeshes.Clear();
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
