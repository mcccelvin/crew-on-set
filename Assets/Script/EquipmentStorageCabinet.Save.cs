using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Player.Equipment;
using Gear = Player.Equipment.Equipment;

public sealed partial class EquipmentStorageCabinet
{
    public const string SaveKey="Equipment.Cabinet.v1";
    [Serializable] private sealed class Manifest { public List<SavedGear> items=new List<SavedGear>(); }
    [Serializable] private sealed class SavedGear
    {
        public string type,name,file;
        public bool recorded;
        public float duration,intensity,kelvin,diffusion;
        public List<FootageData> recordings;
    }
    private Manifest pending;

    private void SaveContents()
    {
        if(Room)return;
        var data=new Manifest();
        foreach(var item in stored.Where(item=>item!=null)) data.items.Add(Describe(item));
        // Unresolved entries remain intact if a lesson has not restored that model yet.
        if(pending?.items!=null)data.items.AddRange(pending.items);
        GameSavePrefs.SetString(SaveKey,JsonUtility.ToJson(data));GameSavePrefs.Save();
        // This state follows the existing production checkpoint; it does not
        // turn an unfinished purchase into free owned equipment on Continue.
    }

    private static SavedGear Describe(Gear item)
    {
            var record=new SavedGear{type=item.GetType().FullName,name=item.EquipmentName};
            if(item is SDCardItem card)
            {
                record.recorded=card.isUsedCard;record.file=card.recordedFileName;
                record.duration=card.videoDuration;record.recordings=card.recordings;
            }
            if(item is FilmLightItem light)
            {record.intensity=light.intensityPercent;record.kelvin=light.colorTemperature;record.diffusion=light.diffusionPercent;}
        return record;
    }

    // The studio unloads before grading. Stage a manifest for this attempt,
    // then commit it only when the player actually passes the contract.
    private static string packedManifest;
    private static int packedLevel;
    private static GameSaveSlot packedCareer;

    public static void PackForReview()
    {
        packedManifest=null;
        if(Active==null || Active.Room || GameSaveManager.IsTrainingRoom)return;
        var data=new Manifest();
        foreach(var item in FindObjectsOfType<Gear>(true))
        {
            if(item.gameObject.scene!=Active.gameObject.scene || item is ProductionKit kit && kit.template)continue;
            bool inInventory=item.GetComponentInParent<Player.Interactor.EquipmentInteractor>(true)!=null;
            bool loadedCard=item is SDCardItem && (item.GetComponentInParent<FilmCameraItem>(true)!=null || item.GetComponentInParent<ComputerStation>(true)!=null);
            if(!item.gameObject.activeInHierarchy && !Active.stored.Contains(item) && !inInventory && !loadedCard)continue;
            data.items.Add(Describe(item));
        }
        if(Active.pending?.items!=null)data.items.AddRange(Active.pending.items);
        packedLevel=CampaignProgression.GetCurrentLevel();
        packedCareer=GameSaveManager.Instance?.Active;
        packedManifest=JsonUtility.ToJson(data);
    }

    public static void CommitPackedGear(int level)
    {
        if(packedManifest!=null && level==packedLevel && packedCareer==GameSaveManager.Instance?.Active)
            GameSavePrefs.SetString(SaveKey,packedManifest);
        packedManifest=null;
    }
    private IEnumerator Start()
    {
        yield return null;yield return null;
        if(Room)yield break;
        string json=GameSavePrefs.GetString(SaveKey,"");
        if(string.IsNullOrEmpty(json))yield break;
        try{pending=JsonUtility.FromJson<Manifest>(json);}
        catch(ArgumentException error){Debug.LogWarning("Unable to read equipment storage: "+error.Message);yield break;}
        if(pending?.items==null)yield break;
        float until=Time.realtimeSinceStartup+8;
        while(pending.items.Count>0 && Time.realtimeSinceStartup<until)
        {
            RestoreContents();
            yield return new WaitForSecondsRealtime(.5f);
        }
    }

    private void RestoreContents()
    {
        var scene=gameObject.scene;
        var candidates=FindObjectsOfType<Gear>(true).Where(item=>item.gameObject.scene==scene &&
            !stored.Contains(item) && item.GetComponentInParent<Player.Interactor.EquipmentInteractor>()==null &&
            !(item is ProductionKit kit && kit.template)).ToList();
        foreach(var record in pending.items.ToArray())
        {
            if(record==null || string.IsNullOrEmpty(record.type) || string.IsNullOrEmpty(record.name))
            {pending.items.Remove(record);continue;}
            var item=candidates.FirstOrDefault(candidate=>candidate.GetType().FullName==record.type &&
                candidate.EquipmentName==record.name && (!(candidate is SDCardItem card) ||
                !card.isUsedCard || card.recordedFileName==record.file));
            // Cards are physical recording media and are not recreated by the
            // normal owned-equipment restorer. Rehydrate only a saved cabinet card.
            if(item==null && record.type==typeof(SDCardItem).FullName)
            {
                var shop=FindObjectsOfType<ShopTerminal>(true).FirstOrDefault(s=>s.gameObject.scene==scene);
                var prefab=shop?.availableItems.FirstOrDefault(entry=>entry?.prefabToSpawn!=null &&
                    entry.prefabToSpawn.GetComponent<SDCardItem>()!=null)?.prefabToSpawn;
                if(prefab!=null)item=Instantiate(prefab,transform).GetComponent<Gear>();
            }
            if(item==null && record.type==typeof(FilmLightItem).FullName && record.name=="Level 3 Soft Light")
            {
                var shop=FindObjectsOfType<ShopTerminal>(true).FirstOrDefault(s=>s.gameObject.scene==scene);
                if(shop!=null)item=shop.CreateStoredSoftLight(transform);
            }
            if(item==null)continue;
            if(item is SDCardItem sd)
            {sd.isUsedCard=record.recorded;sd.recordedFileName=record.file;sd.videoDuration=record.duration;sd.recordings=record.recordings??new List<FootageData>();}
            if(item is FilmLightItem light)
            {light.intensityPercent=record.intensity;light.colorTemperature=record.kelvin;light.diffusionPercent=record.diffusion;}
            candidates.Remove(item);stored.Add(item);item.transform.SetParent(transform,true);item.gameObject.SetActive(false);
            pending.items.Remove(record);
        }
        if(IsOpen)RefreshContents();
    }
}
