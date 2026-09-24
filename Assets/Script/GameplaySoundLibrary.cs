using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GameplaySoundLibrary : ScriptableObject
{
    [Serializable]
    public sealed class SoundEntry
    {
        public string name;
        [Tooltip("Stable action key used by gameplay. Rename the display name freely; keep this key for existing actions.")]
        public string key;
        [Tooltip("Drag sounds here. One is chosen randomly each time. Empty slots mute this action.")]
        public AudioClip[] variants = new AudioClip[0];
    }

    public List<SoundEntry> sounds = new List<SoundEntry>();
    [HideInInspector] public bool namedSoundsInitialized;
    // Retain old references and support callers not migrated into a named entry.
    [HideInInspector] public AudioClip[] clips;

    public AudioClip Find(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (sounds != null)
            foreach (var sound in sounds)
            {
                if (sound == null || !string.Equals(sound.key, key, StringComparison.OrdinalIgnoreCase)) continue;
                if (sound.variants == null || sound.variants.Length == 0) return null;
                return sound.variants[UnityEngine.Random.Range(0, sound.variants.Length)];
            }
        if (clips != null)
            foreach (var clip in clips)
                if (clip != null && clip.name.StartsWith(key, StringComparison.OrdinalIgnoreCase)) return clip;
        return null;
    }
}
