using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PlayerPrefs = GameSavePrefs;

namespace Player.Equipment
{
    public partial class FilmCameraItem
    {
        private bool settingsLoaded, settingsOpen, manualFocus;
        public bool GridEnabled { get; private set; }

        private void RefreshGridVisibility()
        {
            if (ruleOfThirdsGrid == null) return;
            ruleOfThirdsGrid.SetActive(isCameraActive && CameraFeatureUnlocks.Level >= 2 && GridEnabled);
            bool lesson = GokeLevelManager.Instance != null && GokeLevelManager.Instance.ShowThirdsLessonGuide;
            foreach (Transform child in ruleOfThirdsGrid.transform)
                if (child.name.Contains("Power Point") || child.name.Contains("Label") || child.name.Contains("Lesson Panel"))
                    child.gameObject.SetActive(lesson);
        }
        private int settingRow, featureLevel;
        private float manualDistance = 5f, whiteBalance = 5600f, tint, iso = 800f, iris = 4f, shutterAngle = 180f;
        private float nextSettingRepeat;
        private Volume cameraSettingsVolume;
        private VolumeProfile cameraSettingsProfile;
        private ColorAdjustments cameraExposure;
        private WhiteBalance cameraBalance;
        private UnityEngine.Rendering.Universal.DepthOfField cameraFocus;
        private MotionBlur cameraMotion;
        private bool playerPostProcessing;

        private void MatchPlayerCameraLook(Camera source)
        {
            if (source == null || filmCamera == null) return;
            filmCamera.allowHDR = source.allowHDR;
            filmCamera.allowMSAA = source.allowMSAA;
            filmCamera.backgroundColor = source.backgroundColor;
            filmCamera.clearFlags = source.clearFlags;
            var sourceData = source.GetUniversalAdditionalCameraData();
            var data = filmCamera.GetUniversalAdditionalCameraData();
            playerPostProcessing = sourceData.renderPostProcessing;
            data.renderPostProcessing = playerPostProcessing;
            data.renderShadows = sourceData.renderShadows;
            data.volumeLayerMask = sourceData.volumeLayerMask;
            data.volumeTrigger = sourceData.volumeTrigger != null ? sourceData.volumeTrigger : source.transform;
        }

        // Explicitly prepare manual capture too: render requests can bypass normal camera callbacks.
        public void PrepareRecordingLook()
        {
            ApplyCameraLook();
            HideCameraBody();
            if (cameraSettingsVolume != null) cameraSettingsVolume.weight = 1f;
        }

        public void FinishRecordingLook()
        {
            if (cameraSettingsVolume != null) cameraSettingsVolume.weight = 0f;
            if (!isCameraActive) RestoreCameraBody();
        }
        public bool ManualFocusPracticed { get; private set; }
        public bool WhiteBalancePracticed { get; private set; }
        public bool ExposurePracticed { get; private set; }
        private static readonly float[] IsoStops = {100,200,400,800,1250,1600,2500,3200,6400,12800,25600,32000};
        private static readonly float[] IrisStops = {1.4f,2f,2.8f,4f,5.6f,8f,11f,16f,22f};
        private static readonly float[] ShutterStops = {45,90,144,172.8f,180,216,270,360};

        private void LoadCameraSettings()
        {
            if (settingsLoaded) return;
            settingsLoaded = true;
            GridEnabled = PlayerPrefs.GetInt("CameraControls.Grid", 0) == 1;
            manualFocus = PlayerPrefs.GetInt("CameraControls.Manual",0) == 1;
            manualDistance = Mathf.Clamp(PlayerPrefs.GetFloat("CameraControls.Focus",5f),.1f,100f);
            whiteBalance = Mathf.Clamp(PlayerPrefs.GetFloat("CameraControls.WB",5600f),2500f,9900f);
            tint = Mathf.Clamp(PlayerPrefs.GetFloat("CameraControls.Tint",0),-100,100);
            iso = Mathf.Clamp(PlayerPrefs.GetFloat("CameraControls.ISO",800),100,32000);
            iris = Mathf.Clamp(PlayerPrefs.GetFloat("CameraControls.Iris",4),1.4f,22);
            shutterAngle = Mathf.Clamp(PlayerPrefs.GetFloat("CameraControls.Shutter",180),45,360);
        }

        private void ReadCameraSettings()
        {
            LoadCameraSettings();
            featureLevel = CameraFeatureUnlocks.Level;
            if (featureLevel >= 2 && ruleOfThirdsGrid == null) CreateRuleOfThirdsGrid();
            RefreshGridVisibility();
            EquipmentControls = "[LMB] View | [C] SD | [R] Record | [G] Drop | [Scroll] Zoom | [Q/E] Height | [Ctrl] Smooth move" +
                (featureLevel >= 2 ? " | [F2] Settings | [ / ] Manual focus" : " | Autofocus");
            var key = Keyboard.current;
            if (key == null || featureLevel < 2) { settingsOpen = false; return; }
            if (key.f2Key.wasPressedThisFrame) settingsOpen = !settingsOpen;
            bool changed = false;
            if (key.leftBracketKey.isPressed || key.rightBracketKey.isPressed)
            {
                if (!manualFocus) manualDistance = currentFocusDistance;
                manualFocus = true;
                manualDistance = Mathf.Clamp(manualDistance +
                    (key.rightBracketKey.isPressed ? 1 : -1) * Mathf.Max(.5f,manualDistance*.5f) * Time.deltaTime,.1f,100f);
                ManualFocusPracticed = changed = true;
            }
            if (settingsOpen)
            {
                int rows = featureLevel >= 4 ? 8 : featureLevel >= 3 ? 5 : 3;
                if (key.upArrowKey.wasPressedThisFrame) settingRow = (settingRow+rows-1)%rows;
                if (key.downArrowKey.wasPressedThisFrame) settingRow = (settingRow+1)%rows;
                settingRow = Mathf.Clamp(settingRow,0,rows-1);
                bool press = key.leftArrowKey.wasPressedThisFrame || key.rightArrowKey.wasPressedThisFrame;
                if (press || (settingRow > 1 && Time.unscaledTime >= nextSettingRepeat &&
                    (key.leftArrowKey.isPressed || key.rightArrowKey.isPressed)))
                {
                    nextSettingRepeat = Time.unscaledTime + .15f;
                    int direction = key.rightArrowKey.isPressed ? 1 : -1;
                    switch (settingRow - 1)
                    {
                        case -1: GridEnabled = direction > 0; break;
                        case 0: if (!manualFocus) manualDistance = currentFocusDistance; manualFocus = !manualFocus; break;
                        case 1: manualFocus=true; manualDistance=Mathf.Clamp(manualDistance+direction*.1f,.1f,100f); ManualFocusPracticed=true; break;
                        case 2: whiteBalance=Mathf.Clamp(whiteBalance+direction*100,2500,9900); WhiteBalancePracticed=true; break;
                        case 3: tint=Mathf.Clamp(tint+direction*5,-100,100); WhiteBalancePracticed=true; break;
                        case 4: iso=Step(IsoStops,iso,direction); ExposurePracticed=true; break;
                        case 5: iris=Step(IrisStops,iris,direction); ExposurePracticed=true; break;
                        case 6: shutterAngle=Step(ShutterStops,shutterAngle,direction); ExposurePracticed=true; break;
                    }
                    changed = true;
                }
            }
            if (changed)
            {
                SaveCameraSettings();
                ApplyManualFocus();
                ApplyCameraLook();
                RefreshGridVisibility();
                RefreshDynamicHUD();
            }
        }

        private static float Step(float[] stops,float current,int direction)
        {
            int nearest=0;
            for(int i=1;i<stops.Length;i++) if(Mathf.Abs(stops[i]-current)<Mathf.Abs(stops[nearest]-current))nearest=i;
            return stops[Mathf.Clamp(nearest+direction,0,stops.Length-1)];
        }

        private void SaveCameraSettings()
        {
            PlayerPrefs.SetInt("CameraControls.Grid", GridEnabled ? 1 : 0);
            PlayerPrefs.SetInt("CameraControls.Manual",manualFocus?1:0);
            PlayerPrefs.SetFloat("CameraControls.Focus",manualDistance);
            PlayerPrefs.SetFloat("CameraControls.WB",whiteBalance);
            PlayerPrefs.SetFloat("CameraControls.Tint",tint);
            PlayerPrefs.SetFloat("CameraControls.ISO",iso);
            PlayerPrefs.SetFloat("CameraControls.Iris",iris);
            PlayerPrefs.SetFloat("CameraControls.Shutter",shutterAngle);
        }

        private bool ApplyManualFocus()
        {
            if (featureLevel < 2 || !manualFocus) return false;
            currentFocusDistance = targetFocusDistance = manualDistance;
            if (depthOfField != null) depthOfField.focusDistance.value = manualDistance;
            return true;
        }

        private void ApplyCameraLook()
        {
            if (filmCamera == null) return;
            if (cameraSettingsVolume == null)
            {
                var host = new GameObject("Camera settings - local render");
                host.transform.SetParent(filmCamera.transform,false);
                cameraSettingsVolume=host.AddComponent<Volume>();
                cameraSettingsVolume.isGlobal=true;
                cameraSettingsVolume.priority=10000;
                cameraSettingsVolume.weight=0;
                cameraSettingsProfile=ScriptableObject.CreateInstance<VolumeProfile>();
                cameraSettingsVolume.sharedProfile=cameraSettingsProfile;
                cameraExposure=cameraSettingsProfile.Add<ColorAdjustments>(false);
                cameraBalance=cameraSettingsProfile.Add<WhiteBalance>(false);
                cameraFocus=cameraSettingsProfile.Add<UnityEngine.Rendering.Universal.DepthOfField>(false);
                cameraMotion=cameraSettingsProfile.Add<MotionBlur>(false);
                RenderPipelineManager.beginCameraRendering += PrepareCameraLook;
                RenderPipelineManager.endCameraRendering += FinishCameraLook;
                var data=filmCamera.GetUniversalAdditionalCameraData();
                data.requiresDepthTexture=true;
            }
            // Teaching approximation: aperture/ISO/shutter affect exposure; iris also affects depth of field.
            float exposure=featureLevel>=4 ? Mathf.Log(iso/800f*(16f/(iris*iris))*(shutterAngle/180f),2f) : 0f;
            cameraExposure.postExposure.Override(Mathf.Clamp(exposure,-8,8));
            cameraBalance.temperature.Override(featureLevel>=3 ? Mathf.Clamp((whiteBalance-5600f)/43f,-100,100) : 0);
            cameraBalance.tint.Override(featureLevel>=3 ? tint : 0);
            cameraFocus.mode.Override(DepthOfFieldMode.Bokeh);
            cameraFocus.focusDistance.Override(currentFocusDistance);
            cameraFocus.aperture.Override(featureLevel>=4 ? iris : 4f);
            cameraFocus.focalLength.Override(7.75f / Mathf.Tan(filmCamera.fieldOfView*Mathf.Deg2Rad*.5f));
            cameraMotion.intensity.Override(featureLevel>=4 ? shutterAngle/360f : .5f);
            // Neutral settings preserve the normal view, including its post-processing choice.
            bool adjusted = Mathf.Abs(exposure) > .001f ||
                (featureLevel >= 3 && (Mathf.Abs(whiteBalance - 5600f) > 1f || Mathf.Abs(tint) > .01f)) || manualFocus;
            filmCamera.GetUniversalAdditionalCameraData().renderPostProcessing = playerPostProcessing || adjusted;
        }

        private void PrepareCameraLook(ScriptableRenderContext context,Camera camera)
        {
            if (camera == filmCamera && isCameraActive) HideCameraBody();
            if(cameraSettingsVolume!=null)cameraSettingsVolume.weight=camera==filmCamera && isCameraActive ? 1 : 0;
            if (camera == filmCamera && isCameraActive)
            {
                var data = filmCamera.GetUniversalAdditionalCameraData();
                var trigger = data.volumeTrigger != null ? data.volumeTrigger : filmCamera.transform;
                if (data.volumeStack != null) VolumeManager.instance.Update(data.volumeStack, trigger, data.volumeLayerMask);
                else VolumeManager.instance.Update(trigger, data.volumeLayerMask);
            }
        }
        private void FinishCameraLook(ScriptableRenderContext context,Camera camera)
        {
            if (camera == filmCamera && !isCameraActive) RestoreCameraBody();
            if(cameraSettingsVolume!=null && camera==filmCamera)cameraSettingsVolume.weight=0;
        }
        private void ReleaseCameraSettings()
        {
            RenderPipelineManager.beginCameraRendering-=PrepareCameraLook;
            RenderPipelineManager.endCameraRendering-=FinishCameraLook;
            if(cameraSettingsVolume!=null)Destroy(cameraSettingsVolume.gameObject);
            if(cameraSettingsProfile!=null)
            {
                foreach(var component in cameraSettingsProfile.components)Destroy(component);
                Destroy(cameraSettingsProfile);
            }
        }
        private string SettingsHUDText()
        {
            if (!settingsOpen || featureLevel < 2) return "";
            string[] rows = { "Grid   " + (GridEnabled ? "ON" : "OFF"), "Focus mode   " + (manualFocus ? "MANUAL" : "AF-C"),
                "Focus distance   " + manualDistance.ToString("F1") + "m",
                "White balance   " + whiteBalance.ToString("F0") + "K",
                "Tint   " + tint.ToString("F0"),
                "ISO   " + iso.ToString("F0"),
                "Aperture   F" + iris.ToString("F1"),
                "Shutter   " + shutterAngle.ToString("F1") + " deg" };
            int count = featureLevel >= 4 ? 8 : featureLevel >= 3 ? 5 : 3;
            string text = "<color=#FFD866>CAMERA SETTINGS</color>\n<size=75%>Live preview</size>\n\n";
            for (int i = 0; i < count; i++)
                text += i == settingRow ? "<color=#FFD866>> " + rows[i] + "</color>\n" : "  " + rows[i] + "\n";
            return text + "\n<size=75%>Up/Down: select   Left/Right: adjust\nF2: close</size>";
        }
    }
}
