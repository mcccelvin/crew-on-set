using UnityEngine;
using UnityEngine.InputSystem;
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

        [Tooltip("Unity light intensity at 100% (not a calibrated lux measurement).")]
        public float maxLux = 4f;

        [Header("Studio Beam Profile")]
        public float standardRange = 30f;
        public float advancedRange = 40f;
        [Tooltip("Panel centre in player-camera space, away from the crosshair.")]
        public Vector3 heldPanelPosition = new Vector3(0.52f, -0.30f, 1.15f);
        [Range(0.3f, 1f)] public float heldPresentationScale = 0.55f;

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
        private Transform headPivot;
        private Quaternion neutralBeamRotation;
        private Renderer diffuserRenderer;
        private MaterialPropertyBlock diffuserProperties;
        private Material diffuserMaterial;
        private Transform heldBeamReference;

        // Safety lock for Shop Prefabs!
        private bool isHeld = false;
        private StudioLightHaze haze;
        
        private float heightExtension = .5f;
        private Vector3 originalHeadPosition;
        private Transform originalStand;
        private Vector3 standScale, standPosition;
        private float standHeight, standBottom;
        public float HeightExtension => heightExtension;

        public void AdjustStandHeight(float metres)
        {
            if (headPivot == null) return;
            heightExtension = Mathf.Clamp(heightExtension + metres, 0f, 1.5f);
            float rootScale = Mathf.Max(.001f, Mathf.Abs(transform.lossyScale.y));
            // Stored metres describe the deployed stand, not its miniature held model.
            float deployedScale = isHeld ? rootScale / heldPresentationScale : rootScale;
            headPivot.localPosition = originalHeadPosition + Vector3.up * (heightExtension / deployedScale);
            if (originalStand != null && standHeight > .001f)
            {
                float extension = heightExtension / deployedScale;
                float factor = 1f + extension / standHeight;
                originalStand.localScale = new Vector3(standScale.x, standScale.y * factor, standScale.z);
                // Preserve the authored stand's bottom, not its mesh pivot.
                originalStand.localPosition = standPosition;
                float newBottom = GetStandBounds().min.y;
                originalStand.position += transform.up * ((standBottom - newBottom) * rootScale);
            }
            RefreshPlacementControls();
        }

        private void RefreshPlacementControls()
        {
            EquipmentControls = "[LMB] Power " + (isLightOn ? "ON" : "OFF") +
                " | [SCROLL] Intensity " + Mathf.RoundToInt(intensityPercent) + "%" +
                " | [ARROWS] Tilt " + currentTilt.ToString("+0;-0;0") + "°" +
                " | [Q UP / E DOWN] Height +" + heightExtension.ToString("F2") + " m | [G] Drop";
            if (HasAdvancedFeatures()) EquipmentControls += " | [Z / K] Temperature " + Mathf.RoundToInt(colorTemperature) + "K | [V / B] Diffusion " + Mathf.RoundToInt(diffusionPercent) + "%";
        }

        protected override void Awake()
        {
            base.Awake();

            if (spotlight != null)
            {
                ConfigureLightHead();
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
                lightUICanvas.SetActive(false);
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
            if (holdPoint == null || isHeld) return;
            base.OnPickedUp(holdPoint);
            isHeld = true;
            heldBeamReference = FindHeldBeamReference(holdPoint);
            transform.localScale *= heldPresentationScale;
            UpdateHeldBeamTransform();
            RefreshPlacementControls();
            if (lightUICanvas != null) lightUICanvas.SetActive(false);
            UpdateLightUI();
        }

        // Triggered when you press G to drop it
        public override void OnDropped(Camera playerCamera)
        {
            // Keep the same heading and head tilt. Only lower the stand onto its surface.
            Quaternion headRotation = headPivot != null ? headPivot.rotation : Quaternion.identity;
            Vector3 dropPosition = transform.position;
            isHeld = false;
            heldBeamReference = null;
            base.OnDropped(playerCamera);
            transform.position = dropPosition;
            if (headPivot != null) headPivot.rotation = headRotation;
            SettleOnSurface(playerCamera);

            if (allRigidbodies != null)
            {
                foreach (Rigidbody lightRigidbody in allRigidbodies)
                {
                    if (lightRigidbody == null) continue;

                    if (!lightRigidbody.isKinematic)
                    {
                        lightRigidbody.velocity = Vector3.zero;
                        lightRigidbody.angularVelocity = Vector3.zero;
                    }
                }
            }

            if (lightUICanvas != null) lightUICanvas.SetActive(false);
        }

        private void SettleOnSurface(Camera playerCamera)
        {
            float originHeight = playerCamera != null ? playerCamera.transform.position.y + 0.5f : transform.position.y + 2f;
            Vector3 origin = new Vector3(transform.position.x, originHeight, transform.position.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Vector3 surface = transform.position;
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(transform) || Vector3.Dot(hit.normal, Vector3.up) < 0.7f) continue;
                if (playerCamera != null && hit.transform.IsChildOf(playerCamera.transform.root)) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance;
                surface = hit.point;
            }
            if (!float.IsPositiveInfinity(nearest)) PlaceOnSurface(surface);
        }

        public void PlaceOnSurface(Vector3 surface)
        {
            transform.position = new Vector3(surface.x, transform.position.y, surface.z);
            float bottom = float.PositiveInfinity;
            foreach (Renderer part in GetComponentsInChildren<Renderer>(true))
            {
                if (part is MeshRenderer && part.enabled && part.GetComponent<StudioLightHaze>() == null) bottom = Mathf.Min(bottom, part.bounds.min.y);
            }
            if (!float.IsPositiveInfinity(bottom)) transform.position += Vector3.up * (surface.y + 0.01f - bottom);
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
            if (input != null && input.CanReadGameplayAction() && Keyboard.current != null)
            {
                var keys = Keyboard.current;
                float heightInput = (keys.qKey.isPressed ? 1f : 0f) - (keys.eKey.isPressed ? 1f : 0f);
                if (heightInput != 0) AdjustStandHeight(heightInput * .6f * Time.deltaTime);

            }
            if (!isLightOn) return;

            float scroll = input.EquipmentAdjust;
            if (scroll > 0) AdjustIntensity(5f);
            else if (scroll < 0) AdjustIntensity(-5f);

            if (input.LightTilt != 0f) TiltLight(-input.LightTilt * tiltStep);

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
            UpdateLightOutput();
            RefreshPlacementControls();
            UpdateLightUI();
        }

        public void AimAt(Vector3 targetPosition)
        {
            if (spotlight == null || headPivot == null) return;

            Vector3 targetDirection = targetPosition - spotlight.transform.position;
            if (targetDirection.sqrMagnitude <= 0.001f) return;

            currentTilt = Mathf.Asin(Mathf.Clamp(targetDirection.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            headPivot.rotation = Quaternion.LookRotation(targetDirection.normalized, Vector3.up) * Quaternion.Inverse(neutralBeamRotation);
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
            UpdateHeldBeamTransform();
        }

        private void UpdateHeldBeamTransform()
        {
            if (!isHeld || heldBeamReference == null || headPivot == null) return;
            Vector3 heading = Vector3.ProjectOnPlane(heldBeamReference.forward, Vector3.up);
            if (heading.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
            headPivot.rotation = heldBeamReference.rotation * Quaternion.Euler(-currentTilt, 0f, 0f) * Quaternion.Inverse(neutralBeamRotation);
            // Move the physical panel, not an invisible detached light source.
            // Preserve the held stand base as the telescopic head rises. Without this
            // offset the hand-follow code cancels every height adjustment.
            Vector3 panelPosition = heldBeamReference.position + heldBeamReference.rotation * heldPanelPosition
                + Vector3.up * (heightExtension * heldPresentationScale);
            transform.position += panelPosition - headPivot.position;
        }

        private void LateUpdate()
        {
            UpdateHeldBeamTransform();
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

        private void ConfigureLightHead()
        {
            if (spotlight == null || headPivot != null) return;
            Transform housing = transform.Find("Light/Cube.001");
            Renderer housingRenderer = housing != null ? housing.GetComponent<Renderer>() : null;
            Vector3 beamForward = spotlight.transform.forward;
            Vector3 centre = housingRenderer != null ? housingRenderer.bounds.center : spotlight.transform.position;
            headPivot = new GameObject("Panel Tilt Pivot").transform;
            headPivot.SetParent(transform, false);
            headPivot.position = centre;
            neutralBeamRotation = Quaternion.Inverse(headPivot.rotation) * spotlight.transform.rotation;
            diffuserRenderer = spotlight.GetComponentInParent<MeshRenderer>();
            if (housing != null) housing.SetParent(headPivot, true);
            else spotlight.transform.SetParent(headPivot, true);

            // Imported meshes have scaled/rotated pivots. Measure their real front face.
            float front = 0f;
            if (housingRenderer != null)
            {
                Bounds bounds = housingRenderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    front = Mathf.Max(front, Vector3.Dot(housing.TransformPoint(corner) - centre, beamForward));
                }
            }
            if (diffuserRenderer != null)
            {
                float diffuserDepth = Vector3.Dot(diffuserRenderer.bounds.center - centre, beamForward);
                diffuserRenderer.transform.position += beamForward * (front + 0.001f - diffuserDepth);
                if (diffuserRenderer.sharedMaterial != null)
                {
                    diffuserMaterial = new Material(diffuserRenderer.sharedMaterial);
                    diffuserMaterial.EnableKeyword("_EMISSION");
                    diffuserMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    diffuserRenderer.sharedMaterial = diffuserMaterial;
                }
                diffuserProperties = new MaterialPropertyBlock();
            }
            // The diffuser has thickness too. Its centre is not its outer face.
            if (diffuserRenderer != null)
            {
                Bounds diffuserBounds = diffuserRenderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = diffuserBounds.center + Vector3.Scale(diffuserBounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    front = Mathf.Max(front, Vector3.Dot(diffuserRenderer.transform.TransformPoint(corner) - centre, beamForward));
                }
            }
            // Keep the emitter out of the imported housing's non-uniform scale.
            // The head still owns both the visible panel and its beam when tilted.
            spotlight.transform.SetParent(headPivot, true);
            spotlight.transform.localScale = Vector3.one;
            spotlight.transform.position = centre + beamForward * (front + 0.06f);
            spotlight.type = LightType.Spot;
            spotlight.shadowNearPlane = 0.05f;
            spotlight.shadowNormalBias = 0.1f;
            originalHeadPosition = headPivot.localPosition;
            originalStand = transform.Find("Light/Stick");
            if (originalStand != null)
            {
                standScale = originalStand.localScale;
                standPosition = originalStand.localPosition;
                Bounds bounds = GetStandBounds();
                standHeight = bounds.size.y;
                standBottom = bounds.min.y;
            }
            var hazeObject = new GameObject("Studio Haze Beam");
            hazeObject.layer = spotlight.gameObject.layer;
            hazeObject.transform.SetParent(transform, false);
            haze = hazeObject.AddComponent<StudioLightHaze>();
            haze.Initialize(spotlight);
            // Apply the starting extension only after the authored head and stand
            // have been measured. Pickups retain the player's current adjustment.
            AdjustStandHeight(0f);
        }

        private Bounds GetStandBounds()
        {
            Bounds result = new Bounds(); bool first = true;
            foreach (Renderer part in originalStand.GetComponentsInChildren<Renderer>())
            {
                Bounds local = part.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = transform.InverseTransformPoint(part.transform.TransformPoint(corner));
                    if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                    else result.Encapsulate(point);
                }
            }
            return result;
        }

        private void OnDestroy()
        {
            if (diffuserMaterial == null) return;
            if (Application.isPlaying) Destroy(diffuserMaterial);
            else DestroyImmediate(diffuserMaterial);
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
            // Production lights must illuminate wall interiors, not just mesh vertices
            // when the quality preset's automatic pixel-light budget is exhausted.
            spotlight.renderMode = LightRenderMode.ForcePixel;

            if (!HasAdvancedFeatures())
            {
                spotlight.range = Mathf.Max(30f, standardRange);
                // A panel needs a broad feathered beam, not a flat circular pool.
                // Beam falloff is separate from the equipment's shadow hardness.
                spotlight.spotAngle = 72f;
                spotlight.innerSpotAngle = 12f;
                spotlight.shadows = LightShadows.Hard;
                spotlight.shadowStrength = 0.72f;
                return;
            }

            float diffusionAmount = diffusionPercent / 100f;
            spotlight.range = Mathf.Max(40f, advancedRange);
            spotlight.spotAngle = Mathf.Lerp(68f, 90f, diffusionAmount);
            spotlight.innerSpotAngle = Mathf.Lerp(18f, 8f, diffusionAmount);
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
            CreateFeatureText("Controls", "[Z / K] TEMPERATURE     [V / B] DIFFUSION", new Vector2(0f, -58f), 17f, Color.white, TextAlignmentOptions.Center);
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
            RefreshPlacementControls();
            if (diffuserRenderer != null && diffuserProperties != null)
            {
                diffuserRenderer.GetPropertyBlock(diffuserProperties);
                Color lampColor = Mathf.CorrelatedColorTemperatureToRGB(GetColorTemperature());
                diffuserProperties.SetColor("_EmissionColor", isLightOn ? lampColor * (0.25f + intensityPercent / 100f) : Color.black);
                diffuserRenderer.SetPropertyBlock(diffuserProperties);
            }

            // 1. Intensity Text Update
            if (intensityText != null)
            {
                intensityText.text = $"{Mathf.RoundToInt(intensityPercent)}%";
                intensityText.color = isLightOn ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
            }

            // 2. Tilt Text Update
            if (tiltText != null)
            {
                tiltText.text = currentTilt.ToString("+0;-0;0") + "°";
                tiltText.enableWordWrapping = false;
                tiltText.color = isLightOn ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
            }

            // 3. Tilt Slider Update
            if (tiltSlider != null)
            {
                // The lowest numerical value (-45) must be the minValue
                tiltSlider.minValue = maxTiltUp;

                // The highest numerical value (45) must be the maxValue
                tiltSlider.maxValue = maxTiltDown;

                tiltSlider.SetValueWithoutNotify(currentTilt);
            }

            // 4. Intensity Slider Update
            if (intensitySlider != null)
            {
                intensitySlider.minValue = 0f;
                intensitySlider.maxValue = 100f;
                intensitySlider.SetValueWithoutNotify(intensityPercent);
            }

            // 5. Swap the Icons!
            if (lightOnIcon != null) lightOnIcon.SetActive(isLightOn);
            if (lightOffIcon != null) lightOffIcon.SetActive(!isLightOn);

            if (advancedFeaturePanel != null) advancedFeaturePanel.SetActive(HasAdvancedFeatures());

            if (temperatureText != null)
            {
                string temperatureName = colorTemperature <= 3600f ? "WARM" : colorTemperature >= 5200f ? "DAYLIGHT" : "NEUTRAL";
                temperatureText.text = "COLOR TEMP     " + Mathf.RoundToInt(colorTemperature) + "K  " + temperatureName;
                temperatureText.color = isLightOn ? Color.white : Color.gray;
            }

            if (diffusionText != null)
            {
                string diffusionName = diffusionPercent >= 75f ? "SOFT" : diffusionPercent >= 40f ? "MEDIUM" : "HARD";
                diffusionText.text = "DIFFUSION       " + Mathf.RoundToInt(diffusionPercent) + "%  " + diffusionName;
                diffusionText.color = isLightOn ? Color.white : Color.gray;
            }
        }
    }
}
