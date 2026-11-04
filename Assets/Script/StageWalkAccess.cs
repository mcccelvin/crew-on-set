using UnityEngine;

// Shared by solo/training and multiplayer studio decoration. Does not move the
// authored stage, products, or lesson anchors.
public sealed class StageWalkAccess : MonoBehaviour
{
    private Mesh rampMesh;

    private void Start()
    {
        var platform = StageInterior.FindStagePlatform();
        if (platform == null) return;
        Bounds bounds = platform.bounds;
        // Both studio layouts open toward negative world Z.
        Vector3 probe = new Vector3(bounds.center.x, bounds.max.y + 1f, bounds.min.z - .5f);
        float floor = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(probe, Vector3.down, 5f,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.attachedRigidbody != null || hit.normal.y < .9f ||
                hit.collider.transform.IsChildOf(platform.transform) || hit.point.y >= bounds.max.y - .05f)
                continue;
            // The lowest supporting surface is the room floor, not a prop top.
            floor = Mathf.Min(floor, hit.point.y);
        }
        if (float.IsPositiveInfinity(floor))
        {
            Debug.LogWarning("Stage ramp: no supporting studio floor found.", platform);
            return;
        }
        float height = bounds.max.y - floor;
        if (height <= .05f || height > 2f) return;
        float length = Mathf.Max(.8f, height * 4f);
        float halfWidth = bounds.extents.x;
        if (halfWidth <= 0) return;
        var ramp = new GameObject("Walk-on stage ramp", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        ramp.transform.SetParent(transform, false);
        ramp.layer = platform.gameObject.layer;
        ramp.transform.position = new Vector3(bounds.center.x, floor, bounds.min.z - length);
        // The lip overlaps the stage slightly so there is no collision seam.
        float back = length + .08f;
        rampMesh = new Mesh { name = "Stage access wedge" };
        rampMesh.vertices = new[] {
            new Vector3(-halfWidth,0,0), new Vector3(halfWidth,0,0),
            new Vector3(-halfWidth,height,length), new Vector3(halfWidth,height,length),
            new Vector3(-halfWidth,0,back), new Vector3(halfWidth,0,back),
            new Vector3(-halfWidth,height,back), new Vector3(halfWidth,height,back)
        };
        rampMesh.triangles = new[] {
            0,2,1, 1,2,3, // Walkable top
            2,6,3, 3,6,7, // Flat lip at full height before the stage edge
            0,4,2, 2,4,6, 1,3,5, 3,7,5, // Sides
            6,4,7, 7,4,5, // Back
            0,1,4, 1,5,4 // Bottom
        };
        rampMesh.uv = new[] { new Vector2(0,0), new Vector2(1,0),
            new Vector2(0,1), new Vector2(1,1), new Vector2(0,1), new Vector2(1,1),
            new Vector2(0,1), new Vector2(1,1) };
        rampMesh.RecalculateNormals();
        rampMesh.RecalculateBounds();
        ramp.GetComponent<MeshFilter>().sharedMesh = rampMesh;
        ramp.GetComponent<MeshRenderer>().sharedMaterial = platform.sharedMaterial;
        ramp.GetComponent<MeshCollider>().sharedMesh = rampMesh;
        Physics.SyncTransforms();
    }

    private void OnDestroy()
    {
        if (rampMesh != null) Destroy(rampMesh);
    }
}
