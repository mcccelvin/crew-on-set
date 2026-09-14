using PlayerPrefs = GameSavePrefs;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public static class GameOptions
{
    public const string SensitivityKey = "Options.MouseSensitivityMultiplier";
    public const string FullscreenKey = "Options.Fullscreen";
    public const float MinimumMouseSensitivity = 0.01f;
    public const float MaximumMouseSensitivity = 3f;
    public static float MouseSensitivityMultiplier => Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), MinimumMouseSensitivity, MaximumMouseSensitivity);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void LoadDisplaySettings()
    {
        if (PlayerPrefs.HasKey(FullscreenKey)) ApplyFullscreen(PlayerPrefs.GetInt(FullscreenKey) == 1);
        AudioListener.volume=Mathf.Clamp01(PlayerPrefs.GetFloat("Options.MasterVolume",1));
        if(PlayerPrefs.HasKey("Options.Quality"))QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt("Options.Quality"),0,QualitySettings.names.Length-1));
        if(PlayerPrefs.HasKey("Options.FPS")){QualitySettings.vSyncCount=0;Application.targetFrameRate=PlayerPrefs.GetInt("Options.FPS",60);}
    }

    public static void ApplyFullscreen(bool enabled)
    {
        if (Application.isEditor) return;
        // Give windowed mode a visible border instead of keeping a desktop-sized window.
        var desktop=Screen.currentResolution;
        int width=desktop.width>0?desktop.width:Screen.width;
        int height=desktop.height>0?desktop.height:Screen.height;
        if(enabled)
        {
            if(!Screen.fullScreen){PlayerPrefs.SetInt("Options.WindowWidth",Screen.width);PlayerPrefs.SetInt("Options.WindowHeight",Screen.height);}
            Screen.SetResolution(width,height,FullScreenMode.FullScreenWindow);
        }
        else
        {
            int maxWidth=Mathf.Max(640,Mathf.RoundToInt(width*.85f));
            int maxHeight=Mathf.Max(360,Mathf.RoundToInt(height*.85f));
            int windowWidth=Mathf.Clamp(PlayerPrefs.GetInt("Options.WindowWidth",1280),640,maxWidth);
            int windowHeight=Mathf.Clamp(PlayerPrefs.GetInt("Options.WindowHeight",720),360,maxHeight);
            Screen.SetResolution(windowWidth,windowHeight,FullScreenMode.Windowed);
        }
    }
}
