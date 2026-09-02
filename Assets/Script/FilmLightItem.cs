using UnityEngine;
using Player.Manager;
using TMPro;
using UnityEngine.UI;

namespace Player.Equipment
{
    public class FilmLightItem : Equipment
    {
        [Header("Light Component")]
        public Light spotlight;

        [Header("Main Features: Intensity & Meter")]
        [Range(0, 100)]
        [Tooltip("0 to 100% Intensity Slider")]
        public float intensityPercent = 100f;

        [Tooltip("The maximum Lux output at 100%")]
        public float maxLux = 4f;

        [Header("Studio Beam Profile")]
        public float standardRange = 15f;
        public float advancedRange = 22f;
        public Vector3 heldBeamPosition = new Vector3(0.35f, -0.15f, 0.9f);
        public Vector3 heldBeamRotation = new Vector3(0f, 90f, 0f);

        [Header("Stand Tilt")]
        public float tiltStep = 5f;
        public float maxTiltUp = -45f;
        public float maxTiltDown = 45f;

        [Header("Low-End Restrictions (160 LED Panel)")]
        [Tooltip("Prevents matching the room's ambient light")]
        public bool isFixedKelvin = true;
        public float fixedColorTemperature = 5600f; // 5600K Daylight

        [Tooltip("Forces hard shadows - Will trigger Academic Error on AI Client")]
        public bool forcesHardLight = true;

        [Header("Level 3 Soft Light Features")]
        [Range(3200, 6500)]
        public float colorTemperature = 4300f;
        public float colorTemperatureStep = 1100f;

        [Range(0, 100)]
        public float diffusionPercent = 50f;
        public float diffusionStep = 25f;

        // --- HUD UI REFERENCES ---
        [Header("--- HUD UI REFERENCES ---")]
        public GameObject lightUICanvas;

        [Header("Text & Sliders")]
        public TMP_Text intensityText;
        public TMP_Text tiltText;
        public Slider tiltSlider;
        public Slider intensitySlider;

        [Header("Status Icons")]
        public GameObject lightOnIcon;
        public GameObject lightOffIcon;

        [Header("Level 3 Feature UI")]
        public GameObject advancedFeaturePanel;
        public TMP_Text temperatureText;
        public TMP_Text diffusionText;

        private bool isLightOn = false;
        private float currentTilt = 0f;
        private Vector3 startAngles;
        private Vector3 originalSpotlightPosition;
        private Transform heldBeamReference;

        // Safety lock for Shop Prefabs!
        private bool isHeld = false;

        protected override void Awake()
        {
            base.Awake();

            if (spotlight != null)
            {
                originalSpotlightPosition = spotlight.transform.localPosition;
                ResetHeldBeamAim();
                spotlight.enabled = isLightOn;
                UpdateLightOutput();
            }

            // Hide the UI instantly when spawned by the shop
            if (lightUICanvas != null) lightUICanvas.SetActive(false);
        }

        // Triggered when swapping TO this item in your Hotbar
        private void OnEnable()
        {
            if (isHeld && lightUICanvas != null)
            {
                lightUICanvas.SetActive(true);
                UpdateLightUI();
            }
        }

        // Triggered when swapping AWAY from this item in your Hotbar
        private void OnDisable()
        {
            if (lightUICanvas != null) lightUICanvas.SetActive(false);
        }

        // Triggered when you press E to pick it up off the shop table
        public override void OnPickedUp(Transform holdPoint)
        {
            base.OnPickedUp(holdPoint);
            SetLevel3PlaceholderHeld(true);
            isHeld = true;
            heldBeamReference = FindHeldBeamReference(holdPoint);
            ResetHeldBeamAim();
            EnsureAdvancedFeatureUI();
            if (lightUICanvas != null) lightUICanvas.SetActive(true);
            UpdateLightUI();
        }

        // Triggered when you press G to drop it
        public override void OnDropped(Camera playerCamera)
        {
            Quaternion beamRotation = spotlight != null ? spotlight.transform.rotation : Quaternion.identity;
            Vector3 flatForward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;

            if (flatForward.sqrMagnitude <= 0.001f) flatForward = playerCamera.transform.forward;

            Vector3 dropPosition = FindLightDropPosition(playerCamera, flatForward);
            Quaternion dropRotation = Quaternion.LookRotation(flatForward, Vector3.up);

            SetLevel3PlaceholderHeld(false);
            isHeld = false;
            heldBeamReference = null;
            base.OnDropped(playerCamera);
            transform.SetPositionAndRotation(dropPosition, dropRotation);

            if (allRigidbodies != null)
            {
                foreach (Rigidbody lightRigidbody in allRigidbodies)
                {
                    if (lightRigidbody == null) continue;

                    lightRigidbody.velocity = Vector3.zero;
                    lightRigidbody.angularVelocity = Vector3.zero;
                }
            }

            if (spotlight != null)
            {
                spotlight.transform.localPosition = originalSpotlightPosition;
                spotlight.transform.rotation = beamRotation;
                startAngles = spotlight.transform.localEulerAngles;
            }

            if (lightUICanvas != null) lightUICanvas.SetActive(false);
        }

        private Vector3 FindLightDropPosition(Camera playerCamera, Vector3 flatForward)
        {
            Vector3 dropPosition = playerCamera.transform.position + flatForward * 1.75f;
            Vector3 floorCheckPosition = dropPosition + Vector3.up * 2f;

            if (Physics.Raycast(floorCheckPosition, Vector3.down, out RaycastHit floorHit, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                float floorOffset = EquipmentName == "Level 3 Soft Light" ? 0.02f : 0.54f;
                dropPosition = floorHit.point + Vector3.up * floorOffset;
            }

            return dropPosition;
        }

        private void SetLevel3PlaceholderHeld(bool isBeingHeld)
        {
            if (EquipmentName != "Level 3 Soft Light") return;

            Transform lightPanel = transform.Find("Thin Light Panel");
            Transform lightStand = transform.Find("Thin Light Stand");
            Transform lightBase = transform.Find("Thin Light Base");

            if (lightPanel != null)
            {
                lightPanel.localPosition = isBeingHeld ? Vector3.zero : new Vector3(0f, 1.15f, 0f);
                lightPanel.localScale = isBeingHeld ? new Vector3(0.1f, 0.3f, 0.58f) : new Vector3(0.12f, 0.42f, 0.82f);
            }

            if (lightStand != null) lightStand.gameObject.SetActive(!isBeingHeld);
            if (lightBase != null) lightBase.gameObject.SetActive(!isBeingHeld);
        }

        public override void OnUse(Camera playerCamera)
        {
            isLightOn = !isLightOn;
            if (spotlight != null) spotlight.enabled = isLightOn;

            if (isLightOn && TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnLightTurnedOn(this);
            }

            UpdateLightUI();
        }

        public override void OnHeldUpdate(InputManager input)
        {
            UpdateHeldBeamTransform();
            if (!isLightOn) return;

            float scroll = input.EquipmentAdjust;
            if (scroll > 0) AdjustIntensity(5f);
            else if (scroll < 0) AdjustIntensity(-5f);

            if (input.LightTilt != 0f) TiltLight(input.LightTilt * tiltStep);

            if (HasAdvancedFeatures())
            {
                if (input.LightTemperature != 0f) AdjustColorTemperature(input.LightTemperature * colorTemperatureStep);
                if (input.LightDiffusion != 0f) AdjustDiffusion(input.LightDiffusion * diffusionStep);
            }
        }

        public bool IsPoweredOn()
        {
            return isLightOn && spotlight != null && spotlight.enabled;
        }

        public float GetCurrentTilt()
        {
            return currentTilt;
        }

        public float GetCurrentOutput()
        {
            return maxLux * (intensityPercent / 100f);
        }

        public float GetColorTemperature()
        {
            return isFixedKelvin ? fixedColorTemperature : colorTemperature;
        }

        public float GetDiffusionPercent()
        {
            return forcesHardLight ? 0f : diffusionPercent;
        }

        public bool HasAdvancedFeatures()
        {
            return !isFixedKelvin && !forcesHardLight;
        }

        public void RefreshAdvancedFeatures()
        {
            ApplyFeatureSettings();
            EnsureAdvancedFeatureUI();
            UpdateLightUI();
        }

        public void AimAt(Vector3 targetPosition)
        {
            if (spotlight == null) return;

            Vector3 targetDirection = targetPosition - spotlight.transform.position;
            if (targetDirection.sqrMagnitude <= 0.001f) return;

            spotlight.transform.rotation = Quaternion.LookRotation(targetDirection.normalized, Vector3.up);
            startAngles = spotlight.transform.localEulerAngles;
            currentTilt = 0f;
            UpdateLightUI();
        }

        private void TiltLight(float amount)
        {
            if (spotlight == null) return;
            currentTilt = Mathf.Clamp(currentTilt + amount, maxTiltUp, maxTiltDown);
            UpdateLightTransform();

            UpdateLightUI(); // Update the UI bar and text!

            // --- THE FIX: Send the current tilt number! ---
            if (TutorialManager.Instance != null) TutorialManager.Instance.OnLightTilted(currentTilt);
        }

        private void UpdateLightTransform()
        {
            if (spotlight == null) return;

            if (isHeld && heldBeamReference != null)
            {
                UpdateHeldBeamTransform();
                return;
            }

            spotlight.transform.localEulerAngles = new Vector3(startAngles.x + currentTilt, startAngles.y, startAngles.z);
        }

        private void UpdateHeldBeamTransform()
        {
            if (!isHeld || heldBeamReference == null || spotlight == null) return;

            spotlight.transform.position = heldBeamReference.TransformPoint(heldBeamPosition);
            spotlight.transform.rotation = heldBeamReference.rotation * Quaternion.Euler(currentTilt, 0f, 0f);
        }

        private Transform FindHeldBeamReference(Transform holdPoint)
        {
            Camera[] playerCameras = holdPoint.root.GetComponentsInChildren<Camera>(true);

            foreach (Camera playerCamera in playerCameras)
            {
                if (playerCamera.enabled && playerCamera.targetTexture == null) return playerCamera.transform;
            }

            return holdPoint.parent != null ? holdPoint.parent : holdPoint;
        }

        private void ResetHeldBeamAim()
        {
            if (spotlight == null) return;

            startAngles = heldBeamRotation;
            UpdateLightTransform();
        }

        private void AdjustIntensity(float amount)
        {
            intensityPercent = Mathf.Clamp(intensityPercent + amount, 0f, 100f);
            UpdateLightOutput();

            UpdateLightUI(); // Update the UI text & slider!

            // --- THE FIX: Send the intensity percentage! ---
            if (TutorialManager.Instance != null) TutorialManager.Instance.OnLightIntensityChanged(intensityPercent, this);
        }

        private void AdjustColorTemperature(float amount)
        {
            colorTemperature = Mathf.Clamp(colorTemperature + amount, 3200f, 6500f);
            colorTemperature = Mathf.Round(colorTemperature / 100f) * 100f;
            ApplyFeatureSettings();
            UpdateLightUI();

            if (TutorialManager.Instance != null) TutorialManager.Instance.OnLightFeatureChanged(this);
        }

        private void AdjustDiffusion(float amount)
        {
            diffusionPercent = Mathf.Clamp(diffusionPercent + amount, 0f, 100f);
            ApplyFeatureSettings();
            UpdateLightUI();

            if (TutorialManager.Instance != null) TutorialManager.Instance.OnLightFeatureChanged(this);
        }

        private void UpdateLightOutput()
        {
            if (spotlight != null)
            {
                spotlight.intensity = maxLux * (intensityPercent / 100f);
                ApplyFeatureSettings();
            }
        }

        private void ApplyFeatureSettings()
        {
            if (spotlight == null) return;

            spotlight.useColorTemperature = true;
            spotlight.colorTemperature = GetColorTemperature();

            if (!HasAdvancedFeatures())
            {
                spotlight.range = standardRange;
                spotlight.spotAngle = 48f;
                spotlight.innerSpotAngle = 38f;
                spotlight.shadows = LightShadows.Hard;
                spotlight.shadowStrength = 0.72f;
                return;
            }

            float diffusionAmount = diffusionPercent / 100f;
            spotlight.range = advancedRange;
            spotlight.spotAngle = Mathf.Lerp(48f, 75f, diffusionAmount);
            spotlight.innerSpotAngle = Mathf.Lerp(42f, 32f, diffusionAmount);
            spotlight.shadows = LightShadows.Soft;
            spotlight.shadowStrength = Mathf.Lerp(0.8f, 0.45f, diffusionAmount);
        }

        private void EnsureAdvancedFeatureUI()
        {
            if (!HasAdvancedFeatures()) return;

            if (advancedFeaturePanel != null)
            {
                RemoveAdvancedFeatureBackground();
                PositionAdvancedFeaturePanel();
                return;
            }

            if (lightUICanvas == null)
            {
                lightUICanvas = new GameObject("Level 3 Light UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                lightUICanvas.transform.SetParent(transform, false);

                Canvas canvas = lightUICanvas.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 30;

                CanvasScaler canvasScaler = lightUICanvas.GetComponent<CanvasScaler>();
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            advancedFeaturePanel = new GameObject("Level 3 Feature Text", typeof(RectTransform));
            advancedFeaturePanel.transform.SetParent(lightUICanvas.transform, false);

            PositionAdvancedFeaturePanel();

            CreateFeatureText("Header", "LEVEL 3 SOFT LIGHT", new Vector2(0f, 50f), 25f, Color.white, TextAlignmentOptions.Center);
            temperatureText = CreateFeatureText("Temperature", "", new Vector2(-205f, 12f), 22f, Color.white, TextAlignmentOptions.Left);
            diffusionText = CreateFeatureText("Diffusion", "", new Vector2(-205f, -22f), 22f, Color.white, TextAlignmentOptions.Left);
            CreateFeatureText("Controls", "[Z / X] TEMPERATURE     [V / B] DIFFUSION", new Vector2(0f, -58f), 17f, Color.white, TextAlignmentOptions.Center);
        }

        private void RemoveAdvancedFeatureBackground()
        {
            Image panelImage = advancedFeaturePanel.GetComponent<Image>();
            Outline panelOutline = advancedFeaturePanel.GetComponent<Outline>();

            if (panelImage != null) Destroy(panelImage);
            if (panelOutline != null) Destroy(panelOutline);
        }

        private void PositionAdvancedFeaturePanel()
        {
            if (advancedFeaturePanel == null) return;

            RectTransform panelRect = advancedFeaturePanel.GetComponent<RectTransform>();
            if (panelRect == null) return;

            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.anchoredPosition = new Vector2(-45f, 55f);
            panelRect.sizeDelta = new Vector2(470f, 150f);
        }

        private TMP_Text CreateFeatureText(string objectName, string text, Vector2 position, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(advancedFeaturePanel.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = alignment == TextAlignmentOptions.Left ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = position;
            textRect.sizeDelta = new Vector2(420f, 40f);

            TMP_Text featureText = textObject.GetComponent<TMP_Text>();
            featureText.text = text;
            featureText.fontSize = fontSize;
            featureText.color = color;
            featureText.alignment = alignment;
            featureText.enableWordWrapping = false;

            if (intensityText != null)
            {
                featureText.font = intensityText.font;
                featureText.fontSharedMaterial = intensityText.fontSharedMaterial;
            }

            return featureText;
        }

        // Syncs the numbers and images to your visual HUD
        private void UpdateLightUI()
        {
            EnsureAdvancedFeatureUI();

            // 1. Intensity Text Update
            if (intensityText != null)
            {
                intensityText.text = $"{Mathf.RoundToInt(intensityPercent)}%";
                intensityText.color = isLightOn ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
            }

            // 2. Tilt Text Update
            if (tiltText != null)
            {
                tiltText.text = $"{Mathf.RoundToInt(currentTilt)} degrees";
                tiltText.color = isLightOn ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
            }

            // 3. Tilt Slider Update
            if (tiltSlider != null)
            {
                // The lowest numerical value (-45) must be the minValue
                tiltSlider.minValue = maxTiltUp;

                // The highest numerical value (45) must be the maxValue
                tiltSlider.maxValue = maxTiltDown;

                tiltSlider.value = currentTilt;
            }

            // 4. Intensity Slider Update
            if (intensitySlider != null)
            {
                intensitySlider.minValue = 0f;
                intensitySlider.maxValue = 100f;
                intensitySlider.value = intensityPercent;
            }

            // 5. Swap the Icons!
            if (lightOnIcon != null) lightOnIcon.SetActive(isLightOn);
            if (lightOffIcon != null) lightOffIcon.SetActive(!isLightOn);

            if (advancedFeaturePanel != null) advancedFeaturePanel.SetActive(HasAdvancedFeatures());

            if (temperatureText != null)
            {
                string temperatureName = colorTemperature <= 3600f ? "WARM" : colorTemperature >= 5200f ? "DAYLIGHT" : "NEUTRAL";
                temperatureText.text = "COLOR TEMP     " + Mathf.RoundToInt(colorTemperature) + "K  " + temperatureName;
                temperatureText.color = isLightOn ? new Color(1f, 0.82f, 0.52f, 1f) : new Color(0.5f, 0.5f, 0.5f, 1f);
            }

            if (diffusionText != null)
            {
                string diffusionName = diffusionPercent >= 75f ? "SOFT" : diffusionPercent >= 40f ? "MEDIUM" : "HARD";
                diffusionText.text = "DIFFUSION       " + Mathf.RoundToInt(diffusionPercent) + "%  " + diffusionName;
                diffusionText.color = isLightOn ? new Color(0.55f, 0.9f, 1f, 1f) : new Color(0.5f, 0.5f, 0.5f, 1f);
            }
        }
    }
}
