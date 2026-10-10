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
        BudgetChecks();
        Console.WriteLine("All session checks passed.");
    }
    static void Set(string key, int value)
    {
        GameSavePrefs.SetInt(key,value);
    }
    static int Money(GameSaveSlot slot) => slot.values.Find(v=>v.key=="PlayerMoney" && v.kind==0)?.integer ?? 0;
    static void TextValue(string key,string text) { GameSavePrefs.Values.RemoveAll(v=>v.key==key); GameSavePrefs.Values.Add(new GameSaveValue{key=key,kind=2,text=text}); }
    static void BudgetChecks()
    {
        var m=Setup("A",false);var slot=m.CreateGame("Checkpoint budget");m.StartGame(slot,false);
        Set("CurrentLevel",1);Set("PlayerMoney",9000);Set("Level1StartingBudgetGranted",1);m.SaveBudgetCheckpoint();
        Check(Money(slot)==9000,"starter grant establishes a 9000 B-Coin checkpoint");
        Set("PlayerMoney",700);Set("OwnedEquipment.NONY FX",1);Set("OwnedEquipment.160 LED PANEL",1);
        Set("Level2CameraPurchased",1);Set("OwnedInterior.2",1);Set("TutorialProgress",1);
        TextValue("Analytics.Career.v1","spent during this attempt");TextValue("Profile.Career.v1","lifetime evidence");
        TextValue("CCoins.CareerRewardId","stable-reward-career");Set("Knowledge_director_tablet",1);Set("AchivDone_director_tablet",1);
        m.SaveCheckpoint();m.SaveCheckpoint();
        Check(Money(slot)==9000 && GameSavePrefs.GetInt("PlayerMoney")==700,"purchase/analytics saves retain checkpoint without refunding live cash");
        Check(GameSavePrefs.GetInt("OwnedEquipment.NONY FX")==1 && !slot.values.Exists(v=>v.key=="OwnedEquipment.NONY FX"),"live purchase stays usable but is not added to resume ownership");
        m.StartGame(slot,false);Check(GameSavePrefs.GetInt("PlayerMoney")==9000,"Continue restores checkpoint money after abandoned attempt");
        Check(GameSavePrefs.GetInt("OwnedEquipment.NONY FX")==0 && GameSavePrefs.GetInt("OwnedEquipment.160 LED PANEL")==0 && GameSavePrefs.GetInt("Level2CameraPurchased")==0,"Continue rolls back unfinished equipment purchases and shop flags");
        Check(GameSavePrefs.GetInt("OwnedInterior.2")==0 && GameSavePrefs.GetInt("TutorialProgress")==0,"Continue rolls back unfinished interior and lesson state");
        Check(GameSavePrefs.GetInt("Level1StartingBudgetGranted")==1,"Continue keeps checkpoint advance flags without granting twice");
        Check(GameSavePrefs.Values.Exists(v=>v.key=="Analytics.Career.v1" && v.text=="spent during this attempt") && GameSavePrefs.Values.Exists(v=>v.key=="Profile.Career.v1" && v.text=="lifetime evidence"),"completed/lifetime evidence is retained outside production rollback");
        Check(GameSavePrefs.Values.Exists(v=>v.key=="CCoins.CareerRewardId" && v.text=="stable-reward-career") && GameSavePrefs.GetInt("Knowledge_director_tablet")==1 && GameSavePrefs.GetInt("AchivDone_director_tablet")==1,"reward identity, learned skills and earned achievements survive Continue");
        Set("PlayerMoney",1000);Login(m,"A");
        Check(Money(slot)==9000 && GameSavePrefs.GetInt("PlayerMoney")==1000,"same-account login cannot checkpoint mid-contract spending");
        PlayFabClientAPI.ReadSuccess(new GetUserDataResult());
        var uploaded=UnityEngine.JsonUtility.FromJson<GameSaveSlot>(PlayFabClientAPI.LastWrite.Data["CrewCareer_v1_"+slot.id]);
        Check(Money(uploaded)==9000,"automatic cloud upload carries resume budget, not temporary spending");
        PlayFabClientAPI.WriteSuccess(new UpdateUserDataResult());
        Set("CurrentLevel",2);Set("PlayerMoney",2700);Set("OwnedEquipment.NONY FX",1);Set("OwnedEquipment.160 LED PANEL",1);m.SaveBudgetCheckpoint();
        Set("PlayerMoney",100);m.SaveCheckpoint();m.StartGame(slot,false);
        Check(GameSavePrefs.GetInt("PlayerMoney")==2700,"passed contract checkpoints the actual remaining budget plus reward");
        Check(GameSavePrefs.GetInt("OwnedEquipment.NONY FX")==1 && GameSavePrefs.GetInt("OwnedEquipment.160 LED PANEL")==1,"equipment acquired before a completed checkpoint is kept");
        Set("GokeContractAccepted",1);Set("PlayerMoney",13200);m.SaveBudgetCheckpoint();
        Set("PlayerMoney",500);m.SaveCheckpoint();m.StartGame(slot,false);
        Check(GameSavePrefs.GetInt("PlayerMoney")==13200 && GameSavePrefs.GetInt("GokeContractAccepted")==1,"acceptance checkpoint retains advance flag without paying it twice");
        var other=m.CreateGame("Independent budget");m.StartGame(other,false);Set("PlayerMoney",4000);m.SaveBudgetCheckpoint();
        Set("PlayerMoney",0);m.SaveCheckpoint();m.StartGame(slot,false);
        Check(GameSavePrefs.GetInt("PlayerMoney")==13200 && Money(other)==4000,"careers keep independent checkpoint budgets");
        GameSavePrefs.IsRoomSession=true;Set("PlayerMoney",10);m.SaveBudgetCheckpoint();m.SaveCheckpoint();GameSavePrefs.IsRoomSession=false;
        Check(Money(slot)==13200,"room preference writes cannot replace solo budget");
        Set("PlayerMoney",12000);m.SaveBudgetCheckpoint();Set("PlayerMoney",0);m.SaveCheckpoint();m.StartGame(slot,false);
        Check(GameSavePrefs.GetInt("PlayerMoney")==12000,"explicit retry can establish the restored budget");
        Set("PlayerMoney",0);m.SaveBudgetCheckpoint();m.StartGame(slot,false);
        Check(GameSavePrefs.GetInt("PlayerMoney")==0,"explicit career reset can clear budget instead of resurrecting checkpoint funds");

        var type=typeof(GameSaveManager).GetNestedType("LegacyBudgetStart",BindingFlags.NonPublic);
        var start=Activator.CreateInstance(type,true);type.GetField("level").SetValue(start,1);
        type.GetField("values").SetValue(start,new List<GameSaveValue>{new GameSaveValue{key="PlayerMoney",integer=9000}});
        var legacy=m.Repository.Create("Legacy spent balance",new List<GameSaveValue>{new GameSaveValue{key="PlayerMoney",integer=700},new GameSaveValue{key="BudgetRetry.Start.v1",kind=2,text=UnityEngine.JsonUtility.ToJson(start)}});
        m.StartGame(legacy,false);Check(GameSavePrefs.GetInt("PlayerMoney")==9000 && Money(legacy)==700,"legacy Continue recovers known same-contract start without mutating original until save");
        var partial=m.Repository.Create("Old money-only checkpoint with purchased camera",new List<GameSaveValue>{new GameSaveValue{key="PlayerMoney",integer=9000},new GameSaveValue{key="BudgetCheckpoint.BCoins.v1",integer=9000},new GameSaveValue{key="OwnedEquipment.NONY FX",integer=1},new GameSaveValue{key="TutorialProgress",integer=1},new GameSaveValue{key="BudgetRetry.Start.v1",kind=2,text=UnityEngine.JsonUtility.ToJson(start)},new GameSaveValue{key="CCoins.CareerRewardId",kind=2,text="keep-identity"}});
        m.StartGame(partial,false);
        Check(GameSavePrefs.GetInt("PlayerMoney")==9000 && GameSavePrefs.GetInt("OwnedEquipment.NONY FX")==0 && GameSavePrefs.GetInt("TutorialProgress")==0,"existing money-only save recovers matching start purchases and lesson state too");
        Check(GameSavePrefs.Values.Exists(v=>v.key=="CCoins.CareerRewardId" && v.text=="keep-identity") && partial.values.Exists(v=>v.key=="OwnedEquipment.NONY FX"),"legacy normalization retains reward identity and does not rewrite source on load");
        m.SaveCheckpoint();m.StartGame(partial,false);Check(GameSavePrefs.GetInt("OwnedEquipment.NONY FX")==0,"recovered production checkpoint persists through the existing transport");
        m.StartGame(legacy,false);
        m.SaveCheckpoint();Check(Money(legacy)==9000,"recovered legacy budget persists through existing checkpoint transport");
        Set("PlayerMoney",1234);m.SaveBudgetCheckpoint();m.StartGame(legacy,false);
        Check(GameSavePrefs.GetInt("PlayerMoney")==1234,"explicit budget checkpoint supersedes an older retry snapshot");
        var mismatch=m.Repository.Create("Old snapshot different contract",new List<GameSaveValue>{new GameSaveValue{key="CurrentLevel",integer=2},new GameSaveValue{key="PlayerMoney",integer=700},new GameSaveValue{key="BudgetRetry.Start.v1",kind=2,text=UnityEngine.JsonUtility.ToJson(start)}});
        m.StartGame(mismatch,false);Check(GameSavePrefs.GetInt("PlayerMoney")==700,"different-contract legacy snapshot cannot grant a refund");
        var noSnapshot=m.Repository.Create("No known start",new List<GameSaveValue>{new GameSaveValue{key="PlayerMoney",integer=700}});
        m.StartGame(noSnapshot,false);Check(GameSavePrefs.GetInt("PlayerMoney")==700,"legacy balance without recovery snapshot is preserved, not guessed");
        type.GetField("level").SetValue(start,5);
        var complete=m.Repository.Create("Finished campaign",new List<GameSaveValue>{new GameSaveValue{key="CurrentLevel",integer=5},new GameSaveValue{key="CampaignCompleted",integer=1},new GameSaveValue{key="PlayerMoney",integer=700},new GameSaveValue{key="BudgetRetry.Start.v1",kind=2,text=UnityEngine.JsonUtility.ToJson(start)}});
        m.StartGame(complete,false);Check(GameSavePrefs.GetInt("PlayerMoney")==700,"finished campaign does not roll back to last-contract start");
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
public class GameSaveValue { public string key,text; public int kind,integer; public float number; }
public class GameSaveRepository {
    static Dictionary<string,List<GameSaveSlot>> storage = new Dictionary<string,List<GameSaveSlot>>(); public static int ImportFailures;
    public static void Reset(){storage.Clear();ImportFailures=0;}
    public string Owner,Warning; public int Commits; public List<GameSaveSlot> Slots;
    public GameSaveRepository(string root,string owner){Owner=owner;if(!storage.ContainsKey(owner))storage[owner]=new List<GameSaveSlot>();Slots=storage[owner];}
    public GameSaveSlot Create(string name,List<GameSaveValue> values=null){var s=new GameSaveSlot{name=name,owner=Owner,values=values??new List<GameSaveValue>()};Slots.Add(s);return s;}
    public void ImportUnclaimedGuestSaves(GameSaveRepository r){if(ImportFailures>0){ImportFailures--;throw new Exception("simulated local import failure");}foreach(var source in r.Slots){if(source.linkedAccount!=null)continue;if(!Slots.Exists(s=>s.id==source.id)){var copy=Create(source.name,Clone(source.values));copy.id=source.id;}source.linkedAccount=Owner;}} public void Commit(GameSaveSlot s){Commits++;s.revision="changed";}
    public void Write(GameSaveSlot s){} public void MergeCloud(GameSaveSlot s,string id){s.cloudRevision=s.revision;Slots.Add(s);}
    public static List<GameSaveValue> Clone(List<GameSaveValue> v)=>v.ConvertAll(x=>new GameSaveValue{key=x.key,kind=x.kind,integer=x.integer,number=x.number,text=x.text});
}
public static class GameSavePrefs {
    public static List<GameSaveValue> Values; public static bool IsRoomSession;
    public static void Activate(GameSaveSlot s){Values=s==null?null:GameSaveRepository.Clone(s.values);}
    public static int GetInt(string key,int fallback=0)=>Values?.Find(v=>v.key==key && v.kind==0)?.integer ?? fallback;
    public static void SetInt(string key,int value){Values.RemoveAll(v=>v.key==key);Values.Add(new GameSaveValue{key=key,integer=value});}
}
public static class AccountProfileData { public static int Refreshes; public static void Bind(string id){} public static void EnsureBound(){} public static void Refresh(){Refreshes++;} }
public static class CareerProfileProgress { public static bool RetainOnContractRetry(string key)=>key=="Profile.Career.v1" || key=="Profile.CareerOrigin.v1" || key.StartsWith("AchivDone_",StringComparison.Ordinal) || key.StartsWith("AchivProg_",StringComparison.Ordinal); }
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
