using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Player.Manager;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

namespace Player.Equipment
{
    [DefaultExecutionOrder(100)]
    public partial class FilmCameraItem : Equipment
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
        [SerializeField] private GameObject ruleOfThirdsGrid;
        [SerializeField] private Image[] ruleOfThirdsIntersections = new Image[4];
        [SerializeField] private TMP_Text ruleOfThirdsInstructionText;
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
        private SDCardItem insertedSDCard;
        private float takeCapacitySeconds;

        private float currentFocusDistance = 5f;
        private float targetFocusDistance = 5f;
        private float focusVelocity = 0f;

        private TruePixelRecorder pixelRecorder;
        private float noiseOffset;
        private Vector3 originalLensRotation;
        private Vector3 originalLocalPos;
        private Vector3 lensLocalPosition;
        private Camera viewfinderAimCamera;
        private Vector3 stableLensPosition;
        private Vector3 lensPositionVelocity;
        private bool viewfinderPoseInitialized;
        private float breathingTime;
        private Coroutine viewTransition;
        private Transform animationParent;
        private Vector3 restingPosition;
        private Quaternion restingRotation;
        private bool hasAnimationPose;
        private Renderer[] hiddenCameraRenderers;
        private UnityEngine.Rendering.ShadowCastingMode[] previousShadowModes;
        private int heldUpdateFrame = -1;
        private bool lensControlsInitialized;
        private float targetFOV;
        private float zoomVelocity;
        private float heightVelocity;
        private float heightAcceleration;

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
            isLevel2Camera = CameraFeatureUnlocks.ManualFocus;
            if (isLevel2Camera)
            {
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
            EquipmentControls = "[LMB] Viewfinder | [G] Drop | [C] Insert/Eject SD | [R] Record | [Scroll] Zoom | [Q/E] Height | [CTRL + WASD] Smooth move | [CTRL + MOUSE] Fine aim";
            ResolvePixelRecorder();
            noiseOffset = Random.Range(0f, 1000f);

            if (filmCamera != null)
            {
                filmCamera.gameObject.SetActive(false);
                originalLensRotation = filmCamera.transform.localEulerAngles;
                originalLocalPos = filmCamera.transform.localPosition;
                lensLocalPosition = originalLocalPos;
            }
            if (filmUICanvas != null) filmUICanvas.SetActive(false);

            if (recTimerText != null) recTimerText.text = "00:00:000";
            if (focusText != null) focusText.text = "FOCUS: 5.0m";

            if (trackingSquare != null) trackingSquare.gameObject.SetActive(false);
        }

        private void ResolvePixelRecorder()
        {
            // Each camera owns its recorder, even while its viewfinder is inactive.
            pixelRecorder = filmCamera != null ? filmCamera.GetComponent<TruePixelRecorder>() : null;
            if (pixelRecorder == null && filmCamera != null)
            {
                foreach (TruePixelRecorder candidate in GetComponentsInChildren<TruePixelRecorder>(true))
                {
                    if (candidate.filmCamera != filmCamera) continue;
                    pixelRecorder = candidate;
                    break;
                }
            }

            if (pixelRecorder != null) pixelRecorder.filmCamera = filmCamera;
        }

#if UNITY_EDITOR
        public void BakeHierarchyUI()
        {
            if(filmUICanvas==null)
                foreach(var canvas in FindObjectsOfType<Canvas>(true))
                    if(canvas.gameObject.scene==gameObject.scene)
                        foreach(var rect in canvas.GetComponentsInChildren<RectTransform>(true))
                            if(rect.name=="Cam Pov") filmUICanvas=rect.gameObject;
            CreateRuleOfThirdsGrid();
        }
#endif

        private void CreateRuleOfThirdsGrid()
        {
            if (filmUICanvas == null || ruleOfThirdsGrid != null) return;

            var authoredGrid=filmUICanvas.transform.Find("Rule Of Thirds Grid");
            if(authoredGrid!=null)
            {
                ruleOfThirdsGrid=authoredGrid.gameObject;
                string[] names={"Lower Left Power Point","Upper Left Power Point","Lower Right Power Point","Upper Right Power Point"};
                for(int i=0;i<4;i++)ruleOfThirdsIntersections[i]=authoredGrid.Find(names[i]).GetComponent<Image>();
                ruleOfThirdsInstructionText=authoredGrid.Find("Rule Of Thirds Lesson Panel/Rule Of Thirds Instruction").GetComponent<TMP_Text>();
                return;
            }
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
                HotbarUIManager hotbar = FindObjectOfType<HotbarUIManager>(true);
                if (hotbar != null) hotbar.gameObject.SetActive(showUI);

                if (CareerManager.Instance != null && CareerManager.Instance.moneyTextHUD != null)
                {
                    CareerManager.Instance.moneyTextHUD.gameObject.SetActive(showUI);
                }
            }
        }

        public override void OnUse(Camera playerCamera)
        {
            if (viewTransition != null) return;
            if (isCameraActive)
            {
                // Keep the lens active if the tutorial still requires a longer take.
                if (isRecording)
                {
                    ToggleRecording();
                    if (isRecording) return;
                }

                BeginViewTransition(playerCamera, false);
                return;
            }

            if (filmCamera == null)
            {
                Debug.LogWarning("This film camera is missing its lens camera reference.", this);
                return;
            }

            if (!isCameraActive && !isSDCardInserted)
            {
                GameFeedback.Show("NO SD CARD\nInsert an SD card [C] before opening the viewfinder.", true);
                HotbarUIManager hotbar = FindObjectOfType<HotbarUIManager>();
                if (hotbar != null)
                {
                    hotbar.UpdateGuideText("<color=red>INSERT SD CARD FIRST (Press C)</color>");
                }
                Debug.LogWarning("Cannot look through camera. Insert an SD Card first!");
                return;
            }

            BeginViewTransition(playerCamera, true);
        }

        private void OpenViewfinder(Camera playerCamera)
        {
            isCameraActive = true;
            viewfinderAimCamera = playerCamera;
            viewfinderPoseInitialized = false;
            breathingTime = 0f;
            MatchPlayerCameraLook(playerCamera);
            HideCameraBody();
            HidePlayerBody(playerCamera);

            RefreshGridVisibility();

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
            OpenDynamicHUD();

            TogglePlayerUI(!isCameraActive);
            TutorialUIManager.Instance?.SetTaskViewfinderVisible(true);
        }

        private void CloseViewfinder()
        {
            settingsOpen = false;
            if (cameraSettingsVolume != null) cameraSettingsVolume.weight = 0;
            if (builtInSettingsVolume != null) builtInSettingsVolume.weight = 0;
            CancelViewTransition();
            RestoreCameraBody();
            RestorePlayerBody();
            viewfinderPoseInitialized = false;
            viewfinderAimCamera = null;
            if (filmCamera != null)
            {
                filmCamera.transform.localPosition = lensLocalPosition;
                filmCamera.transform.localRotation = Quaternion.Euler(originalLensRotation);
            }
            lensControlsInitialized = false;
            bool ownedCameraView = isCameraActive;
            if (ownedCameraView && dynamicHUD != null) dynamicHUD.Hide();
            isCameraActive = false;
            if (filmCamera != null) filmCamera.gameObject.SetActive(false);
            if (ruleOfThirdsGrid != null) ruleOfThirdsGrid.SetActive(false);

            // Unequipping an idle camera must not hide another camera's shared HUD.
            if (!ownedCameraView) return;
            TutorialUIManager.Instance?.SetTaskViewfinderVisible(false);
            if (filmUICanvas != null) filmUICanvas.SetActive(false);
            if (trackingSquare != null) trackingSquare.gameObject.SetActive(false);
            if (TutorialManager.Instance != null) TutorialManager.Instance.OnCameraViewExited(EquipmentName);
            TogglePlayerUI(true);
        }

        public override void OnHeldUpdate(InputManager input)
        {
            if (ProductionKitShop.HasCameraGripLesson(CampaignProgression.GetCurrentLevel()))
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.jKey.wasPressedThisFrame) ProductionKit.MountCamera(this);
                    if (keyboard.kKey.wasPressedThisFrame) ProductionKit.RunDolly(this);
                    if (isCameraActive && keyboard.mKey.wasPressedThisFrame) ProductionExposureMonitor.Toggle(filmCamera);
                }
            }
            if (viewTransition != null) return;
            heldUpdateFrame = Time.frameCount;
            if (input.InsertCard) InsertSDCard();

            if (!isCameraActive || filmCamera == null)
            {
                lensControlsInitialized = false;
                return;
            }
            ReadCameraSettings();
            if (!lensControlsInitialized)
            {
                targetFOV = filmCamera.fieldOfView;
                zoomVelocity = heightVelocity = heightAcceleration = 0f;
                lensControlsInitialized = true;
            }

            if (input.Record)
            {
                if (!isRecording && !isSDCardInserted) { GameFeedback.Show("NO SD CARD\nInsert an SD card [C] before recording.", true); return; }
                ToggleRecording();
            }

            // --- THE FIX: Only allow camera adjustments if NOT recording ---
            if (!isRecording)
            {
                float scroll = input.EquipmentAdjust;
                if (scroll > 0) targetFOV -= zoomSpeed;
                else if (scroll < 0) targetFOV += zoomSpeed;
                targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);
                filmCamera.fieldOfView = Mathf.SmoothDamp(filmCamera.fieldOfView, targetFOV, ref zoomVelocity, 0.12f);

                heightVelocity = Mathf.SmoothDamp(heightVelocity, input.CameraPedestal * pedestalSpeed, ref heightAcceleration, 0.1f);
                float pedestalShift = heightVelocity * Time.deltaTime;
                if (pedestalShift != 0)
                {
                    Vector3 newPos = lensLocalPosition;
                    newPos.y = Mathf.Clamp(newPos.y + pedestalShift, originalLocalPos.y + maxPedestalDown, originalLocalPos.y + maxPedestalUp);
                    lensLocalPosition = newPos;
                }
            }
            else
            {
                if ((Mathf.Abs(input.EquipmentAdjust) > 0.01f || Mathf.Abs(input.CameraPedestal) > 0.01f) &&
                    TutorialManager.Instance != null)
                    TutorialManager.Instance.WarnTutorialRecordingMovement();
                targetFOV = filmCamera.fieldOfView;
                zoomVelocity = heightVelocity = heightAcceleration = 0f;
            }

        }

        private void LateUpdate()
        {
            if (!isCameraActive || filmCamera == null || PauseManager.isPaused) return;
            UpdateStableViewfinderPose();
            RefreshGridVisibility();
            HideCameraBody();
            // Only advance lessons/recording checks when normal equipment input ran this frame.
            if (heldUpdateFrame != Time.frameCount) return;
            HandleSmoothAutoFocus();
            ApplyCameraLook();
            UpdateCameraHUD();
            RefreshDynamicHUD();
            UpdateTrackingSquare();
            UpdateRuleOfThirdsPractice();

            if (isRecording)
            {
                if (Time.time - recordingStartTime >= takeCapacitySeconds || (pixelRecorder != null && pixelRecorder.RecordingLimitReached))
                {
                    ToggleRecording(false, true);
                    GameFeedback.Show("SD CARD FULL\nTake saved. Eject with [C] and delete unwanted clips at the computer to free space.");
                    return;
                }
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
            if (!CameraFeatureUnlocks.ManualFocus || filmCamera == null || GokeLevelManager.Instance == null) return;
            bool independent = GokeLevelManager.Instance.ThirdsIndependentPractice;
            if (settingsOpen) { GokeLevelManager.Instance.OnRuleOfThirdsPracticeUpdated(false); return; }
            if (GridEnabled == independent)
            {
                GokeLevelManager.Instance.OnRuleOfThirdsPracticeUpdated(false);
                if (ruleOfThirdsInstructionText != null)
                    ruleOfThirdsInstructionText.text = independent ? "PRESS F2 — TURN GRID OFF, THEN CLOSE SETTINGS" : "PRESS F2 — TURN GRID ON, THEN CLOSE SETTINGS";
                LayoutThirdsLessonPanel();
                return;
            }

            if (targetSubject == null) CacheTargetSubject();
            if (targetSubject == null)
            {
                GokeLevelManager.Instance.OnRuleOfThirdsPracticeUpdated(false);
                if (ruleOfThirdsInstructionText != null) ruleOfThirdsInstructionText.text = "FIND THE PRACTICE PRODUCT IN THE VIEWFINDER";
                return;
            }

            Vector3 targetCenter = GetSubjectCenter(targetSubject);
            Vector3 viewPosition = filmCamera.WorldToViewportPoint(targetCenter);
            bool hasViewportBounds = TryGetViewportBounds(targetRenderers, out Vector4 viewportBounds);

            float targetX = GokeLevelManager.Instance.ThirdsPracticeIntersection == 0 ? .333f : .666f;
            float horizontalDistance = Mathf.Abs(viewPosition.x - targetX);
            float verticalDistance = Mathf.Abs(viewPosition.y - .333f);
            float subjectCoverage = hasViewportBounds ? Mathf.Max(viewportBounds.z - viewportBounds.x, viewportBounds.w - viewportBounds.y) : 0f;

            bool isOnIntersection = horizontalDistance <= 0.065f && verticalDistance <= 0.085f;
            bool hasUsefulShotSize = subjectCoverage >= 0.2f && subjectCoverage <= 0.58f;
            bool isFullyVisible = hasViewportBounds && GradeViewportVisibility(viewportBounds, 0.03f) >= 0.99f;
            bool hasCorrectComposition = viewPosition.z > 0f && isOnIntersection && hasUsefulShotSize && isFullyVisible;

            UpdateRuleOfThirdsLessonOverlay(viewPosition, subjectCoverage, hasViewportBounds, isFullyVisible, hasCorrectComposition);

            GokeLevelManager.Instance.OnRuleOfThirdsPracticeUpdated(hasCorrectComposition);
            if (hasCorrectComposition && ruleOfThirdsInstructionText != null)
            {
                int progress = Mathf.RoundToInt(GokeLevelManager.Instance.ThirdsFramingProgress * 100f);
                ruleOfThirdsInstructionText.text = "<color=#55FF88>GOOD FRAMING!</color>\nKEEP THIS POSITION — " + progress + "%";
            }
        }

        private void UpdateRuleOfThirdsLessonOverlay(Vector3 viewPosition, float subjectCoverage, bool hasViewportBounds, bool isFullyVisible, bool hasCorrectComposition)
        {
            LayoutThirdsLessonPanel();
            float leftDistance = Mathf.Abs(viewPosition.x - 0.333f);
            float rightDistance = Mathf.Abs(viewPosition.x - 0.666f);
            float bottomDistance = Mathf.Abs(viewPosition.y - 0.333f);
            float topDistance = Mathf.Abs(viewPosition.y - 0.666f);
            int closestIntersection = (leftDistance <= rightDistance ? 0 : 2) + (bottomDistance <= topDistance ? 0 : 1);
            if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.ThirdsPracticeActive)
                closestIntersection = GokeLevelManager.Instance.ThirdsPracticeIntersection;
            float targetX = closestIntersection < 2 ? .333f : .666f;
            float targetY = closestIntersection % 2 == 0 ? .333f : .666f;

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

            if (!isFullyVisible || subjectCoverage > .58f)
            {
                ruleOfThirdsInstructionText.text = "SCROLL DOWN TO ZOOM OUT — KEEP THE WHOLE CAN INSIDE THE FRAME";
                return;
            }
            if (subjectCoverage < .2f)
            {
                ruleOfThirdsInstructionText.text = "SCROLL UP TO ZOOM IN — MAKE THE SUBJECT EASIER TO SEE";
                return;
            }
            if (Mathf.Abs(viewPosition.x - targetX) > 0.065f)
            {
                ruleOfThirdsInstructionText.text = viewPosition.x > targetX
                    ? "MOVE MOUSE RIGHT\nUNTIL THE SUBJECT'S CENTRE REACHES THE GOLD DOT"
                    : "MOVE MOUSE LEFT\nUNTIL THE SUBJECT'S CENTRE REACHES THE GOLD DOT";
                return;
            }

            if (Mathf.Abs(viewPosition.y - targetY) > 0.085f)
            {
                ruleOfThirdsInstructionText.text = viewPosition.y > targetY
                    ? "MOVE MOUSE UP\nUNTIL THE SUBJECT'S CENTRE REACHES THE GOLD DOT"
                    : "MOVE MOUSE DOWN\nUNTIL THE SUBJECT'S CENTRE REACHES THE GOLD DOT";
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

            ruleOfThirdsInstructionText.text = closestIntersection < 2
                ? "<color=#55FF88>HOLD STEADY</color>\nCAN ON LEFT • MESSAGE SPACE ON RIGHT"
                : "<color=#55FF88>HOLD STEADY</color>\nCAN ON RIGHT • MESSAGE SPACE ON LEFT";
        }

        private void LayoutThirdsLessonPanel()
        {
            if (ruleOfThirdsInstructionText == null) return;
            var panel = ruleOfThirdsInstructionText.transform.parent as RectTransform;
            if (panel == null) return;
            // Apply to saved prefab UI as well as dynamically created viewfinders.
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(1f, 0f);
            panel.anchoredPosition = new Vector2(-24f, 100f);
            var parent = panel.parent as RectTransform;
            float width = parent != null ? Mathf.Min(460f, parent.rect.width * .38f) : 460f;
            ruleOfThirdsInstructionText.fontSize = 20f;
            ruleOfThirdsInstructionText.enableAutoSizing = false;
            ruleOfThirdsInstructionText.enableWordWrapping = true;
            ruleOfThirdsInstructionText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            ruleOfThirdsInstructionText.alignment = TextAlignmentOptions.MidlineLeft;
            panel.sizeDelta = new Vector2(width, Mathf.Max(72f,
                ruleOfThirdsInstructionText.GetPreferredValues(ruleOfThirdsInstructionText.text, Mathf.Max(1f, width - 40f), Mathf.Infinity).y + 16f));
        }

        public bool IsCameraViewActive()
        {
            return isCameraActive;
        }

        private void HandleSmoothAutoFocus()
        {
            if (filmCamera == null || ApplyManualFocus()) return;

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

        private void BeginViewTransition(Camera playerCamera, bool opening)
        {
            if (playerCamera == null || transform.parent == null)
            {
                if (opening) OpenViewfinder(playerCamera);
                else CloseViewfinder();
                return;
            }
            if (!opening) CloseViewfinder();
            viewTransition = StartCoroutine(AnimateViewTransition(playerCamera, opening));
        }

        private System.Collections.IEnumerator AnimateViewTransition(Camera playerCamera, bool opening)
        {
            float duration = opening ? .24f : .2f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                holdingRaise = opening ? t : 1f - t;
                yield return null;
                if (!PauseManager.isPaused) elapsed += Time.deltaTime;
            }
            holdingRaise = opening ? 1f : 0f;
            viewTransition = null;
            if (opening) OpenViewfinder(playerCamera);
        }

        private void RestoreHeldPose()
        {
            if (hasAnimationPose && transform.parent == animationParent)
            {
                transform.localPosition = restingPosition;
                transform.localRotation = restingRotation;
            }
            hasAnimationPose = false;
        }

        private void CancelViewTransition()
        {
            holdingRaise = 0f;
            if (viewTransition != null) StopCoroutine(viewTransition);
            viewTransition = null;
            RestoreHeldPose();
        }

        private readonly System.Collections.Generic.Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode> hiddenPlayerBody =
            new System.Collections.Generic.Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode>();

        private void HidePlayerBody(Camera playerCamera)
        {
            if (hiddenPlayerBody.Count != 0) return;
            var owner = playerCamera != null ? playerCamera.GetComponentInParent<global::Player.PlayerController.PlayerController>() : null;
            if (owner == null) owner = GetComponentInParent<global::Player.PlayerController.PlayerController>();
            if (owner == null) owner = FindObjectOfType<global::Player.PlayerController.PlayerController>();
            if (owner == null) return;
            foreach (var part in owner.GetComponentsInChildren<Renderer>(true))
            {
                // Camera housing is already managed separately by HideCameraBody.
                if (part.transform.IsChildOf(transform)) continue;
                hiddenPlayerBody.Add(part, part.shadowCastingMode);
                part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
        }

        private void RestorePlayerBody()
        {
            foreach (var entry in hiddenPlayerBody)
                if (entry.Key != null) entry.Key.shadowCastingMode = entry.Value;
            hiddenPlayerBody.Clear();
        }

        private void HideCameraBody()
        {
            if (hiddenCameraRenderers != null) return;
            hiddenCameraRenderers = GetComponentsInChildren<Renderer>(true);
            previousShadowModes = new UnityEngine.Rendering.ShadowCastingMode[hiddenCameraRenderers.Length];
            for (int i = 0; i < hiddenCameraRenderers.Length; i++)
            {
                previousShadowModes[i] = hiddenCameraRenderers[i].shadowCastingMode;
                hiddenCameraRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
        }

        private void RestoreCameraBody()
        {
            if (hiddenCameraRenderers == null) return;
            for (int i = 0; i < hiddenCameraRenderers.Length; i++)
                if (hiddenCameraRenderers[i] != null)
                    hiddenCameraRenderers[i].shadowCastingMode = previousShadowModes[i];
            hiddenCameraRenderers = null;
            previousShadowModes = null;
        }

        private void UpdateStableViewfinderPose()
        {
            Transform lensParent = filmCamera.transform.parent;
            Vector3 desiredPosition = lensParent != null ? lensParent.TransformPoint(lensLocalPosition) : lensLocalPosition;
            if (!viewfinderPoseInitialized || Vector3.Distance(stableLensPosition, desiredPosition) > 2f)
            {
                stableLensPosition = desiredPosition;
                lensPositionVelocity = Vector3.zero;
                viewfinderPoseInitialized = true;
            }
            stableLensPosition = Vector3.SmoothDamp(stableLensPosition, desiredPosition, ref lensPositionVelocity, 0.06f);
            Quaternion desiredRotation = viewfinderAimCamera != null
                ? viewfinderAimCamera.transform.rotation
                : (lensParent != null ? lensParent.rotation : Quaternion.identity) * Quaternion.Euler(originalLensRotation);
            // Gentle four-second breathing cycle, shared by the viewfinder and recording.
            // Apply to the stable pose, never accumulate offsets or alter the player's aim.
            breathingTime += Time.deltaTime;
            float phase = breathingTime * (Mathf.PI * 2f / 4f);
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(breathingTime));
            float zoomScale = Mathf.Clamp(filmCamera.fieldOfView / 60f, .15f, 1f);
            Vector3 breathOffset = desiredRotation * Vector3.up * (Mathf.Sin(phase) * .0015f * blend * zoomScale);
            Quaternion breathRotation = Quaternion.Euler(
                Mathf.Sin(phase) * .055f * blend * zoomScale,
                Mathf.Sin(phase * .5f) * .025f * blend * zoomScale, 0f);
            filmCamera.transform.SetPositionAndRotation(stableLensPosition + breathOffset, desiredRotation * breathRotation);
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

            if (focusText != null)
            {
                string shotLabel = "";
                if (CampaignProgression.GetCurrentLevel() == 4)
                {
                    // The authored focus label starts hidden. Reuse its compact box for coverage.
                    focusText.gameObject.SetActive(true);
                    focusText.enableAutoSizing = true;
                    focusText.fontSizeMin = 18f;
                    focusText.fontSizeMax = 24f;
                    focusText.enableWordWrapping = false;
                    shotLabel = "FRAME BOTH\n";
                    CacheCampaignTargets(4);
                    if (TryGetViewportBounds(level3ActorRenderers, out Vector4 actorView) &&
                        TryGetViewportBounds(campaignProductRenderers, out Vector4 coffeeView))
                    {
                        int shot = ClassifyShotCoverage(GetViewportCoverage(CombineViewportBounds(actorView, coffeeView)));
                        shotLabel = (shot == 1 ? "WIDE" : shot == 2 ? "MEDIUM" : "CLOSE-UP") + "\n";
                    }
                }
                focusText.text = shotLabel + $"FOCUS: {currentFocusDistance:F1}m";
            }

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
                        if (dynamicHUD == null) trackingSquare.position = screenPos;

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
                            Vector3 screenCorner = dynamicHUD != null ? filmCamera.WorldToViewportPoint(corner) : filmCamera.WorldToScreenPoint(corner);
                            minX = Mathf.Min(minX, screenCorner.x);
                            minY = Mathf.Min(minY, screenCorner.y);
                            maxX = Mathf.Max(maxX, screenCorner.x);
                            maxY = Mathf.Max(maxY, screenCorner.y);
                        }

                        float width = maxX - minX;
                        float height = maxY - minY;
                        if (dynamicHUD != null) dynamicHUD.PlaceTracking(trackingSquare, viewPos, width, height);
                        float padding = 40f;

                        width = Mathf.Clamp(width + padding, 50f, 800f);
                        height = Mathf.Clamp(height + padding, 50f, 800f);

                        if (dynamicHUD == null) trackingSquare.sizeDelta = new Vector2(width, height);

                        Vector3 directionToTarget = targetCenter - filmCamera.transform.position;
                        float distToSub = Vector3.Distance(filmCamera.transform.position, targetCenter);

                        bool isBlocked = IsSubjectBlocked(directionToTarget, distToSub);
                        if (dynamicHUD != null) dynamicHUD.SetTrackingState(isBlocked);

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
                SampleCoffeeCommercialFrame();
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
            float lightingScore = currentLevel <= 2 ? GradeBasicLighting(targetCenter) : Grade3PointLighting(targetCenter);

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

                bool compositionLesson = CampaignProgression.GetCurrentLevel() == 2;
                float intensityScore = compositionLesson ? 10f * Mathf.InverseLerp(0f, 30f, light.intensityPercent) : 10f * Mathf.Clamp01(1f - Mathf.Abs(light.intensityPercent - 45f) / 55f);
                // Goke rewards illuminating the subject, not copying a tutorial tilt.
                float tiltScore = compositionLesson ? 5f * Mathf.InverseLerp(.5f, .95f, Vector3.Dot(light.spotlight.transform.forward, directionToTarget)) : 5f * Mathf.Clamp01(1f - Mathf.Abs(light.GetCurrentTilt() + 5f) / 15f);
                float aimScore = 8f * Mathf.InverseLerp(0.5f, 0.95f, Vector3.Dot(light.spotlight.transform.forward, directionToTarget));
                float placementScore = 4f * Mathf.InverseLerp(-0.1f, 0.8f, Vector3.Dot(cameraArrow, lightArrow));
                float distanceScore = 3f * GradeRange(Vector3.Distance(lightPosition, targetCenter), 1.5f, 6f);

                bestScore = Mathf.Max(bestScore, intensityScore + tiltScore + aimScore + placementScore + distanceScore);
            }

            return Mathf.Clamp(bestScore, 0f, 30f);
        }

        private float Grade3PointLighting(Vector3 targetCenter, bool creativeIntensity = false)
        {
            FilmLightItem[] lights = GetActiveLights();
            FindThreePointLights(targetCenter, lights, out FilmLightItem keyLight, out FilmLightItem fillLight, out FilmLightItem backLight);

            float score = GradeThreePointRole(keyLight, targetCenter, 75f, 50f, false, creativeIntensity);
            score += GradeThreePointRole(fillLight, targetCenter, 40f, 35f, false, creativeIntensity);
            score += GradeThreePointRole(backLight, targetCenter, 60f, 45f, true, creativeIntensity);

            if (keyLight != null && fillLight != null)
            {
                float keySide = Vector3.Dot((keyLight.spotlight.transform.position - targetCenter).normalized, filmCamera.transform.right);
                float fillSide = Vector3.Dot((fillLight.spotlight.transform.position - targetCenter).normalized, filmCamera.transform.right);
                if (keySide * fillSide >= -0.05f) score -= 4f;
                if (creativeIntensity && fillLight.GetCurrentOutput() >= keyLight.GetCurrentOutput()) score -= 3f;
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

        private float GradeThreePointRole(FilmLightItem light, Vector3 targetCenter, float idealIntensity, float intensityTolerance, bool isBackLight, bool creativeIntensity = false)
        {
            if (light == null || light.spotlight == null) return 0f;

            Vector3 lightPosition = light.spotlight.transform.position;
            Vector3 directionToTarget = (targetCenter - lightPosition).normalized;
            Vector3 cameraArrow = (filmCamera.transform.position - targetCenter).normalized;
            Vector3 lightArrow = (lightPosition - targetCenter).normalized;

            float score = 1f;
            score += creativeIntensity ? 3f : 3f * Mathf.Clamp01(1f - Mathf.Abs(light.intensityPercent - idealIntensity) / intensityTolerance);
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

            if (level3Vehicle == null)
            {
                RecordMissingEvidence();
                framesSampled++;
                return;
            }

            if (!TryGetViewportBounds(level3VehicleRenderers, out Vector4 vehicleViewport) ||
                !TryGetWorldBounds(level3VehicleRenderers, out Bounds vehicleBounds))
            {
                RecordMissingEvidence();
                framesSampled++;
                return;
            }

            float cameraScore = GradeLevel3Composition(vehicleViewport);
            bool isVehicleBlocked = IsCampaignTargetBlocked(vehicleBounds.center, level3Vehicle.transform);
            if (isVehicleBlocked) cameraScore -= 15f;

            bool allSubjectsVisible = GradeViewportVisibility(vehicleViewport, 0.05f) >= 0.99f && !isVehicleBlocked;
            RecordProductionEvidence(vehicleViewport, allSubjectsVisible, null, vehicleBounds.center);

            totalCameraScoreAccumulated += Mathf.Clamp(cameraScore, 0f, 70f);
            totalLightingScoreAccumulated += GradeLevel3Lighting(vehicleBounds.center);
            framesSampled++;
        }

        private float GradeLevel3Composition(Vector4 vehicleViewport)
        {
            return LamborminiBrief.Composition(vehicleViewport);
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
                float score = isSoftLight ? 3f : 0f;
                bool creativeVehicleLight = recordingCampaignLevel == 3;
                score += creativeVehicleLight ? 6f * Mathf.InverseLerp(5f, 30f, light.intensityPercent) : 6f * Mathf.Clamp01(1f - Mathf.Abs(light.intensityPercent - 75f) / 45f);
                score += creativeVehicleLight ? 3f : 3f * Mathf.Clamp01(1f - Mathf.Abs(light.GetCurrentTilt() + 10f) / 25f);
                score += 7f * Mathf.InverseLerp(0.45f, 0.95f, Vector3.Dot(light.spotlight.transform.forward, directionToTarget));
                score += 2f * Mathf.InverseLerp(-0.15f, 0.75f, Vector3.Dot(cameraArrow, lightArrow));
                score += 2f * GradeRange(Vector3.Distance(lightPosition, targetCenter), 2f, 8f);
                score += 3f * Mathf.Clamp01(1f - Mathf.Abs(light.GetColorTemperature() - 3200f) / 2200f);
                score += creativeVehicleLight ? 4f * Mathf.InverseLerp(10f, 50f, light.GetDiffusionPercent()) : 4f * Mathf.Clamp01(1f - Mathf.Abs(light.GetDiffusionPercent() - 75f) / 50f);

                if (!isSoftLight) score = Mathf.Min(score * 0.5f, 7f);
                bestScore = Mathf.Max(bestScore, score);
            }

            // Better-Light control is the foundation; distinct roles improve the result.
            // Do not invalidate a readable take merely because it uses fewer lights.
            float roles = Grade3PointLighting(targetCenter, true);
            return Mathf.Clamp(bestScore * .8f + roles * .2f, 0f, 30f);
        }

        private void SampleCoffeeCommercialFrame()
        {
            CampaignProduct cup = null, packaging = null;
            foreach (var product in FindObjectsOfType<CampaignProduct>())
            {
                if (product.campaignLevel != 4) continue;
                if (product.IsCoffeeCup) cup = product; else packaging = product;
            }
            bool cupVisible = false, packageVisible = false, actorVisible = false;
            Vector4 cupView = Vector4.zero, packageView = Vector4.zero, actorView = Vector4.zero;
            if (cup != null) cupVisible = TryGetViewportBounds(cup.GetComponentsInChildren<Renderer>(), out cupView) && GradeViewportVisibility(cupView, .03f) >= .99f && !IsCampaignTargetBlocked(cup.transform.position + Vector3.up * .1f, cup.transform);
            if (packaging != null) packageVisible = TryGetViewportBounds(packaging.GetComponentsInChildren<Renderer>(), out packageView) && GradeViewportVisibility(packageView, .03f) >= .99f && !IsCampaignTargetBlocked(packaging.transform.position + Vector3.up * .1f, packaging.transform);
            var actor = FindObjectOfType<CubeActor>();
            if (actor != null) actorVisible = TryGetViewportBounds(actor.GetComponentsInChildren<Renderer>(), out actorView) && GradeViewportVisibility(actorView, .03f) >= .99f;
            var bot = actor != null ? actor.GetComponent<ActorBot>() : null;
            var stage = FindObjectOfType<DirectorTerminal>();
            bool coffeeSet = stage != null && stage.HasWall() && GameSavePrefs.GetInt("Studio.SelectedInterior", 0) > 0;
            bool usingCoffee = cupVisible && actorVisible && coffeeSet && bot != null &&
                ((bot.CanMixCoffee && actor.GetPoseName() == "Action") || actor.GetPoseName() == "Using Machine");
            string evidence = usingCoffee ? "Coffee Use" : cupVisible && packageVisible ? "Product Overview" : "Incomplete";
            Vector4 viewport = usingCoffee ? CombineViewportBounds(cupView, actorView) : CombineViewportBounds(cupView, packageView);
            bool valid = evidence != "Incomplete";
            RecordProductionEvidence(viewport, valid, null, cup != null ? cup.transform.position : transform.position);
            if (string.IsNullOrEmpty(recordedActorPose)) recordedActorPose = evidence;
            else if (recordedActorPose != evidence) recordedActorPose = "Mixed";
            totalCameraScoreAccumulated += valid ? 70f * GradeRange(GetViewportCoverage(viewport), .25f, .85f) : 20f;
            totalLightingScoreAccumulated += 30f;
            framesSampled++;
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
            // Level 4 assesses storytelling; it does not repeat the Level 3 lighting recipe.
            totalLightingScoreAccumulated += 30f;
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
            if (Time.time < nextLevel3TargetRefreshTime && level3Vehicle != null) return;

            level3Actor = null;
            level3Vehicle = FindObjectOfType<CubeVehicle>();
            level3ActorRenderers = null;
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
                string pose = actor.GetPoseName();
                if (recordingCampaignLevel != 4 || string.IsNullOrEmpty(recordedActorPose)) recordedActorPose = pose;
                else if (recordedActorPose != pose) recordedActorPose = "Mixed";
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
            return ClassifyShotCoverage(averageCoverage);
        }

        internal static int ClassifyShotCoverage(float coverage)
        {
            if (coverage <= 0.45f) return 1;
            if (coverage <= 0.75f) return 2;
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
            if (isSDCardInserted)
            {
                if (isRecording) { GameFeedback.Show("Stop recording before ejecting the SD card."); return; }
                if (TutorialManager.Instance != null && TutorialManager.Instance.currentStep >= TutorialManager.TutorialStep.InsertSDCard &&
                    TutorialManager.Instance.currentStep <= TutorialManager.TutorialStep.RecordVideo)
                { TutorialManager.Instance.ShowWarning("Finish your first take before ejecting this card."); return; }
                EjectUsedSDCard();
                CloseViewfinder();
                return;
            }
            if (TutorialManager.Instance != null && !TutorialManager.Instance.CanInsertSDCard(EquipmentName)) return;

            Player.Interactor.EquipmentInteractor hotbar = GetComponentInParent<Player.Interactor.EquipmentInteractor>();
            if (hotbar != null && hotbar.HasBlankSDCard())
            {
                insertedSDCard = hotbar.TakeSDCard();
                if (insertedSDCard == null) return;
                insertedSDCard.transform.SetParent(transform, true);
                isSDCardInserted = true;

                HotbarUIManager ui = FindObjectOfType<HotbarUIManager>();
                if (ui != null) ui.UpdateEquipmentGuide(EquipmentControls);

                if (TutorialManager.Instance != null) TutorialManager.Instance.OnCardInsertedToCamera(EquipmentName);
            }
            else GameFeedback.Show("NO CARD WITH SPACE\nUse an SD card with room available, or delete clips at the computer.");
        }

        private void ToggleRecording(bool forceCancel = false, bool capacityStop = false)
        {
            if (isRecording && forceCancel)
            {
                if (pixelRecorder == null) ResolvePixelRecorder();
                if (pixelRecorder != null) pixelRecorder.CancelRecording();
                isRecording = false;
                GameplayAudioManager.SetRecording(this, false);

                if (TutorialManager.Instance != null) TutorialManager.Instance.SetTutorialRecordingLookLock(false);

                UpdateCameraHUD(true);
                return;
            }

            if (!isRecording)
            {
                if (insertedSDCard == null || !insertedSDCard.HasSpace)
                { GameFeedback.Show("SD CARD FULL\nDelete unwanted clips at the computer or insert another card."); return; }
                takeCapacitySeconds = insertedSDCard.RemainingSeconds;
                if (TutorialManager.Instance != null && TutorialManager.Instance.currentStep == TutorialManager.TutorialStep.RecordVideo && takeCapacitySeconds < 10f)
                { GameFeedback.Show("This lesson needs a 10-second take. Free space on this card or insert another one."); return; }
                if (Level3Manager.Instance != null && Level3Manager.Instance.RecordingBlockedByPractice)
                {
                    GameFeedback.Show("RECORDING LOCKED: Finish the practice lesson first. Use the viewfinder to rehearse without recording.");
                    return;
                }
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

            if (isRecording && !forceCancel && !capacityStop)
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

            if (pixelRecorder == null) ResolvePixelRecorder();
            if (pixelRecorder == null)
            {
                Debug.LogError("FilmCameraItem: TruePixelRecorder is missing from the camera!");
                return;
            }

            isRecording = !isRecording;
            string generatedFileName = "";
            float finalDuration = 0f;
            float finalCamGrade = 0f;
            float finalLightGrade = 0f;

            if (isRecording)
            {
                if (TutorialManager.Instance != null) TutorialManager.Instance.SetTutorialRecordingLookLock(true);
                foreach (var actor in FindObjectsOfType<CubeActor>()) actor.BeginTake();

                if (!pixelRecorder.StartRecording(takeCapacitySeconds))
                {
                    isRecording = false;
                    if (TutorialManager.Instance != null) TutorialManager.Instance.SetTutorialRecordingLookLock(false);
                    UpdateCameraHUD(true);
                    return;
                }

                recordingStartTime = Time.time;
                GameplayAudioManager.SetRecording(this, true);
                GameplayAudioManager.PlayRecordingCue("Start Recording");
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
                GameplayAudioManager.SetRecording(this, false);
                GameplayAudioManager.PlayRecordingCue("Stop Recording");
                finalDuration = pixelRecorder.LastRecordedDuration;

                if (framesSampled > 0)
                {
                    finalCamGrade = totalCameraScoreAccumulated / framesSampled;
                    finalLightGrade = totalLightingScoreAccumulated / framesSampled;
                }
            }

            GameObject ejectedSDCard = null;
            if (!isRecording)
            {
                if (string.IsNullOrEmpty(generatedFileName)) return;
                var data = new FootageData {
                    fileName = generatedFileName, duration = finalDuration, camScore = finalCamGrade, lightScore = finalLightGrade,
                    campaignLevel = recordingCampaignLevel, shotType = GetRecordedShotType(),
                    screenDirection = recordedMetadataSamples > 0 ? recordedScreenDirectionAccumulated / recordedMetadataSamples : 0f,
                    actorPose = recordedActorPose, requiredSubjectsVisible = recordedMetadataSamples > 0 && recordedVisibleSamples == recordedMetadataSamples,
                    usedSoftLight = recordedMetadataSamples > 0 && recordedSoftLightSamples >= Mathf.CeilToInt(recordedMetadataSamples * .5f),
                    hasThreePointRoles = recordedMetadataSamples > 0 && recordedThreePointSamples == recordedMetadataSamples
                };
                if (!insertedSDCard.AddRecording(data))
                { GameFeedback.Show("Could not add this take to the SD card. Its recording file was kept."); return; }
                PlayerAnalytics.TakeRecorded(recordingCampaignLevel, finalDuration);
                CampaignLevelManager.Instance?.OnCoffeeTakeRecorded(insertedSDCard);
                // The first tutorial still demonstrates handing a recorded card
                // to the computer. Ordinary recording keeps it mounted for more takes.
                if (TutorialManager.Instance != null && TutorialManager.Instance.currentStep == TutorialManager.TutorialStep.RecordVideo)
                    ejectedSDCard = EjectUsedSDCard();
                else GameFeedback.Show($"TAKE SAVED\n{insertedSDCard.GetRecordings().Count} clips · {insertedSDCard.UsedSeconds:0.#}/60 seconds used");
            }

            if (TutorialManager.Instance != null && !isRecording && !forceCancel) TutorialManager.Instance.OnRecordingFinished(ejectedSDCard);
        }

        public override void OnDropped(Camera playerCamera)
        {
            ProductionKit.DetachCamera(this);
            if (isRecording) ToggleRecording(true);
            CloseViewfinder();
            base.OnDropped(playerCamera);
        }

        private void OnDisable()
        {
            if (isRecording) ToggleRecording(true);
            CloseViewfinder();
        }

        private void OnDestroy()
        {
            RestorePlayerBody();
            GameplayAudioManager.SetRecording(this, false);
            if (dynamicHUD != null) Destroy(dynamicHUD.gameObject);
            ReleaseCameraSettings();
            // The grid is parented to the shared HUD, not this equipment object.
            if (ruleOfThirdsGrid != null) Destroy(ruleOfThirdsGrid);
        }

        private GameObject EjectUsedSDCard()
        {
            if (insertedSDCard == null) return null;
            isSDCardInserted = false;
            if (insertedSDCard != null)
            {
                Transform spawnLoc = ejectPoint != null ? ejectPoint : transform;
                SDCardItem cardScript = insertedSDCard;
                GameObject ejectedCard = cardScript.gameObject;
                insertedSDCard = null;
                cardScript.OnDropped(null);
                ejectedCard.transform.SetPositionAndRotation(spawnLoc.position, spawnLoc.rotation);
                MeshRenderer renderer = ejectedCard.GetComponentInChildren<MeshRenderer>();
                if (renderer != null && cardScript.isUsedCard)
                {
                    MaterialPropertyBlock cardProperties = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(cardProperties);
                    cardProperties.SetColor("_Color", Color.red);
                    renderer.SetPropertyBlock(cardProperties);
                }

                // The authored SD-card collider lives on its mesh child.
                Collider col = ejectedCard.GetComponentInChildren<Collider>(true);
                if (col == null)
                {
                    MeshFilter cardMesh = ejectedCard.GetComponentInChildren<MeshFilter>(true);
                    if (cardMesh != null && cardMesh.sharedMesh != null)
                    {
                        BoxCollider cardCollider = cardMesh.gameObject.AddComponent<BoxCollider>();
                        cardCollider.center = cardMesh.sharedMesh.bounds.center;
                        cardCollider.size = cardMesh.sharedMesh.bounds.size;
                    }
                    else
                    {
                        BoxCollider cardCollider = ejectedCard.AddComponent<BoxCollider>();
                        cardCollider.size = new Vector3(0.08f, 0.095f, 0.005f);
                    }
                }
                Rigidbody rb = ejectedCard.GetComponent<Rigidbody>();
                if (rb == null) rb = ejectedCard.AddComponent<Rigidbody>();

                // A tiny thrown card can tunnel through the set or land behind the camera.
                // Release it just above a nearby surface with solid, reusable card physics.
                var player = GetComponentInParent<Player.PlayerController.PlayerController>();
                Vector3 origin = player != null ? player.transform.position : transform.position;
                Vector3 forward = player != null ? player.transform.forward : transform.forward;
                forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
                Vector3 rayOrigin = origin + forward * 0.9f + Vector3.up * 1.2f;
                Vector3 landing = origin + forward * 0.9f;
                RaycastHit[] surfaces = Physics.RaycastAll(rayOrigin, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(surfaces, (a, b) => a.distance.CompareTo(b.distance));
                foreach (RaycastHit surface in surfaces)
                {
                    if (surface.normal.y < 0.7f || surface.transform.IsChildOf(ejectedCard.transform) ||
                        surface.transform.IsChildOf(transform) ||
                        (player != null && surface.transform.IsChildOf(player.transform))) continue;
                    landing = surface.point;
                    break;
                }
                ejectedCard.SetActive(true);
                ejectedCard.transform.rotation = Quaternion.identity;
                ejectedCard.transform.position = landing;
                Renderer[] cardRenderers = ejectedCard.GetComponentsInChildren<Renderer>();
                if (cardRenderers.Length > 0)
                {
                    Bounds bounds = cardRenderers[0].bounds;
                    foreach (Renderer part in cardRenderers) bounds.Encapsulate(part.bounds);
                    ejectedCard.transform.position += Vector3.up * (landing.y - bounds.min.y + 0.03f);
                }
                foreach (Collider cardCollider in ejectedCard.GetComponentsInChildren<Collider>(true))
                    cardCollider.enabled = true;
                cardScript.OnDropped(null);
                var inventory = GetComponentInParent<Player.Interactor.EquipmentInteractor>();
                if (inventory == null) inventory = FindObjectOfType<Player.Interactor.EquipmentInteractor>();
                if (cardScript != null && inventory != null) inventory.StoreEjectedCard(cardScript);
                return ejectedCard;
            }

            return null;
        }
    }

}
