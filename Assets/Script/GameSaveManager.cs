using System;
using System.Collections.Generic;
using System.IO;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameSaveManager : MonoBehaviour
{
    public static GameSaveManager Instance { get; private set; }
    public GameSaveRepository Repository { get; private set; }
    public GameSaveSlot Active { get; private set; }
    public string Status { get; private set; } = "Saves are stored on this device.";
    public bool Syncing { get; private set; }
    public event Action Changed;
    private string authenticatedId;
    private int session;
    private float nextSync = float.PositiveInfinity;
    private const string CloudPrefix = "CrewCareer_v1_";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; GameSavePrefs.Activate(null); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install() { Ensure(); }
    public static GameSaveManager Ensure()
    {
        if (Instance == null) new GameObject("Game Saves").AddComponent<GameSaveManager>();
        return Instance;
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; if (Instance == this) Instance = null; }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Main Menu") return;
        Active = null;
        GameSavePrefs.Activate(null);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    public void SetAccount(string playFabId)
    {
        if (string.IsNullOrWhiteSpace(playFabId)) return;
        session++;
        authenticatedId = playFabId;
        PlayerPrefs.SetString("PlayFabId", playFabId);
        PlayerPrefs.Save();
        Active = null;
        GameSavePrefs.Activate(null);
        Repository = null;
        Syncing = false;
        OpenRepository();
        SyncCloud();
    }
    public void OpenRepository()
    {
        string owner = PlayerPrefs.GetString("PlayFabId", "guest");
        if (string.IsNullOrWhiteSpace(owner)) owner = "guest";
        if (Repository != null && Repository.Owner == owner) return;
        Repository = new GameSaveRepository(Path.Combine(Application.persistentDataPath, "CareerSaves"), owner);
        Status = Repository.Warning ?? (owner == "guest" ? "Guest saves · this device only" : "Local saves ready · log in to sync with PlayFab");
        // Preserve the original shared career exactly once, without deleting its PlayerPrefs.
        if (!PlayerPrefs.HasKey("SaveSystem.LegacyImported") && LegacyGameSave.HasProgress())
        {
            Repository.Create("Existing local game", LegacyGameSave.Read());
            PlayerPrefs.SetString("SaveSystem.LegacyImported", owner);
            PlayerPrefs.Save();
        }
    }
    public void OpenMenu()
    {
        try { OpenRepository(); GameSaveMenu.Show(this); SyncCloud(); }
        catch (Exception) { GameFeedback.Show("SAVE LIST UNAVAILABLE\nCould not read the save folder. Your existing saves have been kept.", true); }
    }
    public void StartGame(GameSaveSlot slot, bool isNew)
    {
        if (Syncing || slot == null || Repository == null || !Repository.Slots.Contains(slot)) return;
        Active = slot;
        GameSavePrefs.Activate(slot);
        // Transient editor/recording state must never cross between careers.
        if (ProjectDataManager.Instance != null) ProjectDataManager.Instance.ClearProject();
        CrossSceneData.finalGrades = default;
        CrossSceneData.submittedLevel = 0;
        CrossSceneData.resultApplied = false;
        DevTutorialBypass.ResetForNewGame();
        PauseManager.isPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(isNew ? "CutScene" : "SingleStudio");
    }
    public GameSaveSlot CreateGame(string name)
    {
        OpenRepository();
        var slot = Repository.Create(name);
        nextSync = Time.unscaledTime + 1f;
        return slot;
    }
    public void SaveCheckpoint()
    {
        if (Active == null || GameSavePrefs.Values == null || Repository == null) return;
        try
        {
            var previous = Active.values;
            Active.values = GameSaveRepository.Clone(GameSavePrefs.Values);
            try { Repository.Commit(Active); }
            catch { Active.values = previous; throw; }
            Status = "Checkpoint saved on this device.";
            nextSync = Time.unscaledTime + 2f;
            Changed?.Invoke();
        }
        catch (Exception) { Status = "Checkpoint could not be saved. Check available disk space."; GameFeedback.Show(Status, true); }
    }
    private void Update()
    {
        if (!Syncing && Time.unscaledTime >= nextSync) { nextSync = float.PositiveInfinity; SyncCloud(); }
    }
    public void SyncCloud()
    {
        if (Syncing || Repository == null) return;
        if (authenticatedId != Repository.Owner || !PlayFabClientAPI.IsClientLoggedIn())
        {
            Status = "Saved on this device · log in to sync with PlayFab";
            Changed?.Invoke();
            return;
        }
        var repo = Repository;
        int requestSession = session;
        Syncing = true;
        Status = "Syncing saves with PlayFab…";
        Changed?.Invoke();
        try
        {
            PlayFabClientAPI.GetUserData(new GetUserDataRequest(), result =>
            {
                if (session != requestSession || Repository != repo) return;
                try
                {
                    if (result.Data != null)
                        foreach (var pair in result.Data)
                        {
                            if (!pair.Key.StartsWith(CloudPrefix, StringComparison.Ordinal)) continue;
                            var remote = JsonUtility.FromJson<GameSaveSlot>(pair.Value.Value);
                            if (remote == null || pair.Key != CloudPrefix + remote.id) throw new InvalidDataException();
                            repo.MergeCloud(remote, Active?.id);
                        }
                    UploadNext(repo, requestSession);
                }
                catch (Exception) { SyncFailed("Some cloud saves could not be read. Local saves are still available."); }
            }, error => { if (session == requestSession) SyncFailed("Cloud sync unavailable. Local saves are ready; use RETRY SYNC later."); });
        }
        catch (Exception) { SyncFailed("Cloud sync unavailable. Local saves are ready; use RETRY SYNC later."); }
    }
    private void UploadNext(GameSaveRepository repo, int requestSession)
    {
        if (session != requestSession || Repository != repo) return;
        var slot = repo.Slots.Find(s => s.cloudRevision != s.revision);
        if (slot == null)
        {
            Syncing = false;
            Status = repo.Warning ?? "All checkpoints synced with PlayFab.";
            Changed?.Invoke();
            return;
        }
        string revision = slot.revision;
        string json = JsonUtility.ToJson(slot);
        PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest
        {
            Permission = UserDataPermission.Private,
            Data = new Dictionary<string, string> { { CloudPrefix + slot.id, json } }
        }, result =>
        {
            if (session != requestSession || Repository != repo) return;
            try { slot.cloudRevision = revision; repo.Write(slot); UploadNext(repo, requestSession); }
            catch (Exception) { SyncFailed("Cloud sync paused. Your local checkpoint is still available."); }
        }, error => { if (session == requestSession) SyncFailed("Cloud upload failed. Checkpoint saved locally; use RETRY SYNC later."); });
    }
    private void SyncFailed(string message) { Syncing = false; Status = message; Changed?.Invoke(); }
}
