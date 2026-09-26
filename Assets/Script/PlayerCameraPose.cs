using UnityEngine;

// Applied after locomotion and player look, before the recording camera updates.
// Solves the existing humanoid arms without replacing its locomotion controller.
public sealed class PlayerCameraPose
{
    private sealed class Arm
    {
        public Transform upper, lower, hand, finger, thumb;
        public Quaternion upperPose, lowerPose, handPose;
        public bool applied;
    }
    private readonly Arm left, right;
    private float weight;

    public PlayerCameraPose(Animator animator)
    {
        left = Create(animator, true);
        right = Create(animator, false);
    }

    private static Arm Create(Animator animator, bool left)
    {
        return new Arm {
            upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm),
            lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm),
            hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand),
            finger = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal),
            thumb = animator.GetBoneTransform(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal)
        };
    }

    public void RestoreAnimation()
    {
        Restore(left); Restore(right);
    }

    public void Reset()
    {
        RestoreAnimation();
        weight = 0f;
    }

    private static void Restore(Arm arm)
    {
        if (!arm.applied) return;
        if (arm.upper != null) arm.upper.localRotation = arm.upperPose;
        if (arm.lower != null) arm.lower.localRotation = arm.lowerPose;
        if (arm.hand != null) arm.hand.localRotation = arm.handPose;
        arm.applied = false;
    }

    public void Apply(Vector3 rightGrip, Vector3 leftGrip, Quaternion aim)
    {
        weight = Mathf.MoveTowards(weight, 1f, Time.deltaTime * 6f);
        Solve(right, rightGrip, aim, false, weight);
        Solve(left, leftGrip, aim, true, weight);
    }

    private static void Solve(Arm arm, Vector3 target, Quaternion aim, bool left, float blend)
    {
        if (arm.upper == null || arm.lower == null || arm.hand == null) return;
        arm.upperPose = arm.upper.localRotation;
        arm.lowerPose = arm.lower.localRotation;
        arm.handPose = arm.hand.localRotation;
        arm.applied = true;
        Vector3 shoulder = arm.upper.position;
        float upperLength = Vector3.Distance(shoulder, arm.lower.position);
        float lowerLength = Vector3.Distance(arm.lower.position, arm.hand.position);
        Vector3 delta = target - shoulder;
        if (upperLength < .001f || lowerLength < .001f || delta.sqrMagnitude < .000001f) return;
        float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(upperLength - lowerLength) + .001f,
            upperLength + lowerLength - .001f);
        Vector3 direction = delta.normalized;
        Vector3 bend = Vector3.ProjectOnPlane(aim * new Vector3(left ? -.45f : .45f, -1f, -.2f), direction).normalized;
        if (bend.sqrMagnitude < .001f) bend = Vector3.Cross(direction, aim * Vector3.forward).normalized;
        float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
        Vector3 elbow = shoulder + direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        arm.upper.rotation = Quaternion.FromToRotation(arm.lower.position - shoulder, elbow - shoulder) * arm.upper.rotation;
        arm.lower.rotation = Quaternion.FromToRotation(arm.hand.position - arm.lower.position,
            shoulder + direction * distance - arm.lower.position) * arm.lower.rotation;
        if (arm.finger != null && arm.thumb != null)
        {
            Vector3 fingers = arm.finger.position - arm.hand.position;
            Vector3 thumb = arm.thumb.position - arm.hand.position;
            if (Vector3.Cross(fingers, thumb).sqrMagnitude > .00000001f)
            {
                Quaternion basis = Quaternion.LookRotation(fingers, thumb);
                Quaternion desired = Quaternion.LookRotation(aim * (left ? Vector3.right : Vector3.down), aim * Vector3.forward);
                arm.hand.rotation = desired * Quaternion.Inverse(basis) * arm.hand.rotation;
            }
        }
        arm.upper.localRotation = Quaternion.Slerp(arm.upperPose, arm.upper.localRotation, blend);
        arm.lower.localRotation = Quaternion.Slerp(arm.lowerPose, arm.lower.localRotation, blend);
        arm.hand.localRotation = Quaternion.Slerp(arm.handPose, arm.hand.localRotation, blend);
    }
}
