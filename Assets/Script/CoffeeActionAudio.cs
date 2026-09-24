using UnityEngine;

public sealed class CoffeeActionAudio : MonoBehaviour
{
    private AudioSource source;
    private Transform machine;
    private float elapsed;
    private int phase;

    public static void Begin(GameObject owner, Transform target)
    {
        var sound = owner.GetComponent<CoffeeActionAudio>();
        if (sound == null) sound = owner.AddComponent<CoffeeActionAudio>();
        sound.machine = target;
        sound.elapsed = 0; sound.phase = 0;
        if (sound.source == null)
        {
            sound.source = owner.AddComponent<AudioSource>();
            sound.source.playOnAwake = false;
            sound.source.spatialBlend = 1;
            sound.source.minDistance = 1;
            sound.source.maxDistance = 12;
            sound.source.volume = .45f * GameOptions.SfxVolume;
        }
        sound.enabled = true;
        sound.Play("Coffee Beans");
    }

    private void Play(string key) { source.Stop(); source.clip = GameplayAudioManager.Clip(key); if (source.clip != null) source.Play(); }
    public static void End(GameObject owner)
    {
        var sound = owner.GetComponent<CoffeeActionAudio>();
        if (sound != null) sound.enabled = false;
    }
    private void Update()
    {
        source.volume = .45f * GameOptions.SfxVolume;
        if (machine == null || !machine.gameObject.activeInHierarchy) { enabled = false; return; }
        if (PauseManager.isPaused) { source.Pause(); return; }
        source.UnPause();
        elapsed += Time.deltaTime;
        if (phase == 0 && elapsed >= .4f) { phase = 1; Play("Coffee Grinder"); }
        if (phase == 1 && elapsed >= 1.5f) { phase = 2; Play("Coffee Drip"); }
        if (elapsed >= 3f) { elapsed = 0; phase = 0; Play("Coffee Beans"); }
    }
    private void OnDisable() { if (source != null) source.Stop(); }
}
