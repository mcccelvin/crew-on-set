// Runs the real single-career adapter and file repository. The SDK and Unity
// serializer are fixtures; no real accounts, credentials or saves are accessed.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using PlayFab;
using PlayFab.ClientModels;

static class SingleCareerCloudChecks
{
    static int checks;
    static object Call(GameSaveManager m,string method,params object[] args)
    {
        try{return typeof(GameSaveManager).GetMethod(method,BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic).Invoke(m,args);}
        catch(TargetInvocationException e){throw e.InnerException;}
    }
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
    static void Reject(Action action,string name)
    {bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Check(rejected,name);}
    static GameSaveValue I(string key,int value)=>new GameSaveValue{key=key,integer=value};
    static GameSaveValue S(string key,string value)=>new GameSaveValue{key=key,kind=2,text=value};
    static GameSaveSlot Ensure(GameSaveManager m,bool create)=>(GameSaveSlot)Call(m,"EnsurePrimaryCareer",m.Repository,create);
    static Dictionary<string,object> Json(string json)=>(Dictionary<string,object>)new JavaScriptSerializer().DeserializeObject(json);
    static void Publish(GameSaveManager m)
    {
        foreach(var slot in m.Repository.Slots){slot.cloudRevision=slot.revision;m.Repository.Write(slot);}
        m.Syncing=true;Call(m,"PublishCareerProfile",m.Repository,m.session);
    }
    static void Main()
    {
        var path=Path.Combine(Path.GetTempPath(),"crew-single-career-checks-"+Guid.NewGuid().ToString("N"));
        var repo=new GameSaveRepository(path,"account-A");var m=new GameSaveManager{Repository=repo};
        Check(Ensure(m,false)==null && repo.Slots.Count==0,"opening an empty catalogue doesn't create a career");
        var old=repo.Create("Existing career",new List<GameSaveValue>{I("CurrentLevel",3),I("PlayerMoney",4200),
            I("FlowerContractGraded",1),I("GokeContractGraded",1),S("CCoins.CareerRewardId","keep-reward-id"),
            S("Unknown.Existing.Value","keep-this"),I("Knowledge_Composition",1)});
        string oldJson=UnityEngine.JsonUtility.ToJson(old);
        var primary=Ensure(m,false);
        Check(primary.id==GameSaveManager.PrimaryCareerId("account-A"),"migration uses the account's fixed identity");
        Check(repo.Slots.Count==2 && UnityEngine.JsonUtility.ToJson(old)==oldJson,"migration retains original checkpoint unchanged");
        Check(primary.Int("PlayerMoney",0)==4200 && primary.Int("GokeContractGraded",0)==1,"migration retains budget and completion");
        Check(primary.values.Find(v=>v.key=="CCoins.CareerRewardId").text=="keep-reward-id","migration retains reward identity");
        Check(primary.values.Find(v=>v.key=="Unknown.Existing.Value").text=="keep-this","migration retains unknown fields");
        Check(primary.values.Find(v=>v.key==GameSaveRepository.ProfileOriginKey).text==old.values.Find(v=>v.key==GameSaveRepository.ProfileOriginKey).text,"lifetime history retains its deduplication origin");
        for(int i=0;i<10;i++)Check(Ensure(m,true)==primary && repo.Slots.Count==2,"repeated create/replay requests reuse the primary career");
        var reloaded=new GameSaveManager{Repository=new GameSaveRepository(path,"account-A")};
        Check(Ensure(reloaded,false).id==primary.id && reloaded.Repository.Slots.Count==2,"file reload retains the same primary career");
        Check(GameSaveManager.PrimaryCareerId("account-A")!=GameSaveManager.PrimaryCareerId("account-B"),"accounts have separate identities");
        var empty=new GameSaveManager{Repository=new GameSaveRepository(path,"account-B")};
        var fresh=Ensure(empty,true);Check(fresh.id==GameSaveManager.PrimaryCareerId("account-B") && fresh.Int("PlayerMoney",0)==0,"fresh career has no invented funds");

        PlayFabClientAPI.Reset();PlayFabClientAPI.Data["unrelated-key"]="preserve";Publish(m);
        var profile=Json(PlayFabClientAPI.LastWrite.Data[GameSaveManager.CareerProfileCloudKey]);
        Check((string)profile["saveModel"]=="single_career" && (bool)profile["replayableContracts"],"PlayFab payload declares one replayable career");
        Check((string)profile["careerId"]==primary.id && Convert.ToInt32(profile["contractCount"])==5,"profile identifies the canonical checkpoint and contracts");
        Check(PlayFabClientAPI.LastWrite.Permission==UserDataPermission.Private && PlayFabClientAPI.LastWrite.Data.Count==2,"profile and checkpoint publish privately together");
        Check(m.Syncing && m.Completed==0,"write submission alone is not reported as verified");
        PlayFabClientAPI.AckWrite();Check(m.Completed==0 && PlayFabClientAPI.Reads==1,"write acknowledgement requires a fresh read");
        PlayFabClientAPI.Reply();Check(m.Completed==1 && !m.Syncing,"matching read-back completes synchronization");
        Check(PlayFabClientAPI.Data["unrelated-key"]=="preserve","unrelated PlayFab data is preserved");
        int writes=PlayFabClientAPI.Writes;Publish(m);Check(PlayFabClientAPI.Writes==writes,"unchanged verified profile avoids another write");

        var cloud=PlayFabClientAPI.Response();
        string published=cloud.Data[GameSaveManager.CareerProfileCloudKey].Value;
        repo.Commit(primary);cloud.Data["CrewCareer_v1_"+primary.id].Value=UnityEngine.JsonUtility.ToJson(primary);
        Check(Call(null,"ReadCareerProfile",cloud.Data,"account-A",false)!=null,"an interrupted checkpoint/profile update can recover");
        Reject(()=>Call(null,"ReadCareerProfile",cloud.Data,"account-A",true),"read-back rejects a mismatched checkpoint revision");
        var bad=Json(published);bad["version"]=99;cloud.Data[GameSaveManager.CareerProfileCloudKey].Value=new JavaScriptSerializer().Serialize(bad);
        Reject(()=>Call(null,"ReadCareerProfile",cloud.Data,"account-A",false),"future schema is not overwritten");
        cloud.Data[GameSaveManager.CareerProfileCloudKey].Value=published;
        Reject(()=>Call(null,"ReadCareerProfile",cloud.Data,"account-B",false),"another account's primary profile is rejected");
        cloud.Data.Remove("CrewCareer_v1_"+primary.id);
        Reject(()=>Call(null,"ReadCareerProfile",cloud.Data,"account-A",false),"missing cloud checkpoint is not replaced by an empty career");

        var failed=new GameSaveManager{Repository=repo};PlayFabClientAPI.Reset();Publish(failed);PlayFabClientAPI.AckWrite();
        PlayFabClientAPI.Data.Remove(GameSaveManager.CareerProfileCloudKey);PlayFabClientAPI.Reply();
        Check(!failed.Syncing && failed.Completed==0 && primary.Int("PlayerMoney",0)==4200,"failed verification retains local progress and schedules retry");
        var switched=new GameSaveManager{Repository=repo};PlayFabClientAPI.Reset();Publish(switched);
        switched.session++;switched.Repository=empty.Repository;PlayFabClientAPI.AckWrite();
        Check(PlayFabClientAPI.Reads==0 && switched.Completed==0,"stale account callback cannot verify or modify another account");

        var live=repo.Create("Older running session",new List<GameSaveValue>{I("CurrentLevel",4),I("PlayerMoney",3100)});
        m.Active=live;string before=UnityEngine.JsonUtility.ToJson(primary);Ensure(m,false);
        Check(m.Active.id==primary.id && m.Active.Int("PlayerMoney",0)==3100,"an already-running legacy career moves onto the stable identity");
        Check(repo.Slots.Exists(s=>s.name=="Career migration backup" && s.Int("PlayerMoney",0)==4200),"previous primary checkpoint remains recoverable as a backup");
        Console.WriteLine("Single-career checks passed: "+checks+". Fake SDK; temporary files only.");
    }
}

public sealed partial class GameSaveManager
{
    public GameSaveRepository Repository;public GameSaveSlot Active;public bool Syncing;
    public string Status;public int session=1,Completed,PendingUpload;
    public event Action Changed;
    private const string CloudPrefix="CrewCareer_v1_";
    private float nextProductionSync,nextSync;
    public void OpenRepository(){}
    public static int UnlockedContractLevel(GameSaveSlot slot)=>slot.Level;
    private bool IsCurrentSync(GameSaveRepository repo,int generation)=>Repository==repo && session==generation;
    private void RecoverProductionLogs(){}
    private void SyncFailed(string message){Syncing=false;Status=message;}
    private void CloudRequestFailed(PlayFabError error,string message)=>SyncFailed(message);
    private void UploadNext(GameSaveRepository repo,int generation)
    {
        if(repo.Slots.Any(s=>s.cloudRevision!=s.revision)){PendingUpload++;return;}
        Completed++;PublishCareerProfile(repo,generation);
    }
}
public static class GameSavePrefs
{
    public static bool IsRoomSession;
    public static bool IsGlobal(string key)=>key.StartsWith("Options.")||key.StartsWith("SaveSystem.")||key=="PlayFabId"||key=="PlayerName";
}
namespace UnityEngine
{
    public static class Time {public static float unscaledTime=1;}
    public static class Mathf {public static int Clamp(int n,int min,int max)=>Math.Max(min,Math.Min(max,n));}
    public static class JsonUtility
    {
        static readonly JavaScriptSerializer Serializer=new JavaScriptSerializer{MaxJsonLength=int.MaxValue};
        public static string ToJson(object value)=>Serializer.Serialize(value);
        public static T FromJson<T>(string json)=>(T)ConvertValue(typeof(T),Serializer.DeserializeObject(json));
        static object ConvertValue(Type type,object value)
        {
            if(value==null)return null;
            if(type==typeof(string)||type.IsPrimitive||type.IsEnum)return System.Convert.ChangeType(value,type);
            var listType=type.IsGenericType&&type.GetGenericTypeDefinition()==typeof(List<>);
            if(listType){var list=(IList)Activator.CreateInstance(type);foreach(var entry in (IEnumerable)value)list.Add(ConvertValue(type.GetGenericArguments()[0],entry));return list;}
            var instance=Activator.CreateInstance(type,true);var data=(Dictionary<string,object>)value;
            foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.Instance))
                if(data.TryGetValue(field.Name,out var entry))field.SetValue(instance,ConvertValue(field.FieldType,entry));
            return instance;
        }
    }
}
namespace PlayFab
{
    public class PlayFabError{}
    public static class PlayFabClientAPI
    {
        public static readonly Dictionary<string,string> Data=new Dictionary<string,string>();
        public static UpdateUserDataRequest LastWrite;public static int Writes,Reads;
        static Action<UpdateUserDataResult> write;static Action<GetUserDataResult> read;static List<string> keys;
        public static void Reset(){Data.Clear();Writes=Reads=0;write=null;read=null;}
        public static void UpdateUserData(UpdateUserDataRequest request,Action<UpdateUserDataResult> ok,Action<PlayFabError> fail)
        {LastWrite=request;Writes++;write=ok;}
        public static void GetUserData(GetUserDataRequest request,Action<GetUserDataResult> ok,Action<PlayFabError> fail)
        {Reads++;keys=request.Keys;read=ok;}
        public static void AckWrite(){foreach(var pair in LastWrite.Data)Data[pair.Key]=pair.Value;var callback=write;write=null;callback(new UpdateUserDataResult());}
        public static GetUserDataResult Response()=>new GetUserDataResult{Data=Data.Where(p=>keys==null||keys.Contains(p.Key)).ToDictionary(p=>p.Key,p=>new UserDataRecord{Value=p.Value})};
        public static void Reply(){var callback=read;read=null;callback(Response());}
    }
}
namespace PlayFab.ClientModels
{
    public class GetUserDataRequest {public List<string> Keys;}
    public class GetUserDataResult {public Dictionary<string,UserDataRecord> Data;}
    public class UserDataRecord {public string Value;}
    public class UpdateUserDataRequest {public UserDataPermission Permission;public Dictionary<string,string> Data;}
    public class UpdateUserDataResult{}
    public enum UserDataPermission {Private}
}
