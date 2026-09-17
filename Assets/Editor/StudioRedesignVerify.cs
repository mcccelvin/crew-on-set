using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class StudioRedesignVerify
{
    static StudioRedesignVerify() { EditorApplication.delayCall += Run; }
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists("Logs/StudioRedesign/verified.txt")) return;
        if (SceneManager.GetActiveScene().path != "Assets/Scenes/SingleStudio.unity") return;
        var report = new System.Text.StringBuilder();
        var scene = SceneManager.GetActiveScene();
        var director = Object.FindObjectOfType<DirectorTerminal>(true);
        var shop = Object.FindObjectOfType<ShopTerminal>(true);
        var screen = Object.Instantiate(director.wallPrefab, director.spawnPoint.position, director.spawnPoint.rotation);
        var surface = screen.GetComponentsInChildren<Renderer>().First(r => r.name.StartsWith("Screen"));
        var platform = scene.GetRootGameObjects().First(r => r.name == "Product Studio").GetComponentsInChildren<Renderer>().First(r => r.name == "stage");
        report.AppendLine("Backdrop floor=" + surface.bounds.min.y + "; stage top=" + platform.bounds.max.y);
        report.AppendLine("Backdrop paint materials=" + surface.sharedMaterials.Count(m => m != null && m.HasProperty("_Color")));
        Object.DestroyImmediate(screen);
        var flower = ProductModelCatalog.Create(1, "Flower verification");
        report.AppendLine("Flower renderers=" + flower.GetComponentsInChildren<Renderer>().Length + "; subject=" + (flower.GetComponent<RecordableSubject>() != null) + "; collider=" + (flower.GetComponent<Collider>() != null));
        Object.DestroyImmediate(flower);
        report.AppendLine("Shop canvas=" + (shop.worldSpaceCanvas != null ? shop.worldSpaceCanvas.transform.position.ToString() : "none") + "; attached=" + (shop.worldSpaceCanvas != null && shop.worldSpaceCanvas.transform.IsChildOf(shop.transform.parent)));
        report.AppendLine("Delivery=" + shop.deliveryZone.position);
        File.WriteAllText("Logs/StudioRedesign/verified.txt", report.ToString());
    }
}
