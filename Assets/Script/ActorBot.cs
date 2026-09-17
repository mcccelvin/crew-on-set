using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Director-controlled performer with repeatable gestures and optional straight-line blocking.
public sealed class ActorBot : MonoBehaviour
{
    private Animator animator;
    private HumanPoseHandler poseHandler;
    private HumanPose pose;
    private HumanPose restingPose;
    private PlayableGraph graph;
    private AnimationClipPlayable idle;
    private int tier;
    private int performance;
    private float elapsed;
    private Transform product;
    private float nextTargetCheck;
    private Vector3 startMark,endMark;
    private Quaternion startFacing;
    private bool hasStart,hasEnd,walking;
    public bool HasWalk => hasStart && hasEnd;
    public void SetStartMark()
    {
        walking = false;
        startMark = transform.position;
        startFacing = transform.rotation;
        hasStart = true;
        hasEnd = false;
    }
    public void SetEndMark()
    {
        walking = false;
        endMark = transform.position;
        endMark.y = startMark.y;
        hasEnd = hasStart && Vector3.Distance(startMark, endMark) > .2f;
        GameFeedback.Show(hasEnd ? "END saved. K: rehearse. J: return to start. Recording repeats this walk. H: clear walk."
            : "Set START with B first, then move the actor at least 0.2 metres before pressing N.");
    }
    public void ReturnToStartMark()
    {
        walking = false;
        if (hasStart) { transform.position = startMark; transform.rotation = startFacing; }
    }
    public void ClearWalk() { ReturnToStartMark(); hasStart = hasEnd = false; }
    public void RehearseWalk()
    {
        if (!HasWalk) { GameFeedback.Show("Place the actor at START and press B, then move to END and press N."); return; }
        ReturnToStartMark();
        walking = true;
        elapsed = 0;
    }
    private bool PathBlocked(Vector3 direction, float distance)
    {
        foreach (var hit in Physics.CapsuleCastAll(transform.position + Vector3.up * .4f,
            transform.position + Vector3.up * 1.5f, .22f, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform) && hit.normal.y < .7f) return true;
        return false;
    }
    private readonly System.Collections.Generic.List<Material> materials = new System.Collections.Generic.List<Material>();
    private static readonly System.Collections.Generic.Dictionary<string, int> muscles = BuildMuscleLookup();

    public int SkillTier => tier;
    public static string TierName(int tier) => tier <= 0 ? "ROOKIE" : tier == 1 ? "TRAINED" : "EXPERT";
    public static int HirePrice(int tier, int basePrice) => Mathf.Max(1, basePrice) * (tier <= 0 ? 1 : tier == 1 ? 3 : 6);

    private static System.Collections.Generic.Dictionary<string, int> BuildMuscleLookup()
    {
        var result = new System.Collections.Generic.Dictionary<string, int>();
        for (int i = 0; i < HumanTrait.MuscleCount; i++) result[HumanTrait.MuscleName[i]] = i;
        return result;
    }

    public static bool TryCreate(GameObject actor, int skillTier)
    {
        var model = Resources.Load<GameObject>("Character/Models/Armature");
        if (model == null) return false;
        var visual = Instantiate(model, actor.transform, false);
        visual.name = "Actor Bot Visual";
        var rig = visual.GetComponentInChildren<Animator>();
        if (rig == null || rig.avatar == null || !rig.avatar.isValid || !rig.avatar.isHuman)
        {
            visual.SetActive(false);
            Destroy(visual);
            return false;
        }
        foreach (var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled = false;
        var bot = actor.AddComponent<ActorBot>();
        bot.tier = Mathf.Clamp(skillTier, 0, 2);
        bot.animator = rig;
        rig.runtimeAnimatorController = null;
        rig.applyRootMotion = false;
        rig.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        rig.Rebind();
        foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            renderer.updateWhenOffscreen = true;
            string part = renderer.name.ToLowerInvariant().Contains("leg") ? "Legs" : renderer.name.ToLowerInvariant().Contains("arm") ? "Arms" : "Body";
            Shader shader = Shader.Find(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? "Universal Render Pipeline/Lit" : "Standard");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = "Actor " + part };
            material.mainTexture = Resources.Load<Texture2D>("Character/Textures/Armature_" + part + "_AlbedoTransparency");
            material.color = part == "Legs" ? new Color(.22f,.25f,.3f) :
                bot.tier == 0 ? new Color(.75f,.35f,.25f) : bot.tier == 1 ? new Color(.3f,.55f,.8f) : new Color(.75f,.62f,.25f);
            bot.materials.Add(material);
            var slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
        }
        foreach (var clip in Resources.LoadAll<AnimationClip>("Character/Animations/Stand--Idle.anim"))
        {
            if (clip.name.StartsWith("__preview__")) continue;
            bot.graph = PlayableGraph.Create("Actor idle");
            bot.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            bot.idle = AnimationClipPlayable.Create(bot.graph, clip);
            var output = AnimationPlayableOutput.Create(bot.graph, "Actor", rig);
            output.SetSourcePlayable(bot.idle);
            bot.graph.Play();
            bot.graph.Evaluate(0f);
            break;
        }
        bot.poseHandler = new HumanPoseHandler(rig.avatar, rig.transform);
        bot.poseHandler.GetHumanPose(ref bot.pose);
        bot.restingPose = bot.pose;
        bot.restingPose.muscles = (float[])bot.pose.muscles.Clone();
        // Use the mesh in its idle pose to normalize the NPC to human scale and place its feet on the mark.
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.transform.localScale *= 1.85f / Mathf.Max(.01f, bounds.size.y);
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.transform.position += Vector3.up * (actor.transform.position.y - bounds.min.y);
        }
        var selection = actor.AddComponent<BoxCollider>();
        selection.center = new Vector3(0,.925f,0);
        selection.size = new Vector3(.8f,1.85f,.55f);
        return true;
    }

    public void SetPerformance(int action)
    {
        performance = Mathf.Clamp(action, 0, 2);
        RestartTake();
    }

    public void RestartTake()
    {
        if (HasWalk) RehearseWalk();
        elapsed = 0f;
        if (idle.IsValid()) idle.SetTime(0);
        EvaluatePerformance();
    }

    private void LateUpdate()
    {
        if (PauseManager.isPaused || poseHandler == null) return;
        elapsed += Time.deltaTime;
        if(walking)
        {
            var direction=endMark-transform.position;direction.y=0;
            if(direction.magnitude<.03f) { transform.position=endMark; walking=false; }
            else if (PathBlocked(direction.normalized, Mathf.Min(direction.magnitude, Time.deltaTime * .9f + .1f)))
            { walking=false; GameFeedback.Show("Actor path blocked. Move the start or end mark to leave a clear walking route."); }
            else
            {
                transform.rotation=Quaternion.LookRotation(direction);
                transform.position=Vector3.MoveTowards(transform.position,endMark,Time.deltaTime*.9f);
            }
        }
        EvaluatePerformance();
    }

    private void EvaluatePerformance()
    {
        if (poseHandler == null) return;
        if (graph.IsValid())
        {
            float length = idle.GetAnimationClip().length;
            if (length > 0f) idle.SetTime(Mathf.Repeat(elapsed, length));
            graph.Evaluate(0f);
            poseHandler.GetHumanPose(ref pose);
        }
        else
        {
            pose.bodyPosition = restingPose.bodyPosition;
            pose.bodyRotation = restingPose.bodyRotation;
            System.Array.Copy(restingPose.muscles, pose.muscles, pose.muscles.Length);
        }
        // Rookie has a slower, smaller gesture with a pause; experts have fluid, expressive delivery.
        float speed = tier == 0 ? .65f : tier == 1 ? .85f : 1f;
        float phase = elapsed * speed * Mathf.PI * 2f / 3f;
        float wave = Mathf.Sin(phase);
        float expression = tier == 0 ? .55f : tier == 1 ? .8f : 1f;
        if (tier == 0) wave = Mathf.SmoothStep(-1f, 1f, Mathf.Clamp01((wave + .3f) / 1.3f));
        SetMuscle("Left Arm Down-Up", -.65f);
        SetMuscle("Right Arm Down-Up", -.65f);
        if (performance == 1)
        {
            SetMuscle("Left Arm Down-Up", .45f + .12f * wave * expression);
            SetMuscle("Left Arm Front-Back", .1f);
            SetMuscle("Left Forearm Stretch", -.2f + .2f * wave * expression);
            SetMuscle("Left Hand Down-Up", .25f * wave * expression);
        }
        else if (performance == 2)
        {
            float offer = .5f + .5f * wave;
            SetMuscle("Left Arm Down-Up", -.4f + .15f * offer * expression);
            SetMuscle("Right Arm Down-Up", -.4f + .15f * offer * expression);
            SetMuscle("Left Arm Front-Back", .35f);
            SetMuscle("Right Arm Front-Back", .35f);
            SetMuscle("Left Forearm Stretch", -.35f + .2f * offer * expression);
            SetMuscle("Right Forearm Stretch", -.35f + .2f * offer * expression);
        }
        if (Time.time >= nextTargetCheck)
        {
            nextTargetCheck = Time.time + 1f;
            product = null;
            foreach (var candidate in FindObjectsOfType<CampaignProduct>())
                if (candidate.campaignLevel == CampaignProgression.GetCurrentLevel()) { product = candidate.transform; break; }
        }
        if (performance == 2 && product != null)
        {
            Vector3 direction = transform.InverseTransformDirection(product.position - transform.position);
            SetMuscle("Head Turn Left-Right", Mathf.Clamp(Mathf.Atan2(direction.x, direction.z) / Mathf.PI, -.3f, .3f) * expression);
        }
        SetMuscle("Head Nod Down-Up", performance == 0 ? 0f : .035f * wave * expression);
        if(walking)
        {
            float stride=Mathf.Sin(elapsed*8f)*.45f;
            SetMuscle("Left Upper Leg Front-Back",stride);SetMuscle("Right Upper Leg Front-Back",-stride);
            SetMuscle("Left Lower Leg Stretch",-.2f-Mathf.Max(0,-stride));SetMuscle("Right Lower Leg Stretch",-.2f-Mathf.Max(0,stride));
            SetMuscle("Left Arm Front-Back",-stride*.5f);SetMuscle("Right Arm Front-Back",stride*.5f);
        }
        poseHandler.SetHumanPose(ref pose);
    }

    private void SetMuscle(string name, float value)
    {
        if (muscles.TryGetValue(name, out int index)) pose.muscles[index] = value;
    }

    private void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
        poseHandler?.Dispose();
        foreach (var material in materials) if (material != null) Destroy(material);
    }
}

