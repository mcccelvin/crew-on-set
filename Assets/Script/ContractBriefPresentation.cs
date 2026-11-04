using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared presentation only. Solo signing/career logic and multiplayer room
// commands remain with their own controllers.
public static class ContractBriefPresentation
{
    public static void ConfigureCanvas(Canvas canvas)
    {
        if(canvas==null)return;
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=canvas.GetComponent<CanvasScaler>()??canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);
        scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight=.5f;
    }
    public static void Apply(Image book,int level,ScrollRect scroll,TMP_Text body,bool expanded)
    {
        if(book==null || scroll==null || body==null)return;
        string[] art={"contractArtisanOpen","contractGokeOpen","contractTerrariOpen","contractKapeOpen","contractHarayaOpen"};
        string key=art[Mathf.Clamp(level,1,5)-1];bool fullCanvasArt=ExportUIArt.Get(key)!=null;
        ExportUIArt.Apply(book,fullCanvasArt?key:"psdOpenFolder");
        Centre(book.rectTransform,new Vector2(20,0),fullCanvasArt?new Vector2(1920,1080):new Vector2(1346,860));
        foreach(string name in new[]{"Project title tape","Project title","Reference photo frame","Product photo","Product photo label"})
        {
            var decoration=book.transform.Find(name);
            if(decoration!=null)decoration.gameObject.SetActive(!fullCanvasArt);
        }
        // Coordinates are the same page bounds used by BuildQualificationsPanel.
        Centre(scroll.viewport,new Vector2(294,8),new Vector2(530,740));
        var mask=scroll.viewport.GetComponent<RectMask2D>()??scroll.viewport.gameObject.AddComponent<RectMask2D>();
        mask.enabled=true;
        var paper=scroll.viewport.GetComponent<Image>()??scroll.viewport.gameObject.AddComponent<Image>();
        CrewPaperStyle.Round(paper);paper.color=new Color32(255,249,231,247);paper.raycastTarget=true;
        scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
        var toolbar=book.transform.Find("Brief view controls");if(toolbar!=null)toolbar.gameObject.SetActive(false);
        var font=TMP_Settings.defaultFontAsset??Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF");
        if(font!=null){body.font=font;body.fontSharedMaterial=font.material;}
        body.color=new Color32(48,32,24,255);body.fontStyle=FontStyles.Normal;
        body.fontSize=expanded?23:25;body.enableAutoSizing=false;body.richText=true;body.enableWordWrapping=true;
        body.alignment=TextAlignmentOptions.TopLeft;body.characterSpacing=0;body.lineSpacing=expanded?4:6;
        body.margin=new Vector4(16,12,20,12);body.maskable=true;body.raycastTarget=false;
        ConfigureCanvas(book.GetComponentInParent<Canvas>());
    }
    private static void Centre(RectTransform rect,Vector2 position,Vector2 size)
    {
        rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;
        rect.localScale=Vector3.one;rect.localRotation=Quaternion.identity;
        rect.anchoredPosition=position;rect.sizeDelta=size;
    }
}
