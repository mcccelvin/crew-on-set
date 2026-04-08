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
        // TARGET STATUS TEXT REMOVED!

        [Header("--- NEW UI FEATURES ---")]
        public TMP_Text recordStateText;
        public RectTransform trackingSquare;
        private RecordableSubject targetSubject;

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

        private float recordingStartTime = 0f;
        private float totalCameraScoreAccumulated = 0f;
        private float totalLightingScoreAccumulated = 0f;
        private int framesSampled = 0;
        private float nextSampleTime = 0f;

        private GameObject mainPlayerUI;

        private void Start()
        {
            Canvas[] allCanvases = FindObjectsOfType<Canvas>(true);

            foreach (Canvas canvas in allCanvases)
            {
                Transform[] allChildren = canvas.GetComponentsInChildren<Transform>(true);
                foreach (Transform child in allChildren)
                {
                    if (child.name == "Cam Pov") filmUICanvas = child.gameObject;
                    else if (child.name == "TimerText") recTimerText = child.GetComponent<TMP_Text>();
                    else if (child.name == "FocusText") focusText = child.GetComponent<TMP_Text>();

                    else if (child.name == "RecordStateText") recordStateText = child.GetComponent<TMP_Text>();
                    else if (child.name == "TrackingSquare") trackingSquare = child.GetComponent<RectTransform>();

                    else if (child.name == "Player UI" || child.name == "PlayerUI" || child.name == "Main UI")
                    {
                        mainPlayerUI = child.gameObject;
                    }
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

            if (recTimerText != null) recTimerText.text = "00:00:000";
            if (focusText != null) focusText.text = "FOCUS: 5.0m";

            if (trackingSquare != null) trackingSquare.gameObject.SetActive(false);
        }

        private void TogglePlayerUI(bool showUI)
        {
            if (mainPlayerUI != null)
            {
                mainPlayerUI.SetActive(showUI);
            }
            else
            {
                HotbarUIManager hotbar = FindObjectOfType<HotbarUIManager>();
                if (hotbar != null) hotbar.gameObject.SetActive(showUI);

                if (CareerManager.Instance != null && CareerManager.Instance.moneyTextHUD != null)
                {
                    CareerManager.Instance.moneyTextHUD.gameObject.SetActive(showUI);
                }
            }
        }

        public override void OnUse(Camera playerCamera)
        {
            if (!isCameraActive && !isSDCardInserted)
            {
                HotbarUIManager hotbar = FindObjectOfType<HotbarUIManager>();
                if (hotbar != null)
                {
                    hotbar.UpdateGuideText("<color=red>INSERT SD CARD FIRST (Press C)</color>");
                }
                Debug.LogWarning("Cannot look through camera. Insert an SD Card first!");
                return;
            }

            isCameraActive = !isCameraActive;

            if (isCameraActive && TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnCameraViewEntered();
            }

            if (filmCamera != null)
            {
                filmCamera.gameObject.SetActive(isCameraActive);
                if (playerCamera != null) filmCamera.depth = playerCamera.depth + 1;
            }
            if (filmUICanvas != null) filmUICanvas.SetActive(isCameraActive);

            TogglePlayerUI(!isCameraActive);

            if (!isCameraActive && isRecording) ToggleRecording();
        }

        public override void OnHeldUpdate(InputManager input)
        {
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) InsertSDCard();

            if (!isCameraActive) return;

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
            UpdateTrackingSquare();

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

        private Vector3 GetSubjectCenter(RecordableSubject sub)
        {
            if (sub == null) return Vector3.zero;
            Renderer[] rends = sub.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                foreach (Renderer r in rends) b.Encapsulate(r.bounds);
                return b.center;
            }
            return sub.transform.position + Vector3.up * 0.5f;
        }

        private void UpdateCameraHUD()
        {
            if (focusText != null) focusText.text = $"FOCUS: {currentFocusDistance:F1}m";

            float time = isRecording ? (Time.time - recordingStartTime) : 0f;
            int minutes = (int)(time / 60f);
            int seconds = (int)(time % 60f);
            int milliseconds = (int)((time - Mathf.Floor(time)) * 1000f);

            if (recTimerText != null)
            {
                recTimerText.text = string.Format("{0:00}:{1:00}:{2:000}", minutes, seconds, milliseconds);
                recTimerText.color = isRecording ? Color.red : Color.white;
            }

            if (recordStateText != null)
            {
                if (isRecording)
                {
                    recordStateText.text = "● REC";
                    recordStateText.color = Color.red;
                }
                else
                {
                    recordStateText.text = "STD";
                    recordStateText.color = Color.white;
                }
            }
            // Target Status Text logic has been completely removed!
        }

        // ==========================================
        // SMART TRACKING SQUARE (Size + Color Detection)
        // ==========================================
        private void UpdateTrackingSquare()
        {
            if (targetSubject == null)
            {
                targetSubject = FindObjectOfType<RecordableSubject>();
            }

            if (targetSubject != null && trackingSquare != null && filmCamera != null)
            {
                Renderer[] rends = targetSubject.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    Vector3 targetCenter = GetSubjectCenter(targetSubject);
                    Vector3 viewPos = filmCamera.WorldToViewportPoint(targetCenter);
                    Vector3 screenPos = filmCamera.WorldToScreenPoint(targetCenter);

                    // Check if the subject is actually on your screen
                    if (viewPos.z > 0 && viewPos.x >= 0 && viewPos.x <= 1 && viewPos.y >= 0 && viewPos.y <= 1)
                    {
                        trackingSquare.gameObject.SetActive(true);
                        trackingSquare.position = screenPos;

                        // Scale the box
                        Bounds bounds = rends[0].bounds;
                        foreach (Renderer r in rends) bounds.Encapsulate(r.bounds);

                        Vector3[] corners = new Vector3[8];
                        corners[0] = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
                        corners[1] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
                        corners[2] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
                        corners[3] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
                        corners[4] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
                        corners[5] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
                        corners[6] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
                        corners[7] = new Vector3(bounds.max.x, bounds.max.y, bounds.max.z);

                        float minX = float.MaxValue, minY = float.MaxValue;
                        float maxX = float.MinValue, maxY = float.MinValue;

                        foreach (Vector3 corner in corners)
                        {
                            Vector3 screenCorner = filmCamera.WorldToScreenPoint(corner);
                            minX = Mathf.Min(minX, screenCorner.x);
                            minY = Mathf.Min(minY, screenCorner.y);
                            maxX = Mathf.Max(maxX, screenCorner.x);
                            maxY = Mathf.Max(maxY, screenCorner.y);
                        }

                        float width = maxX - minX;
                        float height = maxY - minY;
                        float padding = 40f;

                        width = Mathf.Clamp(width + padding, 50f, 800f);
                        height = Mathf.Clamp(height + padding, 50f, 800f);

                        trackingSquare.sizeDelta = new Vector2(width, height);

                        // --- NEW: Change color based on if it is blocked! ---
                        Vector3 directionToTarget = targetCenter - filmCamera.transform.position;
                        float distToSub = Vector3.Distance(filmCamera.transform.position, targetCenter);

                        bool isBlocked = false;

                        RaycastHit[] hits = Physics.RaycastAll(filmCamera.transform.position, directionToTarget, distToSub);
                        foreach (RaycastHit hit in hits)
                        {
                            if (hit.collider.transform.root == this.transform.root) continue;
                            if (hit.collider.isTrigger) continue;
                            if (hit.collider.GetComponentInParent<RecordableSubject>() != null) continue;

                            if (hit.distance < distToSub - 0.3f)
                            {
                                isBlocked = true;
                                break;
                            }
                        }

                        UnityEngine.UI.Image squareImage = trackingSquare.GetComponent<UnityEngine.UI.Image>();
                        if (squareImage != null)
                        {
                            if (isBlocked)
                            {
                                squareImage.color = Color.red; // Red = Blocked!
                            }
                            else
                            {
                                squareImage.color = Color.green; // Green = Perfect shot!
                                if (TutorialManager.Instance != null) TutorialManager.Instance.OnSubjectFramed();
                            }
                        }
                    }
                    else
                    {
                        trackingSquare.gameObject.SetActive(false);
                    }
                }
                else
                {
                    trackingSquare.gameObject.SetActive(false);
                }
            }
            else if (trackingSquare != null)
            {
                trackingSquare.gameObject.SetActive(false);
            }
        }

        private void SampleVideoFrame()
        {
            if (targetSubject == null) targetSubject = FindObjectOfType<RecordableSubject>();
            if (targetSubject == null) { framesSampled++; return; }

            Vector3 targetCenter = GetSubjectCenter(targetSubject);
            Vector3 viewPos = filmCamera.WorldToViewportPoint(targetCenter);

            if (viewPos.z <= 0 || viewPos.x < 0 || viewPos.x > 1 || viewPos.y < 0 || viewPos.y > 1) { framesSampled++; return; }

            Vector3 directionToTarget = targetCenter - filmCamera.transform.position;
            float distToSub = Vector3.Distance(filmCamera.transform.position, targetCenter);

            bool isBlocked = false;
            RaycastHit[] hits = Physics.RaycastAll(filmCamera.transform.position, directionToTarget, distToSub);

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.root == this.transform.root) continue;
                if (hit.collider.isTrigger) continue;
                if (hit.collider.GetComponentInParent<RecordableSubject>() != null) continue;

                if (hit.distance < distToSub - 0.3f)
                {
                    isBlocked = true;
                    break;
                }
            }

            if (isBlocked) { framesSampled++; return; }

            int progress = PlayerPrefs.GetInt("TutorialProgress", 0);
            bool isTutorial = (progress < 2);

            float framingScore = isTutorial ? GradeCenterFraming(viewPos) : GradeRuleOfThirds(viewPos);
            float lightingScore = isTutorial ? GradeBasicLighting(targetCenter) : Grade3PointLighting(targetCenter);
            float focusScore = 30f;

            totalCameraScoreAccumulated += (framingScore + focusScore);
            totalLightingScoreAccumulated += lightingScore;
            framesSampled++;
        }

        private float GradeCenterFraming(Vector3 viewPos)
        {
            float score = 40f;
            float distFromCenter = Vector2.Distance(new Vector2(0.5f, 0.5f), new Vector2(viewPos.x, viewPos.y));

            if (distFromCenter > 0.1f) score -= (distFromCenter - 0.1f) * 200f;

            return Mathf.Clamp(score, 0f, 40f);
        }

        private float GradeRuleOfThirds(Vector3 viewPos)
        {
            if (viewPos.x > 0.4f && viewPos.x < 0.6f)
            {
                return 0f;
            }

            float score = 40f;
            float distToLeftThird = Mathf.Abs(viewPos.x - 0.33f);
            float distToRightThird = Mathf.Abs(viewPos.x - 0.66f);
            float closestThirdDist = Mathf.Min(distToLeftThird, distToRightThird);

            if (closestThirdDist > 0.1f) score -= (closestThirdDist - 0.1f) * 400f;

            return Mathf.Clamp(score, 0f, 40f);
        }

        private float GradeBasicLighting(Vector3 targetCenter)
        {
            float score = 0f;
            FilmLightItem[] activeLights = FindObjectsOfType<FilmLightItem>();

            foreach (FilmLightItem light in activeLights)
            {
                if (light.spotlight.enabled)
                {
                    Vector3 cameraArrow = (filmCamera.transform.position - targetCenter).normalized;
                    Vector3 lightArrow = (light.transform.position - targetCenter).normalized;
                    float angle = Vector3.Dot(cameraArrow, lightArrow);

                    if (angle < -0.2f) score = 30f;
                }
            }
            return score;
        }

        private float Grade3PointLighting(Vector3 targetCenter)
        {
            FilmLightItem[] activeLights = FindObjectsOfType<FilmLightItem>();
            bool hasKey = false, hasFill = false, hasBacklight = false;

            foreach (FilmLightItem light in activeLights)
            {
                if (!light.spotlight.enabled) continue;

                Vector3 cameraArrow = (filmCamera.transform.position - targetCenter).normalized;
                Vector3 lightArrow = (light.transform.position - targetCenter).normalized;
                float angle = Vector3.Dot(cameraArrow, lightArrow);

                if (angle > 0.5f) hasBacklight = true;
                else if (angle < -0.2f)
                {
                    if (!hasKey) hasKey = true;
                    else hasFill = true;
                }
            }

            float score = 0f;
            if (hasKey) score += 10f;
            if (hasFill) score += 10f;
            if (hasBacklight) score += 10f;

            return score;
        }

        private void InsertSDCard()
        {
            if (isSDCardInserted) return;
            Player.Interactor.EquipmentInteractor hotbar = GetComponentInParent<Player.Interactor.EquipmentInteractor>();
            if (hotbar != null && hotbar.HasBlankSDCard())
            {
                hotbar.ConsumeBlankSDCard();
                isSDCardInserted = true;

                HotbarUIManager ui = FindObjectOfType<HotbarUIManager>();
                if (ui != null) ui.UpdateGuideText(EquipmentControls);
            }
            if (TutorialManager.Instance != null) TutorialManager.Instance.OnCardInsertedToCamera();
        }

        private void ToggleRecording()
        {
            if (!isRecording)
            {
                if (TutorialManager.Instance != null && !TutorialManager.Instance.CanRecord())
                {
                    return;
                }
            }

            if (isRecording && TutorialManager.Instance != null && TutorialManager.Instance.currentStep == TutorialManager.TutorialStep.RecordVideo)
            {
                float currentDuration = Time.time - recordingStartTime;
                if (currentDuration < 10f)
                {
                    TutorialManager.Instance.ShowWarning($"Keep recording! We need at least 10 seconds. You only have {currentDuration:F1}s.");
                    return;
                }
            }

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
                    totalCameraScoreAccumulated = 0f;
                    totalLightingScoreAccumulated = 0f;
                    framesSampled = 0;
                    nextSampleTime = Time.time + 0.5f;
                }
                else
                {
                    generatedFileName = pixelRecorder.StopRecording();
                    finalDuration = Time.time - recordingStartTime;

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
                    cardScript.cameraScore = camScore;
                    cardScript.lightScore = lightScore;
                    cardScript.MarkAsUsed();
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
                TogglePlayerUI(true);
            }
            base.OnDropped(playerCamera);
        }
    }
}