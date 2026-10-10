using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Room decoration is independent of contract products, grading and career saves.
public sealed class StudioSetDressing : MonoBehaviour
{
    public const float ComputerDeskHeight = 1.05f;
    private const string HostName = "Shared Studio Decorations";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
    }

    private static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "SingleStudio" || scene.name == "MultiStudio") Apply(scene);
    }

    public static void Apply(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        var roots = scene.GetRootGameObjects();
        foreach (var root in roots)
            if (root.GetComponent<StudioSetDressing>() != null) return;

        var host = new GameObject(HostName);
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<StudioSetDressing>();
        RaiseComputers(roots);

        var catalog = Resources.Load<ProductModelCatalog>("ProductModels");
        if (catalog == null || catalog.decorators == null) return;
        Renderer floor = null;
        var meshes = new List<MeshFilter>();
        foreach (var root in roots)
        {
            meshes.AddRange(root.GetComponentsInChildren<MeshFilter>(true));
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.name.Equals("Floor", StringComparison.OrdinalIgnoreCase) &&
                    renderer.gameObject.activeInHierarchy &&
                    (floor == null || renderer.bounds.size.x * renderer.bounds.size.z > floor.bounds.size.x * floor.bounds.size.z))
                    floor = renderer;
        }

        foreach (var entry in catalog.decorators)
        {
            if (entry == null || entry.model == null) continue;
            var existing = FindAuthoredProp(meshes, entry.model);
            if (existing != null)
            {
                // Keep the player's placement, but not a level-specific parent that may be hidden.
                existing.transform.SetParent(host.transform, true);
                EnsureSolidColliders(existing);
                meshes.RemoveAll(m => m == null || m.transform.IsChildOf(existing.transform));
                continue;
            }
            if (floor == null)
            {
                Debug.LogWarning("Studio decorations: no studio Floor found; cannot safely place " + entry.model.name);
                continue;
            }
            Spawn(entry, host.transform, floor);
        }
        Physics.SyncTransforms();
    }

    private static GameObject FindAuthoredProp(List<MeshFilter> sceneMeshes, GameObject model)
    {
        var modelMeshes = model.GetComponentsInChildren<MeshFilter>(true);
        if (modelMeshes.Length == 0) return null;
        var allowed = new HashSet<Mesh>();
        foreach (var filter in modelMeshes) if (filter.sharedMesh != null) allowed.Add(filter.sharedMesh);
        foreach (var filter in sceneMeshes)
        {
            if (filter == null || !allowed.Contains(filter.sharedMesh)) continue;
            var candidate = filter.transform;
            while (candidate.parent != null)
            {
                if (candidate.parent.name.Replace("(Clone)", "").Trim().Equals(model.name, StringComparison.OrdinalIgnoreCase))
                { candidate = candidate.parent; continue; }
                // Stop at the imported prop root, not a disabled level container above it.
                if (candidate.name.Replace("(Clone)", "").Trim().Equals(model.name, StringComparison.OrdinalIgnoreCase)) break;
                var siblings = candidate.parent.GetComponentsInChildren<MeshFilter>(true);
                if (siblings.Length > modelMeshes.Length) break;
                bool sameModel = true;
                foreach (var sibling in siblings)
                    if (sibling.sharedMesh != null && !allowed.Contains(sibling.sharedMesh)) sameModel = false;
                if (!sameModel || candidate.parent.GetComponent<RectTransform>() != null) break;
                candidate = candidate.parent;
            }
            // Do not turn a purchased/held product or interactive station into scenery.
            bool interactive = false;
            foreach (var component in candidate.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is IInteractable) interactive = true;
            if (!interactive && candidate.gameObject.activeSelf) return candidate.gameObject;
        }
        return null;
    }

    public static GameObject Spawn(ProductModelCatalog.Decorator entry, Transform parent, Renderer floor)
    {
        var root = new GameObject("Studio Decor - " + entry.model.name);
        root.transform.SetParent(parent, false);
        var visual = Instantiate(entry.model, root.transform);
        visual.SetActive(true);
        visual.transform.localRotation = Quaternion.Euler(entry.rotation) * visual.transform.localRotation;
        foreach (var camera in visual.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (var light in visual.GetComponentsInChildren<Light>(true)) light.enabled = false;
        foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true))
        { body.isKinematic = true; body.useGravity = false; }
        if (!TryBounds(visual, out var bounds) || bounds.size.y < .001f)
        { Destroy(root); return null; }
        visual.transform.localScale *= Mathf.Max(.05f, entry.height) / bounds.size.y;
        TryBounds(visual, out bounds);
        visual.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        TryBounds(visual, out bounds);
        Physics.SyncTransforms();

        var floorBounds = floor.bounds;
        // Preserve a walking margin around the walls, doors and other stations.
        float marginX = bounds.extents.x + .75f, marginZ = bounds.extents.z + .75f;
        if (floorBounds.size.x <= marginX * 2 || floorBounds.size.z <= marginZ * 2)
        { Destroy(root); return null; }
        var positions = new[] { entry.floorPosition, new Vector2(.08f, .85f), new Vector2(.08f, .65f),
            new Vector2(.08f, .45f), new Vector2(.92f, .85f), new Vector2(.92f, .65f),
            new Vector2(.92f, .45f), new Vector2(.25f, .92f), new Vector2(.75f, .92f) };
        bool placed = false;
        foreach (var position in positions)
        {
            var point = new Vector3(Mathf.Lerp(floorBounds.min.x + marginX, floorBounds.max.x - marginX, Mathf.Clamp01(position.x)),
                floorBounds.max.y + Mathf.Max(0, entry.floorClearance),
                Mathf.Lerp(floorBounds.min.z + marginZ, floorBounds.max.z - marginZ, Mathf.Clamp01(position.y)));
            if (entry.floorClearance > 0)
            {
                // Raised decorations are mounted on a wall, not suspended in the middle of the room.
                var direction = position.y < .5f ? Vector3.back : Vector3.forward;
                point.z = position.y < .5f ? floorBounds.min.z + bounds.extents.z + .08f
                    : floorBounds.max.z - bounds.extents.z - .08f;
                var origin = point + Vector3.up * bounds.extents.y - direction * 2f;
                if (Physics.Raycast(origin, direction, out var wall, 4f, ~0, QueryTriggerInteraction.Ignore) &&
                    !wall.transform.IsChildOf(root.transform) && Mathf.Abs(wall.normal.y) < .2f)
                    point.z = wall.point.z - direction.z * (bounds.extents.z + .025f);
            }
            var centre = point + bounds.center;
            var collisions = Physics.OverlapBox(centre, bounds.extents * .95f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            bool blocked = false;
            foreach (var hit in collisions)
                if (!hit.transform.IsChildOf(root.transform) && hit.gameObject != floor.gameObject) blocked = true;
            if (blocked) continue;
            root.transform.position = point;
            placed = true;
            break;
        }
        if (!placed)
        {
            Debug.LogWarning("Studio decorations: no clear wall-side placement for " + entry.model.name);
            Destroy(root);
            return null;
        }
        EnsureSolidColliders(visual);
        Physics.SyncTransforms();
        return root;
    }

    public static void EnsureSolidColliders(GameObject prop)
    {
        foreach (var filter in prop.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled) continue;
            bool covered = false;
            for (var node = filter.transform; node != null; node = node.parent)
            {
                foreach (var collider in node.GetComponents<Collider>())
                    if (collider.enabled && !collider.isTrigger) covered = true;
                if (node == prop.transform) break;
            }
            if (covered) continue;
            // Primitive collisions also work for imported FBX meshes without Read/Write.
            // Separate mesh parts do not fill the gaps between an entire group of props.
            var box = filter.gameObject.AddComponent<BoxCollider>();
            box.center = filter.sharedMesh.bounds.center;
            var size = filter.sharedMesh.bounds.size;
            box.size = new Vector3(Mathf.Max(.001f, size.x), Mathf.Max(.001f, size.y), Mathf.Max(.001f, size.z));
        }
    }

    public static void RaiseComputers(GameObject[] roots)
    {
        var tables = new List<Renderer>();
        var stations = new List<Transform>();
        foreach (var root in roots)
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.gameObject.activeInHierarchy && renderer.name.StartsWith("editor table", StringComparison.OrdinalIgnoreCase))
                    tables.Add(renderer);
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                if (node.gameObject.activeInHierarchy && node.name == "EditorPlayground") stations.Add(node);
        }
        var adjusted = new HashSet<Renderer>();
        foreach (var station in stations)
        {
            if (!TryBounds(station.gameObject, out var computerBounds)) continue;
            Renderer desk = null;
            float nearest = 3f;
            foreach (var table in tables)
            {
                if (adjusted.Contains(table)) continue;
                var difference = table.bounds.center - computerBounds.center;
                difference.y = 0;
                if (difference.magnitude < nearest) { nearest = difference.magnitude; desk = table; }
            }
            if (desk == null) continue;
            var before = desk.bounds;
            var parts = new List<Renderer>();
            foreach (var table in tables)
            {
                var distance = table.bounds.center - desk.bounds.center; distance.y = 0;
                if (table.transform.parent == desk.transform.parent && distance.magnitude < .3f)
                { parts.Add(table); before.Encapsulate(table.bounds); adjusted.Add(table); }
            }
            if (before.size.y < .05f || before.size.y >= ComputerDeskHeight - .01f) continue;
            float factor = ComputerDeskHeight / before.size.y;
            bool upright = true;
            foreach (var part in parts)
                if (VerticalAxis(part.transform) < 0 || part.transform.IsChildOf(station)) upright = false;
            if (!upright) continue;
            // Stretch every desk/outline part about the same floor contact, including Z-up exports.
            foreach (var part in parts)
            {
                var node = part.transform;
                float bottom = part.bounds.min.y;
                var scale = node.localScale; scale[VerticalAxis(node)] *= factor; node.localScale = scale;
                node.position += Vector3.up * (before.min.y + (bottom - before.min.y) * factor - part.bounds.min.y);
                EnsureSolidColliders(part.gameObject);
            }
            float lift = ComputerDeskHeight - before.size.y;
            // Monitor, keyboard, mouse, interaction collider and card release point move together.
            station.position += Vector3.up * lift;
        }
    }

    private static int VerticalAxis(Transform node)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            var direction = Vector3.zero; direction[axis] = 1;
            if (Mathf.Abs(Vector3.Dot(node.TransformDirection(direction).normalized, Vector3.up)) > .99f) return axis;
        }
        return -1;
    }

    public static bool TryBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds(); bool found = false;
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found;
    }
}
