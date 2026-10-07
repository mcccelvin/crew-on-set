// Compile together with Assets/Script/GameSaveManager.cs using RunGameSaveSessionChecks.ps1.
// Fake SDK/filesystem: tests execute the real manager without touching accounts or saves.
using System;
using System.Collections.Generic;
using System.Reflection;
using PlayFab;
using PlayFab.ClientModels;

static class GameSaveSessionChecks
{
    static void Call(GameSaveManager m, string method, params object[] args) => typeof(GameSaveManager).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(m, args);
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static GameSaveManager Setup(string owner, bool authenticated)
    {
        UnityEngine.PlayerPrefs.SetString("PlayFabId", owner);
        PlayFabSettings.staticPlayer.PlayFabId = owner;
        PlayFabClientAPI.LoggedIn = authenticated;
        PlayFabClientAPI.Reads = PlayFabClientAPI.Writes = 0;
        GameSaveRepository.Reset(); LegacyGameSave.Progress = false;
        UnityEngine.PlayerPrefs.DeleteKey("SaveSystem.LegacyImported");
        CCoinService.LinkAttempts = CCoinService.LinkFailures = 0;
        CCoinService.Refreshes = AccountProfileData.Refreshes = AccountAppearanceData.Refreshes = 0;
        UnityEngine.Application.internetReachability = UnityEngine.NetworkReachability.ReachableViaLocalAreaNetwork;
        UnityEngine.Time.unscaledTime = 1;
        var m = new GameSaveManager();
        m.OpenRepository();
        return m;
    }
    static void Login(GameSaveManager m, string id) { PlayFabClientAPI.LoggedIn = true; PlayFabSettings.staticPlayer.PlayFabId = id; m.SetAccount(id); }
    public static void Main()
    {
        var m = Setup("A", false);
        m.SetAccount("A"); m.SyncCloud();
        Check(!m.HasCloudSession && PlayFabClientAPI.Reads == 0, "remembered ID cannot authenticate or upload");
        m.RetryCloudSync();
        Check(LoadingScreenController.Last == "Login" && UnityEngine.PlayerPrefs.GetString("PlayFabId", "") == "A", "sign-in route retains remembered save owner");

        m = Setup("A", true);
        var slot = m.CreateGame("Existing level 3");
        m.StartGame(slot, false);
        var selectedValues = GameSavePrefs.Values;
        Call(m, "Update");
        Check(m.HasCloudSession && PlayFabClientAPI.Reads == 1, "SDK login reconnects an unbound save manager");
        Check(ReferenceEquals(m.Active, slot) && ReferenceEquals(GameSavePrefs.Values, selectedValues), "reconnecting preserves selected career and gameplay values");
        Check(m.Repository.Commits == 1, "reconnecting checkpoints current progress before cloud operations");
        PlayFabClientAPI.ReadSuccess(new GetUserDataResult());
        Check(PlayFabClientAPI.Writes == 1, "reconnected account uploads its pending checkpoint");
        PlayFabClientAPI.WriteSuccess(new UpdateUserDataResult());
        Check(!m.Syncing && slot.cloudRevision == slot.revision, "upload completion marks the actual revision synced");

        PlayFabClientAPI.ForgetAllCredentials(); Call(m, "ReconcileSession");
        Check(!m.HasCloudSession && !m.Syncing && ReferenceEquals(m.Active, slot), "session loss keeps career and clears in-flight state");
        m.RetryCloudSync();
        Check(LoadingScreenController.Last == "Login", "disconnected retry opens authentication");
        Login(m, "A");
        Check(m.HasCloudSession && ReferenceEquals(m.Active, slot), "same-account login retains the career");
        var staleSuccess = PlayFabClientAPI.ReadSuccess;
        var staleFailure = PlayFabClientAPI.ReadFailure;
        int writes = PlayFabClientAPI.Writes;
        PlayFabSettings.staticPlayer.PlayFabId = "B";
        staleSuccess(new GetUserDataResult());
        Check(PlayFabClientAPI.Writes == writes, "account change blocks old callback even before reconciliation");
        Call(m, "ReconcileSession");
        Check(m.HasCloudSession && m.Repository.Owner == "B" && m.Active == null, "different account switches to its own repository");
        staleFailure(new PlayFabError { Error = PlayFabErrorCode.InvalidSessionTicket });
        Check(m.HasCloudSession, "stale authentication error cannot log out the new account");
        PlayFabClientAPI.ReadFailure(new PlayFabError { Error = PlayFabErrorCode.ConnectionError });
        Check(m.HasCloudSession && !m.Syncing, "network failure preserves authentication and allows retry");
        UnityEngine.Time.unscaledTime += 61; Call(m, "Update");
        Check(m.Syncing, "network failure retries after the delay");
        PlayFabClientAPI.ReadFailure(new PlayFabError { Error = PlayFabErrorCode.InvalidSessionTicket });
        Check(!m.HasCloudSession && !m.Syncing && m.Status.Contains("Sign in"), "rejected session routes to reauthentication without deleting saves");

        m = Setup("A", true); Login(m, "A");
        var pending = m.CreateGame("pending");
        PlayFabClientAPI.ReadSuccess(new GetUserDataResult());
        var oldWrite = PlayFabClientAPI.WriteSuccess;
        m.Logout(); oldWrite(new UpdateUserDataResult());
        Check(pending.cloudRevision != pending.revision && !m.HasCloudSession, "late upload receipt cannot mutate state after logout");
        Check(UnityEngine.PlayerPrefs.GetString("PlayFabId", "") == "", "logout clears remembered identity");

        m = Setup("guest", false);
        var guest = m.Repository;
        var guestSlot = guest.Create("Offline production");
        Login(m, "A");
        Check(m.HasCloudSession && PlayFabClientAPI.Reads == 1, "login starts cloud download without a sync button");
        Check(m.Repository.Slots.Count == 1 && m.Repository.Slots[0].id == guestSlot.id && m.Repository.Slots[0].owner == "A", "login links unclaimed local guest progress to the authenticated owner");
        Check(guestSlot.linkedAccount == "A" && guest.Slots.Count == 1, "guest originals remain with a stable one-account claim");
        PlayFabClientAPI.ReadSuccess(new GetUserDataResult());
        Check(PlayFabClientAPI.Writes == 1 && PlayFabClientAPI.LastWrite.Permission == UserDataPermission.Private, "login automatically uploads the linked private checkpoint");
        PlayFabClientAPI.WriteSuccess(new UpdateUserDataResult());
        Check(m.LogsReadyToUpload, "login rearms completed-production uploads after checkpoint sync");
        Call(m, "Update");
        Check(m.ProductionSyncAttempts == 1, "production-log queue runs without a manual sync action");
        Login(m, "A");
        Check(m.Repository.Slots.Count == 1, "repeat login does not duplicate linked guest progress");
        Login(m, "B");
        Check(m.Repository.Slots.Count == 0 && guestSlot.linkedAccount == "A", "a different login cannot claim another account's local progress");

        m = Setup("guest", false);
        guestSlot = m.Repository.Create("Import needs retry");
        GameSaveRepository.ImportFailures = 1;
        Login(m, "A");
        Check(PlayFabClientAPI.Reads == 1, "local import failure does not stop automatic account sync");
        PlayFabClientAPI.ReadSuccess(new GetUserDataResult());
        Check(!m.Syncing && guestSlot.linkedAccount == null, "failed linking keeps the guest original unclaimed");
        UnityEngine.Time.unscaledTime += 61; Call(m, "Update");
        Check(m.Repository.Slots.Count == 1 && guestSlot.linkedAccount == "A" && m.Syncing, "failed guest linking retries automatically without another login");
        PlayFabClientAPI.ReadSuccess(new GetUserDataResult());PlayFabClientAPI.WriteSuccess(new UpdateUserDataResult());
        Check(m.Repository.Slots[0].cloudRevision == m.Repository.Slots[0].revision, "automatic retry uploads the actual linked checkpoint");

        m = Setup("A", false); var own = m.Repository.Create("Account-owned offline checkpoint");
        CCoinService.LinkFailures = 1; Login(m, "A");
        PlayFabClientAPI.ReadSuccess(new GetUserDataResult());PlayFabClientAPI.WriteSuccess(new UpdateUserDataResult());
        Check(own.cloudRevision == own.revision && m.LogsReadyToUpload, "wallet-link failure cannot block checkpoint or production-log syncing");
        UnityEngine.Time.unscaledTime += 61; Call(m, "Update");
        Check(CCoinService.LinkAttempts == 2 && m.Syncing, "wallet linking also retries without a sync button");

        m = Setup("A", false); Login(m, "A");
        var remote = new GameSaveSlot { id = "downloaded", owner = "A", name = "Other device production", revision = "remote" };
        PlayFabClientAPI.ReadSuccess(new GetUserDataResult { Data = new Dictionary<string,UserDataRecord> {
            { "CrewCareer_v1_downloaded", new UserDataRecord { Value = UnityEngine.JsonUtility.ToJson(remote) } }
        } });
        Check(!m.Syncing && m.Repository.Slots.Contains(remote) && m.LastRecoveredSlots == 1 && m.LogsReadyToUpload, "downloaded completed snapshots are recovered and scheduled in the same automatic sync");

        m = Setup("guest", false);
        UnityEngine.PlayerPrefs.SetString("PlayFabId", "A");LegacyGameSave.Progress = true;
        m = new GameSaveManager();Login(m, "B");
        var oldAccount = new GameSaveRepository("FAKE-SAVES-NO-IO", "A");
        Check(UnityEngine.PlayerPrefs.GetString("SaveSystem.LegacyImported", "") == "A" && oldAccount.Slots.Count == 1 && m.Repository.Slots.Count == 0, "legacy local progress keeps its remembered owner before a different login");

        m = Setup("A", false);
        var offline = m.Repository.Create("Offline checkpoint");
        UnityEngine.Application.internetReachability = UnityEngine.NetworkReachability.NotReachable;
        Login(m, "A"); Call(m, "Update");
        Check(m.HasCloudSession && !m.Syncing && PlayFabClientAPI.Reads == 0 && CCoinService.Refreshes == 0, "offline login keeps local progress without cloud requests");
        UnityEngine.Application.internetReachability = UnityEngine.NetworkReachability.ReachableViaLocalAreaNetwork;
        UnityEngine.Time.unscaledTime += 2; Call(m, "Update");
        Check(m.Syncing && PlayFabClientAPI.Reads == 1 && CCoinService.Refreshes == 1 && AccountProfileData.Refreshes == 1 && AccountAppearanceData.Refreshes == 1, "internet return immediately schedules saves, wallet, profile and appearance");
        Call(m, "Update");
        Check(PlayFabClientAPI.Reads == 1 && CCoinService.Refreshes == 1, "stable internet does not start duplicate reconnect requests");
        PlayFabClientAPI.ReadSuccess(new GetUserDataResult()); PlayFabClientAPI.WriteSuccess(new UpdateUserDataResult());
        Check(offline.cloudRevision == offline.revision, "reconnect uploads the existing local checkpoint automatically");
        UnityEngine.Time.unscaledTime += 61; Call(m, "Update");
        Check(m.Syncing && PlayFabClientAPI.Reads == 2, "connected session refreshes periodically without sync controls");
        PlayFabClientAPI.ReadSuccess(new GetUserDataResult());
        Call(m, "OnApplicationFocus", true);
        Check(PlayFabClientAPI.Reads == 3 && CCoinService.Refreshes == 2 && AccountAppearanceData.Refreshes == 2, "browser return automatically requests the confirmed wallet and appearance");

        m = Setup("A", false); Login(m, "A");
        Check(CCoinService.Refreshes == 1 && AccountProfileData.Refreshes == 1 && AccountAppearanceData.Refreshes == 1, "every authenticated login refreshes all account services");
        Console.WriteLine("All session checks passed.");
    }
}

namespace UnityEngine {
    public class MonoBehaviour { protected static void Destroy(object o) {} protected static void DontDestroyOnLoad(object o) {} public GameObject gameObject = null; }
    public class GameObject { public string name; public GameObject(string n) { name = n; } public T AddComponent<T>() where T:new() => new T(); public T[] GetComponentsInChildren<T>(bool b) => new T[0]; }
    public class RectTransform { public string name; public RectTransform parent; public GameObject gameObject = new GameObject(""); public T GetComponent<T>() where T:class => null; }
    public enum RuntimeInitializeLoadType { SubsystemRegistration, BeforeSceneLoad }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) {} }
    public static class PlayerPrefs { static Dictionary<string,string> d = new Dictionary<string,string>(); public static void SetString(string k,string v) { d[k]=v; } public static string GetString(string k,string fallback) => d.ContainsKey(k)?d[k]:fallback; public static bool HasKey(string k)=>d.ContainsKey(k); public static void DeleteKey(string k)=>d.Remove(k); public static void Save() {} }
    public enum NetworkReachability { NotReachable, ReachableViaCarrierDataNetwork, ReachableViaLocalAreaNetwork }
    public static class Application { public static string persistentDataPath = "FAKE-SAVES-NO-IO"; public static NetworkReachability internetReachability = NetworkReachability.ReachableViaLocalAreaNetwork; }
    public static class Time { public static float unscaledTime, timeScale; }
    public enum CursorLockMode { None }
    public static class Cursor { public static CursorLockMode lockState; public static bool visible; }
    public static class JsonUtility { static Dictionary<string,object> snapshots = new Dictionary<string,object>(); public static T FromJson<T>(string s) where T:new()=>snapshots.ContainsKey(s)?(T)snapshots[s]:new T(); public static string ToJson(object o){string id=Guid.NewGuid().ToString("N");snapshots[id]=o;return id;} }
    public static class Debug { public static void LogWarning(string text){Console.WriteLine("EXPECTED WARNING: "+text);} }
}
namespace UnityEngine.SceneManagement {
    public struct Scene { public string name; public UnityEngine.GameObject[] GetRootGameObjects()=>new UnityEngine.GameObject[0]; }
    public enum LoadSceneMode { Single }
    public static class SceneManager { public static event Action<Scene,LoadSceneMode> sceneLoaded; }
}
namespace PlayFab {
    public enum PlayFabErrorCode { InvalidSessionTicket, AuthTokenExpired, NotAuthenticated, ConnectionError }
    public class PlayFabError { public PlayFabErrorCode Error; }
    public class Player { public string PlayFabId; }
    public static class PlayFabSettings { public static Player staticPlayer=new Player(); }
    public static class PlayFabClientAPI {
        public static bool LoggedIn; public static int Reads,Writes;
        public static Action<GetUserDataResult> ReadSuccess; public static Action<PlayFabError> ReadFailure;
        public static Action<UpdateUserDataResult> WriteSuccess;
        public static UpdateUserDataRequest LastWrite;
        public static bool IsClientLoggedIn()=>LoggedIn;
        public static void ForgetAllCredentials(){LoggedIn=false;}
        public static void GetUserData(GetUserDataRequest r,Action<GetUserDataResult> ok,Action<PlayFabError> fail){Reads++;ReadSuccess=ok;ReadFailure=fail;}
        public static void UpdateUserData(UpdateUserDataRequest r,Action<UpdateUserDataResult> ok,Action<PlayFabError> fail){Writes++;LastWrite=r;WriteSuccess=ok;}
    }
}
namespace PlayFab.ClientModels {
    public class GetUserDataRequest {} public class GetUserDataResult { public Dictionary<string,UserDataRecord> Data; }
    public class UserDataRecord { public string Value; }
    public class UpdateUserDataRequest { public UserDataPermission Permission; public Dictionary<string,string> Data; }
    public class UpdateUserDataResult {} public enum UserDataPermission { Private }
}
public class GameSaveSlot { public string id=Guid.NewGuid().ToString("N"),name,owner,linkedAccount,revision="local",cloudRevision,updatedUtc; public List<GameSaveValue> values=new List<GameSaveValue>(); }
public class GameSaveValue {}
public class GameSaveRepository {
    static Dictionary<string,List<GameSaveSlot>> storage = new Dictionary<string,List<GameSaveSlot>>(); public static int ImportFailures;
    public static void Reset(){storage.Clear();ImportFailures=0;}
    public string Owner,Warning; public int Commits; public List<GameSaveSlot> Slots;
    public GameSaveRepository(string root,string owner){Owner=owner;if(!storage.ContainsKey(owner))storage[owner]=new List<GameSaveSlot>();Slots=storage[owner];}
    public GameSaveSlot Create(string name,List<GameSaveValue> values=null){var s=new GameSaveSlot{name=name,owner=Owner,values=values??new List<GameSaveValue>()};Slots.Add(s);return s;}
    public void ImportUnclaimedGuestSaves(GameSaveRepository r){if(ImportFailures>0){ImportFailures--;throw new Exception("simulated local import failure");}foreach(var source in r.Slots){if(source.linkedAccount!=null)continue;if(!Slots.Exists(s=>s.id==source.id)){var copy=Create(source.name,Clone(source.values));copy.id=source.id;}source.linkedAccount=Owner;}} public void Commit(GameSaveSlot s){Commits++;s.revision="changed";}
    public void Write(GameSaveSlot s){} public void MergeCloud(GameSaveSlot s,string id){s.cloudRevision=s.revision;Slots.Add(s);}
    public static List<GameSaveValue> Clone(List<GameSaveValue> v)=>new List<GameSaveValue>(v);
}
public static class GameSavePrefs { public static List<GameSaveValue> Values; public static bool IsRoomSession; public static void Activate(GameSaveSlot s){Values=s?.values;} }
public static class AccountProfileData { public static int Refreshes; public static void Bind(string id){} public static void EnsureBound(){} public static void Refresh(){Refreshes++;} }
public static class AccountAppearanceData { public static int Refreshes; public static void Refresh(){Refreshes++;} }
public class CCoinService { public static int LinkAttempts,LinkFailures,Refreshes; public bool DevWalletActive=>false; public static CCoinService Ensure()=>new CCoinService(); public void BindAccount(string id){} public void LinkGuestRewards(){LinkAttempts++;if(LinkFailures>0){LinkFailures--;throw new Exception("simulated wallet-link failure");}} public void Refresh(){Refreshes++;} }
public class SaveLoadPanelHost { public static void AddLogoutButton(){} }
public static class GameSaveMenu { public static void Show(GameSaveManager m){} }
public static class GameFeedback { public static void Show(string s,bool b){} }
public static class LegacyGameSave { public static bool Progress; public static bool HasProgress()=>Progress; public static List<GameSaveValue> Read()=>new List<GameSaveValue>(); }
public class ProjectDataManager { public static ProjectDataManager Instance; public void ClearProject(){} }
public static class CrossSceneData { public static int finalGrades,submittedLevel; public static bool resultApplied; }
public static class DevTutorialBypass { public static void ResetForNewGame(){} }
public static class PauseManager { public static bool isPaused; }
public static class LoadingScreenController { public static string Last; public static void LoadScene(string s){Last=s;} }
public sealed partial class GameSaveManager {
    class Upload { public void Dispose(){} }
    Upload productionUpload; object productionJournal; float nextProductionSync;
    public int LastRecoveredSlots,ProductionSyncAttempts;
    public bool LogsReadyToUpload=>!Syncing&&HasCloudSession&&nextProductionSync<=UnityEngine.Time.unscaledTime;
    void BindProductionLogs(string id){productionUpload=new Upload();nextProductionSync=float.PositiveInfinity;}
    void RecoverProductionLogs(){LastRecoveredSlots=Repository.Slots.Count;}
    void TickProductionLogs(){if(LogsReadyToUpload){ProductionSyncAttempts++;nextProductionSync=float.PositiveInfinity;}}
}
