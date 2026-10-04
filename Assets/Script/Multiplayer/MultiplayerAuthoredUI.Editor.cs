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
    private RawImage computerScreen;
    private Texture2D previewTexture;
    private List<byte[]> frames;
    private bool playing;
    private float playTime, trimOut, lastPlayback;
    private int selectedTake, loadedFrame = -1, cachedShots = -1;
    private readonly List<GameObject> clipCards = new List<GameObject>();
    private int pendingDownload;
    private int requestedPreview;

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
        if (!recording.Has(id))
        {
            pendingDownload = id; requestedPreview = 0;
            if (!recording.Downloading) { recording.Download(take); requestedPreview = id; }
            Message = recording.Status; return;
        }
        pendingDownload = 0;
        frames = recording.Frames(id); selectedTake = id; playTime = 0; trimOut = take.duration; loadedFrame = -1; playing = false;
        if (Panel == "computer")
        {
            refs.Get<GameObject>("ComputerUIManager.recordingsGridPanel")?.SetActive(false);
            Activate(refs.Get<GameObject>("ComputerUIManager.videoPlayerPanel"));
            Set(refs.Get<TMP_Text>("ComputerUIManager.playerTitleText"), take.size + " — TAKE " + id);
            var loading = Find(panels["computer"].transform,"Loading"); if (loading != null) loading.gameObject.SetActive(false);
        }
    }

    private int editorRequest;
    private EditorManager sharedEditor;
    private readonly Dictionary<string, int> roomPaths = new Dictionary<string, int>();
    private void OpenEditor()
    {
        if (!Crew.HasRole(CrewRole.Editor)) return;
        pendingDownload = 0;
        int request = ++editorRequest;
        Panel = "editor-loading"; RoleSelectionUI.Open = true;
        StartCoroutine(LoadSharedEditor(request));
    }
    private System.Collections.IEnumerator LoadSharedEditor(int request)
    {
        var recording = MultiplayerRecording.Instance;
        if (recording == null) { Message = "Room recordings are not ready."; Close(); yield break; }
        var takes = Crew.State.shots.Where(s => s.uploaded && s.footageReady && s.contractLevel == Crew.State.contractLevel).ToArray();
        foreach (var take in takes)
        {
            if (!recording.Has(take.id))
            {
                while (recording.Downloading && request == editorRequest) yield return null;
                if (request != editorRequest) yield break;
                recording.Download(take);
                while (recording.Downloading && request == editorRequest)
                { Message = recording.Status; yield return null; }
                if (request != editorRequest) yield break;
                if (!recording.Has(take.id)) { Close(); Message = recording.Status; yield break; }
            }
        }
        if (request != editorRequest) yield break;
        var footage = new List<FootageData>();
        try
        {
            foreach (var take in takes)
            {
                var data = recording.EditorFootage(take);
                if (data == null) continue;
                footage.Add(data);
                roomPaths[System.IO.Path.Combine(Application.persistentDataPath, data.fileName)] = take.id;
            }
        }
        catch (Exception ex) { Close(); Message = "Could not prepare room footage: " + ex.Message; yield break; }
        if (editorView == null)
        {
            var prefab = Resources.Load<GameObject>("CrewUI/EditorGameplay");
            if (prefab == null) { Close(); Message = "Stop Play Mode and refresh Multiplayer UI Copies. The shared editor is missing."; yield break; }
            editorView = Instantiate(prefab);
            editorRefs = editorView.GetComponent<MultiplayerUIReferences>();
            editorRefs.RestoreArtwork();
            sharedEditor = editorView.GetComponentInChildren<EditorManager>(true);
            if (sharedEditor == null) { Close(); Message = "The copied editor has no EditorManager."; yield break; }
            sharedEditor.RoomFootage = footage;
            foreach (var canvas in editorView.GetComponentsInChildren<Canvas>(true))
            { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 80; canvas.enabled = true; }
            editorView.name = "Shared singleplayer Editor — room footage";
            var canvasRoot = editorView.GetComponentsInChildren<Canvas>(true).First(c => c.transform.parent == null || c.transform.parent.GetComponentInParent<Canvas>() == null);
            var back = Button(canvasRoot.transform, "Room editor back", "BACK TO COMPUTER", () => Open("computer"));
            Anchor(back.transform, new Vector2(.02f,.95f), new Vector2(.2f,.995f));
        }
        else
        {
            editorView.SetActive(true);
            sharedEditor.ImportRoomFootage(footage);
        }
        editorView.SetActive(true); Panel = "editor"; RoleSelectionUI.Open = true;
        Message = "Use the same editor controls as singleplayer. Export to review, then submit to your crew.";
    }
    private void RefreshEditorClips()
    {
        // The real EditorManager owns its clip bank and timeline. A room revision must not
        // destroy those controls (or the Editor's in-progress trims and branding).
    }
    public void SubmitSharedCommercial(float cameraScore, float lightScore, float seconds)
    {
        if (Panel != "editor" || sharedEditor == null || !Crew.HasRole(CrewRole.Editor) || Crew.State.recording) return;
        var cuts = new List<CrewCut>();
        foreach (var clip in sharedEditor.timelineContainer.GetComponentsInChildren<DraggableClip>())
        {
            if (!clip.isOnTimeline || !roomPaths.TryGetValue(clip.clipFilePath, out int shot)) continue;
            cuts.Add(new CrewCut { shot = shot, start = clip.startFrame / TapeSettings.framesPerSecond,
                end = clip.endFrame / TapeSettings.framesPerSecond });
        }
        if (cuts.Count == 0) { Message = "Add a room recording to the timeline before submitting."; return; }
        var result = sharedEditor.grader.GenerateGrades(cameraScore, lightScore, seconds);
        awaitingReviewRevision = Crew.State.revision;
        Crew.Send(new CrewCommand { action = "submit", value = Crew.State.contractLevel, result = result, cuts = cuts });
    }
    private void UpdatePlayback()
    {
        float delta=Time.unscaledTime-lastPlayback; lastPlayback=Time.unscaledTime;
        if (pendingDownload!=0 && MultiplayerRecording.Instance!=null && MultiplayerRecording.Instance.Has(pendingDownload))
        {int id=pendingDownload;pendingDownload=0;PreviewTake(id);}
        else if (Panel == "computer" && pendingDownload != 0 && MultiplayerRecording.Instance != null && !MultiplayerRecording.Instance.Downloading)
        {
            if (requestedPreview == pendingDownload) { pendingDownload = 0; Message = MultiplayerRecording.Instance.Status; }
            else
            {
                var take = Crew.State.shots.Find(s=>s.id==pendingDownload);
                requestedPreview = pendingDownload;
                if (take != null) MultiplayerRecording.Instance.Download(take); else pendingDownload = 0;
            }
        }
        if (Panel!="computer") return;
        if (playing) {playTime+=delta;if(playTime>=trimOut){playTime=trimOut;playing=false;}}
        if (frames!=null && frames.Count>0)
        {
            int index=Mathf.Clamp((int)(playTime*MultiplayerRecording.FPS),0,frames.Count-1);
            if(index!=loadedFrame){if(previewTexture==null)previewTexture=new Texture2D(2,2);previewTexture.LoadImage(frames[index]);loadedFrame=index;}
            if(computerScreen!=null)computerScreen.texture=previewTexture;
        }
    }
}
