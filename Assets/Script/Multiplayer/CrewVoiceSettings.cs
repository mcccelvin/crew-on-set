using UnityEngine;
using UnityEngine.InputSystem;

public sealed class CrewVoiceSettings
{
    public int mode=1,key;
    public float volume=1,gain=1;
    public string device="";
    public bool muted;
    internal static CrewVoiceSettings preview;
    public static CrewVoiceSettings Current=>preview??Load();
    public CrewVoiceSettings Copy()=>(CrewVoiceSettings)MemberwiseClone();
    public static CrewVoiceSettings Load()=>new CrewVoiceSettings {
        mode=Mathf.Clamp(GameSavePrefs.GetInt("Options.Voice.Mode",1),0,2),
        key=Mathf.Clamp(GameSavePrefs.GetInt("Options.Voice.Key",0),0,2),
        volume=Mathf.Clamp01(GameSavePrefs.GetFloat("Options.Voice.Volume",1)),
        gain=Mathf.Clamp(GameSavePrefs.GetFloat("Options.Voice.Gain",1),0,2),
        device=GameSavePrefs.GetString("Options.Voice.Device",""),
        muted=GameSavePrefs.GetInt("Options.Voice.Muted",0)!=0
    };
    public void Save()
    {
        GameSavePrefs.SetInt("Options.Voice.Mode",mode);GameSavePrefs.SetInt("Options.Voice.Key",key);
        GameSavePrefs.SetFloat("Options.Voice.Volume",volume);GameSavePrefs.SetFloat("Options.Voice.Gain",gain);
        GameSavePrefs.SetString("Options.Voice.Device",device??"");GameSavePrefs.SetInt("Options.Voice.Muted",muted?1:0);
        GameSavePrefs.Save();CrewVoiceChat.RefreshSettings();
    }
    public static string KeyName(int index)=>index==1?"RIGHT ALT":index==2?"F8":"LEFT ALT";
    public bool TalkHeld
    {
        get{var keyboard=Keyboard.current;return keyboard!=null &&
            (key==1?keyboard.rightAltKey.isPressed:key==2?keyboard.f8Key.isPressed:keyboard.leftAltKey.isPressed);}
    }
}
