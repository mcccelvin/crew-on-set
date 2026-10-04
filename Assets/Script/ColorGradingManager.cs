using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ColorGradingManager : MonoBehaviour
{
    // The beginner controls are bounded: every available look earns full credit.
    public const float BeginnerBrightnessMin = .75f, BeginnerBrightnessMax = 1.25f;
    public const float BeginnerContrastMin = .75f, BeginnerContrastMax = 1.50f;
    public const float BeginnerSaturationMin = .65f, BeginnerSaturationMax = 1.40f;

    [Header("Video Output")]
    public RawImage computerScreen;
    private Material gradingMat;
    public DraggableClip SelectedClip { get; private set; }
    internal EditorEditState CaptureUndo()
    {
        bool perClip = CampaignProgression.GetCurrentLevel() == 4;
        float b = brightnessSlider != null ? brightnessSlider.value : 1f;
        float c = contrastSlider != null ? contrastSlider.value : 1f;
        float s = saturationSlider != null ? saturationSlider.value : 1f;
        bool fade = fadeInToggle != null && fadeInToggle.isOn;
        return new EditorEditState {
            key = "grade", fingerprint = fade + (perClip ? "" : "/" + EditorEditState.Number(b) + "/" + EditorEditState.Number(c) + "/" + EditorEditState.Number(s)),
            restore = () => {
                if (this == null) return;
                if (!perClip) {
                    brightnessSlider?.SetValueWithoutNotify(b); contrastSlider?.SetValueWithoutNotify(c); saturationSlider?.SetValueWithoutNotify(s);
                }
                fadeInToggle?.SetIsOnWithoutNotify(fade);
                RefreshAfterUndo();
            }
        };
    }
    public void RefreshAfterUndo()
    {
        if (CampaignProgression.GetCurrentLevel() == 4)
        {
            if (SelectedClip == null || !SelectedClip.isOnTimeline) SelectedClip = DraggableClip.Selected;
            if (SelectedClip != null && SelectedClip.isOnTimeline) {
                brightnessSlider?.SetValueWithoutNotify(SelectedClip.gradeBrightness);
                contrastSlider?.SetValueWithoutNotify(SelectedClip.gradeContrast);
                saturationSlider?.SetValueWithoutNotify(SelectedClip.gradeSaturation);
            }
        }
        appliedB = appliedC = appliedS = float.NaN;
        UpdateReadouts();
    }
    public void SelectClip(DraggableClip clip)
    {
        SelectedClip = clip;
        if (clip == null) return;
        brightnessSlider.SetValueWithoutNotify(clip.gradeBrightness);
        contrastSlider.SetValueWithoutNotify(clip.gradeContrast);
        saturationSlider.SetValueWithoutNotify(clip.gradeSaturation);
        appliedB = appliedC = appliedS = float.NaN;
        var player = FindObjectOfType<CommercialCompiler>()?.editorPlayer;
        if (player != null)
            player.PlaySequence(new System.Collections.Generic.List<ClipSegment> {
                new ClipSegment { path = clip.clipFilePath, startFrame = clip.startFrame, endFrame = clip.endFrame,
                    uiStartX = GokeSequence.Left(clip), uiWidth = clip.GetComponent<RectTransform>().rect.width }
            }, false, true);
    }

    [Header("Sliders")]
    public Slider brightnessSlider;
    public Slider contrastSlider;
    public Slider saturationSlider;

    [Header("Live UI Readouts")]
    public TextMeshProUGUI brightnessText;
    public TextMeshProUGUI contrastText;
    public TextMeshProUGUI saturationText;

    [Header("Transitions")]
    [Tooltip("Drag your Fade In Checkbox/Toggle UI here")]
    public Toggle fadeInToggle;

    [SerializeField] private TextMeshProUGUI qualityText;
    private bool compareOriginal;
    [SerializeField] private TMP_InputField[] valueInputs = new TMP_InputField[3];
    private float appliedB = float.NaN;
    private float appliedC = float.NaN;
    private float appliedS = float.NaN;

    private float targetBrightness;
    private float targetContrast;
    private float targetSaturation;
    private float brightnessTolerance;
    private float contrastTolerance;
    private float saturationTolerance;

    void Start()
    {
        if (computerScreen != null && computerScreen.material != null)
        {
            gradingMat = new Material(computerScreen.material);
            computerScreen.material = gradingMat;
        }

        SetupRecommendedGrade();
        SetupSlider(brightnessSlider, BeginnerBrightnessMin, BeginnerBrightnessMax, EditorWorkspaceUI.Control);
        SetupSlider(contrastSlider, BeginnerContrastMin, BeginnerContrastMax, EditorWorkspaceUI.Control);
        SetupSlider(saturationSlider, BeginnerSaturationMin, BeginnerSaturationMax, EditorWorkspaceUI.Control);

        if (brightnessSlider) brightnessSlider.value = 1f;
        if (contrastSlider) contrastSlider.value = 1f;
        if (saturationSlider) saturationSlider.value = 1f;
        if (fadeInToggle) fadeInToggle.isOn = false;

        if (CampaignProgression.GetCurrentLevel() >= 3)
        {
            CreateTargetMarker(brightnessSlider, targetBrightness);
            CreateTargetMarker(contrastSlider, targetContrast);
            CreateTargetMarker(saturationSlider, targetSaturation);
        }
        CreateQualityPanel();
        UpdateReadouts();
    }

    void Update()
    {
        if (brightnessSlider == null || contrastSlider == null || saturationSlider == null) return;
        if (CampaignProgression.GetCurrentLevel() == 4)
        {
            bool selected = SelectedClip != null && SelectedClip.isOnTimeline;
            brightnessSlider.interactable = contrastSlider.interactable = saturationSlider.interactable = selected;
            if (!selected)
            {
                if (qualityText != null) qualityText.text = "SELECT A TIMELINE CLIP TO COLOR GRADE";
                return;
            }
        }

        SyncNumericFields();
        ProcessTutorialTarget();
        ReconcileTutorialTarget();

        if (!Mathf.Approximately(brightnessSlider.value, appliedB) ||
            !Mathf.Approximately(contrastSlider.value, appliedC) ||
            !Mathf.Approximately(saturationSlider.value, appliedS))
        {
            appliedB = brightnessSlider.value;
            appliedC = contrastSlider.value;
            appliedS = saturationSlider.value;
            if (CampaignProgression.GetCurrentLevel() == 4 && SelectedClip != null && SelectedClip.isOnTimeline)
            {
                SelectedClip.gradeBrightness = appliedB;
                SelectedClip.gradeContrast = appliedC;
                SelectedClip.gradeSaturation = appliedS;
            }

            if (gradingMat != null)
            {
                gradingMat.SetFloat("_Brightness", compareOriginal ? 1f : appliedB);
                gradingMat.SetFloat("_Contrast", compareOriginal ? 1f : appliedC);
                gradingMat.SetFloat("_Saturation", compareOriginal ? 1f : appliedS);
            }

            UpdateReadouts();
        }
    }

    private void SetupRecommendedGrade()
    {
        int currentLevel = CampaignProgression.GetCurrentLevel();

        targetBrightness = 1f;
        targetContrast = 1.05f;
        targetSaturation = 1f;
        brightnessTolerance = .15f;
        contrastTolerance = .25f;
        saturationTolerance = .30f;

        if (currentLevel == 1)
        {
            targetBrightness = (BeginnerBrightnessMin + BeginnerBrightnessMax) * .5f;
            targetContrast = (BeginnerContrastMin + BeginnerContrastMax) * .5f;
            targetSaturation = (BeginnerSaturationMin + BeginnerSaturationMax) * .5f;
            brightnessTolerance = (BeginnerBrightnessMax - BeginnerBrightnessMin) * .5f;
            contrastTolerance = (BeginnerContrastMax - BeginnerContrastMin) * .5f;
            saturationTolerance = (BeginnerSaturationMax - BeginnerSaturationMin) * .5f;
        }
        if (currentLevel <= 2) return;
        if (currentLevel == 3)
        {
            targetBrightness = (LamborminiBrief.BrightnessMin + LamborminiBrief.BrightnessMax) * .5f;
            targetContrast = (LamborminiBrief.ContrastMin + LamborminiBrief.ContrastMax) * .5f;
            targetSaturation = (LamborminiBrief.SaturationMin + LamborminiBrief.SaturationMax) * .5f;
            brightnessTolerance = (LamborminiBrief.BrightnessMax - LamborminiBrief.BrightnessMin) * .5f;
            contrastTolerance = (LamborminiBrief.ContrastMax - LamborminiBrief.ContrastMin) * .5f;
            saturationTolerance = (LamborminiBrief.SaturationMax - LamborminiBrief.SaturationMin) * .5f;
        }
        else if (currentLevel == 4)
        {
            targetBrightness = (BeginnerBrightnessMin + BeginnerBrightnessMax) * .5f;
            targetContrast = (BeginnerContrastMin + BeginnerContrastMax) * .5f;
            targetSaturation = (BeginnerSaturationMin + BeginnerSaturationMax) * .5f;
            brightnessTolerance = (BeginnerBrightnessMax - BeginnerBrightnessMin) * .5f;
            contrastTolerance = (BeginnerContrastMax - BeginnerContrastMin) * .5f;
            saturationTolerance = (BeginnerSaturationMax - BeginnerSaturationMin) * .5f;
        }
        else if (currentLevel >= 5)
        {
            targetBrightness = 1f;
            targetContrast = 1.24f;
            targetSaturation = 1.1f;
            brightnessTolerance = 0.06f;
            contrastTolerance = 0.1f;
            saturationTolerance = 0.09f;
        }
    }

    private void SetupSlider(Slider slider, float minimum, float maximum, Color accentColor)
    {
        if (slider == null) return;

        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.wholeNumbers = false;

        if (slider.fillRect != null)
        {
            Image fillImage = slider.fillRect.GetComponent<Image>();
            if (fillImage != null) fillImage.color = accentColor;
        }

        if (slider.handleRect != null)
        {
            RectTransform handle = slider.handleRect;
            handle.anchorMin = new Vector2(handle.anchorMin.x, 0.5f);
            handle.anchorMax = new Vector2(handle.anchorMax.x, 0.5f);
            handle.sizeDelta = new Vector2(20f, 20f);
            handle.localScale = Vector3.one;
            Image handleImage = slider.handleRect.GetComponent<Image>();
            if (handleImage != null) handleImage.enabled = false;
            // Slider drives the handle's vertical anchors. A fixed-size child
            // keeps the visible knob circular even when its hit area stretches.
            var existingKnob = handle.GetComponentInChildren<CircularSliderKnob>(true);
            if (existingKnob != null) { slider.targetGraphic = existingKnob; return; }
            var knobObject = new GameObject("Round Knob", typeof(RectTransform), typeof(CanvasRenderer), typeof(CircularSliderKnob));
            knobObject.transform.SetParent(handle, false);
            RectTransform knobRect = knobObject.GetComponent<RectTransform>();
            knobRect.anchorMin = knobRect.anchorMax = new Vector2(0.5f, 0.5f);
            knobRect.sizeDelta = new Vector2(22f, 22f);
            CircularSliderKnob knob = knobObject.GetComponent<CircularSliderKnob>();
            knob.color = new Color32(205, 205, 205, 255);
            slider.targetGraphic = knob;
        }
    }

    private void CreateTargetMarker(Slider slider, float targetValue)
    {
        if (slider == null) return;

        var existingMarker = slider.transform.Find("Recommended Grade Marker");
        GameObject markerObject = existingMarker != null ? existingMarker.gameObject : new GameObject("Recommended Grade Marker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        // Keep compatibility with scene/editor setup, but never display target ticks.
        markerObject.SetActive(false);
        markerObject.layer = slider.gameObject.layer;
        markerObject.transform.SetParent(slider.transform, false);

        float normalizedTarget = Mathf.InverseLerp(slider.minValue, slider.maxValue, targetValue);
        RectTransform markerRect = markerObject.GetComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(normalizedTarget, 0.5f);
        markerRect.anchorMax = new Vector2(normalizedTarget, 0.5f);
        markerRect.anchoredPosition = Vector2.zero;
        markerRect.sizeDelta = new Vector2(3f, 16f);

        Image markerImage = markerObject.GetComponent<Image>();
        markerImage.color = new Color(0.35f, 1f, 0.55f, 0.95f);
        markerImage.raycastTarget = false;
    }

    [SerializeField] private Button compareButton, resetAllButton;
    private Coroutine comparisonScroll;
    public void RevealComparisonForTutorial()
    {
        if (compareButton == null || !isActiveAndEnabled) return;
        var scroll = compareButton.GetComponentInParent<ScrollRect>();
        if (scroll == null || !scroll.gameObject.activeInHierarchy) return;
        if (comparisonScroll != null) StopCoroutine(comparisonScroll);
        comparisonScroll = StartCoroutine(ScrollToComparison(scroll));
    }

    private System.Collections.IEnumerator ScrollToComparison(ScrollRect scroll)
    {
        yield return null; // Let the dialogue and panel layout settle first.
        Canvas.ForceUpdateCanvases();
        scroll.StopMovement();
        float start = scroll.verticalNormalizedPosition;
        float elapsed = 0f;
        const float duration = .65f;
        while (elapsed < duration && scroll != null && scroll.gameObject.activeInHierarchy)
        {
            if (!PauseManager.isPaused)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                scroll.verticalNormalizedPosition = Mathf.Lerp(start, 0f, t);
                scroll.StopMovement();
            }
            yield return null;
        }
        comparisonScroll = null;
    }
    public bool IsComparisonControl(Transform target)
    {
        return compareButton != null && (target == compareButton.transform || target.IsChildOf(compareButton.transform));
    }
    [SerializeField] private Button[] resetRowButtons = new Button[3];
    private bool gradeControlsBound;
    private void BindGradeControls()
    {
        if (gradeControlsBound) return;
        gradeControlsBound = true;
        compareButton.onClick.AddListener(() => {
            compareOriginal = !compareOriginal;
            compareButton.GetComponentInChildren<TextMeshProUGUI>().text = compareOriginal ? "Viewing original" : "Before / After";
            appliedB = appliedC = appliedS = float.NaN;
        });
        resetAllButton.onClick.AddListener(() => {
            foreach (var slider in new[] { brightnessSlider, contrastSlider, saturationSlider })
                if (slider != null && slider.interactable) slider.value = 1;
        });
        for (int i=0;i<3;i++)
        {
            var slider=i==0?brightnessSlider:i==1?contrastSlider:saturationSlider;
            var field=valueInputs[i];
            if (slider == null || field == null) continue;
            field.onEndEdit.AddListener(input => {
                if (slider.interactable && float.TryParse(input,out float number) && !float.IsNaN(number) && !float.IsInfinity(number))
                    slider.value=Mathf.Clamp(number,slider.minValue,slider.maxValue);
                field.SetTextWithoutNotify(slider.value.ToString("F2"));
            });
            if(resetRowButtons[i]!=null) resetRowButtons[i].onClick.AddListener(() => { if(slider.interactable) slider.value=1; });
        }
    }
#if UNITY_EDITOR
    public void BakeHierarchyUI()
    {
        SetupSlider(brightnessSlider,BeginnerBrightnessMin,BeginnerBrightnessMax,EditorWorkspaceUI.Control);
        SetupSlider(contrastSlider,BeginnerContrastMin,BeginnerContrastMax,EditorWorkspaceUI.Control);
        SetupSlider(saturationSlider,BeginnerSaturationMin,BeginnerSaturationMax,EditorWorkspaceUI.Control);
        CreateQualityPanel();
        foreach(var slider in new[] {brightnessSlider,contrastSlider,saturationSlider})
        {
            CreateTargetMarker(slider,1);
            if(slider!=null) slider.transform.Find("Recommended Grade Marker").gameObject.SetActive(false);
        }
    }
#endif
    private void CreateQualityPanel()
    {
        if(compareButton!=null) { BindGradeControls(); EnsureGradeScroll(); return; }
        Transform root=EditorManager.Instance!=null && EditorManager.Instance.colorGradingBin!=null ? EditorManager.Instance.colorGradingBin.transform : null;
        if(root==null) return;
        foreach(Transform child in root) child.gameObject.SetActive(false);
        EditorWorkspaceUI.Surface(root);
        BuildGradeRow(root,brightnessSlider,"Brightness",0,.63f);
        BuildGradeRow(root,contrastSlider,"Contrast",1,.39f);
        BuildGradeRow(root,saturationSlider,"Saturation",2,.15f);
        compareButton=EditorWorkspaceUI.Button(root,"Before / After",.05f,.025f,.49f,.10f,()=>{});
        resetAllButton=EditorWorkspaceUI.Button(root,"Reset",.51f,.025f,.95f,.10f,()=>{});
        qualityText=EditorWorkspaceUI.Label(root,"Grade status","",.03f,.01f,.97f,.10f);
        qualityText.gameObject.SetActive(false);
        BindGradeControls();
        EnsureGradeScroll();
    }

    private void EnsureGradeScroll()
    {
        if (compareButton == null) return;
        var root = compareButton.transform.parent as RectTransform;
        if (root == null) return;
        if (root.name == "Grade Scroll Content") { EnsureScrollingLabels(root); return; }
        var children = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in root) children.Add(child);
        var viewportObject = new GameObject("Grade Scroll View", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        var viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(root, false);
        viewport.anchorMin = new Vector2(0f, .08f);
        viewport.anchorMax = new Vector2(1f, .82f);
        viewport.offsetMin = new Vector2(12f, 8f); viewport.offsetMax = new Vector2(-12f, -8f);
        viewportObject.GetComponent<Image>().color = new Color32(29, 29, 29, 255);
        var content = new GameObject("Grade Scroll Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1f);
        content.sizeDelta = new Vector2(0f, 540f);
        content.anchoredPosition = Vector2.zero;
        foreach (var child in children)
        {
            // Leave the authored panel heading fixed outside the scroll viewport.
            var heading = child.GetComponent<TMP_Text>();
            if (heading != null && heading.text.Replace(" ", "").Trim().ToUpperInvariant() == "COLOR") continue;
            child.SetParent(content, false);
        }
        var scroll = viewportObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;
        scroll.verticalNormalizedPosition = 1f;
        var sliders = new[] { brightnessSlider, contrastSlider, saturationSlider };
        for (int i = 0; i < sliders.Length; i++)
        {
            var slider = sliders[i];
            if (slider == null) continue;
            float top = -20f - i * 150f;
            PlaceGradeControl((RectTransform)slider.transform, .04f, .74f, top - 64f, 30f);
            if (valueInputs[i] != null) PlaceGradeControl((RectTransform)valueInputs[i].transform, .65f, .96f, top, 34f);
            if (resetRowButtons[i] != null) PlaceGradeControl((RectTransform)resetRowButtons[i].transform, .78f, .96f, top - 64f, 30f);
            foreach (var label in slider.GetComponentsInChildren<TMP_Text>(true))
            {
                string name = label.text.Trim().ToUpperInvariant();
                if (name != "BRIGHTNESS" && name != "CONTRAST" && name != "SATURATION") continue;
                label.transform.SetParent(content, false);
                PlaceGradeControl(label.rectTransform, .04f, .62f, top, 34f);
                label.raycastTarget = false;
            }
        }
        PlaceGradeControl((RectTransform)compareButton.transform, .04f, .48f, -478f, 40f);
        PlaceGradeControl((RectTransform)resetAllButton.transform, .52f, .96f, -478f, 40f);
        EnsureScrollingLabels(content);
    }
    private void EnsureScrollingLabels(RectTransform content)
    {
        var backdrop = content.parent.GetComponent<Image>();
        if (backdrop != null) backdrop.color = new Color32(29, 29, 29, 255);
        string[] names = { "BRIGHTNESS", "CONTRAST", "SATURATION" };
        for (int i = 0; i < names.Length; i++)
        {
            string objectName = names[i] + " Scroll Label";
            if (content.Find(objectName) != null) continue;
            // Replace any old text labels; artwork labels behind the viewport are covered.
            foreach (var old in content.GetComponentsInChildren<TMP_Text>(true))
                if (old.text.Trim().ToUpperInvariant() == names[i]) old.gameObject.SetActive(false);
            var label = EditorWorkspaceUI.Label(content, objectName, names[i], 0, 0, 1, 1);
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 22f; label.fontStyle = FontStyles.Bold;
            label.color = Color.white; label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            PlaceGradeControl(label.rectTransform, .04f, .62f, -20f - i * 150f, 34f);
        }
    }
    private static void PlaceGradeControl(RectTransform rect, float left, float right, float top, float height)
    {
        rect.anchorMin = new Vector2(left, 1f); rect.anchorMax = new Vector2(right, 1f);
        rect.pivot = new Vector2(.5f, 1f); rect.localScale = Vector3.one;
        rect.offsetMin = new Vector2(0f, top - height); rect.offsetMax = new Vector2(0f, top);
    }
    private void BuildGradeRow(Transform root,Slider slider,string label,int index,float bottom)
    {
        if(slider==null) return;
        slider.transform.SetParent(root,false); slider.gameObject.SetActive(true);
        EditorWorkspaceUI.Place(slider.GetComponent<RectTransform>(),.05f,bottom,.76f,bottom+.07f);
        var obj=new GameObject(label+" value",typeof(RectTransform),typeof(Image),typeof(TMP_InputField));
        obj.transform.SetParent(root,false);
        EditorWorkspaceUI.Place(obj.GetComponent<RectTransform>(),.65f,bottom+.08f,.95f,bottom+.16f);
        obj.GetComponent<Image>().color=EditorWorkspaceUI.Control;
        var field=obj.GetComponent<TMP_InputField>();
        var value=EditorWorkspaceUI.Label(obj.transform,"Value",slider.value.ToString("F2"),0,0,1,1);
        value.alignment=TextAlignmentOptions.Center; value.color=Color.white;
        field.textViewport=obj.GetComponent<RectTransform>(); field.textComponent=value;
        field.contentType=TMP_InputField.ContentType.DecimalNumber;field.characterLimit=6;
        field.SetTextWithoutNotify(slider.value.ToString("F2"));
        valueInputs[index]=field;
        resetRowButtons[index]=EditorWorkspaceUI.Button(root,"Reset",.78f,bottom,.95f,bottom+.07f,()=>{});
    }

    private void SyncNumericFields()
    {
        for (int i=0;i<3;i++)
        {
            var field=valueInputs[i];
            var slider=i==0 ? brightnessSlider : i==1 ? contrastSlider : saturationSlider;
            if (field == null || slider == null) continue;
            field.interactable=slider.interactable;
            if (!field.isFocused) field.SetTextWithoutNotify(slider.value.ToString("F2"));
        }
    }

    private void ProcessTutorialTarget()
    {
        if (CampaignProgression.GetCurrentLevel() <= 2) return;
        if (EditorTutorialManager.Instance == null || !EditorTutorialManager.Instance.gameObject.activeInHierarchy || !EditorTutorialManager.Instance.isTaskPhaseActive) return;

        if (EditorTutorialManager.Instance.currentStep == EditorTutorialManager.EditorStep.AdjustBrightness && Mathf.Abs(brightnessSlider.value - targetBrightness) <= 0.015f)
        {
            brightnessSlider.value = targetBrightness;
        }
        else if (EditorTutorialManager.Instance.currentStep == EditorTutorialManager.EditorStep.AdjustContrast && Mathf.Abs(contrastSlider.value - targetContrast) <= 0.015f)
        {
            contrastSlider.value = targetContrast;
        }
        else if (EditorTutorialManager.Instance.currentStep == EditorTutorialManager.EditorStep.AdjustSaturation && Mathf.Abs(saturationSlider.value - targetSaturation) <= 0.015f)
        {
            saturationSlider.value = targetSaturation;
        }
    }

    private void ReconcileTutorialTarget()
    {
        if (CampaignProgression.GetCurrentLevel() <= 2) { TrackBeginnerPractice(); return; }
        if (EditorTutorialManager.Instance == null || !EditorTutorialManager.Instance.gameObject.activeInHierarchy || !EditorTutorialManager.Instance.isTaskPhaseActive) return;

        if (EditorTutorialManager.Instance.currentStep == EditorTutorialManager.EditorStep.AdjustBrightness && Mathf.Abs(brightnessSlider.value - targetBrightness) <= 0.01f)
        {
            EditorTutorialManager.Instance.OnBrightnessAdjusted();
        }
        else if (EditorTutorialManager.Instance.currentStep == EditorTutorialManager.EditorStep.AdjustContrast && Mathf.Abs(contrastSlider.value - targetContrast) <= 0.01f)
        {
            EditorTutorialManager.Instance.OnContrastAdjusted();
        }
        else if (EditorTutorialManager.Instance.currentStep == EditorTutorialManager.EditorStep.AdjustSaturation && Mathf.Abs(saturationSlider.value - targetSaturation) <= 0.01f)
        {
            EditorTutorialManager.Instance.OnSaturationAdjusted();
        }
    }

    private int practiceStep = -1;
    private float practiceValue, practiceChangedAt;
    private bool practiceChanged;
    private void TrackBeginnerPractice()
    {
        var lesson=EditorTutorialManager.Instance;
        if(lesson==null||!lesson.gameObject.activeInHierarchy||!lesson.isTaskPhaseActive){practiceStep=-1;return;}
        var step=lesson.currentStep;
        Slider slider=step==EditorTutorialManager.EditorStep.AdjustBrightness?brightnessSlider:
            step==EditorTutorialManager.EditorStep.AdjustContrast?contrastSlider:
            step==EditorTutorialManager.EditorStep.AdjustSaturation?saturationSlider:null;
        if(slider==null){practiceStep=-1;return;}
        if(practiceStep!=(int)step){practiceStep=(int)step;practiceValue=slider.value;practiceChanged=false;return;}
        if(!Mathf.Approximately(practiceValue,slider.value))
        {practiceValue=slider.value;practiceChanged=true;practiceChangedAt=Time.unscaledTime;}
        if(!practiceChanged||Time.unscaledTime-practiceChangedAt<1.5f)return;
        practiceChanged=false;
        if(step==EditorTutorialManager.EditorStep.AdjustBrightness)lesson.OnBrightnessAdjusted();
        else if(step==EditorTutorialManager.EditorStep.AdjustContrast)lesson.OnContrastAdjusted();
        else lesson.OnSaturationAdjusted();
    }

    private void UpdateReadouts()
    {
        if (brightnessText != null && brightnessSlider != null)
        {
            brightnessText.text = brightnessSlider.value.ToString("F2");
            brightnessText.color = GetReadoutColor(brightnessSlider.value, targetBrightness, brightnessTolerance);
        }

        if (contrastText != null && contrastSlider != null)
        {
            contrastText.text = contrastSlider.value.ToString("F2");
            contrastText.color = GetReadoutColor(contrastSlider.value, targetContrast, contrastTolerance);
        }

        if (saturationText != null && saturationSlider != null)
        {
            saturationText.text = saturationSlider.value.ToString("F2");
            saturationText.color = GetReadoutColor(saturationSlider.value, targetSaturation, saturationTolerance);
        }

        UpdateQualityText();
    }

    private Color GetReadoutColor(float value, float target, float tolerance)
    {
        if (Mathf.Abs(value - target) <= tolerance + 0.0001f) return new Color(0.35f, 1f, 0.55f);
        return new Color(1f, 0.72f, 0.25f);
    }

    private void UpdateQualityText()
    {
        if (qualityText == null || brightnessSlider == null || contrastSlider == null || saturationSlider == null) return;

        bool brightnessReady = Mathf.Abs(brightnessSlider.value - targetBrightness) <= brightnessTolerance + 0.0001f;
        bool contrastReady = Mathf.Abs(contrastSlider.value - targetContrast) <= contrastTolerance + 0.0001f;
        bool saturationReady = Mathf.Abs(saturationSlider.value - targetSaturation) <= saturationTolerance + 0.0001f;
        bool deliveryReady = brightnessReady && contrastReady && saturationReady;

        string status = deliveryReady ? "<color=#9DC8AA>Grade matches the brief</color>" : "<color=#C7BAB0>Adjust to the contract brief</color>";
        qualityText.text = status;
    }

    private string GetRange(float target, float tolerance)
    {
        return (target - tolerance).ToString("F2") + "–" + (target + tolerance).ToString("F2");
    }

    public bool IsProfessionalGrade()
    {
        if (brightnessSlider == null || contrastSlider == null || saturationSlider == null) return false;

        return Mathf.Abs(brightnessSlider.value - targetBrightness) <= brightnessTolerance + 0.0001f &&
               Mathf.Abs(contrastSlider.value - targetContrast) <= contrastTolerance + 0.0001f &&
               Mathf.Abs(saturationSlider.value - targetSaturation) <= saturationTolerance + 0.0001f;
    }

    public void ApplyRecommendedGrade()
    {
        if (brightnessSlider) brightnessSlider.value = targetBrightness;
        if (contrastSlider) contrastSlider.value = targetContrast;
        if (saturationSlider) saturationSlider.value = targetSaturation;
    }

    public void ResetGrading()
    {
        if (brightnessSlider) { brightnessSlider.interactable = true; brightnessSlider.value = 1f; }
        if (contrastSlider) { contrastSlider.interactable = true; contrastSlider.value = 1f; }
        if (saturationSlider) { saturationSlider.interactable = true; saturationSlider.value = 1f; }
        if (fadeInToggle) fadeInToggle.isOn = false;

        UpdateReadouts();
    }

    private void OnDestroy()
    {
        if (gradingMat != null) Destroy(gradingMat);
    }
}


