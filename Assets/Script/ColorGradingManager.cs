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

    private TextMeshProUGUI qualityText;
    private bool compareOriginal;
    private TMP_InputField[] valueInputs = new TMP_InputField[3];
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
            targetBrightness = 1.02f;
            targetContrast = 1.16f;
            targetSaturation = 1.12f;
            brightnessTolerance = 0.07f;
            contrastTolerance = 0.08f;
            saturationTolerance = 0.08f;
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

        GameObject markerObject = new GameObject("Recommended Grade Marker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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

    private void CreateQualityPanel()
    {
        Transform root = EditorManager.Instance != null && EditorManager.Instance.colorGradingBin != null
            ? EditorManager.Instance.colorGradingBin.transform : null;
        if (root == null) return;
        // Retain the sliders and callbacks used by the tutorial and export.
        foreach (Transform child in root) child.gameObject.SetActive(false);
        EditorWorkspaceUI.Surface(root);
        // COLOR and the three slider labels are already part of the panel artwork.

        BuildGradeRow(root, brightnessSlider, "Brightness", 0, 0.63f);
        BuildGradeRow(root, contrastSlider, "Contrast", 1, 0.39f);
        BuildGradeRow(root, saturationSlider, "Saturation", 2, 0.15f);
        Button compare = null;
        compare = EditorWorkspaceUI.Button(root,"Before / After",0.05f,0.025f,0.49f,0.10f,() =>
        {
            compareOriginal = !compareOriginal;
            compare.GetComponentInChildren<TextMeshProUGUI>().text = compareOriginal ? "Viewing original" : "Before / After";
            appliedB = float.NaN; appliedC = float.NaN; appliedS = float.NaN;
        });
        EditorWorkspaceUI.Button(root,"Reset",0.51f,0.025f,0.95f,0.10f,() =>
        {
            if (brightnessSlider != null && brightnessSlider.interactable) brightnessSlider.value = 1f;
            if (contrastSlider != null && contrastSlider.interactable) contrastSlider.value = 1f;
            if (saturationSlider != null && saturationSlider.interactable) saturationSlider.value = 1f;
        });
        qualityText = EditorWorkspaceUI.Label(root,"Grade status","",0.03f,0.01f,0.97f,0.10f);
        qualityText.gameObject.SetActive(false);
    }

    private void BuildGradeRow(Transform root, Slider slider, string label, int index, float bottom)
    {
        if (slider == null) return;
        slider.transform.SetParent(root, false); slider.gameObject.SetActive(true);
        EditorWorkspaceUI.Place(slider.GetComponent<RectTransform>(),0.05f,bottom,0.76f,bottom+0.07f);

        if (CampaignProgression.GetCurrentLevel() <= 2)
        {
            // The authored artwork already labels each slider. Keep its title clear.
        }

        var fieldObject = new GameObject(label+" value", typeof(RectTransform),typeof(Image),typeof(TMP_InputField));
        fieldObject.transform.SetParent(root,false);
        EditorWorkspaceUI.Place(fieldObject.GetComponent<RectTransform>(),0.65f,bottom+0.08f,0.95f,bottom+0.16f);
        fieldObject.GetComponent<Image>().color = EditorWorkspaceUI.Control;
        var field = fieldObject.GetComponent<TMP_InputField>();
        var value = EditorWorkspaceUI.Label(fieldObject.transform,"Value",slider.value.ToString("F2"),0,0,1,1);
        value.alignment = TextAlignmentOptions.Center;
        value.color = Color.white;
        field.textViewport = fieldObject.GetComponent<RectTransform>();
        field.textComponent = value;
        field.contentType = TMP_InputField.ContentType.DecimalNumber;
        field.characterLimit = 6;
        field.SetTextWithoutNotify(slider.value.ToString("F2"));
        field.onEndEdit.AddListener(input =>
        {
            if (slider.interactable && float.TryParse(input, out float number) && !float.IsNaN(number) && !float.IsInfinity(number))
                slider.value = Mathf.Clamp(number,slider.minValue,slider.maxValue);
            field.SetTextWithoutNotify(slider.value.ToString("F2"));
        });
        valueInputs[index] = field;
        EditorWorkspaceUI.Button(root,"Reset",0.78f,bottom,0.95f,bottom+0.07f,() =>
        { if (slider.interactable) slider.value = 1f; });
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

// A UI mesh needs no built-in resource or imported sprite.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CircularSliderKnob : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        const int segments = 48;
        mesh.AddVert(center, color, new Vector2(0.5f, 0.5f));
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            mesh.AddVert(center + direction * radius, color, Vector2.one * 0.5f + direction * 0.5f);
        }
        for (int i = 0; i < segments; i++)
            mesh.AddTriangle(0, i + 1, (i + 1) % segments + 1);
    }
}
