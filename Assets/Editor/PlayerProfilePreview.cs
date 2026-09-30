using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PlayerProfilePreview
{
    static object Call(AlmanacManager manager, string name, params object[] args)
    { return typeof(AlmanacManager).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, args); }

    [MenuItem("Crew-On-Set/Profile/Render Preview")]
    public static void Render()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before rendering the profile preview.");
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Profile preview");
        SceneManager.MoveGameObjectToScene(root, scene);
        var manager = root.AddComponent<AlmanacManager>();
        var book = new GameObject("Almanac", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        book.transform.SetParent(root.transform, false);
        manager.almanacCanvas = book;
        var cameraObject = new GameObject("Preview camera", typeof(Camera));
        cameraObject.transform.SetParent(root.transform, false);
        var camera = cameraObject.GetComponent<Camera>();
        camera.scene = scene;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        var target = new RenderTexture(1920, 1080, 24);
        camera.targetTexture = target;
        var oldActive = RenderTexture.active;
        try
        {
            Call(manager, "EnsureEquipmentAndTechniqueEntries");
            Call(manager, "BuildIllustratedBook");
            Call(manager, "BuildProfileUI");
            book.SetActive(false);
            var profile = root.transform.Find("Player Profile").gameObject;
            profile.SetActive(true);
            var canvas = profile.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Call(manager, "RefreshAllUI");
            Directory.CreateDirectory("Logs/ProfilePreview");
            foreach (int tab in new[] { 0, 2 })
            {
                Call(manager, "OpenTab", tab);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                texture.Apply();
                File.WriteAllBytes("Logs/ProfilePreview/" + (tab == 0 ? "profile" : "achievements") + ".png", texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        finally
        {
            typeof(AlmanacManager).GetField("profileCanvas", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, null);
            RenderTexture.active = oldActive;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}

