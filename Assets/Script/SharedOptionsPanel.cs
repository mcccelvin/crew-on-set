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
    private float sensitivity,volume,sfxVolume,musicVolume;
    private bool fullscreen;
    public bool IsOpen=>root.activeSelf;
    public GameObject Root=>root;

    private SettingsLayout layout;
    public SharedOptionsPanel(Transform parent,System.Action onClosed)
    {
        closed=onClosed;
        var existing=parent.Find("Shared Settings");
        if(existing!=null && existing.TryGetComponent<SettingsLayout>(out layout))
        {
            root=existing.gameObject; frame=layout.frame;
            for(int i=0;i<3;i++) tabs[i]=layout.tabs[i];
        }
        else
        {
            root=new GameObject("Shared Settings",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.transform.SetParent(parent,false);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=250;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var shade=Rect("Shade",root.transform,Vector2.zero,new Vector2(4000,4000));shade.gameObject.AddComponent<Image>().color=new Color(0,0,0,.8f);
            frame=Art("Settings frame",root.transform,"settingsFrame",new Vector2(0,5),new Vector2(1254,823));
            layout=root.AddComponent<SettingsLayout>();layout.frame=frame;
            string[] names={"GENERAL","AUDIO","GRAPHICS"};
            for(int i=0;i<3;i++) tabs[i]=layout.tabs[i]=Button(frame,names[i],"settingsTab",new Vector2(-340+i*340,312),new Vector2(291,103),()=>{});
            layout.save=Button(frame,"SAVE","settingsSave",new Vector2(-190,-309),new Vector2(273,110),()=>{});
            layout.reset=Button(frame,"RESET","settingsReset",new Vector2(188,-309),new Vector2(273,110),()=>{});
            layout.close=Button(frame,"Close","close",new Vector2(682,334),new Vector2(85,85),()=>{});
            layout.close.GetComponentInChildren<TMP_Text>().text="";
            for(int i=0;i<3;i++)
            {
                page=Rect(names[i]+" page",frame,Vector2.zero,Vector2.zero).gameObject;layout.pages[i]=page;
                if(i==0)
                {
                    layout.sensitivity=MakeSlider("MOUSE SENSITIVITY",168,Mathf.Log10(GameOptions.MinimumMouseSensitivity),Mathf.Log10(GameOptions.MaximumMouseSensitivity),out layout.sensitivityValue);
                    Label(page.transform,"CONTROLS",new Vector2(-305,74),new Vector2(440,60),32,true);
                    layout.controls=Button(page.transform,"VIEW CONTROLS","",new Vector2(270,74),new Vector2(430,54),()=>{});
                    layout.controlsHelp=Label(page.transform,"WASD — MOVE    •    MOUSE — LOOK\nE — INTERACT    •    G — DROP EQUIPMENT\nP — ALMANAC    •    TAB — CONTRACT\nESC — PAUSE / BACK",new Vector2(0,-88),new Vector2(1020,170),26,false);
                    layout.controlsHelp.name="Controls help";layout.controlsHelp.gameObject.SetActive(false);
                }
                else if(i==1)
                {
                    layout.volume=MakeSlider("MASTER VOLUME",168,0,1,out layout.volumeValue);
                    Label(page.transform,"Controls all game audio.\nPress SAVE to apply your changes.",new Vector2(0,-35),new Vector2(950,150),28,false);
                }
                else
                {
                    string[] options={"FULLSCREEN","QUALITY","FRAME RATE"};
                    for(int j=0;j<3;j++)
                    {
                        Label(page.transform,options[j],new Vector2(-305,168-j*94),new Vector2(440,60),32,true);
                        layout.choices[j]=Button(page.transform,options[j],"",new Vector2(270,168-j*94),new Vector2(480,54),()=>{});
                    }
                    Label(page.transform,"Press SAVE to apply display changes.",new Vector2(0,-157),new Vector2(1000,60),23,false);
                }
            }
        }
        // Upgrade scene-authored panels as well as the runtime fallback, retaining their artwork.
        layout.controlsHelp.text="WASD — MOVE    •    MOUSE — LOOK\nSHIFT — SPRINT    •    CTRL — SILENT SLOW WALK\nSPACE — JUMP    •    E — INTERACT    •    G — DROP\nP — ALMANAC    •    TAB — CONTRACT    •    ESC — BACK";
        page=layout.pages[1];
        foreach(var label in page.GetComponentsInChildren<TMP_Text>(true))
            if(label.text.StartsWith("Controls all game audio."))
            {
                label.text="Press SAVE to apply audio changes.";
                label.rectTransform.anchoredPosition=new Vector2(0,-185);
                label.rectTransform.sizeDelta=new Vector2(950,50);
            }
        if(layout.sfx==null)layout.sfx=MakeSlider("SOUND EFFECTS",58,0,1,out layout.sfxValue);
        if(layout.music==null)layout.music=MakeSlider("MUSIC",-52,0,1,out layout.musicValue);
        for(int i=0;i<3;i++) {int index=i;Bind(tabs[i],()=>{section=index;Refresh();});}
        Bind(layout.save,()=>Close(true));Bind(layout.close,()=>Close(false));
        Bind(layout.reset,()=>{sensitivity=1;volume=1;sfxVolume=1;musicVolume=1;fullscreen=true;quality=1;fps=60;Refresh();});
        Bind(layout.controls,()=>layout.controlsHelp.gameObject.SetActive(true));
        Bind(layout.choices[0],()=>{fullscreen=!fullscreen;Refresh();});
        Bind(layout.choices[1],()=>{quality=(quality+1)%3;Refresh();});
        Bind(layout.choices[2],()=>{fps=fps==30?60:fps==60?120:fps==120?-1:30;Refresh();});
        layout.sensitivity.onValueChanged.RemoveAllListeners();layout.sensitivity.onValueChanged.AddListener(v=>{sensitivity=Mathf.Pow(10,v);layout.sensitivityValue.text=sensitivity.ToString("0.00")+"x";});
        layout.volume.onValueChanged.RemoveAllListeners();layout.volume.onValueChanged.AddListener(v=>{volume=v;layout.volumeValue.text=Mathf.RoundToInt(v*100)+"%";});
        layout.sfx.onValueChanged.RemoveAllListeners();layout.sfx.onValueChanged.AddListener(v=>{sfxVolume=v;layout.sfxValue.text=Mathf.RoundToInt(v*100)+"%";});
        layout.music.onValueChanged.RemoveAllListeners();layout.music.onValueChanged.AddListener(v=>{musicVolume=v;layout.musicValue.text=Mathf.RoundToInt(v*100)+"%";});
        foreach(var p in layout.pages) p.SetActive(false);
        root.SetActive(false);
    }
    private static void Bind(Button button,UnityEngine.Events.UnityAction action)
    {button.onClick.RemoveAllListeners();button.onClick.AddListener(action);}
    private Slider MakeSlider(string name,float y,float min,float max,out TMP_Text readout)
    {
        Label(page.transform,name,new Vector2(-305,y),new Vector2(440,60),32,true);
        var area=Rect(name,page.transform,new Vector2(270,y),new Vector2(485,54));area.gameObject.AddComponent<Image>().color=Color.clear;
        Art("Track",area,"settingsTrack",Vector2.zero,new Vector2(485,28));
        var travel=Rect("Handle travel",area,Vector2.zero,new Vector2(395,54));
        var handle=Art("Handle",travel,"settingsKnob",Vector2.zero,new Vector2(95,54));handle.sizeDelta=new Vector2(95,0);
        var slider=area.gameObject.AddComponent<Slider>();slider.minValue=min;slider.maxValue=max;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();
        readout=Label(page.transform,"Value",new Vector2(270,y-43),new Vector2(450,35),22,false);return slider;
    }
    public void Open()
    {
        sensitivity=GameOptions.MouseSensitivityMultiplier;volume=PlayerPrefs.GetFloat("Options.MasterVolume",1);
        sfxVolume=GameOptions.SfxVolume;musicVolume=GameOptions.MusicVolume;
        fullscreen=Application.isEditor ? PlayerPrefs.GetInt(GameOptions.FullscreenKey,1)==1 : Screen.fullScreen;
        quality=GameOptions.SavedQualityPreset;
        fps=PlayerPrefs.GetInt("Options.FPS",60);section=0;root.SetActive(true);Refresh();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    }
    public void Close(bool save)
    {
        if(!IsOpen)return;
        if(save)
        {
            PlayerPrefs.SetFloat(GameOptions.SensitivityKey,sensitivity);PlayerPrefs.SetFloat("Options.MasterVolume",volume);
            PlayerPrefs.SetFloat(GameOptions.SfxKey,sfxVolume);PlayerPrefs.SetFloat(GameOptions.MusicKey,musicVolume);
            PlayerPrefs.SetInt(GameOptions.FullscreenKey,fullscreen?1:0);PlayerPrefs.SetInt("Options.Quality",GameOptions.QualityIndex(quality));PlayerPrefs.SetInt("Options.FPS",fps);
            AudioListener.volume=volume;GameOptions.ApplyFullscreen(fullscreen);QualitySettings.SetQualityLevel(GameOptions.QualityIndex(quality));QualitySettings.vSyncCount=0;Application.targetFrameRate=fps;
            PlayerPrefs.Save();
        }
        root.SetActive(false);if(UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);closed?.Invoke();
    }
    private void Refresh()
    {
        for(int i=0;i<3;i++)
        {
            layout.pages[i].SetActive(i==section);
            var image=tabs[i].GetComponent<Image>();ExportUIArt.Apply(image,i==section?"settingsTabSelected":"settingsTab");
            image.color=i==section?Color.white:new Color(.65f,.65f,.65f);
        }
        layout.sensitivity.SetValueWithoutNotify(Mathf.Log10(Mathf.Max(GameOptions.MinimumMouseSensitivity,sensitivity)));
        layout.volume.SetValueWithoutNotify(volume);
        layout.sfx.SetValueWithoutNotify(sfxVolume);layout.music.SetValueWithoutNotify(musicVolume);
        layout.sfxValue.text=Mathf.RoundToInt(sfxVolume*100)+"%";
        layout.musicValue.text=Mathf.RoundToInt(musicVolume*100)+"%";
        layout.sensitivityValue.text=sensitivity.ToString("0.00")+"x";
        layout.volumeValue.text=Mathf.RoundToInt(volume*100)+"%";
        layout.choices[0].GetComponentInChildren<TMP_Text>().text=(fullscreen?"ON":"OFF")+"  >";
        layout.choices[1].GetComponentInChildren<TMP_Text>().text=GameOptions.QualityNames[quality]+"  >";
        layout.choices[2].GetComponentInChildren<TMP_Text>().text=(fps<0?"UNLIMITED":fps+" FPS")+"  >";
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
        var t=Rect(value,parent,pos,size).gameObject.AddComponent<TextMeshProUGUI>();t.font=TMP_Settings.defaultFontAsset;t.text=value;t.fontSize=font;t.alignment=TextAlignmentOptions.Center;t.color=Color.black;t.raycastTarget=false;t.enableAutoSizing=true;t.fontSizeMin=18;t.fontSizeMax=font;if(outline)ExportUIArt.OutlineText(t);return t;
    }
    private static Button Button(Transform parent,string text,string art,Vector2 pos,Vector2 size,System.Action action)
    {
        var r=Rect(text,parent,pos,size);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.64f,.63f,.58f);if(!string.IsNullOrEmpty(art))ExportUIArt.Apply(image,art);
        var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>action());
        Label(r,text,Vector2.zero,size-new Vector2(20,8),art=="settingsSave"||art=="settingsReset"?44:32,true);return button;
    }
}
