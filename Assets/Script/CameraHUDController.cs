using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Shared authored viewfinder. The live image is separate from the UI and tape capture.
public sealed class CameraHUDController : MonoBehaviour
{
    public RectTransform viewport;
    public RawImage liveImage;
    public TMP_Text recordLabel, timerLabel, formatLabel, cardLabel, focusLabel, distanceLabel;
    public TMP_Text shutterLabel, apertureLabel, isoLabel, wbLabel, exposureLabel, menuLabel, helpLabel;
    private Camera source;
    private RenderTexture preview, previousTarget;
    private Rect previousRect;
    private float previousAspect;

    public struct State
    {
        public bool recording, card, manual;
        public float seconds, focus, kelvin, iso, aperture, shutterAngle, fps;
        public int level, width, height;
        public string menu;
    }

    public static CameraHUDController Create()
    {
        var prefab = Resources.Load<GameObject>("CameraHUD");
        var art = Resources.Load<CameraHUDArt>("CameraHUDArt");
        var hud = prefab != null ? Instantiate(prefab).GetComponent<CameraHUDController>() : Build(art != null ? art.focusArea : null, art != null ? art.exposure : null);
        hud.gameObject.name = "Camera Viewfinder - CAM FX3";
        if (hud.menuLabel != null)
        {
            var panel = hud.menuLabel.transform.parent as RectTransform;
            panel.anchorMin = new Vector2(.66f, .15f);
            panel.anchorMax = new Vector2(.98f, .87f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            var background = panel.GetComponent<Image>();
            if (background != null) background.color = new Color(.035f, .045f, .06f, .94f);
            hud.menuLabel.fontSize = 22;
            hud.menuLabel.fontStyle = FontStyles.Normal;
        }
        hud.gameObject.SetActive(false);
        return hud;
    }

    public void Show(Camera camera)
    {
        if (camera == null) return;
        if (source != camera || preview == null)
        {
            ReleaseView();
            source = camera;
            previousTarget = camera.targetTexture;
            previousRect = camera.rect;
            previousAspect = camera.aspect;
            preview = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "CAM FX3 live preview" };
            preview.Create();
            camera.targetTexture = preview;
            camera.rect = new Rect(0, 0, 1, 1);
            camera.aspect = 16f / 9f;
            liveImage.texture = preview;
        }
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        ReleaseView();
        gameObject.SetActive(false);
    }

    private void ReleaseView()
    {
        if (source != null)
        {
            source.targetTexture = previousTarget;
            source.rect = previousRect;
            source.aspect = previousAspect;
        }
        source = null;
        if (liveImage != null) liveImage.texture = null;
        if (preview != null) { preview.Release(); Destroy(preview); preview = null; }
    }
    private void OnDisable() { ReleaseView(); }
    private void OnDestroy() { ReleaseView(); }

    public void Refresh(State s)
    {
        recordLabel.text = s.recording ? "● REC" : "STBY";
        recordLabel.color = s.recording ? new Color(1f, .18f, .16f) : Color.white;
        int total = Mathf.Max(0, Mathf.FloorToInt(s.seconds));
        timerLabel.text = (total / 3600).ToString("00") + ":" + (total / 60 % 60).ToString("00") + ":" + (total % 60).ToString("00");
        formatLabel.text = s.width + " × " + s.height + "   " + s.fps.ToString("0.#") + "p";
        cardLabel.text = s.card ? "SD 1  READY" : "NO SD CARD";
        cardLabel.color = s.card ? Color.white : new Color(1f, .65f, .2f);
        focusLabel.text = s.manual ? "MF" : "AF-C";
        distanceLabel.text = s.focus > 0 ? s.focus.ToString("0.0") + " m" : "";
        bool exposure = s.level >= 4;
        shutterLabel.text = exposure ? "1/" + Mathf.Max(1, Mathf.RoundToInt(s.fps * 360f / Mathf.Max(1f, s.shutterAngle))) : "SHUTTER AUTO";
        apertureLabel.text = exposure ? "F" + s.aperture.ToString("0.0") : "IRIS AUTO";
        isoLabel.text = exposure ? "ISO " + s.iso.ToString("0") : "ISO AUTO";
        wbLabel.text = s.level >= 3 ? "WB " + s.kelvin.ToString("0") + "K" : "WB AUTO";
        float ev = exposure ? Mathf.Log(Mathf.Max(.001f, s.iso / 800f * 16f / (s.aperture * s.aperture) * s.shutterAngle / 180f), 2f) : 0;
        exposureLabel.text = ev.ToString("+0.0;-0.0;0.0") + " EV";
        // Beginner controls remain hidden until their lesson unlocks them.
        shutterLabel.gameObject.SetActive(exposure);
        apertureLabel.gameObject.SetActive(exposure);
        isoLabel.gameObject.SetActive(exposure);
        exposureLabel.transform.parent.gameObject.SetActive(exposure);
        wbLabel.gameObject.SetActive(s.level >= 3);
        menuLabel.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(s.menu));
        menuLabel.text = s.menu ?? "";
        helpLabel.text = s.level >= 2 ? "R  RECORD     LMB  EXIT     F2  SETTINGS" : "R  RECORD     LMB  EXIT     SCROLL  ZOOM";
    }

    public void PlaceTracking(RectTransform target, Vector3 center, float width, float height)
    {
        if (target.parent != viewport) target.SetParent(viewport, false);
        target.anchorMin = target.anchorMax = target.pivot = new Vector2(.5f, .5f);
        target.localScale = Vector3.one;
        target.anchoredPosition = new Vector2((center.x - .5f) * viewport.rect.width, (center.y - .5f) * viewport.rect.height);
        target.sizeDelta = new Vector2(Mathf.Clamp(width * viewport.rect.width + 20, 30, viewport.rect.width),
            Mathf.Clamp(height * viewport.rect.height + 20, 30, viewport.rect.height));
    }

    // Called by the editor authoring tool; also handles an as-yet-unbaked project.
    public static CameraHUDController Build(Texture focusArt, Texture exposureArt)
    {
        var root = new GameObject("CameraHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CameraHUDController));
        root.layer = 5;
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 150;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var hud = root.GetComponent<CameraHUDController>();
        Box(root.transform, "Black viewfinder surround", new Vector2(0,0), new Vector2(1,1), Color.black);
        var inset = Rect(root.transform, "Inset", new Vector2(.07f,.07f), new Vector2(.93f,.93f));
        hud.viewport = Rect(inset, "Live view - 16 by 9", Vector2.zero, Vector2.one);
        var aspect = hud.viewport.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = 16f / 9f;
        hud.liveImage = hud.viewport.gameObject.AddComponent<RawImage>(); hud.liveImage.raycastTarget = false;
        hud.viewport.gameObject.AddComponent<RectMask2D>();
        Box(hud.viewport,"Top shade",new Vector2(0,.90f),Vector2.one,new Color(0,0,0,.58f));
        Box(hud.viewport,"Bottom shade",Vector2.zero,new Vector2(1,.09f),new Color(0,0,0,.58f));
        hud.cardLabel = Label(hud.viewport,"SD status",.025f,.91f,.22f,.98f,23);
        hud.formatLabel = Label(hud.viewport,"Actual recording format",.30f,.91f,.70f,.98f,23,TextAlignmentOptions.Center);
        hud.recordLabel = Label(hud.viewport,"Record status",.77f,.91f,.97f,.98f,26,TextAlignmentOptions.Right);
        hud.timerLabel = Label(hud.viewport,"Recording time",.75f,.84f,.97f,.90f,24,TextAlignmentOptions.Right);
        hud.focusLabel = Label(hud.viewport,"Focus mode",.025f,.22f,.14f,.28f,26);
        hud.distanceLabel = Label(hud.viewport,"Focus distance",.025f,.15f,.19f,.21f,22);
        Icon(hud.viewport,"PSD focus area",focusArt,.025f,.30f,.070f,.36f);
        Box(hud.viewport,"Crosshair horizontal",new Vector2(.489f,.4988f),new Vector2(.511f,.5012f),Color.white);
        Box(hud.viewport,"Crosshair vertical",new Vector2(.4993f,.481f),new Vector2(.5007f,.519f),Color.white);
        hud.shutterLabel = Label(hud.viewport,"Shutter",.025f,.012f,.19f,.078f,25);
        hud.apertureLabel = Label(hud.viewport,"Aperture",.22f,.012f,.35f,.078f,25);
        var exposure = Rect(hud.viewport,"Exposure",new Vector2(.39f,.012f),new Vector2(.59f,.078f));
        Icon(exposure,"PSD exposure",exposureArt,0,0,.20f,1);
        hud.exposureLabel = Label(exposure,"EV",.25f,0,1,1,24);
        hud.wbLabel = Label(hud.viewport,"White balance",.61f,.012f,.80f,.078f,24);
        hud.isoLabel = Label(hud.viewport,"ISO",.81f,.012f,.98f,.078f,25,TextAlignmentOptions.Right);
        var menu = Box(hud.viewport,"Camera settings",new Vector2(.20f,.21f),new Vector2(.65f,.82f),new Color(0,0,0,.88f));
        hud.menuLabel = Label(menu,"Live settings",.04f,.04f,.96f,.96f,24);
        hud.menuLabel.alignment = TextAlignmentOptions.TopLeft;
        menu.gameObject.SetActive(false);
        hud.helpLabel = Label(root.transform,"Controls",.15f,.013f,.85f,.057f,21,TextAlignmentOptions.Center);
        return hud;
    }
    private static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max)
    {
        var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer=5; rect.SetParent(parent,false); rect.anchorMin=min; rect.anchorMax=max;
        rect.offsetMin=rect.offsetMax=Vector2.zero; return rect;
    }
    private static RectTransform Box(Transform parent,string name,Vector2 min,Vector2 max,Color color)
    {
        var rect=Rect(parent,name,min,max); var img=rect.gameObject.AddComponent<Image>();
        img.color=color; img.raycastTarget=false; return rect;
    }
    private static TMP_Text Label(Transform parent,string name,float x,float y,float r,float t,int size,TextAlignmentOptions align=TextAlignmentOptions.Left)
    {
        var rect=Rect(parent,name,new Vector2(x,y),new Vector2(r,t));
        var label=rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font=TMP_Settings.defaultFontAsset;
        label.fontSize=size; label.fontStyle=FontStyles.Bold; label.color=Color.white; label.alignment=align;
        label.enableAutoSizing=true; label.fontSizeMin=size*.65f; label.fontSizeMax=size;
        label.raycastTarget=false; label.enableWordWrapping=false; return label;
    }
    private static void Icon(Transform parent,string name,Texture texture,float x,float y,float r,float t)
    {
        if(texture==null)return;
        var rect=Rect(parent,name,new Vector2(x,y),new Vector2(r,t));
        var img=rect.gameObject.AddComponent<RawImage>(); img.texture=texture; img.raycastTarget=false;
    }
}
