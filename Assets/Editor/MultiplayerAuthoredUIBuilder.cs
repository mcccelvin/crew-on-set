using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

// Builds independent view copies in edit mode. Never loads the career scene at runtime.
[InitializeOnLoad]
public static class MultiplayerAuthoredUIBuilder
{
    private const string Folder = "Assets/Resources/CrewUI";
    private const int CopyVersion = 3;
    static MultiplayerAuthoredUIBuilder()
    {
        EditorApplication.delayCall += EnsureCopies;
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingEditMode) EnsureCopies(); };
    }
    [MenuItem("Crew-On-Set/Refresh Multiplayer UI Copies")]
    public static void Rebuild() { if (!EditorApplication.isPlaying && !IsClone()) { Build("SingleStudio", "Studio"); Build("Editor", "Editor"); AssetDatabase.SaveAssets(); } }
    private static bool IsClone()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("ParrelSync.ClonesManager")).FirstOrDefault(t=>t!=null);
        var method = type?.GetMethod("IsClone",BindingFlags.Public|BindingFlags.Static);
        return method != null && method.GetParameters().Length == 0 && method.Invoke(null,null) is bool clone && clone;
    }
    private static void EnsureCopies()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || IsClone()) return;
        if (Outdated("SingleStudio", "Studio") || Outdated("Editor", "Editor")) Rebuild();
    }
    private static string Signature(string sceneName) => AssetDatabase.GetAssetDependencyHash("Assets/Scenes/" + sceneName + ".unity").ToString()
        + AssetDatabase.GetAssetDependencyHash("Assets/Resources/ExportUIArt.asset").ToString();
    private static bool Outdated(string sceneName, string assetName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + assetName + ".prefab");
        var refs = prefab != null ? prefab.GetComponent<MultiplayerUIReferences>() : null;
        return refs == null || refs.copyVersion != CopyVersion || refs.sourceSignature != Signature(sceneName);
    }
    private static void Build(string sceneName, string assetName)
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "CrewUI");
        string sourcePath = "Assets/Scenes/" + sceneName + ".unity";
        Scene sourceScene = SceneManager.GetSceneByPath(sourcePath);
        bool openedSource = !sourceScene.IsValid() || !sourceScene.isLoaded;
        if (openedSource) sourceScene = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Additive);
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var originals = sourceScene.GetRootGameObjects();
            var scripts = originals.SelectMany(o => o.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c != null).ToArray();
            var root = new GameObject("Crew " + assetName + " Authored UI");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.SetActive(false);
            var map = new Dictionary<Object, Object>();
            IEnumerable<GameObject> views = sceneName == "SingleStudio"
                ? originals.Where(o => o.name == "UI")
                : originals.SelectMany(o => o.GetComponentsInChildren<Canvas>(true))
                    .Where(c => c.transform.parent == null || c.transform.parent.GetComponentInParent<Canvas>() == null).Select(c => c.gameObject);
            foreach (var view in views)
            {
                var copy = Object.Instantiate(view, root.transform, false);
                copy.name = view.name; Map(view.transform, copy.transform, map);
            }
            var refs = root.AddComponent<MultiplayerUIReferences>();
            refs.copyVersion = CopyVersion;
            refs.sourceSignature = Signature(sceneName);
            var player = scripts.OfType<Player.PlayerController.PlayerController>().FirstOrDefault();
            if (player != null)
            {
                var data = new SerializedObject(player);
                refs.playerModel = data.FindProperty("CharacterModel")?.objectReferenceValue as GameObject;
                refs.playerHeight = data.FindProperty("CharacterHeight")?.floatValue ?? 1.85f;
                refs.playerAnimatorController = player.GetComponent<Animator>()?.runtimeAnimatorController;
            }
            if(sceneName=="SingleStudio")
            {
                var audioSource=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Equipments and Products/Equipments and Products/All low/LowAudio/LowAudio.fbx");
                if(audioSource!=null)
                {
                    var audio=Object.Instantiate(audioSource,root.transform,false);
                    audio.SetActive(false); PrefabUtility.SaveAsPrefabAsset(audio,Folder+"/audio.prefab");Object.DestroyImmediate(audio);
                }
            }
            foreach (var script in scripts)
            {
                string type = script.GetType().Name;
                if (!new[] { "DirectorTerminal", "ShopTerminal", "ComputerStation", "ComputerUIManager", "TutorialUIManager", "AlmanacManager", "HotbarUIManager", "ContractUIManager", "EditorManager", "ColorGradingManager", "TruePixelPlayer" }.Contains(type)) continue;
                foreach (var field in script.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!typeof(Object).IsAssignableFrom(field.FieldType)) continue;
                    var value = field.GetValue(script) as Object;
                    if (value == null) continue;
                    if (map.TryGetValue(value, out var mapped)) value = mapped;
                    else if (!AssetDatabase.Contains(value)) continue;
                    refs.entries.Add(new MultiplayerUIReferences.Entry { key = type + "." + field.Name, value = value });
                }
                if (script is ShopTerminal shop)
                {
                    refs.shop = shop.availableItems.Where(i => i != null).Select(i => new ShopItem { itemName = i.itemName, price = i.price, prefabToSpawn = i.prefabToSpawn }).ToList();
                    refs.deliveryPosition = shop.deliveryZone != null ? shop.deliveryZone.position : script.transform.position;
                    refs.shopPosition = shop.transform.position;
                    for (int i = 0; i < shop.availableItems.Count; i++)
                    {
                        var item = shop.availableItems[i];
                        if (item == null || item.prefabToSpawn == null || i > 3) continue;
                        string key = new[] { "camera", "light", "sd", "megaphone" }[i];
                        var equipment = Object.Instantiate(item.prefabToSpawn, root.transform, false);
                        equipment.SetActive(false);
                        foreach (var behaviour in equipment.GetComponentsInChildren<MonoBehaviour>(true)) if (behaviour != null) Object.DestroyImmediate(behaviour);
                        foreach (var camera in equipment.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(camera);
                        foreach (var listener in equipment.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(listener);
                        PrefabUtility.SaveAsPrefabAsset(equipment, Folder + "/" + key + ".prefab");
                        Object.DestroyImmediate(equipment);
                    }
                }
                if (script is DirectorTerminal director)
                {
                    refs.tabletPosition = script.transform.position;
                    if (director.topDownCamera != null)
                    {
                        var camera = director.topDownCamera;
                        refs.hasTabletCamera = true;
                        refs.tabletCameraPosition = camera.transform.position;
                        refs.tabletCameraRotation = camera.transform.rotation;
                        refs.tabletCameraFieldOfView = camera.fieldOfView;
                        refs.tabletCameraOrthographic = camera.orthographic;
                        refs.tabletCameraSize = camera.orthographicSize;
                    }
                }
                if (script is ComputerStation) refs.computerPosition = script.transform.position;
                if (script is AlmanacManager book)
                {
                    // Default entries are normally populated by Start. Copy them without running career UI or saves.
                    var scratch = new GameObject("Crew knowledge copy");
                    scratch.SetActive(false); scratch.transform.SetParent(root.transform, false);
                    try
                    {
                        var data = scratch.AddComponent<AlmanacManager>();
                        data.database = book.database.Select(k => JsonUtility.FromJson<KnowledgeEntry>(JsonUtility.ToJson(k))).ToList();
                        foreach (string method in new[] { "EnsureDefaultKnowledgeEntries", "EnsureEquipmentAndTechniqueEntries", "RemoveLegacyKnowledgeEntries" })
                            typeof(AlmanacManager).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(data, null);
                        refs.knowledge = data.database;
                    }
                    finally { Object.DestroyImmediate(scratch); }
                }
            }
            refs.CaptureArtwork();
            // Keep graphics/layout controls; replace all career callbacks with room adapters.
            foreach (var button in root.GetComponentsInChildren<Button>(true)) button.onClick = new Button.ButtonClickedEvent();
            foreach (var slider in root.GetComponentsInChildren<Slider>(true)) slider.onValueChanged = new Slider.SliderEvent();
            foreach (var input in root.GetComponentsInChildren<TMP_InputField>(true)) { input.onValueChanged = new TMP_InputField.OnChangeEvent(); input.onEndEdit = new TMP_InputField.SubmitEvent(); }
            foreach (var trigger in root.GetComponentsInChildren<UnityEngine.EventSystems.EventTrigger>(true)) trigger.triggers.Clear();
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (script == null || script == refs) continue;
                string ns = script.GetType().Namespace ?? "";
                if (!ns.StartsWith("UnityEngine.") && !ns.StartsWith("TMPro")) Object.DestroyImmediate(script);
            }
            foreach (var camera in root.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(camera);
            foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(listener);
            // Bindings to stripped controllers are intentionally discarded.
            refs.entries.RemoveAll(e => e.value == null);
            foreach(var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + assetName + ".prefab");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); if (openedSource) EditorSceneManager.CloseScene(sourceScene, true); }
    }
    private static void Map(Transform source, Transform target, Dictionary<Object, Object> map)
    {
        map[source.gameObject] = target.gameObject;
        var from = source.GetComponents<Component>(); var to = target.GetComponents<Component>();
        for (int i = 0; i < from.Length && i < to.Length; i++) if (from[i] != null && to[i] != null) map[from[i]] = to[i];
        for (int i = 0; i < source.childCount; i++) Map(source.GetChild(i), target.GetChild(i), map);
    }
}
