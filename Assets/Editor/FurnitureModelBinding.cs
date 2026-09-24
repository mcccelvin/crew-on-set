using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public sealed class FurnitureModelBinding : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    static FurnitureModelBinding() { EditorApplication.delayCall += Bind; }
    public void OnPreprocessBuild(BuildReport report) { Bind(); }
    private static void Bind()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var catalog = AssetDatabase.LoadAssetAtPath<ProductModelCatalog>("Assets/Resources/ProductModels.asset");
        var chair = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Product/Chair.fbx");
        if (catalog == null || chair == null || catalog.practiceChair == chair) return;
        catalog.practiceChair = chair;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }
}
