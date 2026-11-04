using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shares the original RGB slider values; changing mode never paints a new colour.
public sealed class DirectorColorPicker : MonoBehaviour
{
    private Slider[] rgb;
    private Action<Color> apply;
    private Func<bool> allowed;
    private GameObject wheelPanel;
    private DirectorColorWheel wheel;
    private Slider brightness;
    private TMP_Text brightnessText;
    private CanvasGroup wheelGroup;
    private Button flip;
    private readonly Dictionary<GameObject,bool> rows=new Dictionary<GameObject,bool>();

    public void Initialize(Slider red,Slider green,Slider blue,Action<Color> paint,Func<bool> canPaint)
    {
        apply=paint;allowed=canPaint;
        if(wheelPanel!=null || red==null || green==null || blue==null)return;
        rgb=new[]{red,green,blue};
        var parent=red.transform.parent as RectTransform;
        if(parent==null)return;
        float low=float.PositiveInfinity,high=float.NegativeInfinity;
        foreach(var slider in rgb){float y=parent.InverseTransformPoint(slider.transform.position).y;low=Mathf.Min(low,y);high=Mathf.Max(high,y);}
        foreach(Transform child in parent)
            if(child.localPosition.y>=low-44 && child.localPosition.y<=high+44)rows[child.gameObject]=child.gameObject.activeSelf;
        flip=TabletFlipIcon.Create(parent,"Flip RGB sliders and colour wheel",Toggle);
        var button=(RectTransform)flip.transform;button.anchorMin=button.anchorMax=new Vector2(1,1);button.pivot=Vector2.one;
        button.anchoredPosition=new Vector2(-10,48);
        wheelPanel=new GameObject("Colour wheel mode",typeof(RectTransform),typeof(CanvasGroup));
        var root=(RectTransform)wheelPanel.transform;root.SetParent(parent,false);root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
        wheelGroup=wheelPanel.GetComponent<CanvasGroup>();
        wheel= new GameObject("Colour wheel",typeof(RectTransform),typeof(CanvasRenderer),typeof(DirectorColorWheel)).GetComponent<DirectorColorWheel>();
        wheel.transform.SetParent(root,false);wheel.rectTransform.anchorMin=wheel.rectTransform.anchorMax=new Vector2(.5f,.5f);
        wheel.rectTransform.sizeDelta=new Vector2(224,224);wheel.rectTransform.anchoredPosition=new Vector2(0,14);
        wheel.Changed=Paint;
        Label(root,"BRIGHTNESS",new Vector2(-149,-128),new Vector2(95,30),14);
        brightnessText=Label(root,"100%",new Vector2(168,-128),new Vector2(56,30),16);
        var track=Box(root,"Colour brightness",new Vector2(24,-128),new Vector2(230,22),new Color32(34,58,88,255));
        brightness=track.gameObject.AddComponent<Slider>();brightness.minValue=0;brightness.maxValue=1;
        var handle=Box(track,"Brightness handle",Vector2.zero,new Vector2(20,26),Color.white);
        handle.gameObject.AddComponent<Outline>().effectColor=new Color32(20,38,60,255);
        brightness.handleRect=handle;brightness.targetGraphic=handle.GetComponent<Image>();brightness.direction=Slider.Direction.LeftToRight;
        brightness.onValueChanged.AddListener(v=>wheel.SetValue(v));
        wheelPanel.SetActive(false);Refresh();
    }
    private void Toggle()
    {
        if(allowed!=null && !allowed())return;
        bool show=!wheelPanel.activeSelf;
        foreach(var row in rows)if(row.Key!=null)row.Key.SetActive(!show && row.Value);
        wheelPanel.SetActive(show);Refresh();
    }
    private void Paint(Color colour)
    {
        if(allowed!=null && !allowed())return;
        apply?.Invoke(colour);Refresh();
    }
    private void Update(){if(wheelPanel!=null && wheelPanel.transform.parent.gameObject.activeInHierarchy)Refresh();}
    private void Refresh()
    {
        if(wheel==null)return;
        bool canPaint=allowed==null || allowed();
        flip.interactable=canPaint;wheel.Interactable=canPaint;brightness.interactable=canPaint;wheelGroup.alpha=canPaint?1:.45f;
        var colour=new Color(rgb[0].normalizedValue,rgb[1].normalizedValue,rgb[2].normalizedValue);
        wheel.SetColorWithoutNotify(colour);brightness.SetValueWithoutNotify(wheel.Value);
        brightnessText.text=Mathf.RoundToInt(wheel.Value*100)+"%";
    }
    private static RectTransform Box(Transform parent,string name,Vector2 position,Vector2 size,Color tint)
    {
        var rect=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=size;rect.anchoredPosition=position;rect.GetComponent<Image>().color=tint;return rect;
    }
    private static TMP_Text Label(Transform parent,string caption,Vector2 position,Vector2 size,int fontSize)
    {
        var text=new GameObject(caption,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();text.transform.SetParent(parent,false);
        text.rectTransform.anchorMin=text.rectTransform.anchorMax=new Vector2(.5f,.5f);text.rectTransform.anchoredPosition=position;text.rectTransform.sizeDelta=size;
        text.text=caption;text.font=TMP_Settings.defaultFontAsset;text.fontSize=fontSize;text.color=new Color32(28,47,70,255);text.fontStyle=FontStyles.Bold;
        text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.enableWordWrapping=false;return text;
    }
}
