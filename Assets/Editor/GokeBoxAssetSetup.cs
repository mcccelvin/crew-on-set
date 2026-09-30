using UnityEditor;
using UnityEngine;

public static class GokeBoxAssetSetup
{
    [InitializeOnLoadMethod]
    static void Queue()
    {
        EditorApplication.delayCall += Bind;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Bind;
        };
    }
    static void Bind()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var catalog = AssetDatabase.LoadAssetAtPath<ProductModelCatalog>("Assets/Resources/ProductModels.asset");
        if (catalog == null || catalog.gokeBox != null) return;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Product/box.fbx");
        if (model == null) return;
        const string path = "Assets/Studio/Goke Box Brown.mat";
        var brown = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (brown == null)
        {
            brown = new Material(Shader.Find("Standard"));
            brown.color = new Color(.48f,.28f,.12f);
            brown.SetFloat("_Glossiness",.12f);
            AssetDatabase.CreateAsset(brown,path);
        }
        catalog.gokeBox = model; catalog.gokeBoxMaterial = brown;
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
    }
}
