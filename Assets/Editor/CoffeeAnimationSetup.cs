using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CoffeeAnimationSetup
{
    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.delayCall += Bind;
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Bind; };
    }
    static AnimationClip Import(string name)
    {
        string path = "Assets/Product/" + name + ".blend";
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return null;
        if (importer.animationType != ModelImporterAnimationType.Human || !importer.importAnimation)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.SaveAndReimport();
        }
        var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__") && c.isHumanMotion);
        if (clip == null) Debug.LogWarning(name + ": no Humanoid animation imported; check the model's Rig mapping and exported action.");
        return clip;
    }
    static void Bind()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var catalog = AssetDatabase.LoadAssetAtPath<ProductModelCatalog>("Assets/Resources/ProductModels.asset");
        if (catalog == null) return;
        if (catalog.coffeeMixAnimation == null) catalog.coffeeMixAnimation = Import("Mix");
        if (catalog.coffeePressAnimation == null) catalog.coffeePressAnimation = Import("Press");
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
    }
}
