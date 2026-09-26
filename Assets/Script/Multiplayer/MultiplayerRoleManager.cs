using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public sealed class MultiplayerRoleManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public static MultiplayerRoleManager Instance { get; private set; }
    public const string StateKey = "COS.Crew.V1";
    private const byte CommandEvent = 171, NoticeEvent = 172, SnapshotRequestEvent = 173;
    public CrewSession State { get; private set; }
    public string Notice { get; private set; } = "Connecting to crew...";
    public NetworkStudioFactory Studio { get; private set; }
    public event Action Changed;
    private readonly System.Collections.Generic.Dictionary<int, float> lastCommand = new System.Collections.Generic.Dictionary<int, float>();
    private float nextRecordTick;
    private float nextRoomSync;

    private void Awake()
    {
        Instance = this;
        Studio = gameObject.AddComponent<NetworkStudioFactory>();
        gameObject.AddComponent<RoleSelectionUI>();
        gameObject.AddComponent<MultiplayerRecording>();
        gameObject.AddComponent<MultiplayerAuthoredUI>();
    }

    private void Start()
    {
        RequestRoomState();
    }

    public void RequestRoomState()
    {
        nextRoomSync = Time.unscaledTime + 2f;
        if (!PhotonNetwork.InRoom) { Notice = "Join a Photon room from the main menu."; return; }
        ReadSnapshot();
        if (PhotonNetwork.IsMasterClient)
        {
            if (State == null) State = new CrewSession();
            ReconcileMembers();
            Publish();
        }
        else
        {
            Notice = "Waiting for the host's crew lobby...";
            PhotonNetwork.RaiseEvent(SnapshotRequestEvent, null,
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        }
    }

    public bool HasRole(int actor, CrewRole role) => State != null && State.members.Any(m => m.id == actor && (m.roles & (int)role) != 0);
    public bool HasRole(CrewRole role) => HasRole(PhotonNetwork.LocalPlayer.ActorNumber, role);
    public CrewObject Object(int id) => State?.objects.Find(o => o.id == id);

    public void Send(CrewCommand command)
    {
        if (!PhotonNetwork.InRoom) return;
        if (PhotonNetwork.IsMasterClient) Handle(command, PhotonNetwork.LocalPlayer.ActorNumber);
        else PhotonNetwork.RaiseEvent(CommandEvent, JsonUtility.ToJson(command),
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    public void OnEvent(EventData data)
    {
        if (!PhotonNetwork.InRoom) return;
        if (data.Code == SnapshotRequestEvent)
        {
            if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.Players.ContainsKey(data.Sender))
            { ReadSnapshot(); if (State == null) State = new CrewSession(); ReconcileMembers(); Publish(); }
            return;
        }
        if (data.Code == NoticeEvent && data.Sender == PhotonNetwork.MasterClient.ActorNumber)
        { Notice = data.CustomData as string ?? ""; Changed?.Invoke(); return; }
        if (data.Code != CommandEvent || !PhotonNetwork.IsMasterClient || !(data.CustomData is string json) || json.Length > 2048) return;
        try { Handle(JsonUtility.FromJson<CrewCommand>(json), data.Sender); }
        catch (Exception ex) { Debug.LogWarning("Crew command rejected: " + ex.Message); }
    }

    private void Reject(int sender, string message)
    {
        if (sender == PhotonNetwork.LocalPlayer.ActorNumber) { Notice = message; Changed?.Invoke(); }
        else PhotonNetwork.RaiseEvent(NoticeEvent, message, new RaiseEventOptions { TargetActors = new[] { sender } }, SendOptions.SendReliable);
    }

    private void Handle(CrewCommand c, int sender)
    {
        if (State == null || c == null || !PhotonNetwork.CurrentRoom.Players.ContainsKey(sender)) return;
        if (lastCommand.TryGetValue(sender, out float time) && Time.unscaledTime - time < .04f) return;
        lastCommand[sender] = Time.unscaledTime;
        var member = State.members.Find(m => m.id == sender);
        if (member == null) return;
        if (c.action == "role")
        {
            if (c.value != 1 && c.value != 2 && c.value != 4 && c.value != 8) return;
            if (State.phase == "lobby" && member.rolesLocked) { Reject(sender, "Unlock your roles before changing your selection."); return; }
            // Once production starts, only vacant roles can be claimed (for a departed crew member).
            if (State.phase != "lobby" && (member.roles & c.value) != 0) { Reject(sender, "Roles stay assigned during production."); return; }
            if (State.recording) { Reject(sender, "Finish the take before changing roles."); return; }
            if ((member.roles & c.value) != 0) member.roles &= ~c.value;
            else
            {
                if (State.members.Any(m => m.id != sender && (m.roles & c.value) != 0)) { Reject(sender, "That role is already assigned."); return; }
                member.roles |= c.value;
            }
            member.ready = false;
            member.rolesLocked = State.phase != "lobby";
            State.message = "Crew roles updated.";
            Publish(); return;
        }
        if (c.action == "lockRoles")
        {
            if (State.phase != "lobby" || (c.value != 0 && c.value != 1)) return;
            int target = c.id == 0 ? sender : c.id;
            if (target != sender && (sender != PhotonNetwork.MasterClient.ActorNumber || c.value != 0)) return;
            var selected = State.members.Find(m => m.id == target);
            if (selected == null) return;
            if (c.value == 1 && selected.roles == 0) { Reject(sender, "Choose at least one role first."); return; }
            selected.rolesLocked = c.value == 1;
            selected.ready = false;
            State.message = selected.rolesLocked ? "Roles locked. Press READY when you are ready." : "Roles unlocked. Choose roles, then lock them again.";
            Publish(); return;
        }
        if (c.action == "ready")
        {
            if (State.phase != "lobby") return;
            if (!member.rolesLocked || member.roles == 0) { Reject(sender, "Lock your roles before pressing READY."); return; }
            member.ready = !member.ready;
            State.message = "Crew readiness updated."; Publish(); return;
        }
        if (c.action == "start")
        {
            if (sender != PhotonNetwork.MasterClient.ActorNumber || State.phase != "lobby") return;
            if (!MultiplayerContractManager.CanStart(State)) { Reject(sender, "Need 2–4 players covering all four roles, with roles locked and everyone ready."); return; }
            foreach (var person in State.members) person.briefed = false;
            State.briefingPage = 0;
            State.phase = "briefing"; State.message = "Meet the Boss: read your crew briefing."; Publish(); return;
        }
        if (c.action == "briefingNext" && State.phase == "briefing")
        {
            // The first press for this page wins. Concurrent/stale presses cannot skip a page.
            if (c.index != State.briefingPage) return;
            State.briefingPage++;
            AdvanceBriefing(); Publish(); return;
        }
        if (State.phase != "build" && State.phase != "review") return;
        if (MultiplayerRoomActions.Handle(this, c, sender, out var actionError))
        { if (actionError != null) Reject(sender, actionError); else Publish(); return; }
        if (c.action == "buy")
        {
            if (c.kind != "camera" && c.kind != "megaphone" && c.kind != "editing") return;
            if (!HasRole(sender, MultiplayerContractManager.RoleFor(c.kind))) { Reject(sender, "Only the assigned role can buy this kit."); return; }
            int cost = MultiplayerContractManager.Price(c.kind);
            if (State.recording || State.equipment.Contains(c.kind) || State.budget < cost) { Reject(sender, "Already owned, recording, or insufficient team budget."); return; }
            State.budget -= cost; State.equipment.Add(c.kind);
            State.message = c.kind + " kit ready for its crew role."; Publish(); return;
        }
        if (c.action == "footage")
        {
            var take = State.shots.Find(s => s.id == c.id && s.owner == sender);
            if (take != null) { take.footageReady = true; Publish(); }
            return;
        }
        if (c.action == "cut" || c.action == "removeCut" || c.action == "raiseCut")
        {
            if (!HasRole(sender, CrewRole.Editor) || State.recording) return;
            if (c.action == "cut")
            {
                var take = State.shots.Find(s => s.id == c.id && s.footageReady && s.uploaded);
                if (take == null || State.cuts.Count >= 12 || !MultiplayerContractManager.Finite(new Vector3(c.number, c.number2, 0)) || c.number < 0 || c.number2 > take.duration || c.number2 - c.number < .5f) return;
                State.cuts.Add(new CrewCut { shot = c.id, start = c.number, end = c.number2 });
            }
            else if (c.index >= 0 && c.index < State.cuts.Count)
            {
                if (c.action == "removeCut") State.cuts.RemoveAt(c.index);
                else if (c.index > 0) { var cut = State.cuts[c.index]; State.cuts.RemoveAt(c.index); State.cuts.Insert(c.index - 1, cut); }
            }
            State.phase = "build"; State.message = "Editor updated the commercial cut list."; Publish(); return;
        }
        if (c.action == "sample" && State.recording && sender == State.recorder)
        {
            if (MultiplayerContractManager.Finite(c.position) && MultiplayerContractManager.Finite(c.rotation) && c.number >= 15 && c.number <= 90)
            {
                State.samples++;
                if (Studio.GoodFrame(c.position, Quaternion.Euler(c.rotation), c.number, State.shotSize, State)) State.goodSamples++;
            }
            return;
        }
        if (c.action == "action" && HasRole(sender, CrewRole.Director))
        {
            if (!MultiplayerRoomActions.Holding(State, sender, "megaphone")) { Reject(sender, "Buy and collect the Director megaphone first."); return; }
            if (State.recording) return;
            if (!State.objects.Any(o => o.kind == "actor") || !State.objects.Any(o => o.kind == "cup" || o.kind == "product"))
            { Reject(sender, "Place an actor and coffee product before calling ACTION."); return; }
            State.takeArmed = true; State.message = "ACTION! Camera operator: press R to record."; Publish(); return;
        }
        if (c.action == "record" && HasRole(sender, CrewRole.Camera))
        {
            if (!MultiplayerRoomActions.Holding(State, sender, "camera")) { Reject(sender, "Buy and collect a camera first."); return; }
            if (State.recording)
            {
                if (sender != State.recorder) return;
                FinishTake(); Publish(); return;
            }
            if (!State.takeArmed) { Reject(sender, "Ask the Director to call ACTION first."); return; }
            var camera = State.objects.Find(o => o.kind == "camera" && o.holder == sender);
            if (camera == null || !State.objects.Any(o => o.kind == "sd" && o.loadedInto == camera.id && o.tape == 0))
            { Reject(sender, "Collect an unused SD card, hold the camera and press C to insert it."); return; }
            if (c.kind != "Wide" && c.kind != "Medium" && c.kind != "Close") return;
            if (State.shots.Count >= 12) { Reject(sender, "The session holds 12 takes. Edit and submit the existing footage."); return; }
            State.activeShot = State.nextShot++;
            State.recording = true; State.phase = "build"; State.recordStarted = PhotonNetwork.Time;
            State.recorder = sender; State.samples = State.goodSamples = 0; State.shotSize = c.kind;
            State.message = "Recording " + c.kind + ". Hold a readable composition for at least 5 seconds."; Publish(); return;
        }
        if (c.action == "submit" && HasRole(sender, CrewRole.Editor) && !State.recording)
        {
            if (new[] { "Wide", "Medium", "Close" }.Any(size =>
                State.cuts.Where(cut => State.shots.Any(s => s.id == cut.shot && s.size == size && s.footageReady)).Sum(cut => cut.end - cut.start) < 5))
            { Reject(sender, "Add five seconds of Wide, Medium and Close footage to the Editor cut list."); return; }
            float grade = State.cuts.Average(cut => State.shots.First(s => s.id == cut.shot).score);
            State.phase = "review"; State.message = "TEAM COMMERCIAL: " + Mathf.RoundToInt(grade) + "/100. Review your coverage, or call ACTION to improve a take.";
            Publish(); return;
        }
        if (c.action == "spawn")
        {
            int price = MultiplayerContractManager.Price(c.kind);
            if ((c.kind == "backdrop" || c.kind == "interior") && State.equipment.Contains(c.kind)) price = 0;
            if (price < 0 || !HasRole(sender, MultiplayerContractManager.RoleFor(c.kind))) { Reject(sender, "This item belongs to another crew role."); return; }
            if (State.recording || State.objects.Count >= 32 || State.budget < price) { Reject(sender, "Cannot buy: recording, object limit, or insufficient team budget."); return; }
            if (!Studio.ValidPosition(c.position) || float.IsNaN(c.number) || float.IsInfinity(c.number)) { Reject(sender, "Place the item near the studio stage."); return; }
            if (!Studio.CanCreate(c.kind)) { Reject(sender, "Missing model assignment for " + c.kind + ". No money spent."); return; }
            if (c.kind == "interior" || c.kind == "backdrop")
            {
                if (State.objects.Any(o => o.kind == "actor" && o.target != 0)) { Reject(sender, "Stop actor furniture interactions before changing sets."); return; }
                State.objects.RemoveAll(o => o.kind == "interior" || o.kind == "backdrop");
            }
            State.budget -= price;
            if ((c.kind == "backdrop" || c.kind == "interior") && !State.equipment.Contains(c.kind)) State.equipment.Add(c.kind);
            State.objects.Add(new CrewObject { id = State.nextId++, revision = 1, kind = c.kind, position = c.position,
                rotation = new Vector3(0, c.number, 0), color = State.backdropColor, tier = Mathf.Clamp(c.value, 0, 2) });
            State.message = "Purchased " + c.kind + " with team budget."; Publish(); return;
        }
        var item = Object(c.id);
        if (item == null || !HasRole(sender, MultiplayerContractManager.RoleFor(item.kind))) { Reject(sender, "Your role cannot control that object."); return; }
        if (c.action == "move" || c.action == "delete")
        {
            if (State.recording || State.objects.Any(o => o.heldProduct == item.id || o.target == item.id)) { Reject(sender, "Stop recording or release the actor's interaction first."); return; }
            if (c.action == "delete") State.objects.Remove(item);
            else
            {
                if (!Studio.ValidPosition(c.position) || !MultiplayerContractManager.Finite(c.rotation)) return;
                item.position = c.position; item.rotation = c.rotation; item.action = "idle"; item.target = 0; item.revision++;
            }
            State.message = "Stage updated."; Publish(); return;
        }
        if (item.kind == "light" && c.action == "light")
        {
            if (float.IsNaN(c.number) || float.IsNaN(c.number2) || !MultiplayerContractManager.Finite(c.rotation)) return;
            item.powered = c.value != 0; item.kelvin = Mathf.Clamp(c.number, 2500, 8500);
            item.intensity = Mathf.Clamp(c.number2, 0, 8); item.diffusion = Mathf.Clamp(c.index, 0, 100); item.rotation = c.rotation;
            item.revision++; Publish(); return;
        }
        if (item.kind != "actor") return;
        if (!MultiplayerRoomActions.Holding(State, sender, "megaphone")) { Reject(sender, "Collect the megaphone before cueing actors."); return; }
        if (item.action == "walk" && Studio.Objects.TryGetValue(item.id, out var moving)) item.position = moving.transform.position;
        switch (c.action)
        {
            case "pose": item.performance = Mathf.Clamp(c.value, 0, 2); break;
            case "stop": item.action = "idle"; item.target = item.heldProduct = 0; break;
            case "startMark": item.start = item.position; item.hasStart = true; item.hasEnd = false; break;
            case "endMark":
                if (!item.hasStart || Vector3.Distance(item.start, item.position) < .2f) { Reject(sender, "Set START, reposition at least 0.2m, then set END."); return; }
                item.end = item.position; item.end.y = item.start.y; item.hasEnd = true; break;
            case "walk":
                if (!item.hasEnd) { Reject(sender, "Save START and END marks first."); return; }
                item.action = "walk"; item.target = 0; item.walkTime = PhotonNetwork.Time; item.position = item.start; break;
            case "return": if (item.hasStart) item.position = item.start; item.action = "idle"; item.target = 0; break;
            case "clear": item.hasStart = item.hasEnd = false; item.action = "idle"; break;
            case "interact":
                var target = Object(c.target);
                if (target == null || Vector3.Distance(item.position, target.position) > 12f) return;
                if (target.kind == "cup" || target.kind == "product")
                {
                    if (State.objects.Any(o => o.id != item.id && o.heldProduct == target.id)) { Reject(sender, "That product is already held."); return; }
                    item.heldProduct = target.id;
                }
                else if (target.kind == "chair" || target.kind == "interior")
                {
                    if (State.objects.Any(o => o.id != item.id && o.target == target.id && o.targetIndex == c.index)) { Reject(sender, "That seat or machine is occupied."); return; }
                    var interactable = Studio.Interaction(target.id, c.index);
                    if (interactable == null || interactable.action == Contract4Interactable.Action.Product) return;
                    item.action = "furniture"; item.target = target.id; item.targetIndex = c.index;
                }
                else return;
                break;
            default: return;
        }
        item.revision++; State.message = "Director cue: " + c.action; Publish();
    }

    private void FinishTake()
    {
        float duration = (float)(PhotonNetwork.Time - State.recordStarted);
        float frame = State.samples > 0 ? (float)State.goodSamples / State.samples : 0;
        bool actor = State.objects.Any(o => o.kind == "actor" && (o.performance != 0 || o.action != "idle" || o.heldProduct != 0));
        bool set = State.objects.Any(o => o.kind == "interior" || o.kind == "backdrop");
        float score = frame * 45 + Studio.LightingScore(State) + (actor ? 20 : 0) + (set ? 10 : 0);
        if (duration < 5) score *= .25f;
        State.shots.Add(new CrewShot { id = State.activeShot, owner = State.recorder, size = State.shotSize, duration = duration, score = score });
        var camera = State.objects.Find(o => o.kind == "camera" && o.holder == State.recorder);
        var card = camera != null ? State.objects.Find(o => o.loadedInto == camera.id) : null;
        if (card != null) { card.loadedInto = 0; card.tape = State.activeShot; card.revision++; }
        if (State.shots.Count > 12) State.shots.RemoveAt(0);
        State.recording = State.takeArmed = false;
        State.message = State.shotSize + " saved: " + Mathf.RoundToInt(score) + "/100. " +
            (duration < 5 ? "Too short: retake for at least 5 seconds." : "Framing " + Mathf.RoundToInt(frame * 100) + "%. Keep continuity for the next angle.");
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom) return;
        if ((State == null || !State.members.Any(m => m.id == PhotonNetwork.LocalPlayer.ActorNumber)) && Time.unscaledTime >= nextRoomSync)
            RequestRoomState();
        if (!PhotonNetwork.IsMasterClient || State == null || !State.recording || Time.unscaledTime < nextRecordTick) return;
        nextRecordTick = Time.unscaledTime + .5f;
        if (!PhotonNetwork.CurrentRoom.Players.ContainsKey(State.recorder) || PhotonNetwork.Time - State.recordStarted >= 30)
        { FinishTake(); Publish(); }
        else Publish(); // Preserve sample totals for a new host without restarting the take.
    }

    private void ReconcileMembers()
    {
        State.members.RemoveAll(m => !PhotonNetwork.CurrentRoom.Players.ContainsKey(m.id));
        foreach (var item in State.objects.Where(o => o.holder != 0 && !PhotonNetwork.CurrentRoom.Players.ContainsKey(o.holder)))
        { item.holder = item.slot = item.loadedInto = 0; item.revision++; }
        foreach (var player in PhotonNetwork.PlayerList)
            if (!State.members.Any(m => m.id == player.ActorNumber)) State.members.Add(new CrewMember { id = player.ActorNumber });
        AdvanceBriefing();
    }

    private void AdvanceBriefing()
    {
        if (State.phase == "briefing" && State.briefingPage >= MultiplayerContractManager.Briefing.Length)
        {
            foreach (var member in State.members) member.briefed = true;
            State.phase = "build"; State.message = "CONTRACT 4: shop for your role's kit, stage the coffee commercial, record and edit together.";
        }
    }

    private void Publish()
    {
        State.revision++;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { StateKey, JsonUtility.ToJson(State) } });
        Apply();
    }

    private void ReadSnapshot()
    {
        if (!PhotonNetwork.InRoom || !(PhotonNetwork.CurrentRoom.CustomProperties[StateKey] is string json)) return;
        try
        {
            var next = JsonUtility.FromJson<CrewSession>(json);
            if (next == null || next.schema != 1 || (State != null && next.revision <= State.revision)) return;
            State = next; Apply();
        }
        catch (Exception ex) { Notice = "Could not read room state: " + ex.Message; }
    }

    private void Apply() { Notice = State.message; Studio.Apply(State); Changed?.Invoke(); }
    public override void OnJoinedRoom() { RequestRoomState(); }
    public override void OnRoomPropertiesUpdate(Hashtable changed) { if (changed.ContainsKey(StateKey)) ReadSnapshot(); }
    public override void OnPlayerEnteredRoom(Photon.Realtime.Player player) { if (PhotonNetwork.IsMasterClient && State != null) { ReconcileMembers(); Publish(); } }
    public override void OnPlayerLeftRoom(Photon.Realtime.Player player) { if (PhotonNetwork.IsMasterClient && State != null) { ReconcileMembers(); State.message = "Crew member left. Reassign their roles from the crew menu."; Publish(); } }
    public override void OnMasterClientSwitched(Photon.Realtime.Player player) { ReadSnapshot(); if (PhotonNetwork.IsMasterClient && State != null) { ReconcileMembers(); State.message = "New host selected. Crew session continues."; Publish(); } }
    public override void OnLeftRoom() { SceneManager.LoadScene("Main Menu"); }
    public override void OnDisconnected(DisconnectCause cause) { SceneManager.LoadScene("Main Menu"); }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
