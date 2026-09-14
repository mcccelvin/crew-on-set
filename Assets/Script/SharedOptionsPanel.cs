using PlayerPrefs = GameSavePrefs;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public sealed class SharedOptionsPanel
{
    private readonly GameObject root;
    private readonly Transform frame;
    private readonly System.Action closed;
    private GameObject page;
    private readonly Button[] tabs=new Button[3];
    private int section,quality,fps;
    private float sensitivity,volume;
    private bool fullscreen;
    private bool originalFullscreen;
    public bool IsOpen=>root.activeSelf;
    public GameObject Root=>root;

    public SharedOptionsPanel(Transform parent,System.Action onClosed)
    {
        closed=onClosed;
        root=new GameObject("Shared Settings",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(parent,false);
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=250;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var shade=Rect("Shade",root.transform,Vector2.zero,new Vector2(4000,4000));shade.gameObject.AddComponent<Image>().color=new Color(0,0,0,.8f);
        frame=Art("Settings frame",root.transform,"settingsFrame",new Vector2(0,5),new Vector2(1254,823));
        string[] names={"GENERAL","AUDIO","GRAPHICS"};
        for(int i=0;i<3;i++){int index=i;tabs[i]=Button(frame,names[i],"settingsTab",new Vector2(-340+i*340,312),new Vector2(291,103),()=>{section=index;Refresh();});}
        Button(frame,"SAVE","settingsSave",new Vector2(-190,-309),new Vector2(273,110),()=>Close(true));
        Button(frame,"RESET","settingsReset",new Vector2(188,-309),new Vector2(273,110),()=>{sensitivity=1;volume=1;fullscreen=true;quality=Mathf.Min(2,QualitySettings.names.Length-1);fps=60;Refresh();});
        Button(frame,"","close",new Vector2(682,334),new Vector2(85,85),()=>Close(false));
        root.SetActive(false);
    }
    public void Open()
    {
        sensitivity=GameOptions.MouseSensitivityMultiplier;volume=PlayerPrefs.GetFloat("Options.MasterVolume",1);
        fullscreen=PlayerPrefs.GetInt(GameOptions.FullscreenKey,Screen.fullScreen?1:0)==1;
        originalFullscreen=fullscreen;
        quality=Mathf.Clamp(PlayerPrefs.GetInt("Options.Quality",QualitySettings.GetQualityLevel()),0,QualitySettings.names.Length-1);
        fps=PlayerPrefs.GetInt("Options.FPS",60);section=0;root.SetActive(true);Refresh();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    }
    public void Close(bool save)
    {
        if(!IsOpen)return;
        if(save)
        {
            PlayerPrefs.SetFloat(GameOptions.SensitivityKey,sensitivity);PlayerPrefs.SetFloat("Options.MasterVolume",volume);
            PlayerPrefs.SetInt(GameOptions.FullscreenKey,fullscreen?1:0);PlayerPrefs.SetInt("Options.Quality",quality);PlayerPrefs.SetInt("Options.FPS",fps);PlayerPrefs.Save();
            AudioListener.volume=volume;GameOptions.ApplyFullscreen(fullscreen);QualitySettings.SetQualityLevel(quality);QualitySettings.vSyncCount=0;Application.targetFrameRate=fps;
        }
        if(!save&&fullscreen!=originalFullscreen)GameOptions.ApplyFullscreen(originalFullscreen);
        root.SetActive(false);if(UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);closed?.Invoke();
    }
    private void Refresh()
    {
        if(page!=null){page.SetActive(false);Object.Destroy(page);}
        page=Rect("Settings page",frame,Vector2.zero,Vector2.zero).gameObject;
        for(int i=0;i<tabs.Length;i++)
        {
            var image=tabs[i].GetComponent<Image>();ExportUIArt.Apply(image,i==section?"settingsTabSelected":"settingsTab");
            image.color=i==section?Color.white:new Color(.65f,.65f,.65f);
        }
        if(section==0)
        {
            Slider("MOUSE SENSITIVITY",168,Mathf.Log10(GameOptions.MinimumMouseSensitivity),Mathf.Log10(GameOptions.MaximumMouseSensitivity),Mathf.Log10(sensitivity),v=>sensitivity=Mathf.Pow(10,v),v=>Mathf.Pow(10,v).ToString("0.00")+"x");
            Label(page.transform,"CONTROLS",new Vector2(-305,74),new Vector2(440,60),32,true);
            Button(page.transform,"VIEW CONTROLS","",new Vector2(270,74),new Vector2(430,54),()=>{
                if(page.transform.Find("Controls help")!=null)return;
                Label(page.transform,"WASD — MOVE    •    MOUSE — LOOK\nE — INTERACT    •    G — DROP EQUIPMENT\nP — ALMANAC    •    TAB — CONTRACT\nESC — PAUSE / BACK",new Vector2(0,-88),new Vector2(1020,170),26,false).name="Controls help";
            });
        }
        else if(section==1)
        {
            Slider("MASTER VOLUME",168,0,1,volume,v=>volume=v,v=>Mathf.RoundToInt(v*100)+"%");
            Label(page.transform,"Controls all game audio.\nPress SAVE to apply your changes.",new Vector2(0,-35),new Vector2(950,150),28,false);
        }
        else
        {
            Choice("FULLSCREEN",168,fullscreen?"ON":"OFF",()=>{fullscreen=!fullscreen;GameOptions.ApplyFullscreen(fullscreen);Refresh();});
            Choice("QUALITY",74,QualitySettings.names[quality],()=>{quality=(quality+1)%QualitySettings.names.Length;Refresh();});
            Choice("FRAME RATE",-20,fps<0?"UNLIMITED":fps+" FPS",()=>{fps=fps==30?60:fps==60?120:fps==120?-1:30;Refresh();});
            Label(page.transform,Application.isEditor?"Fullscreen changes apply in the built game.":"Press SAVE to apply display changes.",new Vector2(0,-157),new Vector2(1000,60),23,false);
        }
    }
    private void Choice(string name,float y,string value,System.Action change)
    {
        Label(page.transform,name,new Vector2(-305,y),new Vector2(440,60),32,true);
        Button(page.transform,value+"  >","",new Vector2(270,y),new Vector2(480,54),change);
    }
    private void Slider(string name,float y,float min,float max,float value,System.Action<float> changed,System.Func<float,string> format)
    {
        Label(page.transform,name,new Vector2(-305,y),new Vector2(440,60),32,true);
        var area=Rect(name,page.transform,new Vector2(270,y),new Vector2(485,54));area.gameObject.AddComponent<Image>().color=Color.clear;
        Art("Track",area,"settingsTrack",Vector2.zero,new Vector2(485,28));
        var travel=Rect("Handle travel",area,Vector2.zero,new Vector2(395,54));
        var handle=Art("Handle",travel,"settingsKnob",Vector2.zero,new Vector2(95,54));
        handle.sizeDelta=new Vector2(95,0); // Slider stretches the handle vertically across its travel area.
        var slider=area.gameObject.AddComponent<Slider>();slider.minValue=min;slider.maxValue=max;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();
        var readout=Label(page.transform,format(value),new Vector2(270,y-43),new Vector2(450,35),22,false);
        slider.SetValueWithoutNotify(value);slider.onValueChanged.AddListener(v=>{changed(v);readout.text=format(v);});
    }
    private static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=Vector2.one*.5f;r.anchoredPosition=pos;r.sizeDelta=size;return r;
    }
    private static RectTransform Art(string name,Transform parent,string key,Vector2 pos,Vector2 size)
    {
        var r=Rect(name,parent,pos,size);var image=r.gameObject.AddComponent<Image>();ExportUIArt.Apply(image,key);image.raycastTarget=false;return r;
    }
    private static TextMeshProUGUI Label(Transform parent,string value,Vector2 pos,Vector2 size,float font,bool outline)
    {
        var t=Rect(value,parent,pos,size).gameObject.AddComponent<TextMeshProUGUI>();t.text=value;t.fontSize=font;t.alignment=TextAlignmentOptions.Center;t.color=Color.black;t.raycastTarget=false;t.enableAutoSizing=true;t.fontSizeMin=18;t.fontSizeMax=font;if(outline)ExportUIArt.OutlineText(t);return t;
    }
    private static Button Button(Transform parent,string text,string art,Vector2 pos,Vector2 size,System.Action action)
    {
        var r=Rect(text,parent,pos,size);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.64f,.63f,.58f);if(!string.IsNullOrEmpty(art))ExportUIArt.Apply(image,art);
        var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>action());
        Label(r,text,Vector2.zero,size-new Vector2(20,8),art=="settingsSave"||art=="settingsReset"?44:32,true);return button;
    }
}
