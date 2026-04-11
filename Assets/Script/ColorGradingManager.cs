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

    private float lastB = 1f;
    private float lastC = 1f;
    private float lastS = 1f;

    void Start()
    {
        if (computerScreen != null && computerScreen.material != null)
        {
            gradingMat = new Material(computerScreen.material);
            computerScreen.material = gradingMat;
        }

        if (brightnessSlider) brightnessSlider.value = 1f;
        if (contrastSlider) contrastSlider.value = 1f;
        if (saturationSlider) saturationSlider.value = 1f;
        if (fadeInToggle) fadeInToggle.isOn = false;

        UpdateReadouts();
    }

    void Update()
    {
        if (gradingMat == null) return;

        // ======================================================================
        // --- THE FIX: Snaps and LOCKS the sliders to exact values! ---
        // ======================================================================
        if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy && EditorTutorialManager.Instance.isTaskPhaseActive)
        {
            if (EditorTutorialManager.Instance.currentStep == EditorTutorialManager.EditorStep.AdjustBrightness)
            {
                if (Mathf.Abs(brightnessSlider.value - 0.95f) <= 0.05f)
                {
                    brightnessSlider.value = 0.95f;
                    brightnessSlider.interactable = false; // Locks the slider!
                }
            }
            else if (EditorTutorialManager.Instance.currentStep == EditorTutorialManager.EditorStep.AdjustContrast)
            {
                if (Mathf.Abs(contrastSlider.value - 1.35f) <= 0.05f)
                {
                    contrastSlider.value = 1.15f;
                    contrastSlider.interactable = false;
                }
            }
            else if (EditorTutorialManager.Instance.currentStep == EditorTutorialManager.EditorStep.AdjustSaturation)
            {
                if (Mathf.Abs(saturationSlider.value - 1.45f) <= 0.05f)
                {
                    saturationSlider.value = 1.10f;
                    saturationSlider.interactable = false;
                }
            }
        }

        gradingMat.SetFloat("_Brightness", brightnessSlider.value);
        gradingMat.SetFloat("_Contrast", contrastSlider.value);
        gradingMat.SetFloat("_Saturation", saturationSlider.value);

        UpdateReadouts();

        if (EditorTutorialManager.Instance != null)
        {
            if (brightnessSlider.value != lastB)
            {
                if (brightnessSlider.value == 0.95f) EditorTutorialManager.Instance.OnBrightnessAdjusted();
                lastB = brightnessSlider.value;
            }

            if (contrastSlider.value != lastC)
            {
                if (contrastSlider.value == 1.15f) EditorTutorialManager.Instance.OnContrastAdjusted();
                lastC = contrastSlider.value;
            }

            if (saturationSlider.value != lastS)
            {
                if (saturationSlider.value == 1.10f) EditorTutorialManager.Instance.OnSaturationAdjusted();
                lastS = saturationSlider.value;
            }
        }
    }

    private void UpdateReadouts()
    {
        if (brightnessText != null && brightnessSlider != null) brightnessText.text = brightnessSlider.value.ToString("F2");
        if (contrastText != null && contrastSlider != null) contrastText.text = contrastSlider.value.ToString("F2");
        if (saturationText != null && saturationSlider != null) saturationText.text = saturationSlider.value.ToString("F2");
    }

    public void ResetGrading()
    {
        // Unlock them for the actual game!
        if (brightnessSlider) { brightnessSlider.interactable = true; brightnessSlider.value = 1f; }
        if (contrastSlider) { contrastSlider.interactable = true; contrastSlider.value = 1f; }
        if (saturationSlider) { saturationSlider.interactable = true; saturationSlider.value = 1f; }
        if (fadeInToggle) fadeInToggle.isOn = false;

        UpdateReadouts();
    }
}