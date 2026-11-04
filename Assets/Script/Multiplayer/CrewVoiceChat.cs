using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable=ExitGames.Client.Photon.Hashtable;

// A room-only channel: no buffered events, career writes, or saved voice clips.
public sealed class CrewVoiceChat : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public const string StateKey="COS.Mic.V1";
    private const byte VoiceEvent=178;
    public static CrewVoiceChat Instance {get;private set;}
    private CrewVoiceSettings settings;
    private AudioClip microphone;
    private string device,room;
    private string[] devices=new string[0];
    private int readCursor,inputFrames,channels,lastPublished=-1;
    private ushort sequence;
    private float nextDevices,nextStart,startedAt,speechUntil,localSpeakingUntil;
    private bool fault,permissionRequested,wasSending,microphoneReady;
    public bool Testing {get;private set;}
    public float InputLevel {get;private set;}
    public string Instruction=>settings.mode==0?"MIC OFF  ·  Enable voice in Options > Audio":
        settings.muted?"MIC MUTED  ·  Click your microphone to unmute":settings.mode==1?
        "Hold "+CrewVoiceSettings.KeyName(settings.key)+" to talk  ·  Click a microphone to mute":
        "OPEN MIC  ·  Click your microphone to mute";
    public string MicrophoneStatus=>devices.Length==0?"NO MICROPHONE":fault?"MIC UNAVAILABLE":
        microphone!=null?"MIC READY":"MIC IDLE";
    private float[] input;
    private readonly float[] frame=new float[CrewVoiceCodec.Samples],decoded=new float[CrewVoiceCodec.Samples];
    private float previousSample,previousFiltered;
    private readonly Dictionary<int,CrewVoiceSpeaker> speakers=new Dictionary<int,CrewVoiceSpeaker>();
    private readonly HashSet<int> locallyMuted=new HashSet<int>();
    private readonly Dictionary<int,float> tokens=new Dictionary<int,float>(),tokenAt=new Dictionary<int,float>();
    private readonly RaiseEventOptions routing=new RaiseEventOptions {
        Receivers=ReceiverGroup.Others,CachingOption=EventCaching.DoNotCache,SequenceChannel=1
    };

    public static CrewVoiceChat Ensure()
    {
        if(Instance==null)new GameObject("Crew voice channel").AddComponent<CrewVoiceChat>();
        return Instance;
    }
    private void Awake()
    {
        if(Instance!=null && Instance!=this){Destroy(gameObject);return;}
        Instance=this;DontDestroyOnLoad(gameObject);settings=CrewVoiceSettings.Current.Copy();
    }
    public static void RefreshSettings()
    {
        if(Instance==null)return;
        var updated=CrewVoiceSettings.Current.Copy();
        if(updated.device!=Instance.settings.device)
        {Instance.StopMicrophone();Instance.nextStart=0;Instance.fault=false;}
        Instance.settings=updated;
    }
    public static void ToggleMicrophone()
    {
        var config=CrewVoiceSettings.Current.Copy();
        if(config.mode==0){config.mode=1;config.muted=false;}else config.muted=!config.muted;
        if(CrewVoiceSettings.preview!=null){CrewVoiceSettings.preview.mode=config.mode;CrewVoiceSettings.preview.muted=config.muted;RefreshSettings();}
        else config.Save();
    }
    public void SetTesting(bool enabled)
    {Testing=enabled;if(enabled){nextStart=0;EnsureMicrophone();}else if(!PhotonNetwork.InRoom)StopMicrophone();}
    public static bool MutedForMe(int actor)=>Instance!=null && Instance.locallyMuted.Contains(actor);
    public static void TogglePeer(int actor)
    {
        var channel=Ensure();
        if(!channel.locallyMuted.Add(actor))channel.locallyMuted.Remove(actor);
        if(channel.speakers.TryGetValue(actor,out var speaker))speaker.Buffer.Clear();
    }
    public static string Caption(Photon.Realtime.Player player)
    {
        if(player==null)return "";
        if(player.IsLocal)
        {
            var channel=Instance;
            if(channel==null)return "NO VOICE";
            int state=channel.LocalState();
            return state==0?"MIC OFF":state==1?"NO MIC":state==2?"MUTED":
                Time.unscaledTime<channel.localSpeakingUntil?"TALKING":"MIC ON";
        }
        if(MutedForMe(player.ActorNumber))return "MUTED";
        if(!(player.CustomProperties[StateKey] is int value))return "NO VOICE";
        if(value==0)return "MIC OFF";if(value==1)return "NO MIC";if(value==2)return "MUTED";
        return Instance!=null && Instance.speakers.TryGetValue(player.ActorNumber,out var remote) &&
            Time.unscaledTime<remote.SpeakingUntil?"TALKING":"MIC ON";
    }
    private int LocalState()=>settings.mode==0?0:settings.muted?2:devices.Length==0 || fault?1:3;
    private void BindRoom()
    {
        ClearRoom();room=PhotonNetwork.CurrentRoom?.Name;sequence=0;lastPublished=-1;nextDevices=0;
    }
    private void ClearRoom()
    {
        foreach(var speaker in speakers.Values)if(speaker!=null)Destroy(speaker.gameObject);
        speakers.Clear();locallyMuted.Clear();tokens.Clear();tokenAt.Clear();room=null;
        StopMicrophone();localSpeakingUntil=0;wasSending=false;
    }
    public override void OnJoinedRoom()=>BindRoom();
    public override void OnLeftRoom()=>ClearRoom();
    public override void OnDisconnected(DisconnectCause cause)=>ClearRoom();
    public override void OnPlayerLeftRoom(Photon.Realtime.Player other)
    {
        if(speakers.TryGetValue(other.ActorNumber,out var speaker))Destroy(speaker.gameObject);
        speakers.Remove(other.ActorNumber);locallyMuted.Remove(other.ActorNumber);
        tokens.Remove(other.ActorNumber);tokenAt.Remove(other.ActorNumber);
    }
    private void Update()
    {
        if(Instance!=this)return;
        bool joined=PhotonNetwork.InRoom;
        if(joined && room!=PhotonNetwork.CurrentRoom.Name)BindRoom();
        else if(!joined && room!=null)ClearRoom();
        if(Time.unscaledTime>=nextDevices)
        {
            nextDevices=Time.unscaledTime+2;
            devices=Microphone.devices??new string[0];
            if(microphone!=null && !devices.Contains(device))StopMicrophone();
        }
        bool capture=Application.isFocused && (Testing || joined && settings.mode!=0 && !settings.muted);
        if(capture)EnsureMicrophone();else StopMicrophone();
        if(microphone!=null)Capture();else InputLevel=0;
        foreach(var entry in speakers)
            if(entry.Value!=null)entry.Value.Volume=locallyMuted.Contains(entry.Key)?0:settings.volume;
        if(joined)
        {
            int status=LocalState();
            if(status!=lastPublished)
            {PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable{{StateKey,status}});lastPublished=status;}
        }
    }
    private void EnsureMicrophone()
    {
        if(!Application.isFocused || Time.unscaledTime<nextStart)return;
        string selected=string.IsNullOrEmpty(settings.device)?devices.FirstOrDefault():settings.device;
        if(microphone!=null && device==selected)return;
        StopMicrophone();nextStart=Time.unscaledTime+3;
        if(string.IsNullOrEmpty(selected) || !devices.Contains(selected)){fault=devices.Length>0;return;}
#if UNITY_ANDROID && !UNITY_EDITOR
        if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            if(!permissionRequested){permissionRequested=true;UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);}
            fault=true;return;
        }
#endif
#if UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        if(!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {if(!permissionRequested){permissionRequested=true;Application.RequestUserAuthorization(UserAuthorization.Microphone);}fault=true;return;}
#endif
        try
        {
            Microphone.GetDeviceCaps(selected,out int min,out int max);
            int rate=CrewVoiceCodec.SampleRate;if(min>0)rate=Mathf.Max(rate,min);if(max>0)rate=Mathf.Min(rate,max);
            microphone=Microphone.Start(selected,true,1,rate);device=selected;
            if(microphone==null){fault=true;return;}
            channels=Mathf.Max(1,microphone.channels);inputFrames=Mathf.Max(1,Mathf.RoundToInt(microphone.frequency*.06f));
            input=new float[inputFrames*channels];readCursor=0;startedAt=Time.unscaledTime;fault=false;microphoneReady=false;
            previousSample=previousFiltered=0;wasSending=false;
        }
        catch(Exception error)
        {fault=true;StopMicrophone();Debug.LogWarning("Voice microphone could not start: "+error.Message);}
    }
    private void StopMicrophone()
    {
        if(microphone!=null)
        {
            if(!string.IsNullOrEmpty(device))Microphone.End(device);
            Destroy(microphone);microphone=null;
        }
        InputLevel=0;wasSending=false;
    }
    private void Capture()
    {
        int position=Microphone.GetPosition(device);
        if(position<0 || !Microphone.IsRecording(device))
        {fault=true;StopMicrophone();nextStart=Time.unscaledTime+5;return;}
        if(!microphoneReady)
        {
            if(position==0)
            {if(Time.unscaledTime-startedAt>3){fault=true;StopMicrophone();nextStart=Time.unscaledTime+5;}return;}
            microphoneReady=true;
        }
        bool canSend=PhotonNetwork.InRoom && PhotonNetwork.IsMessageQueueRunning && Application.isFocused &&
            !settings.muted && (settings.mode==2 || settings.mode==1 && settings.TalkHeld);
        // Never send microphone samples from before a push-to-talk press.
        if(canSend && !wasSending)readCursor=position;
        wasSending=canSend;
        int available=(position-readCursor+microphone.samples)%microphone.samples;
        if(available>inputFrames*3){readCursor=(position-inputFrames+microphone.samples)%microphone.samples;available=inputFrames;}
        for(int block=0;block<2 && available>=inputFrames;block++)
        {
            if(!microphone.GetData(input,readCursor)){fault=true;return;}
            readCursor=(readCursor+inputFrames)%microphone.samples;available-=inputFrames;
            double energy=0;
            for(int i=0;i<frame.Length;i++)
            {
                float offset=i*(inputFrames-1f)/(frame.Length-1);int a=(int)offset,b=Math.Min(a+1,inputFrames-1);
                float sample=0;for(int ch=0;ch<channels;ch++)sample+=Mathf.Lerp(input[a*channels+ch],input[b*channels+ch],offset-a);
                sample/=channels;
                float filtered=sample-previousSample+.995f*previousFiltered;previousSample=sample;previousFiltered=filtered;
                float value=Mathf.Clamp(filtered*settings.gain,-1,1);frame[i]=value;energy+=value*value;
            }
            InputLevel=(float)Math.Sqrt(energy/frame.Length);
            if(InputLevel>.008f)speechUntil=Time.unscaledTime+.25f;
            if(canSend && (settings.mode==1 || Time.unscaledTime<speechUntil))
            {
                byte[] packet=CrewVoiceCodec.Encode(frame,sequence++,PhotonNetwork.ServerTimestamp);
                if(PhotonNetwork.RaiseEvent(VoiceEvent,packet,routing,new SendOptions{Reliability=false,Encrypt=true}) && InputLevel>.008f)
                    localSpeakingUntil=Time.unscaledTime+.3f;
            }
        }
    }
    public void OnEvent(EventData ev)
    {
        if(ev.Code!=VoiceEvent || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom.Name!=room ||
            ev.Sender==PhotonNetwork.LocalPlayer.ActorNumber || settings.volume<=0 || locallyMuted.Contains(ev.Sender))return;
        if(!PhotonNetwork.CurrentRoom.Players.ContainsKey(ev.Sender) || !(ev.CustomData is byte[] packet) ||
            !CrewVoiceCodec.Decode(packet,decoded,out ushort seq,out int timestamp))return;
        if(!CrewVoiceCodec.Fresh(timestamp,PhotonNetwork.ServerTimestamp))return;
        float now=Time.unscaledTime;
        float budget=tokens.TryGetValue(ev.Sender,out float prior)?Mathf.Min(4,prior+(now-tokenAt[ev.Sender])*18):4;
        tokenAt[ev.Sender]=now;if(budget<1){tokens[ev.Sender]=budget;return;}tokens[ev.Sender]=budget-1;
        if(!speakers.TryGetValue(ev.Sender,out var speaker))
        {speaker=CrewVoiceSpeaker.Create(transform,ev.Sender);speakers.Add(ev.Sender,speaker);}
        if(speaker.Buffer.Push(decoded,seq))
        {
            double energy=0;foreach(float value in decoded)energy+=value*value;
            if(energy/decoded.Length>.000064)speaker.SpeakingUntil=now+.3f;
            speaker.Volume=settings.volume;
        }
    }
    private void OnApplicationFocus(bool focused){if(!focused)StopMicrophone();}
    private void OnApplicationPause(bool paused){if(paused)StopMicrophone();}
    private void OnApplicationQuit()=>StopMicrophone();
    public override void OnDisable(){base.OnDisable();StopMicrophone();}
    private void OnDestroy(){if(Instance==this){ClearRoom();Instance=null;}}
}
