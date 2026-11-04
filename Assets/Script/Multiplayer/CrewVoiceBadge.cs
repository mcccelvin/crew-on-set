using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared by waiting-room, role-selection and pause crew cards.
public sealed class CrewVoiceBadge : MonoBehaviour
{
    private int actor;
    private Image background,slash;
    private TMP_Text caption;
    public static CrewVoiceBadge Attach(RectTransform rect)
    {
        var badge=rect.gameObject.AddComponent<CrewVoiceBadge>();
        badge.background=rect.gameObject.AddComponent<Image>();CrewPaperStyle.Round(badge.background);badge.background.raycastTarget=true;
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=badge.background;
        button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(badge.Click);
        var ink=(Color)CrewPaperStyle.Ink;
        badge.Mark("Microphone",new Vector2(15,3),new Vector2(7,13),ink);
        badge.Mark("Left arm",new Vector2(9.5f,-1),new Vector2(1.5f,9),ink);
        badge.Mark("Right arm",new Vector2(20.5f,-1),new Vector2(1.5f,9),ink);
        badge.Mark("Mic cradle",new Vector2(15,-5),new Vector2(12,1.5f),ink);
        badge.Mark("Stem",new Vector2(15,-8),new Vector2(1.5f,5),ink);
        badge.Mark("Foot",new Vector2(15,-10),new Vector2(8,1.5f),ink);
        badge.slash=badge.Mark("Muted slash",new Vector2(15,0),new Vector2(2.5f,25),new Color32(182,55,35,255));
        badge.slash.rectTransform.localRotation=Quaternion.Euler(0,0,-40);
        var text=new GameObject("Voice state",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(rect,false);text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;
        text.rectTransform.offsetMin=new Vector2(32,1);text.rectTransform.offsetMax=new Vector2(-4,-1);
        text.font=TMP_Settings.defaultFontAsset;text.fontSize=14;text.enableAutoSizing=true;text.fontSizeMin=10;text.fontSizeMax=14;
        text.alignment=TextAlignmentOptions.MidlineLeft;text.color=ink;text.raycastTarget=false;text.enableWordWrapping=false;
        text.fontStyle=FontStyles.Bold;badge.caption=text;return badge;
    }
    private Image Mark(string title,Vector2 position,Vector2 size,Color color)
    {
        var image=new GameObject(title,typeof(RectTransform),typeof(Image)).GetComponent<Image>();image.transform.SetParent(transform,false);
        image.rectTransform.anchorMin=image.rectTransform.anchorMax=new Vector2(0,.5f);
        image.rectTransform.anchoredPosition=position;image.rectTransform.sizeDelta=size;
        image.color=color;image.raycastTarget=false;return image;
    }
    public void Bind(Photon.Realtime.Player player)
    {actor=player?.ActorNumber??0;gameObject.SetActive(player!=null);}
    private void Click()
    {
        if(!PhotonNetwork.InRoom || !PhotonNetwork.CurrentRoom.Players.TryGetValue(actor,out var player))return;
        if(player.IsLocal)CrewVoiceChat.ToggleMicrophone();else CrewVoiceChat.TogglePeer(actor);
    }
    private void Update()
    {
        if(!PhotonNetwork.InRoom || !PhotonNetwork.CurrentRoom.Players.TryGetValue(actor,out var player)){gameObject.SetActive(false);return;}
        string text=CrewVoiceChat.Caption(player);caption.text=text;
        slash.enabled=text!="MIC ON" && text!="TALKING";
        background.color=text=="TALKING"?new Color32(169,222,183,255):new Color32(238,226,193,255);
    }
}
