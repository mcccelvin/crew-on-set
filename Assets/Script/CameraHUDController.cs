using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Shared authored viewfinder. The live image is separate from the UI and tape capture.
[DefaultExecutionOrder(300)]
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
    private bool previousCameraEnabled;
    private Player.Equipment.FilmCameraItem sourceEquipment;
    private readonly Image[] trackingMarks = new Image[8];
    private RectTransform styledTracking;
    private RectTransform exposureStrip;
    private Image recordChip;
    private SettingsCard settingsCard;
    public static readonly Color32 InstrumentPanel = new Color32(33, 40, 48, 240);
    public static readonly Color32 InstrumentBorder = new Color32(73, 84, 96, 255);
    public static readonly Color32 ReadoutInk = new Color32(238, 241, 244, 255);
    public static readonly Color32 MutedReadout = new Color32(174, 185, 197, 255);
    public static readonly Color32 CopperAccent = new Color32(213, 162, 110, 255);

    public struct State
    {
        public bool recording, card, manual, settingsOpen, gridEnabled;
        public float seconds, cardSeconds, cardCapacity, focus, kelvin, iso, aperture, shutterAngle, fps, tint;
        public int level, width, height, selectedSetting;
        public string menu;
    }

    public static CameraHUDController Create()
    {
        var prefab = Resources.Load<GameObject>("CameraHUD");
        var art = Resources.Load<CameraHUDArt>("CameraHUDArt");
        var hud = prefab != null ? Instantiate(prefab).GetComponent<CameraHUDController>() : Build(art != null ? art.focusArea : null, art != null ? art.exposure : null);
        hud.gameObject.name = "Camera Viewfinder - CAM FX3";
        hud.ApplyGameStyle();
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
            previousCameraEnabled = camera.enabled;
            sourceEquipment = camera.GetComponentInParent<Player.Equipment.FilmCameraItem>();
            preview = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "CAM FX3 live preview" };
            preview.Create();
            camera.targetTexture = preview;
            camera.rect = new Rect(0, 0, 1, 1);
            camera.aspect = 16f / 9f;
            liveImage.texture = preview;
            // FilmCameraItem owns an explicit processed render after its lens/pose update.
            // Unowned cameras (e.g. legacy multiplayer previews) retain automatic rendering.
            if (sourceEquipment != null) camera.enabled = false;
        }
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        ReleaseView();
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (source != null && sourceEquipment != null && preview != null)
            sourceEquipment.RenderCameraFrame(preview);
    }

    private void ReleaseView()
    {
        if (source != null)
        {
            source.targetTexture = previousTarget;
            source.rect = previousRect;
            source.aspect = previousAspect;
            source.enabled = previousCameraEnabled;
        }
        source = null;
        sourceEquipment = null;
        if (liveImage != null) liveImage.texture = null;
        if (preview != null) { preview.Release(); Destroy(preview); preview = null; }
    }
    private void OnDisable() { ReleaseView(); }
    private void OnDestroy() { ReleaseView(); }

    public void Refresh(State s)
    {
        recordLabel.text = s.recording ? "REC" : "STBY";
        recordLabel.color = s.recording ? new Color32(255, 111, 99, 255) : ReadoutInk;
        if (recordChip != null) recordChip.color = s.recording ? new Color32(79, 38, 40, 255) : new Color32(49, 63, 69, 255);
        int elapsedMilliseconds = Mathf.Max(0, Mathf.FloorToInt(s.seconds * 1000f));
        int total = elapsedMilliseconds / 1000;
        timerLabel.text = "<mspace=0.6em>" + (total / 3600).ToString("00") + ":" +
            (total / 60 % 60).ToString("00") + ":" + (total % 60).ToString("00") +
            "<size=75%><color=#D5A26E>." + (elapsedMilliseconds % 1000).ToString("000") + "</color></size></mspace>";
        formatLabel.text = s.width + " × " + s.height + "   " + s.fps.ToString("0.#") + "p";
        cardLabel.text = !s.card ? "NO SD CARD" : s.cardCapacity > 0
            ? "SD 1  " + Mathf.Min(s.cardCapacity, s.cardSeconds).ToString("0.#") + " / " + s.cardCapacity.ToString("0") + "s"
            : "SD 1  READY";
        cardLabel.color = s.card ? ReadoutInk : new Color32(255, 174, 102, 255);
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
        RefreshSettingsCard(s);
        string cardControl = s.cardCapacity > 0 ? KeyBadge("C") + " EJECT SD     " : "";
        helpLabel.text = KeyBadge("R") + " RECORD     " + cardControl + KeyBadge("LMB") + " EXIT     " +
            (s.level >= 2 ? KeyBadge("F2") + " SETTINGS" : KeyBadge("SCROLL") + " ZOOM");
        if (exposureStrip != null)
        {
            exposureStrip.gameObject.SetActive(s.level >= 3);
            Anchors(exposureStrip, new Vector2(exposure ? .28f : .78f, .023f), new Vector2(.98f, .083f));
            Anchors(wbLabel.rectTransform, new Vector2(.02f, .08f), new Vector2(exposure ? .25f : .98f, .92f));
        }
    }

    public void PlaceTracking(RectTransform target, Vector3 center, float width, float height)
    {
        StyleTracking(target);
        if (target.parent != viewport) target.SetParent(viewport, false);
        target.anchorMin = target.anchorMax = target.pivot = new Vector2(.5f, .5f);
        target.localScale = Vector3.one;
        target.anchoredPosition = new Vector2((center.x - .5f) * viewport.rect.width, (center.y - .5f) * viewport.rect.height);
        target.sizeDelta = new Vector2(Mathf.Clamp(width * viewport.rect.width + 20, 30, viewport.rect.width),
            Mathf.Clamp(height * viewport.rect.height + 20, 30, viewport.rect.height));
    }

    private static string KeyBadge(string key) => "<color=#D5A26E><b>[ " + key + " ]</b></color>";

    public void SetTrackingState(bool blocked)
    {
        foreach (var mark in trackingMarks)
            if (mark != null) mark.color = blocked ? new Color32(180, 49, 36, 255) : CrewPaperStyle.Gold;
    }

    private void StyleTracking(RectTransform target)
    {
        if (styledTracking == target) return;
        styledTracking = target;
        var oldFrame = target.GetComponent<Image>();
        if (oldFrame != null) oldFrame.enabled = false;
        for (int i = 0; i < trackingMarks.Length; i++)
        {
            var corner = new Vector2(i / 2 % 2, i / 4);
            var mark = target.Find("Production focus corner " + i) as RectTransform;
            if (mark == null) mark = Rect(target, "Production focus corner " + i, corner, corner);
            mark.pivot = corner; mark.anchoredPosition = Vector2.zero;
            mark.sizeDelta = i % 2 == 0 ? new Vector2(28, 3) : new Vector2(3, 28);
            var image = mark.GetComponent<Image>(); if (image == null) image = mark.gameObject.AddComponent<Image>();
            image.color = CrewPaperStyle.Gold; image.raycastTarget = false;
            var outline = mark.GetComponent<Outline>(); if (outline == null) outline = mark.gameObject.AddComponent<Outline>();
            outline.effectColor = CrewPaperStyle.Ink; outline.effectDistance = new Vector2(1, -1);
            trackingMarks[i] = image;
        }
    }

    private void ApplyGameStyle()
    {
        var surround = transform.Find("Black viewfinder surround");
        if (surround != null) surround.GetComponent<Image>().color = new Color32(20, 24, 30, 255);
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
        {
            label.color = ReadoutInk;
            label.raycastTarget = false;
            label.margin = Vector4.zero;
        }
        foreach (string name in new[] { "Top shade", "Bottom shade", "Time paper", "Focus paper" })
        {
            var bar = viewport.Find(name);
            if (bar != null) bar.gameObject.SetActive(false);
        }
        var oldControls = transform.Find("Controls paper");
        if (oldControls != null) oldControls.gameObject.SetActive(false);

        // Instrument readouts sit outside the picture, not across its top edge.
        var header = Surface(transform, "Instrument header", new Vector2(.07f, .938f), new Vector2(.93f, .988f));
        var brand = header.Find("Camera identity") as RectTransform;
        if (brand == null) brand = Label(header, "Camera identity", .016f, .08f, .22f, .92f, 18).rectTransform;
        PositionLabel(brand.GetComponent<TMP_Text>(), header, .016f, .08f, .175f, .92f, 16, TextAlignmentOptions.MidlineLeft);
        brand.GetComponent<TMP_Text>().text = "CREW / CAM FX3";
        brand.GetComponent<TMP_Text>().color = CopperAccent;
        PositionLabel(cardLabel, header, .19f, .08f, .355f, .92f, 20, TextAlignmentOptions.MidlineLeft);
        var clock = Surface(header, "Elapsed time chip", new Vector2(.37f, .07f), new Vector2(.63f, .93f));
        clock.GetComponent<Image>().color = new Color32(17, 22, 29, 255);
        clock.GetComponent<Outline>().effectColor = CopperAccent;
        PositionLabel(timerLabel, clock, .025f, .04f, .975f, .96f, 36, TextAlignmentOptions.Center);
        timerLabel.fontStyle = FontStyles.Bold;
        timerLabel.fontSizeMin = 32;
        timerLabel.richText = true;
        timerLabel.enableAutoSizing = true;
        timerLabel.enableWordWrapping = false;
        PositionLabel(formatLabel, header, .66f, .08f, .878f, .92f, 17, TextAlignmentOptions.Center);
        formatLabel.color = MutedReadout;
        var chip = Surface(header, "Recording chip", new Vector2(.90f, .16f), new Vector2(.984f, .84f));
        recordChip = chip.GetComponent<Image>();
        PositionLabel(recordLabel, chip, .02f, .03f, .98f, .97f, 19, TextAlignmentOptions.Center);
        var rule = header.Find("Copper rule") as RectTransform;
        if (rule == null) rule = Box(header, "Copper rule", Vector2.zero, new Vector2(1, 0), CopperAccent);
        rule.pivot = new Vector2(.5f, 0); rule.sizeDelta = new Vector2(0, 2);

        // A single low-profile focus ribbon replaces the large vertical cream card.
        var focus = Surface(viewport, "Focus ribbon", new Vector2(.02f, .023f), new Vector2(.245f, .083f));
        PositionLabel(focusLabel, focus, .18f, .08f, .49f, .92f, 19, TextAlignmentOptions.MidlineLeft);
        PositionLabel(distanceLabel, focus, .53f, .08f, .95f, .92f, 18, TextAlignmentOptions.MidlineRight);
        distanceLabel.color = MutedReadout;
        var focusIcon = viewport.Find("PSD focus area");
        if (focusIcon != null)
        {
            focusIcon.SetParent(focus, false);
            Anchors((RectTransform)focusIcon, new Vector2(.035f, .23f), new Vector2(.13f, .77f));
            focusIcon.GetComponent<RawImage>().color = CopperAccent;
        }
        exposureStrip = Surface(viewport, "Exposure telemetry", new Vector2(.28f, .023f), new Vector2(.98f, .083f));
        PositionLabel(wbLabel, exposureStrip, .02f, .08f, .25f, .92f, 18, TextAlignmentOptions.MidlineLeft);
        PositionLabel(shutterLabel, exposureStrip, .27f, .08f, .41f, .92f, 18, TextAlignmentOptions.Center);
        PositionLabel(apertureLabel, exposureStrip, .43f, .08f, .55f, .92f, 18, TextAlignmentOptions.Center);
        var exposure = (RectTransform)exposureLabel.transform.parent;
        exposure.SetParent(exposureStrip, false);
        Anchors(exposure, new Vector2(.57f, .08f), new Vector2(.73f, .92f));
        Anchors(exposureLabel.rectTransform, Vector2.zero, Vector2.one);
        exposureLabel.fontSize = exposureLabel.fontSizeMax = 18;
        var exposureIcon = exposure.Find("PSD exposure");
        if (exposureIcon != null) exposureIcon.gameObject.SetActive(false);
        PositionLabel(isoLabel, exposureStrip, .75f, .08f, .98f, .92f, 18, TextAlignmentOptions.MidlineRight);
        foreach (string name in new[] { "Crosshair horizontal", "Crosshair vertical" })
        {
            var crosshair = viewport.Find(name);
            if (crosshair != null) crosshair.GetComponent<Image>().color = new Color32(238, 241, 244, 180);
        }
        var footer = Surface(transform, "Instrument shortcuts", new Vector2(.07f, .014f), new Vector2(.93f, .058f));
        PositionLabel(helpLabel, footer, .02f, .05f, .98f, .95f, 18, TextAlignmentOptions.Center);
        helpLabel.richText = true;
        if (menuLabel != null)
        {
            var panel = (RectTransform)menuLabel.transform.parent;
            panel.anchorMin = new Vector2(.66f, .15f); panel.anchorMax = new Vector2(.98f, .87f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            StyleSurface(panel.GetComponent<Image>());
            menuLabel.fontSize = 22; menuLabel.fontStyle = FontStyles.Normal;
        }
    }

    private static void Anchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }

    private static void PositionLabel(TMP_Text label, Transform parent, float left, float bottom, float right,
        float top, int size, TextAlignmentOptions alignment)
    {
        label.transform.SetParent(parent, false);
        Anchors(label.rectTransform, new Vector2(left, bottom), new Vector2(right, top));
        label.fontSize = label.fontSizeMax = size; label.fontSizeMin = size * .75f;
        label.alignment = alignment; label.color = ReadoutInk;
        label.characterSpacing = 0; label.margin = Vector4.zero;
    }

    private static RectTransform Surface(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var panel = parent.Find(name) as RectTransform;
        if (panel == null) panel = Box(parent, name, min, max, InstrumentPanel);
        Anchors(panel, min, max);
        StyleSurface(panel.GetComponent<Image>());
        return panel;
    }

    private static void StyleSurface(Image image)
    {
        CrewPaperStyle.Round(image); image.color = InstrumentPanel;
        var outline = image.GetComponent<Outline>(); if (outline == null) outline = image.gameObject.AddComponent<Outline>();
        outline.enabled = true; outline.effectColor = InstrumentBorder; outline.effectDistance = new Vector2(1, -1);
        foreach (var shadow in image.GetComponents<Shadow>()) if (!(shadow is Outline)) shadow.enabled = false;
    }

    public static void StyleTutorialHint(RectTransform row, TMP_Text label, Image icon, Image underline)
    {
        StyleSurface(row.GetComponent<Image>());
        bool completed = label.text.StartsWith("<s>");
        label.color = completed ? MutedReadout : ReadoutInk;
        label.text = label.text.Replace("#9A421E", "#D5A26E");
        if (icon != null) icon.color = CopperAccent;
        if (underline != null) underline.color = CopperAccent;
    }

    private void RefreshSettingsCard(State state)
    {
        if (menuLabel == null) return;
        var panel = (RectTransform)menuLabel.transform.parent;
        bool detailed = state.settingsOpen && state.level >= 2;
        panel.gameObject.SetActive(detailed || !string.IsNullOrEmpty(state.menu));
        menuLabel.gameObject.SetActive(!detailed);
        if (detailed)
        {
            if (settingsCard == null) settingsCard = new SettingsCard(panel);
            settingsCard.Refresh(state, viewport);
        }
        else
        {
            if (settingsCard != null) settingsCard.Hide();
            // Retain the old text entry point for external/legacy HUD callers.
            menuLabel.text = (state.menu ?? "").Replace("#FFD866", "#D5A26E");
        }
    }

    // Runtime decoration works with the existing authored menu reference; no
    // prefab rebuild or new input ownership is required.
    private sealed class SettingsCard
    {
        private const float Width = 420f;
        private static readonly string[] Names = { "THIRDS GRID", "WHITE BALANCE", "TINT", "ISO", "APERTURE", "SHUTTER" };
        private static readonly string[] Descriptions = {
            "Use the thirds grid to balance your shot.",
            "Cool or warm the image to suit your lighting.",
            "Shift the image between green and magenta.",
            "Change sensitivity to brighten or darken the shot.",
            "Change brightness and depth of field.",
            "Adjust exposure time and motion blur."
        };
        private readonly RectTransform panel, content, description, controls, holdHint;
        private readonly CanvasGroup fade;
        private readonly RectTransform[] sections = new RectTransform[3];
        private readonly RectTransform[] rows = new RectTransform[6];
        private readonly Image[] rowImages = new Image[6], valueImages = new Image[6], selectionBars = new Image[6];
        private readonly TMP_Text[] titles = new TMP_Text[6], values = new TMP_Text[6], leftArrows = new TMP_Text[6], rightArrows = new TMP_Text[6];
        private readonly TMP_Text descriptionText, countText;
        private bool showing;

        public SettingsCard(RectTransform parent)
        {
            panel = parent;
            StyleSurface(panel.GetComponent<Image>());
            panel.GetComponent<Image>().color = new Color32(27, 33, 42, 252);
            var outline = panel.GetComponent<Outline>();
            outline.effectColor = new Color32(132, 107, 81, 255);
            outline.effectDistance = new Vector2(2, -2);
            fade = panel.GetComponent<CanvasGroup>();
            if (fade == null) fade = panel.gameObject.AddComponent<CanvasGroup>();
            fade.interactable = false; fade.blocksRaycasts = false;
            content = Pixels(panel, "Settings instrument card", 0, 0, Width, 300);
            PixelBox(content, "Copper top rule", 14, 0, Width - 28, 3, CopperAccent);
            var dial = PixelBox(content, "Adjustment icon", 18, 22, 36, 36, new Color32(53, 62, 73, 255), true);
            for (int i = 0; i < 3; i++)
            {
                PixelBox(dial, "Dial track " + i, 7, 9 + i * 9, 22, 2, MutedReadout);
                PixelBox(dial, "Dial stop " + i, i == 1 ? 19 : 10, 6 + i * 9, 4, 8, CopperAccent);
            }
            var heading = PixelText(content, "Settings title", "CAMERA SETTINGS", 68, 20, 272, 25, 21, CopperAccent);
            heading.characterSpacing = 1;
            countText = PixelText(content, "Available controls", "", 68, 49, 265, 18, 12, MutedReadout);
            var close = PixelBox(content, "F2 key badge", 348, 22, 54, 28, new Color32(48, 57, 68, 255), true, true);
            PixelText(close, "Close key", "F2", 0, 0, 54, 28, 15, CopperAccent, TextAlignmentOptions.Center);
            var live = PixelBox(content, "Live preview badge", 348, 55, 54, 18, new Color32(42, 69, 62, 255), true);
            PixelText(live, "Live preview", "LIVE", 0, 0, 54, 18, 10, new Color32(165, 215, 185, 255), TextAlignmentOptions.Center);
            PixelBox(content, "Header divider", 18, 78, Width - 36, 1, InstrumentBorder);
            string[] groups = { "COMPOSITION", "COLOUR", "EXPOSURE" };
            for (int i = 0; i < groups.Length; i++)
                sections[i] = PixelText(content, groups[i], groups[i], 18, 0, Width - 36, 18, 12, MutedReadout).rectTransform;
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = PixelBox(content, Names[i] + " row", 16, 0, Width - 32, 52, new Color32(39, 47, 59, 255), true);
                rowImages[i] = rows[i].GetComponent<Image>();
                selectionBars[i] = PixelBox(rows[i], "Selected copper edge", 0, 9, 3, 34, CopperAccent, true).GetComponent<Image>();
                PixelText(rows[i], "Control number", (i + 1).ToString("00"), 12, 0, 26, 52, 12, MutedReadout, TextAlignmentOptions.Center);
                titles[i] = PixelText(rows[i], "Control name", Names[i], 47, 0, 172, 52, 16, ReadoutInk);
                var value = PixelBox(rows[i], "Value badge", 234, 10, 142, 32, new Color32(29, 36, 46, 255), true, true);
                valueImages[i] = value.GetComponent<Image>();
                values[i] = PixelText(value, "Current value", "", 20, 0, 102, 32, 17, ReadoutInk, TextAlignmentOptions.Center);
                leftArrows[i] = PixelText(value, "Decrease hint", "<", 3, 0, 18, 32, 17, CopperAccent, TextAlignmentOptions.Center);
                rightArrows[i] = PixelText(value, "Increase hint", ">", 121, 0, 18, 32, 17, CopperAccent, TextAlignmentOptions.Center);
            }
            descriptionText = PixelText(content, "Selected control explanation", "", 18, 0, Width - 36, 36, 15, MutedReadout);
            descriptionText.fontStyle = FontStyles.Normal;
            descriptionText.enableWordWrapping = true;
            description = descriptionText.rectTransform;
            controls = Pixels(content, "Keyboard controls", 18, 0, Width - 36, 30);
            Key(controls, "Select keys", "UP / DOWN", 0, 98);
            PixelText(controls, "Select caption", "SELECT", 108, 0, 72, 30, 12, MutedReadout);
            Key(controls, "Adjust keys", "LEFT / RIGHT", 190, 116);
            PixelText(controls, "Adjust caption", "ADJUST", 316, 0, 68, 30, 12, MutedReadout);
            holdHint = PixelText(content, "Hold to adjust hint", "Hold LEFT / RIGHT for faster changes  |  F2 closes", 18, 0, Width - 36, 22, 12, MutedReadout).rectTransform;
        }

        public void Refresh(State state, RectTransform viewport)
        {
            content.gameObject.SetActive(true);
            if (!showing) { fade.alpha = 0; showing = true; }
            fade.alpha = Mathf.MoveTowards(fade.alpha, 1f, Time.unscaledDeltaTime * 10f);
            int count = state.level >= 4 ? 6 : state.level >= 3 ? 3 : 1;
            int selected = Mathf.Clamp(state.selectedSetting, 0, count - 1);
            countText.text = "CAM FX3  /  " + count + (count == 1 ? " CONTROL" : " CONTROLS") + " AVAILABLE";
            sections[1].gameObject.SetActive(count >= 3);
            sections[2].gameObject.SetActive(count == 6);
            float cursor = 92;
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].gameObject.SetActive(i < count);
                if (i >= count) continue;
                if (i == 0 || i == 1 || i == 3)
                {
                    Top(sections[i == 0 ? 0 : i == 1 ? 1 : 2], cursor);
                    cursor += 24;
                }
                Top(rows[i], cursor); cursor += 60;
                bool active = i == selected;
                rowImages[i].color = active ? new Color32(60, 52, 45, 255) : new Color32(39, 47, 59, 255);
                selectionBars[i].gameObject.SetActive(active);
                titles[i].color = active ? CopperAccent : ReadoutInk;
                valueImages[i].color = active ? CopperAccent : new Color32(29, 36, 46, 255);
                values[i].color = active ? new Color32(34, 28, 24, 255) : ReadoutInk;
                leftArrows[i].gameObject.SetActive(active); rightArrows[i].gameObject.SetActive(active);
                leftArrows[i].color = rightArrows[i].color = values[i].color;
                switch (i)
                {
                    case 0: values[i].text = state.gridEnabled ? "ON" : "OFF"; break;
                    case 1: values[i].text = state.kelvin.ToString("0") + " K"; break;
                    case 2: values[i].text = state.tint.ToString("+0;-0;0"); break;
                    case 3: values[i].text = state.iso.ToString("0"); break;
                    case 4: values[i].text = "F" + state.aperture.ToString("0.0"); break;
                    case 5: values[i].text = state.shutterAngle.ToString("0") + " deg"; break;
                }
            }
            Top(description, cursor + 6); descriptionText.text = Descriptions[selected]; cursor += 50;
            Top(controls, cursor); cursor += 36;
            Top(holdHint, cursor); cursor += 38;
            float scale = 1f;
            if (viewport != null && viewport.rect.width > 0 && viewport.rect.height > 0)
                scale = Mathf.Min(1f, viewport.rect.width * .34f / Width, viewport.rect.height * .78f / cursor);
            panel.anchorMin = panel.anchorMax = new Vector2(.98f, .89f);
            panel.pivot = Vector2.one; panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(Width, cursor) * scale;
            content.sizeDelta = new Vector2(Width, cursor); content.localScale = Vector3.one * scale;
        }

        public void Hide()
        {
            showing = false; fade.alpha = 1f;
            content.gameObject.SetActive(false);
        }

        private static void Top(RectTransform rect, float top) => rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -top);
        private static RectTransform Pixels(Transform parent, string name, float x, float top, float width, float height)
        {
            var rect = Rect(parent, name, new Vector2(0, 1), new Vector2(0, 1));
            rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -top); rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
        private static RectTransform PixelBox(Transform parent, string name, float x, float top, float width, float height, Color color, bool rounded = false, bool outlined = false)
        {
            var rect = Pixels(parent, name, x, top, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            if (rounded) CrewPaperStyle.Round(image);
            image.color = color; image.raycastTarget = false;
            if (outlined)
            {
                var outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = InstrumentBorder; outline.effectDistance = new Vector2(1, -1);
            }
            return rect;
        }
        private static TMP_Text PixelText(Transform parent, string name, string text, float x, float top, float width, float height, int size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var rect = Pixels(parent, name, x, top, width, height);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset; label.text = text; label.color = color;
            label.fontSize = label.fontSizeMax = size; label.fontSizeMin = size * .85f;
            label.fontStyle = FontStyles.Bold; label.alignment = alignment;
            label.enableAutoSizing = true; label.enableWordWrapping = false; label.raycastTarget = false;
            label.margin = Vector4.zero;
            return label;
        }
        private static void Key(Transform parent, string name, string text, float x, float width)
        {
            var badge = PixelBox(parent, name, x, 0, width, 28, new Color32(46, 55, 67, 255), true, true);
            PixelText(badge, "Key label", text, 0, 0, width, 28, 12, CopperAccent, TextAlignmentOptions.Center);
        }
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
        hud.ApplyGameStyle();
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
