#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.Burst;

// Apply once on this machine; the Jobs > Burst menu can re-enable compilation later.
internal static class BurstEditorWorkaround
{
    [InitializeOnLoadMethod]
    private static void Apply()
    {
        string key = "CrewOnSet.BurstEditorWorkaround." + Application.dataPath;
        if (EditorPrefs.GetBool(key, false)) return;
        EditorPrefs.SetBool("BurstCompilation", false);
        BurstCompiler.Options.EnableBurstCompilation = false;
        EditorPrefs.SetBool(key, true);
        Debug.Log("Burst Editor compilation disabled to avoid Windows blocking generated DLLs. Restart Unity if a previous Burst error remains.");
    }
}
#endif
