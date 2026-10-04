#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class CCoinSettingsEditor : IPreprocessBuildWithReport
{
    public int callbackOrder => 10;
    [MenuItem("Crew-On-Set/C-Coins/Copy Public Environment Configuration into Build")]
    public static void CopyEnvironment()
    {
        string function = Environment.GetEnvironmentVariable("CREW_CCOINS_FUNCTION");
        if (!CCoinRules.ValidId(function)) { Debug.LogWarning("Set CREW_CCOINS_FUNCTION to the deployed function name first. No secrets or balance are copied."); return; }
        string website = Environment.GetEnvironmentVariable("CREW_CCOINS_WEBSITE_URL");
        if (!string.IsNullOrWhiteSpace(website) && !CCoinSettings.IsApprovedWebsite(website))
        {
            Debug.LogWarning("Only the user-approved Crew On Set website can be bundled."); return;
        }
        var settings = Resources.Load<CCoinSettings>("CCoinSettings");
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<CCoinSettings>();
            AssetDatabase.CreateAsset(settings,"Assets/Resources/CCoinSettings.asset");
        }
        Undo.RecordObject(settings,"Configure C-Coins function"); settings.functionName = function;
        if (!string.IsNullOrWhiteSpace(website))
        {
            settings.websiteUrl = website;
        }
        EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        Debug.Log("C-Coins public function name saved for player builds. The existing PlayFab title and server credentials were not changed.");
    }
    public void OnPreprocessBuild(BuildReport report)
    {
        var settings = Resources.Load<CCoinSettings>("CCoinSettings");
        if (settings == null || !CCoinRules.ValidId(settings.functionName)) throw new BuildFailedException("C-Coins public connection settings are missing/invalid.");
        string function = Environment.GetEnvironmentVariable("CREW_CCOINS_FUNCTION");
        if (!string.IsNullOrWhiteSpace(function) && function != settings.functionName)
            throw new BuildFailedException("Use Crew-On-Set > C-Coins > Copy Public Environment Configuration into Build before building. Machine environment variables are not bundled automatically.");
        if (!CCoinSettings.IsApprovedWebsite(settings.websiteUrl)) throw new BuildFailedException("The C-Coins website URL is not the approved HTTPS website.");
        string website = Environment.GetEnvironmentVariable("CREW_CCOINS_WEBSITE_URL");
        if (!string.IsNullOrWhiteSpace(website) && website != settings.websiteUrl) throw new BuildFailedException("Copy the public C-Coins website environment configuration before building.");
    }
}
#endif
