using System;
using System.Collections.Generic;
using System.IO;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class GameSaveManager : MonoBehaviour
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
    private float nextSessionCheck;
    private const string CloudPrefix = "CrewCareer_v1_";
    // PlayerPrefs remembers the local save owner, not a valid PlayFab login.
    public bool HasCloudSession => PlayFabClientAPI.IsClientLoggedIn() &&
        !string.IsNullOrEmpty(authenticatedId) && PlayFabSettings.staticPlayer.PlayFabId == authenticatedId &&
        Repository != null && Repository.Owner == authenticatedId;

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
    private void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; productionUpload?.Dispose(); if (Instance == this) Instance = null; }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if(scene.name=="Account"){SaveLoadPanelHost.AddLogoutButton();return;}
        if (scene.name != "Main Menu") return;
        Active = null;
        GameSavePrefs.Activate(null);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // The Load tab opens a panel directly; it does not go through SceneController.
        foreach(var root in scene.GetRootGameObjects())
            foreach(var rect in root.GetComponentsInChildren<RectTransform>(true))
                if(rect.name=="load"&&rect.parent!=null&&rect.parent.name=="Play"&&rect.GetComponent<SaveLoadPanelHost>()==null)
                    rect.gameObject.AddComponent<SaveLoadPanelHost>();
        SaveLoadPanelHost.AddLogoutButton();
    }
    public void SetAccount(string playFabId)
    {
        // Never bind cloud writes from a remembered ID alone or another SDK account.
        if (string.IsNullOrWhiteSpace(playFabId) || !PlayFabClientAPI.IsClientLoggedIn() ||
            PlayFabSettings.staticPlayer.PlayFabId != playFabId) return;
        bool sameCareerOwner = Repository != null && Repository.Owner == playFabId;
        SaveCheckpoint();
        session++;
        productionUpload?.Dispose();
        authenticatedId = playFabId;
        PlayerPrefs.SetString("PlayFabId", playFabId);
        PlayerPrefs.Save();
        AccountProfileData.Bind(playFabId);
        CCoinService.Ensure().BindAccount(playFabId);
        if (!sameCareerOwner)
        {
            Active = null;
            GameSavePrefs.Activate(null);
            Repository = null;
        }
        Syncing = false;
        OpenRepository();
        BindProductionLogs(playFabId);
        try
        {
            var guest = new GameSaveRepository(Path.Combine(Application.persistentDataPath, "CareerSaves"), "guest");
            Repository.ImportUnclaimedGuestSaves(guest);
            CCoinService.Ensure().LinkGuestRewards();
        }
        catch (Exception)
        {
            Status = "Some local saves could not be linked. Originals are kept; log in again to retry.";
            Changed?.Invoke();
            return;
        }
        SyncCloud();
        SaveLoadPanelHost.AddLogoutButton();
    }
    // Recover the save binding if the SDK has authenticated through another login
    // entry point, or if the manager was recreated while the SDK session survived.
    private void ReconcileSession()
    {
        string sdkOwner = PlayFabClientAPI.IsClientLoggedIn() ? PlayFabSettings.staticPlayer.PlayFabId : null;
        if (!string.IsNullOrEmpty(sdkOwner))
        {
            if (!HasCloudSession) SetAccount(sdkOwner);
        }
        else if (!string.IsNullOrEmpty(authenticatedId))
        {
            session++;
            authenticatedId = null;
            Syncing = false;
            productionUpload?.Dispose();
            productionUpload = null;
            Status = "Saved on this device · session disconnected. Sign in to sync.";
            Changed?.Invoke();
            SaveLoadPanelHost.AddLogoutButton();
        }
    }
    public void SignInToSync()
    {
        SaveCheckpoint();
        // Keep the remembered owner and its files while the login screen is open.
        LoadingScreenController.LoadScene("Login");
    }
    public void RetryCloudSync()
    {
        ReconcileSession();
        if (HasCloudSession) { nextProductionSync = Time.unscaledTime; SyncCloud(); }
        else SignInToSync();
    }
    private void OnApplicationFocus(bool focused)
    {
        if (!focused) return;
        ReconcileSession();
        if (HasCloudSession && !Syncing) { nextProductionSync = Time.unscaledTime; SyncCloud(); }
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
        LoadingScreenController.LoadScene(isNew ? "CutScene" : "SingleStudio");
    }
    public GameSaveSlot CreateGame(string name)
    {
        OpenRepository();
        var slot = Repository.Create(name);
        nextSync = Time.unscaledTime + 1f;
        Changed?.Invoke();
        return slot;
    }
    public void Logout()
    {
        // Invalidate in-flight callbacks before changing the save owner.
        session++;authenticatedId=null;Syncing=false;nextSync=float.PositiveInfinity;
        productionUpload?.Dispose(); productionUpload = null; productionJournal = null;
        PlayFabClientAPI.ForgetAllCredentials();
        Active=null;GameSavePrefs.Activate(null);Repository=null;
        PlayerPrefs.DeleteKey("PlayFabId");PlayerPrefs.DeleteKey("PlayerName");PlayerPrefs.Save();
        AccountProfileData.Bind("guest");
        CCoinService.Ensure().BindAccount("guest");
        OpenRepository();Changed?.Invoke();
        LoadingScreenController.LoadScene("Main Menu");
    }
    public void SaveCheckpoint()
    {
        if (GameSavePrefs.IsRoomSession) return;
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
        if (Time.unscaledTime >= nextSessionCheck)
        {
            nextSessionCheck = Time.unscaledTime + 1f;
            ReconcileSession();
        }
        if (!Syncing && Time.unscaledTime >= nextSync) { nextSync = float.PositiveInfinity; SyncCloud(); }
        TickProductionLogs();
    }
    public void SyncCloud()
    {
        if (Syncing || Repository == null) return;
        if (!HasCloudSession)
        {
            Status = "Saved on this device · no active cloud session. Sign in to sync.";
            Changed?.Invoke();
            return;
        }
        var repo = Repository;
        RecoverProductionLogs();
        int requestSession = session;
        Syncing = true;
        Status = "Syncing saves with PlayFab…";
        Changed?.Invoke();
        try
        {
            PlayFabClientAPI.GetUserData(new GetUserDataRequest(), result =>
            {
                if (!IsCurrentSync(repo, requestSession)) return;
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
            }, error => { if (IsCurrentSync(repo, requestSession)) CloudRequestFailed(error, "Cloud sync unavailable. Local saves are ready; retrying automatically."); });
        }
        catch (Exception) { SyncFailed("Cloud sync unavailable. Local saves are ready; retrying automatically."); }
    }
    private void UploadNext(GameSaveRepository repo, int requestSession)
    {
        if (!IsCurrentSync(repo, requestSession)) return;
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
            if (!IsCurrentSync(repo, requestSession)) return;
            try { slot.cloudRevision = revision; repo.Write(slot); UploadNext(repo, requestSession); }
            catch (Exception) { SyncFailed("Cloud sync paused. Your local checkpoint is still available."); }
        }, error => { if (IsCurrentSync(repo, requestSession)) CloudRequestFailed(error, "Cloud upload failed. Checkpoint saved locally; retrying automatically."); });
    }
    private bool IsCurrentSync(GameSaveRepository repo, int generation) =>
        session == generation && Repository == repo && HasCloudSession;
    private void CloudRequestFailed(PlayFabError error, string message)
    {
        if (HandleSessionError(error)) return;
        SyncFailed(message + " (" + error.Error + ")");
    }
    private bool HandleSessionError(PlayFabError error)
    {
        if (error.Error != PlayFabErrorCode.InvalidSessionTicket && error.Error != PlayFabErrorCode.AuthTokenExpired &&
            error.Error != PlayFabErrorCode.NotAuthenticated) return false;
        PlayFabClientAPI.ForgetAllCredentials();
        ReconcileSession();
        return true;
    }
    private void SyncFailed(string message)
    {
        Syncing = false; Status = message;
        nextSync = Time.unscaledTime + 60f;
        Changed?.Invoke();
    }
}
