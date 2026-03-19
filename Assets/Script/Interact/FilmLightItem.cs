using UnityEngine;
using UnityEngine.InputSystem;
using Player.Manager;

namespace Player.Equipment
{
    public class FilmLightItem : Equipment
    {
        [Header("Light Beam Settings")]
        [Tooltip("Drag the invisible Spot Light object here")]
        public Light spotlight;

        [Tooltip("How many degrees it snaps per click")]
        public float tiltStep = 5f; // THE FIX: Exactly 5 degrees per tap!

        public float maxTiltUp = -45f;
        public float maxTiltDown = 45f;

        private bool isLightOn = false;
        private float currentTilt = 0f;
        private float startAngleX = 0f;

        protected override void Awake()
        {
            base.Awake();

            if (spotlight != null)
            {
                spotlight.enabled = isLightOn;
                startAngleX = spotlight.transform.localEulerAngles.x;
            }
        }

        // Toggle the light on and off
        public override void OnUse(Camera playerCamera)
        {
            isLightOn = !isLightOn;
            if (spotlight != null) spotlight.enabled = isLightOn;
            Debug.Log($"Stage Light is now {(isLightOn ? "ON" : "OFF")}");
        }

        public override void OnHeldUpdate(InputManager input)
        {
            if (Keyboard.current == null) return;

            // THE FIX: "wasPressedThisFrame" ensures it only moves exactly once per click!
            if (Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                TiltLight(-tiltStep);
            }
            else if (Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                TiltLight(tiltStep);
            }
        }

        private void TiltLight(float amount)
        {
            if (spotlight == null) return;

            currentTilt += amount;
            currentTilt = Mathf.Clamp(currentTilt, maxTiltUp, maxTiltDown);

            spotlight.transform.localEulerAngles = new Vector3(startAngleX + currentTilt, 0, 0);
        }
    }
}