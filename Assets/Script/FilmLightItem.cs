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

        [Tooltip("The maximum Lux output at 100%")]
        public float maxLux = 20f;

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

        private bool isLightOn = false;
        private float currentTilt = 0f;
        private Vector3 startAngles;

        // Safety lock for Shop Prefabs!
        private bool isHeld = false;

        protected override void Awake()
        {
            base.Awake();

            if (spotlight != null)
            {
                startAngles = spotlight.transform.localEulerAngles;
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
            isHeld = true;
            if (lightUICanvas != null) lightUICanvas.SetActive(true);
            UpdateLightUI();
        }

        // Triggered when you press G to drop it
        public override void OnDropped(Camera playerCamera)
        {
            base.OnDropped(playerCamera);
            isHeld = false;
            if (lightUICanvas != null) lightUICanvas.SetActive(false);
        }

        public override void OnUse(Camera playerCamera)
        {
            isLightOn = !isLightOn;
            if (spotlight != null) spotlight.enabled = isLightOn;

            if (isLightOn && TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnLightTurnedOn();
            }

            UpdateLightUI();
        }

        public override void OnHeldUpdate(InputManager input)
        {
            if (!isLightOn) return;

            // Scroll wheel for intensity
            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.y.ReadValue();
                if (scroll > 0) AdjustIntensity(5f);
                else if (scroll < 0) AdjustIntensity(-5f);
            }

            // Up/Down arrows for tilt
            if (Keyboard.current != null)
            {
                if (Keyboard.current.upArrowKey.wasPressedThisFrame) TiltLight(-tiltStep);
                if (Keyboard.current.downArrowKey.wasPressedThisFrame) TiltLight(tiltStep);
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
            spotlight.transform.localEulerAngles = new Vector3(startAngles.x + currentTilt, startAngles.y, startAngles.z);
        }

        private void AdjustIntensity(float amount)
        {
            intensityPercent = Mathf.Clamp(intensityPercent + amount, 0f, 100f);
            UpdateLightOutput();

            UpdateLightUI(); // Update the UI text & slider!

            // --- THE FIX: Send the intensity percentage! ---
            if (TutorialManager.Instance != null) TutorialManager.Instance.OnLightIntensityChanged(intensityPercent);
        }

        private void UpdateLightOutput()
        {
            if (spotlight != null)
            {
                spotlight.intensity = maxLux * (intensityPercent / 100f);
            }
        }

        // Syncs the numbers and images to your visual HUD
        private void UpdateLightUI()
        {
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
        }
    }
}
