using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Room replicas are separate from career actors, equipment and saves.
public sealed class NetworkStudioFactory : MonoBehaviour
{
    public readonly Dictionary<int, NetworkStageObject> Objects = new Dictionary<int, NetworkStageObject>();
    public bool CanCreate(string kind)
    {
        var catalog = Resources.Load<ProductModelCatalog>("ProductModels");
        if (catalog == null) return false;
        switch (kind)
        {
            case "actor": return catalog.coffeeActorModels != null && catalog.coffeeActorModels.Length > 0 && catalog.coffeeActorModels[0] != null;
            case "chair": return catalog.practiceChair != null;
            case "table": return catalog.flowerTable != null;
            case "interior": return ProductModelCatalog.GetCoffeeInteriorEntry()?.model != null || catalog.coffeeInterior != null;
            case "backdrop": return ProductModelCatalog.GetActiveGreenScreen()?.model != null;
            case "light": return Resources.Load<GameObject>("EquipmentModels/MidLightParts") != null;
            case "cup": case "product": return catalog.products != null && catalog.products.Any(p => p.level == 4 && p.model != null && p.model.name.ToLowerInvariant().Contains("cup") == (kind == "cup"));
            default: return false;
        }
    }
    public Bounds Stage { get { var r = StageInterior.FindStagePlatform(); return r != null ? r.bounds : new Bounds(Vector3.zero, new Vector3(12, .2f, 8)); } }
    public bool ValidPosition(Vector3 p) => MultiplayerContractManager.Finite(p) &&
        Mathf.Abs(p.x - Stage.center.x) < Stage.extents.x + 8 && Mathf.Abs(p.z - Stage.center.z) < Stage.extents.z + 8 &&
        p.y >= Stage.min.y - 2 && p.y <= Stage.max.y + 6;
    public Contract4Interactable Interaction(int id, int index)
    {
        if (!Objects.TryGetValue(id, out var view)) return null;
        var list = view.GetComponentsInChildren<Contract4Interactable>();
        return index >= 0 && index < list.Length ? list[index] : null;
    }
    public void Apply(CrewSession state)
    {
        // Release hands before removing furniture/products.
        foreach (var view in Objects.Values)
            if (view.Bot != null && (!state.objects.Any(o => o.id == view.Id) ||
                state.objects.Find(o => o.id == view.Id).revision != view.Revision)) view.Bot.ReleaseProduct();
        foreach (int id in Objects.Keys.Where(id => !state.objects.Any(o => o.id == id)).ToArray())
        { var root = Objects[id].gameObject; root.SetActive(false); Destroy(root); Objects.Remove(id); }
        foreach (var item in state.objects)
        {
            if (!Objects.TryGetValue(item.id, out var view))
            {
                var root = Create(item);
                if (root == null) { Debug.LogError("Missing multiplayer model: " + item.kind); continue; }
                view = root.AddComponent<NetworkStageObject>(); view.Id = item.id; Objects.Add(item.id, view);
            }
        }
        foreach (var item in state.objects.Where(o => o.kind != "actor"))
            if (Objects.TryGetValue(item.id, out var view)) view.Apply(item, this);
        foreach (var item in state.objects.Where(o => o.kind == "actor"))
            if (Objects.TryGetValue(item.id, out var view)) view.Apply(item, this);
    }
    private GameObject Create(CrewObject item)
    {
        GameObject root = null;
        switch (item.kind)
        {
            case "actor":
                root = new GameObject("Crew Actor");
                if (!ActorBot.TryCreateForCrew(root, item.tier)) { Destroy(root); return null; }
                break;
            case "chair": root = ProductModelCatalog.CreatePracticeChair(); break;
            case "table": root = ProductModelCatalog.CreateFurniture(true, new Vector3(2, .8f, 1)); break;
            case "interior":
                root = ProductModelCatalog.CreateFurniture(false, Stage.size);
                if (root != null) Contract4Interactable.BindFurniture(root);
                break;
            case "cup": case "product":
                var catalog = Resources.Load<ProductModelCatalog>("ProductModels");
                var entry = catalog?.products?.FirstOrDefault(p => p.level == 4 && p.model != null &&
                    (p.model.name.ToLowerInvariant().Contains("cup") == (item.kind == "cup")));
                if (entry != null) root = ProductModelCatalog.Create(4, "Crew " + item.kind, entry.model.name);
                break;
            case "backdrop":
                var screen = ProductModelCatalog.GetActiveGreenScreen();
                if (screen?.model == null) break;
                root = new GameObject("Crew Backdrop");
                var visual = Instantiate(screen.model, root.transform);
                visual.transform.localRotation = Quaternion.Euler(screen.rotation);
                visual.transform.localScale = Vector3.Scale(visual.transform.localScale, screen.scale) * screen.size;
                StageInterior.FitInsideStage(root, Stage, screen.position, true);
                // Keep the fitted geometry local to the room placement root.
                root.transform.position = Vector3.zero;
                var fitted = BoundsOf(root);
                visual.transform.position -= new Vector3(fitted.center.x, fitted.min.y, fitted.center.z);
                break;
            case "light":
                var source = Resources.Load<GameObject>("EquipmentModels/MidLightParts");
                if (source == null) break;
                root = new GameObject("Crew Light");
                var model = Instantiate(source, root.transform);
                var bounds = BoundsOf(model);
                model.transform.localScale *= 2f / Mathf.Max(.01f, bounds.size.y);
                bounds = BoundsOf(model);
                model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                foreach (var old in root.GetComponentsInChildren<Light>()) old.enabled = false;
                var lamp = new GameObject("Crew Spotlight").AddComponent<Light>();
                lamp.transform.SetParent(root.transform, false); lamp.transform.localPosition = Vector3.up * 1.7f;
                lamp.type = LightType.Spot; lamp.range = 20; lamp.shadows = LightShadows.Soft;
                var box = root.AddComponent<BoxCollider>(); box.center = Vector3.up; box.size = new Vector3(.65f, 2, .65f);
                break;
        }
        if (root != null) { root.name = "Crew " + item.kind + " " + item.id; root.transform.SetParent(transform, true); }
        return root;
    }
    public static Bounds BoundsOf(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(root.transform.position, Vector3.one);
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }
    public bool GoodFrame(Vector3 position, Quaternion rotation, float fov, string size, CrewSession state)
    {
        var subject = state.objects.FirstOrDefault(o => size == "Close" ? o.kind == "cup" || o.kind == "product" : o.kind == "actor");
        if (subject == null || !Objects.TryGetValue(subject.id, out var view)) return false;
        var bounds = BoundsOf(view.gameObject);
        var local = Quaternion.Inverse(rotation) * (bounds.center - position);
        if (local.z <= .1f) return false;
        float halfHeight = Mathf.Tan(fov * .5f * Mathf.Deg2Rad) * local.z;
        if (Mathf.Abs(local.y) > halfHeight * .8f || Mathf.Abs(local.x) > halfHeight * 1.3f) return false;
        float coverage = bounds.size.y / (halfHeight * 2);
        if (Physics.Linecast(position, bounds.center, out var hit, ~0, QueryTriggerInteraction.Ignore) &&
            hit.collider.GetComponentInParent<NetworkStageObject>() != view) return false;
        return size == "Wide" ? coverage >= .12f && coverage <= .55f : size == "Medium" ? coverage > .4f && coverage < 1.2f : coverage > .2f && coverage < 1.4f;
    }
    public float LightingScore(CrewSession state)
    {
        var subject = state.objects.FirstOrDefault(o => o.kind == "actor");
        if (subject == null) return 0;
        return state.objects.Any(o => o.kind == "light" && o.powered && o.intensity >= 1 && o.intensity <= 6 &&
            Vector3.Distance(o.position, subject.position) < 12 &&
            Vector3.Dot(Quaternion.Euler(o.rotation) * Vector3.forward, (subject.position + Vector3.up - o.position).normalized) > .65f) ? 25 : 0;
    }
}
