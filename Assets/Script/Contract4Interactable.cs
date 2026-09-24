using UnityEngine;
using System.Collections.Generic;

// Runtime wiring also covers furniture spawned by the director tablet.
public sealed class Contract4Interactable : MonoBehaviour, IInteractable
{
    public enum Action { Sit, Product, Machine }
    public Action action;
    private static readonly HashSet<Contract4Interactable> activeItems = new HashSet<Contract4Interactable>();
    private void OnEnable() { activeItems.Add(this); }
    private void OnDisable() { activeItems.Remove(this); }
    public Transform seatAnchor;
    public Vector3 SeatPosition => seatAnchor != null ? seatAnchor.position :
        new Vector3(Bounds.center.x, Bounds.max.y, Bounds.center.z);

    public static Contract4Interactable FindCommandTarget(Ray ray, float range, Transform player)
    {
        Contract4Interactable best = null;
        float closest = range;
        foreach (var item in activeItems)
        {
            if (item == null || !item.isActiveAndEnabled) continue;
            if (item.action == Action.Product &&
                (!item.TryGetComponent<CampaignProduct>(out var product) || product.Holder != null)) continue;
            var collider = item.GetComponent<Collider>();
            if (collider == null || !collider.enabled) continue;
            Bounds bounds = item.Bounds;
            bounds.Expand(.12f);
            if (!bounds.IntersectRay(ray, out float distance) || distance >= closest) continue;
            bool blocked = false;
            foreach (var hit in Physics.RaycastAll(ray, Mathf.Max(0f, distance - .1f), ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(player) && hit.collider.GetComponentInParent<Contract4Interactable>() != item)
                { blocked = true; break; }
            if (blocked) continue;
            closest = distance;
            best = item;
        }
        return best;
    }

    // Aim assistance uses visible furniture bounds, without adding solid obstacles.
    public static Contract4Interactable FindTarget(Ray ray, float range, Transform player)
    {
        if (CampaignProgression.GetCurrentLevel() < 4) return null;
        Contract4Interactable best = null;
        float bestScore = float.PositiveInfinity;
        var items = new List<Contract4Interactable>(activeItems);
        if (items.Count == 0) items.AddRange(Object.FindObjectsOfType<Contract4Interactable>(true));
        foreach (var item in items)
        {
            if (item == null || !item.isActiveAndEnabled) continue;
            if (item.action == Action.Product &&
                (!item.TryGetComponent<CampaignProduct>(out var product) || product.Holder != null)) continue;
            Bounds bounds = item.Bounds;
            Vector3 toTarget = bounds.center - ray.origin;
            float distance = toTarget.magnitude;
            if (distance > Mathf.Max(range, 4.5f)) continue;
            float alignment = Vector3.Dot(ray.direction, toTarget / Mathf.Max(.001f, distance));
            if (alignment < .72f) continue;
            float score = (1f - alignment) * 10f + distance * .01f;
            if (score < bestScore) { bestScore = score; best = item; }
        }
        return best;
    }
    public string Prompt => action == Action.Sit ? "[E] Sit on chair" :
        action == Action.Product ? "[E] Take product" : "[E] Use coffee machine";
    public Bounds Bounds
    {
        get
        {
            var parts = GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(transform.position, Vector3.zero);
            bool first = true;
            foreach (var part in parts)
                if (part.enabled) { if (first) bounds = part.bounds; else bounds.Encapsulate(part.bounds); first = false; }
            if (first && TryGetComponent<Collider>(out var collider)) bounds = collider.bounds;
            return bounds;
        }
    }

    public static void BindFurniture(GameObject root)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
        {
            string name = renderer.name.ToLowerInvariant();
            if (name.Contains("seat")) Attach(renderer.gameObject, Action.Sit);
            else if (name == "bodycoffee" || name == "coffee machine") Attach(renderer.gameObject, Action.Machine);
            else BindMaterialTargets(renderer);
        }
    }

    private static void BindMaterialTargets(Renderer renderer)
    {
        var filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;
        var mesh = filter.sharedMesh;
        var materials = renderer.sharedMaterials;
        for (int sub = 0; sub < Mathf.Min(materials.Length, mesh.subMeshCount); sub++)
        {
            if (materials[sub] == null) continue;
            string label = materials[sub].name.ToLowerInvariant();
            bool seat = label.Contains("seat");
            if (!seat && !label.Contains("bodycoffee")) continue;
            string prefix = "Contract4 target " + sub + " ";
            if (renderer.transform.Find(prefix + "0") != null) continue;
            if (!mesh.isReadable)
            {
                Debug.LogWarning("Cafe interaction mesh must have Read/Write enabled: " + mesh.name, renderer);
                continue;
            }
            // Imported furniture uses generic object names. Separate disconnected
            // surfaces of the Seat material so each stool gets its own target.
            var vertices = mesh.vertices;
            var triangles = mesh.GetTriangles(sub);
            var parents = new int[vertices.Length];
            var coincident = new Dictionary<Vector3, int>();
            for (int i = 0; i < parents.Length; i++) parents[i] = i;
            foreach (int index in triangles)
            {
                if (coincident.TryGetValue(vertices[index], out int other)) parents[Root(parents, index)] = Root(parents, other);
                else coincident.Add(vertices[index], index);
            }
            for (int i = 0; i < triangles.Length; i += 3)
            {
                parents[Root(parents, triangles[i + 1])] = Root(parents, triangles[i]);
                parents[Root(parents, triangles[i + 2])] = Root(parents, triangles[i]);
            }
            var groups = new Dictionary<int, Bounds>();
            foreach (int index in triangles)
            {
                int key = seat ? Root(parents, index) : 0;
                if (!groups.TryGetValue(key, out Bounds bounds)) bounds = new Bounds(vertices[index], Vector3.zero);
                bounds.Encapsulate(vertices[index]);
                groups[key] = bounds;
            }
            int number = 0;
            foreach (var bounds in groups.Values)
            {
                var target = new GameObject(prefix + number++);
                target.layer = renderer.gameObject.layer;
                target.transform.SetParent(renderer.transform, false);
                var box = target.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = Vector3.Max(bounds.size, Vector3.one * .001f);
                box.isTrigger = true;
                target.AddComponent<Contract4Interactable>().action = seat ? Action.Sit : Action.Machine;
            }
        }
    }

    private static int Root(int[] parents, int index)
    {
        while (parents[index] != index)
        {
            parents[index] = parents[parents[index]];
            index = parents[index];
        }
        return index;
    }

    public static void Attach(GameObject target, Action action)
    {
        if (target.GetComponent<Contract4Interactable>() != null) return;
        var item = target.AddComponent<Contract4Interactable>();
        item.action = action;
        bool hasCollider = false;
        foreach (var collider in target.GetComponentsInChildren<Collider>())
            if (collider.enabled && !collider.isTrigger) { hasCollider = true; break; }
        if (!hasCollider) target.AddComponent<BoxCollider>();
    }

    public void OnInteract(GameObject player)
    {
        if (CampaignProgression.GetCurrentLevel() < 4) return;
        var interactor = player.GetComponent<Player.Interactor.EquipmentInteractor>();
        if (interactor != null && interactor.GetHeldItem() != null)
        {
            GameFeedback.Show("Select an empty hotbar slot before using the set.");
            return;
        }
        var performer = player.GetComponent<Contract4PlayerAction>();
        if (performer == null) performer = player.AddComponent<Contract4PlayerAction>();
        performer.Begin(this);
    }
    public void OnDrop() { }
}
