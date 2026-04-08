using UnityEngine;
using UnityEngine.UI;
using TMPro; // Needed to talk to TextMeshPro!

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
    private bool lastFade = false; // --- NEW STATE TRACKER ---

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

        gradingMat.SetFloat("_Brightness", brightnessSlider.value);
        gradingMat.SetFloat("_Contrast", contrastSlider.value);
        gradingMat.SetFloat("_Saturation", saturationSlider.value);

        // --- THE FIX: Constantly refresh the text to match the sliders ---
        UpdateReadouts();

        // --- NEW HYPER-SPECIFIC TUTORIAL PINGS ---
        if (EditorTutorialManager.Instance != null)
        {
            if (brightnessSlider.value != lastB)
            { EditorTutorialManager.Instance.OnBrightnessAdjusted(); lastB = brightnessSlider.value; }

            if (contrastSlider.value != lastC)
            { EditorTutorialManager.Instance.OnContrastAdjusted(); lastC = contrastSlider.value; }

            if (saturationSlider.value != lastS)
            { EditorTutorialManager.Instance.OnSaturationAdjusted(); lastS = saturationSlider.value; }

            // --- NEW PING FOR FADE IN TOGGLE ---
            if (fadeInToggle != null && fadeInToggle.isOn != lastFade)
            {
                if (fadeInToggle.isOn) EditorTutorialManager.Instance.OnFadeInToggled();
                lastFade = fadeInToggle.isOn;
            }
        }
    }

    private void UpdateReadouts()
    {
        // "F2" formats the number so it always shows two decimal places (e.g., 1.05 instead of 1.05321684)
        if (brightnessText != null && brightnessSlider != null)
            brightnessText.text = brightnessSlider.value.ToString("F2");

        if (contrastText != null && contrastSlider != null)
            contrastText.text = contrastSlider.value.ToString("F2");

        if (saturationText != null && saturationSlider != null)
            saturationText.text = saturationSlider.value.ToString("F2");
    }

    public void ResetGrading()
    {
        if (brightnessSlider) brightnessSlider.value = 1f;
        if (contrastSlider) contrastSlider.value = 1f;
        if (saturationSlider) saturationSlider.value = 1f;
        if (fadeInToggle) fadeInToggle.isOn = false;

        UpdateReadouts();
    }
}