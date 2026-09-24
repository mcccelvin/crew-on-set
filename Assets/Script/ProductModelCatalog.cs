using System;
using UnityEngine;

// References keep the original FBX files in place and include them in player builds.
public sealed class ProductModelCatalog : ScriptableObject
{
    public const float StandardCharacterHeight = 1.85f;
    public const float SingleStudioEnvironmentScale = .7f;
    // The filming set has its own size; reducing the building must not miniaturize it.
    public const float SingleStudioBackdropScale = 1.85f / 3f;

    [Serializable]
    public sealed class Product
    {
        public int level;
        public string displayName;
        public GameObject model;
        public float size = 1f;
        public Vector3 rotation;
    }

    public Product[] products;

    [Serializable]
    public sealed class GreenScreen
    {
        public string name = "Green Screen";
        [Tooltip("Enable for the Coffee Interior entry used by CHOOSE SET > Coffee Interior.")]
        public bool coffeeInteriorEntry;
        public GameObject model;
        [Tooltip("World-space offset in metres. Backdrops use the grounded stage spawn anchor; coffee interiors use their centred placement.")]
        public Vector3 position;
        [Tooltip("Overall model size multiplier, applied together with Scale. Backdrops use this saved value without an additional studio scale multiplier.")]
        [Min(.01f)] public float size = 1f;
        [Tooltip("Local X/Y/Z multipliers, applied in addition to Size.")]
        public Vector3 scale = Vector3.one;
        [Tooltip("Rotation in degrees relative to the stage spawn orientation.")]
        public Vector3 rotation;
    }

    [InspectorName("Backdrops And Interiors")]
    public GreenScreen[] greenScreens;
    [Tooltip("Which Green Screens list entry to spawn. Element 0 is index 0.")]
    [Min(0)] public int activeGreenScreen;

    public static GreenScreen GetActiveGreenScreen()
    {
        var catalog = Resources.Load<ProductModelCatalog>("ProductModels");
        if (catalog == null || catalog.greenScreens == null || catalog.greenScreens.Length == 0) return null;
        var entry = catalog.greenScreens[Mathf.Clamp(catalog.activeGreenScreen, 0, catalog.greenScreens.Length - 1)];
        if (entry != null && entry.model != null && !entry.coffeeInteriorEntry) return entry;
        foreach (var backdrop in catalog.greenScreens)
            if (backdrop != null && backdrop.model != null && !backdrop.coffeeInteriorEntry) return backdrop;
        return null;
    }

    public static GreenScreen GetCoffeeInteriorEntry()
    {
        var catalog = Resources.Load<ProductModelCatalog>("ProductModels");
        if (catalog == null || catalog.greenScreens == null) return null;
        foreach (var entry in catalog.greenScreens)
            if (entry != null && entry.coffeeInteriorEntry && entry.model != null) return entry;
        return null;
    }

    public GameObject flowerTable;
    [HideInInspector] public GameObject coffeeInterior;
    [Tooltip("Per-axis local scale before automatic fitting. Axes follow the model rotation. The final interior is constrained to the stage and height limit. Reapply the set to see changes.")]
    [HideInInspector] public Vector3 coffeeInteriorScale = Vector3.one;
    [Tooltip("Keep the revised FBX size; only shrink it when it exceeds the stage or height limit.")]
    public bool preserveCoffeeInteriorScale = true;
    // Retained for serialized compatibility; wall geometry is now authored in Blender.
    [HideInInspector] public float coffeeBackWallHeightMultiplier = 1f;
    public GameObject practiceChair;
    [Min(.1f)] public float practiceChairHeight = .95f;
    public Vector3 practiceChairRotation = new Vector3(0f, 180f, 0f);
    public Vector3 practiceChairSeatOffset = new Vector3(0f, .46f, 0f);
    [Min(.1f)] public float coffeeInteriorMaxHeight = StandardCharacterHeight * 1.7f;
    public GameObject coffeeActors;
    public GameObject[] coffeeActorModels;
    public float coffeeActorScale = .8f;
    // Turn the counter's customer side toward the stage front (-Z).
    [HideInInspector] public Vector3 coffeeInteriorRotation = new Vector3(0f, -90f, 0f);

    // Set dressing must never count as a recorded product or actor.
    public static GameObject CreateFurniture(bool table, Vector3 availableSize)
    {
        var catalog = Resources.Load<ProductModelCatalog>("ProductModels");
        var entry = table ? null : GetCoffeeInteriorEntry();
        var source = catalog == null ? null : table ? catalog.flowerTable : entry != null ? entry.model : catalog.coffeeInterior;
        if (source == null) return null;
        var root = new GameObject(table ? "Flower Table" : "Coffee Interior");
        if (table) root.AddComponent<ImportedProductVisual>().Initialize(false);
        var visual = Instantiate(source, root.transform);
        // Present the two curved legs at the front (-Z), with the third leg behind.
        if (table) visual.transform.localRotation = Quaternion.Euler(0, 180, 0) * visual.transform.localRotation;
        else visual.transform.localRotation = Quaternion.Euler(entry != null ? entry.rotation : catalog.coffeeInteriorRotation) * visual.transform.localRotation;
        foreach (var camera in visual.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (var light in visual.GetComponentsInChildren<Light>(true)) light.enabled = false;
        foreach (var animator in visual.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { Destroy(root); return null; }
        // Fit the final authored proportions, including catalog multipliers.
        // Applying these after fitting can push the interior beyond the stage.
        if (!table)
        {
            var axes = entry != null ? entry.scale : catalog.coffeeInteriorScale;
            visual.transform.localScale = Vector3.Scale(visual.transform.localScale,
                new Vector3(Mathf.Max(.01f, axes.x), Mathf.Max(.01f, axes.y), Mathf.Max(.01f, axes.z)))
                * (entry != null ? Mathf.Max(.01f, entry.size) : 1f);
        }
        var bounds = GetBounds(renderers);
        float scale = table ? availableSize.y / Mathf.Max(.001f, bounds.size.y) :
            Mathf.Min(availableSize.x / Mathf.Max(.001f, bounds.size.x),
                availableSize.z / Mathf.Max(.001f, bounds.size.z));
        if (!table)
            scale = Mathf.Min(scale, Mathf.Max(.1f, catalog.coffeeInteriorMaxHeight)
                / Mathf.Max(.001f, bounds.size.y));
        if (!table && catalog.preserveCoffeeInteriorScale) scale = Mathf.Min(1f, scale);
        visual.transform.localScale *= scale;
        bounds = GetBounds(renderers);
        visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        // Preserve the authored mesh: walls share geometry with counters and shelf trim.
        // Individual mesh colliders leave the room and the spaces between furniture accessible.
        foreach (var filter in visual.GetComponentsInChildren<MeshFilter>())
            if (filter.sharedMesh != null) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        return root;
    }

    public static GameObject CreatePracticeChair()
    {
        var catalog = Resources.Load<ProductModelCatalog>("ProductModels");
        if (catalog == null) return null;
#if UNITY_EDITOR
        // Resolve the actual FBX main object instead of relying on a guessed local file ID.
        if (catalog.practiceChair == null)
            catalog.practiceChair = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Product/Chair.fbx");
#endif
        if (catalog.practiceChair == null) { Debug.LogError("ProductModels: assign the Chair FBX to Practice Chair."); return null; }
        var root = new GameObject("Practice Chair");
        var visual = Instantiate(catalog.practiceChair, root.transform);
        visual.SetActive(true);
        foreach (var part in visual.GetComponentsInChildren<Transform>(true)) part.gameObject.SetActive(true);
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
        visual.transform.localRotation = Quaternion.Euler(catalog.practiceChairRotation) * visual.transform.localRotation;
        foreach (var camera in visual.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (var light in visual.GetComponentsInChildren<Light>(true)) light.enabled = false;
        foreach (var animator in visual.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { Destroy(root); return null; }
        var bounds = GetBounds(renderers);
        visual.transform.localScale *= catalog.practiceChairHeight / Mathf.Max(.001f, bounds.size.y);
        bounds = GetBounds(renderers);
        visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        bounds = GetBounds(renderers);
        var box = root.AddComponent<BoxCollider>();
        box.center = bounds.center;
        box.size = bounds.size;
        var seat = new GameObject("Seat Anchor").transform;
        seat.SetParent(root.transform, false);
        seat.localPosition = catalog.practiceChairSeatOffset;
        seat.localRotation = Quaternion.Euler(catalog.practiceChairRotation);
        var interaction = root.AddComponent<Contract4Interactable>();
        interaction.action = Contract4Interactable.Action.Sit;
        interaction.seatAnchor = seat;
        return root;
    }

    public static GameObject Create(int level, string objectName, string modelName = null)
    {
        var catalog = Resources.Load<ProductModelCatalog>("ProductModels");
        if (catalog == null || catalog.products == null) return null;
        foreach (var product in catalog.products)
        {
            if (product == null || product.level != level || product.model == null) continue;
            if (!string.IsNullOrEmpty(modelName) && product.model.name != modelName) continue;
            // An FBX containing only transforms is not a usable product.
            if (product.model.GetComponentsInChildren<Renderer>(true).Length == 0) return null;
            return Build(product, objectName);
        }
        return null;
    }

    private static GameObject Build(Product product, string objectName)
    {
        var root = new GameObject(objectName);
        var visual = Instantiate(product.model, root.transform);
        visual.name = product.model.name;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(product.rotation) * visual.transform.localRotation;
        foreach (var camera in visual.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (var light in visual.GetComponentsInChildren<Light>(true)) light.enabled = false;
        foreach (var animator in visual.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;

        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Destroy(root);
            return null;
        }
        Bounds bounds = GetBounds(renderers);
        float extent = product.level == 3 ? Mathf.Max(bounds.size.x, bounds.size.z) : bounds.size.y;
        if (extent <= 0.0001f)
        {
            Destroy(root);
            return null;
        }
        float scale = product.size / extent;
        if (product.level == 3)
        {
            // Match a low sports-car roof to the standing player, not the enormous set.
            float standingHeight = 1.94f;
            var player = FindObjectOfType<Player.PlayerController.PlayerController>();
            var capsule = player != null ? player.GetComponent<CapsuleCollider>() : null;
            if (capsule != null && capsule.direction == 1)
                standingHeight = capsule.height * Mathf.Abs(capsule.transform.lossyScale.y);
            scale = standingHeight * 0.7f / bounds.size.y;
        }
        visual.transform.localScale *= scale;
        bounds = GetBounds(renderers);
        // Ground the mesh, not its exported pivot (the perfume pivot is inside the bottle).
        visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        bounds = GetBounds(renderers);
        var selectionCollider = root.AddComponent<BoxCollider>();
        selectionCollider.center = bounds.center;
        selectionCollider.size = bounds.size;

        if (product.level == 3) root.AddComponent<CubeVehicle>();
        else root.AddComponent<RecordableSubject>();
        if (product.level >= 4) root.AddComponent<CampaignProduct>().campaignLevel = product.level;
        root.AddComponent<ImportedProductVisual>().Initialize(product.level == 3);
        return root;
    }

    private static Bounds GetBounds(Renderer[] renderers)
    {
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}
