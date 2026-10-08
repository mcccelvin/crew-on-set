#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CharacterCosmeticRigBaker
{
    [MenuItem("Crew-On-Set/Cosmetics/Rebuild profile clothing rig")]
    public static void Rebuild()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode before rebuilding clothing.");
        Bake("Assets/Resources/CharacterCosmeticRig.asset",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Product/DefaultCharacGirlRig.fbx"),CharacterCosmeticCatalog.Load());
    }
    public static void Bake(string output,GameObject prefab,CharacterCosmeticCatalog catalog)
    {
        if(prefab==null||catalog==null)throw new InvalidOperationException("Character or customization catalog missing.");
        var rig=UnityEngine.Object.Instantiate(prefab);
        var generated=new List<Mesh>();
        try
        {
            foreach(var animator in rig.GetComponentsInChildren<Animator>())animator.enabled=false;
            var body=Array.Find(rig.GetComponentsInChildren<SkinnedMeshRenderer>(),s=>s.name=="girl body");
            if(body==null)throw new InvalidOperationException("Original weighted body is missing.");
            var skeleton=body.bones;var reference=body.sharedMesh.vertices;var weights=body.sharedMesh.boneWeights;
            for(int i=0;i<reference.Length;i++)reference[i]=rig.transform.InverseTransformPoint(body.transform.TransformPoint(reference[i]));
            var bindposes=new Matrix4x4[skeleton.Length];
            // The imported humanoid's current transforms are not necessarily its
            // mesh bind pose. Preserve the original skin's authored bind matrices.
            for(int i=0;i<skeleton.Length;i++)bindposes[i]=body.sharedMesh.bindposes[i]*body.transform.worldToLocalMatrix*rig.transform.localToWorldMatrix;
            int head=Array.FindIndex(skeleton,t=>t.name=="mixamorig:Head");
            var entries=new List<CharacterCosmeticRig.Part>();
            foreach(var item in CharacterCosmetics.Items)
            {
                var source=catalog.Model(CharacterCosmetics.ModelKey(item.id));
                if(source==null)throw new InvalidOperationException("Missing model "+item.id);
                if(source.name!=CharacterCosmetics.ModelKey(item.id))
                    throw new InvalidOperationException("Cosmetic key "+item.id+" points to "+source.name+". Model keys must match their FBX filenames; product names are mapped in CharacterCosmetics.");
                var model=UnityEngine.Object.Instantiate(source);
                try
                {
                    var combines=new List<CombineInstance>();var materials=new List<Material>();
                    foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
                    {
                        var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null)continue;
                        for(int sub=0;sub<filter.sharedMesh.subMeshCount;sub++)
                        {
                            combines.Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=sub,transform=filter.transform.localToWorldMatrix});
                            materials.Add(renderer.sharedMaterials[Mathf.Min(sub,renderer.sharedMaterials.Length-1)]);
                        }
                    }
                    var mesh=new Mesh{name=item.id};mesh.CombineMeshes(combines.ToArray(),false,true);generated.Add(mesh);
                    var vertices=mesh.vertices;var transferred=new BoneWeight[vertices.Length];
                    for(int v=0;v<vertices.Length;v++)
                    {
                        if((item.kind=="hair"||item.kind=="face")&&head>=0){transferred[v]=new BoneWeight{boneIndex0=head,weight0=1};continue;}
                        // All modular models share the base character's authored space.
                        // Transfer from the weighted body at rest, before sampling idle.
                        float closest=float.MaxValue;int nearest=0;
                        for(int b=0;b<reference.Length;b++){float d=(reference[b]-vertices[v]).sqrMagnitude;if(d<closest){closest=d;nearest=b;}}
                        transferred[v]=weights[nearest];
                    }
                    mesh.boneWeights=transferred;mesh.bindposes=bindposes;mesh.RecalculateBounds();
                    entries.Add(new CharacterCosmeticRig.Part{id=item.id,mesh=mesh,materials=materials.ToArray()});
                }
                finally{UnityEngine.Object.DestroyImmediate(model);}
            }
            // Preserve an existing asset's GUID; regenerate only derived mesh subassets.
            var data=AssetDatabase.LoadAssetAtPath<CharacterCosmeticRig>(output);
            if(data==null){data=ScriptableObject.CreateInstance<CharacterCosmeticRig>();AssetDatabase.CreateAsset(data,output);}
            else foreach(var old in AssetDatabase.LoadAllAssetsAtPath(output))if(old is Mesh)UnityEngine.Object.DestroyImmediate(old,true);
            data.bones=Array.ConvertAll(skeleton,t=>t.name);data.parts=entries.ToArray();
            foreach(var mesh in generated)AssetDatabase.AddObjectToAsset(mesh,data);
            EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
        }
        finally{UnityEngine.Object.DestroyImmediate(rig);foreach(var mesh in generated)if(mesh!=null&&!AssetDatabase.Contains(mesh))UnityEngine.Object.DestroyImmediate(mesh);}
    }
}
#endif
