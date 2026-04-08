using UnityEngine;
using UnityEngine.UI;

public class TimelinePlayhead : MonoBehaviour
{
    public static TimelinePlayhead Instance;
    private RectTransform myRect;

    public bool isPlaying = false;

    void Awake()
    {
        Instance = this;
        myRect = GetComponent<RectTransform>();

        myRect.anchorMin = new Vector2(0, 0.5f);
        myRect.anchorMax = new Vector2(0, 0.5f);
        myRect.pivot = new Vector2(0.5f, 0.5f);
        myRect.anchoredPosition = new Vector2(0, 0);
    }

    // These act as flags now. The TruePixelPlayer does all the actual moving!
    public void StartPlayback()
    {
        isPlaying = true;
    }

    public void StopPlayback()
    {
        isPlaying = false;
    }

    public void PausePlayback()
    {
        isPlaying = false;
    }
}