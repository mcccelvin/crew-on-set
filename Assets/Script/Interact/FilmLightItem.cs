using UnityEngine;
using UnityEngine.InputSystem; // Needed to read the 'Q' key directly
using Player.Manager;

namespace Player.Equipment
{
    public class FilmLightItem : Equipment
    {
        [Header("Film Light Settings")]
        [SerializeField] private Light filmLight;
        [SerializeField] private Transform lightPivot;

        [Header("Tilt Settings")]
        [SerializeField] private float tiltStep = 15f; // How many degrees it moves per click
        [SerializeField] private float maxUpAngle = -45f;
        [SerializeField] private float maxDownAngle = 45f;

        private bool isLightOn = false;
        private float currentTilt = 0f;

        protected override void Awake()
        {
            base.Awake();
            if (filmLight != null) filmLight.enabled = false;
        }

        // F KEY: Toggles the light ON and OFF
        public override void OnUse(Camera playerCamera)
        {
            isLightOn = !isLightOn;
            if (filmLight != null) filmLight.enabled = isLightOn;
            Debug.Log($"Film Light is now: {(isLightOn ? "ON" : "OFF")}");
        }

        // Runs every frame while holding the light
        public override void OnHeldUpdate(InputManager input)
        {
            // Q KEY -> Tilt Up
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            {
                currentTilt -= tiltStep;
                currentTilt = Mathf.Clamp(currentTilt, maxUpAngle, maxDownAngle);
                ApplyTilt();
            }

            // E KEY (Interact) -> Tilt Down
            if (input.Interact)
            {
                currentTilt += tiltStep;
                currentTilt = Mathf.Clamp(currentTilt, maxUpAngle, maxDownAngle);
                ApplyTilt();
            }
        }

        private void ApplyTilt()
        {
            if (lightPivot != null)
            {
                lightPivot.localEulerAngles = new Vector3(currentTilt, 0, 0);
            }
        }

        public override void OnDropped(Camera playerCamera)
        {
            // I removed the code that turns the light off.
            // Now, it will stay on when dropped!
            base.OnDropped(playerCamera);
        }
    }
}