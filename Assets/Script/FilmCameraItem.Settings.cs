using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using LegacyPost = UnityEngine.Rendering.PostProcessing;
using PlayerPrefs = GameSavePrefs;

namespace Player.Equipment
{
    public partial class FilmCameraItem
    {
        private bool settingsLoaded, settingsOpen, manualFocus;
        public bool GridEnabled { get; private set; }
        public bool SettingsOpen => settingsOpen;

        private void RefreshGridVisibility()
        {
            if (ruleOfThirdsGrid == null) return;
            bool practice = GokeLevelManager.Instance != null && GokeLevelManager.Instance.ThirdsPracticeActive;
            ruleOfThirdsGrid.SetActive(isCameraActive && CameraFeatureUnlocks.Level >= 2 && (GridEnabled || practice));
            bool lesson = GokeLevelManager.Instance != null && GokeLevelManager.Instance.ShowThirdsLessonGuide;
            foreach (Transform child in ruleOfThirdsGrid.transform)
                if (child.name.Contains("Lesson Panel")) child.gameObject.SetActive(practice && !settingsOpen);
                else if (child.name.Contains("Power Point") || child.name.Contains("Label"))
                    child.gameObject.SetActive(lesson && GridEnabled);
                else child.gameObject.SetActive(GridEnabled);
        }
        private int settingRow, featureLevel;
        private float manualDistance = 5f, whiteBalance = 5600f, tint, iso = 800f, iris = 4f, shutterAngle = 180f;
        private float nextSettingRepeat;
        private bool recordingLookActive;
        private Volume cameraSettingsVolume;
        private VolumeProfile cameraSettingsProfile;
        private ColorAdjustments cameraExposure;
        private WhiteBalance cameraBalance;
        private UnityEngine.Rendering.Universal.DepthOfField cameraFocus;
        private MotionBlur cameraMotion;
        private bool playerPostProcessing;
        private LegacyPost.PostProcessLayer builtInCameraLayer;
        private LegacyPost.PostProcessVolume builtInSettingsVolume;
        private LegacyPost.PostProcessProfile builtInSettingsProfile;
        private LegacyPost.ColorGrading builtInGrading;
        private LegacyPost.DepthOfField builtInFocus;
        private LegacyPost.MotionBlur builtInMotion;
        private LayerMask originalBuiltInVolumeMask;
        private bool originalBuiltInLayerEnabled, createdBuiltInLayer;

        private void MatchPlayerCameraLook(Camera source)
        {
            if (source == null || filmCamera == null) return;
            filmCamera.allowHDR = source.allowHDR;
            filmCamera.allowMSAA = source.allowMSAA;
            filmCamera.backgroundColor = source.backgroundColor;
            filmCamera.clearFlags = source.clearFlags;
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) return;
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
            recordingLookActive = true;
            ApplyCameraLook();
            HideCameraBody();
            if (cameraSettingsVolume != null) cameraSettingsVolume.weight = 1f;
            if (builtInSettingsVolume != null) builtInSettingsVolume.weight = 1f;
            UpdateSettingsVolumeStack();
        }

        public void FinishRecordingLook()
        {
            recordingLookActive = false;
            if (cameraSettingsVolume != null) cameraSettingsVolume.weight = 0f;
            if (builtInSettingsVolume != null) builtInSettingsVolume.weight = 0f;
            if (!isCameraActive) RestoreCameraBody();
        }

        // Both the viewfinder and recorder must prepare the look before rendering,
        // not depend on automatic camera/volume callback ordering.
        public void RenderCameraFrame(RenderTexture destination)
        {
            if (filmCamera == null || destination == null) return;
            RenderTexture previousTarget = filmCamera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                PrepareRecordingLook();
                if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
                {
                    var request = new UniversalRenderPipeline.SingleCameraRequest { destination = destination };
                    RenderPipeline.SubmitRenderRequest(filmCamera, request);
                }
                else
                {
                    filmCamera.targetTexture = destination;
                    filmCamera.Render();
                }
            }
            finally
            {
                FinishRecordingLook();
                filmCamera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
            }
        }
        public bool ManualFocusPracticed { get; private set; }
        public float ViewfinderFieldOfView => filmCamera != null ? filmCamera.fieldOfView : 180f;
        public bool WhiteBalancePracticed { get; private set; }
        public float WhiteBalanceKelvin => whiteBalance;
        public float WhiteBalanceTint => tint;
        public bool ExposurePracticed { get; private set; }
        private static readonly float[] IsoStops = {100,200,400,800,1250,1600,2500,3200,6400,12800,25600,32000};
        private static readonly float[] IrisStops = {1.4f,2f,2.8f,4f,5.6f,8f,11f,16f,22f};
        private static readonly float[] ShutterStops = {45,90,144,172.8f,180,216,270,360};

        private void LoadCameraSettings()
        {
            if (settingsLoaded) return;
            settingsLoaded = true;
            GridEnabled = PlayerPrefs.GetInt("CameraControls.Grid", 0) == 1;
            manualFocus = false; // Autofocus only, including previously saved manual settings.
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
                (featureLevel >= 2 ? " | [F2] Settings | Autofocus" : " | Autofocus");
            var key = Keyboard.current;
            if (key == null || featureLevel < 2) { settingsOpen = false; return; }
            if (key.f2Key.wasPressedThisFrame) settingsOpen = !settingsOpen;
            bool changed = false;
            if (settingsOpen)
            {
                int rows = featureLevel >= 4 ? 6 : featureLevel >= 3 ? 3 : 1;
                if (key.upArrowKey.wasPressedThisFrame) settingRow = (settingRow+rows-1)%rows;
                if (key.downArrowKey.wasPressedThisFrame) settingRow = (settingRow+1)%rows;
                settingRow = Mathf.Clamp(settingRow,0,rows-1);
                bool press = key.leftArrowKey.wasPressedThisFrame || key.rightArrowKey.wasPressedThisFrame;
                if (press || (settingRow > 0 && Time.unscaledTime >= nextSettingRepeat &&
                    (key.leftArrowKey.isPressed || key.rightArrowKey.isPressed)))
                {
                    nextSettingRepeat = Time.unscaledTime + (press ? .3f : .06f);
                    int direction = key.rightArrowKey.isPressed ? 1 : -1;
                    switch (settingRow == 0 ? -1 : settingRow + 1)
                    {
                        case -1: GridEnabled = direction > 0; break;
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
            manualFocus = false;
            return false;
        }

        private void ApplyCameraLook()
        {
            if (filmCamera == null) return;
            LoadCameraSettings();
            featureLevel = CameraFeatureUnlocks.Level;
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
            {
                ApplyBuiltInCameraLook();
                return;
            }
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
            // The runtime Volume must be on a layer the film camera actually reads.
            var cameraData = filmCamera.GetUniversalAdditionalCameraData();
            int volumeMask = cameraData.volumeLayerMask.value;
            if (volumeMask == 0) { volumeMask = 1; cameraData.volumeLayerMask = volumeMask; }
            for (int layer = 0; layer < 32; layer++)
                if ((volumeMask & (1 << layer)) != 0) { cameraSettingsVolume.gameObject.layer = layer; break; }
            cameraBalance.active = true;
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
            if(cameraSettingsVolume!=null)cameraSettingsVolume.weight=camera==filmCamera && (isCameraActive || recordingLookActive) ? 1 : 0;
            if (camera == filmCamera && (isCameraActive || recordingLookActive))
            {
                UpdateSettingsVolumeStack();
            }
        }
        private void UpdateSettingsVolumeStack()
        {
            if (filmCamera == null || !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) return;
            var data = filmCamera.GetUniversalAdditionalCameraData();
            var trigger = data.volumeTrigger != null ? data.volumeTrigger : filmCamera.transform;
            if (data.volumeStack != null) VolumeManager.instance.Update(data.volumeStack, trigger, data.volumeLayerMask);
            else VolumeManager.instance.Update(trigger, data.volumeLayerMask);
        }
        private void FinishCameraLook(ScriptableRenderContext context,Camera camera)
        {
            if (camera == filmCamera && !isCameraActive) RestoreCameraBody();
            if(cameraSettingsVolume!=null && camera==filmCamera)cameraSettingsVolume.weight=0;
        }
        private void ReleaseCameraSettings()
        {
            Camera.onPreCull -= PrepareBuiltInCameraLook;
            Camera.onPostRender -= FinishBuiltInCameraLook;
            if (builtInCameraLayer != null)
            {
                builtInCameraLayer.volumeLayer = originalBuiltInVolumeMask;
                builtInCameraLayer.enabled = originalBuiltInLayerEnabled;
                if (createdBuiltInLayer) Destroy(builtInCameraLayer);
            }
            if (builtInSettingsVolume != null) Destroy(builtInSettingsVolume.gameObject);
            if (builtInSettingsProfile != null)
            {
                foreach (var setting in builtInSettingsProfile.settings) Destroy(setting);
                Destroy(builtInSettingsProfile);
            }
            RenderPipelineManager.beginCameraRendering-=PrepareCameraLook;
            RenderPipelineManager.endCameraRendering-=FinishCameraLook;
            if(cameraSettingsVolume!=null)Destroy(cameraSettingsVolume.gameObject);
            if(cameraSettingsProfile!=null)
            {
                foreach(var component in cameraSettingsProfile.components)Destroy(component);
                Destroy(cameraSettingsProfile);
            }
        }
        private void ApplyBuiltInCameraLook()
        {
            if (builtInSettingsProfile == null)
            {
                builtInCameraLayer = filmCamera.GetComponent<LegacyPost.PostProcessLayer>();
                createdBuiltInLayer = builtInCameraLayer == null;
                if (createdBuiltInLayer)
                {
                    var resources = Resources.Load<LegacyPost.PostProcessResources>("FilmCameraPostProcessResources");
                    if (resources == null) { Debug.LogError("Film-camera post-processing resources are missing.", this); return; }
                    builtInCameraLayer = filmCamera.gameObject.AddComponent<LegacyPost.PostProcessLayer>();
                    builtInCameraLayer.Init(resources);
                }
                originalBuiltInLayerEnabled = !createdBuiltInLayer && builtInCameraLayer.enabled;
                originalBuiltInVolumeMask = builtInCameraLayer.volumeLayer;
                var host = new GameObject("Camera settings - built-in render");
                host.transform.SetParent(filmCamera.transform, false);
                host.layer = 31;
                builtInSettingsVolume = host.AddComponent<LegacyPost.PostProcessVolume>();
                builtInSettingsVolume.isGlobal = true;
                builtInSettingsVolume.priority = 10000;
                builtInSettingsVolume.weight = 0;
                builtInSettingsProfile = ScriptableObject.CreateInstance<LegacyPost.PostProcessProfile>();
                builtInSettingsVolume.sharedProfile = builtInSettingsProfile;
                builtInGrading = builtInSettingsProfile.AddSettings<LegacyPost.ColorGrading>();
                builtInFocus = builtInSettingsProfile.AddSettings<LegacyPost.DepthOfField>();
                builtInMotion = builtInSettingsProfile.AddSettings<LegacyPost.MotionBlur>();
                Camera.onPreCull += PrepareBuiltInCameraLook;
                Camera.onPostRender += FinishBuiltInCameraLook;
            }
            builtInCameraLayer.volumeLayer = originalBuiltInVolumeMask.value | (1 << 31);
            builtInCameraLayer.enabled = true;
            // postExposure is an HDR-grading parameter in the built-in stack.
            builtInGrading.enabled.Override(true);
            builtInGrading.gradingMode.Override(LegacyPost.GradingMode.HighDefinitionRange);
            float exposure = featureLevel >= 4 ? Mathf.Log(iso / 800f * (16f / (iris * iris)) * (shutterAngle / 180f), 2f) : 0f;
            builtInGrading.postExposure.Override(Mathf.Clamp(exposure, -8, 8));
            builtInGrading.temperature.Override(featureLevel >= 3 ? Mathf.Clamp((whiteBalance - 5600f) / 43f, -100, 100) : 0);
            builtInGrading.tint.Override(featureLevel >= 3 ? tint : 0);
            builtInFocus.enabled.Override(true);
            builtInFocus.focusDistance.Override(currentFocusDistance);
            builtInFocus.aperture.Override(featureLevel >= 4 ? iris : 4f);
            builtInFocus.focalLength.Override(7.75f / Mathf.Tan(filmCamera.fieldOfView * Mathf.Deg2Rad * .5f));
            builtInMotion.enabled.Override(featureLevel >= 4);
            builtInMotion.shutterAngle.Override(featureLevel >= 4 ? shutterAngle : 180f);
            builtInSettingsVolume.weight = isCameraActive || recordingLookActive ? 1 : 0;
        }

        private void PrepareBuiltInCameraLook(Camera camera)
        {
            if (builtInSettingsVolume == null) return;
            // Scope the private profile to this camera; other scene views stay unchanged.
            builtInSettingsVolume.weight = camera == filmCamera && (isCameraActive || recordingLookActive) ? 1 : 0;
            if (camera == filmCamera && isCameraActive) HideCameraBody();
        }

        private void FinishBuiltInCameraLook(Camera camera)
        {
            if (builtInSettingsVolume != null && camera == filmCamera) builtInSettingsVolume.weight = 0;
            if (camera == filmCamera && !isCameraActive) RestoreCameraBody();
        }

        private string SettingsHUDText()
        {
            if (!settingsOpen || featureLevel < 2) return "";
            string[] rows = { "Grid   " + (GridEnabled ? "ON" : "OFF"),
                "White balance   " + whiteBalance.ToString("F0") + "K",
                "Tint   " + tint.ToString("F0"),
                "ISO   " + iso.ToString("F0"),
                "Aperture   F" + iris.ToString("F1"),
                "Shutter   " + shutterAngle.ToString("F1") + " deg" };
            int count = featureLevel >= 4 ? 6 : featureLevel >= 3 ? 3 : 1;
            string text = "<color=#FFD866>CAMERA SETTINGS</color>\n<size=75%>Live preview</size>\n\n";
            for (int i = 0; i < count; i++)
                text += i == settingRow ? "<color=#FFD866>> " + rows[i] + "</color>\n" : "  " + rows[i] + "\n";
            return text + "\n<size=75%>Up/Down: select | Left/Right: adjust\nHold Left/Right: faster adjustment\n[F2] Close settings</size>";
        }
    }
}
