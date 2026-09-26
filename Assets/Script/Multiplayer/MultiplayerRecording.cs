using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Session-only, low-bandwidth silent rushes. No career tape index or save data is changed.
public sealed class MultiplayerRecording : MonoBehaviour, IOnEventCallback
{
    private const byte Request = 180, Chunk = 181;
    private const int ChunkSize = 12000, MaxBytes = 4 * 1024 * 1024;
    public const int FPS = 6;
    public static MultiplayerRecording Instance { get; private set; }
    public string Status = "Silent preview footage: 320 x 180, 6 fps. Camera player must stay connected for downloads.";
    private readonly Dictionary<int, byte[]> tapes = new Dictionary<int, byte[]>();
    private readonly HashSet<int> sending = new HashSet<int>();
    private readonly Dictionary<int, float> requests = new Dictionary<int, float>();
    private TruePixelRecorder recorder;
    private int capturing, wanted, received;
    private byte[] incoming;
    private float lastReceive, nextAnnounce;
    private readonly HashSet<int> pendingReady = new HashSet<int>();
    private MultiplayerRoleManager Crew => MultiplayerRoleManager.Instance;
    private void OnEnable() { Instance = this; PhotonNetwork.AddCallbackTarget(this); }
    private void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); if (Instance == this) Instance = null; }

    private void Update()
    {
        var state = Crew?.State;
        if (state == null || !PhotonNetwork.InRoom) return;
        bool mine = state.recording && state.recorder == PhotonNetwork.LocalPlayer.ActorNumber;
        if (capturing != 0 && (!mine || state.activeShot != capturing)) SaveTake();
        if (mine && capturing == 0)
        {
            var view = MultiplayerCrewController.Local?.View;
            if (view == null) return;
            if (recorder == null) recorder = gameObject.AddComponent<TruePixelRecorder>();
            recorder.filmCamera = view; recorder.captureWidth = 320; recorder.captureHeight = 180;
            recorder.framesPerSecond = FPS; recorder.jpgQuality = 35;
            capturing = state.activeShot;
            if (!recorder.StartRecording()) Status = "Recording failed. Stop this take and try again.";
        }
        if (Time.unscaledTime >= nextAnnounce)
        {
            nextAnnounce = Time.unscaledTime + 1;
            foreach (int id in pendingReady.ToArray())
            {
                var take = state.shots.Find(s => s.id == id);
                if (take == null) continue;
                if (take.footageReady) pendingReady.Remove(id);
                else { Crew.Send(new CrewCommand { action = "footage", id = id }); break; }
            }
        }
        if (wanted != 0 && Time.unscaledTime - lastReceive > 20)
        { wanted = 0; incoming = null; Status = "Download timed out. Ask the Camera player to stay connected and retry."; }
    }

    private void SaveTake()
    {
        int id = capturing; capturing = 0;
        string file = recorder != null ? recorder.StopRecording() : "";
        if (string.IsNullOrEmpty(file)) { Status = "No frames saved for this take."; return; }
        try
        {
            string path = Path.Combine(Application.persistentDataPath, file);
            if (new FileInfo(path).Length > MaxBytes) { Status = "Take too large for crew transfer; local tape kept."; return; }
            byte[] bytes = File.ReadAllBytes(path);
            if (ReadFrames(bytes) == null) { Status = "Invalid take; record again."; return; }
            tapes[id] = bytes; pendingReady.Add(id);
            Status = "Take " + id + " saved. Editor can download it now.";
        }
        catch (Exception ex) { Status = "Could not save take: " + ex.Message; }
    }

    public bool Has(int id) => tapes.ContainsKey(id);
    public List<byte[]> Frames(int id) => tapes.TryGetValue(id, out var bytes) ? ReadFrames(bytes) : null;
    public void Download(CrewShot take)
    {
        if (Has(take.id)) { Status = "Take ready."; return; }
        if (wanted != 0) { Status = "Wait for the current download, or retry after timeout."; return; }
        if (!take.footageReady || !PhotonNetwork.CurrentRoom.Players.ContainsKey(take.owner))
        { Status = "Footage unavailable. The Camera player must finish saving and stay connected."; return; }
        wanted = take.id; received = 0; incoming = null; lastReceive = Time.unscaledTime;
        PhotonNetwork.RaiseEvent(Request, take.id, new RaiseEventOptions { TargetActors = new[] { take.owner } }, SendOptions.SendReliable);
        Status = "Downloading take " + take.id + "...";
    }

    public void OnEvent(EventData data)
    {
        if (!PhotonNetwork.InRoom || Crew?.State == null || !PhotonNetwork.CurrentRoom.Players.ContainsKey(data.Sender)) return;
        if (data.Code == Request && data.CustomData is int id)
        {
            var take = Crew.State.shots.Find(s => s.id == id && s.owner == PhotonNetwork.LocalPlayer.ActorNumber);
            if (take == null || !tapes.ContainsKey(id) || sending.Contains(data.Sender)) return;
            if (requests.TryGetValue(data.Sender, out float last) && Time.unscaledTime - last < 2) return;
            requests[data.Sender] = Time.unscaledTime;
            StartCoroutine(SendTape(data.Sender, id)); return;
        }
        if (data.Code != Chunk || !(data.CustomData is object[] values) || values.Length != 4 ||
            !(values[0] is int shot) || !(values[1] is int total) || !(values[2] is int offset) || !(values[3] is byte[] bytes)) return;
        var source = Crew.State.shots.Find(s => s.id == shot && s.owner == data.Sender);
        if (source == null || shot != wanted || total < 8 || total > MaxBytes || offset != received || bytes.Length == 0 || bytes.Length > ChunkSize || offset > total - bytes.Length) return;
        if (incoming == null) incoming = new byte[total];
        if (incoming.Length != total) return;
        Buffer.BlockCopy(bytes, 0, incoming, offset, bytes.Length); received += bytes.Length; lastReceive = Time.unscaledTime;
        Status = "Downloading take " + shot + ": " + received * 100 / total + "%";
        if (received != total) return;
        if (ReadFrames(incoming) != null) { tapes[shot] = incoming; Status = "Take " + shot + " ready to preview."; }
        else Status = "Invalid tape received; retry download.";
        incoming = null; wanted = 0;
    }

    private IEnumerator SendTape(int target, int id)
    {
        sending.Add(target);
        byte[] bytes = tapes[id];
        for (int offset = 0; offset < bytes.Length; offset += ChunkSize)
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.CurrentRoom.Players.ContainsKey(target)) break;
            var part = new byte[Math.Min(ChunkSize, bytes.Length - offset)];
            Buffer.BlockCopy(bytes, offset, part, 0, part.Length);
            PhotonNetwork.RaiseEvent(Chunk, new object[] { id, bytes.Length, offset, part },
                new RaiseEventOptions { TargetActors = new[] { target } }, SendOptions.SendReliable);
            yield return new WaitForSecondsRealtime(.15f);
        }
        sending.Remove(target);
    }

    private static List<byte[]> ReadFrames(byte[] bytes)
    {
        try
        {
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream))
            {
                int count = reader.ReadInt32();
                if (count < 1 || count > 240) return null;
                var frames = new List<byte[]>(count);
                for (int i = 0; i < count; i++)
                {
                    int length = reader.ReadInt32();
                    if (length < 4 || length > 128000 || length > stream.Length - stream.Position) return null;
                    var frame = reader.ReadBytes(length);
                    if (frame[0] != 255 || frame[1] != 216) return null;
                    frames.Add(frame);
                }
                return stream.Position == stream.Length ? frames : null;
            }
        }
        catch (Exception) { return null; }
    }
}
