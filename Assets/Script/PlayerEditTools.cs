using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System.IO;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class PlayerEditTools : MonoBehaviour
{
    public enum CameraMotionMode { None, SlowPushIn, SlowPullOut, PanLeft, PanRight }
    public enum GraphicAnimationMode { Cut, Fade, SlideUp, Pop }
    public enum TransitionMode { Cut, FadeInOut, DipToBlack }
    public enum MusicMode { None, Clean, Energy, Cinematic }

    public static PlayerEditTools Instance;

    [HideInInspector] public CameraMotionMode selectedCameraMotion = CameraMotionMode.None;
    [HideInInspector] public GraphicAnimationMode selectedGraphicAnimation = GraphicAnimationMode.Cut;
    [HideInInspector] public TransitionMode selectedTransition = TransitionMode.Cut;
    [HideInInspector] public MusicMode selectedMusic = MusicMode.None;

    [SerializeField] private GameObject toolsPanel;
    [SerializeField] private TextMeshProUGUI cameraMotionText;
    [SerializeField] private TextMeshProUGUI graphicAnimationText;
    [SerializeField] private TextMeshProUGUI transitionText;
    [SerializeField] private TextMeshProUGUI musicText;
    [SerializeField] private RectTransform cameraMotionButtonRect;
    [SerializeField] private RectTransform graphicAnimationButtonRect;
    [SerializeField] private RectTransform transitionButtonRect;
    [SerializeField] private RectTransform musicButtonRect;

    private void Awake()
    {
        Instance = this;
    }

    public void Initialize(GameObject brandingPanel)
    {
        if (brandingPanel == null) return;
        if (toolsPanel != null)
        {
            BindTool(cameraMotionButtonRect, CycleCameraMotion);
            BindTool(graphicAnimationButtonRect, CycleGraphicAnimation);
            BindTool(transitionButtonRect, CycleTransition);
            BindTool(musicButtonRect, CycleMusic);
            RefreshLabels();
            return;
        }

        MakeRoomForTools(brandingPanel.transform);

        toolsPanel = new GameObject("Player Edit Tools", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        toolsPanel.layer = brandingPanel.layer;
        toolsPanel.transform.SetParent(brandingPanel.transform, false);

        RectTransform panelRect = toolsPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 12f);
        panelRect.sizeDelta = new Vector2(-24f, 230f);

        Image panelImage = toolsPanel.GetComponent<Image>();
        panelImage.color = new Color32(29, 29, 29, 255);

        Outline outline = toolsPanel.GetComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1f, -1f);

        TextMeshProUGUI header = CreateText("Effects & audio\n<size=75%>Choose a treatment · Click a control to change it</size>", toolsPanel.transform, 16f, TextAlignmentOptions.Center);
        SetRect(header.rectTransform, new Vector2(0f, 0.73f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-12f, 0f));
        header.color = Color.white;

        cameraMotionText = CreateToolButton("Camera Motion", toolsPanel.transform, new Vector2(0.02f, 0.39f), new Vector2(0.49f, 0.72f), CycleCameraMotion, out cameraMotionButtonRect);
        graphicAnimationText = CreateToolButton("Graphic Animation", toolsPanel.transform, new Vector2(0.51f, 0.39f), new Vector2(0.98f, 0.72f), CycleGraphicAnimation, out graphicAnimationButtonRect);
        transitionText = CreateToolButton("Transition", toolsPanel.transform, new Vector2(0.02f, 0.04f), new Vector2(0.49f, 0.37f), CycleTransition, out transitionButtonRect);
        musicText = CreateToolButton("Music", toolsPanel.transform, new Vector2(0.51f, 0.04f), new Vector2(0.98f, 0.37f), CycleMusic, out musicButtonRect);


        RefreshLabels();
    }

    private void BindTool(RectTransform rect, UnityEngine.Events.UnityAction action)
    {
        if (rect == null) return;
        var button = rect.GetComponent<Button>();
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private string GetLevelFinishBrief()
    {
        int level = CampaignProgression.GetCurrentLevel();
        if (level == 2) return "GOKE: INTRO 2s / FOOTAGE 6s / OUTRO 2s";
        if (level == 3) return "TERRARI: INTRO 2s • BACK 7s • SIDE 7s • OVERALL 7s • OUTRO 2s = 25s";
        if (level == 4) return "KAPE STORY: WAVE > ACTION > SITTING | 15s | CLOSING BRAND | STYLE OPTIONAL";
        return "PRODUCT TARGET: PUSH IN • FADE/POP • FADE • CLEAN";
    }

    public void SetVisible(bool visible)
    {
        if (toolsPanel != null) toolsPanel.SetActive(visible);
    }

    public RectTransform GetCameraMotionButtonRect() { return cameraMotionButtonRect; }
    public RectTransform GetGraphicAnimationButtonRect() { return graphicAnimationButtonRect; }
    public RectTransform GetTransitionButtonRect() { return transitionButtonRect; }
    public RectTransform GetMusicButtonRect() { return musicButtonRect; }
    public void CycleCameraMotion()
    {
        selectedCameraMotion = (CameraMotionMode)(((int)selectedCameraMotion + 1) % 5);
        RefreshLabels();
        NotifyEditChanged();
    }

    public void CycleGraphicAnimation()
    {
        selectedGraphicAnimation = (GraphicAnimationMode)(((int)selectedGraphicAnimation + 1) % 4);
        RefreshLabels();
        NotifyEditChanged();
    }

    public void CycleTransition()
    {
        selectedTransition = (TransitionMode)(((int)selectedTransition + 1) % 3);
        RefreshLabels();
        NotifyEditChanged();
    }

    public void CycleMusic()
    {
        selectedMusic = (MusicMode)(((int)selectedMusic + 1) % 4);
        RefreshLabels();
        NotifyEditChanged();
    }

    private void NotifyEditChanged()
    {
        TruePixelPlayer[] players = FindObjectsOfType<TruePixelPlayer>(true);
        foreach (TruePixelPlayer player in players)
        {
            if (player != null) player.RefreshPlayerCreatedEffects();
        }

        if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
        {
            EditorTutorialManager.Instance.OnPlayerEditToolChanged();
        }
    }

    private void RefreshLabels()
    {
        if (cameraMotionText != null) cameraMotionText.text = "CAMERA MOTION\n<color=#E6B58D>" + GetCameraMotionName() + "</color>";
        if (graphicAnimationText != null) graphicAnimationText.text = "GRAPHIC ANIMATION\n<color=#E6B58D>" + GetGraphicAnimationName() + "</color>";
        if (transitionText != null) transitionText.text = "TRANSITION\n<color=#E6B58D>" + GetTransitionName() + "</color>";
        if (musicText != null) musicText.text = "MUSIC\n<color=#E6B58D>" + selectedMusic.ToString().ToUpper() + "</color>";
    }

    private string GetCameraMotionName()
    {
        if (selectedCameraMotion == CameraMotionMode.SlowPushIn) return "SLOW PUSH IN";
        if (selectedCameraMotion == CameraMotionMode.SlowPullOut) return "SLOW PULL OUT";
        if (selectedCameraMotion == CameraMotionMode.PanLeft) return "PAN LEFT";
        if (selectedCameraMotion == CameraMotionMode.PanRight) return "PAN RIGHT";
        return "OFF";
    }

    private string GetGraphicAnimationName()
    {
        if (selectedGraphicAnimation == GraphicAnimationMode.SlideUp) return "SLIDE UP";
        return selectedGraphicAnimation.ToString().ToUpper();
    }

    private string GetTransitionName()
    {
        if (selectedTransition == TransitionMode.FadeInOut) return "FADE IN / OUT";
        if (selectedTransition == TransitionMode.DipToBlack) return "DIP TO BLACK";
        return "STRAIGHT CUT";
    }

    private void MakeRoomForTools(Transform brandingPanel)
    {
        RectTransform[] children = brandingPanel.GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform child in children)
        {
            if (child == null || child.transform == brandingPanel || child.name != "Assets") continue;

            Vector2 offsetMin = child.offsetMin;
            offsetMin.y = Mathf.Max(offsetMin.y, 254f);
            child.offsetMin = offsetMin;
            break;
        }
    }

    private TextMeshProUGUI CreateToolButton(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction action, out RectTransform buttonRect)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.layer = parent.gameObject.layer;
        buttonObject.transform.SetParent(parent, false);

        buttonRect = buttonObject.GetComponent<RectTransform>();
        SetRect(buttonRect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = Color.white;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = EditorWorkspaceUI.Control;
        colors.highlightedColor = EditorWorkspaceUI.Hover;
        colors.pressedColor = EditorWorkspaceUI.Accent;
        colors.fadeDuration = 0.12f;
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.onClick.AddListener(action);

        TextMeshProUGUI label = CreateText(objectName + " Label", buttonObject.transform, 16f, TextAlignmentOptions.Center);
        SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 4f), new Vector2(-6f, -4f));
        return label;
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = parent.gameObject.layer;
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Normal;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private void SetRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}



internal static class EditorWorkspaceUI
{
    // Tutorial palette shared by the editor and campaign interfaces.
    public static readonly Color Panel = new Color32(224, 224, 224, 255);
    public static readonly Color Control = new Color32(48, 48, 48, 255);
    public static readonly Color Accent = new Color32(76, 163, 85, 255);
    public static readonly Color Hover = new Color32(76, 76, 76, 255);
    public static readonly Color Ink = new Color32(24, 24, 24, 255);
    public static void Place(RectTransform rect, float x0, float y0, float x1, float y1)
    {
        rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1);
        rect.offsetMin = new Vector2(8, 5); rect.offsetMax = new Vector2(-8, -5);
        rect.localScale = Vector3.one;
    }
    public static void Surface(Transform root)
    {
        // Preserve scene-authored panel sprites, headings, fonts and colors.
    }
    public static TextMeshProUGUI Label(Transform root, string name, string value, float x0, float y0, float x1, float y1)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = root.gameObject.layer; go.transform.SetParent(root, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset; text.text = value;
        text.fontSize = 22; text.enableAutoSizing = true; text.fontSizeMin = 14; text.fontSizeMax = 22;
        text.color = Color.white; text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        Place(text.rectTransform, x0,y0,x1,y1);
        return text;
    }
    public static Button Button(Transform root, string title, float x0, float y0, float x1, float y1, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
        go.layer = root.gameObject.layer; go.transform.SetParent(root, false);
        Place(go.GetComponent<RectTransform>(),x0,y0,x1,y1);
        var image = go.GetComponent<Image>(); image.color = Color.white;
        var button = go.GetComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.normalColor = Control;
        colors.highlightedColor = Hover; colors.pressedColor = Accent;
        colors.selectedColor = colors.highlightedColor; colors.fadeDuration = 0.12f; button.colors = colors;
        button.onClick.AddListener(action);
        var label = Label(go.transform,"Label",title,0,0,1,1);
        label.alignment = TextAlignmentOptions.Center; label.color = Color.white;
        return button;
    }
}

