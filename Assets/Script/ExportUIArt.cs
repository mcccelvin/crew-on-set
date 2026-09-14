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
        text.color=Color.white;text.fontStyle=TMPro.FontStyles.Bold;
        if(text.fontSharedMaterial!=null)
        {
            var material=new Material(text.fontSharedMaterial);
            var shader=Shader.Find("TextMeshPro/Distance Field");if(shader!=null)material.shader=shader;
            material.EnableKeyword("OUTLINE_ON");text.fontMaterial=material;
        }
        text.outlineColor=Color.black;text.outlineWidth=.22f;text.UpdateMeshPadding();
    }
}
