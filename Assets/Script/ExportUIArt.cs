using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// References the original UI-EXPORT textures so builds include the artwork.
public sealed class ExportUIArt : ScriptableObject
{
    [System.Serializable] public struct Entry { public string key; public Texture2D texture; }
    public Entry[] entries;
    static ExportUIArt catalog;
    static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
    public static Sprite Get(string key)
    {
        if(sprites.TryGetValue(key,out var cached)&&cached!=null)return cached;
        if(catalog==null)catalog=Resources.Load<ExportUIArt>("ExportUIArt");
        if(catalog==null)return null;
        foreach(var e in catalog.entries)if(e.key==key && e.texture!=null)
        {
            var sprite=Sprite.Create(e.texture,new Rect(0,0,e.texture.width,e.texture.height),new Vector2(.5f,.5f),100);
            sprite.name="UI-EXPORT/"+key;sprites[key]=sprite;return sprite;
        }
        return null;
    }
    // Make a white version of the original artwork without changing imported assets.
    public static Sprite GetWhite(string key)
    {
        string cacheKey = key + "/white";
        if (sprites.TryGetValue(cacheKey, out var cached) && cached != null) return cached;
        Sprite original = Get(key);
        if (original == null) return null;
        Texture2D source = original.texture;
        RenderTexture previous = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Texture2D white = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            white.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            Color32[] pixels = white.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, pixels[i].a);
            white.SetPixels32(pixels);
            white.Apply();
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
        white.name = cacheKey;
        Sprite sprite = Sprite.Create(white, original.rect, new Vector2(.5f, .5f), original.pixelsPerUnit);
        sprite.name = original.name + "/white";
        sprites[cacheKey] = sprite;
        return sprite;
    }

    public static void Apply(Image image,string key)
    {
        var sprite=Get(key);if(image==null||sprite==null)return;
        image.sprite=sprite;image.type=Image.Type.Simple;image.color=Color.white;
    }
    public static void Decoration(Transform parent,string key)
    {
        if(Get(key)==null)return;
        var o=new GameObject(key,typeof(RectTransform),typeof(Image));o.transform.SetParent(parent,false);
        var r=o.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
        Apply(o.GetComponent<Image>(),key);o.GetComponent<Image>().raycastTarget=false;
    }
    public static void OutlineText(TMPro.TextMeshProUGUI text)
    {
        if(text.font==null)text.font=TMPro.TMP_Settings.defaultFontAsset;
        text.color=Color.white;text.fontStyle=TMPro.FontStyles.Bold;
        if(text.fontSharedMaterial==null)return;
        if(text.fontSharedMaterial!=null)
        {
            var material=new Material(text.fontSharedMaterial);
            var shader=Shader.Find("TextMeshPro/Distance Field");if(shader!=null)material.shader=shader;
            material.EnableKeyword("OUTLINE_ON");
            material.SetColor("_OutlineColor",Color.black);material.SetFloat("_OutlineWidth",.22f);
            text.fontSharedMaterial=material;
        }
        text.UpdateMeshPadding();
    }
}
