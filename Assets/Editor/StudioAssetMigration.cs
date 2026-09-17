using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.Linq;

[InitializeOnLoad]
public static class StudioAssetMigration
{
    static StudioAssetMigration() { EditorApplication.delayCall += ProcessRequest; }
    static void ProcessRequest()
    {
        if (!File.Exists("Assets/Editor/StudioRedesign.request")) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall += ProcessRequest; return; }
        try { Apply(); File.Delete("Assets/Editor/StudioRedesign.request"); }
        catch (Exception e) { File.WriteAllText("Logs/StudioRedesign/error.txt", e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Crew-On-Set/Studio/Apply Product Studio Redesign")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        var active = SceneManager.GetActiveScene();
        if (active.path != "Assets/Scenes/SingleStudio.unity") throw new InvalidOperationException("Open SingleStudio first.");
        Directory.CreateDirectory("Logs/StudioRedesign");
        // Back up the current on-disk scenes before migrating; existing objects keep their references.
        foreach (string name in new[] { "SingleStudio", "MultiStudio" })
        {
            string source = "Assets/Scenes/" + name + ".unity";
            string backup = "Logs/StudioRedesign/" + name + ".before.unity";
            if (!File.Exists(backup)) File.Copy(source, backup);
        }
        var catalog = Resources.Load<ProductModelCatalog>("ProductModels");
        catalog.products.First(p => p.level == 1).model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Product/FlowerBase (1).fbx");
        EditorUtility.SetDirty(catalog);
        var screen = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Product/GreenScreen.fbx"));
        screen.name = "GreenScreen Backdrop";
        screen.transform.localScale = Vector3.one * .95f;
        AddMeshColliders(screen);
        var backdrop = PrefabUtility.SaveAsPrefabAsset(screen, "Assets/Studio/GreenScreenBackdrop.prefab");
        UnityEngine.Object.DestroyImmediate(screen);
        Migrate(active, backdrop);
        EditorSceneManager.SaveScene(active);
        var multi = SceneManager.GetSceneByPath("Assets/Scenes/MultiStudio.unity");
        bool alreadyOpen = multi.IsValid() && multi.isLoaded;
        if (!alreadyOpen) multi = EditorSceneManager.OpenScene("Assets/Scenes/MultiStudio.unity", OpenSceneMode.Additive);
        Migrate(multi, backdrop);
        EditorSceneManager.SaveScene(multi);
        if (!alreadyOpen) EditorSceneManager.CloseScene(multi, true);
        SceneManager.SetActiveScene(active);
        AssetDatabase.SaveAssets();
        Inspect();
        File.WriteAllText("Logs/StudioRedesign/complete.txt", "Studio, stations, backdrop and flower updated in both studio scenes.");
    }

    static void AddMeshColliders(GameObject root)
    {
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            if (filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0 && filter.GetComponent<Collider>() == null)
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
    }
    static Bounds BoundsOf(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => !(r is LineRenderer) && !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
        var bounds = renderers[0].bounds;
        foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
        return bounds;
    }
    static void Seat(GameObject item, Renderer table, Vector3 offset, float yaw = 0)
    {
        item.transform.Rotate(Vector3.up, yaw, Space.World);
        var b = BoundsOf(item); var t = table.bounds;
        item.transform.position += new Vector3(t.center.x - b.center.x, t.max.y - b.min.y + .025f, t.center.z - b.center.z) + offset;
    }
    static void Migrate(Scene scene, GameObject backdrop)
    {
        var roots = scene.GetRootGameObjects();
        var stage = roots.First(r => r.name == "Stage");
        var platform = stage.GetComponentsInChildren<Renderer>().First(r => r.name == "stage");
        var target = platform.bounds;
        var model = roots.FirstOrDefault(r => r.name == "Product Studio") ?? (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Product/STUDIOP.fbx"), scene);
        model.name = "Product Studio";
        var newStage = model.GetComponentsInChildren<Renderer>().First(r => r.name == "stage");
        model.transform.position += new Vector3(target.center.x - newStage.bounds.center.x, target.max.y - newStage.bounds.max.y, target.center.z - newStage.bounds.center.z);
        var reference = model.transform.Find("a.player ref"); if (reference != null) reference.gameObject.SetActive(false);
        AddMeshColliders(model);
        var oldStudio = roots.FirstOrDefault(r => r.name == "Studio");
        if (oldStudio != null)
        {
            foreach (var r in oldStudio.GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var c in oldStudio.GetComponentsInChildren<Collider>()) c.enabled = false;
        }
        var stageGlow = platform.GetComponent<TutorialGlowTarget>();
        if (stageGlow != null) stageGlow.extraPartsToGlow = (stageGlow.extraPartsToGlow ?? new GameObject[0]).Concat(new[] { newStage.gameObject }).Distinct().ToArray();
        platform.enabled = false;
        foreach (var c in platform.GetComponents<Collider>()) c.enabled = false;
        var equipment = roots.First(r => r.name == "Equipment");
        var tables = model.GetComponentsInChildren<Renderer>();
        var directorTable = tables.First(r => r.name == "director table");
        var editorTable = tables.First(r => r.name == "editor table.001");
        var deliveryTable = tables.First(r => r.name == "kiosk table.001");
        var audioTable = tables.First(r => r.name == "sound table.002");
        var director = equipment.GetComponentInChildren<DirectorTerminal>(true);
        director.wallPrefab = backdrop;
        Seat(director.gameObject, directorTable, new Vector3(.9f, 0, 0));
        var computer = equipment.GetComponentInChildren<ComputerStation>(true);
        computer.transform.rotation = Quaternion.Euler(0, 97, 0);
        Seat(computer.gameObject, editorTable, Vector3.zero);
        var shop = equipment.GetComponentInChildren<ShopTerminal>(true);
        var kiosk = tables.First(r => r.name == "kiosk");
        // Keep the existing interactive shop mesh and its canvas together.
        var shopGroup = shop.transform.parent.gameObject;
        var shopBounds = shop.GetComponent<Renderer>().bounds;
        shopGroup.transform.position += kiosk.bounds.center - shopBounds.center;
        kiosk.enabled = false;
        foreach (var c in kiosk.GetComponents<Collider>()) c.enabled = false;
        shop.deliveryZone.position = new Vector3(deliveryTable.bounds.center.x, deliveryTable.bounds.max.y + .15f, deliveryTable.bounds.center.z);
        shop.deliveryZone.rotation = Quaternion.identity;
        var memory = equipment.transform.Find("NoMemoryBox"); if (memory != null) Seat(memory.gameObject, deliveryTable, new Vector3(0, 0, 1));
        var megaphone = equipment.transform.Find("LowDirectorMegaPhone"); if (megaphone != null) Seat(megaphone.gameObject, directorTable, new Vector3(-1, 0, 0));
        var chair = equipment.transform.Find("LowDirectorChair");
        if (chair != null) { var b = BoundsOf(chair.gameObject); chair.position += new Vector3(directorTable.bounds.center.x - b.center.x, model.transform.position.y + .11f - b.min.y, directorTable.bounds.min.z - .6f - b.center.z); }
        var audio = equipment.transform.Find("LowAudio"); if (audio != null) Seat(audio.gameObject, audioTable, Vector3.zero);
        var help = equipment.transform.Find("HelpDesk");
        if (help != null) { var b = BoundsOf(help.gameObject); help.position += new Vector3(9 - b.center.x, model.transform.position.y + .11f - b.min.y, model.transform.position.z - 18 - b.center.z); }
        // Keep spawn and all stage lesson markers in the same world positions.
        var player = roots.FirstOrDefault(r => r.name == "Player");
        if (player != null) player.transform.position = new Vector3(0, model.transform.position.y + .25f, model.transform.position.z - 2);
        EditorSceneManager.MarkSceneDirty(scene);
    }
    [MenuItem("Crew-On-Set/Studio/Inspect Replacement Assets")]
    public static void Inspect()
    {
        var report = new StringBuilder();
        report.AppendLine("Playing: " + EditorApplication.isPlaying);
        foreach (string path in new[] { "Assets/Product/STUDIOP.fbx", "Assets/Product/GreenScreen.fbx", "Assets/Product/FlowerBase (1).fbx", "Assets/Studio/wall-both.prefab" })
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            report.AppendLine("ASSET " + path);
            if (asset == null) { report.AppendLine("MISSING"); continue; }
            var copy = UnityEngine.Object.Instantiate(asset);
            try { Describe(copy.transform, report, true); }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            report.AppendLine("SCENE " + scene.path);
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<Canvas>() == null) Describe(root.transform, report, false);
        }
        Directory.CreateDirectory("Logs/StudioRedesign");
        File.WriteAllText("Logs/StudioRedesign/inspection.txt", report.ToString());
    }
    static void Describe(Transform node, StringBuilder report, bool asset)
    {
        if (!asset && node.GetComponent<Canvas>() != null) return;
        string path = node.name;
        for (var p = node.parent; p != null; p = p.parent) path = p.name + "/" + path;
        var renderer = node.GetComponent<Renderer>();
        report.Append(path + " pos=" + node.position.ToString("F3") + " rot=" + node.eulerAngles.ToString("F1") + " scale=" + node.lossyScale.ToString("F3"));
        if (renderer != null) report.Append(" bounds=" + renderer.bounds.center.ToString("F3") + " size=" + renderer.bounds.size.ToString("F3"));
        foreach (var component in node.GetComponents<MonoBehaviour>()) if (component != null) report.Append(" [" + component.GetType().Name + "]");
        report.AppendLine();
        foreach (Transform child in node) Describe(child, report, asset);
    }
}
