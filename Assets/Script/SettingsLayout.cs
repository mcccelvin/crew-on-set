using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Scene-owned settings pages; SharedOptionsPanel only binds callbacks and values.
public sealed class SettingsLayout : MonoBehaviour
{
    public Transform frame;
    public Button[] tabs = new Button[3];
    public GameObject[] pages = new GameObject[3];
    public Button save, reset, close, controls;
    public TMP_Text controlsHelp;
    public Slider sensitivity, volume;
    public TMP_Text sensitivityValue, volumeValue;
    public Button[] choices = new Button[3];
}
