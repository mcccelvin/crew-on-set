using System.Linq;
using UnityEngine;

// Replace only visible meshes. Existing equipment scripts, cameras, lights and colliders keep their references.
public static class EquipmentModelVisuals
{
    static MeshRenderer[] Meshes(Transform root) => root.GetComponentsInChildren<MeshRenderer>(true)
        .Where(r => r.enabled && r.GetComponentInParent<Canvas>() == null &&
            r.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null &&
            filter.sharedMesh.vertexCount > 0).ToArray();

    static Bounds BoundsOf(MeshRenderer[] meshes)
    {
        var bounds = meshes[0].bounds;
        foreach (var mesh in meshes) bounds.Encapsulate(mesh.bounds);
        return bounds;
    }

    static void Fit(Transform visual, Bounds target, bool heightOnly = true)
    {
        var meshes = Meshes(visual);
        var bounds = BoundsOf(meshes);
        float factor = target.size.y / Mathf.Max(.0001f, bounds.size.y);
        if (!heightOnly) factor = Mathf.Min(factor, target.size.x / Mathf.Max(.0001f, bounds.size.x),
            target.size.z / Mathf.Max(.0001f, bounds.size.z));
        visual.localScale *= factor;
        visual.position += target.center - BoundsOf(meshes).center;
    }

    static void SetLayer(Transform visual, int layer)
    {
        foreach (Transform child in visual.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
    }

    public static void Camera(Transform equipment)
    {
        if (equipment.Find("Mid Camera Visual") != null) return;
        var model = Resources.Load<GameObject>("EquipmentModels/MidCamera");
        var old = Meshes(equipment);
        if (model == null) return;
        Bounds target;
        if (old.Length > 0) target = BoundsOf(old);
        else
        {
            var box = equipment.GetComponent<BoxCollider>();
            if (box == null) return;
            target = new Bounds(equipment.TransformPoint(box.center), Vector3.Scale(box.size,
                new Vector3(Mathf.Abs(equipment.lossyScale.x), Mathf.Abs(equipment.lossyScale.y), Mathf.Abs(equipment.lossyScale.z))));
        }
        if (target.size.x < .0001f || target.size.y < .0001f || target.size.z < .0001f) return;
        var visual = Object.Instantiate(model, equipment).transform;
        visual.name = "Mid Camera Visual";
        foreach (Transform child in visual.GetComponentsInChildren<Transform>(true)) child.gameObject.SetActive(true);
        if (Meshes(visual).Length == 0) { Object.Destroy(visual.gameObject); return; }
        SetLayer(visual, old.Length > 0 ? old[0].gameObject.layer : equipment.gameObject.layer);
        // Turn the lens away from the player before matching the original camera height.
        visual.localRotation = Quaternion.Euler(0f, 270f, 0f) * visual.localRotation;
        // Fitting to the thinnest axis made the replacement camera miniature.
        Fit(visual, target);
        foreach (var renderer in old) renderer.enabled = false;
    }

    public static bool SoftLight(Transform equipment, Transform head, Transform stand, Light emitter)
    {
        if (head == null || stand == null || emitter == null) return false;
        var model = Resources.Load<GameObject>("EquipmentModels/MidLightParts");
        var oldHead = Meshes(head);
        var oldStand = Meshes(stand);
        if (model == null || oldHead.Length == 0 || oldStand.Length == 0) return false;
        var imported = Object.Instantiate(model, equipment).transform;
        var newHead = imported.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "SoftLightHead");
        var newStand = imported.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "SoftLightStand");
        if (newHead == null || newStand == null || Meshes(newHead).Length == 0 || Meshes(newStand).Length == 0)
        { Object.Destroy(imported.gameObject); return false; }
        SetLayer(imported, oldHead[0].gameObject.layer);
        newHead.SetParent(head, true);
        newStand.SetParent(stand, true);
        newHead.rotation = Quaternion.FromToRotation(equipment.forward, emitter.transform.forward) * newHead.rotation;
        Fit(newHead, BoundsOf(oldHead));
        Fit(newStand, BoundsOf(oldStand));
        // The imported head and stand were fitted independently; reconnect the mounting joint.
        var standBounds = BoundsOf(Meshes(newStand));
        var headBounds = BoundsOf(Meshes(newHead));
        newHead.position += Vector3.up * (standBounds.max.y - headBounds.min.y + .005f);
        foreach (var renderer in oldHead) renderer.enabled = false;
        foreach (var renderer in oldStand) renderer.enabled = false;
        // Move the beam beyond the new softbox housing so it cannot shadow its own light.
        var bounds = BoundsOf(Meshes(newHead));
        Vector3 forward = emitter.transform.forward;
        float radius = Vector3.Dot(new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z)), bounds.extents);
        emitter.transform.position = bounds.center + forward * (radius + .06f);
        Object.Destroy(imported.gameObject);
        return true;
    }
}
