using UnityEngine;

// Precision standing marks use the player's feet/body, never the camera position.
public sealed class PracticePointLock
{
    public const float CenterRadius = .22f;
    const RigidbodyConstraints HorizontalLock = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
    Rigidbody body;
    RigidbodyConstraints addedConstraints;

    public static bool IsAtCenter(Transform player, Transform marker)
    {
        if (player == null || marker == null) return false;
        Vector3 offset = player.position - marker.position;
        if (offset.x * offset.x + offset.z * offset.z > CenterRadius * CenterRadius) return false;
        // Do not complete the walking task while jumping/falling over the mark,
        // or standing on a different surface directly above it.
        var collider = player.GetComponent<Collider>();
        float feet = collider != null && collider.enabled ? collider.bounds.min.y : player.position.y;
        if (Mathf.Abs(feet - marker.position.y) > .6f) return false;
        var rigidbody = player.GetComponent<Rigidbody>();
        return rigidbody == null || Mathf.Abs(rigidbody.velocity.y) < .35f;
    }

    public bool TryLock(Transform player, Transform marker)
    {
        if (!IsAtCenter(player, marker)) return false;
        CenterAndLock(player, marker);
        return true;
    }

    // The developer skip can intentionally bypass the walking check.
    public void CenterAndLock(Transform player, Transform marker)
    {
        if (player == null || marker == null) return;
        Release();
        body = player.GetComponent<Rigidbody>();
        Vector3 position = body != null ? body.position : player.position;
        position.x = marker.position.x;
        position.z = marker.position.z;
        var controller = player.GetComponent<CharacterController>();
        bool controllerEnabled = controller != null && controller.enabled;
        if (controllerEnabled) controller.enabled = false;
        if (body != null)
        {
            addedConstraints = HorizontalLock & ~body.constraints;
            body.velocity = Vector3.up * body.velocity.y;
        }
        player.position = position;
        if (body != null)
        {
            body.position = position;
            // Freeze axes only after both poses are synchronized. Otherwise
            // interpolation/constraint rebuilding can keep the old off-center pose.
            Physics.SyncTransforms();
            body.constraints |= HorizontalLock;
        }
        if (controllerEnabled) controller.enabled = true;
        // Rotation, camera pitch/yaw, body height and gravity are left untouched.
    }

    public void Release()
    {
        if (body != null) body.constraints &= ~addedConstraints;
        body = null;
        addedConstraints = RigidbodyConstraints.None;
    }
}
