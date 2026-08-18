using UnityEngine;
using TMPro;
using Player.Manager;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

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

        [Header("--- NEW UI FEATURES ---")]
        public TMP_Text recordStateText;
        public RectTransform trackingSquare;
        private RecordableSubject targetSubject;
        private Renderer[] targetRenderers;
        private Image trackingSquareImage;
        private Vector3[] trackingCorners = new Vector3[8];
        private RaycastHit[] trackingHits = new RaycastHit[16];
        private GameObject ruleOfThirdsGrid;
        private Image[] ruleOfThirdsIntersections = new Image[4];
        private TMP_Text ruleOfThirdsInstructionText;
        private bool isLevel2Camera = false;

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
        private float nextHUDUpdateTime = 0f;

        private FilmLightItem[] activeLights;
        private float nextLightRefreshTime = 0f;

        private CubeActor level3Actor;
        private CubeVehicle level3Vehicle;
        private Renderer[] level3ActorRenderers;
        private Renderer[] level3VehicleRenderers;
        private float nextLevel3TargetRefreshTime = 0f;

        private CampaignProduct campaignProduct;
        private Renderer[] campaignProductRenderers;
        private float nextCampaignTargetRefreshTime = 0f;

        private int recordingCampaignLevel = 1;
        private float recordedCoverageAccumulated = 0f;
        private float recordedScreenDirectionAccumulated = 0f;
        private int recordedMetadataSamples = 0;
        private int recordedVisibleSamples = 0;
        private int recordedSoftLightSamples = 0;
        private int recordedThreePointSamples = 0;
        private string recordedActorPose = "";

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

            if (trackingSquare != null) trackingSquareImage = trackingSquare.GetComponent<Image>();
            CacheTargetSubject();
            isLevel2Camera = EquipmentName == "Level 2 Camera";
            if (isLevel2Camera)
            {
                ConfigureLevel2Camera();
                CreateRuleOfThirdsGrid();
            }
        }

        private void ConfigureLevel2Camera()
        {
            minFOV = 28f;
            maxFOV = 55f;
            zoomSpeed = 2f;
            pedestalSpeed = 0.35f;
            maxPedestalUp = 0.75f;
            maxPedestalDown = -0.75f;
            focusBoxRadius = 0.4f;
            focusSmoothTime = 0.15f;
            swayIntensity = 0.12f;
            swaySpeed = 0.35f;

            if (filmCamera != null) filmCamera.fieldOfView = 42f;
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

        private void CreateRuleOfThirdsGrid()
        {
            if (filmUICanvas == null || ruleOfThirdsGrid != null) return;

            ruleOfThirdsGrid = new GameObject("Rule Of Thirds Grid", typeof(RectTransform));
            ruleOfThirdsGrid.transform.SetParent(filmUICanvas.transform, false);

            RectTransform gridRect = ruleOfThirdsGrid.GetComponent<RectTransform>();
            gridRect.anchorMin = Vector2.zero;
            gridRect.anchorMax = Vector2.one;
            gridRect.offsetMin = Vector2.zero;
            gridRect.offsetMax = Vector2.zero;

            CreateGridLine("Left Third", ruleOfThirdsGrid.transform, new Vector2(0.333f, 0f), new Vector2(0.333f, 1f), new Vector2(2f, 0f));
            CreateGridLine("Right Third", ruleOfThirdsGrid.transform, new Vector2(0.666f, 0f), new Vector2(0.666f, 1f), new Vector2(2f, 0f));
            CreateGridLine("Top Third", ruleOfThirdsGrid.transform, new Vector2(0f, 0.666f), new Vector2(1f, 0.666f), new Vector2(0f, 2f));
            CreateGridLine("Bottom Third", ruleOfThirdsGrid.transform, new Vector2(0f, 0.333f), new Vector2(1f, 0.333f), new Vector2(0f, 2f));
            ruleOfThirdsIntersections[0] = CreateGridIntersection("Lower Left Power Point", ruleOfThirdsGrid.transform, new Vector2(0.333f, 0.333f));
            ruleOfThirdsIntersections[1] = CreateGridIntersection("Upper Left Power Point", ruleOfThirdsGrid.transform, new Vector2(0.333f, 0.666f));
            ruleOfThirdsIntersections[2] = CreateGridIntersection("Lower Right Power Point", ruleOfThirdsGrid.transform, new Vector2(0.666f, 0.333f));
            ruleOfThirdsIntersections[3] = CreateGridIntersection("Upper Right Power Point", ruleOfThirdsGrid.transform, new Vector2(0.666f, 0.666f));
            CreateGridLabel("Left Third Label", ruleOfThirdsGrid.transform, new Vector2(0.333f, 0.08f), "LEFT THIRD");
            CreateGridLabel("Right Third Label", ruleOfThirdsGrid.transform, new Vector2(0.666f, 0.08f), "RIGHT THIRD");
            CreateRuleOfThirdsLessonPanel(ruleOfThirdsGrid.transform);

            ruleOfThirdsGrid.transform.SetAsLastSibling();
            ruleOfThirdsGrid.SetActive(false);
        }

        private void CreateGridLine(string lineName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
        {
            GameObject lineObject = new GameObject(lineName, typeof(RectTransform), typeof(Image));
            lineObject.transform.SetParent(parent, false);

            RectTransform lineRect = lineObject.GetComponent<RectTransform>();
            lineRect.anchorMin = anchorMin;
            lineRect.anchorMax = anchorMax;
            lineRect.anchoredPosition = Vector2.zero;
            lineRect.sizeDelta = sizeDelta;

            Image lineImage = lineObject.GetComponent<Image>();
            lineImage.color = new Color(1f, 1f, 1f, 0.8f);
            lineImage.raycastTarget = false;
        }

        private Image CreateGridIntersection(string markerName, Transform parent, Vector2 anchor)
        {
            GameObject markerObject = new GameObject(markerName, typeof(RectTransform), typeof(Image));
            markerObject.transform.SetParent(parent, false);

            RectTransform markerRect = markerObject.GetComponent<RectTransform>();
            markerRect.anchorMin = anchor;
            markerRect.anchorMax = anchor;
            markerRect.anchoredPosition = Vector2.zero;
            markerRect.sizeDelta = new Vector2(18f, 18f);
            markerRect.localEulerAngles = new Vector3(0f, 0f, 45f);

            Image markerImage = markerObject.GetComponent<Image>();
            markerImage.color = new Color(1f, 0.78f, 0.05f, 0.75f);
            markerImage.raycastTarget = false;
            return markerImage;
        }

        private void CreateGridLabel(string labelName, Transform parent, Vector2 anchor, string labelText)
        {
            GameObject labelObject = new GameObject(labelName, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = anchor;
            labelRect.anchorMax = anchor;
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(180f, 28f);

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = focusText != null ? focusText.font : TMP_Settings.defaultFontAsset;
            label.fontSize = 17f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.86f, 0.25f, 0.9f);
            label.raycastTarget = false;
            label.text = labelText;
        }

        private void CreateRuleOfThirdsLessonPanel(Transform parent)
        {
            GameObject panelObject = new GameObject("Rule Of Thirds Lesson Panel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.92f);
            panelRect.anchorMax = new Vector2(0.5f, 0.92f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(920f, 82f);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.02f, 0.04f, 0.08f, 0.82f);
            panelImage.raycastTarget = false;

            GameObject instructionObject = new GameObject("Rule Of Thirds Instruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            instructionObject.transform.SetParent(panelObject.transform, false);

            RectTransform instructionRect = instructionObject.GetComponent<RectTransform>();
            instructionRect.anchorMin = Vector2.zero;
            instructionRect.anchorMax = Vector2.one;
            instructionRect.offsetMin = new Vector2(20f, 8f);
            instructionRect.offsetMax = new Vector2(-20f, -8f);

            ruleOfThirdsInstructionText = instructionObject.GetComponent<TextMeshProUGUI>();
            ruleOfThirdsInstructionText.font = focusText != null ? focusText.font : TMP_Settings.defaultFontAsset;
            ruleOfThirdsInstructionText.fontSize = 21f;
            ruleOfThirdsInstructionText.fontStyle = FontStyles.Bold;
            ruleOfThirdsInstructionText.alignment = TextAlignmentOptions.Center;
            ruleOfThirdsInstructionText.color = Color.white;
            ruleOfThirdsInstructionText.raycastTarget = false;
            ruleOfThirdsInstructionText.text = "RULE OF THIRDS  •  PLACE THE PRODUCT ON A YELLOW POWER POINT";
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

            if (ruleOfThirdsGrid != null) ruleOfThirdsGrid.SetActive(isCameraActive);

            if (TutorialManager.Instance != null)
            {
                if (isCameraActive) TutorialManager.Instance.OnCameraViewEntered(EquipmentName);
                else TutorialManager.Instance.OnCameraViewExited(EquipmentName);
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
            if (input.InsertCard) InsertSDCard();

            if (!isCameraActive) return;

            if (input.Record)
            {
                if (!isRecording && !isSDCardInserted) { Debug.LogWarning("Insert SD Card first!"); return; }
                ToggleRecording();
            }

            // --- THE FIX: Only allow camera adjustments if NOT recording ---
            if (!isRecording)
            {
                float scroll = input.EquipmentAdjust;
                if (scroll > 0) filmCamera.fieldOfView -= zoomSpeed;
                else if (scroll < 0) filmCamera.fieldOfView += zoomSpeed;
                filmCamera.fieldOfView = Mathf.Clamp(filmCamera.fieldOfView, minFOV, maxFOV);

                float pedestalShift = input.CameraPedestal * pedestalSpeed * Time.deltaTime;
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
            UpdateRuleOfThirdsPractice();

            if (isRecording)
            {
                if (Time.time >= nextSampleTime)
                {
                    SampleVideoFrame();
                    nextSampleTime = Time.time + 0.5f;
                }

                bool requiresCenterFraming = CampaignProgression.GetCurrentLevel() == 1;
                if (requiresCenterFraming && targetSubject != null && filmCamera != null)
                {
                    Vector3 targetCenter = GetSubjectCenter(targetSubject);
                    Vector3 viewPos = filmCamera.WorldToViewportPoint(targetCenter);

                    bool isCenteredX = viewPos.x >= 0.35f && viewPos.x <= 0.65f;
                    bool isCenteredY = viewPos.y >= 0.35f && viewPos.y <= 0.65f;
                    bool isInFrontOfCamera = viewPos.z > 0;

                    if (!isCenteredX || !isCenteredY || !isInFrontOfCamera)
                    {
                        if (TutorialManager.Instance != null)
                            TutorialManager.Instance.ShowWarning("You moved the camera! Keep the subject in the center! Recording stopped.");

                        ToggleRecording(true);
                    }
                }
            }
        }

        private void UpdateRuleOfThirdsPractice()
        {
            if (!isLevel2Camera || filmCamera == null || GokeLevelManager.Instance == null) return;

            if (targetSubject == null) CacheTargetSubject();
            if (targetSubject == null)
            {
                GokeLevelManager.Instance.OnRuleOfThirdsPracticeUpdated(false);
                return;
            }

            Vector3 targetCenter = GetSubjectCenter(targetSubject);
            Vector3 viewPosition = filmCamera.WorldToViewportPoint(targetCenter);
            bool hasViewportBounds = TryGetViewportBounds(targetRenderers, out Vector4 viewportBounds);

            float horizontalDistance = Mathf.Min(Mathf.Abs(viewPosition.x - 0.333f), Mathf.Abs(viewPosition.x - 0.666f));
            float verticalDistance = Mathf.Min(Mathf.Abs(viewPosition.y - 0.333f), Mathf.Abs(viewPosition.y - 0.666f));
            float subjectCoverage = hasViewportBounds ? Mathf.Max(viewportBounds.z - viewportBounds.x, viewportBounds.w - viewportBounds.y) : 0f;

            bool isOnIntersection = horizontalDistance <= 0.065f && verticalDistance <= 0.085f;
            bool hasUsefulShotSize = subjectCoverage >= 0.2f && subjectCoverage <= 0.58f;
            bool isFullyVisible = hasViewportBounds && GradeViewportVisibility(viewportBounds, 0.03f) >= 0.99f;
            bool hasCorrectComposition = viewPosition.z > 0f && isOnIntersection && hasUsefulShotSize && isFullyVisible;

            UpdateRuleOfThirdsLessonOverlay(viewPosition, subjectCoverage, hasViewportBounds, isFullyVisible, hasCorrectComposition);

            GokeLevelManager.Instance.OnRuleOfThirdsPracticeUpdated(hasCorrectComposition);
        }

        private void UpdateRuleOfThirdsLessonOverlay(Vector3 viewPosition, float subjectCoverage, bool hasViewportBounds, bool isFullyVisible, bool hasCorrectComposition)
        {
            float leftDistance = Mathf.Abs(viewPosition.x - 0.333f);
            float rightDistance = Mathf.Abs(viewPosition.x - 0.666f);
            float bottomDistance = Mathf.Abs(viewPosition.y - 0.333f);
            float topDistance = Mathf.Abs(viewPosition.y - 0.666f);
            int closestIntersection = (leftDistance <= rightDistance ? 0 : 2) + (bottomDistance <= topDistance ? 0 : 1);

            for (int i = 0; i < ruleOfThirdsIntersections.Length; i++)
            {
                if (ruleOfThirdsIntersections[i] == null) continue;

                bool isClosest = i == closestIntersection;
                ruleOfThirdsIntersections[i].color = hasCorrectComposition && isClosest
                    ? new Color(0.2f, 1f, 0.45f, 1f)
                    : isClosest
                        ? new Color(1f, 0.78f, 0.05f, 1f)
                        : new Color(1f, 0.78f, 0.05f, 0.45f);
                ruleOfThirdsIntersections[i].rectTransform.sizeDelta = isClosest ? new Vector2(24f, 24f) : new Vector2(18f, 18f);
            }

            if (ruleOfThirdsInstructionText == null) return;

            if (viewPosition.z <= 0f || !hasViewportBounds)
            {
                ruleOfThirdsInstructionText.text = "<color=#FF6666>FIND THE PRODUCT</color>  •  KEEP IT INSIDE THE VIEWFINDER";
                return;
            }

            if (Mathf.Min(leftDistance, rightDistance) > 0.065f)
            {
                ruleOfThirdsInstructionText.text = "<color=#FFD84A>MOVE LEFT OR RIGHT</color>  •  ALIGN THE PRODUCT WITH A VERTICAL THIRD";
                return;
            }

            if (Mathf.Min(bottomDistance, topDistance) > 0.085f)
            {
                ruleOfThirdsInstructionText.text = "<color=#FFD84A>USE Q / E</color>  •  ALIGN THE PRODUCT WITH A HORIZONTAL THIRD";
                return;
            }

            if (!isFullyVisible)
            {
                ruleOfThirdsInstructionText.text = "<color=#FF9B54>LEAVE BREATHING ROOM</color>  •  DO NOT CROP THE PRODUCT";
                return;
            }

            if (subjectCoverage < 0.2f)
            {
                ruleOfThirdsInstructionText.text = "<color=#FFD84A>SCROLL UP TO ZOOM IN</color>  •  MAKE THE PRODUCT VISUALLY DOMINANT";
                return;
            }

            if (subjectCoverage > 0.58f)
            {
                ruleOfThirdsInstructionText.text = "<color=#FFD84A>SCROLL DOWN TO ZOOM OUT</color>  •  PRESERVE NEGATIVE SPACE FOR GRAPHICS";
                return;
            }

            ruleOfThirdsInstructionText.text = "<color=#55FF88>GOOD VISUAL HIERARCHY</color>  •  HOLD THE RULE OF THIRDS FRAME STEADY";
        }

        public bool IsCameraViewActive()
        {
            return isCameraActive;
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

            Renderer[] rends = targetRenderers;
            if (sub != targetSubject || rends == null)
                rends = sub.GetComponentsInChildren<Renderer>();

            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                foreach (Renderer r in rends) b.Encapsulate(r.bounds);
                return b.center;
            }
            return sub.transform.position + Vector3.up * 0.5f;
        }

        private void UpdateCameraHUD(bool forceUpdate = false)
        {
            if (!forceUpdate && Time.unscaledTime < nextHUDUpdateTime) return;
            nextHUDUpdateTime = Time.unscaledTime + 0.05f;

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
        }

        private void UpdateTrackingSquare()
        {
            if (targetSubject == null) CacheTargetSubject();

            if (targetSubject != null && trackingSquare != null && filmCamera != null)
            {
                Renderer[] rends = targetRenderers;
                if (rends.Length > 0)
                {
                    Vector3 targetCenter = GetSubjectCenter(targetSubject);
                    Vector3 viewPos = filmCamera.WorldToViewportPoint(targetCenter);
                    Vector3 screenPos = filmCamera.WorldToScreenPoint(targetCenter);

                    if (viewPos.z > 0 && viewPos.x >= 0 && viewPos.x <= 1 && viewPos.y >= 0 && viewPos.y <= 1)
                    {
                        trackingSquare.gameObject.SetActive(true);
                        trackingSquare.position = screenPos;

                        Bounds bounds = rends[0].bounds;
                        foreach (Renderer r in rends) bounds.Encapsulate(r.bounds);

                        trackingCorners[0] = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
                        trackingCorners[1] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
                        trackingCorners[2] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
                        trackingCorners[3] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
                        trackingCorners[4] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
                        trackingCorners[5] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
                        trackingCorners[6] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
                        trackingCorners[7] = new Vector3(bounds.max.x, bounds.max.y, bounds.max.z);

                        float minX = float.MaxValue, minY = float.MaxValue;
                        float maxX = float.MinValue, maxY = float.MinValue;

                        foreach (Vector3 corner in trackingCorners)
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

                        Vector3 directionToTarget = targetCenter - filmCamera.transform.position;
                        float distToSub = Vector3.Distance(filmCamera.transform.position, targetCenter);

                        bool isBlocked = IsSubjectBlocked(directionToTarget, distToSub);

                        if (trackingSquareImage != null)
                        {
                            if (isBlocked)
                            {
                                trackingSquareImage.color = Color.red;
                            }
                            else
                            {
                                trackingSquareImage.color = Color.green;
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
            int currentLevel = recordingCampaignLevel;

            if (currentLevel == 3)
            {
                SampleLevel3Frame();
                return;
            }

            if (currentLevel == 4)
            {
                SampleLevel4Frame();
                return;
            }

            if (currentLevel == 5)
            {
                SampleLevel5Frame();
                return;
            }

            if (targetSubject == null) CacheTargetSubject();
            if (targetSubject == null)
            {
                RecordMissingEvidence();
                framesSampled++;
                return;
            }

            Vector3 targetCenter = GetSubjectCenter(targetSubject);
            Vector3 viewPos = filmCamera.WorldToViewportPoint(targetCenter);
            bool hasViewportBounds = TryGetViewportBounds(targetRenderers, out Vector4 targetViewport);

            if (viewPos.z <= 0 || viewPos.x < 0 || viewPos.x > 1 || viewPos.y < 0 || viewPos.y > 1)
            {
                RecordProductionEvidence(targetViewport, false, null, targetCenter);
                framesSampled++;
                return;
            }

            Vector3 directionToTarget = targetCenter - filmCamera.transform.position;
            float distToSub = Vector3.Distance(filmCamera.transform.position, targetCenter);
            bool isBlocked = IsSubjectBlocked(directionToTarget, distToSub);
            bool isFullyVisible = hasViewportBounds && GradeViewportVisibility(targetViewport, 0.05f) >= 0.99f && !isBlocked;
            RecordProductionEvidence(targetViewport, isFullyVisible, null, targetCenter);

            if (isBlocked)
            {
                framesSampled++;
                return;
            }

            bool isLevel1 = currentLevel == 1;

            float framingScore = isLevel1 ? GradeCenterFraming(viewPos) : GradeRuleOfThirds(viewPos);
            float shotSizeScore = GradeSubjectSize(targetRenderers, 0.22f, 0.65f);
            float lightingScore = isLevel1 ? GradeBasicLighting(targetCenter) : Grade3PointLighting(targetCenter);

            totalCameraScoreAccumulated += (framingScore + shotSizeScore);
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
            float distToLeftThird = Mathf.Abs(viewPos.x - 0.33f);
            float distToRightThird = Mathf.Abs(viewPos.x - 0.66f);
            float distToBottomThird = Mathf.Abs(viewPos.y - 0.33f);
            float distToTopThird = Mathf.Abs(viewPos.y - 0.66f);

            float horizontalScore = 24f * Mathf.Clamp01(1f - Mathf.Min(distToLeftThird, distToRightThird) / 0.17f);
            float verticalScore = 16f * Mathf.Clamp01(1f - Mathf.Min(distToBottomThird, distToTopThird) / 0.22f);

            return horizontalScore + verticalScore;
        }

        private float GradeSubjectSize(Renderer[] renderers, float idealMinimum, float idealMaximum)
        {
            if (!TryGetViewportBounds(renderers, out Vector4 viewportBounds)) return 0f;

            float subjectWidth = viewportBounds.z - viewportBounds.x;
            float subjectHeight = viewportBounds.w - viewportBounds.y;
            float subjectCoverage = Mathf.Max(subjectWidth, subjectHeight);
            float score = 30f;

            if (subjectCoverage < idealMinimum)
                score -= (idealMinimum - subjectCoverage) * 140f;
            else if (subjectCoverage > idealMaximum)
                score -= (subjectCoverage - idealMaximum) * 120f;

            if (viewportBounds.x < 0f) score -= Mathf.Abs(viewportBounds.x) * 100f;
            if (viewportBounds.y < 0f) score -= Mathf.Abs(viewportBounds.y) * 100f;
            if (viewportBounds.z > 1f) score -= (viewportBounds.z - 1f) * 100f;
            if (viewportBounds.w > 1f) score -= (viewportBounds.w - 1f) * 100f;

            return Mathf.Clamp(score, 0f, 30f);
        }

        private float GradeBasicLighting(Vector3 targetCenter)
        {
            float bestScore = 0f;
            FilmLightItem[] lights = GetActiveLights();

            foreach (FilmLightItem light in lights)
            {
                if (light == null || !light.IsPoweredOn() || light.spotlight == null) continue;

                Vector3 lightPosition = light.spotlight.transform.position;
                Vector3 directionToTarget = (targetCenter - lightPosition).normalized;
                Vector3 cameraArrow = (filmCamera.transform.position - targetCenter).normalized;
                Vector3 lightArrow = (lightPosition - targetCenter).normalized;

                float intensityScore = 10f * Mathf.Clamp01(1f - Mathf.Abs(light.intensityPercent - 45f) / 55f);
                float tiltScore = 5f * Mathf.Clamp01(1f - Mathf.Abs(light.GetCurrentTilt() + 5f) / 15f);
                float aimScore = 8f * Mathf.InverseLerp(0.5f, 0.95f, Vector3.Dot(light.spotlight.transform.forward, directionToTarget));
                float placementScore = 4f * Mathf.InverseLerp(-0.1f, 0.8f, Vector3.Dot(cameraArrow, lightArrow));
                float distanceScore = 3f * GradeRange(Vector3.Distance(lightPosition, targetCenter), 1.5f, 6f);

                bestScore = Mathf.Max(bestScore, intensityScore + tiltScore + aimScore + placementScore + distanceScore);
            }

            return Mathf.Clamp(bestScore, 0f, 30f);
        }

        private float Grade3PointLighting(Vector3 targetCenter)
        {
            FilmLightItem[] lights = GetActiveLights();
            FindThreePointLights(targetCenter, lights, out FilmLightItem keyLight, out FilmLightItem fillLight, out FilmLightItem backLight);

            float score = GradeThreePointRole(keyLight, targetCenter, 75f, 50f, false);
            score += GradeThreePointRole(fillLight, targetCenter, 40f, 35f, false);
            score += GradeThreePointRole(backLight, targetCenter, 60f, 45f, true);

            if (keyLight != null && fillLight != null)
            {
                float keySide = Vector3.Dot((keyLight.spotlight.transform.position - targetCenter).normalized, filmCamera.transform.right);
                float fillSide = Vector3.Dot((fillLight.spotlight.transform.position - targetCenter).normalized, filmCamera.transform.right);
                if (keySide * fillSide >= -0.05f) score -= 4f;
            }

            if (keyLight == null || fillLight == null || backLight == null) score = Mathf.Min(score, 7f);

            return Mathf.Clamp(score, 0f, 30f);
        }

        public bool HasThreePointLightingRoles(Vector3 targetCenter)
        {
            return HasThreePointLightingRoles(targetCenter, FindObjectsOfType<FilmLightItem>());
        }

        private bool HasThreePointLightingRoles(Vector3 targetCenter, FilmLightItem[] lights)
        {
            if (filmCamera == null) return false;

            FindThreePointLights(targetCenter, lights, out FilmLightItem keyLight, out FilmLightItem fillLight, out FilmLightItem backLight);
            if (keyLight == null || fillLight == null || backLight == null) return false;

            float keySide = Vector3.Dot((keyLight.spotlight.transform.position - targetCenter).normalized, filmCamera.transform.right);
            float fillSide = Vector3.Dot((fillLight.spotlight.transform.position - targetCenter).normalized, filmCamera.transform.right);
            return keySide * fillSide < -0.05f;
        }

        private void FindThreePointLights(Vector3 targetCenter, FilmLightItem[] lights, out FilmLightItem keyLight, out FilmLightItem fillLight, out FilmLightItem backLight)
        {
            keyLight = null;
            fillLight = null;
            backLight = null;
            float keyOutput = 0f;
            float fillOutput = 0f;
            float backOutput = 0f;

            foreach (FilmLightItem light in lights)
            {
                if (light == null || !light.IsPoweredOn() || light.spotlight == null) continue;

                Vector3 lightPosition = light.spotlight.transform.position;
                Vector3 directionToTarget = (targetCenter - lightPosition).normalized;
                if (Vector3.Dot(light.spotlight.transform.forward, directionToTarget) < 0.45f) continue;

                Vector3 cameraArrow = (filmCamera.transform.position - targetCenter).normalized;
                Vector3 lightArrow = (lightPosition - targetCenter).normalized;
                float cameraSideDot = Vector3.Dot(cameraArrow, lightArrow);
                float lightOutput = light.GetCurrentOutput();

                if (cameraSideDot < -0.15f)
                {
                    if (lightOutput > backOutput)
                    {
                        backLight = light;
                        backOutput = lightOutput;
                    }
                    continue;
                }

                if (lightOutput > keyOutput)
                {
                    fillLight = keyLight;
                    fillOutput = keyOutput;
                    keyLight = light;
                    keyOutput = lightOutput;
                }
                else if (lightOutput > fillOutput)
                {
                    fillLight = light;
                    fillOutput = lightOutput;
                }
            }
        }

        private float GradeThreePointRole(FilmLightItem light, Vector3 targetCenter, float idealIntensity, float intensityTolerance, bool isBackLight)
        {
            if (light == null || light.spotlight == null) return 0f;

            Vector3 lightPosition = light.spotlight.transform.position;
            Vector3 directionToTarget = (targetCenter - lightPosition).normalized;
            Vector3 cameraArrow = (filmCamera.transform.position - targetCenter).normalized;
            Vector3 lightArrow = (lightPosition - targetCenter).normalized;

            float score = 1f;
            score += 3f * Mathf.Clamp01(1f - Mathf.Abs(light.intensityPercent - idealIntensity) / intensityTolerance);
            score += 3f * Mathf.InverseLerp(0.5f, 0.95f, Vector3.Dot(light.spotlight.transform.forward, directionToTarget));
            score += GradeRange(Vector3.Distance(lightPosition, targetCenter), 1.5f, 7f);

            if (isBackLight)
            {
                score += 2f * Mathf.InverseLerp(0.15f, 0.75f, -Vector3.Dot(cameraArrow, lightArrow));
            }
            else
            {
                float cameraSideScore = Mathf.InverseLerp(0f, 0.55f, Vector3.Dot(cameraArrow, lightArrow));
                float sideAngleScore = Mathf.InverseLerp(0.2f, 0.7f, Mathf.Abs(Vector3.Dot(lightArrow, filmCamera.transform.right)));
                score += cameraSideScore + sideAngleScore;
            }

            return score;
        }

        private void SampleLevel3Frame()
        {
            CacheLevel3Targets();

            if (level3Actor == null || level3Vehicle == null)
            {
                RecordMissingEvidence();
                framesSampled++;
                return;
            }

            if (!TryGetViewportBounds(level3ActorRenderers, out Vector4 actorViewport) ||
                !TryGetViewportBounds(level3VehicleRenderers, out Vector4 vehicleViewport) ||
                !TryGetWorldBounds(level3ActorRenderers, out Bounds actorBounds) ||
                !TryGetWorldBounds(level3VehicleRenderers, out Bounds vehicleBounds))
            {
                RecordMissingEvidence();
                framesSampled++;
                return;
            }

            Bounds productionBounds = vehicleBounds;
            productionBounds.Encapsulate(actorBounds);

            float cameraScore = GradeLevel3Composition(actorViewport, vehicleViewport);
            bool isActorBlocked = IsCampaignTargetBlocked(actorBounds.center, level3Actor.transform);
            bool isVehicleBlocked = IsCampaignTargetBlocked(vehicleBounds.center, level3Vehicle.transform);
            if (isActorBlocked) cameraScore -= 10f;
            if (isVehicleBlocked) cameraScore -= 15f;

            Vector4 groupViewport = CombineViewportBounds(actorViewport, vehicleViewport);
            bool allSubjectsVisible = GradeViewportVisibility(actorViewport, 0.05f) >= 0.99f &&
                                      GradeViewportVisibility(vehicleViewport, 0.05f) >= 0.99f &&
                                      !isActorBlocked && !isVehicleBlocked;
            RecordProductionEvidence(groupViewport, allSubjectsVisible, level3Actor, productionBounds.center);

            totalCameraScoreAccumulated += Mathf.Clamp(cameraScore, 0f, 70f);
            totalLightingScoreAccumulated += GradeLevel3Lighting(productionBounds.center);
            framesSampled++;
        }

        private float GradeLevel3Composition(Vector4 actorViewport, Vector4 vehicleViewport)
        {
            float score = 0f;
            score += 12f * GradeViewportVisibility(actorViewport, 0.05f);
            score += 16f * GradeViewportVisibility(vehicleViewport, 0.05f);

            float groupMinimumX = Mathf.Min(actorViewport.x, vehicleViewport.x);
            float groupMinimumY = Mathf.Min(actorViewport.y, vehicleViewport.y);
            float groupMaximumX = Mathf.Max(actorViewport.z, vehicleViewport.z);
            float groupMaximumY = Mathf.Max(actorViewport.w, vehicleViewport.w);
            float groupWidth = groupMaximumX - groupMinimumX;
            float groupHeight = groupMaximumY - groupMinimumY;
            float groupCoverage = Mathf.Max(groupWidth, groupHeight);
            score += 14f * GradeRange(groupCoverage, 0.4f, 0.82f);

            Vector2 groupCenter = new Vector2((groupMinimumX + groupMaximumX) * 0.5f, (groupMinimumY + groupMaximumY) * 0.5f);
            score += 10f * Mathf.Clamp01(1f - Vector2.Distance(groupCenter, new Vector2(0.5f, 0.5f)) / 0.35f);

            float vehicleCenterX = (vehicleViewport.x + vehicleViewport.z) * 0.5f;
            float closestThird = Mathf.Min(Mathf.Abs(vehicleCenterX - 0.33f), Mathf.Abs(vehicleCenterX - 0.66f));
            score += 10f * Mathf.Clamp01(1f - closestThird / 0.2f);

            float actorCenterX = (actorViewport.x + actorViewport.z) * 0.5f;
            score += 5f * GradeRange(Mathf.Abs(actorCenterX - vehicleCenterX), 0.12f, 0.45f);
            score += 3f * GradeLowViewportOverlap(actorViewport, vehicleViewport);

            return Mathf.Clamp(score, 0f, 70f);
        }

        private float GradeLevel3Lighting(Vector3 targetCenter)
        {
            float bestScore = 0f;
            FilmLightItem[] lights = GetActiveLights();

            foreach (FilmLightItem light in lights)
            {
                if (light == null || !light.IsPoweredOn() || light.spotlight == null) continue;

                Vector3 lightPosition = light.spotlight.transform.position;
                Vector3 directionToTarget = (targetCenter - lightPosition).normalized;
                Vector3 cameraArrow = (filmCamera.transform.position - targetCenter).normalized;
                Vector3 lightArrow = (lightPosition - targetCenter).normalized;

                bool isSoftLight = light.EquipmentName == "Level 3 Soft Light" || !light.forcesHardLight;
                float score = isSoftLight ? 5f : 0f;
                score += 7f * Mathf.Clamp01(1f - Mathf.Abs(light.intensityPercent - 75f) / 45f);
                score += 4f * Mathf.Clamp01(1f - Mathf.Abs(light.GetCurrentTilt() + 10f) / 25f);
                score += 8f * Mathf.InverseLerp(0.45f, 0.95f, Vector3.Dot(light.spotlight.transform.forward, directionToTarget));
                score += 3f * Mathf.InverseLerp(-0.15f, 0.75f, Vector3.Dot(cameraArrow, lightArrow));
                score += 3f * GradeRange(Vector3.Distance(lightPosition, targetCenter), 2f, 8f);

                if (!isSoftLight) score = Mathf.Min(score * 0.5f, 7f);
                bestScore = Mathf.Max(bestScore, score);
            }

            return Mathf.Clamp(bestScore, 0f, 30f);
        }

        private void SampleLevel4Frame()
        {
            CacheCampaignTargets(4);

            if (level3Actor == null || campaignProduct == null)
            {
                RecordMissingEvidence();
                framesSampled++;
                return;
            }

            if (!TryGetViewportBounds(level3ActorRenderers, out Vector4 actorViewport) ||
                !TryGetViewportBounds(campaignProductRenderers, out Vector4 productViewport) ||
                !TryGetWorldBounds(level3ActorRenderers, out Bounds actorBounds) ||
                !TryGetWorldBounds(campaignProductRenderers, out Bounds productBounds))
            {
                RecordMissingEvidence();
                framesSampled++;
                return;
            }

            Bounds productionBounds = productBounds;
            productionBounds.Encapsulate(actorBounds);

            float cameraScore = GradeLevel4Composition(actorViewport, productViewport);
            bool isActorBlocked = IsCampaignTargetBlocked(actorBounds.center, level3Actor.transform);
            bool isProductBlocked = IsCampaignTargetBlocked(productBounds.center, campaignProduct.transform);
            if (isActorBlocked) cameraScore -= 12f;
            if (isProductBlocked) cameraScore -= 18f;

            Vector4 groupViewport = CombineViewportBounds(actorViewport, productViewport);
            bool allSubjectsVisible = GradeViewportVisibility(actorViewport, 0.05f) >= 0.99f &&
                                      GradeViewportVisibility(productViewport, 0.05f) >= 0.99f &&
                                      !isActorBlocked && !isProductBlocked;
            RecordProductionEvidence(groupViewport, allSubjectsVisible, level3Actor, productionBounds.center, campaignProduct.transform);

            totalCameraScoreAccumulated += Mathf.Clamp(cameraScore, 0f, 70f);
            totalLightingScoreAccumulated += GradeLevel3Lighting(productionBounds.center);
            framesSampled++;
        }

        private float GradeLevel4Composition(Vector4 actorViewport, Vector4 productViewport)
        {
            float score = 0f;
            score += 18f * GradeViewportVisibility(actorViewport, 0.05f);
            score += 22f * GradeViewportVisibility(productViewport, 0.05f);

            Vector4 groupViewport = CombineViewportBounds(actorViewport, productViewport);
            float groupCoverage = GetViewportCoverage(groupViewport);
            score += 14f * GradeRange(groupCoverage, 0.35f, 0.75f);

            Vector2 groupCenter = GetViewportCenter(groupViewport);
            score += 8f * Mathf.Clamp01(1f - Vector2.Distance(groupCenter, new Vector2(0.5f, 0.5f)) / 0.35f);

            float productCenterX = GetViewportCenter(productViewport).x;
            float closestThird = Mathf.Min(Mathf.Abs(productCenterX - 0.33f), Mathf.Abs(productCenterX - 0.66f));
            score += 5f * Mathf.Clamp01(1f - closestThird / 0.2f);
            score += 3f * GradeLowViewportOverlap(actorViewport, productViewport);

            return Mathf.Clamp(score, 0f, 70f);
        }

        private void SampleLevel5Frame()
        {
            CacheCampaignTargets(5);

            if (level3Actor == null || campaignProduct == null || level3Vehicle == null)
            {
                RecordMissingEvidence();
                framesSampled++;
                return;
            }

            if (!TryGetViewportBounds(level3ActorRenderers, out Vector4 actorViewport) ||
                !TryGetViewportBounds(campaignProductRenderers, out Vector4 productViewport) ||
                !TryGetViewportBounds(level3VehicleRenderers, out Vector4 vehicleViewport) ||
                !TryGetWorldBounds(level3ActorRenderers, out Bounds actorBounds) ||
                !TryGetWorldBounds(campaignProductRenderers, out Bounds productBounds) ||
                !TryGetWorldBounds(level3VehicleRenderers, out Bounds vehicleBounds))
            {
                RecordMissingEvidence();
                framesSampled++;
                return;
            }

            Bounds productionBounds = vehicleBounds;
            productionBounds.Encapsulate(actorBounds);
            productionBounds.Encapsulate(productBounds);

            float cameraScore = GradeLevel5Composition(actorViewport, productViewport, vehicleViewport);
            bool isActorBlocked = IsCampaignTargetBlocked(actorBounds.center, level3Actor.transform);
            bool isProductBlocked = IsCampaignTargetBlocked(productBounds.center, campaignProduct.transform);
            bool isVehicleBlocked = IsCampaignTargetBlocked(vehicleBounds.center, level3Vehicle.transform);
            if (isActorBlocked) cameraScore -= 8f;
            if (isProductBlocked) cameraScore -= 15f;
            if (isVehicleBlocked) cameraScore -= 12f;

            Vector4 groupViewport = CombineViewportBounds(CombineViewportBounds(actorViewport, productViewport), vehicleViewport);
            bool allSubjectsVisible = GradeViewportVisibility(actorViewport, 0.05f) >= 0.99f &&
                                      GradeViewportVisibility(productViewport, 0.05f) >= 0.99f &&
                                      GradeViewportVisibility(vehicleViewport, 0.05f) >= 0.99f &&
                                      !isActorBlocked && !isProductBlocked && !isVehicleBlocked;
            RecordProductionEvidence(groupViewport, allSubjectsVisible, level3Actor, productionBounds.center, campaignProduct.transform);

            totalCameraScoreAccumulated += Mathf.Clamp(cameraScore, 0f, 70f);
            totalLightingScoreAccumulated += Grade3PointLighting(productionBounds.center);
            framesSampled++;
        }

        private float GradeLevel5Composition(Vector4 actorViewport, Vector4 productViewport, Vector4 vehicleViewport)
        {
            float score = 0f;
            score += 12f * GradeViewportVisibility(actorViewport, 0.05f);
            score += 14f * GradeViewportVisibility(productViewport, 0.05f);
            score += 16f * GradeViewportVisibility(vehicleViewport, 0.05f);

            Vector4 groupViewport = CombineViewportBounds(CombineViewportBounds(actorViewport, productViewport), vehicleViewport);
            float groupCoverage = GetViewportCoverage(groupViewport);
            score += 10f * GradeRange(groupCoverage, 0.45f, 0.88f);

            Vector2 groupCenter = GetViewportCenter(groupViewport);
            score += 6f * Mathf.Clamp01(1f - Vector2.Distance(groupCenter, new Vector2(0.5f, 0.5f)) / 0.4f);

            float productCenterX = GetViewportCenter(productViewport).x;
            float closestThird = Mathf.Min(Mathf.Abs(productCenterX - 0.33f), Mathf.Abs(productCenterX - 0.66f));
            score += 7f * Mathf.Clamp01(1f - closestThird / 0.22f);

            float lowOverlapScore = GradeLowViewportOverlap(actorViewport, productViewport);
            lowOverlapScore += GradeLowViewportOverlap(productViewport, vehicleViewport);
            score += 5f * Mathf.Clamp01(lowOverlapScore * 0.5f);

            return Mathf.Clamp(score, 0f, 70f);
        }

        private void CacheCampaignTargets(int campaignLevel)
        {
            bool hasRequiredTargets = campaignProduct != null && level3Actor != null && (campaignLevel != 5 || level3Vehicle != null);
            if (Time.time < nextCampaignTargetRefreshTime && hasRequiredTargets) return;

            level3Actor = FindObjectOfType<CubeActor>();
            level3Vehicle = campaignLevel == 5 ? FindObjectOfType<CubeVehicle>() : null;
            campaignProduct = null;

            CampaignProduct[] campaignProducts = FindObjectsOfType<CampaignProduct>();
            foreach (CampaignProduct product in campaignProducts)
            {
                if (product != null && product.campaignLevel == campaignLevel)
                {
                    campaignProduct = product;
                    break;
                }
            }

            level3ActorRenderers = level3Actor != null ? level3Actor.GetComponentsInChildren<Renderer>() : null;
            level3VehicleRenderers = level3Vehicle != null ? level3Vehicle.GetComponentsInChildren<Renderer>() : null;
            campaignProductRenderers = campaignProduct != null ? campaignProduct.GetComponentsInChildren<Renderer>() : null;
            nextCampaignTargetRefreshTime = Time.time + 1f;
        }

        private void CacheLevel3Targets()
        {
            if (Time.time < nextLevel3TargetRefreshTime && level3Actor != null && level3Vehicle != null) return;

            level3Actor = FindObjectOfType<CubeActor>();
            level3Vehicle = FindObjectOfType<CubeVehicle>();
            level3ActorRenderers = level3Actor != null ? level3Actor.GetComponentsInChildren<Renderer>() : null;
            level3VehicleRenderers = level3Vehicle != null ? level3Vehicle.GetComponentsInChildren<Renderer>() : null;
            nextLevel3TargetRefreshTime = Time.time + 1f;
        }

        private bool TryGetViewportBounds(Renderer[] renderers, out Vector4 viewportBounds)
        {
            viewportBounds = Vector4.zero;
            if (!TryGetWorldBounds(renderers, out Bounds worldBounds) || filmCamera == null) return false;

            SetBoundsCorners(worldBounds);
            float minimumX = float.MaxValue;
            float minimumY = float.MaxValue;
            float maximumX = float.MinValue;
            float maximumY = float.MinValue;

            for (int i = 0; i < trackingCorners.Length; i++)
            {
                Vector3 viewportPoint = filmCamera.WorldToViewportPoint(trackingCorners[i]);
                if (viewportPoint.z <= 0f) return false;

                minimumX = Mathf.Min(minimumX, viewportPoint.x);
                minimumY = Mathf.Min(minimumY, viewportPoint.y);
                maximumX = Mathf.Max(maximumX, viewportPoint.x);
                maximumY = Mathf.Max(maximumY, viewportPoint.y);
            }

            viewportBounds = new Vector4(minimumX, minimumY, maximumX, maximumY);
            return true;
        }

        private bool TryGetWorldBounds(Renderer[] renderers, out Bounds worldBounds)
        {
            worldBounds = new Bounds();
            if (renderers == null || renderers.Length == 0) return false;

            bool foundRenderer = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

                if (!foundRenderer)
                {
                    worldBounds = renderer.bounds;
                    foundRenderer = true;
                }
                else
                {
                    worldBounds.Encapsulate(renderer.bounds);
                }
            }

            return foundRenderer;
        }

        private void SetBoundsCorners(Bounds bounds)
        {
            Vector3 minimum = bounds.min;
            Vector3 maximum = bounds.max;

            trackingCorners[0] = new Vector3(minimum.x, minimum.y, minimum.z);
            trackingCorners[1] = new Vector3(maximum.x, minimum.y, minimum.z);
            trackingCorners[2] = new Vector3(minimum.x, maximum.y, minimum.z);
            trackingCorners[3] = new Vector3(maximum.x, maximum.y, minimum.z);
            trackingCorners[4] = new Vector3(minimum.x, minimum.y, maximum.z);
            trackingCorners[5] = new Vector3(maximum.x, minimum.y, maximum.z);
            trackingCorners[6] = new Vector3(minimum.x, maximum.y, maximum.z);
            trackingCorners[7] = new Vector3(maximum.x, maximum.y, maximum.z);
        }

        private float GradeViewportVisibility(Vector4 viewportBounds, float frameMargin)
        {
            float overflow = Mathf.Max(0f, frameMargin - viewportBounds.x);
            overflow += Mathf.Max(0f, frameMargin - viewportBounds.y);
            overflow += Mathf.Max(0f, viewportBounds.z - (1f - frameMargin));
            overflow += Mathf.Max(0f, viewportBounds.w - (1f - frameMargin));
            return Mathf.Clamp01(1f - overflow * 3f);
        }

        private float GradeLowViewportOverlap(Vector4 firstBounds, Vector4 secondBounds)
        {
            float overlapWidth = Mathf.Max(0f, Mathf.Min(firstBounds.z, secondBounds.z) - Mathf.Max(firstBounds.x, secondBounds.x));
            float overlapHeight = Mathf.Max(0f, Mathf.Min(firstBounds.w, secondBounds.w) - Mathf.Max(firstBounds.y, secondBounds.y));
            float overlapArea = overlapWidth * overlapHeight;
            float firstArea = Mathf.Max(0.001f, (firstBounds.z - firstBounds.x) * (firstBounds.w - firstBounds.y));
            float secondArea = Mathf.Max(0.001f, (secondBounds.z - secondBounds.x) * (secondBounds.w - secondBounds.y));
            float overlapRatio = overlapArea / Mathf.Min(firstArea, secondArea);
            return Mathf.Clamp01(1f - overlapRatio / 0.3f);
        }

        private Vector4 CombineViewportBounds(Vector4 firstBounds, Vector4 secondBounds)
        {
            return new Vector4(
                Mathf.Min(firstBounds.x, secondBounds.x),
                Mathf.Min(firstBounds.y, secondBounds.y),
                Mathf.Max(firstBounds.z, secondBounds.z),
                Mathf.Max(firstBounds.w, secondBounds.w)
            );
        }

        private float GetViewportCoverage(Vector4 viewportBounds)
        {
            return Mathf.Max(viewportBounds.z - viewportBounds.x, viewportBounds.w - viewportBounds.y);
        }

        private Vector2 GetViewportCenter(Vector4 viewportBounds)
        {
            return new Vector2(
                (viewportBounds.x + viewportBounds.z) * 0.5f,
                (viewportBounds.y + viewportBounds.w) * 0.5f
            );
        }

        private void RecordProductionEvidence(Vector4 groupViewport, bool allSubjectsVisible, CubeActor actor, Vector3 targetCenter, Transform continuityReference = null)
        {
            recordedMetadataSamples++;
            recordedCoverageAccumulated += GetViewportCoverage(groupViewport);
            if (allSubjectsVisible) recordedVisibleSamples++;
            if (IsUsingSoftLight(targetCenter)) recordedSoftLightSamples++;
            if (recordingCampaignLevel == 5 && HasThreePointLightingRoles(targetCenter, GetActiveLights())) recordedThreePointSamples++;

            if (actor != null)
            {
                recordedActorPose = actor.GetPoseName();
                recordedScreenDirectionAccumulated += GetActorScreenDirection(actor, continuityReference);
            }
        }

        private void RecordMissingEvidence()
        {
            recordedMetadataSamples++;
        }

        private int GetRecordedShotType()
        {
            if (recordedMetadataSamples <= 0) return 2;

            float averageCoverage = recordedCoverageAccumulated / recordedMetadataSamples;
            if (averageCoverage <= 0.45f) return 1;
            if (averageCoverage <= 0.75f) return 2;
            return 3;
        }

        private float GetActorScreenDirection(CubeActor actor, Transform continuityReference)
        {
            if (actor == null || filmCamera == null) return 0f;

            Vector3 actorPosition = actor.transform.position + Vector3.up;
            Vector3 actorViewport = filmCamera.WorldToViewportPoint(actorPosition);
            float horizontalDirection;

            if (continuityReference != null)
            {
                Vector3 referencePosition = continuityReference.position + Vector3.up * 0.5f;
                Vector3 referenceViewport = filmCamera.WorldToViewportPoint(referencePosition);
                horizontalDirection = actorViewport.x - referenceViewport.x;
            }
            else
            {
                Vector3 facingViewport = filmCamera.WorldToViewportPoint(actorPosition + actor.transform.forward);
                horizontalDirection = facingViewport.x - actorViewport.x;
            }

            if (Mathf.Abs(horizontalDirection) < 0.02f) return 0f;
            return Mathf.Sign(horizontalDirection);
        }

        private bool IsUsingSoftLight(Vector3 targetCenter)
        {
            FilmLightItem[] lights = GetActiveLights();
            foreach (FilmLightItem light in lights)
            {
                if (light == null || !light.IsPoweredOn() || light.spotlight == null) continue;

                bool isSoftLight = light.EquipmentName == "Level 3 Soft Light" || !light.forcesHardLight;
                if (!isSoftLight) continue;

                Vector3 lightPosition = light.spotlight.transform.position;
                Vector3 directionToTarget = (targetCenter - lightPosition).normalized;
                float aim = Vector3.Dot(light.spotlight.transform.forward, directionToTarget);
                float distance = Vector3.Distance(lightPosition, targetCenter);

                if (aim >= 0.45f && distance <= 12f) return true;
            }

            return false;
        }

        private float GradeRange(float value, float idealMinimum, float idealMaximum)
        {
            if (value >= idealMinimum && value <= idealMaximum) return 1f;

            float tolerance = Mathf.Max(idealMaximum - idealMinimum, 0.01f);
            if (value < idealMinimum) return Mathf.Clamp01(1f - (idealMinimum - value) / tolerance);
            return Mathf.Clamp01(1f - (value - idealMaximum) / tolerance);
        }

        private bool IsCampaignTargetBlocked(Vector3 targetCenter, Transform targetRoot)
        {
            Vector3 directionToTarget = targetCenter - filmCamera.transform.position;
            float distanceToTarget = directionToTarget.magnitude;
            int hitCount = Physics.RaycastNonAlloc(filmCamera.transform.position, directionToTarget, trackingHits, distanceToTarget);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = trackingHits[i];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (hit.collider.transform.root == transform.root) continue;
                if (hit.collider.transform.root == targetRoot) continue;
                if (hit.distance < distanceToTarget - 0.15f) return true;
            }

            return false;
        }

        private void CacheTargetSubject()
        {
            targetSubject = FindObjectOfType<RecordableSubject>();
            targetRenderers = targetSubject != null ? targetSubject.GetComponentsInChildren<Renderer>() : null;
        }

        private bool IsSubjectBlocked(Vector3 directionToTarget, float distanceToTarget)
        {
            int hitCount = Physics.RaycastNonAlloc(
                filmCamera.transform.position,
                directionToTarget,
                trackingHits,
                distanceToTarget
            );

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = trackingHits[i];
                if (hit.collider.transform.root == this.transform.root) continue;
                if (hit.collider.isTrigger) continue;
                if (hit.collider.GetComponentInParent<RecordableSubject>() != null) continue;
                if (hit.distance < distanceToTarget - 0.3f) return true;
            }

            return false;
        }

        private FilmLightItem[] GetActiveLights()
        {
            if (activeLights == null || Time.time >= nextLightRefreshTime)
            {
                activeLights = FindObjectsOfType<FilmLightItem>();
                nextLightRefreshTime = Time.time + 1f;
            }

            return activeLights;
        }

        private void InsertSDCard()
        {
            if (isSDCardInserted) return;
            if (TutorialManager.Instance != null && !TutorialManager.Instance.CanInsertSDCard(EquipmentName)) return;

            Player.Interactor.EquipmentInteractor hotbar = GetComponentInParent<Player.Interactor.EquipmentInteractor>();
            if (hotbar != null && hotbar.HasBlankSDCard())
            {
                hotbar.ConsumeBlankSDCard();
                isSDCardInserted = true;

                HotbarUIManager ui = FindObjectOfType<HotbarUIManager>();
                if (ui != null) ui.UpdateGuideText(EquipmentControls);

                if (TutorialManager.Instance != null) TutorialManager.Instance.OnCardInsertedToCamera(EquipmentName);
            }
        }

        private void ToggleRecording(bool forceCancel = false)
        {
            if (isRecording && forceCancel)
            {
                if (pixelRecorder == null) pixelRecorder = FindObjectOfType<TruePixelRecorder>();
                if (pixelRecorder != null) pixelRecorder.CancelRecording();
                isRecording = false;

                if (TutorialManager.Instance != null) TutorialManager.Instance.SetTutorialRecordingLookLock(false);

                UpdateCameraHUD(true);
                return;
            }

            if (!isRecording)
            {
                if (TutorialManager.Instance != null && !TutorialManager.Instance.CanRecord()) return;

                bool requiresCenterFraming = CampaignProgression.GetCurrentLevel() == 1;
                if (targetSubject == null) CacheTargetSubject();
                if (requiresCenterFraming && targetSubject != null && filmCamera != null)
                {
                    Vector3 targetCenter = GetSubjectCenter(targetSubject);
                    Vector3 viewPos = filmCamera.WorldToViewportPoint(targetCenter);

                    bool isCenteredX = viewPos.x >= 0.4f && viewPos.x <= 0.6f;
                    bool isCenteredY = viewPos.y >= 0.4f && viewPos.y <= 0.6f;
                    bool isInFrontOfCamera = viewPos.z > 0;

                    if (!isCenteredX || !isCenteredY || !isInFrontOfCamera)
                    {
                        if (TutorialManager.Instance != null)
                            TutorialManager.Instance.ShowWarning("The subject is not centered! Move your camera to frame it perfectly in the middle.");
                        return;
                    }
                }
            }

            if (isRecording && !forceCancel)
            {
                if (TutorialManager.Instance != null && TutorialManager.Instance.currentStep == TutorialManager.TutorialStep.RecordVideo)
                {
                    float currentDuration = Time.time - recordingStartTime;
                    if (currentDuration < 10f)
                    {
                        TutorialManager.Instance.ShowWarning($"Keep recording! We need at least 10 seconds. You only have {currentDuration:F1}s.");
                        return;
                    }
                }
            }

            if (pixelRecorder == null) pixelRecorder = FindObjectOfType<TruePixelRecorder>();
            if (pixelRecorder == null)
            {
                Debug.LogError("FilmCameraItem: TruePixelRecorder is missing from the camera!");
                return;
            }

            isRecording = !isRecording;
            string generatedFileName = "";
            float finalDuration = 0f;
            float finalGrade = 0f;
            float finalCamGrade = 0f;
            float finalLightGrade = 0f;

            if (isRecording)
            {
                if (TutorialManager.Instance != null) TutorialManager.Instance.SetTutorialRecordingLookLock(true);

                if (!pixelRecorder.StartRecording())
                {
                    isRecording = false;
                    if (TutorialManager.Instance != null) TutorialManager.Instance.SetTutorialRecordingLookLock(false);
                    UpdateCameraHUD(true);
                    return;
                }

                recordingStartTime = Time.time;
                recordingCampaignLevel = CampaignProgression.GetCurrentLevel();
                totalCameraScoreAccumulated = 0f;
                totalLightingScoreAccumulated = 0f;
                framesSampled = 0;
                recordedCoverageAccumulated = 0f;
                recordedScreenDirectionAccumulated = 0f;
                recordedMetadataSamples = 0;
                recordedVisibleSamples = 0;
                recordedSoftLightSamples = 0;
                recordedThreePointSamples = 0;
                recordedActorPose = "";
                nextSampleTime = Time.time + 0.5f;
            }
            else
            {
                if (TutorialManager.Instance != null) TutorialManager.Instance.SetTutorialRecordingLookLock(false);

                generatedFileName = pixelRecorder.StopRecording();
                finalDuration = Time.time - recordingStartTime;

                if (framesSampled > 0)
                {
                    finalCamGrade = totalCameraScoreAccumulated / framesSampled;
                    finalLightGrade = totalLightingScoreAccumulated / framesSampled;
                    finalGrade = finalCamGrade + finalLightGrade;
                }
            }

            GameObject ejectedSDCard = null;
            if (!isRecording) ejectedSDCard = EjectUsedSDCard(generatedFileName, finalDuration, finalGrade, finalCamGrade, finalLightGrade);

            if (TutorialManager.Instance != null && !isRecording && !forceCancel) TutorialManager.Instance.OnRecordingFinished(ejectedSDCard);
        }

        public override void OnDropped(Camera playerCamera)
        {
            if (isRecording) ToggleRecording(true);
            if (isCameraActive)
            {
                isCameraActive = false;
                if (filmCamera != null) filmCamera.gameObject.SetActive(false);
                if (filmUICanvas != null) filmUICanvas.SetActive(false);
                if (ruleOfThirdsGrid != null) ruleOfThirdsGrid.SetActive(false);
                if (TutorialManager.Instance != null) TutorialManager.Instance.OnCameraViewExited(EquipmentName);
                TogglePlayerUI(true);
            }
            base.OnDropped(playerCamera);
        }

        private void OnDisable()
        {
            if (!isRecording) return;

            if (pixelRecorder != null) pixelRecorder.CancelRecording();
            isRecording = false;

            if (TutorialManager.Instance != null) TutorialManager.Instance.SetTutorialRecordingLookLock(false);
        }

        private GameObject EjectUsedSDCard(string savedFileName, float duration, float finalScore, float camScore, float lightScore)
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
                    cardScript.campaignLevel = recordingCampaignLevel;
                    cardScript.shotType = GetRecordedShotType();
                    cardScript.screenDirection = recordedMetadataSamples > 0 ? recordedScreenDirectionAccumulated / recordedMetadataSamples : 0f;
                    cardScript.actorPose = recordedActorPose;
                    cardScript.requiredSubjectsVisible = recordedMetadataSamples > 0 && recordedVisibleSamples == recordedMetadataSamples;
                    cardScript.usedSoftLight = recordedMetadataSamples > 0 && recordedSoftLightSamples >= Mathf.CeilToInt(recordedMetadataSamples * 0.5f);
                    cardScript.hasThreePointRoles = recordedMetadataSamples > 0 && recordedThreePointSamples == recordedMetadataSamples;
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
                return ejectedCard;
            }

            return null;
        }
    }

}
