using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public sealed partial class GameSaveManager
{
    public const string CareerProfileCloudKey = "CrewCareerProfile_v1";
    private string observedCareerProfile;

    [Serializable] private sealed class CareerCloudProfile
    {
        public int version;
        public string saveModel;
        public bool replayableContracts;
        public int contractCount;
        public string careerId;
        public string careerRevision;
    }

    // A fixed, account-scoped ID is identical on every device. Replaying a
    // contract, opening an old creation panel or retrying a sync cannot mint a
    // second playable career. Old files/keys remain available as backups.
    public static string PrimaryCareerId(string owner)
    {
        using(var sha=SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes("CrewOnSet.SingleCareer.v1/"+owner)),0,16)
                .Replace("-","").ToLowerInvariant();
    }

    public GameSaveSlot GetPrimaryCareer()
    {
        OpenRepository();
        if(Repository==null)return null;
        // Let the initial download finish before migrating an older local
        // career, so a new device can discover the already-established one.
        return Syncing ? FindPrimaryOrLegacy(Repository) : EnsurePrimaryCareer(Repository,false);
    }

    private GameSaveSlot FindPrimaryOrLegacy(GameSaveRepository repo)
    {
        var primary=repo.Slots.Find(s=>s.id==PrimaryCareerId(repo.Owner));
        if(primary!=null)return primary.Int("SaveDeleted",0)==0?primary:null;
        if(Active!=null && Active.owner==repo.Owner && repo.Slots.Contains(Active) && Active.Int("SaveDeleted",0)==0)return Active;
        return repo.Slots.Where(s=>s.owner==repo.Owner && s.Int("SaveDeleted",0)==0)
            .OrderByDescending(UnlockedContractLevel).ThenByDescending(s=>s.updatedUtc).ThenBy(s=>s.id,StringComparer.Ordinal).FirstOrDefault();
    }

    private GameSaveSlot EnsurePrimaryCareer(GameSaveRepository repo,bool create)
    {
        string id=PrimaryCareerId(repo.Owner);
        var primary=repo.Slots.Find(s=>s.id==id);
        if(primary!=null && primary.Int("SaveDeleted",0)==0)
        {
            if(Active!=null && Active.id!=id && Active.owner==repo.Owner && repo.Slots.Contains(Active) && !GameSavePrefs.IsRoomSession)
            {
                // Login can finish while an older local career is in use.
                // Retain both checkpoints before moving that live session onto
                // the established identity; its working preferences stay live.
                repo.Create("Career migration backup",primary.values);
                var previous=primary.values;
                try {primary.values=GameSaveRepository.Clone(Active.values);repo.Commit(primary);}
                catch {primary.values=previous;throw;}
                Active=primary;
            }
            return primary;
        }
        var source=primary==null?FindPrimaryOrLegacy(repo):null;
        if(source==null && !create)return null;
        var values=source!=null?GameSaveRepository.Clone(source.values):new List<GameSaveValue>();
        var migrated=new GameSaveSlot {id=id,owner=repo.Owner,name="My contracts",values=values};
        repo.Commit(migrated);
        if(primary!=null)repo.Slots.Remove(primary);
        repo.Slots.Add(migrated);
        if(source!=null && Active==source)Active=migrated;
        return migrated;
    }

    private static string CareerProfileJson(GameSaveSlot primary) => JsonUtility.ToJson(new CareerCloudProfile {
        version=1,saveModel="single_career",replayableContracts=true,contractCount=5,
        careerId=primary?.id??"",careerRevision=primary?.revision??"" });

    private static string ReadCareerProfile(Dictionary<string,UserDataRecord> data,string owner,bool requireMatchingRevision=false)
    {
        if(data==null || !data.TryGetValue(CareerProfileCloudKey,out var record))return null;
        var profile=JsonUtility.FromJson<CareerCloudProfile>(record.Value);
        if(profile==null || profile.version!=1 || profile.saveModel!="single_career" || !profile.replayableContracts ||
            profile.contractCount!=5 || (!string.IsNullOrEmpty(profile.careerId) && !Guid.TryParseExact(profile.careerId,"N",out _)))
            throw new InvalidDataException("Unsupported single-career cloud profile.");
        if(!string.IsNullOrEmpty(profile.careerId))
        {
            if(profile.careerId!=PrimaryCareerId(owner) || !data.TryGetValue(CloudPrefix+profile.careerId,out var checkpoint))
                throw new InvalidDataException("The primary cloud checkpoint is missing or belongs to another save model.");
            var remote=JsonUtility.FromJson<GameSaveSlot>(checkpoint.Value);
            GameSaveRepository.Validate(remote,owner);
            if(remote.id!=profile.careerId || requireMatchingRevision && remote.revision!=profile.careerRevision)
                throw new InvalidDataException("The primary cloud checkpoint does not match its profile.");
        }
        else if(!string.IsNullOrEmpty(profile.careerRevision))throw new InvalidDataException("Invalid empty-career cloud profile.");
        return JsonUtility.ToJson(profile);
    }

    private void PublishCareerProfile(GameSaveRepository repo,int requestSession)
    {
        if(!IsCurrentSync(repo,requestSession))return;
        var primary=EnsurePrimaryCareer(repo,false);
        string expected=CareerProfileJson(primary);
        if(observedCareerProfile==expected){CompleteCloudSync(repo);return;}
        var data=new Dictionary<string,string>{{CareerProfileCloudKey,expected}};
        var keys=new List<string>{CareerProfileCloudKey};
        string id=primary?.id,revision=primary?.revision;
        if(primary!=null)
        {
            // Publish the index and the checkpoint it identifies together.
            data[CloudPrefix+id]=JsonUtility.ToJson(primary);
            keys.Add(CloudPrefix+id);
        }
        try
        {
        PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest {Permission=UserDataPermission.Private,Data=data},written=>
        {
            if(!IsCurrentSync(repo,requestSession))return;
            try
            {
            PlayFabClientAPI.GetUserData(new GetUserDataRequest {Keys=keys},read=>
            {
                if(!IsCurrentSync(repo,requestSession))return;
                try
                {
                    if(ReadCareerProfile(read.Data,repo.Owner,true)!=expected)throw new InvalidDataException("Career profile read-back did not match.");
                    if(id!=null)
                    {
                        if(read.Data==null || !read.Data.TryGetValue(CloudPrefix+id,out var checkpoint))
                            throw new InvalidDataException("The primary checkpoint is missing.");
                        var remote=JsonUtility.FromJson<GameSaveSlot>(checkpoint.Value);
                        GameSaveRepository.Validate(remote,repo.Owner);
                        if(remote.id!=id || remote.revision!=revision)throw new InvalidDataException("The primary checkpoint revision did not match.");
                    }
                    observedCareerProfile=expected;
                    // A checkpoint may change while a request is in flight.
                    // Upload the new revision before declaring sync complete.
                    UploadNext(repo,requestSession);
                }
                catch(Exception){SyncFailed("The single-career cloud record could not be verified. Progress is saved locally; retrying automatically.");}
            },error=>{if(IsCurrentSync(repo,requestSession))CloudRequestFailed(error,"Could not verify the single-career cloud record.");});
            }
            catch(Exception){SyncFailed("Could not verify the single-career cloud record. Progress is saved locally; retrying automatically.");}
        },error=>{if(IsCurrentSync(repo,requestSession))CloudRequestFailed(error,"Could not publish the single-career cloud record. Progress is saved locally.");});
        }
        catch(Exception){SyncFailed("Could not publish the single-career cloud record. Progress is saved locally; retrying automatically.");}
    }

    private void CompleteCloudSync(GameSaveRepository repo)
    {
        RecoverProductionLogs();nextProductionSync=Time.unscaledTime;
        Syncing=false;nextSync=Time.unscaledTime+60f;
        Status=repo.Warning??"Single career and replayable contracts synced with PlayFab.";
        Changed?.Invoke();
    }
}
