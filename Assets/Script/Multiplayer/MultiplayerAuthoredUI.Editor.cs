using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MultiplayerAuthoredUI
{
    private GameObject editorView;
    private MultiplayerUIReferences editorRefs;
    private RawImage computerScreen, editorScreen;
    private Texture2D previewTexture;
    private Material gradingMaterial;
    private List<byte[]> frames;
    private bool playing, playSequence;
    private float playTime, trimIn, trimOut, lastPlayback;
    private int selectedTake, loadedFrame = -1, sequenceIndex, cachedShots = -1, cachedCuts = -1;
    private Transform editorBank, timeline;
    private TMP_Text trimLabel, editorStatus;
    private TMP_Text brandPreview;
    private Slider inSlider, outSlider;
    private TMP_InputField brandTitle;
    private readonly List<GameObject> clipCards = new List<GameObject>(), cutCards = new List<GameObject>();
    private readonly List<CrewCut> playbackCuts = new List<CrewCut>();
    private int pendingDownload;

    private void SetupComputer()
    {
        var root = panels["computer"]?.transform; if (root == null) return;
        computerScreen = Find(root,"Player")?.GetComponentInChildren<RawImage>(true);
        Bind(root,"FolderLogo", ComputerGrid);
        var folder = Find(root,"FolderLogo");
        if (folder != null && folder.GetComponent<Button>() == null) { var button = folder.gameObject.AddComponent<Button>(); button.onClick.AddListener(ComputerGrid); }
        Bind(root,"Editor", () => Open("editor"));
        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            if (button.name.StartsWith("Close")) button.onClick.AddListener(Close);
            if (button.name == "Play") button.onClick.AddListener(() => playing = true);
            if (button.name == "Pause") button.onClick.AddListener(() => playing = false);
            if (button.name == "Back") button.onClick.AddListener(ComputerGrid);
        }
        var progress = Find(root,"Progress")?.GetComponent<Slider>();
        if (progress != null) progress.onValueChanged.AddListener(value => { var shot = Crew.State.shots.Find(s => s.id == selectedTake); if (shot != null) { playTime = value * shot.duration; playing = false; } });
    }
    private void ComputerHome()
    {
        refs.Get<GameObject>("ComputerUIManager.recordingsGridPanel")?.SetActive(false);
        refs.Get<GameObject>("ComputerUIManager.videoPlayerPanel")?.SetActive(false);
        var image = Find(panels["computer"].transform,"Image"); if (image != null && image.GetComponentInChildren<Button>(true) != null) image.gameObject.SetActive(false);
    }
    private void ComputerGrid()
    {
        refs.Get<GameObject>("ComputerUIManager.videoPlayerPanel")?.SetActive(false);
        Activate(refs.Get<GameObject>("ComputerUIManager.recordingsGridPanel"));
        RefreshClips(); playing = false;
    }
    private void RefreshClips()
    {
        var container = refs.Get<Transform>("ComputerUIManager.gridContentContainer"); if (container == null) return;
        int count = Crew.State.shots.Count(s => s.uploaded && s.footageReady);
        if (count == cachedShots) return;
        cachedShots = count;
        foreach (Transform child in container) child.gameObject.SetActive(false);
        foreach (var card in clipCards) Destroy(card); clipCards.Clear();
        foreach (var take in Crew.State.shots.Where(s => s.uploaded && s.footageReady))
        {
            var card = Button(container,"Crew recorded take " + take.id,"TAKE " + take.id + "\n" + take.size + "  " + take.duration.ToString("0.0") + "s", () => PreviewTake(take.id));
            clipCards.Add(card.gameObject);
        }
    }
    private void PreviewTake(int id)
    {
        var take = Crew.State.shots.Find(s => s.id == id && s.uploaded);
        if (take == null) return;
        var recording = MultiplayerRecording.Instance;
        if (recording == null) return;
        if (!recording.Has(id)) { recording.Download(take); pendingDownload = id; Message = recording.Status; return; }
        frames = recording.Frames(id); selectedTake = id; trimIn = playTime = 0; trimOut = take.duration; loadedFrame = -1; playing = playSequence = false;
        if (Panel == "computer")
        {
            refs.Get<GameObject>("ComputerUIManager.recordingsGridPanel")?.SetActive(false);
            Activate(refs.Get<GameObject>("ComputerUIManager.videoPlayerPanel"));
            Set(refs.Get<TMP_Text>("ComputerUIManager.playerTitleText"), take.size + " — TAKE " + id);
            var loading = Find(panels["computer"].transform,"Loading"); if (loading != null) loading.gameObject.SetActive(false);
        }
        if (inSlider != null) { inSlider.minValue = 0; inSlider.maxValue = Mathf.Max(0,take.duration - .5f); inSlider.SetValueWithoutNotify(0); }
        if (outSlider != null) { outSlider.minValue = .5f; outSlider.maxValue = Mathf.Max(.5f,take.duration); outSlider.SetValueWithoutNotify(take.duration); }
    }
    private void OpenEditor()
    {
        if (!Crew.HasRole(CrewRole.Editor)) return;
        if (editorView == null)
        {
            var prefab = Resources.Load<GameObject>("CrewUI/Editor");
            if (prefab == null) { Message = "Exit Play Mode so Unity can generate the Editor UI copy."; return; }
            editorView = Instantiate(prefab); editorRefs = editorView.GetComponent<MultiplayerUIReferences>();
            editorRefs.RestoreArtwork();
            editorView.name = "Multiplayer authored Editor";
            foreach (var canvas in editorView.GetComponentsInChildren<Canvas>(true))
            { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 80; canvas.enabled = true; }
            editorBank = editorRefs.Get<Transform>("EditorManager.clipBankContainer");
            timeline = editorRefs.Get<Transform>("EditorManager.timelineContainer");
            editorScreen = editorRefs.Get<RawImage>("ColorGradingManager.computerScreen");
            foreach (var name in new[] {"EditorManager.reviewVideoPanel","EditorManager.finalGradePanel"}) editorRefs.Get<GameObject>(name)?.SetActive(false);
            var screenMat = editorScreen != null ? editorScreen.material : null;
            if (screenMat != null) { gradingMaterial = new Material(screenMat); editorScreen.material = gradingMaterial; }
            if (editorScreen != null) brandPreview=Text(editorScreen.transform,"Crew brand overlay",new Vector2(.08f,.06f),new Vector2(.92f,.19f),22);
            SetupEditorControls();
        }
        Activate(editorView); Panel = "editor"; RoleSelectionUI.Open = true;
        RefreshEditorClips();
    }
    private void SetupEditorControls()
    {
        var canvas = editorView.GetComponentInChildren<Canvas>(true).transform;
        var transport = new GameObject("Crew editing controls",typeof(RectTransform)); transport.transform.SetParent(canvas,false);
        Anchor(transport.transform,new Vector2(.02f,.02f),new Vector2(.98f,.15f));
        var background = transport.AddComponent<Image>(); background.color = new Color(.04f,.055f,.08f,.97f);
        var play = Button(transport.transform,"Crew play","PLAY / PAUSE",() => { if (playTime >= trimOut) playTime = trimIn; playing = !playing; }); Anchor(play,new Vector2(0,.52f),new Vector2(.16f,1));
        var add = Button(transport.transform,"Crew add cut","ADD CUT",() => { if (selectedTake != 0) Crew.Send(new CrewCommand { action = "cut",id = selectedTake, number = trimIn, number2 = trimOut }); }); Anchor(add,new Vector2(.17f,.52f),new Vector2(.3f,1));
        var sequence = Button(transport.transform,"Crew preview sequence","PLAY COMMERCIAL",PlayCommercial); Anchor(sequence,new Vector2(.31f,.52f),new Vector2(.51f,1));
        var submit = Button(transport.transform,"Crew submit","SUBMIT",() => Crew.Send(new CrewCommand { action = "submit" })); Anchor(submit,new Vector2(.52f,.52f),new Vector2(.68f,1));
        var exit = Button(transport.transform,"Crew leave editor","BACK TO COMPUTER",() => Open("computer")); Anchor(exit,new Vector2(.69f,.52f),new Vector2(1,1));
        inSlider = SimpleSlider(transport.transform,"IN",new Vector2(.02f,.02f),new Vector2(.31f,.38f));
        outSlider = SimpleSlider(transport.transform,"OUT",new Vector2(.34f,.02f),new Vector2(.63f,.38f));
        inSlider.onValueChanged.AddListener(value => trimIn = Mathf.Min(value,trimOut - .5f));
        outSlider.onValueChanged.AddListener(value => trimOut = Mathf.Max(value,trimIn + .5f));
        trimLabel = Text(transport.transform,"Cut range",new Vector2(.65f,0),new Vector2(1,.45f),16);
        editorStatus = Text(canvas,"Crew editor status",new Vector2(.05f,.93f),new Vector2(.95f,.99f),18);
        // Keep the authored clip bank, timeline, branding panel and grading sliders.
        foreach (var button in editorView.GetComponentsInChildren<Button>(true))
        {
            if (button.transform.IsChildOf(transport.transform)) continue;
            string text = (button.GetComponentInChildren<TMP_Text>(true)?.text ?? button.name).ToLowerInvariant();
            if (text.Contains("export") || text.Contains("submit")) button.onClick.AddListener(() => Crew.Send(new CrewCommand {action="submit"}));
            else if (text.Contains("brand")) button.onClick.AddListener(() => EditorPhase(1));
            else if (text.Contains("color") || text.Contains("grad")) button.onClick.AddListener(() => EditorPhase(2));
            else if (text.Contains("clip") || text.Contains("cut")) button.onClick.AddListener(() => EditorPhase(0));
            else if (text.Contains("play")) button.onClick.AddListener(PlayCommercial);
            else if (text.Contains("back") || text.Contains("close")) button.onClick.AddListener(() => Open("computer"));
        }
        foreach (string field in new[] {"brightnessSlider","contrastSlider","saturationSlider"})
        {
            var slider = editorRefs.Get<Slider>("ColorGradingManager." + field);
            if (slider == null) continue;
            slider.onValueChanged.AddListener(value => SendGrade());
        }
        var branding = editorRefs.Get<Transform>("EditorManager.brandingBinPanel");
        if (branding != null)
        {
            var title = Text(branding,"Crew brand label",new Vector2(.05f,.7f),new Vector2(.95f,.9f),20); title.text = "COMMERCIAL BRAND TITLE";
            var field = new GameObject("Crew brand title",typeof(RectTransform),typeof(Image),typeof(TMP_InputField)); field.transform.SetParent(branding,false);
            Anchor(field.transform,new Vector2(.05f,.4f),new Vector2(.95f,.65f)); field.GetComponent<Image>().color = new Color(.15f,.18f,.22f);
            var text = Text(field.transform,"Text",new Vector2(.03f,0),new Vector2(.97f,1),20) as TextMeshProUGUI;
            brandTitle = field.GetComponent<TMP_InputField>(); brandTitle.textViewport = (RectTransform)field.transform; brandTitle.textComponent = text; brandTitle.characterLimit = 80;
            brandTitle.SetTextWithoutNotify(Crew.State.commercialTitle); brandTitle.onEndEdit.AddListener(value => SendGrade());
        }
        EditorPhase(0);
    }
    private void EditorPhase(int phase)
    {
        if (editorRefs == null) return;
        editorRefs.Get<GameObject>("EditorManager.brandingBinPanel")?.SetActive(phase == 1);
        editorRefs.Get<GameObject>("EditorManager.colorGradingBin")?.SetActive(phase == 2);
        if (editorBank != null) editorBank.gameObject.SetActive(phase == 0);
    }
    private void SendGrade()
    {
        float Value(string key, float fallback) { var slider = editorRefs.Get<Slider>("ColorGradingManager." + key); return slider != null ? slider.value : fallback; }
        Crew.Send(new CrewCommand {action="grade",position=new Vector3(Value("brightnessSlider",1),Value("contrastSlider",1),Value("saturationSlider",1)),kind=brandTitle != null ? brandTitle.text : Crew.State.commercialTitle});
    }
    private void RefreshEditorClips()
    {
        if (editorRefs == null) return;
        // Reuse the copied scene containers, with room-backed clip actions.
        if (editorBank != null)
        {
            foreach (Transform child in editorBank) { child.gameObject.SetActive(false); if (child.name.StartsWith("Crew take")) Destroy(child.gameObject); }
            int index = 0;
            foreach (var take in Crew.State.shots.Where(s=>s.uploaded && s.footageReady))
            {
                var card = Button(editorBank,"Crew take " + take.id,take.size + "\nTAKE " + take.id,()=>PreviewTake(take.id));
                ((RectTransform)card.transform).sizeDelta = new Vector2(140,85);
                if (editorBank.GetComponent<LayoutGroup>() == null) ((RectTransform)card.transform).anchoredPosition = new Vector2(index % 2 * 145,-(index / 2) * 90); index++;
            }
        }
        if (timeline == null) return;
        foreach (var obj in cutCards) Destroy(obj); cutCards.Clear();
        float x = 0;
        for (int i=0;i<Crew.State.cuts.Count;i++)
        {
            int index=i; var cut = Crew.State.cuts[i];
            var card = Button(timeline,"Crew cut " + i,"TAKE " + cut.shot + "  " + (cut.end-cut.start).ToString("0.0") + "s",()=>PreviewTake(cut.shot));
            float width = Mathf.Max(160,(cut.end-cut.start)*40);
            var rect=(RectTransform)card.transform; rect.anchorMin=rect.anchorMax=new Vector2(0,.5f); rect.pivot=new Vector2(0,.5f); rect.sizeDelta=new Vector2(width,70); rect.anchoredPosition=new Vector2(x,0); x+=width+4;
            var remove=Button(card.transform,"Remove","REMOVE",()=>Crew.Send(new CrewCommand {action="removeCut",index=index})); Anchor(remove,new Vector2(.5f,0),new Vector2(1,.35f));
            var up=Button(card.transform,"Move earlier","EARLIER",()=>Crew.Send(new CrewCommand {action="raiseCut",index=index})); Anchor(up,new Vector2(0,0),new Vector2(.5f,.35f));
            cutCards.Add(card.gameObject);
        }
        var content = timeline as RectTransform; if (content != null) content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Max(700,x));
    }
    private void PlayCommercial()
    {
        var missing = Crew.State.cuts.Select(c=>Crew.State.shots.Find(s=>s.id==c.shot)).FirstOrDefault(s=>s!=null&&!MultiplayerRecording.Instance.Has(s.id));
        if (missing != null) { MultiplayerRecording.Instance.Download(missing); Message="Download all sequence takes, then press PLAY COMMERCIAL."; return; }
        playbackCuts.Clear(); playbackCuts.AddRange(Crew.State.cuts.Select(c=>new CrewCut {shot=c.shot,start=c.start,end=c.end})); sequenceIndex=0; PlayNextCut();
    }
    private void PlayNextCut()
    {
        if (sequenceIndex>=playbackCuts.Count) {playing=playSequence=false;return;}
        var cut=playbackCuts[sequenceIndex++]; PreviewTake(cut.shot); playTime=trimIn=cut.start; trimOut=cut.end; playing=playSequence=true;
    }
    private void UpdatePlayback()
    {
        float delta=Time.unscaledTime-lastPlayback; lastPlayback=Time.unscaledTime;
        if (pendingDownload!=0 && MultiplayerRecording.Instance!=null && MultiplayerRecording.Instance.Has(pendingDownload))
        {int id=pendingDownload;pendingDownload=0;PreviewTake(id);}
        if (Panel!="computer" && Panel!="editor") return;
        if (playing) {playTime+=delta;if(playTime>=trimOut){if(playSequence)PlayNextCut();else{playTime=trimOut;playing=false;}}}
        if (frames!=null && frames.Count>0)
        {
            int index=Mathf.Clamp((int)(playTime*MultiplayerRecording.FPS),0,frames.Count-1);
            if(index!=loadedFrame){if(previewTexture==null)previewTexture=new Texture2D(2,2);previewTexture.LoadImage(frames[index]);loadedFrame=index;}
            if(computerScreen!=null)computerScreen.texture=previewTexture;
            if(editorScreen!=null)editorScreen.texture=previewTexture;
        }
        Set(trimLabel,"IN "+trimIn.ToString("0.0")+"s   OUT "+trimOut.ToString("0.0")+"s");
        Set(editorStatus,Crew.Notice+"\n"+(MultiplayerRecording.Instance?.Status??""));
        Set(brandPreview,Crew.State.commercialTitle);
        if(gradingMaterial!=null){gradingMaterial.SetFloat("_Brightness",Crew.State.brightness);gradingMaterial.SetFloat("_Contrast",Crew.State.contrast);gradingMaterial.SetFloat("_Saturation",Crew.State.saturation);}
    }
    private static Slider SimpleSlider(Transform parent,string name,Vector2 min,Vector2 max)
    {
        var obj=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Slider));obj.transform.SetParent(parent,false);Anchor(obj.transform,min,max);obj.GetComponent<Image>().color=new Color(.2f,.22f,.26f);
        var handle=new GameObject("Handle",typeof(RectTransform),typeof(Image));handle.transform.SetParent(obj.transform,false);handle.GetComponent<Image>().color=new Color(1,.68f,.15f);((RectTransform)handle.transform).sizeDelta=new Vector2(16,20);
        var slider=obj.GetComponent<Slider>();slider.handleRect=(RectTransform)handle.transform;slider.targetGraphic=handle.GetComponent<Image>();return slider;
    }
}
