using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;

public sealed class MultiplayerWorkbench : MonoBehaviour
{
    public static MultiplayerWorkbench Instance { get; private set; }
    private int page, tab, selected, frame = -1, sequenceIndex;
    private float trimIn, trimOut, playTime, lastTick;
    private bool playing, sequence;
    private Texture2D preview;
    private List<byte[]> frames;
    private MultiplayerRoleManager Crew => MultiplayerRoleManager.Instance;
    private static readonly string[] Brief = {
        "Welcome, crew! Make a coffee commercial together. You share B20,000; your career money is untouched.",
        "Director: hire and position actors, cue their performance with the megaphone, then call ACTION. Keep the product's story clear.",
        "Camera: buy your camera kit, choose Wide, Medium or Close, and press R to record. Capture five seconds of each. Match screen direction between shots.",
        "AV Technician: build the set and place coffee props and lights. Adjust Kelvin, intensity and direction so the product reads clearly.",
        "Editor: buy the editing kit, download the Camera player's takes, trim and arrange them. Use a wide shot for context, then closer details.",
        "Press TAB for your role tools, SHOP and ALMANAC. Finish the briefing, prepare the set, shoot, then let the Editor submit. Stay connected while sharing footage."
    };
    private void Awake() { Instance = this; }
    public void DrawBriefing()
    {
        GUILayout.Label("BOSS — CREW BRIEFING  " + (page + 1) + "/" + Brief.Length);
        GUILayout.Label(Brief[page], new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 19 }, GUILayout.MinHeight(125));
        var member = Crew.State.members.Find(m => m.id == PhotonNetwork.LocalPlayer.ActorNumber);
        if (member != null && member.briefed) { GUILayout.Label("Ready. Waiting for the other crew members to finish reading..."); return; }
        GUILayout.BeginHorizontal();
        if (page > 0 && GUILayout.Button("BACK", GUILayout.Height(38))) page--;
        if (GUILayout.Button(page == Brief.Length - 1 ? "READY TO WORK" : "NEXT", GUILayout.Height(38)))
        { if (page < Brief.Length - 1) page++; else Crew.Send(new CrewCommand { action = "briefed" }); }
        GUILayout.EndHorizontal();
    }
    public void Draw()
    {
        tab = GUILayout.Toolbar(tab, new[] { "ROLE TOOLS", "SHOP", "ALMANAC", "FOOTAGE / EDIT" }, GUILayout.Height(36));
        if (tab == 1) DrawShop();
        if (tab == 2) DrawAlmanac();
        if (tab == 3) DrawEditor();
    }
    public bool ShowRoleTools => tab == 0;
    private void DrawShop()
    {
        GUILayout.Label("ROLE SHOP — purchases use the shared room budget. Kits unlock controls; stage items are placed after selection.");
        foreach (string kind in new[] { "megaphone", "camera", "editing", "actor", "chair", "cup", "product", "table", "backdrop", "interior", "light" })
        {
            var role = MultiplayerContractManager.RoleFor(kind);
            bool kit = kind == "camera" || kind == "megaphone" || kind == "editing";
            bool owned = Crew.State.equipment.Contains(kind);
            bool allowed = Crew.HasRole(role);
            GUI.enabled = allowed && !owned && !Crew.State.recording && Crew.State.budget >= MultiplayerContractManager.Price(kind);
            if (GUILayout.Button(kind.ToUpperInvariant() + " — " + MultiplayerContractManager.RoleName(role) +
                (owned ? " — OWNED" : " — B" + MultiplayerContractManager.Price(kind)), GUILayout.Height(30)))
            {
                if (kit) Crew.Send(new CrewCommand { action = "buy", kind = kind });
                else MultiplayerCrewController.Local?.BeginPlace(kind);
            }
            GUI.enabled = true;
        }
    }
    private void DrawAlmanac()
    {
        GUILayout.Label("ALMANAC — free reference for everyone. YOUR ROLE marks your responsibilities.");
        foreach (var role in new[] { CrewRole.Director, CrewRole.Camera, CrewRole.AVTechnician, CrewRole.Editor })
        {
            GUILayout.Space(8);
            GUILayout.Label(MultiplayerContractManager.RoleName(role).ToUpperInvariant() + (Crew.HasRole(role) ? " — YOUR ROLE" : ""));
            GUILayout.Label(MultiplayerContractManager.Tasks(Crew.State, role));
        }
        GUILayout.Label("DIRECTING: LMB select actor, then click a seat/machine/cup to cue it. H clears selection. T moves; Q/E rotate the preview. Z cycles animations; B/N marks; K walk; J return; O stop.");
        GUILayout.Label("CAMERA: wide establishes place; medium shows the action; close highlights the coffee. Wheel zooms. Director calls ACTION, R starts/stops. Take limit: 30 seconds, 12 takes per room.");
        GUILayout.Label("LIGHTING: select light, Z/X Kelvin, C/V intensity, F power. Lower Kelvin is warm; higher is cool. Avoid clipped highlights. Use the role tools for tilt and diffusion.");
        GUILayout.Label("EDITING: download footage, preview, set IN/OUT, add to cut list. UP changes order; REMOVE removes a cut, not the source. Include five seconds of each shot size before submitting.");
    }
    private void Load(CrewShot shot)
    {
        var tape = MultiplayerRecording.Instance;
        if (tape == null || !tape.Has(shot.id)) { tape?.Download(shot); return; }
        frames = tape.Frames(shot.id); selected = shot.id; frame = -1;
        trimIn = 0; trimOut = shot.duration; playTime = 0; playing = false; sequence = false;
    }
    private void DrawEditor()
    {
        var state = Crew.State; var tape = MultiplayerRecording.Instance;
        GUILayout.Label(tape != null ? tape.Status : "Recorder loading...");
        GUILayout.Label("All crew can preview; only the Editor can change or submit the commercial. Silent 6 fps rushes, not a full-resolution video export.");
        foreach (var shot in state.shots)
        {
            GUI.enabled = shot.footageReady;
            if (GUILayout.Button("TAKE " + shot.id + " — " + shot.size + " — " + shot.duration.ToString("0.0") + "s — " +
                (tape != null && tape.Has(shot.id) ? "PREVIEW" : shot.footageReady ? "DOWNLOAD" : "SAVING"))) Load(shot);
            GUI.enabled = true;
        }
        var current = state.shots.Find(s => s.id == selected);
        bool editor = Crew.HasRole(CrewRole.Editor) && state.equipment.Contains("editing") && !state.recording;
        if (current != null && frames != null && frames.Count > 0)
        {
            int index = Mathf.Clamp((int)(playTime * MultiplayerRecording.FPS), 0, frames.Count - 1);
            if (index != frame)
            {
                if (preview == null) preview = new Texture2D(2, 2);
                preview.LoadImage(frames[index]); frame = index;
            }
            Rect rect = GUILayoutUtility.GetRect(320, 180, GUILayout.ExpandWidth(false));
            if (preview != null) GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
            if (GUILayout.Button(playing ? "PAUSE" : "PLAY TAKE")) { sequence = false; playing = !playing; if (playTime >= trimOut) playTime = trimIn; }
            float scrub = GUILayout.HorizontalSlider(playTime, 0, current.duration);
            if (Mathf.Abs(scrub - playTime) > .01f) { playTime = scrub; playing = false; sequence = false; }
            GUI.enabled = editor;
            GUILayout.Label("IN " + trimIn.ToString("0.0") + "s"); trimIn = GUILayout.HorizontalSlider(trimIn, 0, Mathf.Max(0, trimOut - .5f));
            GUILayout.Label("OUT " + trimOut.ToString("0.0") + "s"); trimOut = GUILayout.HorizontalSlider(trimOut, trimIn + .5f, Mathf.Max(trimIn + .5f, current.duration));
            if (GUILayout.Button("ADD TRIMMED TAKE TO CUT LIST")) Crew.Send(new CrewCommand { action = "cut", id = selected, number = trimIn, number2 = trimOut });
            GUI.enabled = true;
        }
        GUILayout.Label("COMMERCIAL CUT LIST");
        for (int i = 0; i < state.cuts.Count; i++)
        {
            var cut = state.cuts[i];
            GUILayout.BeginHorizontal();
            GUILayout.Label((i + 1) + ". Take " + cut.shot + "  " + cut.start.ToString("0.0") + "–" + cut.end.ToString("0.0") + "s");
            GUI.enabled = editor;
            if (i > 0 && GUILayout.Button("UP")) Crew.Send(new CrewCommand { action = "raiseCut", index = i });
            if (GUILayout.Button("REMOVE")) Crew.Send(new CrewCommand { action = "removeCut", index = i });
            GUI.enabled = true; GUILayout.EndHorizontal();
        }
        if (state.cuts.Count > 0 && GUILayout.Button("PLAY COMMERCIAL CUT LIST"))
        {
            var missing = state.cuts.Select(c => state.shots.Find(s => s.id == c.shot)).FirstOrDefault(s => s != null && !tape.Has(s.id));
            if (missing != null) tape.Download(missing);
            else { sequenceIndex = 0; PlayCut(); }
        }
        GUI.enabled = editor;
        if (GUILayout.Button("SUBMIT COMMERCIAL", GUILayout.Height(36))) Crew.Send(new CrewCommand { action = "submit" });
        GUI.enabled = true;
        if (!editor) GUILayout.Label("Editing requires the Editor role and the editing kit from SHOP. Finish recording before editing.");
    }
    private void PlayCut()
    {
        var state = Crew.State;
        if (sequenceIndex >= state.cuts.Count) { playing = sequence = false; return; }
        var cut = state.cuts[sequenceIndex]; var shot = state.shots.Find(s => s.id == cut.shot);
        if (shot == null) { playing = false; return; }
        Load(shot); trimIn = cut.start; trimOut = cut.end; playTime = trimIn; playing = sequence = true;
    }
    private void Update()
    {
        float now = Time.unscaledTime, delta = now - lastTick; lastTick = now;
        if (!playing || !RoleSelectionUI.Open || tab != 3) return;
        playTime += delta;
        if (playTime >= trimOut)
        { if (sequence) { sequenceIndex++; PlayCut(); } else { playTime = trimOut; playing = false; } }
    }
    private void OnDestroy() { if (preview != null) Destroy(preview); if (Instance == this) Instance = null; }
}
