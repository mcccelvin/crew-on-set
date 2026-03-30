using UnityEngine;
using UnityEngine.UI;

public class ColorGradingManager : MonoBehaviour
{
    [Header("Video Output")]
    public RawImage computerScreen;
    private Material gradingMat;

    [Header("Sliders")]
    public Slider brightnessSlider;
    public Slider contrastSlider;
    public Slider saturationSlider;

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
    }

    void Update()
    {
        if (gradingMat == null) return;

        gradingMat.SetFloat("_Brightness", brightnessSlider.value);
        gradingMat.SetFloat("_Contrast", contrastSlider.value);
        gradingMat.SetFloat("_Saturation", saturationSlider.value);

        // --- NEW HYPER-SPECIFIC TUTORIAL PINGS ---
        if (EditorTutorialManager.Instance != null)
        {
            if (brightnessSlider.value != lastB)
            { EditorTutorialManager.Instance.OnBrightnessAdjusted(); lastB = brightnessSlider.value; }

            if (contrastSlider.value != lastC)
            { EditorTutorialManager.Instance.OnContrastAdjusted(); lastC = contrastSlider.value; }

            if (saturationSlider.value != lastS)
            { EditorTutorialManager.Instance.OnSaturationAdjusted(); lastS = saturationSlider.value; }
        }
    }

    public void ResetGrading()
    {
        brightnessSlider.value = 1f; contrastSlider.value = 1f; saturationSlider.value = 1f;
    }
}   