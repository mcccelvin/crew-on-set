using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Profile-only greeting: does not drive the gameplay character or change equipment ownership.
public sealed class ProfileCharacterPreview : MonoBehaviour
{
    private GameObject rigRoot, portrait;
    private Camera portraitCamera;
    private Light portraitLight;
    private RenderTexture target;
    private Animator animator;
    private PlayableGraph graph;
    private AnimationClipPlayable idle, wave;
    private AnimationMixerPlayable mixer;
    private float waveLength;
    public float WaveDuration => waveLength;
    private float idleLength, elapsed, footHeight;
    private Transform leftFoot, rightFoot;
    private readonly System.Collections.Generic.List<SkinnedMeshRenderer> skins = new System.Collections.Generic.List<SkinnedMeshRenderer>();
    private readonly System.Collections.Generic.List<Mesh> posedMeshes = new System.Collections.Generic.List<Mesh>();
    private float nextRender;

    public void Initialize(GameObject root, GameObject model, Camera camera, Light light, RenderTexture texture)
    {
        Release();
        rigRoot = root; portrait = model; portraitCamera = camera; portraitLight = light; target = texture;
        foreach (var candidate in model.GetComponentsInChildren<Animator>())
            if (candidate.avatar != null && candidate.avatar.isValid && candidate.avatar.isHuman) { animator = candidate; break; }
        if (animator != null)
        {
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var clip in Resources.LoadAll<AnimationClip>("Character/Animations/Stand--Idle.anim"))
            {
                if (clip.name.StartsWith("__preview__") || !clip.humanMotion) continue;
                graph = PlayableGraph.Create("Profile live idle and wave");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                idle = AnimationClipPlayable.Create(graph, clip);
                idleLength = Mathf.Max(.01f, clip.length);
                mixer = AnimationMixerPlayable.Create(graph, 2);
                graph.Connect(idle, 0, mixer, 0); mixer.SetInputWeight(0, 1);
                var waveController = Resources.Load<RuntimeAnimatorController>("ProfileWave");
                if (waveController != null)
                    foreach (var gesture in waveController.animationClips)
                    {
                        if (!gesture.humanMotion || gesture.length <= 0) continue;
                        wave = AnimationClipPlayable.Create(graph, gesture);
                        waveLength = gesture.length;
                        graph.Connect(wave, 0, mixer, 1);
                        break;
                    }
                AnimationPlayableOutput.Create(graph, "Profile", animator).SetSourcePlayable(mixer);
                graph.Play(); graph.Evaluate(0);
                break;
            }
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }
        // Manual cameras can reuse the Animator's earlier skinning result. Bake the final
        // greeting pose into reusable meshes, so both idle and late bone edits are visible.
        foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!skin.enabled || skin.sharedMesh == null) continue;
            var mesh = new Mesh { name = "Profile posed " + skin.name };
            mesh.MarkDynamic();
            var display = new GameObject("Profile display " + skin.name, typeof(MeshFilter), typeof(MeshRenderer));
            display.layer = 31; display.transform.SetParent(skin.transform, false);
            // BakeMesh(true) accounts for the imported rig scale; keep its original
            // transform on the child display so the posed mesh stays aligned.
            display.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = display.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = skin.sharedMaterials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            skins.Add(skin); posedMeshes.Add(mesh); skin.enabled = false;
        }
        footHeight = FeetHeight(); elapsed = 0; nextRender = 0;
        Sample(0);
    }

    private float FeetHeight() => leftFoot != null && rightFoot != null
        ? Mathf.Min(leftFoot.position.y, rightFoot.position.y) : portrait.transform.position.y;

    private void LateUpdate()
    {
        elapsed += Time.unscaledDeltaTime;
        if (elapsed < nextRender) return;
        nextRender = elapsed + 1f / 30f;
        Sample(elapsed); // The profile also animates while gameplay is paused.
    }

    // Deterministic sampling is also used by the isolated native-Unity visual checks.
    public void Sample(float seconds)
    {
        if (rigRoot == null || target == null) return;
        if (graph.IsValid())
        {
            idle.SetTime(seconds % idleLength);
            float weight = WaveWeightAt(seconds);
            if (wave.IsValid()) wave.SetTime(seconds < 1 ? 0 : Mathf.Min(waveLength, (seconds - 1) % (waveLength + 5)));
            mixer.SetInputWeight(0, 1 - weight); mixer.SetInputWeight(1, weight);
            graph.Evaluate(0);
            portrait.transform.position += Vector3.up * (footHeight - FeetHeight());
        }
        for (int i = 0; i < skins.Count; i++) skins[i].BakeMesh(posedMeshes[i], true);
        portraitLight.enabled = true;
        portraitCamera.targetTexture = target;
        try { portraitCamera.Render(); }
        finally { portraitCamera.targetTexture = null; portraitLight.enabled = false; }
    }

    public float WaveWeightAt(float seconds)
    {
        if (seconds < 1 || waveLength <= 0) return 0;
        float phase = (seconds - 1) % (waveLength + 5f);
        if (phase >= waveLength) return 0;
        float blend = Mathf.Min(.2f, waveLength * .2f);
        return Mathf.SmoothStep(0, 1, phase / blend) * Mathf.SmoothStep(0, 1, (waveLength - phase) / blend);
    }

    public void Release()
    {
        if (graph.IsValid()) graph.Destroy();
        foreach (var mesh in posedMeshes) if (mesh != null) Dispose(mesh);
        posedMeshes.Clear(); skins.Clear();
        if (rigRoot != null) { rigRoot.SetActive(false); Dispose(rigRoot); }
        rigRoot = null; portrait = null; target = null; animator = null;
        leftFoot = rightFoot = null; waveLength = 0;
    }
    private void OnDisable() { Release(); }
    private void OnDestroy() { Release(); }
    private static void Dispose(Object value)
    {
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}
