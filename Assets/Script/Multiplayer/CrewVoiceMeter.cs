using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CrewVoiceMeter : MonoBehaviour
{
    public Image level;
    public TMP_Text status;
    public Button testButton;
    private float shown;
    private void Update()
    {
        var voice=CrewVoiceChat.Instance;
        shown=Mathf.Lerp(shown,voice!=null?Mathf.Clamp01(voice.InputLevel*5):0,Time.unscaledDeltaTime*14);
        if(level!=null){level.rectTransform.localScale=new Vector3(shown,1,1);level.color=shown>.9f?new Color32(192,63,43,255):new Color32(39,123,99,255);}
        if(status!=null)status.text=voice!=null?voice.MicrophoneStatus:"MIC IDLE";
        if(testButton!=null)testButton.GetComponentInChildren<TMP_Text>().text=voice!=null && voice.Testing?"STOP MIC TEST":"TEST MIC";
    }
}
