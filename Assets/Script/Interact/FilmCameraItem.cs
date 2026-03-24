using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Player.Manager;
using UnityEngine.Rendering.PostProcessing;

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

        [Header("--- LENS BLUR (DEPTH OF FIELD) ---")]
        public PostProcessVolume postProcessVolume;
        private DepthOfField depthOfField;

        [Header("--- CAMERA CONTROLS ---")]
        public float minFOV = 15f;
        public float maxFOV = 60f;
        public float zoomSpeed = 5f;

        [Header("Pedestal (Vertical Move - Q/E)")]
        public float pedestalSpeed = 0.5f;
        public float maxPedestalUp = 0.5f;
        public float maxPedestalDown = -0.5f;

        [Header("AF-C (Smooth Auto-Focus)")]
        public float focusBoxRadius = 0.3f;
        public float focusSmoothTime = 0.3f;

        [Header("Camera Sway")]
        public float swayIntensity = 0.5f;
        public float swaySpeed = 0.5f;

        // Internal tracking variables
        private bool isCameraActive = false;
        private bool isRecording = false;
        private bool isSDCardInserted = false;

        private float currentFocusDistance = 5f;
        private float targetFocusDistance = 5f;
        private float focusVelocity = 0f;

        private TruePixelRecorder pixelRecorder;
        private float noiseOffset;
        private Vector3 originalLensRotation;
        private Vector3 originalLocalPos;

        // Split score variables
        private float recordingStartTime = 0f;
        private float totalCameraScoreAccumulated = 0f;
        private float totalLightingScoreAccumulated = 0f;
        private int framesSampled = 0;
        private float nextSampleTime = 0f;

        // --- NEW: A bucket to hold your standard crosshair/hotbar UI ---
        private GameObject mainPlayerUI;

        private void Start()
        {
            // Find the master UI object in the scene
            GameObject gameUIObj = GameObject.Find("GameUI");

            if (gameUIObj != null)
            {
                // --- THE FIX: Find your standard Player UI so we can hide it later ---
                Transform standardUI = gameUIObj.transform.Find("Player UI");
                if (standardUI != null) mainPlayerUI = standardUI.gameObject;

                // Find the Camera POV UI
                Transform hiddenCanvas = gameUIObj.transform.Find("Cam Pov");
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
                originalLocalPos = filmCamera.transform.localPosition;
            }
            if (filmUICanvas != null) filmUICanvas.SetActive(false);

            if (recTimerText != null) recTimerText.text = "REC: 0.0s";
            if (focusText != null) focusText.text = "FOCUS: 5.0m";
            if (targetStatusText != null) targetStatusText.text = "NO SUBJECT";
        }

        public override void OnUse(Camera playerCamera)
        {
            isCameraActive = !isCameraActive;

            if (filmCamera != null)
            {
                filmCamera.gameObject.SetActive(isCameraActive);
                if (playerCamera != null) filmCamera.depth = playerCamera.depth + 1;
            }
            if (filmUICanvas != null) filmUICanvas.SetActive(isCameraActive);

            // --- THE FIX: Hide the player UI if the camera is up, show it if the camera is down! ---
            if (mainPlayerUI != null) mainPlayerUI.SetActive(!isCameraActive);

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
                float pedestalShift = 0f;
                if (Keyboard.current.qKey.isPressed) pedestalShift -= pedestalSpeed * Time.deltaTime;
                if (Keyboard.current.eKey.isPressed) pedestalShift += pedestalSpeed * Time.deltaTime;

                if (pedestalShift != 0)
                {
                    Vector3 newPos = filmCamera.transform.localPosition;
                    newPos.y = Mathf.Clamp(newPos.y + pedestalShift, originalLocalPos.y + maxPedestalDown, originalLocalPos.y + maxPedestalUp);
                    filmCamera.transform.localPosition = newPos;
                }
            }

            HandleSmoothAutoFocus();
            ApplyCameraSway();
            UpdateCameraHUD();

            if (isRecording && Time.time >= nextSampleTime)
            {
                SampleVideoFrame();
                nextSampleTime = Time.time + 0.5f;
            }
        }

        private void HandleSmoothAutoFocus()
        {
            if (filmCamera == null) return;

            if (Physics.Raycast(filmCamera.transform.position, filmCamera.transform.forward, out RaycastHit hit, 100f))
            {
                targetFocusDistance = hit.distance;
            }
            else if (Physics.SphereCast(filmCamera.transform.position, focusBoxRadius, filmCamera.transform.forward, out RaycastHit sphereHit, 100f))
            {
                targetFocusDistance = sphereHit.distance;
            }
            else
            {
                targetFocusDistance = 50f;
            }

            targetFocusDistance = Mathf.Max(targetFocusDistance, 0.1f);
            currentFocusDistance = Mathf.SmoothDamp(currentFocusDistance, targetFocusDistance, ref focusVelocity, focusSmoothTime);

            if (depthOfField != null)
            {
                depthOfField.focusDistance.value = currentFocusDistance;
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

            Vector3 targetCenter = target.transform.position;
            Collider targetCol = target.GetComponentInChildren<Collider>();
            if (targetCol != null) targetCenter = targetCol.bounds.center;

            Vector3 viewPos = filmCamera.WorldToViewportPoint(targetCenter);

            if (viewPos.z <= 0 || viewPos.x < 0 || viewPos.x > 1 || viewPos.y < 0 || viewPos.y > 1)
            { framesSampled++; return; }

            if (Physics.Raycast(filmCamera.transform.position, targetCenter - filmCamera.transform.position, out RaycastHit hit))
            {
                if (hit.collider.gameObject != target.gameObject && hit.collider.GetComponentInParent<RecordableSubject>() == null)
                {
                    totalCameraScoreAccumulated += 30f; // Pity points if partially blocked
                    framesSampled++; return;
                }
            }

            // --- 1. FRAMING (40 Points) ---
            float distFromCenter = Vector2.Distance(new Vector2(0.5f, 0.5f), new Vector2(viewPos.x, viewPos.y));
            float framingScore = 40f;
            if (distFromCenter > 0.4f) framingScore -= (distFromCenter - 0.4f) * 40f;
            framingScore = Mathf.Clamp(framingScore, 0f, 40f);

            // --- 2. FOCUS (30 Points) ---
            float focusScore = 30f;

            // --- 3. THE LIVE LIGHTING CHECK (30 Points) ---
            float lightingScore = 0f;
            FilmLightItem[] activeLights = FindObjectsOfType<FilmLightItem>();

            foreach (FilmLightItem light in activeLights)
            {
                if (light.spotlight.enabled)
                {
                    Vector3 cameraArrow = (filmCamera.transform.position - targetCenter).normalized;
                    Vector3 lightArrow = (light.transform.position - targetCenter).normalized;
                    float angle = Vector3.Dot(cameraArrow, lightArrow);

                    if (angle < -0.2f) lightingScore = 30f; // Pass! Backlight.
                    else if (angle > 0.2f) lightingScore = 0f;  // Fail! Front-light glare.
                }
            }

            // ADD TO THE SPLIT BUCKETS
            totalCameraScoreAccumulated += (framingScore + focusScore);
            totalLightingScoreAccumulated += lightingScore;
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
            float finalCamGrade = 0f;
            float finalLightGrade = 0f;

            if (pixelRecorder == null) pixelRecorder = FindObjectOfType<TruePixelRecorder>();

            if (pixelRecorder != null)
            {
                if (isRecording)
                {
                    pixelRecorder.StartRecording();
                    recordingStartTime = Time.time;
                    totalCameraScoreAccumulated = 0f; // RESET
                    totalLightingScoreAccumulated = 0f; // RESET
                    framesSampled = 0;
                    nextSampleTime = Time.time + 0.5f;
                }
                else
                {
                    generatedFileName = pixelRecorder.StopRecording();
                    finalDuration = Time.time - recordingStartTime;

                    // CALCULATE THE SPLIT AVERAGES
                    if (framesSampled > 0)
                    {
                        finalCamGrade = totalCameraScoreAccumulated / framesSampled;
                        finalLightGrade = totalLightingScoreAccumulated / framesSampled;
                        finalGrade = finalCamGrade + finalLightGrade;
                    }
                }
            }
            if (!isRecording) EjectUsedSDCard(generatedFileName, finalDuration, finalGrade, finalCamGrade, finalLightGrade);
            if (TutorialManager.Instance != null && !isRecording) TutorialManager.Instance.OnRecordingFinished();
        }

        private void EjectUsedSDCard(string savedFileName, float duration, float finalScore, float camScore, float lightScore)
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

                    // SAVE THE SPLIT SCORES TO THE CARD!
                    cardScript.cameraScore = camScore;
                    cardScript.lightScore = lightScore;
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

                // --- THE FIX: Make sure the crosshair/hotbar comes back if you drop the camera while looking through it! ---
                if (mainPlayerUI != null) mainPlayerUI.SetActive(true);
            }
            base.OnDropped(playerCamera);
        }
    }
}