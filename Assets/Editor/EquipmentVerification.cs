using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Player.Equipment;

// Isolated, low-resolution checks. Does not open/save scenes or change progression.
public static class EquipmentVerification
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Crew-On-Set/Equipment/Verify Models and Handling")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode before running the equipment check.");
            return;
        }
        string folder = Path.GetFullPath("Logs/EquipmentVerification");
        Directory.CreateDirectory(folder);
        var report = new StringBuilder();
        var preview = new PreviewRenderUtility();
        var previousRandom = UnityEngine.Random.state;
        try
        {
            preview.camera.fieldOfView = 60f;
            preview.camera.nearClipPlane = 0.05f;
            preview.camera.farClipPlane = 30f;
            preview.camera.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.10f, 0.12f, 0.15f);
            preview.ambientColor = new Color(0.25f, 0.25f, 0.25f);
            preview.lights[0].intensity = 0.8f;
            preview.lights[0].transform.rotation = Quaternion.Euler(30, 150, 0);
            preview.lights[1].intensity = 0.4f;

            GameObject player = new GameObject("Equipment test player");
            preview.AddSingleGO(player);
            Camera playerCamera = new GameObject("Test viewpoint", typeof(Camera)).GetComponent<Camera>();
            playerCamera.transform.SetParent(player.transform, false);
            playerCamera.transform.position = preview.camera.transform.position;
            Transform hold = new GameObject("Rotated scaled hold point").transform;
            hold.SetParent(player.transform, false);
            hold.localPosition = new Vector3(0, 0.936f, 0.636f);
            hold.localRotation = Quaternion.Euler(0, 90, 0);
            hold.localScale = Vector3.one * 1.2f;

            FilmLightItem light = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Studio/LowLights.prefab")).GetComponent<FilmLightItem>();
            preview.AddSingleGO(light.gameObject);
            light.transform.rotation = Quaternion.identity;
            BoxCollider disabledCollider = light.gameObject.AddComponent<BoxCollider>();
            disabledCollider.enabled = false;
            Invoke(light, "Awake");
            Vector3 originalScale = light.transform.lossyScale;
            light.intensityPercent = 45;
            typeof(FilmLightItem).GetField("currentTilt", Private).SetValue(light, -5f);
            typeof(FilmLightItem).GetField("isLightOn", Private).SetValue(light, true);
            light.spotlight.enabled = true;
            light.RefreshAdvancedFeatures();
            light.OnPickedUp(hold);
            playerCamera.enabled = false;
            Transform head = light.transform.Find("Panel Tilt Pivot");
            Require(head != null, "Real panel has an articulated tilt pivot", report);
            Require(Vector3.Dot(light.spotlight.transform.forward, head.forward) > 0.999f, "Beam follows physical panel", report);
            Require(light.spotlight.transform.position.z > head.position.z, "Emitter is in front of the housing", report);
            SavePreview(preview, Path.Combine(folder, "light-held.png"));
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            preview.AddSingleGO(wall);
            wall.transform.position = new Vector3(0, 1.6f, 5f);
            wall.transform.localScale = new Vector3(10, 6, 0.1f);
            light.spotlight.enabled = false;
            float unlit = SavePreview(preview, Path.Combine(folder, "beam-off.png"));
            light.spotlight.enabled = true;
            float lit = SavePreview(preview, Path.Combine(folder, "beam-on.png"));
            Require(lit > unlit + 0.0001f, "Light illuminates the wall in front: " + unlit.ToString("F4") + " -> " + lit.ToString("F4"), report);
            wall.SetActive(false);
            Renderer housing = head.GetComponentInChildren<MeshRenderer>();
            Vector3 centre = preview.camera.WorldToViewportPoint(housing.bounds.center);
            Require(centre.z > 0.3f && centre.x > 0.58f && centre.x < 0.95f && centre.y > 0.05f && centre.y < 0.5f,
                "Held panel is visible below/right of crosshair: " + centre.ToString("F3"), report);
            Vector3 beforeDropAim = light.spotlight.transform.forward;
            light.OnDropped(playerCamera);
            light.PlaceOnSurface(new Vector3(0, 0, 3));
            Require(Vector3.Distance(originalScale, light.transform.lossyScale) < 0.001f, "World scale restored after placement", report);
            Require(Vector3.Dot(beforeDropAim, light.spotlight.transform.forward) > 0.999f, "Placement preserves beam direction", report);
            Require(!disabledCollider.enabled, "Disabled collider state preserved", report);
            Require(light.intensityPercent == 45 && light.GetCurrentTilt() == -5, "Tutorial settings survive placement", report);
            light.isFixedKelvin = false;
            light.forcesHardLight = false;
            light.colorTemperature = 5400;
            light.diffusionPercent = 75;
            light.maxLux = 6;
            light.RefreshAdvancedFeatures();
            for (int i = 0; i < 3; i++)
            {
                playerCamera.enabled = true;
                light.OnPickedUp(hold);
                playerCamera.enabled = false;
                light.OnDropped(playerCamera);
            }
            Require(Vector3.Distance(originalScale, light.transform.lossyScale) < 0.001f, "Repeated pickup/drop does not shrink equipment", report);
            Require(light.GetColorTemperature() == 5400 && light.GetDiffusionPercent() == 75, "Soft-light controls survive pickup/drop", report);
            light.PlaceOnSurface(new Vector3(0, 0, 3));
            light.AimAt(new Vector3(0, 1.2f, 0));
            SavePreview(preview, Path.Combine(folder, "light-placed.png"));
            light.gameObject.SetActive(false);

            foreach (string path in new[] { "Assets/Studio/Low Camera.prefab", "Assets/Resources/Prefabs/Level 2 Camera Placeholder.prefab" })
            {
                GameObject cameraObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                preview.AddSingleGO(cameraObject);
                var legacy = cameraObject.GetComponent<GokeCameraPlaceholderVisual>();
                if (legacy != null) Invoke(legacy, "Awake");
                var cameraItem = cameraObject.GetComponent<FilmCameraItem>();
                Invoke(cameraItem, "Awake");
                var recorder = typeof(FilmCameraItem).GetField("pixelRecorder", Private).GetValue(cameraItem) as TruePixelRecorder;
                Require(recorder != null && recorder.transform.IsChildOf(cameraObject.transform), path + ": owns its recorder", report);
                Require(cameraObject.transform.Find("Level 2 Camera Placeholder Model") == null, path + ": no replacement block", report);
                int visible = 0;
                foreach (MeshRenderer model in cameraObject.GetComponentsInChildren<MeshRenderer>(true)) if (model.enabled) visible++;
                Require(visible > 0, path + ": original mesh visible", report);
                cameraObject.SetActive(false);
            }
            report.AppendLine("PASS: Equipment checks complete. Gameplay timing/recording still needs a Play Mode test.");
            Debug.Log(report.ToString());
        }
        catch (Exception error)
        {
            report.AppendLine("FAIL: " + error);
            Debug.LogException(error);
        }
        finally
        {
            File.WriteAllText(Path.Combine(folder, "results.txt"), report.ToString());
            preview.Cleanup();
            UnityEngine.Random.state = previousRandom;
        }
    }

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method, Private).Invoke(target, null);
    }
    private static void Require(bool condition, string message, StringBuilder report)
    {
        if (!condition) throw new InvalidOperationException(message);
        report.AppendLine("PASS: " + message);
    }
    private static float SavePreview(PreviewRenderUtility preview, string path)
    {
        preview.BeginStaticPreview(new Rect(0, 0, 960, 540));
        preview.Render();
        Texture2D image = preview.EndStaticPreview();
        File.WriteAllBytes(path, image.EncodeToPNG());
        float brightness = 0;
        Color[] pixels = image.GetPixels();
        foreach (Color pixel in pixels) brightness += pixel.grayscale;
        UnityEngine.Object.DestroyImmediate(image);
        return brightness / pixels.Length;
    }
}
