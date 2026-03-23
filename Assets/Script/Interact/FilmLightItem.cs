using UnityEngine;
using UnityEngine.InputSystem;
using Player.Manager;

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

        private bool isLightOn = false;
        private float currentTilt = 0f;

        // --- THE FIX: Store the ENTIRE starting rotation once! ---
        private Vector3 startAngles;

        protected override void Awake()
        {
            base.Awake();

            if (spotlight != null)
            {
                spotlight.enabled = isLightOn;

                // Lock the initial X, Y, and Z angles in a vault so Unity can't flip them!
                startAngles = spotlight.transform.localEulerAngles;

                // --- ENFORCE RESTRICTIONS ---
                if (forcesHardLight) spotlight.shadows = LightShadows.Hard;

                spotlight.useColorTemperature = true;
                if (isFixedKelvin) spotlight.colorTemperature = fixedColorTemperature;

                UpdateLightOutput();
            }
        }

        public override void OnUse(Camera playerCamera)
        {
            isLightOn = !isLightOn;
            if (spotlight != null) spotlight.enabled = isLightOn;

            if (isLightOn)
            {
                Debug.Log($"[160 LED PANEL] ON | Temp: {fixedColorTemperature}K | WARNING: HARD LIGHT RESTRICTION ACTIVE");
                if (TutorialManager.Instance != null) TutorialManager.Instance.OnLightTurnedOn();
            }
            else
            {
                Debug.Log("[160 LED PANEL] OFF");
            }
        }

        public override void OnHeldUpdate(InputManager input)
        {
            if (Keyboard.current == null || Mouse.current == null) return;

            // --- 1. STAND TILT ---
            if (Keyboard.current.upArrowKey.wasPressedThisFrame) TiltLight(-tiltStep);
            else if (Keyboard.current.downArrowKey.wasPressedThisFrame) TiltLight(tiltStep);

            // --- 2. INTENSITY SLIDER (0-100%) ---
            float scroll = Mouse.current.scroll.y.ReadValue();
            if (scroll > 0) AdjustIntensity(5f);
            else if (scroll < 0) AdjustIntensity(-5f);
        }

        private void TiltLight(float amount)
        {
            if (spotlight == null) return;
            currentTilt = Mathf.Clamp(currentTilt + amount, maxTiltUp, maxTiltDown);
            UpdateLightTransform();
        }

        private void UpdateLightTransform()
        {
            // THE FIX: Use the locked startAngles.y and startAngles.z instead of asking Unity for them dynamically!
            spotlight.transform.localEulerAngles = new Vector3(startAngles.x + currentTilt, startAngles.y, startAngles.z);
        }

        private void AdjustIntensity(float amount)
        {
            intensityPercent = Mathf.Clamp(intensityPercent + amount, 0f, 100f);
            UpdateLightOutput();

            float currentLux = maxLux * (intensityPercent / 100f);
            Debug.Log($"Meter: {intensityPercent}% | Output: {currentLux} Lux / {currentLux * 0.0929f:F1} Footcandles");
        }

        private void UpdateLightOutput()
        {
            if (spotlight != null)
            {
                spotlight.intensity = maxLux * (intensityPercent / 100f);
            }
        }
    }
}