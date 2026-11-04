using PlayerPrefs = GameSavePrefs;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public sealed partial class SharedOptionsPanel
{
    private readonly GameObject root;
    private readonly Transform frame;
    private readonly System.Action closed;
    private GameObject page;
    private readonly Button[] tabs=new Button[3];
    private int section,quality,fps;
    private float sensitivity,volume,sfxVolume,musicVolume;
    private bool fullscreen;
    private bool showFps;
    private Button fpsViewer;
    private bool closing, previewOpen, displayChanged;
    private float previousVolume, sampleAt;
    private int previousQuality, previousVsync, previousFps, previousWidth, previousHeight;
    private FullScreenMode previousMode;
    private bool previousShowFps;

    private void PreviewAudio(bool sample)
    {
        if (!previewOpen || closing) return;
        GameOptions.previewSensitivity = sensitivity;
        GameOptions.previewSfx = sfxVolume;
        GameOptions.previewMusic = musicVolume;
        AudioListener.volume = volume;
        if (sample && Time.unscaledTime >= sampleAt)
        {
            sampleAt = Time.unscaledTime + .22f;
            GameplayAudioManager.PlayRecordingCue("Notification by");
        }
    }

    private void PreviewDisplay()
    {
        if (!previewOpen || closing || Application.isEditor) return;
        displayChanged = true;
        if (fullscreen == (previousMode != FullScreenMode.Windowed))
            Screen.SetResolution(previousWidth, previousHeight, previousMode);
        else if (fullscreen)
            Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
        else
            Screen.SetResolution(PlayerPrefs.GetInt("Options.WindowWidth",1280), PlayerPrefs.GetInt("Options.WindowHeight",720), FullScreenMode.Windowed);
    }
    private void EndPreview(bool revert)
    {
        if (!previewOpen) return;
        previewOpen = false;
        EndVoicePreview();
        GameOptions.previewSensitivity = GameOptions.previewMusic = GameOptions.previewSfx = null;
        if (!revert) return;
        AudioListener.volume = previousVolume;
        if (QualitySettings.GetQualityLevel() != previousQuality) QualitySettings.SetQualityLevel(previousQuality);
        QualitySettings.vSyncCount = previousVsync;
        Application.targetFrameRate = previousFps;
        FpsViewer.SetVisible(previousShowFps);
        if (displayChanged && !Application.isEditor) Screen.SetResolution(previousWidth, previousHeight, previousMode);
    }
    private ScrollRect generalScroll;
    private RectTransform controlsContent;
    private float controlsContentHeight;
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
                }
                else
                {
                    string[] options={"FULLSCREEN","QUALITY","FRAME RATE"};
                    for(int j=0;j<3;j++)
                    {
                        Label(page.transform,options[j],new Vector2(-305,168-j*94),new Vector2(440,60),32,true);
                        layout.choices[j]=Button(page.transform,options[j],"",new Vector2(270,168-j*94),new Vector2(480,54),()=>{});
                    }
                }
            }
        }
        // Upgrade scene-authored panels as well as the runtime fallback, retaining their artwork.
        BuildScrollableGeneralSettings();
        var graphics = layout.pages[2].transform;
        var existingFps = graphics.Find("FPS viewer toggle");
        if (existingFps != null) fpsViewer = existingFps.GetComponent<Button>();
        else
        {
            Label(graphics,"SHOW FPS",new Vector2(-305,-114),new Vector2(440,60),32,true);
            fpsViewer=Button(graphics,"OFF  >","",new Vector2(270,-114),new Vector2(480,54),()=>{});
            fpsViewer.name="FPS viewer toggle";
        }
        foreach(var label in graphics.GetComponentsInChildren<TMP_Text>(true))
            if(label.text=="Press SAVE to apply display changes." || label.text=="Preview your changes. Press SAVE to keep them.")
                label.gameObject.SetActive(false);
        Bind(fpsViewer,()=>{showFps=!showFps;FpsViewer.SetVisible(showFps);Refresh();});
        page=layout.pages[1];
        foreach(var label in page.GetComponentsInChildren<TMP_Text>(true))
            if(label.text.StartsWith("Controls all game audio.") || label.text.StartsWith("Listen as you adjust."))
            {
                label.gameObject.SetActive(false);
            }
        if(layout.sfx==null)layout.sfx=MakeSlider("SOUND EFFECTS",58,0,1,out layout.sfxValue);
        if(layout.music==null)layout.music=MakeSlider("MUSIC",-52,0,1,out layout.musicValue);
        BuildVoiceSettings();
        for(int i=0;i<3;i++) {int index=i;Bind(tabs[i],()=>{section=index;Refresh();});}
        Bind(layout.save,()=>Close(true));Bind(layout.close,()=>Close(false));
        Bind(layout.reset,()=>{sensitivity=1;volume=1;sfxVolume=1;musicVolume=1;fullscreen=true;quality=1;fps=60;showFps=false;ResetVoiceDraft();PreviewAudio(false);PreviewDisplay();QualitySettings.SetQualityLevel(GameOptions.QualityIndex(quality));QualitySettings.vSyncCount=0;Application.targetFrameRate=fps;FpsViewer.SetVisible(showFps);Refresh();});
        Bind(layout.controls,()=>{
            SetControlsExpanded(!controlsContent.gameObject.activeSelf);
        });
        Bind(layout.choices[0],()=>{fullscreen=!fullscreen;PreviewDisplay();Refresh();});
        Bind(layout.choices[1],()=>{quality=(quality+1)%3;QualitySettings.SetQualityLevel(GameOptions.QualityIndex(quality));QualitySettings.vSyncCount=0;Refresh();});
        Bind(layout.choices[2],()=>{fps=fps==30?60:fps==60?120:fps==120?-1:30;QualitySettings.vSyncCount=0;Application.targetFrameRate=fps;Refresh();});
        layout.sensitivity.onValueChanged.RemoveAllListeners();layout.sensitivity.onValueChanged.AddListener(v=>{sensitivity=Mathf.Pow(10,v);layout.sensitivityValue.text=sensitivity.ToString("0.00")+"x";PreviewAudio(false);});
        layout.volume.onValueChanged.RemoveAllListeners();layout.volume.onValueChanged.AddListener(v=>{volume=v;layout.volumeValue.text=Mathf.RoundToInt(v*100)+"%";PreviewAudio(true);});
        layout.sfx.onValueChanged.RemoveAllListeners();layout.sfx.onValueChanged.AddListener(v=>{sfxVolume=v;layout.sfxValue.text=Mathf.RoundToInt(v*100)+"%";PreviewAudio(true);});
        layout.music.onValueChanged.RemoveAllListeners();layout.music.onValueChanged.AddListener(v=>{musicVolume=v;layout.musicValue.text=Mathf.RoundToInt(v*100)+"%";PreviewAudio(false);});
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
        if (previewOpen) return;
        closing = false;
        previewOpen = true; displayChanged = false; sampleAt = 0f;
        BeginVoicePreview();
        previousVolume = AudioListener.volume; previousQuality = QualitySettings.GetQualityLevel();
        previousVsync = QualitySettings.vSyncCount; previousFps = Application.targetFrameRate;
        previousWidth = Screen.width; previousHeight = Screen.height; previousMode = Screen.fullScreenMode;
        previousShowFps = GameOptions.ShowFps;
        layout.cancelPreview = () => EndPreview(true);
        SetControlsExpanded(false);
        PauseManager.PutOverlayOnTop(root, 30010);
        sensitivity=GameOptions.MouseSensitivityMultiplier;volume=PlayerPrefs.GetFloat("Options.MasterVolume",1);
        sfxVolume=GameOptions.SfxVolume;musicVolume=GameOptions.MusicVolume;
        fullscreen=Application.isEditor ? PlayerPrefs.GetInt(GameOptions.FullscreenKey,1)==1 : Screen.fullScreen;
        quality=GameOptions.SavedQualityPreset;
        showFps=GameOptions.ShowFps;
        fps=PlayerPrefs.GetInt("Options.FPS",60);section=0;UITransition.ShowSmooth(root);Refresh();
        if (frame != null) UITransition.ShowSmooth(frame.gameObject, true);
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    }
    public void Close(bool save)
    {
        if(!IsOpen || closing)return;
        closing = true;
        if(save)
        {
            SaveVoiceDraft();
            PlayerPrefs.SetFloat(GameOptions.SensitivityKey,sensitivity);PlayerPrefs.SetFloat("Options.MasterVolume",volume);
            PlayerPrefs.SetFloat(GameOptions.SfxKey,sfxVolume);PlayerPrefs.SetFloat(GameOptions.MusicKey,musicVolume);
            PlayerPrefs.SetInt(GameOptions.FullscreenKey,fullscreen?1:0);PlayerPrefs.SetInt("Options.Quality",GameOptions.QualityIndex(quality));PlayerPrefs.SetInt("Options.FPS",fps);
            if(displayChanged && previousMode==FullScreenMode.Windowed && fullscreen)
            {PlayerPrefs.SetInt("Options.WindowWidth",previousWidth);PlayerPrefs.SetInt("Options.WindowHeight",previousHeight);}
            AudioListener.volume=volume;QualitySettings.SetQualityLevel(GameOptions.QualityIndex(quality));QualitySettings.vSyncCount=0;Application.targetFrameRate=fps;
            PlayerPrefs.SetInt(GameOptions.ShowFpsKey,showFps?1:0);
            PlayerPrefs.Save();
            FpsViewer.SetVisible(showFps);
        }
        EndPreview(!save);
        UITransition.Hide(root, () =>
        {
            closing = false;
            if(UnityEngine.EventSystems.EventSystem.current!=null)UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            closed?.Invoke();
        });
    }
    private void Refresh()
    {
        for(int i=0;i<3;i++)
        {
            if (i == section)
            {
                // Only animate newly selected tabs, not every settings value change.
                if (!layout.pages[i].activeSelf) UITransition.ShowSmooth(layout.pages[i]);
            }
            else layout.pages[i].SetActive(false);
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
        fpsViewer.GetComponentInChildren<TMP_Text>().text=(showFps?"ON":"OFF")+"  >";
        RefreshVoiceSettings();
    }
    private void BuildScrollableGeneralSettings()
    {
        var general = layout.pages[0].transform;
        var existing = general.Find("Settings viewport");
        if (existing != null)
        {
            generalScroll = existing.GetComponent<ScrollRect>();
            controlsContent = generalScroll.content.Find("Controls sections") as RectTransform;
            if (controlsContent != null) controlsContentHeight = controlsContent.sizeDelta.y;
            return;
        }
        // Preserve the authored General layout and scroll it inside the existing
        // frame. Tabs and Save/Reset remain outside the masked region.
        var children = new System.Collections.Generic.List<RectTransform>();
        foreach (Transform child in general)
            if (child is RectTransform rect) children.Add(rect);
        var view = Rect("Settings viewport", general, Vector2.zero, new Vector2(1080, 470));
        view.gameObject.AddComponent<Image>().color = Color.clear;
        view.gameObject.AddComponent<RectMask2D>();
        var scroll = view.gameObject.AddComponent<ScrollRect>();
        generalScroll = scroll;
        scroll.viewport = view; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;
        var content = Rect("General content", view, Vector2.zero, new Vector2(1080, 470));
        content.anchorMin = content.anchorMax = new Vector2(.5f, 1);
        content.pivot = new Vector2(.5f, 1);
        foreach (var child in children)
        {
            var position = child.anchoredPosition;
            child.SetParent(content, false);
            child.anchorMin = child.anchorMax = new Vector2(.5f, 1);
            child.anchoredPosition = position - new Vector2(0, 235);
        }
        // Use bounded text sections rather than one very tall masked TMP mesh.
        controlsContent = Rect("Controls sections", content, Vector2.zero, new Vector2(1020, 0));
        controlsContent.anchorMin = controlsContent.anchorMax = new Vector2(.5f, 1);
        controlsContent.pivot = new Vector2(.5f, 1);
        controlsContent.anchoredPosition = new Vector2(0, -220);
        layout.controlsHelp.gameObject.SetActive(false);
        foreach (var sectionText in ControlsReference.Split(new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            // Fixed, bounded rows are independent of TMP's inactive-canvas
            // preferred-height calculation. Each binding gets its own space.
            foreach (var line in sectionText.Trim().Split('\n'))
            {
                bool heading = line.StartsWith("<b>");
                float height = heading ? 52 : 76;
                var text = Label(controlsContent, line, Vector2.zero, new Vector2(980, height), 26, false);
                text.name = heading ? "Controls heading" : "Control binding";
                text.fontSharedMaterial = text.font.material;
                text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, 1);
                text.rectTransform.pivot = new Vector2(.5f, 1);
                text.rectTransform.anchoredPosition = new Vector2(0, -controlsContentHeight);
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.enableAutoSizing = false; text.enableWordWrapping = true;
                text.richText = true; text.lineSpacing = 0;
                text.overflowMode = TextOverflowModes.Truncate;
                controlsContentHeight += height;
            }
            controlsContentHeight += 28;
        }
        controlsContent.sizeDelta = new Vector2(1020, controlsContentHeight);
        scroll.content = content;
        scroll.verticalScrollbar = null;
        SetControlsExpanded(false);
    }

    private void SetControlsExpanded(bool expanded)
    {
        controlsContent.gameObject.SetActive(expanded);
        layout.controls.GetComponentInChildren<TMP_Text>().text = expanded ? "HIDE CONTROLS" : "VIEW CONTROLS";
        generalScroll.content.sizeDelta = new Vector2(1080, expanded ? Mathf.Max(470, 240 + controlsContentHeight) : 470);
        generalScroll.StopMovement();
        generalScroll.content.anchoredPosition = Vector2.zero;
        if (expanded) Canvas.ForceUpdateCanvases();
    }

    private const string ControlsReference =
        "<b>MOVEMENT & INTERACTION</b>\n" +
        "W / A / S / D — Move     •     Mouse — Look around\n" +
        "Shift — Sprint\n" +
        "Ctrl — Slow, quiet walk; precise camera movement and aim\n" +
        "E — Interact, pick up equipment, or leave an open terminal\n" +
        "Left mouse — Use held equipment     •     G — Drop / deploy\n" +
        "1–5 — Select an equipment slot\n" +
        "F — Insert the held SD card into the computer tower\n\n" +
        "<b>MENUS & DIALOGUE</b>\n" +
        "Esc — Pause / back     •     P — Almanac\n" +
        "Tab — Contract / current task panel     •     I — Player profile\n" +
        "Space / left mouse — Advance boss dialogue when prompted\n" +
        "Left / right arrows — Previous / next page in paged notices\n" +
        "Mouse wheel / scrollbar — Scroll lists; click buttons to select and confirm\n\n" +
        "<b>DIRECTOR TABLET — STAGE BUILDING</b>\n" +
        "Left mouse — Select an element or place the item at the cursor\n" +
        "Right mouse — Remove an element     •     T — Reposition selection\n" +
        "R — Rotate the selected / held stage object by 15 degrees\n" +
        "RGB sliders / RGB and HEX fields — Change the selected object's color\n" +
        "Enter / finish editing a color field — Apply the typed color\n" +
        "STORAGE / SHOP — Switch between owned elements and purchases\n" +
        "CHOOSE SET — Select an interior     •     CLEAR STAGE — Remove stage items\n" +
        "X — Close the Director tablet\n\n" +
        "<b>DIRECTOR TABLET — LIGHT STRIPS & GRIP</b>\n" +
        "Select the strip or grip before using these controls.\n" +
        "Q / E — Turn selected strip or grip left / right\n" +
        "F — Cycle selected strip or grip power\n" +
        "Y — Cycle selected light-strip tilt\n\n" +
        "<b>CAMERA</b>\n" +
        "Left mouse — Enter / exit the viewfinder\n" +
        "C — Insert / eject SD card     •     R — Start / stop recording\n" +
        "Mouse wheel — Zoom     •     Q / E — Adjust camera height\n" +
        "Ctrl + WASD — Smooth movement     •     Ctrl + mouse — Fine aim\n" +
        "J — Mount camera on available support     •     K — Run dolly\n" +
        "M — Toggle exposure monitor while viewing through the camera\n" +
        "G — Drop the camera\n\n" +
        "<b>CAMERA SETTINGS — AS FEATURES UNLOCK</b>\n" +
        "F2 — Open / close camera settings\n" +
        "Up / down arrows — Select a setting\n" +
        "Left / right arrows — Change the selected value\n" +
        "Settings include grid, white balance, ISO, iris and shutter.\n\n" +
        "<b>HELD LIGHTS & PRODUCTION EQUIPMENT</b>\n" +
        "Left mouse — Toggle light power     •     Mouse wheel — Intensity\n" +
        "Up / down arrows — Tilt     •     Q / E — Raise / lower light\n" +
        "Z / X — Color temperature     •     V / B — Diffusion (advanced lights)\n" +
        "G — Drop / deploy the equipment\n" +
        "Held production kit: Q / E — Turn; left mouse — Power\n" +
        "Held light strip: R — Tilt; C — Cycle color\n" +
        "Lighting lessons: hold B — Compare lighting; Enter — Confirm when prompted\n\n" +
        "<b>ACTOR DIRECTION — HOLD THE MEGAPHONE</b>\n" +
        "Left mouse — Select actor; then click a prop for an interaction\n" +
        "Z — Cycle actor animations     •     O — Stop / return product\n" +
        "Arrow keys — Nudge actor     •     R — Turn actor 15 degrees\n" +
        "T — Reposition actor; aim at the floor and left-click to place\n" +
        "B / N — Save start / end walk marks\n" +
        "K — Rehearse walk     •     J — Return to start     •     H — Clear walk\n" +
        "G — Drop megaphone\n\n" +
        "<b>COFFEE SET INTERACTIONS</b>\n" +
        "E — Interact with a seat, product or coffee machine\n" +
        "C — Drink while holding a coffee cup\n" +
        "E / G — Leave the interaction or return the held cup\n\n" +
        "<b>COMPUTER & VIDEO EDITOR</b>\n" +
        "F at the tower — Insert held SD card; E — Open / leave computer\n" +
        "Mouse — Open recordings, select clips and use playback controls\n" +
        "Space — Play / pause video in the editor\n" +
        "Drag clips — Add to timeline / change position\n" +
        "Double-click a timeline video clip — Open trimming\n" +
        "Drag trim handles — Change clip start / end\n" +
        "Click / drag timeline playhead — Seek through the commercial\n" +
        "Ctrl + Z — Undo an editor change\n" +
        "B — Split selected video at the playhead (Contract 4)\n" +
        "Drag overlays — Position branding; drag timeline handles to set timing\n" +
        "On-screen controls — Motion, animation, transitions, music and color grade\n" +
        "EXPORT / review / submit buttons — Finish and submit the commercial\n\n" +
        "<b>MULTIPLAYER — PLACEMENT & ACTORS</b>\n" +
        "Role permissions apply. Some keys differ from solo play.\n" +
        "Left mouse — Select / place     •     T — Reposition / cancel placement\n" +
        "Q / E — Turn while placing; R — Turn in the station placement view\n" +
        "H — Clear selection     •     Delete — Remove selected object\n" +
        "Megaphone: Enter — Call action; Z — Pose; O — Stop\n" +
        "Actor: B / N — Walk marks; K — Walk; J — Return to start\n" +
        "Selected actor + megaphone: click another prop to interact\n\n" +
        "<b>MULTIPLAYER — VOICE CHAT</b>\n" +
        "Left Alt — Hold to talk (default; change the key in Audio Options)\n" +
        "Click your lobby microphone — Mute / unmute your microphone\n" +
        "Click a teammate's microphone — Mute / unmute them for yourself\n\n" +
        "<b>MULTIPLAYER — CAMERA, LIGHT & SOUND</b>\n" +
        "Camera: left mouse — Viewfinder; C — SD card; R — Record\n" +
        "Camera: wheel — Zoom; V — Cycle wide / medium / close shot\n" +
        "Selected light: Z / X — Temperature; C / V — Intensity; F — Power\n" +
        "Held audio equipment: Z / X — Gain; F — Power\n" +
        "E — Pick up / interact     •     1–5 — Equip slot     •     G — Drop\n" +
        "P — Almanac     •     Tab — Contract / crew context     •     Esc — Back\n\n" +
        "<b>DEVELOPMENT BUILDS ONLY</b>\n" +
        "F12 — Developer commands, when enabled     •     Esc — Close commands\n";

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
