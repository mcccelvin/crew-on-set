using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ColorGradingManager : MonoBehaviour
{
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
    private float lastB = 1f;
    private float lastC = 1f;
    private float lastS = 1f;
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
        SetupSlider(brightnessSlider, 0.75f, 1.25f, new Color(0.2f, 0.75f, 1f));
        SetupSlider(contrastSlider, 0.75f, 1.5f, new Color(1f, 0.68f, 0.18f));
        SetupSlider(saturationSlider, 0.65f, 1.4f, new Color(0.9f, 0.3f, 0.7f));

        if (brightnessSlider) brightnessSlider.value = 1f;
        if (contrastSlider) contrastSlider.value = 1f;
        if (saturationSlider) saturationSlider.value = 1f;
        if (fadeInToggle) fadeInToggle.isOn = false;

        CreateTargetMarker(brightnessSlider, targetBrightness);
        CreateTargetMarker(contrastSlider, targetContrast);
        CreateTargetMarker(saturationSlider, targetSaturation);
        CreateQualityPanel();
        UpdateReadouts();
    }

    void Update()
    {
        if (gradingMat == null || brightnessSlider == null || contrastSlider == null || saturationSlider == null) return;

        ProcessTutorialTarget();

        if (!Mathf.Approximately(brightnessSlider.value, appliedB) ||
            !Mathf.Approximately(contrastSlider.value, appliedC) ||
            !Mathf.Approximately(saturationSlider.value, appliedS))
        {
            appliedB = brightnessSlider.value;
            appliedC = contrastSlider.value;
            appliedS = saturationSlider.value;

            gradingMat.SetFloat("_Brightness", appliedB);
            gradingMat.SetFloat("_Contrast", appliedC);
            gradingMat.SetFloat("_Saturation", appliedS);

            UpdateReadouts();
        }

        if (EditorTutorialManager.Instance != null)
        {
            if (!Mathf.Approximately(brightnessSlider.value, lastB))
            {
                if (Mathf.Abs(brightnessSlider.value - targetBrightness) <= 0.01f) EditorTutorialManager.Instance.OnBrightnessAdjusted();
                lastB = brightnessSlider.value;
            }

            if (!Mathf.Approximately(contrastSlider.value, lastC))
            {
                if (Mathf.Abs(contrastSlider.value - targetContrast) <= 0.01f) EditorTutorialManager.Instance.OnContrastAdjusted();
                lastC = contrastSlider.value;
            }

            if (!Mathf.Approximately(saturationSlider.value, lastS))
            {
                if (Mathf.Abs(saturationSlider.value - targetSaturation) <= 0.01f) EditorTutorialManager.Instance.OnSaturationAdjusted();
                lastS = saturationSlider.value;
            }
        }
    }

    private void SetupRecommendedGrade()
    {
        int currentLevel = CampaignProgression.GetCurrentLevel();

        targetBrightness = 0.98f;
        targetContrast = 1.12f;
        targetSaturation = 1.08f;
        brightnessTolerance = 0.04f;
        contrastTolerance = 0.06f;
        saturationTolerance = 0.05f;

        if (currentLevel == 2)
        {
            targetContrast = 1.2f;
            targetSaturation = 1.1f;
            contrastTolerance = 0.06f;
            saturationTolerance = 0.06f;
        }
        else if (currentLevel == 3)
        {
            targetBrightness = 1f;
            targetContrast = 1.22f;
            targetSaturation = 1.06f;
            brightnessTolerance = 0.05f;
            contrastTolerance = 0.08f;
            saturationTolerance = 0.08f;
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
            Image handleImage = slider.handleRect.GetComponent<Image>();
            if (handleImage != null) handleImage.color = Color.Lerp(accentColor, Color.white, 0.55f);
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
        markerRect.sizeDelta = new Vector2(4f, 30f);

        Image markerImage = markerObject.GetComponent<Image>();
        markerImage.color = new Color(0.35f, 1f, 0.55f, 0.95f);
        markerImage.raycastTarget = false;
    }

    private void CreateQualityPanel()
    {
        if (brightnessSlider == null || brightnessSlider.transform.parent == null) return;

        Transform colorPanel = brightnessSlider.transform.parent.parent;
        if (colorPanel == null) colorPanel = brightnessSlider.transform.parent;

        GameObject panelObject = new GameObject("Commercial Grade Monitor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.layer = colorPanel.gameObject.layer;
        panelObject.transform.SetParent(colorPanel, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 50f);
        panelRect.sizeDelta = new Vector2(640f, 72f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.035f, 0.065f, 0.095f, 0.96f);
        panelImage.raycastTarget = false;

        GameObject textObject = new GameObject("Grade Status", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = colorPanel.gameObject.layer;
        textObject.transform.SetParent(panelObject.transform, false);

        qualityText = textObject.GetComponent<TextMeshProUGUI>();
        qualityText.fontSize = 20f;
        qualityText.alignment = TextAlignmentOptions.Center;
        qualityText.color = Color.white;
        qualityText.enableWordWrapping = true;
        qualityText.raycastTarget = false;

        RectTransform textRect = qualityText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 5f);
        textRect.offsetMax = new Vector2(-12f, -5f);
    }

    private void ProcessTutorialTarget()
    {
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
        if (Mathf.Abs(value - target) <= tolerance) return new Color(0.35f, 1f, 0.55f);
        return new Color(1f, 0.72f, 0.25f);
    }

    private void UpdateQualityText()
    {
        if (qualityText == null || brightnessSlider == null || contrastSlider == null || saturationSlider == null) return;

        bool brightnessReady = Mathf.Abs(brightnessSlider.value - targetBrightness) <= brightnessTolerance;
        bool contrastReady = Mathf.Abs(contrastSlider.value - targetContrast) <= contrastTolerance;
        bool saturationReady = Mathf.Abs(saturationSlider.value - targetSaturation) <= saturationTolerance;
        bool deliveryReady = brightnessReady && contrastReady && saturationReady;

        string status = deliveryReady ? "<color=#59FF8C>DELIVERY READY</color>" : "<color=#FFB83F>ADJUST PRIMARY GRADE</color>";
        qualityText.text = "<b>COMMERCIAL LOOK  •  " + status + "</b>\n" +
                           "Target  B " + GetRange(targetBrightness, brightnessTolerance) +
                           "   C " + GetRange(targetContrast, contrastTolerance) +
                           "   S " + GetRange(targetSaturation, saturationTolerance);
    }

    private string GetRange(float target, float tolerance)
    {
        return (target - tolerance).ToString("F2") + "–" + (target + tolerance).ToString("F2");
    }

    public bool IsProfessionalGrade()
    {
        if (brightnessSlider == null || contrastSlider == null || saturationSlider == null) return false;

        return Mathf.Abs(brightnessSlider.value - targetBrightness) <= brightnessTolerance &&
               Mathf.Abs(contrastSlider.value - targetContrast) <= contrastTolerance &&
               Mathf.Abs(saturationSlider.value - targetSaturation) <= saturationTolerance;
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
