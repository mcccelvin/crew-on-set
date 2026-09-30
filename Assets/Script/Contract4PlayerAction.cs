using UnityEngine;

// Applies an animated Humanoid pose after locomotion, without replacing its controller.
[DefaultExecutionOrder(1000)]
public sealed class Contract4PlayerAction : MonoBehaviour
{
    private Contract4Interactable target;
    private Animator rig;
    private HumanPoseHandler handler;
    private HumanPose pose;
    private Player.PlayerController.PlayerController controller;
    private Rigidbody body;
    private bool controllerEnabled, wasKinematic;
    private bool wasStationary;
    private Vector3 returnPosition, visualPosition;
    private Quaternion returnRotation;
    private Transform cameraTransform;
    private Vector3 cameraPosition;
    private Transform originalParent;
    private Vector3 productPosition, productScale;
    private Quaternion productRotation;
    private Collider[] colliders;
    private bool[] colliderStates;
    private Rigidbody productBody;
    private bool productKinematic;
    private float elapsed;
    private bool holdingProduct;
    private bool approachingChair, leavingChair;
    private Vector3 chairPosition;
    private Quaternion chairRotation;
    private CampaignProduct claimedProduct;
    private readonly CoffeeCharacterMotion coffeeMotion = new CoffeeCharacterMotion();
    public bool IsActive => target != null;

    public void Begin(Contract4Interactable item)
    {
        if (IsActive || item == null) return;
        rig = null;
        foreach (var candidate in GetComponentsInChildren<Animator>())
            if (candidate.enabled && candidate.avatar != null && candidate.avatar.isValid && candidate.avatar.isHuman)
            { rig = candidate; break; }
        if (rig == null) { GameFeedback.Show("This action needs an active Humanoid character."); return; }
        if (item.action == Contract4Interactable.Action.Product)
        {
            coffeeMotion.Bind(item.transform, transform);
            var product = item.GetComponent<CampaignProduct>();
            if (rig.GetBoneTransform(HumanBodyBones.RightHand) == null || product == null || !product.TryClaim(transform))
            { GameFeedback.Show("This product is unavailable or already held."); return; }
            claimedProduct = product;
        }
        target = item;
        if (item.action == Contract4Interactable.Action.Machine) CoffeeActionAudio.Begin(gameObject, item.transform);
        holdingProduct = item.action == Contract4Interactable.Action.Product;
        handler = new HumanPoseHandler(rig.avatar, rig.transform);
        visualPosition = rig.transform.localPosition;
        elapsed = 0;
        returnPosition = transform.position;
        returnRotation = transform.rotation;
        controller = GetComponent<Player.PlayerController.PlayerController>();
        body = GetComponent<Rigidbody>();
        cameraTransform = GetComponentInChildren<Camera>()?.transform;
        if (cameraTransform != null) cameraPosition = cameraTransform.localPosition;

        if (item.action == Contract4Interactable.Action.Product)
        {
            originalParent = item.transform.parent;
            productPosition = item.transform.localPosition;
            productRotation = item.transform.localRotation;
            productScale = item.transform.localScale;
            productBody = item.GetComponent<Rigidbody>();
            if (productBody != null) { productKinematic = productBody.isKinematic; productBody.isKinematic = true; }
            SaveColliders(item.GetComponentsInChildren<Collider>());
            item.transform.SetParent(rig.GetBoneTransform(HumanBodyBones.RightHand), true);
            item.transform.localPosition = new Vector3(0, .04f, .08f);
            item.transform.localRotation = Quaternion.identity;
            GameFeedback.Show(claimedProduct.IsCoffeeCup ? "Holding coffee. [C] Drink | [E/G] Return cup." : "Holding product. [E] or [G]: return it to its mark.");
            return;
        }

        controllerEnabled = controller != null && controller.enabled;
        if (controller != null)
        {
            wasStationary = controller.StationaryAction;
            if (item.action == Contract4Interactable.Action.Sit)
                controller.StationaryAction = true;
            else
                controller.enabled = false;
        }
        if (body != null) { wasKinematic = body.isKinematic; if (!wasKinematic) body.velocity = Vector3.zero; body.isKinematic = true; }
        SaveColliders(GetComponents<Collider>());
        Vector3 facing = item.action == Contract4Interactable.Action.Sit
            ? returnPosition - item.Bounds.center : item.Bounds.center - returnPosition;
        facing.y = 0;
        if (facing.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(facing);
        if (item.action == Contract4Interactable.Action.Sit)
        {
            var center = item.SeatPosition;
            if (item.seatAnchor != null) transform.rotation = item.seatAnchor.rotation;
            chairPosition = new Vector3(center.x, returnPosition.y, center.z);
            chairRotation = transform.rotation;
            transform.rotation = returnRotation;
            approachingChair = true;
            leavingChair = false;
            if (controller != null) controller.SyncLookToCamera();
        }
        GameFeedback.Show(item.action == Contract4Interactable.Action.Sit
            ? "Walking to chair. [E/G]: return to your starting position." : "Using coffee machine... [E] or [G]: cancel.");
    }

    private void SaveColliders(Collider[] values)
    {
        colliders = values;
        colliderStates = new bool[values.Length];
        for (int i = 0; i < values.Length; i++) { colliderStates[i] = values[i].enabled; values[i].enabled = false; }
    }

    private void Muscle(string name, float value, float weight)
    {
        for (int i = 0; i < HumanTrait.MuscleCount; i++)
            if (HumanTrait.MuscleName[i] == name) { pose.muscles[i] = Mathf.Lerp(pose.muscles[i], value, weight); return; }
    }

    private void LateUpdate()
    {
        if (handler == null || PauseManager.isPaused) return;
        if (target == null || !target.gameObject.activeInHierarchy || CampaignProgression.GetCurrentLevel() < 4)
        { CompleteFinish(); return; }
        elapsed += Time.deltaTime;
        if (approachingChair || leavingChair)
        {
            Vector3 destination = leavingChair ? returnPosition : chairPosition;
            Vector3 direction = destination - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > .0025f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), 360f * Time.deltaTime);
                transform.position = Vector3.MoveTowards(transform.position, destination, 1.2f * Time.deltaTime);
                rig.transform.localPosition = visualPosition;
                if (cameraTransform != null) cameraTransform.localPosition = cameraPosition;
                handler.GetHumanPose(ref pose);
                float stride = Mathf.Sin(elapsed * 9f);
                Muscle("Left Upper Leg Front-Back", stride * .35f, 1f);
                Muscle("Right Upper Leg Front-Back", -stride * .35f, 1f);
                Muscle("Left Lower Leg Stretch", -.2f - Mathf.Max(0f, -stride) * .4f, 1f);
                Muscle("Right Lower Leg Stretch", -.2f - Mathf.Max(0f, stride) * .4f, 1f);
                Muscle("Left Arm Front-Back", -stride * .2f, 1f);
                Muscle("Right Arm Front-Back", stride * .2f, 1f);
                handler.SetHumanPose(ref pose);
                return;
            }
            if (leavingChair) { CompleteFinish(); return; }
            approachingChair = false;
            transform.SetPositionAndRotation(chairPosition, chairRotation);
            controller?.SyncLookToCamera();
            elapsed = 0f;
            GameFeedback.Show("Seated. [E/G]: walk back to your starting position.");
        }
        float weight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / .4f));
        rig.transform.localPosition = visualPosition;
        handler.GetHumanPose(ref pose);
        bool sit = target.action == Contract4Interactable.Action.Sit;
        Muscle("Left Arm Down-Up", -.65f, weight);
        Muscle("Right Arm Down-Up", -.5f, weight);
        Muscle("Right Arm Front-Back", .45f, weight);
        Muscle("Right Forearm Stretch", -.6f, weight);
        if (sit)
        {
            CoffeeCharacterMotion.SeatPose(ref pose, weight);
        }
        else if (target.action == Contract4Interactable.Action.Machine)
        {
            Muscle("Right Forearm Stretch", -.35f + .2f * Mathf.Sin(elapsed * 5), weight);
            Muscle("Right Hand Down-Up", .15f * Mathf.Sin(elapsed * 5), weight);
            Muscle("Head Nod Down-Up", -.12f, weight);
        }
        handler.SetHumanPose(ref pose);
        if (sit)
        {
            var hips = rig.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 offset = (target.SeatPosition + Vector3.up * .08f - hips.position) * weight;
            rig.transform.position += offset;
            CoffeeCharacterMotion.SeatFeet(rig, transform, target.SeatPosition, weight);
            if (cameraTransform != null)
                cameraTransform.position = cameraTransform.parent.TransformPoint(cameraPosition) + offset;
        }
        if (holdingProduct && claimedProduct != null && claimedProduct.IsCoffeeCup)
        {
            var keys = UnityEngine.InputSystem.Keyboard.current;
            if (keys != null && keys.cKey.wasPressedThisFrame && !Cursor.visible) coffeeMotion.Drink();
            coffeeMotion.ApplyCup(rig, transform, target.transform, Time.deltaTime);
        }
        if (target.action == Contract4Interactable.Action.Machine && elapsed >= 3f)
        { Finish(); GameFeedback.Show("Coffee machine action complete."); }
    }

    public void Finish()
    {
        if (handler == null || leavingChair) return;
        if (isActiveAndEnabled && target != null && target.action == Contract4Interactable.Action.Sit)
        {
            approachingChair = false;
            leavingChair = true;
            elapsed = 0f;
            if (rig != null) rig.transform.localPosition = visualPosition;
            if (cameraTransform != null) cameraTransform.localPosition = cameraPosition;
            GameFeedback.Show("Standing up and returning to your starting position.");
            return;
        }
        CompleteFinish();
    }

    private void CompleteFinish()
    {
        if (handler == null) return;
        approachingChair = leavingChair = false;
        coffeeMotion.Reset();
        CoffeeActionAudio.End(gameObject);
        if (holdingProduct && target != null) GameplayAudioManager.Play("Mug on Table");
        if (holdingProduct)
        {
            if (target != null)
            {
                target.transform.SetParent(originalParent, false);
                target.transform.localPosition = productPosition;
                target.transform.localRotation = productRotation;
                target.transform.localScale = productScale;
            }
            if (productBody != null) productBody.isKinematic = productKinematic;
        }
        else
        {
            transform.SetPositionAndRotation(returnPosition, returnRotation);
            if (body != null) body.isKinematic = wasKinematic;
            if (cameraTransform != null) cameraTransform.localPosition = cameraPosition;
            if (controller != null)
            {
                controller.StationaryAction = wasStationary;
                controller.enabled = controllerEnabled;
                controller.SyncLookToCamera();
            }
        }
        if (rig != null) rig.transform.localPosition = visualPosition;
        if (colliders != null)
            for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
        if (claimedProduct != null) claimedProduct.Release(transform);
        claimedProduct = null;
        handler.Dispose(); handler = null; target = null;
    }
    private void OnDisable() { CompleteFinish(); }
}
