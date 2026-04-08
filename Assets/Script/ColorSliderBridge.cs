using UnityEngine;
using UnityEngine.UI;
public class ColorSliderBridge : MonoBehaviour
{
    public DirectorTerminal director; // Link to Director script
    public Slider rSlider;
    public Slider gSlider;
    public Slider bSlider;

    public void OnPropColorChanged()
    {
        if (director != null)
        {
            director.SetSelectedPropColor(rSlider.value, gSlider.value, bSlider.value);
        }
    }
}