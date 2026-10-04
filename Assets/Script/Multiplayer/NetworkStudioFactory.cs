using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Room replicas are separate from career actors, equipment and saves.
public sealed class NetworkStudioFactory : MonoBehaviour
{
    public readonly Dictionary<int, NetworkStageObject> Objects = new Dictionary<int, NetworkStageObject>();
    public bool CanCreate(string kind)
    {
        if (kind == "softlight") return Resources.Load<GameObject>("EquipmentModels/MidLightParts") != null;
        if (MultiplayerRoomActions.IsEquipment(kind)) return Resources.Load<GameObject>("CrewUI/" + kind) != null;
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
            case "vehicle": return catalog.products != null && catalog.products.Any(p => p.level == 3 && p.model != null);
            case "cup": case "product": return catalog.products != null && catalog.products.Any(p => p.level == (kind == "cup" ? 4 : CampaignProgression.GetCurrentLevel()) && p.model != null && (kind != "cup" || p.model.name.ToLowerInvariant().Contains("cup")));
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
        if (MultiplayerRoomActions.IsEquipment(item.kind) && item.kind != "softlight")
        {
            var prefab = Resources.Load<GameObject>("CrewUI/" + item.kind);
            if (prefab == null) return null;
            root = Instantiate(prefab); root.SetActive(true);
            foreach (var rigidbody in root.GetComponentsInChildren<Rigidbody>()) { rigidbody.isKinematic = true; rigidbody.useGravity = false; }
            if (root.GetComponentInChildren<Collider>() == null)
            {
                var bounds = BoundsOf(root); var box = root.AddComponent<BoxCollider>();
                box.center = root.transform.InverseTransformPoint(bounds.center);
                box.size = new Vector3(bounds.size.x / Mathf.Max(.001f, Mathf.Abs(root.transform.lossyScale.x)), bounds.size.y / Mathf.Max(.001f, Mathf.Abs(root.transform.lossyScale.y)), bounds.size.z / Mathf.Max(.001f, Mathf.Abs(root.transform.lossyScale.z)));
            }
            if (item.kind == "light")
            {
                foreach (var old in root.GetComponentsInChildren<Light>()) old.enabled = false;
                var bounds = BoundsOf(root);
                var lamp = new GameObject("Crew Spotlight").AddComponent<Light>();
                lamp.transform.SetParent(root.transform, true); lamp.transform.position = bounds.center;
                lamp.type = LightType.Spot; lamp.range = 20; lamp.shadows = LightShadows.Soft;
            }
            root.name = "Crew " + item.kind + " " + item.id; root.transform.SetParent(transform, true); return root;
        }
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
            case "cup": case "product": case "vehicle":
                var catalog = Resources.Load<ProductModelCatalog>("ProductModels");
                int level = item.kind == "cup" ? 4 : item.kind == "vehicle" ? 3 : CampaignProgression.GetCurrentLevel();
                var entry = catalog?.products?.FirstOrDefault(p => p.level == level && p.model != null &&
                    (level != 4 || p.model.name.ToLowerInvariant().Contains("cup") == (item.kind == "cup")));
                if (entry != null) root = ProductModelCatalog.Create(level, "Crew " + item.kind, entry.model.name);
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
            case "light": case "softlight":
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
        string coffee = state.contractLevel == 4 ? CoffeeEvidence(state, position, rotation, fov) : null;
        if (coffee == "Incomplete") return false;
        var subject = state.objects.FirstOrDefault(o => coffee != null ? o.kind == (coffee == "Coffee Use" ? "actor" : "product") :
            state.contractLevel <= 3 || size == "Close" ? o.kind == "product" : o.kind == "actor");
        if (subject == null || !Objects.TryGetValue(subject.id, out var view)) return false;
        var bounds = BoundsOf(view.gameObject);
        if (coffee != null)
        {
            var cup = state.objects.FirstOrDefault(o => o.kind == "cup");
            if (cup != null && Objects.TryGetValue(cup.id, out var cupView)) bounds.Encapsulate(BoundsOf(cupView.gameObject));
        }
        var local = Quaternion.Inverse(rotation) * (bounds.center - position);
        if (local.z <= .1f) return false;
        float halfHeight = Mathf.Tan(fov * .5f * Mathf.Deg2Rad) * local.z;
        if (Mathf.Abs(local.y) > halfHeight * .8f || Mathf.Abs(local.x) > halfHeight * 1.3f) return false;
        float coverage = bounds.size.y / (halfHeight * 2);
        if (coffee == null && Physics.Linecast(position, bounds.center, out var hit, ~0, QueryTriggerInteraction.Ignore) &&
            hit.collider.GetComponentInParent<NetworkStageObject>() != view) return false;
        // Coffee grades its story subjects, not a compulsory Wide/Medium/Close choice.
        // Overview footage must not be scored against an actor deliberately kept out of frame.
        if (coffee != null) return coverage >= .08f && coverage <= .95f;
        return size == "Wide" ? coverage >= .12f && coverage <= .55f : size == "Medium" ? coverage > .4f && coverage < 1.2f : coverage > .2f && coverage < 1.4f;
    }
    public float LightingScore(CrewSession state)
    {
        return state.objects.Any(o => LightReachesSubject(state, o)) ? 25 : 0;
    }
    public bool HasSoftLighting(CrewSession state) => state.objects.Any(o =>
        o.kind == "softlight" && o.diffusion >= 50 && LightReachesSubject(state, o));
    private bool LightReachesSubject(CrewSession state, CrewObject item)
    {
        if (!MultiplayerContractManager.IsPoweredLight(state, item) || item.intensity < 1 || item.intensity > 6) return false;
        var subject = state.objects.FirstOrDefault(o => state.contractLevel <= 3 ? o.kind == "product" : o.kind == "actor");
        if (subject == null) return false;
        Vector3 target = Objects.TryGetValue(subject.id, out var subjectView) ? BoundsOf(subjectView.gameObject).center : subject.position + Vector3.up;
        var lamp = Objects.TryGetValue(item.id, out var lightView) ? lightView.transform.Find("Crew Spotlight") : null;
        Vector3 position = lamp != null ? lamp.position : item.position;
        Vector3 direction = lamp != null ? lamp.forward : Quaternion.Euler(item.rotation) * Vector3.forward;
        return Vector3.Distance(position, target) < 12 && Vector3.Dot(direction, (target-position).normalized) > .65f;
    }
    public bool SubjectVisible(CrewObject subject, Vector3 position, Quaternion rotation, float fov)
    {
        if (subject == null || !Objects.TryGetValue(subject.id, out var view)) return false;
        var bounds = BoundsOf(view.gameObject);
        var inverse = Quaternion.Inverse(rotation); float tangent = Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
        for (int corner = 0; corner < 8; corner++)
        {
            var world = bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
            var local = inverse * (world-position);
            if (local.z <= .05f || Mathf.Abs(local.y) > local.z*tangent*.94f || Mathf.Abs(local.x) > local.z*tangent*16/9*.94f) return false;
        }
        return !Physics.Linecast(position,bounds.center,out var hit,~0,QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<NetworkStageObject>() == view;
    }
    public string CoffeeEvidence(CrewSession state, Vector3 position, Quaternion rotation, float fov)
    {
        var cup = state.objects.Find(o => o.kind == "cup");
        if (!SubjectVisible(cup,position,rotation,fov)) return "Incomplete";
        var actor = state.objects.Find(o => o.kind == "actor");
        var bot = actor != null && Objects.TryGetValue(actor.id,out var view) ? view.Bot : null;
        bool usingCoffee = bot != null && SubjectVisible(actor,position,rotation,fov) && state.objects.Any(o=>o.kind=="interior") &&
            (bot.CanMixCoffee && bot.FurniturePoseName == "Action" || bot.FurniturePoseName == "Using Machine");
        if (usingCoffee) return "Coffee Use";
        return SubjectVisible(state.objects.Find(o=>o.kind=="product"),position,rotation,fov) ? "Product Overview" : "Incomplete";
    }
    public bool RequiredSubjectsVisible(CrewSession state, Vector3 position, Quaternion rotation, float fov)
    {
        if (state.contractLevel == 4) return CoffeeEvidence(state,position,rotation,fov) != "Incomplete";
        return state.objects.Where(o => o.kind == "product" || state.contractLevel == 5 && (o.kind == "actor" || o.kind == "vehicle"))
            .All(o => SubjectVisible(o,position,rotation,fov)) && state.objects.Any(o=>o.kind=="product");
    }
    public bool ThreePointRoles(CrewSession state, Vector3 cameraPosition)
    {
        var actor = state.objects.Find(o=>o.kind=="actor"); if (actor == null) return false;
        Vector3 target = Objects.TryGetValue(actor.id,out var actorView) ? BoundsOf(actorView.gameObject).center : actor.position + Vector3.up;
        int front = 0, back = 0;
        foreach (var item in state.objects.Where(o=>MultiplayerContractManager.IsPoweredLight(state,o)))
        {
            var source = Objects.TryGetValue(item.id,out var lightView) ? lightView.transform.Find("Crew Spotlight") : null;
            Vector3 position = source != null ? source.position : item.position;
            if (Vector3.Dot(source != null ? source.forward : Quaternion.Euler(item.rotation)*Vector3.forward,(target-position).normalized)<.45f) continue;
            if (Vector3.Dot((cameraPosition-target).normalized,(position-target).normalized)<-.15f) back++; else front++;
        }
        return front >= 2 && back >= 1;
    }
}
