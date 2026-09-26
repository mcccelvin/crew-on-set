using UnityEngine;

namespace Player.Equipment
{
    public partial class FilmCameraItem
    {
        private CameraHUDController dynamicHUD;

        private void OpenDynamicHUD()
        {
            LoadCameraSettings();
            featureLevel = CameraFeatureUnlocks.Level;
            if (dynamicHUD == null) dynamicHUD = CameraHUDController.Create();
            dynamicHUD.Show(filmCamera);
            if (filmUICanvas != null) filmUICanvas.SetActive(false);
            if (trackingSquare != null) trackingSquare.SetParent(dynamicHUD.viewport, false);
            RefreshDynamicHUD();
        }

        private void RefreshDynamicHUD()
        {
            if (dynamicHUD == null || !isCameraActive) return;
            if (ruleOfThirdsGrid != null && ruleOfThirdsGrid.transform.parent != dynamicHUD.viewport)
            {
                var rect = ruleOfThirdsGrid.GetComponent<RectTransform>();
                rect.SetParent(dynamicHUD.viewport, false);
                rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
                rect.offsetMin=rect.offsetMax=Vector2.zero; rect.localScale=Vector3.one;
            }
            dynamicHUD.Refresh(new CameraHUDController.State {
                recording=isRecording, seconds=isRecording ? Time.time-recordingStartTime : 0,
                card=isSDCardInserted, manual=featureLevel>=2 && manualFocus, focus=currentFocusDistance,
                level=featureLevel, kelvin=whiteBalance, iso=iso, aperture=iris, shutterAngle=shutterAngle,
                fps=pixelRecorder!=null ? pixelRecorder.framesPerSecond : TapeSettings.framesPerSecond,
                width=pixelRecorder!=null ? pixelRecorder.captureWidth : 640,
                height=pixelRecorder!=null ? pixelRecorder.captureHeight : 360, menu=SettingsHUDText()
            });
        }
    }
}
