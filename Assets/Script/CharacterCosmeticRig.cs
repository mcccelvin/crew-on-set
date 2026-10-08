using System;
using System.Collections.Generic;
using UnityEngine;

// Pre-baked clothing weights for the existing profile character. Imported FBXs
// stay unchanged; the preview uses the same skeleton and idle as the base look.
public sealed class CharacterCosmeticRig : ScriptableObject
{
    [Serializable] public sealed class Part
    {
        public string id;
        public Mesh mesh;
        public Material[] materials;
    }
    public string[] bones;
    public Part[] parts;
    public static CharacterCosmeticRig Load()=>Resources.Load<CharacterCosmeticRig>("CharacterCosmeticRig");
    public bool Contains(string id)=>parts!=null && Array.Exists(parts,p=>p!=null && p.id==id && p.mesh!=null);
    public static bool Replaces(string kind,string name)
    {
        switch(kind)
        {
            case "body":return name=="girl body";
            case "shirt":return name=="shirt1";
            case "pants":return name=="pant1";
            case "shoe":return name=="basic shoes";
            case "hair":return name=="hair1";
            case "accessory":return name=="glasses";
            case "face":return name=="brows" || name=="eye right.001" || name=="mouth.001";
            default:return false;
        }
    }
    public int Apply(GameObject character,IEnumerable<string> selection)
    {
        if(character==null || bones==null || parts==null || selection==null)return 0;
        var transforms=new Dictionary<string,Transform>();
        foreach(var t in character.GetComponentsInChildren<Transform>(true))transforms[t.name]=t;
        var mapped=new Transform[bones.Length];
        for(int i=0;i<bones.Length;i++)if(!transforms.TryGetValue(bones[i],out mapped[i]))return 0;
        var originals=character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        // The imported FBX supplies the skeleton only. Render explicitly selected parts.
        foreach(var original in originals)original.enabled=false;
        var slots=new Dictionary<string,string>();
        foreach(var id in selection){var item=CharacterCosmetics.Find(id);if(item!=null&&Contains(id))slots[item.kind]=id;}
        if(!slots.ContainsKey("body"))return 0;
        foreach(var slot in slots)
        {
            var part=Array.Find(parts,p=>p!=null&&p.id==slot.Value);
            var obj=new GameObject("Wearing "+slot.Value);obj.transform.SetParent(character.transform,false);
            var skin=obj.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=part.mesh;skin.sharedMaterials=part.materials;
            skin.bones=mapped;skin.rootBone=character.transform;skin.localBounds=part.mesh.bounds;skin.updateWhenOffscreen=true;
            foreach(var original in originals)if(Replaces(slot.Key,original.name))original.enabled=false;
        }
        return slots.Count;
    }
}
