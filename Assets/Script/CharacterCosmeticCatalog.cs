using System;
using System.Collections.Generic;
using UnityEngine;

// Bundled static parts retain their common authored origin and import scale.
// These are profile models, not a replacement for the animated gameplay rig.
public sealed class CharacterCosmeticCatalog : ScriptableObject
{
    [Serializable] public struct Entry { public string key; public GameObject model; }
    public Entry[] entries;
    private readonly Dictionary<string,RenderTexture> thumbnails = new Dictionary<string,RenderTexture>();
    public static CharacterCosmeticCatalog Load() => Resources.Load<CharacterCosmeticCatalog>("CharacterCosmetics");
    public GameObject Model(string key)
    {
        if(entries!=null)foreach(var entry in entries)if(entry.key==key)return entry.model;
        return null;
    }
    public Texture Thumbnail(string id)
    {
        if(thumbnails.TryGetValue(id,out var image) && image!=null)return image;
        var result=Render(new[]{id},false,256,256);
        if(result!=null)thumbnails[id]=result;
        return result;
    }
    public RenderTexture Render(string[] ids,bool mannequin,int width=560,int height=700)
    {
        var keys=new List<string>();
        if(ids!=null)foreach(var id in ids) {var key=CharacterCosmetics.ModelKey(id);if(key!=null&&Model(key)!=null)keys.Add(key);}
        if(keys.Count==0)return null;
        if(mannequin&&!keys.Exists(k=>k.StartsWith("body_")))keys.Insert(0,"body_girl");
        var root=new GameObject("Static customization preview");root.transform.position=new Vector3(18000,18000,18000);
        RenderTexture result=null;
        try
        {
            foreach(var key in keys)
            {
                var prefab=Model(key);if(prefab==null)continue;
                var part=Instantiate(prefab,root.transform,false);
                foreach(var behaviour in part.GetComponentsInChildren<Behaviour>(true))behaviour.enabled=false;
                foreach(var collider in part.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            }
            Bounds bounds=new Bounds();bool found=false;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                renderer.gameObject.layer=31;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);
            }
            if(!found)return null;
            var camera=new GameObject("Cosmetic camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);
            camera.enabled=false;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
            camera.orthographic=true;camera.aspect=(float)width/height;
            camera.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.x/camera.aspect)*1.15f;
            camera.nearClipPlane=.01f;camera.farClipPlane=Mathf.Max(40,bounds.size.magnitude*3);
            camera.transform.position=bounds.center+Vector3.forward*(bounds.extents.z+10);camera.transform.LookAt(bounds.center);
            var light=new GameObject("Cosmetic light").AddComponent<Light>();light.transform.SetParent(root.transform,false);
            light.type=LightType.Directional;light.intensity=1.4f;light.cullingMask=1<<31;light.transform.rotation=Quaternion.Euler(30,160,0);
            result=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);result.Create();camera.targetTexture=result;camera.Render();camera.targetTexture=null;
            return result;
        }
        catch {if(result!=null){result.Release();Release(result);}throw;}
        finally {root.SetActive(false);Release(root);}
    }
    private void OnDisable(){foreach(var image in thumbnails.Values)if(image!=null){image.Release();Release(image);}thumbnails.Clear();}
    private static void Release(UnityEngine.Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
}
