using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

[InitializeOnLoad]
public static class AlmanacIllustrationPreview
{
    static object Call(AlmanacManager manager, string name, params object[] args)
    { return typeof(AlmanacManager).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, args); }

    [MenuItem("Crew-On-Set/Almanac/Render Illustration Preview")]
    public static void Render()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Almanac preview");
        SceneManager.MoveGameObjectToScene(root, scene);
        var manager = root.AddComponent<AlmanacManager>();
        var canvasObject = new GameObject("Almanac", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(root.transform, false);
        manager.almanacCanvas = canvasObject;
        var cameraObject = new GameObject("Preview camera", typeof(Camera));
        cameraObject.transform.SetParent(root.transform, false);
        var camera = cameraObject.GetComponent<Camera>();
        camera.scene = scene;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.12f, .13f, .15f);
        camera.transform.position = new Vector3(0, 0, -10);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        var target = new RenderTexture(1920, 1080, 24);
        camera.targetTexture = target;
        var oldActive = RenderTexture.active;
        try
        {
            Call(manager, "EnsureEquipmentAndTechniqueEntries");
            foreach (var entry in manager.database) entry.isUnlocked = true;
            Call(manager, "BuildIllustratedBook");
            canvasObject.SetActive(true);
            Directory.CreateDirectory("Logs/AlmanacPreview");
            foreach (var id in new[] { "director_tablet", "led_panel", "sd_card", "actor_megaphone", "creative_brief", "visual_hierarchy", "quality_control" })
            {
                var entry = manager.database.First(e => e.id == id);
                Call(manager, "ApplyIllustratedArticle", entry, entry.description.Substring(0, Mathf.Min(900, entry.description.Length)));
                Canvas.ForceUpdateCanvases();
                foreach(var text in canvasObject.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                texture.Apply();
                File.WriteAllBytes("Logs/AlmanacPreview/" + id + ".png", texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            File.WriteAllText("Logs/AlmanacPreview/result.txt", "Rendered seven Almanac layouts in an isolated preview scene.\nImages: " + Resources.LoadAll<Texture2D>("AlmanacIllustrations").Length);
        }
        finally
        {
            RenderTexture.active = oldActive;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}

