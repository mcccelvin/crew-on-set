using Player.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.PostProcessing; // --- NEW: Allows us to talk to the blur effect! ---

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

        [Header("--- HUD UI REFERENCES ---")]
        public TMP_Text recTimerText;
        public TMP_Text focusText;
        public TMP_Text targetStatusText;

        [Header("--- LENS BLUR (DEPTH OF FIELD) ---")] // --- NEW ---
        public PostProcessVolume postProcessVolume;
        private DepthOfField depthOfField;

        [Header("--- LOW-END FX3 FEATURES ---")]
        [Header("Zoom (Focal Length)")]
        public float minFOV = 15f;
        public float maxFOV = 60f;
        public float zoomSpeed = 5f;

        [Header("Manual Focus")]
        public float currentFocusDistance = 5f;
        public float focusSpeed = 10f;

        [Header("Camera Sway (Drift)")]
        public float swayIntensity = 0.5f;
        public float swaySpeed = 0.5f;

        private bool isCameraActive = false;
        private bool isRecording = false;
        private bool isSDCardInserted = false;

        private TruePixelRecorder pixelRecorder;
        private float noiseOffset;
        private Vector3 originalLensRotation;

        private float recordingStartTime = 0f;

        private float totalScoreAccumulated = 0f;
        private int framesSampled = 0;
        private float nextSampleTime = 0f;

        private void Start()
        {
            if (filmUICanvas == null)
            {
                GameObject playerUI = GameObject.Find("GameUI");
                if (playerUI != null)
                {
                    Transform hiddenCanvas = playerUI.transform.Find("Cam Pov");
                    if (hiddenCanvas != null)
                    {
                        filmUICanvas = hiddenCanvas.gameObject;
                        Transform timerObj = hiddenCanvas.Find("TimerText");
                        if (timerObj != null) recTimerText = timerObj.GetComponent<TMP_Text>();
                        Transform focusObj = hiddenCanvas.Find("FocusText");
                        if (focusObj != null) focusText = focusObj.GetComponent<TMP_Text>();
                        Transform targetObj = hiddenCanvas.Find("TargetStatusText");
                        if (targetObj != null) targetStatusText = targetObj.GetComponent<TMP_Text>();
                    }
                }
            }

            // --- NEW: Grab the Depth of Field effect from the camera! ---
            if (postProcessVolume != null && postProcessVolume.profile != null)
            {
                postProcessVolume.profile.TryGetSettings(out depthOfField);
            }
        }

        protected override void Awake()
        {
            base.Awake();
            pixelRecorder = FindObjectOfType<TruePixelRecorder>();
            noiseOffset = Random.Range(0f, 1000f);

            if (filmCamera != null)
            {
                filmCamera.gameObject.SetActive(false);
                originalLensRotation = filmCamera.transform.localEulerAngles;
            }
            if (filmUICanvas != null) filmUICanvas.SetActive(false);

            if (recTimerText != null) recTimerText.text = "REC: 0.0s";
            if (focusText != null) focusText.text = "FOCUS: 5.0m";
            if (targetStatusText != null) targetStatusText.text = "NO SUBJECT";
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

            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) InsertSDCard();
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                if (!isRecording && !isSDCardInserted) { Debug.LogWarning("Insert SD Card first!"); return; }
                ToggleRecording();
            }

            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.y.ReadValue();
                if (scroll > 0) filmCamera.fieldOfView -= zoomSpeed;
                else if (scroll < 0) filmCamera.fieldOfView += zoomSpeed;
                filmCamera.fieldOfView = Mathf.Clamp(filmCamera.fieldOfView, minFOV, maxFOV);
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.qKey.isPressed) currentFocusDistance -= focusSpeed * Time.deltaTime;
                else if (Keyboard.current.eKey.isPressed) currentFocusDistance += focusSpeed * Time.deltaTime;

                currentFocusDistance = Mathf.Clamp(currentFocusDistance, 0.1f, 50f);
            }

            // --- NEW: Actually blur the camera based on your manual focus dial! ---
            if (depthOfField != null)
            {
                depthOfField.focusDistance.value = currentFocusDistance;
            }

            ApplyCameraSway();
            UpdateCameraHUD();

            if (isRecording && Time.time >= nextSampleTime)
            {
                SampleVideoFrame();
                nextSampleTime = Time.time + 0.5f;
            }
        }

        private void ApplyCameraSway()
        {
            if (filmCamera == null) return;
            float swayX = (Mathf.PerlinNoise(Time.time * swaySpeed + noiseOffset, 0f) * 2f - 1f) * swayIntensity;
            float swayY = (Mathf.PerlinNoise(0f, Time.time * swaySpeed + noiseOffset) * 2f - 1f) * swayIntensity;
            filmCamera.transform.localEulerAngles = originalLensRotation + new Vector3(swayX, swayY, 0f);
        }

        private void UpdateCameraHUD()
        {
            if (focusText != null) focusText.text = $"FOCUS: {currentFocusDistance:F1}m";
            if (recTimerText != null)
            {
                if (isRecording) recTimerText.text = $"<color=red>● REC: {(Time.time - recordingStartTime):F1}s</color>";
                else recTimerText.text = "STBY";
            }
            if (targetStatusText != null)
            {
                RecordableSubject target = FindObjectOfType<RecordableSubject>();
                bool isLookingAtTarget = false;

                if (target != null)
                {
                    Vector3 viewPos = filmCamera.WorldToViewportPoint(target.transform.position);
                    if (viewPos.z > 0 && viewPos.x >= 0 && viewPos.x <= 1 && viewPos.y >= 0 && viewPos.y <= 1)
                    {
                        Vector3 directionToTarget = target.transform.position - filmCamera.transform.position;
                        if (Physics.Raycast(filmCamera.transform.position, directionToTarget, out RaycastHit hit))
                        {
                            if (hit.collider.gameObject == target.gameObject || hit.collider.GetComponentInParent<RecordableSubject>() != null)
                                isLookingAtTarget = true;
                        }
                    }
                }
                if (isLookingAtTarget) targetStatusText.text = "<color=green>[ SUBJECT DETECTED ]</color>";
                else targetStatusText.text = "<color=white>[ NO SUBJECT ]</color>";
            }
        }

        private void SampleVideoFrame()
        {
            RecordableSubject target = FindObjectOfType<RecordableSubject>();
            if (target == null) { framesSampled++; return; }

            Vector3 viewPos = filmCamera.WorldToViewportPoint(target.transform.position);
            if (viewPos.z <= 0 || viewPos.x < 0 || viewPos.x > 1 || viewPos.y < 0 || viewPos.y > 1) { framesSampled++; return; }

            Vector3 directionToTarget = target.transform.position - filmCamera.transform.position;
            if (Physics.Raycast(filmCamera.transform.position, directionToTarget, out RaycastHit hit))
            {
                if (hit.collider.gameObject != target.gameObject && hit.collider.GetComponentInParent<RecordableSubject>() == null)
                { framesSampled++; return; }
            }

            float distFromCenter = Vector2.Distance(new Vector2(0.5f, 0.5f), new Vector2(viewPos.x, viewPos.y));
            float framingScore = Mathf.Clamp(50f - (distFromCenter * 100f), 0f, 50f);

            float actualDistance = Vector3.Distance(filmCamera.transform.position, target.transform.position);
            float focusError = Mathf.Abs(actualDistance - currentFocusDistance);
            float focusScore = Mathf.Clamp(50f - (focusError * 10f), 0f, 50f);

            totalScoreAccumulated += (framingScore + focusScore);
            framesSampled++;
        }

        private void InsertSDCard()
        {
            if (isSDCardInserted) return;
            Player.Interactor.EquipmentInteractor hotbar = GetComponentInParent<Player.Interactor.EquipmentInteractor>();
            if (hotbar != null && hotbar.HasBlankSDCard())
            {
                hotbar.ConsumeBlankSDCard();
                isSDCardInserted = true;
            }
            if (TutorialManager.Instance != null) TutorialManager.Instance.OnCardInsertedToCamera();
        }

        private void ToggleRecording()
        {
            isRecording = !isRecording;
            string generatedFileName = "";
            float finalDuration = 0f;
            float finalGrade = 0f;

            if (pixelRecorder == null) pixelRecorder = FindObjectOfType<TruePixelRecorder>();

            if (pixelRecorder != null)
            {
                if (isRecording)
                {
                    pixelRecorder.StartRecording();
                    recordingStartTime = Time.time;
                    totalScoreAccumulated = 0f;
                    framesSampled = 0;
                    nextSampleTime = Time.time + 0.5f;
                }
                else
                {
                    generatedFileName = pixelRecorder.StopRecording();
                    finalDuration = Time.time - recordingStartTime;
                    if (framesSampled > 0) finalGrade = totalScoreAccumulated / framesSampled;
                }
            }
            if (!isRecording) EjectUsedSDCard(generatedFileName, finalDuration, finalGrade);
            if (TutorialManager.Instance != null && !isRecording) TutorialManager.Instance.OnRecordingFinished();
        }

        private void EjectUsedSDCard(string savedFileName, float duration, float finalScore)
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
                    cardScript.videoDuration = duration;
                    cardScript.videoScore = finalScore;
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