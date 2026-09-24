using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public sealed class GameplaySoundLibraryBuilder : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    static GameplaySoundLibraryBuilder()
    {
        EditorApplication.delayCall += Rebuild;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Rebuild;
        };
    }
    public void OnPreprocessBuild(BuildReport report) { Rebuild(); }

    [MenuItem("Crew-On-Set/Edit Gameplay Sounds")]
    public static void EditSounds()
    {
        Rebuild();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameplaySoundLibrary>("Assets/Resources/GameplaySounds.asset");
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    [MenuItem("Crew-On-Set/Rebuild Sound Library")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        const string path = "Assets/Resources/GameplaySounds.asset";
        var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/SOUND EFFECTS", "Assets/Resources/Character/Sfx" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToArray();
        var library = AssetDatabase.LoadAssetAtPath<GameplaySoundLibrary>(path);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<GameplaySoundLibrary>();
            AssetDatabase.CreateAsset(library, path);
        }
        bool changed = library.clips == null || !library.clips.SequenceEqual(clips);
        library.clips = clips;
        if (!library.namedSoundsInitialized)
        {
            if (library.sounds == null) library.sounds = new List<GameplaySoundLibrary.SoundEntry>();
            void Add(string label, string key)
            {
                if (library.sounds.Any(s => s != null && string.Equals(s.key, key, StringComparison.OrdinalIgnoreCase))) return;
                var clip = clips.FirstOrDefault(c => c.name.StartsWith(key, StringComparison.OrdinalIgnoreCase));
                library.sounds.Add(new GameplaySoundLibrary.SoundEntry {
                    name = label, key = key, variants = clip != null ? new[] { clip } : new AudioClip[0]
                });
            }
            Add("Game / Start", "Game Start");
            Add("Game / Scene Transition", "ClapperBoard Transition");
            Add("UI / Click", "Click Button");
            Add("UI / Cancel or Close", "Cancel-Exit");
            Add("Feedback / Error", "Fail by");
            Add("Feedback / Purchase Success", "Success by");
            Add("Feedback / Notification", "Notification by");
            Add("Camera / Start Recording", "Start Recording");
            Add("Camera / Stop Recording", "Stop Recording");
            Add("Player / Landing", "Player_Land");
            Add("Player / Footstep 1", "Player_Footstep_01");
            Add("Player / Footstep 2", "Player_Footstep_02");
            Add("Player / Footstep 3", "Player_Footstep_03");
            Add("Player / Footstep 4", "Player_Footstep_04");
            Add("Player / Footstep 5", "Player_Footstep_05");
            Add("Player / Footstep 6", "Player_Footstep_06");
            Add("Player / Footstep 7", "Player_Footstep_07");
            Add("Player / Footstep 8", "Player_Footstep_08");
            Add("Player / Footstep 9", "Player_Footstep_09");
            Add("Player / Footstep 10", "Player_Footstep_10");
            Add("Coffee / Beans", "Coffee Beans");
            Add("Coffee / Grinder", "Coffee Grinder");
            Add("Coffee / Drip", "Coffee Drip");
            Add("Coffee / Mug on Table", "Mug on Table");
            Add("Boss / Voice 1", "Voice 1");
            Add("Boss / Voice 2", "Voice 2");
            Add("Boss / Voice 3", "Voice 3");
            Add("Boss / Voice 4", "Voice 4");
            Add("Boss / Voice 5", "Voice 5");
            Add("Boss / Voice 6", "Voice 6");
            Add("Boss / Voice 7", "Voice 7");
            Add("Music / Level 1", "BG Music by Andrii");
            Add("Music / Level 2", "BG Music by FASSounds");
            Add("Music / Level 3", "BG Music by Dmitriy");
            Add("Music / Level 4", "BG Music by Nesterouk");
            Add("Music / Level 5", "bg music kinda lofi");
            foreach (var clip in clips)
                if (!library.sounds.Any(s => s != null && s.variants != null && s.variants.Contains(clip)))
                    Add("Extra / " + clip.name, clip.name);
            library.namedSoundsInitialized = true;
            changed = true;
        }
        // Rebuild the discovery cache only. Never replace or refill user assignments.
        if (!changed) return;
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
    }
}
