using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Attaches to the existing LOAD panel, including when it is initially inactive.
public sealed class SaveLoadPanelHost : MonoBehaviour
{
    private void OnEnable(){GameSaveManager.Ensure().OpenMenu();}
    public static void AddLogoutButton()
    {
        if(string.IsNullOrEmpty(PlayerPrefs.GetString("PlayFabId","")))return;
        foreach(var button in FindObjectsOfType<Button>(true))
        {
            if(!string.Equals(button.name,"sign in",StringComparison.OrdinalIgnoreCase))continue;
            // Reuse the scene's account button, including its placement and size.
            button.onClick=new Button.ButtonClickedEvent();
            button.onClick.AddListener(()=>GameSaveManager.Ensure().Logout());
            ExportUIArt.Apply(button.GetComponent<Image>(),"redButton");
            var text=button.GetComponentInChildren<TextMeshProUGUI>(true);
            if(text==null)
            {
                var label=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(button.transform,false);
                var r=label.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
                text=label.GetComponent<TextMeshProUGUI>();
            }
            text.text="LOG OUT";text.fontSize=26;text.enableAutoSizing=true;text.fontSizeMin=12;text.fontSizeMax=26;
            text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;ExportUIArt.OutlineText(text);
        }
    }
}
