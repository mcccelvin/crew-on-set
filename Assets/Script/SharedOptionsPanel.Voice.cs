using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class SharedOptionsPanel
{
    private CrewVoiceSettings voiceDraft=new CrewVoiceSettings();
    private Slider incomingVoice,microphoneGain;
    private TMP_Text incomingValue,gainValue;
    private Button voiceMode,microphoneMute,microphoneDevice,voiceKey,micTest;
    private void BuildVoiceSettings()
    {
        var audio=layout.pages[1].transform;
        var controls=audio.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r=>r.name=="Voice controls");
        if(controls==null)
        {
            controls=Rect("Voice controls",audio,Vector2.zero,Vector2.zero);page=controls.gameObject;
            incomingVoice=MakeSlider("VOICE CHAT VOLUME",-162,0,1,out incomingValue);incomingValue.name="Voice volume value";
            microphoneGain=MakeSlider("MICROPHONE GAIN",-272,0,2,out gainValue);gainValue.name="Microphone gain value";
            string[] rows={"MICROPHONE MODE","MICROPHONE MUTE","INPUT DEVICE","PUSH-TO-TALK KEY","MICROPHONE TEST"};
            for(int i=0;i<rows.Length;i++)
            {
                float y=-382-i*94;
                Label(controls,rows[i],new Vector2(-305,y),new Vector2(440,60),30,true);
                Button(controls,rows[i]+" choice","",new Vector2(270,y),new Vector2(480,54),()=>{}).name=rows[i]+" choice";
            }
            var meter=Rect("Mic meter",controls,new Vector2(0,-854),new Vector2(980,22));
            var background=meter.gameObject.AddComponent<Image>();CrewPaperStyle.Round(background);background.color=new Color32(194,190,175,255);
            var fill=Rect("Level",meter,Vector2.zero,new Vector2(980,22));fill.pivot=new Vector2(0,.5f);fill.anchoredPosition=new Vector2(-490,0);
            var bar=fill.gameObject.AddComponent<Image>();CrewPaperStyle.Round(bar);bar.color=new Color32(39,123,99,255);
            var monitor=controls.gameObject.AddComponent<CrewVoiceMeter>();monitor.level=bar;
            monitor.status=Label(controls,"MIC IDLE",new Vector2(0,-900),new Vector2(980,40),22,false);
        }
        else
        {
            incomingVoice=controls.GetComponentsInChildren<Slider>(true).First(s=>s.name=="VOICE CHAT VOLUME");
            microphoneGain=controls.GetComponentsInChildren<Slider>(true).First(s=>s.name=="MICROPHONE GAIN");
            incomingValue=controls.Find("Voice volume value").GetComponent<TMP_Text>();
            gainValue=controls.Find("Microphone gain value").GetComponent<TMP_Text>();
        }
        voiceMode=controls.Find("MICROPHONE MODE choice").GetComponent<Button>();
        microphoneMute=controls.Find("MICROPHONE MUTE choice").GetComponent<Button>();
        microphoneDevice=controls.Find("INPUT DEVICE choice").GetComponent<Button>();
        voiceKey=controls.Find("PUSH-TO-TALK KEY choice").GetComponent<Button>();
        micTest=controls.Find("MICROPHONE TEST choice").GetComponent<Button>();
        foreach(var button in new[]{voiceMode,microphoneMute,microphoneDevice,voiceKey,micTest})
        {
            var label=button.GetComponentInChildren<TMP_Text>();label.enableWordWrapping=false;label.overflowMode=TextOverflowModes.Ellipsis;
        }
        controls.GetComponent<CrewVoiceMeter>().testButton=micTest;
        Bind(voiceMode,()=>{voiceDraft.mode=(voiceDraft.mode+1)%3;PreviewVoice();});
        Bind(microphoneMute,()=>{voiceDraft.muted=!voiceDraft.muted;PreviewVoice();});
        Bind(voiceKey,()=>{voiceDraft.key=(voiceDraft.key+1)%3;PreviewVoice();});
        Bind(microphoneDevice,()=>{
            var names=new[]{""}.Concat(Microphone.devices??new string[0]).ToArray();
            int index=System.Array.IndexOf(names,voiceDraft.device);voiceDraft.device=names[(index+1)%names.Length];PreviewVoice();
        });
        Bind(micTest,()=>{var channel=CrewVoiceChat.Ensure();channel.SetTesting(!channel.Testing);});
        incomingVoice.onValueChanged.RemoveAllListeners();incomingVoice.onValueChanged.AddListener(value=>{voiceDraft.volume=value;PreviewVoice();});
        microphoneGain.onValueChanged.RemoveAllListeners();microphoneGain.onValueChanged.AddListener(value=>{voiceDraft.gain=value;PreviewVoice();});
        MakeAudioScrollable(audio);page=layout.pages[1];
    }
    private void MakeAudioScrollable(Transform audio)
    {
        if(audio.Find("Audio settings viewport")!=null)return;
        var children=audio.Cast<Transform>().OfType<RectTransform>().ToArray();
        var viewport=Rect("Audio settings viewport",audio,Vector2.zero,new Vector2(1080,470));
        viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
        var content=Rect("Audio content",viewport,Vector2.zero,new Vector2(1080,1200));
        content.anchorMin=content.anchorMax=new Vector2(.5f,1);content.pivot=new Vector2(.5f,1);
        foreach(var child in children)
        {
            var position=child.anchoredPosition;child.SetParent(content,false);child.anchorMin=child.anchorMax=new Vector2(.5f,1);
            child.anchoredPosition=position-new Vector2(0,235);
        }
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;
        scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=40;scroll.movementType=ScrollRect.MovementType.Clamped;
        scroll.verticalScrollbar=null;
    }
    private void BeginVoicePreview()
    {
        voiceDraft=CrewVoiceSettings.Current.Copy();CrewVoiceSettings.preview=voiceDraft;
        CrewVoiceChat.Ensure();CrewVoiceChat.RefreshSettings();
        var scroll=layout.pages[1].transform.Find("Audio settings viewport")?.GetComponent<ScrollRect>();
        if(scroll!=null){scroll.StopMovement();scroll.content.anchoredPosition=Vector2.zero;}
    }
    private void PreviewVoice()
    {if(!previewOpen || closing)return;CrewVoiceSettings.preview=voiceDraft;CrewVoiceChat.RefreshSettings();RefreshVoiceSettings();}
    private void ResetVoiceDraft(){voiceDraft=new CrewVoiceSettings();PreviewVoice();}
    private void SaveVoiceDraft()=>voiceDraft.Save();
    private void EndVoicePreview()
    {CrewVoiceSettings.preview=null;if(CrewVoiceChat.Instance!=null)CrewVoiceChat.Instance.SetTesting(false);CrewVoiceChat.RefreshSettings();}
    private void RefreshVoiceSettings()
    {
        if(incomingVoice==null)return;
        incomingVoice.SetValueWithoutNotify(voiceDraft.volume);microphoneGain.SetValueWithoutNotify(voiceDraft.gain);
        incomingValue.text=Mathf.RoundToInt(voiceDraft.volume*100)+"%";gainValue.text=Mathf.RoundToInt(voiceDraft.gain*100)+"%";
        voiceMode.GetComponentInChildren<TMP_Text>().text=(voiceDraft.mode==0?"OFF":voiceDraft.mode==1?"PUSH TO TALK":"OPEN MIC")+"  >";
        microphoneMute.GetComponentInChildren<TMP_Text>().text=(voiceDraft.muted?"MUTED":"UNMUTED")+"  >";
        microphoneDevice.GetComponentInChildren<TMP_Text>().text=string.IsNullOrEmpty(voiceDraft.device)?"SYSTEM DEFAULT  >":voiceDraft.device+"  >";
        voiceKey.GetComponentInChildren<TMP_Text>().text=CrewVoiceSettings.KeyName(voiceDraft.key)+"  >";
        voiceKey.interactable=voiceDraft.mode==1;
    }
}
