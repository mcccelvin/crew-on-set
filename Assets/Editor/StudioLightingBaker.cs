using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class StudioLightingBaker
{
    private const string ScenePath = "Assets/Scenes/SingleStudio.unity";
    private const string SettingsPath = "Assets/Scenes/Studio/StudioLightingSettings.lighting";
    private const string RequestPath = "Assets/Editor/StudioLightingBake.request";
    private const string RigName = "Studio Lighting Rig";
    private const string RoomLightRootName = "Room Lights - Realtime (No Bake)";
    private static readonly string[] HouseLightNames = { "A1", "A2", "B1", "B2", "C1", "C2" };

    static StudioLightingBaker()
    {
        EditorApplication.delayCall += ProcessAutomaticRequest;
    }

    [MenuItem("Crew-On-Set/Studio/Restore Original Lights with Warm Surfaces")]
    public static void ConfigureAndBakeFromMenu() => ConfigureAndBake();

    [MenuItem("Crew-On-Set/Studio/Remove Bake and Restore Original Lights")]
    public static void ConfigureOnlyFromMenu() => ConfigureStudioLighting(false);

    // Kept under the original method name so existing automation still works.
    // This setup deliberately does not bake the imported room mesh because its
    // overlapping lightmap UVs produced the large black roof and wall polygons.
    public static void BatchConfigureAndBake()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!ConfigureStudioLighting(false))
            throw new InvalidOperationException("SingleStudio lighting could not be configured.");
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        WriteStatus("COMPLETE: Original six-light setup restored with warm realtime surfaces and no bake.");
    }

    private static void ProcessAutomaticRequest()
    {
        if (!File.Exists(AbsoluteProjectPath(RequestPath))) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ProcessAutomaticRequest;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            WriteStatus("Waiting for Play Mode to stop before configuring studio lighting.");
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += ProcessAutomaticRequest;
            return;
        }
        if (!string.Equals(SceneManager.GetActiveScene().path, ScenePath, StringComparison.OrdinalIgnoreCase))
        {
            WriteStatus("Open SingleStudio, then use Crew-On-Set > Studio > Restore Original Lights with Warm Surfaces.");
            Debug.LogWarning("STUDIO LIGHTING: Open SingleStudio before applying its house lighting.");
            return;
        }
        AssetDatabase.DeleteAsset(RequestPath);
        ConfigureAndBakeBlocking();
    }

    private static void ConfigureAndBakeBlocking()
    {
        if (!ConfigureStudioLighting(false)) return;
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        WriteStatus("COMPLETE: Original six-light layout restored with warmer realtime surfaces and no lightmap bake.");
        Debug.Log("STUDIO LIGHTING: Original room lights restored; warm surfaces use realtime ambience only.");
    }

    private static void ConfigureAndBake()
    {
        if (!ConfigureStudioLighting(false)) return;
        WriteStatus("COMPLETE: Original six-light layout restored with warmer realtime surfaces and no lightmap bake.");
        Debug.Log("STUDIO LIGHTING: Original room lighting restored without baking.");
    }

    private static bool ConfigureStudioLighting(bool prepareForBake)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase))
        {
            WriteStatus("Open SingleStudio before configuring its lighting.");
            Debug.LogWarning("STUDIO LIGHTING: SingleStudio must be active.");
            return false;
        }

        Undo.SetCurrentGroupName("Configure Recording Studio Lighting");
        ConfigureEnvironment();
        int houseLightCount = ConfigureHouseLights();
        if (houseLightCount != HouseLightNames.Length)
        {
            WriteStatus($"FAILED: Restored {houseLightCount} of {HouseLightNames.Length} room lights.");
            Debug.LogError($"STUDIO LIGHTING: Expected {HouseLightNames.Length} room lights but restored {houseLightCount}.");
            return false;
        }
        Transform studioRoot = FindSceneObject("Studio")?.transform;

        // Remove every generated fill, stage wash, reflection probe and probe grid.
        // The pre-redesign six-light layout is restored below.
        RemoveGeneratedRig();
        if (studioRoot != null) ConfigureStudioGeometryForRealtime(studioRoot);

        Lightmapping.lightingSettings = GetOrCreateLightingSettings();
        if (Lightmapping.isRunning) Lightmapping.Cancel();
        Lightmapping.Clear();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        WriteStatus($"Restored {houseLightCount} original room lights with warm realtime surfaces and no bake.");
        Debug.Log($"STUDIO LIGHTING: Restored {houseLightCount} original room lights without baking.");
        return true;
    }

    private static void ConfigureEnvironment()
    {
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        // Warm the existing wall, wood and ceiling materials through realtime
        // ambient response. No lightmap or generated fill light is involved.
        RenderSettings.ambientSkyColor = new Color(0.38f, 0.35f, 0.31f);
        RenderSettings.ambientEquatorColor = new Color(0.30f, 0.27f, 0.23f);
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.19f);
        RenderSettings.ambientIntensity = 0.90f;
        RenderSettings.subtractiveShadowColor = new Color(0.22f, 0.20f, 0.18f);
        RenderSettings.reflectionIntensity = 0.60f;
        RenderSettings.reflectionBounces = 1;
        RenderSettings.defaultReflectionResolution = 128;
    }

    private static int ConfigureHouseLights()
    {
        GameObject root = FindSceneObject(RoomLightRootName);
        if (root == null)
        {
            root = new GameObject(RoomLightRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Realtime Room Lights");
        }
        Undo.RecordObject(root.transform, "Reset Room Light Root");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        int restoredCount = 0;
        foreach (string lightName in HouseLightNames)
        {
            GameObject lightObject = FindSceneObject(lightName);
            if (lightObject == null)
            {
                lightObject = new GameObject(lightName);
                Undo.RegisterCreatedObjectUndo(lightObject, $"Restore {lightName}");
            }
            if (lightObject.transform.parent != root.transform)
                Undo.SetTransformParent(lightObject.transform, root.transform, "Organize Realtime Room Light");

            Light target = lightObject.GetComponent<Light>();
            if (target == null) target = Undo.AddComponent<Light>(lightObject);
            Undo.RecordObject(target, "Restore Original Studio Light");
            target.enabled = true;
            target.type = LightType.Point;
            target.lightmapBakeType = LightmapBakeType.Realtime;
            target.color = Color.white;
            target.useColorTemperature = false;
            target.colorTemperature = 6570f;
            target.intensity = 1f;
            target.range = 30f;
            target.spotAngle = 30f;
            target.innerSpotAngle = 21.80208f;
            target.bounceIntensity = 1f;
            target.shadows = LightShadows.None;
            target.renderMode = LightRenderMode.Auto;

            // These are the exact world positions represented by the original
            // Studio/STUDIO hierarchy before the lighting-redesign prompt.
            Undo.RecordObject(target.transform, "Restore Original Studio Light Transform");
            float x = lightName.EndsWith("1", StringComparison.Ordinal) ? -5f : 5f;
            float z = lightName[0] == 'A' ? 7.42f : lightName[0] == 'B' ? 17.42f : 27.42f;
            target.transform.localPosition = new Vector3(x, 9.48f, z);
            target.transform.localRotation = Quaternion.identity;
            target.transform.localScale = Vector3.one;
            EditorUtility.SetDirty(target);
            EditorUtility.SetDirty(target.transform);
            restoredCount++;
        }
        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(root.transform);
        return restoredCount;
    }

    private static void ConfigureStudioGeometryForRealtime(Transform studioRoot)
    {
        foreach (Renderer renderer in studioRoot.GetComponentsInChildren<Renderer>(true))
        {
            MeshRenderer meshRenderer = renderer as MeshRenderer;
            if (meshRenderer == null) continue;
            GameObject go = meshRenderer.gameObject;
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
            flags &= ~StaticEditorFlags.ContributeGI;
            flags &= ~StaticEditorFlags.ReflectionProbeStatic;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
            meshRenderer.receiveGI = ReceiveGI.LightProbes;
            EditorUtility.SetDirty(meshRenderer);
        }
    }

    private static LightingSettings GetOrCreateLightingSettings()
    {
        LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
        if (settings == null)
        {
            settings = new LightingSettings();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        // The imported studio shell does not have reliable non-overlapping
        // lightmap UVs. Realtime house lighting avoids permanent black patches.
        settings.bakedGI = false;
        settings.realtimeGI = false;
        settings.ao = false;
        EditorUtility.SetDirty(settings);
        return settings;
    }

    private static GameObject FindSceneObject(string exactName)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == exactName) return root;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == exactName) return child.gameObject;
        }
        return null;
    }

    private static void RemoveGeneratedRig()
    {
        GameObject existing = FindSceneObject(RigName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);
    }

    private static string AbsoluteProjectPath(string assetPath) => Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace('/', Path.DirectorySeparatorChar));

    private static void WriteStatus(string message)
    {
        string directory = Path.Combine(Directory.GetCurrentDirectory(), "Library", "CrewOnSet");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "StudioLightingBake.status"), DateTime.Now.ToString("O") + Environment.NewLine + message);
    }
}
