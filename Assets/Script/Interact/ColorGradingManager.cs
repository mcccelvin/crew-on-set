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

    void Start()
    {
        // Create a private instance of the material so we don't change the asset file
        if (computerScreen != null && computerScreen.material != null)
        {
            gradingMat = new Material(computerScreen.material);
            computerScreen.material = gradingMat;
        }

        // Set default slider values
        if (brightnessSlider) brightnessSlider.value = 1f;
        if (contrastSlider) contrastSlider.value = 1f;
        if (saturationSlider) saturationSlider.value = 1f;
    }

    void Update()
    {
        if (gradingMat == null) return;

        // Apply slider values to the Shader in real-time
        gradingMat.SetFloat("_Brightness", brightnessSlider.value);
        gradingMat.SetFloat("_Contrast", contrastSlider.value);
        gradingMat.SetFloat("_Saturation", saturationSlider.value);
    }

    public void ResetGrading()
    {
        brightnessSlider.value = 1f;
        contrastSlider.value = 1f;
        saturationSlider.value = 1f;
    }
}   