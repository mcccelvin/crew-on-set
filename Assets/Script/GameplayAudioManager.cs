using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

// Serialized Resources catalog keeps the original audio assets available in builds.
public sealed class GameplayAudioManager : MonoBehaviour
{
    private static GameplayAudioManager instance;
    private GameplaySoundLibrary library;
    private AudioSource effects, voice, music;
    private readonly HashSet<Button> buttons = new HashSet<Button>();
    private float scanAt, voiceUntil, feedbackAt;
    private int level = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        new GameObject("Gameplay Audio").AddComponent<GameplayAudioManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        library = Resources.Load<GameplaySoundLibrary>("GameplaySounds");
        effects = Source(.45f * GameOptions.SfxVolume);
        voice = Source(.25f * GameOptions.SfxVolume);
        music = Source(.12f * GameOptions.MusicVolume);
        music.loop = true;
        SceneManager.sceneLoaded += SceneLoaded;
        Play("Game Start");
    }

    private AudioSource Source(float volume)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        source.volume = volume;
        return source;
    }

    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        level = -1; scanAt = 0;
        StopVoice();
        Play("ClapperBoard Transition");
    }

    public static AudioClip Clip(string prefix) => instance != null && instance.library != null ? instance.library.Find(prefix) : null;

    public static void Play(string prefix)
    {
        var clip = Clip(prefix);
        if (clip != null) instance.effects.PlayOneShot(clip);
    }

    public static void Feedback(string text, bool error)
    {
        if (instance == null || Time.unscaledTime < instance.feedbackAt) return;
        instance.feedbackAt = Time.unscaledTime + .3f;
        Play(error ? "Fail by" : (text ?? "").StartsWith("PURCHASE CONFIRMED") ? "Success by" : "Notification by");
    }

    public static void Speak()
    {
        if (instance == null) return;
        instance.voice.Stop();
        instance.voice.clip = Clip("Voice " + Random.Range(1, 8));
        instance.voice.Play();
        instance.voiceUntil = Time.unscaledTime + 1.4f;
    }

    public static void StopVoice() { if (instance != null) instance.voice.Stop(); }

    private void Update()
    {
        if (Time.unscaledTime >= voiceUntil) voice.Stop();
        effects.volume = .45f * GameOptions.SfxVolume;
        voice.volume = .25f * GameOptions.SfxVolume;
        music.volume = (PauseManager.isPaused || MultiplayerPauseManager.isPaused || voice.isPlaying ? .035f : .12f) * GameOptions.MusicVolume;
        if (Time.unscaledTime < scanAt) return;
        scanAt = Time.unscaledTime + .5f;
        buttons.RemoveWhere(b => b == null);
        foreach (var button in FindObjectsOfType<Button>(true))
        {
            if (!buttons.Add(button)) continue;
            string name = button.name.ToLowerInvariant();
            bool cancel = name.Contains("cancel") || name.Contains("close") || name.Contains("exit") || name.Contains("back");
            button.onClick.AddListener(() => Play(cancel ? "Cancel-Exit" : "Click Button"));
        }
        int current = CampaignProgression.GetCurrentLevel();
        if (current == level) return;
        level = current;
        string scene = SceneManager.GetActiveScene().name;
        music.Stop();
        if (scene != "SingleStudio" && scene != "MultiStudio") return;
        string[] tracks = { "BG Music by Andrii", "BG Music by FASSounds", "BG Music by Dmitriy", "BG Music by Nesterouk", "bg music kinda lofi" };
        music.clip = Clip(tracks[Mathf.Clamp(level - 1, 0, tracks.Length - 1)]);
        if (music.clip != null) music.Play();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        if (instance == this) instance = null;
    }
}
