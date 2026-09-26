using UnityEngine;

// Shared runtime animation for the existing Humanoid rigs; no controller replacement.
public sealed class CoffeeCharacterMotion
{
    private Vector3 rim, grip;
    private Quaternion cupFacing;
    private float drinkTime = -1f;
    private bool loopDrink;
    public bool Drinking => drinkTime >= 0f;

    public void Bind(Transform cup, Transform character)
    {
        var renderers = cup.GetComponentsInChildren<Renderer>();
        var bounds = new Bounds(cup.position, Vector3.zero);
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        }
        rim = cup.InverseTransformPoint(bounds.center + Vector3.up * bounds.extents.y);
        grip = cup.InverseTransformPoint(bounds.center + character.right * bounds.extents.x * .85f);
        cupFacing = Quaternion.Inverse(character.rotation) * cup.rotation;
        Reset();
    }

    public void Drink(bool loop = false) { loopDrink = loop; if (!Drinking) drinkTime = 0f; }
    public void Reset() { drinkTime = -1f; loopDrink = false; }

    public static void Muscle(ref HumanPose pose, string name, float value, float weight = 1f)
    {
        for (int i = 0; i < HumanTrait.MuscleCount; i++)
            if (HumanTrait.MuscleName[i] == name)
            { pose.muscles[i] = Mathf.Lerp(pose.muscles[i], value, weight); return; }
    }

    public static void SeatPose(ref HumanPose pose, float weight)
    {
        Muscle(ref pose, "Spine Front-Back", .08f, weight);
        Muscle(ref pose, "Chest Front-Back", -.04f, weight);
        foreach (string side in new[] { "Left", "Right" })
        {
            Muscle(ref pose, side + " Upper Leg Front-Back", .65f, weight);
            Muscle(ref pose, side + " Upper Leg In-Out", .08f, weight);
            Muscle(ref pose, side + " Lower Leg Stretch", -.7f, weight);
            Muscle(ref pose, side + " Arm Down-Up", -.75f, weight);
            Muscle(ref pose, side + " Arm Front-Back", .16f, weight);
            Muscle(ref pose, side + " Forearm Stretch", -.35f, weight);
        }
    }

    public static void SeatFeet(Animator rig, Transform character, Vector3 seat, float weight)
    {
        // Feet stay under the knees, with the knees pointing forward rather than folding inward.
        foreach (bool left in new[] { true, false })
        {
            var upper = rig.GetBoneTransform(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
            var lower = rig.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
            var foot = rig.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            if (upper == null || lower == null || foot == null) continue;
            float thigh = Vector3.Distance(upper.position, lower.position);
            float shin = Vector3.Distance(lower.position, foot.position);
            Vector3 target = seat + character.forward * thigh * .9f + character.right * (left ? -.12f : .12f);
            target.y = Mathf.Max(character.position.y + .09f, seat.y - shin);
            var footRotation = foot.rotation;
            Solve(upper, lower, foot, Vector3.Lerp(foot.position, target, weight), character.forward);
            foot.rotation = footRotation;
        }
    }

    public void ApplyCup(Animator rig, Transform character, Transform cup, float deltaTime)
    {
        var upper = rig.GetBoneTransform(HumanBodyBones.RightUpperArm);
        var lower = rig.GetBoneTransform(HumanBodyBones.RightLowerArm);
        var hand = rig.GetBoneTransform(HumanBodyBones.RightHand);
        var head = rig.GetBoneTransform(HumanBodyBones.Head);
        var chest = rig.GetBoneTransform(HumanBodyBones.Chest) ?? rig.GetBoneTransform(HumanBodyBones.Spine);
        if (upper == null || lower == null || hand == null || head == null || chest == null) return;
        if (Drinking)
        {
            drinkTime += deltaTime;
            // Lower the cup fully, rest for a second, then repeat without a pose jump.
            if (loopDrink) drinkTime = Mathf.Repeat(drinkTime, 4.3f);
        }
        float sip = !Drinking ? 0f : drinkTime < 1f ? Mathf.SmoothStep(0, 1, drinkTime) :
            drinkTime < 2.2f ? 1f : 1f - Mathf.SmoothStep(0, 1, (drinkTime - 2.2f) / 1.1f);
        if (!loopDrink && drinkTime >= 3.3f) drinkTime = -1f;
        float size = Mathf.Max(.1f, Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position));
        Vector3 rest = chest.position + character.forward * size * .5f + character.right * size * .25f - Vector3.up * size * .22f;
        Vector3 mouth = head.position + character.forward * size * .18f - Vector3.up * size * .05f;
        Quaternion rotation = character.rotation * Quaternion.AngleAxis(-18f * sip, Vector3.right) * cupFacing;
        cup.rotation = rotation;
        Vector3 gripOffset = cup.TransformVector(grip);
        Vector3 rimOffset = cup.TransformVector(rim);
        Vector3 position = Vector3.Lerp(rest - gripOffset, mouth - rimOffset, sip);
        Vector3 handTarget = position + gripOffset;
        Quaternion wrist = hand.rotation;
        Solve(upper, lower, hand, handTarget, character.right * .5f - character.up);
        hand.rotation = wrist;
        // Reapply after the hand moves: the cup is parented to that hand.
        cup.SetPositionAndRotation(position, rotation);
    }

    private static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 bend)
    {
        Vector3 origin = upper.position;
        float a = Vector3.Distance(origin, lower.position), b = Vector3.Distance(lower.position, end.position);
        Vector3 delta = target - origin;
        if (a < .001f || b < .001f || delta.sqrMagnitude < .000001f) return;
        float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
        Vector3 direction = delta.normalized;
        Vector3 pole = Vector3.ProjectOnPlane(bend, direction).normalized;
        if (pole.sqrMagnitude < .001f) pole = Vector3.Cross(direction, Vector3.right).normalized;
        float along = (a * a - b * b + distance * distance) / (2f * distance);
        Vector3 joint = origin + direction * along + pole * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
        upper.rotation = Quaternion.FromToRotation(lower.position - origin, joint - origin) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(end.position - lower.position, origin + direction * distance - lower.position) * lower.rotation;
    }
}
