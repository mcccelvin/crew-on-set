#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public sealed class CameraHUDBuilder : IPreprocessBuildWithReport
{
    private const string Path = "Assets/Resources/CameraHUD.prefab";
    public int callbackOrder => 0;
    static CameraHUDBuilder() { EditorApplication.delayCall += Ensure; }
    private static void Ensure()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += Ensure; return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Path) == null) Build();
    }
    [MenuItem("Crew-On-Set/UI/Rebuild CAM FX3 Viewfinder")]
    public static void Build()
    {
        var focus=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/CAM_FX3/focus area icon.png");
        var exposure=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/CAM_FX3/exposure icon.png");
        var hud=CameraHUDController.Build(focus,exposure);
        PrefabUtility.SaveAsPrefabAsset(hud.gameObject,Path);
        Object.DestroyImmediate(hud.gameObject);
        AssetDatabase.SaveAssets();
    }
    public void OnPreprocessBuild(BuildReport report)
    { if(AssetDatabase.LoadAssetAtPath<GameObject>(Path)==null)Build(); }
}
#endif
