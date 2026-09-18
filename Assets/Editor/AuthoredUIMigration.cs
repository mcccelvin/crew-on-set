using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AuthoredUIMigration
{
    const string Request = "Temp/AuthoredUI.request";
    static AuthoredUIMigration() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlaying) { EditorApplication.isPlaying=false; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Apply(); }
        catch (Exception e) { Directory.CreateDirectory("Logs/AuthoredUI"); File.WriteAllText("Logs/AuthoredUI/error.txt",e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Crew-On-Set/UI/Save UI into Scene Hierarchy")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before migrating UI.");
        Directory.CreateDirectory("Logs/AuthoredUI");
        var active=SceneManager.GetActiveScene();
        foreach (string name in new[] { "SingleStudio", "MultiStudio", "Editor", "ReviewScene", "Main Menu", "Account", "Login" })
        {
            string path="Assets/Scenes/"+name+".unity";
            if (!File.Exists(path)) continue;
            string backup="Logs/AuthoredUI/"+name+".before.unity";
            if (!File.Exists(backup)) File.Copy(path,backup);
            var scene=SceneManager.GetSceneByPath(path);
            bool open=scene.IsValid() && scene.isLoaded;
            if (!open) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            Bake(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save "+path);
            if (!open) EditorSceneManager.CloseScene(scene,true);
        }
        if(active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        string lightPath="Assets/Studio/LowLights.prefab";
        if(File.Exists(lightPath))
        {
            string backup="Logs/AuthoredUI/LowLights.before.prefab";
            if(!File.Exists(backup))File.Copy(lightPath,backup);
            var prefab=PrefabUtility.LoadPrefabContents(lightPath);
            try
            {
                foreach(var light in prefab.GetComponentsInChildren<Player.Equipment.FilmLightItem>(true))light.BakeHierarchyUI();
                PrefabUtility.SaveAsPrefabAsset(prefab,lightPath);
            }
            finally {PrefabUtility.UnloadPrefabContents(prefab);}
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/AuthoredUI/complete.txt","Authored UI v3 saved in seven scenes. Runtime data rows remain dynamic. "+DateTime.Now);
    }
    static T[] All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
    static void Bake(Scene scene)
    {
        // Remove only an unfinished generated settings layout from a previous interrupted bake.
        foreach(var layout in All<SettingsLayout>(scene))
            if(layout.save==null || layout.pages.Any(page=>page==null)) UnityEngine.Object.DestroyImmediate(layout.gameObject);
        // Do not move existing objects: their names, parent paths and sibling draw order are contracts.
        var feedback=All<GameFeedback>(scene).FirstOrDefault();
        if(feedback==null) feedback=new GameObject("Notifications Manager").AddComponent<GameFeedback>();
        feedback.BakeHierarchyUI();
        foreach(var manager in All<CareerManager>(scene)) manager.BakeHierarchyUI();
        foreach(var manager in All<HotbarUIManager>(scene)) manager.BakeHierarchyUI();
        foreach(var manager in All<TutorialHighlighter>(scene)) manager.BakeHierarchyUI();
        foreach(var manager in All<AlmanacManager>(scene)) manager.BakeHierarchyUI();
        if(scene.name=="SingleStudio" || scene.name=="MultiStudio")
        {
            var contract=All<ContractUIManager>(scene).FirstOrDefault();
            if(contract==null)
            {
                contract=new GameObject("Contract UI Manager").AddComponent<ContractUIManager>();
                contract.contractCanvas=All<Canvas>(scene).FirstOrDefault(c=>c.name=="Contract")?.gameObject;
            }
            contract.BakeHierarchyUI();
        }
        if(scene.name=="Main Menu")
            foreach(var rect in All<RectTransform>(scene))
                if(rect.name=="load" && rect.parent!=null && rect.parent.name=="Play" && rect.GetComponent<SaveLoadPanelHost>()==null)
                    rect.gameObject.AddComponent<SaveLoadPanelHost>();
        if(scene.name=="Main Menu" || scene.name=="Account" || scene.name=="Login")
        {
            var menu=All<GameSaveMenu>(scene).FirstOrDefault();
            if(menu==null)
            {
                var go=new GameObject("Saved Games",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
                var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=200;
                var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
                menu=go.AddComponent<GameSaveMenu>();
            }
            menu.BakeHierarchyUI();
        }
        foreach(var pause in All<PauseManager>(scene))
            if(pause.pauseMenuCanvas!=null) new SharedOptionsPanel(pause.pauseMenuCanvas.transform,()=>{});
        if(scene.name=="Main Menu")
            foreach(var rect in All<RectTransform>(scene))
                if(rect.name=="Options" && rect.GetComponent<UnityEngine.UI.Button>()==null) {new SharedOptionsPanel(rect,()=>{});break;}
        foreach(var shop in All<ShopTerminal>(scene)) shop.BakeHierarchyUI();
        foreach(var director in All<DirectorTerminal>(scene)) director.BakeHierarchyUI();
        foreach(var camera in All<Player.Equipment.FilmCameraItem>(scene)) camera.BakeHierarchyUI();
        foreach(var light in All<Player.Equipment.FilmLightItem>(scene)) light.BakeHierarchyUI();
        foreach(var editor in All<EditorManager>(scene))
        {
            var previous=EditorManager.Instance;
            try { EditorManager.Instance=editor; editor.BakeHierarchyUI(); if(editor.gradingManager!=null) editor.gradingManager.BakeHierarchyUI(); }
            finally { EditorManager.Instance=previous; }
        }
        foreach(var inspector in All<ClipInspector>(scene)) inspector.BakeHierarchyUI();
        foreach(var review in All<FinalGradePanelUI>(scene)) review.BakeHierarchyUI();
        foreach(var grade in All<GradeManager>(scene)) grade.BakeHierarchyUI();
        // Group only top-level screen UI. Internal lookup paths and world-space screens stay intact.
        var screenRoots=scene.GetRootGameObjects().Where(r => r.name=="GameUI" ||
            (r.GetComponent<Canvas>()!=null && r.GetComponent<Canvas>().renderMode!=RenderMode.WorldSpace) ||
            r.name=="Notifications Manager" || r.name=="Contract UI Manager").ToArray();
        if(screenRoots.Length>0)
        {
            var ui=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="UI") ?? new GameObject("UI");
            foreach(var screen in screenRoots) screen.transform.SetParent(ui.transform,true);
        }
        Directory.CreateDirectory("Assets/UI/AuthoredMaterials");
        foreach(var text in All<TMPro.TMP_Text>(scene))
        {
            if(text.font==null)text.font=TMPro.TMP_Settings.defaultFontAsset;
            var material=text.fontSharedMaterial;
            if(material==null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material)))continue;
            string key=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(text.font))+"-"+material.ComputeCRC();
            string path="Assets/UI/AuthoredMaterials/"+key+".mat";
            var saved=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(saved==null){saved=new Material(material);saved.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(saved,path);}
            text.fontSharedMaterial=saved;
        }
        foreach(var component in All<MonoBehaviour>(scene)) if(component!=null) EditorUtility.SetDirty(component);
    }
}
