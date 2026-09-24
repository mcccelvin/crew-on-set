using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Director-controlled performer with repeatable gestures and optional straight-line blocking.
public sealed class ActorBot : MonoBehaviour
{
    // Opt-in network adapter. Career actors never set this flag.
    private bool networkMotion;
    public static bool TryCreateForCrew(GameObject root, int skillTier)
    {
        var catalog = Resources.Load<ProductModelCatalog>("ProductModels");
        if (catalog == null || catalog.coffeeActorModels == null || catalog.coffeeActorModels.Length == 0) return false;
        var model = catalog.coffeeActorModels[Mathf.Clamp(skillTier, 0, catalog.coffeeActorModels.Length - 1)];
        return model != null && TryCreateIndividualCoffeeActor(root, skillTier, model, catalog.coffeeActorScale);
    }
    public void SetCrewMotion(Vector3 position, Quaternion rotation, bool moving)
    {
        networkMotion = true;
        walking = moving;
        transform.SetPositionAndRotation(position, rotation);
    }
    public void ShowCrewPlacementPreview(Vector3 position, float yaw)
    {
        ShowPlacementPreview(position);
        if (placementPreview != null) placementPreview.transform.rotation = Quaternion.Euler(0, yaw, 0);
    }
    private readonly System.Collections.Generic.List<Mesh> actorMeshes = new System.Collections.Generic.List<Mesh>();
    private Animator animator;
    private GameObject placementPreview;
    private readonly System.Collections.Generic.List<Material> previewMaterials = new System.Collections.Generic.List<Material>();

    public void ShowPlacementPreview(Vector3 position)
    {
        if (placementPreview == null)
        {
            placementPreview = new GameObject("Actor position preview");
            placementPreview.transform.SetParent(transform.parent, false);
            placementPreview.transform.localPosition = transform.localPosition;
            placementPreview.transform.localRotation = transform.localRotation;
            placementPreview.transform.localScale = transform.localScale;
            var transforms = new System.Collections.Generic.Dictionary<Transform, Transform>();
            transforms.Add(transform, placementPreview.transform);
            CopyPreviewTransforms(transform, placementPreview.transform, transforms);
            foreach (var source in GetComponentsInChildren<Renderer>())
            {
                if (!source.enabled || !source.gameObject.activeInHierarchy) continue;
                var part = transforms[source.transform].gameObject;
                Renderer renderer;
                if (source is SkinnedMeshRenderer skinned)
                {
                    // Keep the original mesh, bind poses and bone transforms together.
                    // Baking imported rigs can apply their FBX scale differently.
                    var copy = part.AddComponent<SkinnedMeshRenderer>();
                    copy.sharedMesh = skinned.sharedMesh;
                    var bones = skinned.bones;
                    for (int i = 0; i < bones.Length; i++)
                        if (bones[i] != null && transforms.TryGetValue(bones[i], out var bone)) bones[i] = bone;
                    copy.bones = bones;
                    if (skinned.rootBone != null && transforms.TryGetValue(skinned.rootBone, out var rootBone)) copy.rootBone = rootBone;
                    copy.localBounds = skinned.localBounds;
                    copy.quality = skinned.quality;
                    copy.updateWhenOffscreen = true;
                    if (skinned.sharedMesh != null)
                        for (int i = 0; i < skinned.sharedMesh.blendShapeCount; i++)
                            copy.SetBlendShapeWeight(i, skinned.GetBlendShapeWeight(i));
                    renderer = copy;
                }
                else if (source is MeshRenderer)
                {
                    var mesh = source.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) continue;
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    renderer = part.AddComponent<MeshRenderer>();
                }
                else continue;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                var originals = source.sharedMaterials;
                var transparent = new Material[originals.Length];
                for (int i = 0; i < originals.Length; i++)
                {
                    if (originals[i] == null) continue;
                    var material = new Material(originals[i]);
                    if (material.HasProperty("_BaseColor")) { var color = material.GetColor("_BaseColor"); color.a = .3f; material.SetColor("_BaseColor", color); }
                    if (material.HasProperty("_Color")) { var color = material.GetColor("_Color"); color.a = .3f; material.SetColor("_Color", color); }
                    if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
                    if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 2f);
                    if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0);
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.EnableKeyword("_ALPHABLEND_ON");
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    transparent[i] = material;
                    previewMaterials.Add(material);
                }
                renderer.sharedMaterials = transparent;
            }
        }
        placementPreview.transform.position = position;
        placementPreview.SetActive(true);
    }

    public void HidePlacementPreview()
    {
        if (placementPreview != null) placementPreview.SetActive(false);
    }

    private static void CopyPreviewTransforms(Transform source, Transform target,
        System.Collections.Generic.Dictionary<Transform, Transform> transforms)
    {
        target.gameObject.layer = 2;
        foreach (Transform child in source)
        {
            var copy = new GameObject(child.name).transform;
            copy.SetParent(target, false);
            copy.localPosition = child.localPosition;
            copy.localRotation = child.localRotation;
            copy.localScale = child.localScale;
            copy.gameObject.SetActive(child.gameObject.activeSelf);
            transforms.Add(child, copy);
            CopyPreviewTransforms(child, copy, transforms);
        }
    }

    public void ClearPlacementPreview()
    {
        if (placementPreview != null) { placementPreview.SetActive(false); Destroy(placementPreview); }
        placementPreview = null;
        foreach (var material in previewMaterials) if (material != null) Destroy(material);
        previewMaterials.Clear();
    }
    private HumanPoseHandler poseHandler;
    private HumanPose pose;
    private HumanPose restingPose;
    private PlayableGraph graph;
    private AnimationClipPlayable idle;
    private AnimationClipPlayable walkAnimation;
    private AnimationMixerPlayable locomotion;
    private int tier;
    private int performance;
    private float elapsed;
    private Transform product;
    private float nextTargetCheck;
    private Vector3 startMark,endMark;
    private Quaternion startFacing;
    private bool hasStart,hasEnd,walking;
    private Contract4Interactable furniture;
    private bool furnitureActive;
    private CampaignProduct heldProduct;
    private Transform heldOriginalParent;
    private Vector3 heldPosition, heldScale;
    private Quaternion heldRotation;
    private Collider[] heldColliders;
    private bool[] heldColliderStates;
    private Rigidbody heldBody;
    private bool heldWasKinematic;
    public bool IsHoldingProduct => heldProduct != null;

    public bool HoldProduct(CampaignProduct item)
    {
        if (item == null || animator == null || poseHandler == null) return false;
        var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null || item.Holder != null)
        { GameFeedback.Show("That product is already held or this actor has no hand rig."); return false; }
        ReleaseProduct();
        if (!item.TryClaim(transform)) return false;
        heldProduct = item;
        heldOriginalParent = item.transform.parent;
        heldPosition = item.transform.localPosition;
        heldRotation = item.transform.localRotation;
        heldScale = item.transform.localScale;
        heldBody = item.GetComponent<Rigidbody>();
        if (heldBody != null) { heldWasKinematic = heldBody.isKinematic; heldBody.isKinematic = true; }
        heldColliders = item.GetComponentsInChildren<Collider>();
        heldColliderStates = new bool[heldColliders.Length];
        for (int i = 0; i < heldColliders.Length; i++)
        { heldColliderStates[i] = heldColliders[i].enabled; heldColliders[i].enabled = false; }
        EvaluatePerformance();
        item.transform.SetParent(hand, true);
        item.transform.position = hand.position;
        // Centre the visible product in the palm even when the FBX pivot is offset.
        var parts = item.GetComponentsInChildren<Renderer>();
        if (parts.Length > 0)
        {
            Bounds bounds = parts[0].bounds;
            foreach (var part in parts) bounds.Encapsulate(part.bounds);
            item.transform.position += hand.position + transform.forward * .06f - bounds.center;
        }
        GameFeedback.Show("ACTOR HOLDING PRODUCT\n[O] Return product to its mark. You can film or seat the actor while holding it.");
        return true;
    }

    public void ReleaseProduct()
    {
        if (heldProduct != null)
        {
            heldProduct.transform.SetParent(heldOriginalParent, false);
            heldProduct.transform.localPosition = heldPosition;
            heldProduct.transform.localRotation = heldRotation;
            heldProduct.transform.localScale = heldScale;
            heldProduct.Release(transform);
        }
        if (heldBody != null) heldBody.isKinematic = heldWasKinematic;
        if (heldColliders != null)
            for (int i = 0; i < heldColliders.Length; i++)
                if (heldColliders[i] != null) heldColliders[i].enabled = heldColliderStates[i];
        heldProduct = null;
        heldBody = null;
        heldColliders = null;
    }
    private Vector3 furnitureReturnPosition, furnitureVisualPosition;
    private Quaternion furnitureReturnRotation;
    public string FurniturePoseName => !furnitureActive ? null :
        furniture != null && furniture.action == Contract4Interactable.Action.Sit ? "Sitting" : "Using Machine";

    public void PerformFurnitureAction(Contract4Interactable.Action action)
    {
        if (poseHandler == null) { GameFeedback.Show("This actor needs a Humanoid rig."); return; }
        Contract4Interactable nearest = null;
        float best = 36f;
        foreach (var candidate in FindObjectsOfType<Contract4Interactable>())
        {
            if (candidate.action != action) continue;
            var collider = candidate.GetComponent<Collider>();
            if (collider == null || !collider.enabled) continue; // Exclude tablet previews.
            bool occupied = false;
            foreach (var other in FindObjectsOfType<ActorBot>())
                if (other != this && other.furnitureActive && other.furniture != null &&
                    Vector3.Distance(other.furniture.Bounds.center, candidate.Bounds.center) < .3f) occupied = true;
            if (occupied) continue;
            float distance = (candidate.Bounds.center - transform.position).sqrMagnitude;
            if (distance < best) { best = distance; nearest = candidate; }
        }
        if (nearest == null) { GameFeedback.Show("Place this actor near an available " + (action == Contract4Interactable.Action.Sit ? "stool." : "coffee machine.")); return; }
        PerformFurnitureAction(nearest);
    }

    public bool PerformFurnitureAction(Contract4Interactable nearest)
    {
        if (nearest != null && nearest.isActiveAndEnabled && nearest.action == Contract4Interactable.Action.Product)
            return HoldProduct(nearest.GetComponent<CampaignProduct>());
        if (poseHandler == null || nearest == null || !nearest.isActiveAndEnabled || nearest.action == Contract4Interactable.Action.Product) return false;
        foreach (var other in FindObjectsOfType<ActorBot>())
            if (other != this && other.furnitureActive && other.furniture != null &&
                Vector3.Distance(other.furniture.Bounds.center, nearest.Bounds.center) < .3f)
            { GameFeedback.Show("That target is occupied by another actor."); return false; }
        var action = nearest.action;
        StopFurnitureAction();
        furnitureReturnPosition = transform.position;
        furnitureReturnRotation = transform.rotation;
        furnitureVisualPosition = animator.transform.localPosition;
        furniture = nearest;
        furnitureActive = true;
        if (action == Contract4Interactable.Action.Machine) CoffeeActionAudio.Begin(gameObject, nearest.transform);
        walking = false;
        elapsed = 0f;
        if (action == Contract4Interactable.Action.Machine)
        {
            Vector3 outward = transform.position - nearest.Bounds.center;
            outward.y = 0;
            if (outward.sqrMagnitude < .001f) outward = -transform.forward;
            outward.Normalize();
            float clearance = Mathf.Abs(outward.x) * nearest.Bounds.extents.x + Mathf.Abs(outward.z) * nearest.Bounds.extents.z + .4f;
            Vector3 mark = nearest.Bounds.center + outward * clearance;
            mark.y = transform.position.y;
            transform.SetPositionAndRotation(mark, Quaternion.LookRotation(-outward));
        }
        else
        {
            if (nearest.seatAnchor != null) transform.rotation = nearest.seatAnchor.rotation;
            Vector3 seat = nearest.SeatPosition;
            transform.position = new Vector3(seat.x, transform.position.y, seat.z);
        }
        EvaluatePerformance();
        GameFeedback.Show(FurniturePoseName + ". Equip the camera to film. Megaphone O: stop; R: turn.");
        return true;
    }

    public void StopFurnitureAction()
    {
        CoffeeActionAudio.End(gameObject);
        if (!furnitureActive) return;
        furnitureActive = false;
        furniture = null;
        transform.SetPositionAndRotation(furnitureReturnPosition, furnitureReturnRotation);
        if (animator != null) animator.transform.localPosition = furnitureVisualPosition;
    }
    public bool HasWalk => hasStart && hasEnd;
    public bool HasStartMark => hasStart;
    public bool WalkCompleted { get; private set; }
    public bool ReturnedAfterWalk { get; private set; }
    public void SetStartMark()
    {
        StopFurnitureAction();
        walking = false;
        startMark = transform.position;
        startFacing = transform.rotation;
        hasStart = true;
        hasEnd = false;
        WalkCompleted = ReturnedAfterWalk = false;
    }
    public void SetEndMark()
    {
        StopFurnitureAction();
        walking = false;
        endMark = transform.position;
        WalkCompleted = ReturnedAfterWalk = false;
        endMark.y = startMark.y;
        hasEnd = hasStart && Vector3.Distance(startMark, endMark) > .2f;
        GameFeedback.Show(hasEnd ? "END saved. K: rehearse. J: return to start. Recording repeats this walk. H: clear walk."
            : "Set START with B first, then move the actor at least 0.2 metres before pressing N.");
    }
    public void MoveBy(Vector3 offset)
    {
        StopFurnitureAction();
        walking = false;
        transform.position += new Vector3(offset.x, 0f, offset.z);
    }
    public void ReturnToStartMark()
    {
        StopFurnitureAction();
        walking = false;
        if (hasStart && WalkCompleted) ReturnedAfterWalk = true;
        if (hasStart) { transform.position = startMark; transform.rotation = startFacing; }
    }
    public void ClearWalk() { ReturnToStartMark(); hasStart = hasEnd = false; WalkCompleted = ReturnedAfterWalk = false; }
    public void RehearseWalk()
    {
        if (!HasWalk) { GameFeedback.Show("Place the actor at START and press B, then move to END and press N."); return; }
        ReturnToStartMark();
        WalkCompleted = ReturnedAfterWalk = false;
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
        var catalog = Resources.Load<ProductModelCatalog>("ProductModels");
        if (CampaignProgression.GetCurrentLevel() == 4 && catalog != null && catalog.coffeeActorModels != null)
        {
            int index = Mathf.Clamp(skillTier, 0, 2);
            if (index < catalog.coffeeActorModels.Length && catalog.coffeeActorModels[index] != null
                && TryCreateIndividualCoffeeActor(actor, skillTier, catalog.coffeeActorModels[index], catalog.coffeeActorScale))
                return true;
        }
        if (CampaignProgression.GetCurrentLevel() == 4 && catalog != null && catalog.coffeeActors != null
            && TryCreateCoffeeActor(actor, skillTier, catalog.coffeeActors)) return true;
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
            bot.ConnectPlayerWalk(output);
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
            visual.transform.localScale *= ProductModelCatalog.StandardCharacterHeight / Mathf.Max(.01f, bounds.size.y);
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.transform.position += Vector3.up * (actor.transform.position.y - bounds.min.y);
        }
        var selection = actor.AddComponent<BoxCollider>();
        selection.center = new Vector3(0,ProductModelCatalog.StandardCharacterHeight * .5f,0);
        selection.size = new Vector3(.8f,ProductModelCatalog.StandardCharacterHeight,.55f);
        return true;
    }

    // Separate FBXs already contain one complete character, including clothing.
    private static bool TryCreateIndividualCoffeeActor(GameObject actor, int skillTier, GameObject source, float scale)
    {
        var visual = Instantiate(source, actor.transform, false);
        visual.name = "Coffee Actor " + (Mathf.Clamp(skillTier, 0, 2) + 1) + " Visual";
        visual.transform.localScale *= Mathf.Max(.01f, scale);
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var camera in visual.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (var light in visual.GetComponentsInChildren<Light>(true)) light.enabled = false;
        var rig = visual.GetComponentInChildren<Animator>();
        if (rig == null || rig.avatar == null || !rig.avatar.isValid || !rig.avatar.isHuman)
        {
            Debug.LogWarning("Coffee actor needs a valid Humanoid avatar: " + source.name, source);
            visual.SetActive(false);
            Destroy(visual);
            return false;
        }
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { visual.SetActive(false); Destroy(visual); return false; }
        foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
        foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = true;
        Bounds modelBounds = renderers[0].bounds;
        foreach (var renderer in renderers) modelBounds.Encapsulate(renderer.bounds);
        visual.transform.localScale *= ProductModelCatalog.StandardCharacterHeight / Mathf.Max(.01f, modelBounds.size.y);
        modelBounds = renderers[0].bounds;
        foreach (var renderer in renderers) modelBounds.Encapsulate(renderer.bounds);
        var bot = actor.AddComponent<ActorBot>();
        bot.tier = Mathf.Clamp(skillTier, 0, 2);
        bot.InitializeHumanoid(rig);
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        visual.transform.position += actor.transform.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        // Build the selection volume from the scaled model rather than a fixed human height.
        Bounds localBounds = new Bounds();
        bool initialized = false;
        foreach (var renderer in renderers)
        {
            Bounds world = renderer.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = world.center + Vector3.Scale(world.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = actor.transform.InverseTransformPoint(corner);
                if (!initialized) { localBounds = new Bounds(point, Vector3.zero); initialized = true; }
                else localBounds.Encapsulate(point);
            }
        }
        var selection = actor.AddComponent<BoxCollider>();
        selection.center = localBounds.center;
        selection.size = localBounds.size;
        return true;
    }

    // Retarget the existing director performances onto each actor's own Humanoid skeleton.
    private void InitializeHumanoid(Animator rig)
    {
        animator = rig;
        rig.enabled = true;
        rig.runtimeAnimatorController = null;
        rig.applyRootMotion = false;
        rig.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        rig.Rebind();
        foreach (var clip in Resources.LoadAll<AnimationClip>("Character/Animations/Stand--Idle.anim"))
        {
            if (clip.name.StartsWith("__preview__") || !clip.humanMotion) continue;
            graph = PlayableGraph.Create("Coffee actor idle");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            idle = AnimationClipPlayable.Create(graph, clip);
            var output = AnimationPlayableOutput.Create(graph, "Actor", rig);
            output.SetSourcePlayable(idle);
            ConnectPlayerWalk(output);
            graph.Play();
            graph.Evaluate(0f);
            break;
        }
        poseHandler = new HumanPoseHandler(rig.avatar, rig.transform);
        poseHandler.GetHumanPose(ref pose);
        restingPose = pose;
        restingPose.muscles = (float[])pose.muscles.Clone();
        EvaluatePerformance();
    }

    // Reuse the actual player clip so both Humanoid rigs perform the same walk.
    private void ConnectPlayerWalk(AnimationPlayableOutput output)
    {
        var player = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (player == null) return;
        foreach (var rig in player.GetComponentsInChildren<Animator>(true))
        {
            var controller = rig.runtimeAnimatorController;
            if (controller == null) continue;
            foreach (var clip in controller.animationClips)
            {
                if (!clip.humanMotion || !string.Equals(clip.name, "walk", System.StringComparison.OrdinalIgnoreCase)) continue;
                walkAnimation = AnimationClipPlayable.Create(graph, clip);
                locomotion = AnimationMixerPlayable.Create(graph, 2);
                graph.Connect(idle, 0, locomotion, 0);
                graph.Connect(walkAnimation, 0, locomotion, 1);
                locomotion.SetInputWeight(0, 1f);
                locomotion.SetInputWeight(1, 0f);
                output.SetSourcePlayable(locomotion);
                return;
            }
        }
    }

    // Legacy combined FBX support. Preserve its authored materials.
    private static bool TryCreateCoffeeActor(GameObject actor, int skillTier, GameObject source)
    {
        var visual = Instantiate(source, actor.transform, false);
        visual.name = "Coffee Actor Visual";
        var renderers = visual.GetComponentsInChildren<Renderer>();
        var bodies = new System.Collections.Generic.List<Renderer>();
        foreach (var renderer in renderers)
            if (renderer.name.IndexOf("actor body", System.StringComparison.OrdinalIgnoreCase) >= 0)
                bodies.Add(renderer);
        if (bodies.Count == 0) { visual.SetActive(false); Destroy(visual); return false; }
        bodies.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        var selected = bodies[Mathf.Clamp(skillTier, 0, bodies.Count - 1)];
        var bodyCentres = new Vector3[bodies.Count];
        for (int i = 0; i < bodies.Count; i++) bodyCentres[i] = bodies[i].bounds.center;
        var bot = actor.AddComponent<ActorBot>();
        bot.tier = Mathf.Clamp(skillTier, 0, 2);
        var retained = new System.Collections.Generic.List<Renderer>();
        // Clothing can share one mesh across multiple characters. Split by triangle,
        // rather than hiding the entire apron/pants/shoes renderer by its centre.
        foreach (var renderer in renderers)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable)
            {
                renderer.enabled = ClosestCoffeeBody(renderer.bounds.center, bodies, bodyCentres) == selected;
            }
            else
            {
                var mesh = Instantiate(filter.sharedMesh);
                var vertices = mesh.vertices;
                bool any = false;
                Bounds keptBounds = new Bounds();
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var triangles = mesh.GetTriangles(sub);
                    var kept = new System.Collections.Generic.List<int>();
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        Vector3 centre = (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3f;
                        if (ClosestCoffeeBody(renderer.transform.TransformPoint(centre), bodies, bodyCentres) != selected) continue;
                        for (int j = 0; j < 3; j++)
                        {
                            int index = triangles[i + j];
                            kept.Add(index);
                            if (!any) { keptBounds = new Bounds(vertices[index], Vector3.zero); any = true; }
                            else keptBounds.Encapsulate(vertices[index]);
                        }
                    }
                    mesh.SetTriangles(kept, sub, false);
                }
                mesh.bounds = keptBounds;
                filter.sharedMesh = mesh;
                bot.actorMeshes.Add(mesh);
                renderer.enabled = any;
            }
            if (renderer.enabled) retained.Add(renderer);
        }
        if (retained.Count == 0) { visual.SetActive(false); Destroy(visual); Destroy(bot); return false; }
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var camera in visual.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (var light in visual.GetComponentsInChildren<Light>(true)) light.enabled = false;
        foreach (var animator in visual.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        Bounds bounds = retained[0].bounds;
        foreach (var renderer in retained) bounds.Encapsulate(renderer.bounds);
        visual.transform.localScale *= ProductModelCatalog.StandardCharacterHeight / Mathf.Max(.01f, bounds.size.y);
        bounds = retained[0].bounds;
        foreach (var renderer in retained) bounds.Encapsulate(renderer.bounds);
        visual.transform.position += actor.transform.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        var selection = actor.AddComponent<BoxCollider>();
        selection.center = new Vector3(0, ProductModelCatalog.StandardCharacterHeight * .5f, 0);
        selection.size = new Vector3(.8f, ProductModelCatalog.StandardCharacterHeight, .55f);
        return true;
    }

    private static Renderer ClosestCoffeeBody(Vector3 point, System.Collections.Generic.List<Renderer> bodies, Vector3[] centres)
    {
        Renderer nearest = null;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            Vector3 delta = point - centres[i];
            float candidate = delta.x * delta.x + delta.z * delta.z;
            if (candidate < distance) { distance = candidate; nearest = body; }
        }
        return nearest;
    }

    public void SetPerformance(int action)
    {
        StopFurnitureAction();
        performance = Mathf.Clamp(action, 0, 2);
        RestartTake();
    }

    public void RestartTake()
    {
        if (!furnitureActive && HasWalk) RehearseWalk();
        elapsed = 0f;
        if (idle.IsValid()) idle.SetTime(0);
        EvaluatePerformance();
    }

    private void LateUpdate()
    {
        if (PauseManager.isPaused) return;
        if (furnitureActive && (furniture == null || !furniture.gameObject.activeInHierarchy)) StopFurnitureAction();
        elapsed += Time.deltaTime;
        if(walking && !networkMotion)
        {
            var direction=endMark-transform.position;direction.y=0;
            if(direction.magnitude<.03f) { transform.position=endMark; walking=false; WalkCompleted=true; }
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
        if (furnitureActive) animator.transform.localPosition = furnitureVisualPosition;
        if (graph.IsValid())
        {
            float length = idle.GetAnimationClip().length;
            if (length > 0f) idle.SetTime(Mathf.Repeat(elapsed, length));
            if (walkAnimation.IsValid())
            {
                var clip = walkAnimation.GetAnimationClip();
                float clipTravelSpeed = clip.averageSpeed.magnitude;
                float playback = clipTravelSpeed > .01f ? .9f / clipTravelSpeed : 1f;
                walkAnimation.SetTime(Mathf.Repeat(elapsed * playback, Mathf.Max(.001f, clip.length)));
                locomotion.SetInputWeight(0, walking ? 0f : 1f);
                locomotion.SetInputWeight(1, walking ? 1f : 0f);
            }
            graph.Evaluate(0f);
            poseHandler.GetHumanPose(ref pose);
            if (walking && walkAnimation.IsValid())
            {
                pose.bodyPosition = restingPose.bodyPosition;
                pose.bodyRotation = restingPose.bodyRotation;
            }
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
        if (!walking || !walkAnimation.IsValid())
        {
            SetMuscle("Left Arm Down-Up", -.65f);
            SetMuscle("Right Arm Down-Up", -.65f);
        }
        if (!furnitureActive && performance == 1)
        {
            SetMuscle("Left Arm Down-Up", .45f + .12f * wave * expression);
            SetMuscle("Left Arm Front-Back", .1f);
            SetMuscle("Left Forearm Stretch", -.2f + .2f * wave * expression);
            SetMuscle("Left Hand Down-Up", .25f * wave * expression);
        }
        else if (!furnitureActive && performance == 2)
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
        if (!furnitureActive && performance == 2 && product != null)
        {
            Vector3 direction = transform.InverseTransformDirection(product.position - transform.position);
            SetMuscle("Head Turn Left-Right", Mathf.Clamp(Mathf.Atan2(direction.x, direction.z) / Mathf.PI, -.3f, .3f) * expression);
        }
        SetMuscle("Head Nod Down-Up", performance == 0 ? 0f : .035f * wave * expression);
        if(walking && !walkAnimation.IsValid())
        {
            float stride=Mathf.Sin(elapsed*8f)*.45f;
            SetMuscle("Left Upper Leg Front-Back",stride);SetMuscle("Right Upper Leg Front-Back",-stride);
            SetMuscle("Left Lower Leg Stretch",-.2f-Mathf.Max(0,-stride));SetMuscle("Right Lower Leg Stretch",-.2f-Mathf.Max(0,stride));
            SetMuscle("Left Arm Front-Back",-stride*.5f);SetMuscle("Right Arm Front-Back",stride*.5f);
        }
        if (furnitureActive && furniture != null)
        {
            SetMuscle("Left Arm Down-Up", -.65f);
            SetMuscle("Right Arm Down-Up", -.5f);
            SetMuscle("Right Arm Front-Back", .45f);
            SetMuscle("Right Forearm Stretch", -.6f);
            if (furniture.action == Contract4Interactable.Action.Sit)
            {
                SetMuscle("Left Upper Leg Front-Back", .75f);
                SetMuscle("Right Upper Leg Front-Back", .75f);
                SetMuscle("Left Lower Leg Stretch", -.8f);
                SetMuscle("Right Lower Leg Stretch", -.8f);
            }
            else
            {
                SetMuscle("Right Forearm Stretch", -.35f + .2f * Mathf.Sin(elapsed * 5f));
                SetMuscle("Right Hand Down-Up", .15f * Mathf.Sin(elapsed * 5f));
                SetMuscle("Head Nod Down-Up", -.12f);
            }
        }
        if (heldProduct != null)
        {
            SetMuscle("Right Arm Down-Up", -.5f);
            SetMuscle("Right Arm Front-Back", .35f);
            SetMuscle("Right Forearm Stretch", -.65f);
            SetMuscle("Right Hand Down-Up", 0f);
            SetMuscle("Right Thumb 1 Stretched", -.3f);
            SetMuscle("Right Index 1 Stretched", -.5f);
            SetMuscle("Right Middle 1 Stretched", -.5f);
            SetMuscle("Right Ring 1 Stretched", -.5f);
            SetMuscle("Right Little 1 Stretched", -.5f);
        }
        poseHandler.SetHumanPose(ref pose);
        if (furnitureActive && furniture != null && furniture.action == Contract4Interactable.Action.Sit)
        {
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            animator.transform.position += furniture.SeatPosition + Vector3.up * .08f - hips.position;
        }
    }

    private void SetMuscle(string name, float value)
    {
        if (muscles.TryGetValue(name, out int index)) pose.muscles[index] = value;
    }

    private void OnDisable() { ClearPlacementPreview(); ReleaseProduct(); }

    private void OnDestroy()
    {
        ClearPlacementPreview();
        ReleaseProduct();
        if (graph.IsValid()) graph.Destroy();
        poseHandler?.Dispose();
        foreach (var mesh in actorMeshes) if (mesh != null) Destroy(mesh);
        foreach (var material in materials) if (material != null) Destroy(material);
    }
}
