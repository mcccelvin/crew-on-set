using System;
using UnityEngine;

// References keep the original FBX files in place and include them in player builds.
public sealed class ProductModelCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Product
    {
        public int level;
        public GameObject model;
        public float size = 1f;
        public Vector3 rotation;
    }

    public Product[] products;

    public GameObject flowerTable;
    public GameObject coffeeInterior;

    // Set dressing must never count as a recorded product or actor.
    public static GameObject CreateFurniture(bool table, Vector3 availableSize)
    {
        var catalog = Resources.Load<ProductModelCatalog>("ProductModels");
        var source = catalog == null ? null : table ? catalog.flowerTable : catalog.coffeeInterior;
        if (source == null) return null;
        var root = new GameObject(table ? "Flower Table" : "Coffee Interior");
        if (table) root.AddComponent<ImportedProductVisual>().Initialize(false);
        var visual = Instantiate(source, root.transform);
        // Present the two curved legs at the front (-Z), with the third leg behind.
        if (table) visual.transform.localRotation = Quaternion.Euler(0, 180, 0) * visual.transform.localRotation;
        foreach (var camera in visual.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (var light in visual.GetComponentsInChildren<Light>(true)) light.enabled = false;
        foreach (var animator in visual.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { Destroy(root); return null; }
        var bounds = GetBounds(renderers);
        float scale = table ? availableSize.y / Mathf.Max(.001f, bounds.size.y) :
            Mathf.Min(availableSize.x / Mathf.Max(.001f, bounds.size.x), availableSize.z / Mathf.Max(.001f, bounds.size.z));
        visual.transform.localScale *= scale;
        bounds = GetBounds(renderers);
        visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        // Individual mesh colliders leave the room and the spaces between furniture accessible.
        foreach (var filter in visual.GetComponentsInChildren<MeshFilter>())
            if (filter.sharedMesh != null) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        return root;
    }

    public static GameObject Create(int level, string objectName)
    {
        var catalog = Resources.Load<ProductModelCatalog>("ProductModels");
        if (catalog == null || catalog.products == null) return null;
        foreach (var product in catalog.products)
        {
            if (product == null || product.level != level || product.model == null) continue;
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
