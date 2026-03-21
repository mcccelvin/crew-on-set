using UnityEngine;
using UnityEngine.InputSystem;
using Player.Manager;

namespace Player.Equipment
{
    public class FilmCameraItem : Equipment
    {
        [Header("Film Camera Settings")]
        [SerializeField] private Camera filmCamera;
        [SerializeField] private GameObject filmUICanvas;

        [Header("SD Card System")]
        public GameObject sdCardPrefab;
        public Transform ejectPoint;

        [Header("--- LOW-END FX3 FEATURES ---")]
        [Header("Zoom (Focal Length)")]
        public float minFOV = 15f;
        public float maxFOV = 60f;
        public float zoomSpeed = 5f;

        [Header("Manual Focus")]
        public float currentFocusDistance = 5f;
        public float focusSpeed = 10f;

        [Header("Camera Sway (Drift)")]
        public float swayIntensity = 0.5f; // LOWERED so it doesn't make walking feel choppy
        public float swaySpeed = 0.5f;

        private bool isCameraActive = false;
        private bool isRecording = false;
        private bool isSDCardInserted = false;

        private TruePixelRecorder pixelRecorder;
        private float noiseOffset;
        private Vector3 originalLensRotation; // THE FIX: A memory for the camera's original angle!

        protected override void Awake()
        {
            base.Awake();
            pixelRecorder = FindObjectOfType<TruePixelRecorder>();

            // Randomize the sway so it feels organic
            noiseOffset = Random.Range(0f, 1000f);

            if (filmCamera != null)
            {
                filmCamera.gameObject.SetActive(false);
                // Memorize exactly how the lens is sitting before we start wobbling it
                originalLensRotation = filmCamera.transform.localEulerAngles;
            }
            if (filmUICanvas != null) filmUICanvas.SetActive(false);
        }

        public override void OnUse(Camera playerCamera)
        {
            isCameraActive = !isCameraActive;

            if (filmCamera != null) filmCamera.gameObject.SetActive(isCameraActive);
            if (filmUICanvas != null) filmUICanvas.SetActive(isCameraActive);
            if (playerCamera != null) playerCamera.gameObject.SetActive(!isCameraActive);

            if (!isCameraActive && isRecording) ToggleRecording();
        }

        public override void OnHeldUpdate(InputManager input)
        {
            if (!isCameraActive) return;

            // 1. SD CARD CONTROLS (Restored!)
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) InsertSDCard();
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                if (!isRecording && !isSDCardInserted) { Debug.LogWarning("Insert SD Card first!"); return; }
                ToggleRecording();
            }

            // 2. ZOOM CONTROLS
            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.y.ReadValue();
                if (scroll > 0) filmCamera.fieldOfView -= zoomSpeed;
                else if (scroll < 0) filmCamera.fieldOfView += zoomSpeed;

                filmCamera.fieldOfView = Mathf.Clamp(filmCamera.fieldOfView, minFOV, maxFOV);
            }

            // 3. FOCUS CONTROLS
            if (Keyboard.current != null)
            {
                if (Keyboard.current.qKey.isPressed) currentFocusDistance -= focusSpeed * Time.deltaTime;
                else if (Keyboard.current.eKey.isPressed) currentFocusDistance += focusSpeed * Time.deltaTime;

                currentFocusDistance = Mathf.Clamp(currentFocusDistance, 0.1f, 50f);
            }

            // 4. THE DRIFT
            ApplyCameraSway();
            if (TutorialManager.Instance != null) TutorialManager.Instance.OnCameraGrabbed();

        }

        private void ApplyCameraSway()
        {
            if (filmCamera == null) return;

            float swayX = (Mathf.PerlinNoise(Time.time * swaySpeed + noiseOffset, 0f) * 2f - 1f) * swayIntensity;
            float swayY = (Mathf.PerlinNoise(0f, Time.time * swaySpeed + noiseOffset) * 2f - 1f) * swayIntensity;

            // THE FIX: We add the wobble on top of the original rotation, so it doesn't fight your walking!
            filmCamera.transform.localEulerAngles = originalLensRotation + new Vector3(swayX, swayY, 0f);
        }

        // --- THE FULLY RESTORED SD CARD LOGIC ---

        private void InsertSDCard()
        {
            if (isSDCardInserted)
            {
                Debug.Log("Camera: An SD Card is already inserted!");
                return;
            }

            Player.Interactor.EquipmentInteractor hotbar = GetComponentInParent<Player.Interactor.EquipmentInteractor>();

            if (hotbar != null && hotbar.HasBlankSDCard())
            {
                hotbar.ConsumeBlankSDCard();
                isSDCardInserted = true;
                Debug.Log("Camera: Blank SD Card taken from hotbar and inserted.");
            }
            else
            {
                Debug.LogWarning("Camera: You have no blank SD cards in your hotbar!");
            }
            if (TutorialManager.Instance != null) TutorialManager.Instance.OnRecordingFinished();

        }

        private void ToggleRecording()
        {
            isRecording = !isRecording;
            string generatedFileName = "";

            if (pixelRecorder == null) pixelRecorder = FindObjectOfType<TruePixelRecorder>();

            if (pixelRecorder != null)
            {
                if (isRecording) pixelRecorder.StartRecording();
                else generatedFileName = pixelRecorder.StopRecording();
            }

            if (!isRecording) EjectUsedSDCard(generatedFileName);

            Debug.Log($"Recording is now: {(isRecording ? "ON" : "OFF")}");
        }

        private void EjectUsedSDCard(string savedFileName)
        {
            isSDCardInserted = false;

            if (sdCardPrefab != null)
            {
                Transform spawnLoc = ejectPoint != null ? ejectPoint : transform;
                GameObject ejectedCard = Instantiate(sdCardPrefab, spawnLoc.position, spawnLoc.rotation);

                SDCardItem cardScript = ejectedCard.GetComponent<SDCardItem>();
                if (cardScript != null)
                {
                    cardScript.isUsedCard = true;
                    cardScript.recordedFileName = savedFileName;
                }

                MeshRenderer renderer = ejectedCard.GetComponentInChildren<MeshRenderer>();
                if (renderer != null) renderer.material.color = Color.red;

                Collider col = ejectedCard.GetComponent<Collider>();
                if (col == null) col = ejectedCard.AddComponent<BoxCollider>();

                Rigidbody rb = ejectedCard.GetComponent<Rigidbody>();
                if (rb == null) rb = ejectedCard.AddComponent<Rigidbody>();

                rb.isKinematic = false;
                rb.useGravity = true;
                rb.AddForce(transform.up * 2f + transform.forward * 1.5f, ForceMode.Impulse);
            }



            Debug.Log($"Camera: Take complete! Ejected card holding file: {savedFileName}");
        }

        public override void OnDropped(Camera playerCamera)
        {
            if (isRecording) ToggleRecording();
            if (isCameraActive)
            {
                isCameraActive = false;
                if (filmCamera != null) filmCamera.gameObject.SetActive(false);
                if (filmUICanvas != null) filmUICanvas.SetActive(false);
                if (playerCamera != null) playerCamera.gameObject.SetActive(true);
            }
            base.OnDropped(playerCamera);
        }
    }
}