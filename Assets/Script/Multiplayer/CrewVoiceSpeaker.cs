using UnityEngine;

public sealed class CrewVoiceSpeaker : MonoBehaviour
{
    public readonly CrewVoiceBuffer Buffer=new CrewVoiceBuffer();
    public float SpeakingUntil;
    private AudioSource source;
    private AudioClip clip;
    public float Volume {set{if(source!=null)source.volume=value;}}
    public static CrewVoiceSpeaker Create(Transform parent,int actor)
    {
        var root=new GameObject("Crew voice "+actor);root.transform.SetParent(parent,false);
        var speaker=root.AddComponent<CrewVoiceSpeaker>();
        speaker.source=root.AddComponent<AudioSource>();speaker.source.playOnAwake=false;
        speaker.source.spatialBlend=0;speaker.source.loop=true;speaker.source.priority=32;
        speaker.source.ignoreListenerPause=true;speaker.source.dopplerLevel=0;
        speaker.clip=AudioClip.Create("Live crew voice",CrewVoiceCodec.SampleRate,1,CrewVoiceCodec.SampleRate,true,speaker.Buffer.Read);
        speaker.source.clip=speaker.clip;speaker.source.Play();return speaker;
    }
    private void OnDestroy(){if(source!=null)source.Stop();if(clip!=null)Destroy(clip);}
}
