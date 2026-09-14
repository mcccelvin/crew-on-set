using PlayerPrefs = GameSavePrefs;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

[System.Serializable]
public struct TutorialTarget
{
    [Tooltip("The exact word used in the script (e.g., 'shop', 'director', 'camera')")]
    public string targetName;
    [Tooltip("Drag the actual 3D object from the scene here")]
    public Transform targetTransform;
}

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    public const float TutorialBlueTarget = 150f;
    private const float TutorialBlueSnapTolerance = 10f;

    [Header("Spawning Setup")]
    public Transform stageSpawnPoint;

    [Header("Skip Tutorial Starter Gear")]
    public GameObject cameraPrefab;
    public GameObject lightPrefab;
    public GameObject sdCardPrefab;
    public Transform deliveryZone;

    [Header("UI References")]
    public TextMeshProUGUI spacePromptText;
    public GameObject firstContractPanel;

    [Header("Cinematic Title Cards")]
    public CanvasGroup preProductionTitleCard;
    public CanvasGroup productionTitleCard;

    [Header("Objective Line Guide")]
    public LineRenderer objectiveLine;
    public float lineHeightOffset = 0.5f;

    [Tooltip("Add your targets here so the script knows exactly where to draw the line!")]
    public TutorialTarget[] availableTargets;

    [Header("--- UI Highlight Targets ---")]
    public RectTransform acceptContractButtonRect;
    public RectTransform spawnWallButtonRect;
    public RectTransform redColorSliderRect;
    public RectTransform cubePropCardRect;
    public RectTransform flowerPropCardRect;
    public RectTransform shopLightAddToCartBtnRect;
    public RectTransform shopCameraAddToCartBtnRect;
    public RectTransform shopSDCardAddToCartBtnRect;
    public RectTransform shopCheckoutBtnRect;

    [Header("--- Computer UI Highlights ---")]
    public RectTransform compFolderRect;
    public RectTransform compClipCardRect;
    public RectTransform compPlayBtnRect;
    public RectTransform compBackBtnRect;
    public RectTransform compEditorAppRect;
    public RectTransform compConfirmBtnRect;

    [Header("--- Physical Stage Targets ---")]
    public GameObject stageWalkTriggerCircle;
    public GameObject cubePlacementTarget;
    public GameObject cameraWalkTriggerCircle;

    private Transform playerTransform;
    private Transform lineTarget;
    private Player.Manager.InputManager pInput;
    private DirectorTerminal directorTerminal;
    private GameObject tutorialCube;
    private GameObject tutorialFlower;
    private Transform tutorialUsedSDCard;
    private TutorialGlowTarget tutorialUsedSDCardGlow;

    public enum TutorialStep
    {
        Intro, WaitForPrompt, LearnMovement, GameExplanation, OfferFirstContract, SetTrainingObjectAndMoney,
        ShowPreProductionTitle, ExplainPreProduction,
        BuildStageWall, ExplainDirectorTablet,
        Tablet_AddWall, Tablet_SelectWall, Tablet_PaintWall,
        Tablet_SpawnCube, Tablet_MoveCube, Tablet_PaintCube,
        Tablet_SpawnProp, Tablet_MovePropToCube,
        TabletPracticeFinished, FreePlayDirectorTablet,

        BuyLight_WalkToShop, BuyLight_AddToCart, BuyLight_Checkout, BuyLight_CloseShop,

        PickUpLight, WalkToStageWithLight, TurnOnLight,
        PracticeLight_Intensity, AdjustLight_Intensity,
        PracticeLight_Tilt, AdjustLight_Tilt,
        DropLight,

        ShowProductionTitle, ExplainProduction,

        BuyCamera_WalkToShop, BuyCamera_AddToCart, BuySDCard_AddToCart, BuyCamera_Checkout, BuyCamera_CloseShop,

        PickUpCamera, PickUpSDCard, InsertSDCard, WalkToStageWithCamera,

        EquipCameraView, PracticeCameraZoom, PracticeCameraPedestal, FrameSubject, RecordVideo,

        PickUpUsedSDCard, InsertToComputer, OpenComputer, ExplainComputerEditor,

        OpenRecordingsFolder, ClickVideoClip, PlayVideoClip, ClickBack, ClickEditorApp, ClickConfirmEditor,

        Complete, PostEditComplete, OfferLevel1, Level1Accepted
    }

    public TutorialStep currentStep;
    private bool isTransitioning = false;
    private bool isTaskPhaseActive = false;
    private bool isTutorialRecordingLookLocked = false;
    private Coroutine warningCoroutine;
    private bool restoreTaskPanelAfterWarning = false;

    private float lastWarningTime = 0f;

    private bool moved = false, jumped = false, sprinted = false;
    private bool tabletOpened = false, wallAdded = false, wallColorChanged = false;
    private bool cubeSpawned = false, cubeMoved = false, cubePainted = false;
    private bool propSpawned = false, flowerOnCube = false;
    private bool cameraViewEntered = false, cameraZoomed = false, cameraPedestalMoved = false, subjectFramed = false;
    private float cameraPracticeElapsed, cameraPracticeIdle;
    private bool practicedPositive, practicedNegative;

    private float spacebarCooldown = 0f;
    private bool wasJumpHeld = false;
    private bool isTutorialInitialized = false;

    [Header("Game Explanation Dialogue")]
    private string[] explanationPages = new string[]
    {
        "We make short commercials here. You'll build the set, shoot the product, then bring the footage together in the edit.",
        "Before we touch any gear, we read the <color=red>contract</color>. That's our brief: what the client wants, and what we need to deliver.",
        "Keep that brief close. Meeting it earns your grade and payment, and I'll walk you through this first job."
    };
    private int currentExplanationPage = 0;
    private bool isLevel1Retry = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        LockPlayer();

        Player.PlayerController.PlayerController pCtrl = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (pCtrl != null) playerTransform = pCtrl.transform;
        pInput = FindObjectOfType<Player.Manager.InputManager>();
        directorTerminal = FindObjectOfType<DirectorTerminal>();

        if (objectiveLine != null)
        {
            objectiveLine.positionCount = 2;
            objectiveLine.enabled = false;
        }

        if (stageWalkTriggerCircle != null) stageWalkTriggerCircle.SetActive(false);
        if (cubePlacementTarget != null) cubePlacementTarget.SetActive(false);
        if (cameraWalkTriggerCircle != null) cameraWalkTriggerCircle.SetActive(false);

        int progress = PlayerPrefs.GetInt("TutorialProgress", 0);
        int currentLevel = CampaignProgression.GetCurrentLevel();

        if (currentLevel == 1 && PlayerPrefs.GetInt("FlowerContractGraded", 0) == 0)
        {
            PlayerPrefs.SetInt("AlmanacUnlocked", 0);
            PlayerPrefs.Save();
        }

        if (currentLevel == 1 && PlayerPrefs.GetInt("Level1RetryActive", 0) == 1)
        {
            StartCoroutine(StartLevel1RetryWithDelay());
            return;
        }

        if (currentLevel >= 4)
        {
            currentStep = TutorialStep.Level1Accepted;
            FinishTutorialInitialization();
            StartCampaignLevel(currentLevel);
            return;
        }

        if (currentLevel == 3)
        {
            currentStep = TutorialStep.Level1Accepted;
            FinishTutorialInitialization();
            StartLevel3();
            return;
        }

        if (currentLevel == 2)
        {
            currentStep = TutorialStep.Level1Accepted;
            FinishTutorialInitialization();
            StartGokeLevel();
            return;
        }
        else if (progress == 1) { StartCoroutine(StartPostEditTutorial()); return; }

        StartCoroutine(StartTutorialWithDelay());
    }

    public void DisableForDevTesting()
    {
        StopAllCoroutines();
        currentStep = TutorialStep.Level1Accepted;
        isTaskPhaseActive = false;
        isTransitioning = false;
        isTutorialRecordingLookLocked = false;
        if (firstContractPanel != null) firstContractPanel.SetActive(false);
        if (spacePromptText != null) spacePromptText.gameObject.SetActive(false);
        if (preProductionTitleCard != null) preProductionTitleCard.gameObject.SetActive(false);
        if (productionTitleCard != null) productionTitleCard.gameObject.SetActive(false);
        if (stageWalkTriggerCircle != null) stageWalkTriggerCircle.SetActive(false);
        if (cameraWalkTriggerCircle != null) cameraWalkTriggerCircle.SetActive(false);
        if (cubePlacementTarget != null) cubePlacementTarget.SetActive(false);
        PointLineAt("");
        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.HideTasks();
            TutorialUIManager.Instance.ClearDynamicGlows();
        }
        if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();
        if ((PauseManager.isPaused || Cursor.lockState == CursorLockMode.Locked) &&
            (AlmanacManager.Instance == null || !AlmanacManager.Instance.IsOpen()) &&
            (ContractUIManager.Instance == null || !ContractUIManager.Instance.IsContractUIOpen()))
        {
            UnfreezePlayerMovement();
            if (!PauseManager.isPaused) Cursor.visible = false;
        }
        enabled = false;
    }

    private void Update()
    {// --- ADD THIS TO FIX THE POINT C CAMERA TRIGGER ---
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (!isTutorialInitialized) return;

        bool spaceHeld = keyboard != null && keyboard.spaceKey.isPressed;
        bool sprintHeld = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        bool movementHeld = keyboard != null &&
                            (keyboard.wKey.isPressed || keyboard.aKey.isPressed || keyboard.sKey.isPressed || keyboard.dKey.isPressed ||
                             keyboard.upArrowKey.isPressed || keyboard.leftArrowKey.isPressed || keyboard.downArrowKey.isPressed || keyboard.rightArrowKey.isPressed);

        if (PauseManager.isPaused)
        {
            wasJumpHeld = (pInput != null && pInput.Jump) || spaceHeld;
            return;
        }

        if (AlmanacManager.Instance != null && AlmanacManager.Instance.IsOpen())
        {
            wasJumpHeld = (pInput != null && pInput.Jump) || spaceHeld;
            return;
        }

        if (currentStep == TutorialStep.WalkToStageWithCamera && isTaskPhaseActive)
        {
            if (cameraWalkTriggerCircle != null && playerTransform != null)
            {
                // Note: I increased the distance check to 1.5f because Point C looks quite large in your screenshot. 
                // This makes it easier to trigger without finding the exact dead-center pixel.
                if (Vector3.Distance(playerTransform.position, cameraWalkTriggerCircle.transform.position) < 1.5f)
                {
                    TutorialUIManager.Instance.MarkTaskComplete(0);
                    TutorialUIManager.Instance.SetDynamicGlow("pointc", false);

                    // Snap the player to the center of the circle
                    playerTransform.position = new Vector3(
                        cameraWalkTriggerCircle.transform.position.x,
                        playerTransform.position.y,
                        cameraWalkTriggerCircle.transform.position.z
                    );

                    FreezePlayerMovement();

                    // Move to the next step
                    StartCoroutine(TransitionToNextStep(TutorialStep.EquipCameraView, true));
                }
            }
        }
        // --------------------------------------------------
        // --- ADD THIS BLOCK TO FIX THE LINE RENDERER ---
        if (objectiveLine != null && objectiveLine.enabled && lineTarget != null && playerTransform != null)
        {
            // Point 0: The Player
            objectiveLine.SetPosition(0, GuidedPracticeLesson.GuideEndpoint(playerTransform, true));

            // Point 1: The Target Objective
            objectiveLine.SetPosition(1, GuidedPracticeLesson.GuideEndpoint(lineTarget, false));
        }
        // -----------------------------------------------
        UpdatePlacementChecks();

        bool canAdvanceCampaignDialogue = CanAdvanceCampaignDialogue();
        bool bossDialogueReady = TutorialUIManager.Instance == null || TutorialUIManager.Instance.CanAdvanceBossDialogue();

        if (spacePromptText != null)
        {
            bool canShowPrompt = !isTaskPhaseActive && !isTransitioning && (Time.unscaledTime >= spacebarCooldown) && bossDialogueReady && (currentStep != TutorialStep.WaitForPrompt) && canAdvanceCampaignDialogue;
            spacePromptText.gameObject.SetActive(canShowPrompt);
        }

        bool isJumpCurrentlyHeld = (pInput != null && pInput.Jump) || spaceHeld;

        bool jumpJustPressed = (pInput != null && pInput.Continue) || (isJumpCurrentlyHeld && !wasJumpHeld);
        wasJumpHeld = isJumpCurrentlyHeld;

        if (jumpJustPressed && !isTransitioning)
        {
            if (Time.unscaledTime >= spacebarCooldown && bossDialogueReady)
            {
                if (currentStep == TutorialStep.GameExplanation || currentStep == TutorialStep.ExplainComputerEditor)
                {
                    currentExplanationPage++;
                    if (currentStep == TutorialStep.GameExplanation)
                    {
                        if (currentExplanationPage < explanationPages.Length) UpdateBossDialogue();
                        else StartCoroutine(TransitionToNextStep(TutorialStep.OfferFirstContract, false));
                    }
                    else if (currentStep == TutorialStep.ExplainComputerEditor)
                    {
                        StartCoroutine(TransitionToNextStep(TutorialStep.OpenRecordingsFolder, false));
                    }
                }
                else if (!isTaskPhaseActive && canAdvanceCampaignDialogue)
                {
                    AdvanceDialogue();
                }
            }
        }

        bool contextPanelPressed = (pInput != null && pInput.ContextPanel) ||
                                   (keyboard != null && keyboard.tabKey.wasPressedThisFrame);

        if (contextPanelPressed && !isTransitioning && currentStep == TutorialStep.WaitForPrompt)
        {
            StartCoroutine(TransitionToNextStep(TutorialStep.OfferFirstContract, false));
        }

        // --- UPGRADED LEARN MOVEMENT BLOCK ---
        if (currentStep == TutorialStep.LearnMovement && isTaskPhaseActive)
        {
            // 1. Check Movement (WASD or Custom Input)
            if (!moved && ((pInput != null && pInput.Move.sqrMagnitude > 0.01f) || movementHeld))
            {
                moved = true;
                TutorialUIManager.Instance.MarkTaskComplete(0);
            }

            // 2. Check Jump (Spacebar or Custom Input)
            bool jumpPressedThisFrame = (pInput != null && pInput.JumpPressedThisFrame) ||
                                        (pInput == null && keyboard != null && keyboard.spaceKey.wasPressedThisFrame);

            if (!jumped && jumpPressedThisFrame)
            {
                jumped = true;
                TutorialUIManager.Instance.MarkTaskComplete(1);
            }

            // 3. Check Sprint (Shift or Custom Input)
            if (!sprinted && ((pInput != null && pInput.Run) || sprintHeld))
            {
                sprinted = true;
                TutorialUIManager.Instance.MarkTaskComplete(2);
            }

            // Move to next step once all 3 are done
            if (moved && jumped && sprinted && !isTransitioning)
            {
                StartCoroutine(DelayedMovementTransition());
            }
        }
        // -------------------------------------

        if (currentStep == TutorialStep.PracticeCameraZoom && isTaskPhaseActive && !cameraZoomed)
        {
            float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            if (CameraPracticeReady(scroll > 0, scroll < 0)) { cameraZoomed = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.PracticeCameraPedestal, true)); }
        }

        if (currentStep == TutorialStep.PracticeCameraPedestal && isTaskPhaseActive && !cameraPedestalMoved)
        {
            if (CameraPracticeReady(keyboard != null && keyboard.qKey.isPressed, keyboard != null && keyboard.eKey.isPressed)) { cameraPedestalMoved = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.FrameSubject, true)); }
        }

        if (currentStep == TutorialStep.WalkToStageWithLight && isTaskPhaseActive)
        {
            if (stageWalkTriggerCircle != null && playerTransform != null)
            {
                if (Vector3.Distance(playerTransform.position, stageWalkTriggerCircle.transform.position) < 0.8f)
                {
                    TutorialUIManager.Instance.MarkTaskComplete(0);
                    TutorialUIManager.Instance.SetDynamicGlow("pointA", false);

                    playerTransform.position = new Vector3(
                        stageWalkTriggerCircle.transform.position.x,
                        playerTransform.position.y,
                        stageWalkTriggerCircle.transform.position.z
                    );

                    FreezePlayerMovement();

                    StartCoroutine(TransitionToNextStep(TutorialStep.TurnOnLight, true));
                }
            }
        }
        if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
        {
            CheatCompleteCurrentStep();
        }
    }

    private void CheatCompleteCurrentStep()
    {
        // 1. Block if we are currently loading the next screen
        if (isTransitioning) return;

        // 2. If it's just dialogue, skip to the next dialogue/task
        if (!isTaskPhaseActive)
        {
            AdvanceDialogue();
            return;
        }

        // 3. Mark all possible UI checkboxes as complete instantly
        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            TutorialUIManager.Instance.MarkTaskComplete(1);
            TutorialUIManager.Instance.MarkTaskComplete(2);
        }

        // 4. Force the transition and clean up UI glows based on the exact step
        switch (currentStep)
        {
            case TutorialStep.LearnMovement:
                moved = jumped = sprinted = true;
                StartCoroutine(TransitionToNextStep(TutorialStep.GameExplanation, true));
                break;
            case TutorialStep.OfferFirstContract:
                if (firstContractPanel != null) firstContractPanel.SetActive(false);
                StartCoroutine(TransitionToNextStep(TutorialStep.SetTrainingObjectAndMoney, true));
                break;
            case TutorialStep.BuildStageWall:
                tabletOpened = true;
                TutorialUIManager.Instance.SetDynamicGlow("director", false);
                StartCoroutine(TransitionToNextStep(TutorialStep.ExplainDirectorTablet, true));
                break;
            case TutorialStep.Tablet_AddWall: wallAdded = true; StartCoroutine(TransitionToNextStep(TutorialStep.Tablet_SelectWall, true)); break;
            case TutorialStep.Tablet_SelectWall: StartCoroutine(TransitionToNextStep(TutorialStep.Tablet_PaintWall, true)); break;
            case TutorialStep.Tablet_PaintWall: wallColorChanged = true; StartCoroutine(TransitionToNextStep(TutorialStep.Tablet_SpawnCube, true)); break;
            case TutorialStep.Tablet_SpawnCube: cubeSpawned = true; StartCoroutine(TransitionToNextStep(TutorialStep.Tablet_MoveCube, true)); break;
            case TutorialStep.Tablet_MoveCube:
                cubeMoved = true;
                if (cubePlacementTarget != null) cubePlacementTarget.SetActive(false);
                StartCoroutine(TransitionToNextStep(TutorialStep.Tablet_PaintCube, true));
                break;
            case TutorialStep.Tablet_PaintCube: cubePainted = true; StartCoroutine(TransitionToNextStep(TutorialStep.Tablet_SpawnProp, true)); break;
            case TutorialStep.Tablet_SpawnProp: propSpawned = true; StartCoroutine(TransitionToNextStep(TutorialStep.Tablet_MovePropToCube, true)); break;
            case TutorialStep.Tablet_MovePropToCube: flowerOnCube = true; StartCoroutine(TransitionToNextStep(TutorialStep.TabletPracticeFinished, true)); break;
            case TutorialStep.FreePlayDirectorTablet: StartCoroutine(TransitionToNextStep(TutorialStep.BuyLight_WalkToShop, false)); break;

            case TutorialStep.BuyLight_WalkToShop: StartCoroutine(TransitionToNextStep(TutorialStep.BuyLight_AddToCart, true)); break;
            case TutorialStep.BuyLight_AddToCart: StartCoroutine(TransitionToNextStep(TutorialStep.BuyLight_Checkout, true)); break;
            case TutorialStep.BuyLight_Checkout: TutorialUIManager.Instance.SetDynamicGlow("shop", false); StartCoroutine(TransitionToNextStep(TutorialStep.BuyLight_CloseShop, true)); break;
            case TutorialStep.BuyLight_CloseShop: StartCoroutine(TransitionToNextStep(TutorialStep.PickUpLight, true)); break;
            case TutorialStep.PickUpLight: StartCoroutine(TransitionToNextStep(TutorialStep.WalkToStageWithLight, true)); break;
            case TutorialStep.WalkToStageWithLight:
                if (playerTransform != null && stageWalkTriggerCircle != null)
                {
                    playerTransform.position = new Vector3(stageWalkTriggerCircle.transform.position.x, playerTransform.position.y, stageWalkTriggerCircle.transform.position.z);
                    FreezePlayerMovement();
                }
                StartCoroutine(TransitionToNextStep(TutorialStep.TurnOnLight, true));
                break;
            case TutorialStep.TurnOnLight: StartCoroutine(TransitionToNextStep(TutorialStep.PracticeLight_Intensity, true)); break;
            case TutorialStep.PracticeLight_Intensity: StartCoroutine(TransitionToNextStep(TutorialStep.AdjustLight_Intensity, true)); break;
            case TutorialStep.AdjustLight_Intensity: StartCoroutine(TransitionToNextStep(TutorialStep.PracticeLight_Tilt, true)); break;
            case TutorialStep.PracticeLight_Tilt: StartCoroutine(TransitionToNextStep(TutorialStep.AdjustLight_Tilt, true)); break;
            case TutorialStep.AdjustLight_Tilt: StartCoroutine(TransitionToNextStep(TutorialStep.DropLight, true)); break;
            case TutorialStep.DropLight: UnfreezePlayerMovement(); StartCoroutine(TransitionToNextStep(TutorialStep.ShowProductionTitle, true)); break;

            case TutorialStep.BuyCamera_WalkToShop: StartCoroutine(TransitionToNextStep(TutorialStep.BuyCamera_AddToCart, true)); break;
            case TutorialStep.BuyCamera_AddToCart: StartCoroutine(TransitionToNextStep(TutorialStep.BuySDCard_AddToCart, true)); break;
            case TutorialStep.BuySDCard_AddToCart: StartCoroutine(TransitionToNextStep(TutorialStep.BuyCamera_Checkout, true)); break;
            case TutorialStep.BuyCamera_Checkout: TutorialUIManager.Instance.SetDynamicGlow("shop", false); StartCoroutine(TransitionToNextStep(TutorialStep.BuyCamera_CloseShop, true)); break;
            case TutorialStep.BuyCamera_CloseShop: StartCoroutine(TransitionToNextStep(TutorialStep.PickUpCamera, true)); break;
            case TutorialStep.PickUpCamera: StartCoroutine(TransitionToNextStep(TutorialStep.PickUpSDCard, true)); break;
            case TutorialStep.PickUpSDCard: StartCoroutine(TransitionToNextStep(TutorialStep.InsertSDCard, true)); break;
            case TutorialStep.InsertSDCard: TutorialUIManager.Instance.SetDynamicGlow("camera", false); StartCoroutine(TransitionToNextStep(TutorialStep.WalkToStageWithCamera, true)); break;
            case TutorialStep.WalkToStageWithCamera:
                if (playerTransform != null && cameraWalkTriggerCircle != null)
                {
                    playerTransform.position = new Vector3(cameraWalkTriggerCircle.transform.position.x, playerTransform.position.y, cameraWalkTriggerCircle.transform.position.z);
                    FreezePlayerMovement();
                }
                StartCoroutine(TransitionToNextStep(TutorialStep.EquipCameraView, true));
                break;
            case TutorialStep.EquipCameraView: cameraViewEntered = true; StartCoroutine(TransitionToNextStep(TutorialStep.PracticeCameraZoom, true)); break;
            case TutorialStep.PracticeCameraZoom: cameraZoomed = true; StartCoroutine(TransitionToNextStep(TutorialStep.PracticeCameraPedestal, true)); break;
            case TutorialStep.PracticeCameraPedestal: cameraPedestalMoved = true; StartCoroutine(TransitionToNextStep(TutorialStep.FrameSubject, true)); break;
            case TutorialStep.FrameSubject: subjectFramed = true; StartCoroutine(TransitionToNextStep(TutorialStep.RecordVideo, true)); break;
            case TutorialStep.RecordVideo: TutorialUIManager.Instance.SetDynamicGlow("camera", false); StartCoroutine(TransitionToNextStep(TutorialStep.PickUpUsedSDCard, true)); break;

            case TutorialStep.PickUpUsedSDCard: TutorialUIManager.Instance.SetDynamicGlow("sd", false); StartCoroutine(TransitionToNextStep(TutorialStep.InsertToComputer, true)); break;
            case TutorialStep.InsertToComputer: TutorialUIManager.Instance.SetDynamicGlow("computer", false); StartCoroutine(TransitionToNextStep(TutorialStep.OpenComputer, true)); break;
            case TutorialStep.OpenComputer: TutorialUIManager.Instance.SetDynamicGlow("computer", false); StartCoroutine(TransitionToNextStep(TutorialStep.ExplainComputerEditor, true)); break;

            case TutorialStep.OpenRecordingsFolder: StartCoroutine(TransitionToNextStep(TutorialStep.ClickVideoClip, true)); break;
            case TutorialStep.ClickVideoClip: StartCoroutine(TransitionToNextStep(TutorialStep.PlayVideoClip, true)); break;
            case TutorialStep.PlayVideoClip: StartCoroutine(TransitionToNextStep(TutorialStep.ClickBack, true)); break;
            case TutorialStep.ClickBack: StartCoroutine(TransitionToNextStep(TutorialStep.ClickEditorApp, true)); break;
            case TutorialStep.ClickEditorApp: StartCoroutine(TransitionToNextStep(TutorialStep.ClickConfirmEditor, true)); break;
            case TutorialStep.ClickConfirmEditor: StartCoroutine(TransitionToNextStep(TutorialStep.Complete, true)); break;
        }
    }

    private IEnumerator DelayedMovementTransition()
    {
        isTransitioning = true;
        isTaskPhaseActive = false;
        yield return new WaitForSeconds(3f);
        isTransitioning = false;
        StartCoroutine(TransitionToNextStep(TutorialStep.GameExplanation, true));
    }

    private void UpdatePlacementChecks()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed) return;

        bool isPlacingProp = directorTerminal != null && directorTerminal.IsPlacingProp();

        if (currentStep == TutorialStep.Tablet_SpawnCube && isTaskPhaseActive && !cubeSpawned)
        {
            if (tutorialCube != null)
            {
                cubeSpawned = true;
                TutorialUIManager.Instance.MarkTaskComplete(0);
                StartCoroutine(TransitionToNextStep(TutorialStep.Tablet_MoveCube, true));
            }
        }

        if (currentStep == TutorialStep.Tablet_MoveCube && isTaskPhaseActive && !cubeMoved && !isPlacingProp)
        {
            if (tutorialCube != null && cubePlacementTarget != null)
            {
                Vector3 cubePos = tutorialCube.transform.position;
                Vector3 targetPos = cubePlacementTarget.transform.position;
                float hDist = Vector2.Distance(new Vector2(cubePos.x, cubePos.z), new Vector2(targetPos.x, targetPos.z));

                if (IsCubeOnMarker(tutorialCube))
                {
                    cubeMoved = true;
                    cubePlacementTarget.SetActive(false);
                    TutorialUIManager.Instance.MarkTaskComplete(0);
                    StartCoroutine(TransitionToNextStep(TutorialStep.Tablet_PaintCube, true));
                }
            }
        }

        if (currentStep == TutorialStep.Tablet_SpawnProp && isTaskPhaseActive && !propSpawned)
        {
            if (tutorialFlower != null)
            {
                propSpawned = true;
                TutorialUIManager.Instance.MarkTaskComplete(0);
                StartCoroutine(TransitionToNextStep(TutorialStep.Tablet_MovePropToCube, true));
            }
        }

        if (currentStep == TutorialStep.Tablet_MovePropToCube && isTaskPhaseActive && !flowerOnCube && !isPlacingProp)
        {
            if (tutorialFlower != null && tutorialCube != null)
            {
                Vector3 flowerPos = tutorialFlower.transform.position;
                Vector3 cubePos = tutorialCube.transform.position;

                float hDist = Vector2.Distance(new Vector2(flowerPos.x, flowerPos.z), new Vector2(cubePos.x, cubePos.z));
                float vDist = Mathf.Abs(flowerPos.y - cubePos.y);

                if (IsCubeOnMarker(tutorialCube) && IsFlowerOnCube(tutorialFlower))
                {
                    flowerOnCube = true;
                    TutorialUIManager.Instance.SetDynamicGlow("pointB", false);
                    if (cubePlacementTarget != null) cubePlacementTarget.SetActive(false);
                    TutorialUIManager.Instance.MarkTaskComplete(0);
                    StartCoroutine(TransitionToNextStep(TutorialStep.TabletPracticeFinished, true));
                }
            }
        }
    }

    private bool CameraPracticeReady(bool positive, bool negative)
    {
        var interactor = FindObjectOfType<Player.Interactor.EquipmentInteractor>();
        var camera = interactor != null ? interactor.GetHeldItem() as Player.Equipment.FilmCameraItem : null;
        if (camera == null || !camera.IsCameraViewActive()) return false;
        cameraPracticeElapsed += Time.deltaTime;
        practicedPositive |= positive;
        practicedNegative |= negative;
        cameraPracticeIdle = positive || negative ? 0f : cameraPracticeIdle + Time.deltaTime;
        return practicedPositive && practicedNegative && DevTutorialBypass.PracticeDelayComplete(cameraPracticeElapsed, 10f) && DevTutorialBypass.PracticeDelayComplete(cameraPracticeIdle, 1.5f);
    }

    private void ResetCameraPractice()
    {
        cameraPracticeElapsed = cameraPracticeIdle = 0f;
        practicedPositive = practicedNegative = false;
    }

    public void OnPropPlaced(GameObject placedObject)
    {
        if (placedObject == null) return;

        string propName = placedObject.name.ToLower();
        if (propName.Contains("cube")) tutorialCube = placedObject;
        else if (propName.Contains("flower") || propName.Contains("floral")) tutorialFlower = placedObject;
    }

    private bool TryPropBounds(GameObject prop, out Bounds bounds)
    {
        bounds = default;
        if (prop == null) return false;
        Renderer[] renderers = prop.GetComponentsInChildren<Renderer>();
        bool found = false;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled || renderer is LineRenderer) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found;
    }

    private bool IsCubeOnMarker(GameObject cube)
    {
        if (cubePlacementTarget == null || !TryPropBounds(cube, out Bounds bounds)) return false;
        Vector3 target = cubePlacementTarget.transform.position;
        return Vector2.Distance(new Vector2(bounds.center.x, bounds.center.z), new Vector2(target.x, target.z)) <= 0.6f
            && Mathf.Abs(bounds.min.y - target.y) <= 0.35f;
    }

    private bool IsFlowerOnCube(GameObject flower)
    {
        if (!TryPropBounds(tutorialCube, out Bounds cube) || !TryPropBounds(flower, out Bounds item)) return false;
        return item.center.x >= cube.min.x && item.center.x <= cube.max.x
            && item.center.z >= cube.min.z && item.center.z <= cube.max.z
            && Mathf.Abs(item.min.y - cube.max.y) <= 0.25f;
    }

    public bool CanPlaceTutorialProp(GameObject prop)
    {
        if (currentStep < TutorialStep.Tablet_MoveCube || currentStep > TutorialStep.TabletPracticeFinished) return true;
        if (prop == tutorialCube && !IsCubeOnMarker(prop))
        {
            ShowWarning("Move the cube onto the center marker, then click to place it.");
            return false;
        }
        if (prop == tutorialFlower && (!IsCubeOnMarker(tutorialCube) || !IsFlowerOnCube(prop)))
        {
            ShowWarning("Place the flower on top of the cube, then click.");
            return false;
        }
        return true;
    }

    public void OnPropPickedFromUI(GameObject pickedObject)
    {
        if (pickedObject == null || !isTaskPhaseActive) return;

        string propName = pickedObject.name.ToLower();

        if (currentStep == TutorialStep.Tablet_SpawnCube && propName.Contains("cube"))
        {
            tutorialCube = pickedObject;
            cubeSpawned = true;
            cubeMoved = false;
            TutorialUIManager.Instance.MarkTaskComplete(0);
            if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();

            currentStep = TutorialStep.Tablet_MoveCube;
            TutorialUIManager.Instance.SetupTasks(new string[] { "Move the Cube over the center marker, then click to place it" });
            TutorialUIManager.Instance.SetDynamicGlow("pointB", true);
            if (cubePlacementTarget != null) cubePlacementTarget.SetActive(true);
            return;
        }

        if (currentStep == TutorialStep.Tablet_SpawnProp && (propName.Contains("flower") || propName.Contains("floral")))
        {
            tutorialFlower = pickedObject;
            propSpawned = true;
            flowerOnCube = false;
            TutorialUIManager.Instance.MarkTaskComplete(0);
            if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();

            currentStep = TutorialStep.Tablet_MovePropToCube;
            TutorialUIManager.Instance.SetupTasks(new string[] { "Move the Flower over the pink Cube, then click to place it" });
            TutorialUIManager.Instance.SetDynamicGlow("pointB", true);
            if (cubePlacementTarget != null) cubePlacementTarget.SetActive(true);
        }
    }

    public void RegisterDirectorPropCard(string propName, RectTransform propCard)
    {
        if (string.IsNullOrEmpty(propName) || propCard == null) return;

        string lowerName = propName.ToLower();
        if (lowerName.Contains("cube")) cubePropCardRect = propCard;
        else if (lowerName.Contains("flower") || lowerName.Contains("floral")) flowerPropCardRect = propCard;
    }

    public void PointLineAt(string targetIdentifier)
    {
        PointLineAtIdentifier(targetIdentifier);
    }

    public void PointLineAtTransform(Transform target)
    {
        lineTarget = target;
        if (objectiveLine != null) { objectiveLine.useWorldSpace = true; objectiveLine.positionCount = 2; objectiveLine.enabled = target != null; }
    }

    private void PointLineAtIdentifier(string targetIdentifier)
    {
        if (objectiveLine == null) return;

        if (string.IsNullOrEmpty(targetIdentifier))
        {
            objectiveLine.enabled = false;
            lineTarget = null;
            return;
        }

        foreach (TutorialTarget target in availableTargets)
        {
            if (target.targetName.ToLower() == targetIdentifier.ToLower() && target.targetTransform != null)
            {
                lineTarget = target.targetTransform;
                objectiveLine.enabled = true;
                return;
            }
        }

        if (targetIdentifier.ToLower() == "sd")
        {
            FindTutorialUsedSDCard();
            if (tutorialUsedSDCard != null)
            {
                lineTarget = tutorialUsedSDCard;
                objectiveLine.enabled = true;
                return;
            }
        }

        if (targetIdentifier.ToLower() == "computer")
        {
            ComputerStation comp = FindObjectOfType<ComputerStation>();
            if (comp != null) { lineTarget = comp.ejectPoint != null ? comp.ejectPoint : comp.transform; objectiveLine.enabled = true; return; }
        }

        Debug.LogWarning("Objective Line: Could not find '" + targetIdentifier + "'! Check your spelling or your Inspector list.");
        objectiveLine.enabled = false;
    }

    private void FindTutorialUsedSDCard()
    {
        if (tutorialUsedSDCard != null) return;

        Player.Equipment.SDCardItem[] cards = FindObjectsOfType<Player.Equipment.SDCardItem>();
        float closestDistance = float.MaxValue;

        foreach (Player.Equipment.SDCardItem card in cards)
        {
            if (card == null || !card.isUsedCard) continue;

            float distance = playerTransform != null ? Vector3.Distance(playerTransform.position, card.transform.position) : 0f;
            if (distance >= closestDistance) continue;

            closestDistance = distance;
            tutorialUsedSDCard = card.transform;
            tutorialUsedSDCardGlow = card.GetComponent<TutorialGlowTarget>();
            if (tutorialUsedSDCardGlow == null) tutorialUsedSDCardGlow = card.GetComponentInChildren<TutorialGlowTarget>();
        }
    }

    private void SetUsedSDCardGlow(bool state)
    {
        FindTutorialUsedSDCard();

        if (tutorialUsedSDCardGlow != null) TutorialUIManager.Instance.SetDynamicGlow(tutorialUsedSDCardGlow, state);
        else TutorialUIManager.Instance.SetDynamicGlow("sd", state);
    }

    private void LockPlayer()
    {
        Player.PlayerController.PlayerController p = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (p != null)
        {
            p.canMove = false;
            p.canLook = false;
        }
    }

    public void FreezePlayerMovement()
    {
        Player.PlayerController.PlayerController p = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (p != null) p.canMove = false;

    }

    public void UnfreezePlayerMovement()
    {
        Player.PlayerController.PlayerController p = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (p != null)
        {
            p.canMove = true;
            p.canLook = true;
        }
    }

    private IEnumerator UnlockPlayerAfterFrame()
    {
        yield return new WaitUntil(() => Keyboard.current == null || !Keyboard.current.spaceKey.isPressed);
        yield return null;

        Player.PlayerController.PlayerController p = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (p != null)
        {
            p.canLook = true;

            if (currentStep == TutorialStep.TurnOnLight ||
                currentStep == TutorialStep.PracticeLight_Intensity ||
                currentStep == TutorialStep.AdjustLight_Intensity ||
                currentStep == TutorialStep.PracticeLight_Tilt ||
                currentStep == TutorialStep.AdjustLight_Tilt ||
                currentStep == TutorialStep.DropLight ||
                currentStep == TutorialStep.EquipCameraView ||
                currentStep == TutorialStep.PracticeCameraZoom ||
                currentStep == TutorialStep.PracticeCameraPedestal ||
                currentStep == TutorialStep.FrameSubject ||
                currentStep == TutorialStep.RecordVideo)
            {
                p.canMove = false;
            }
            else
            {
                p.canMove = true;
            }
        }
    }

    private IEnumerator FadeTitleCardSequence(CanvasGroup cg, TutorialStep nextStep)
    {
        isTransitioning = true;
        TutorialUIManager.Instance.HideBossDialogue();

        cg.alpha = 0f;
        cg.gameObject.SetActive(true);

        float speed = 1.5f;
        while (cg.alpha < 1f)
        {
            cg.alpha += Time.deltaTime * speed;
            yield return null;
        }
        cg.alpha = 1f;

        yield return new WaitForSeconds(3f);

        while (cg.alpha > 0f)
        {
            cg.alpha -= Time.deltaTime * speed;
            yield return null;
        }
        cg.alpha = 0f;
        cg.gameObject.SetActive(false);

        currentStep = nextStep;
        isTransitioning = false;
        UpdateBossDialogue();
    }

    private IEnumerator StartTutorialWithDelay()
    {
        yield return new WaitForSecondsRealtime(1f);
        currentStep = TutorialStep.Intro;
        FinishTutorialInitialization();
        UpdateBossDialogue();
    }

    private IEnumerator StartLevel1RetryWithDelay()
    {
        yield return new WaitForSecondsRealtime(1f);
        isLevel1Retry = true;
        currentStep = TutorialStep.SetTrainingObjectAndMoney;
        FinishTutorialInitialization();
        UpdateBossDialogue();
    }

    private IEnumerator StartPostEditTutorial()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        if (AlmanacManager.Instance != null) AlmanacManager.Instance.UnlockLevel1Knowledge();
        currentStep = TutorialStep.PostEditComplete;
        FinishTutorialInitialization();
        UpdateBossDialogue();
    }

    private void FinishTutorialInitialization()
    {
        Keyboard keyboard = Keyboard.current;
        bool spaceHeld = keyboard != null && keyboard.spaceKey.isPressed;
        wasJumpHeld = (pInput != null && pInput.Jump) || spaceHeld;
        spacebarCooldown = Time.unscaledTime + 0.2f;
        isTutorialInitialized = true;
    }

    public void ShowWarning(string warningMessage)
    {
        if (Time.time < lastWarningTime + 1.5f) return;
        lastWarningTime = Time.time;

        RememberTaskPanelForWarning();

        if (warningCoroutine != null) StopCoroutine(warningCoroutine);
        warningCoroutine = StartCoroutine(ShowBossWarning(warningMessage));
    }

    public void ShowTimedWarning(string warningMessage, float duration)
    {
        lastWarningTime = Time.time;

        RememberTaskPanelForWarning();

        if (warningCoroutine != null) StopCoroutine(warningCoroutine);
        warningCoroutine = StartCoroutine(ShowTimedBossWarning(warningMessage, duration));
    }

    private IEnumerator ShowBossWarning(string warningMessage)
    {
        TutorialUIManager.Instance.ShowBossDialogue(warningMessage, TutorialUIManager.Instance.poseBoss, false, false);
        float readableDuration = TutorialUIManager.Instance.GetBossDialogueReadyDelay() + 0.5f;
        yield return new WaitForSecondsRealtime(Mathf.Max(1.5f, readableDuration));
        TutorialUIManager.Instance.HideBossDialogue();
        RestoreTaskPanelAfterWarning();
        warningCoroutine = null;
    }

    private IEnumerator ShowTimedBossWarning(string warningMessage, float duration)
    {
        TutorialUIManager.Instance.ShowBossDialogue(warningMessage, TutorialUIManager.Instance.poseBoss, false, false);
        float readableDuration = TutorialUIManager.Instance.GetBossDialogueReadyDelay() + 0.35f;
        yield return new WaitForSecondsRealtime(Mathf.Max(duration, readableDuration));
        TutorialUIManager.Instance.HideBossDialogue();
        RestoreTaskPanelAfterWarning();
        warningCoroutine = null;
    }

    private void RememberTaskPanelForWarning()
    {
        if (TutorialUIManager.Instance != null && TutorialUIManager.Instance.taskPanel != null && TutorialUIManager.Instance.taskPanel.activeSelf)
        {
            restoreTaskPanelAfterWarning = true;
        }
    }

    private void RestoreTaskPanelAfterWarning()
    {
        if (!restoreTaskPanelAfterWarning) return;

        restoreTaskPanelAfterWarning = false;
        if (TutorialUIManager.Instance != null && TutorialUIManager.Instance.taskPanel != null)
        {
            TutorialUIManager.Instance.taskPanel.SetActive(true);
        }
    }

    public void AdvanceDialogue()
    {
        if (isTransitioning) return;

        if (currentStep == TutorialStep.PostEditComplete) { StartCoroutine(TransitionToNextStep(TutorialStep.OfferLevel1, false)); return; }

        if (currentStep == TutorialStep.OfferLevel1)
        {
            currentStep = TutorialStep.Level1Accepted;
            CampaignProgression.SetCurrentLevel(2);
            StartGokeLevel();
            return;
        }

        if (currentStep == TutorialStep.Level1Accepted)
        {
            if (CampaignLevelManager.Instance != null)
            {
                CampaignLevelManager.Instance.CloseBriefing();
                return;
            }

            if (Level3Manager.Instance != null)
            {
                Level3Manager.Instance.CloseBriefing();
                return;
            }

            if (GokeLevelManager.Instance == null)
            {
                StartGokeLevel();
                return;
            }

            GokeLevelManager.Instance.CloseBriefing();
            return;
        }

        if (currentStep == TutorialStep.Intro) { StartCoroutine(TransitionToNextStep(TutorialStep.WaitForPrompt, false)); return; }
        if (currentStep == TutorialStep.WaitForPrompt) { StartCoroutine(TransitionToNextStep(TutorialStep.LearnMovement, false)); return; }

        if (currentStep == TutorialStep.SetTrainingObjectAndMoney)
        {
            int budgetToAdd = ProductionEconomy.StartingBudget;

            if (isLevel1Retry)
            {
                int savedMoney = PlayerPrefs.GetInt("PlayerMoney", 0);
                budgetToAdd = Mathf.Max(0, ProductionEconomy.StartingBudget - savedMoney);
            }
            else if (PlayerPrefs.GetInt("Level1StartingBudgetGranted", 0) == 1)
            {
                budgetToAdd = 0;
            }

            if (CareerManager.Instance != null && budgetToAdd > 0)
            {
                CareerManager.Instance.AddMoney(budgetToAdd);
            }
            else if (budgetToAdd > 0)
            {
                int savedMoney = PlayerPrefs.GetInt("PlayerMoney", 0);
                PlayerPrefs.SetInt("PlayerMoney", savedMoney + budgetToAdd);
            }

            PlayerPrefs.SetInt("Level1StartingBudgetGranted", 1);
            PlayerPrefs.Save();
            StartCoroutine(TransitionToNextStep(TutorialStep.ShowPreProductionTitle, false));
            return;
        }

        if (currentStep == TutorialStep.ExplainPreProduction)
        {
            StartCoroutine(TransitionToNextStep(TutorialStep.BuildStageWall, false));
            return;
        }

        if (currentStep == TutorialStep.ExplainDirectorTablet) { StartCoroutine(TransitionToNextStep(TutorialStep.Tablet_AddWall, false)); return; }
        if (currentStep == TutorialStep.TabletPracticeFinished) { currentStep = TutorialStep.FreePlayDirectorTablet; StartTaskPhase(); return; }

        if (currentStep == TutorialStep.ExplainProduction)
        {
            StartCoroutine(TransitionToNextStep(TutorialStep.BuyCamera_WalkToShop, false));
            return;
        }

        if (currentStep == TutorialStep.Complete) { StartCoroutine(TransitionToNextStep(TutorialStep.OfferLevel1, false)); return; }

        StartTaskPhase();
    }

    private void StartGokeLevel()
    {
        GokeLevelManager gokeLevelManager = GetComponent<GokeLevelManager>();
        if (gokeLevelManager == null) gokeLevelManager = gameObject.AddComponent<GokeLevelManager>();
        gokeLevelManager.BeginLevel(this);
    }

    private void StartLevel3()
    {
        Level3Manager level3Manager = GetComponent<Level3Manager>();
        if (level3Manager == null) level3Manager = gameObject.AddComponent<Level3Manager>();
        level3Manager.BeginLevel(this);
    }

    private void StartCampaignLevel(int level)
    {
        CampaignLevelManager campaignLevelManager = GetComponent<CampaignLevelManager>();
        if (campaignLevelManager == null) campaignLevelManager = gameObject.AddComponent<CampaignLevelManager>();
        campaignLevelManager.BeginLevel(this, level);
    }

    private bool CanAdvanceCampaignDialogue()
    {
        if (currentStep != TutorialStep.Level1Accepted) return true;
        if (CampaignLevelManager.Instance != null) return CampaignLevelManager.Instance.IsBriefingActive();
        if (Level3Manager.Instance != null) return Level3Manager.Instance.IsBriefingActive();
        return GokeLevelManager.Instance == null || GokeLevelManager.Instance.IsBriefingActive();
    }

    private void StartTaskPhase()
    {
        TutorialUIManager.Instance.HideBossDialogue();

        if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();

        if (currentStep == TutorialStep.OfferFirstContract ||
            currentStep == TutorialStep.Tablet_AddWall || currentStep == TutorialStep.Tablet_SelectWall || currentStep == TutorialStep.Tablet_PaintWall ||
            currentStep == TutorialStep.Tablet_SpawnCube || currentStep == TutorialStep.Tablet_MoveCube || currentStep == TutorialStep.Tablet_PaintCube ||
            currentStep == TutorialStep.Tablet_SpawnProp || currentStep == TutorialStep.Tablet_MovePropToCube ||
            currentStep == TutorialStep.FreePlayDirectorTablet ||
            currentStep == TutorialStep.BuyLight_AddToCart || currentStep == TutorialStep.BuyLight_Checkout ||
            currentStep == TutorialStep.BuyCamera_AddToCart || currentStep == TutorialStep.BuySDCard_AddToCart || currentStep == TutorialStep.BuyCamera_Checkout ||
            currentStep == TutorialStep.OpenRecordingsFolder || currentStep == TutorialStep.ClickVideoClip ||
            currentStep == TutorialStep.PlayVideoClip || currentStep == TutorialStep.ClickBack ||
            currentStep == TutorialStep.ClickEditorApp || currentStep == TutorialStep.ClickConfirmEditor)
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            LockPlayer();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            StartCoroutine(UnlockPlayerAfterFrame());
        }

        isTaskPhaseActive = true;

        switch (currentStep)
        {
            case TutorialStep.LearnMovement: TutorialUIManager.Instance.SetupTasks(new string[] { "Use <color=red>[W,A,S,D]</color> to move", "Press <color=red>[Space]</color> to jump", "Hold <color=red>[Shift]</color> to sprint" }); moved = jumped = sprinted = false; break;

            case TutorialStep.OfferFirstContract:
                TutorialUIManager.Instance.SetupTasks(new string[] { "Select the flower contract, read the brief, then close it to begin" });
                if (firstContractPanel != null) firstContractPanel.SetActive(false);
                if (ContractUIManager.Instance == null) new GameObject("Contract UI Manager").AddComponent<ContractUIManager>();
                ContractUIManager.Instance.ShowFlowerContract(OnFirstContractAccepted);
                break;

            case TutorialStep.BuildStageWall:
                TutorialUIManager.Instance.SetupTasks(new string[] { "Look at the Tablet and press <color=red>[E]</color>" });
                tabletOpened = false;
                TutorialUIManager.Instance.SetDynamicGlow("director", true);
                PointLineAt("director");
                break;

            case TutorialStep.Tablet_AddWall:
                TutorialUIManager.Instance.SetupTasks(new string[] { "Click <color=red>'Add Wall'</color> to build stage" });
                wallAdded = false;
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(spawnWallButtonRect);
                break;

            case TutorialStep.Tablet_SelectWall:
                TutorialUIManager.Instance.SetupTasks(new string[] { "Click the Stage Backdrop to select it" });
                TutorialUIManager.Instance.SetDynamicGlow("stage", true);

                break;

            case TutorialStep.Tablet_PaintWall:
                TutorialUIManager.Instance.SetDynamicGlow("director", true);
                TutorialUIManager.Instance.SetDynamicGlow("stage", false);
                TutorialUIManager.Instance.SetupTasks(new string[] { "Set <color=red>Red</color> to ~255", "Set <color=green>Green</color> to 0", "Set <color=blue>Blue</color> to 150" });
                wallColorChanged = false;
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(redColorSliderRect);
                break;

            case TutorialStep.Tablet_SpawnCube:
                TutorialUIManager.Instance.SetupTasks(new string[] { "Click the 'Cube' button to spawn a table top" });
                cubeSpawned = false;
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(cubePropCardRect);
                break;

            case TutorialStep.Tablet_MoveCube:
                TutorialUIManager.Instance.SetupTasks(new string[] { "Move the Cube over the center marker, then click to place it" });
                TutorialUIManager.Instance.SetDynamicGlow("pointB", true);

                cubeMoved = false;

                if (cubePlacementTarget != null) cubePlacementTarget.SetActive(true);
                break;

            case TutorialStep.Tablet_PaintCube:
                TutorialUIManager.Instance.SetDynamicGlow("pointB", false);
                TutorialUIManager.Instance.SetupTasks(new string[] { "Set <color=red>Red</color> to ~255", "Set <color=green>Green</color> to 0", "Set <color=blue>Blue</color> to 150" });
                cubePainted = false;
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(redColorSliderRect);
                break;

            case TutorialStep.Tablet_SpawnProp:
                TutorialUIManager.Instance.SetupTasks(new string[] { "Click the 'Flower' Prop button to spawn it" });
                propSpawned = false;
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(flowerPropCardRect);
                break;

            case TutorialStep.Tablet_MovePropToCube:
                TutorialUIManager.Instance.SetupTasks(new string[] { "Move the Flower over the pink Cube, then click to place it" });
                flowerOnCube = false;

                TutorialUIManager.Instance.SetDynamicGlow("pointB", true);
                if (cubePlacementTarget != null) cubePlacementTarget.SetActive(true);
                break;

            case TutorialStep.FreePlayDirectorTablet:
                TutorialUIManager.Instance.SetupTasks(new string[] { "Press <color=red>[E]</color> to close tablet" });
                break;

            case TutorialStep.BuyLight_WalkToShop:
                TutorialUIManager.Instance.SetupTasks(new string[] { "Walk to the Equipments Shop and press <color=red>[E]</color>" });
                TutorialUIManager.Instance.SetDynamicGlow("shop", true);
                PointLineAt("shop");
                break;

            case TutorialStep.BuyLight_AddToCart:
                TutorialUIManager.Instance.SetupTasks(new string[] { "Click 'Add To Cart' under the Stage Light" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(shopLightAddToCartBtnRect);
                break;

            case TutorialStep.BuyLight_Checkout:
                TutorialUIManager.Instance.SetupTasks(new string[] { "Click 'Buy' or 'Checkout' to pay" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(shopCheckoutBtnRect);
                break;

            case TutorialStep.BuyLight_CloseShop:
                TutorialUIManager.Instance.SetupTasks(new string[] { "Press <color=red>[E]</color> to close the terminal" });
                break;

            case TutorialStep.PickUpLight:
                TutorialUIManager.Instance.SetupTasks(new string[] { "Walk up to the dropped Stage Light and press <color=red>[E]</color> to pick it up" });
                TutorialUIManager.Instance.SetDynamicGlow("light", true);
                break;

            case TutorialStep.WalkToStageWithLight:
                TutorialUIManager.Instance.SetupTasks(new string[] { "Walk over to the Target Circle on the Pink Stage" });
                TutorialUIManager.Instance.SetDynamicGlow("light", false);
                if (stageWalkTriggerCircle != null) stageWalkTriggerCircle.SetActive(true);
                TutorialUIManager.Instance.SetDynamicGlow("pointA", true);
                PointLineAt("pointA");
                break;

            case TutorialStep.TurnOnLight:
                TutorialUIManager.Instance.SetupTasks(new string[] { "Aim at the stage and click <color=red>[Left Mouse Button]</color> to turn it on" });
                TutorialUIManager.Instance.SetDynamicGlow("pointA", false);
                PointLineAt("");
                break;

            case TutorialStep.PracticeLight_Intensity:
                TutorialUIManager.Instance.SetupTasks(new string[] { "Practice adjusting Intensity <color=red>[Scroll Wheel]</color> (5s)" });
                StartCoroutine(PracticeTimer(5f, TutorialStep.AdjustLight_Intensity));
                break;

            case TutorialStep.AdjustLight_Intensity:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Use <color=red>[Scroll Wheel]</color> to set Intensity to 45%" });
                break;

            case TutorialStep.PracticeLight_Tilt:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Practice adjusting Tilt <color=red>[Up/Down Arrows]</color> (5s)" });
                StartCoroutine(PracticeTimer(5f, TutorialStep.AdjustLight_Tilt));
                break;

            case TutorialStep.AdjustLight_Tilt:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Use <color=red>[Up/Down Arrows]</color> to set Tilt to -5°" });
                break;

            case TutorialStep.DropLight:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Press <color=red>[G]</color> to drop the Light" });
                break;

            case TutorialStep.BuyCamera_WalkToShop:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Walk to the Equipments Shop and press <color=red>[E]</color>" });
                TutorialUIManager.Instance.SetDynamicGlow("shop", true);
                PointLineAt("shop");
                break;

            case TutorialStep.BuyCamera_AddToCart:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click 'Add To Cart' under the Film Camera" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(shopCameraAddToCartBtnRect);
                break;

            case TutorialStep.BuySDCard_AddToCart:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click 'Add To Cart' under the SD Card" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(shopSDCardAddToCartBtnRect);
                break;

            case TutorialStep.BuyCamera_Checkout:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click 'Buy' or 'Checkout' to pay" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(shopCheckoutBtnRect);
                break;

            case TutorialStep.BuyCamera_CloseShop:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Press <color=red>[E]</color> to close the terminal" });
                break;

            case TutorialStep.PickUpCamera:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Pick up the Film Camera from the delivery zone" });
                TutorialUIManager.Instance.SetDynamicGlow("camera", true);
                break;

            case TutorialStep.PickUpSDCard:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Pick up the SD Card" });
                TutorialUIManager.Instance.SetDynamicGlow("camera", false);
                TutorialUIManager.Instance.SetDynamicGlow("sd", true);
                break;

            case TutorialStep.InsertSDCard:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Hold the Camera and press <color=red>[C]</color> to insert the SD Card" });
                TutorialUIManager.Instance.SetDynamicGlow("sd", false);
                TutorialUIManager.Instance.SetDynamicGlow("camera", true);
                break;

            case TutorialStep.WalkToStageWithCamera:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Walk over to the Target Circle on the Stage (Point C)" });
                TutorialUIManager.Instance.SetDynamicGlow("camera", false);
                if (cameraWalkTriggerCircle != null) cameraWalkTriggerCircle.SetActive(true);
                TutorialUIManager.Instance.SetDynamicGlow("pointc", true);
                PointLineAt("pointc");
                break;

            case TutorialStep.EquipCameraView:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click <color=red>[Left Mouse Button]</color> to look through the camera lens" });
                cameraViewEntered = false;
                break;

            case TutorialStep.PracticeCameraZoom: TutorialUIManager.Instance.SetupTasks(new string[] { "- Try zooming both in and out with <color=red>[Scroll]</color>", "- Take a moment to explore, then release the controls" }); cameraZoomed = false; ResetCameraPractice(); break;
            case TutorialStep.PracticeCameraPedestal: TutorialUIManager.Instance.SetupTasks(new string[] { "- Try both <color=red>[Q]</color> and <color=red>[E]</color> to change camera height", "- Explore high and low angles, then release the controls" }); cameraPedestalMoved = false; ResetCameraPractice(); break;
            case TutorialStep.FrameSubject: TutorialUIManager.Instance.SetupTasks(new string[] { "- Aim at the prop until HUD says [SUBJECT DETECTED]" }); subjectFramed = false; break;

            case TutorialStep.RecordVideo:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Press <color=red>[R]</color> to record for exactly 10s (Keep Subject Centered!)" });
                TutorialUIManager.Instance.SetDynamicGlow("camera", true);
                PointLineAt("");
                break;

            case TutorialStep.PickUpUsedSDCard:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Pick up the ejected SD Card" });
                SetUsedSDCardGlow(true);
                PointLineAt("sd");
                break;

            case TutorialStep.InsertToComputer:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Hold the used SD card", "- Press <color=red>[F]</color> on the computer tower" });
                SetUsedSDCardGlow(false);
                TutorialUIManager.Instance.SetDynamicGlow("computer", true);
                PointLineAt("computer");
                break;

            case TutorialStep.OpenComputer:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Press <color=red>[E]</color> on the computer monitor to log in" });
                TutorialUIManager.Instance.SetDynamicGlow("computer", true);
                PointLineAt("computer");
                break;

            case TutorialStep.OpenRecordingsFolder:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click the 'RECORDINGS' folder on the desktop" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(GetComputerHighlightTarget("Folder", compFolderRect));
                break;

            case TutorialStep.ClickVideoClip:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click your raw video file to review it" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(GetComputerHighlightTarget("VideoClip", compClipCardRect));
                break;

            case TutorialStep.PlayVideoClip:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click Play to review your camera work" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(GetComputerHighlightTarget("Play", compPlayBtnRect));
                break;

            case TutorialStep.ClickBack:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click the 'Close' or 'Back' button to return to the desktop" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(GetComputerHighlightTarget("Back", compBackBtnRect));
                break;

            case TutorialStep.ClickEditorApp:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click the 'Editor' Application" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(GetComputerHighlightTarget("Editor", compEditorAppRect));
                break;

            case TutorialStep.ClickConfirmEditor:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click 'Confirm' to leave the studio and begin Post-Production" });
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(GetComputerHighlightTarget("Confirm", compConfirmBtnRect));
                break;
        }
    }

    private IEnumerator PracticeTimer(float duration, TutorialStep nextStep)
    {
        yield return DevTutorialBypass.WaitForPractice(duration);
        if (currentStep == TutorialStep.PracticeLight_Intensity || currentStep == TutorialStep.PracticeLight_Tilt)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(nextStep, true));
        }
    }

    private IEnumerator TransitionToNextStep(TutorialStep nextStep, bool didTaskJustComplete)
    {
        if (isTransitioning) yield break;
        CancelActiveWarning();
        isTransitioning = true;
        isTaskPhaseActive = false;
        PointLineAt("");

        if (cubePlacementTarget != null) cubePlacementTarget.SetActive(false);
        TutorialUIManager.Instance.ClearDynamicGlows();

        if (stageWalkTriggerCircle != null) stageWalkTriggerCircle.SetActive(false);
        if (cameraWalkTriggerCircle != null) cameraWalkTriggerCircle.SetActive(false);

        if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();
        if (spacePromptText != null) spacePromptText.gameObject.SetActive(false);

        if (didTaskJustComplete) yield return new WaitForSeconds(0.1f);
        TutorialUIManager.Instance.HideBossDialogue();
        if (TutorialUIManager.Instance.taskPanel != null) TutorialUIManager.Instance.taskPanel.SetActive(false);

        yield return new WaitForSeconds(.1f);
        currentStep = nextStep;
        isTransitioning = false;
        UpdateBossDialogue();
    }

    private void CancelActiveWarning()
    {
        if (warningCoroutine != null)
        {
            StopCoroutine(warningCoroutine);
            warningCoroutine = null;
        }

        restoreTaskPanelAfterWarning = false;
    }

    private RectTransform GetComputerHighlightTarget(string targetName, RectTransform fallbackTarget)
    {
        ComputerUIManager computerUI = FindObjectOfType<ComputerUIManager>(true);
        if (computerUI == null) return fallbackTarget;

        RectTransform activeTarget = computerUI.GetTutorialHighlightTarget(targetName);
        return activeTarget != null ? activeTarget : fallbackTarget;
    }

    public void OnFirstContractAccepted()
    {
        if (currentStep == TutorialStep.OfferFirstContract && isTaskPhaseActive)
        {
            PlayerPrefs.SetInt("FlowerContractAccepted", 1);
            PlayerPrefs.Save();
            if (firstContractPanel != null) firstContractPanel.SetActive(false);
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.SetTrainingObjectAndMoney, true));
        }
    }

    public void OnTabletOpened()
    {
        if (CampaignLevelManager.Instance != null && CampaignLevelManager.Instance.IsActorIntroductionActive())
        {
            CampaignLevelManager.Instance.OnDirectorTerminalOpened();
            return;
        }

        if (currentStep == TutorialStep.BuildStageWall && isTaskPhaseActive && !tabletOpened)
        {
            tabletOpened = true;
            TutorialUIManager.Instance.MarkTaskComplete(0);
            TutorialUIManager.Instance.SetDynamicGlow("director", false);
            StartCoroutine(TransitionToNextStep(TutorialStep.ExplainDirectorTablet, true));
        }
    }

    public void OnWallAdded()
    {
        if (currentStep == TutorialStep.Tablet_AddWall && isTaskPhaseActive && !wallAdded)
        {
            wallAdded = true;
            TutorialUIManager.Instance.MarkTaskComplete(0);

            StartCoroutine(TransitionToNextStep(TutorialStep.Tablet_SelectWall, true));
        }
    }

    public void OnObjectSelected(string objName)
    {
        if (currentStep == TutorialStep.Tablet_SelectWall && isTaskPhaseActive)
        {
            string lowerName = objName.ToLower();
            if (lowerName.Contains("wall") || lowerName.Contains("stage") || lowerName.Contains("studio") || lowerName.Contains("backdrop"))
            {
                TutorialUIManager.Instance.MarkTaskComplete(0);
                StartCoroutine(TransitionToNextStep(TutorialStep.Tablet_PaintWall, true));
            }
        }
    }

    public Color SnapTutorialPaintColor(Color color, bool isWall)
    {
        if (!isActiveAndEnabled || !isTaskPhaseActive) return color;

        TutorialStep paintStep = isWall ? TutorialStep.Tablet_PaintWall : TutorialStep.Tablet_PaintCube;
        if (currentStep != paintStep) return color;

        // Help the player land on 150 without changing unrelated colors or
        // completing the lesson until the Director Terminal sees mouse release.
        float blueValue = color.b * 255f;
        if (Mathf.Abs(blueValue - TutorialBlueTarget) <= TutorialBlueSnapTolerance + 0.001f)
            color.b = TutorialBlueTarget / 255f;

        return color;
    }

    public void CheckWallColor(float rValue, float gValue, float bValue)
    {
        if (currentStep == TutorialStep.Tablet_PaintWall && isTaskPhaseActive && !wallColorChanged)
        {
            if (rValue >= 245f && rValue <= 255f && gValue <= 10f && Mathf.Approximately(bValue, TutorialBlueTarget))
            {
                wallColorChanged = true;
                TutorialUIManager.Instance.MarkTaskComplete(0);
                TutorialUIManager.Instance.MarkTaskComplete(1);
                TutorialUIManager.Instance.MarkTaskComplete(2);
                StartCoroutine(TransitionToNextStep(TutorialStep.Tablet_SpawnCube, true));
            }
        }
    }

    public void OnCubeSpawned() { }
    public void OnCubeMoved() { }
    public void OnPropSpawnedFromUI() { }
    public void OnFlowerPlacedOnCube() { }

    public void CheckCubeColor(float rValue, float gValue, float bValue)
    {
        if (currentStep == TutorialStep.Tablet_PaintCube && isTaskPhaseActive && !cubePainted)
        {
            if (rValue >= 245f && rValue <= 255f && gValue <= 10f && Mathf.Approximately(bValue, TutorialBlueTarget))
            {
                cubePainted = true;
                TutorialUIManager.Instance.MarkTaskComplete(0);
                TutorialUIManager.Instance.MarkTaskComplete(1);
                TutorialUIManager.Instance.MarkTaskComplete(2);
                StartCoroutine(TransitionToNextStep(TutorialStep.Tablet_SpawnProp, true));
            }
        }
    }

    public void OnTabletClosed()
    {
        if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();

        if (CampaignLevelManager.Instance != null && CampaignLevelManager.Instance.IsActorIntroductionActive())
        {
            CampaignLevelManager.Instance.OnDirectorTerminalClosed();
            return;
        }

        if (currentStep == TutorialStep.FreePlayDirectorTablet && isTaskPhaseActive)
            StartCoroutine(TransitionToNextStep(TutorialStep.BuyLight_WalkToShop, false));
    }

    public void OnShopOpened()
    {
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
        {
            GokeLevelManager.Instance.OnShopOpened();
            return;
        }

        if (Level3Manager.Instance != null && Level3Manager.Instance.IsEquipmentIntroductionActive())
        {
            Level3Manager.Instance.OnShopOpened();
            return;
        }

        if (currentStep == TutorialStep.BuyLight_WalkToShop && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.BuyLight_AddToCart, true));
        }
        else if (currentStep == TutorialStep.BuyCamera_WalkToShop && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.BuyCamera_AddToCart, true));
        }
    }

    public void OnLightAddedToCart()
    {
        if (currentStep == TutorialStep.BuyLight_AddToCart && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.BuyLight_Checkout, true));
        }
    }

    public void OnCameraAddedToCart()
    {
        if (currentStep == TutorialStep.BuyCamera_AddToCart && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.BuySDCard_AddToCart, true));
        }
    }

    public void OnSDCardAddedToCart()
    {
        if (currentStep == TutorialStep.BuySDCard_AddToCart && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.BuyCamera_Checkout, true));
        }
    }

    public void OnShopClosed()
    {
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
        {
            GokeLevelManager.Instance.OnShopClosed();
            return;
        }

        if (Level3Manager.Instance != null && Level3Manager.Instance.IsEquipmentIntroductionActive())
        {
            Level3Manager.Instance.OnShopClosed();
            return;
        }

        if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HideHighlight();

        if (currentStep == TutorialStep.BuyLight_CloseShop && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.PickUpLight, true));
        }
        else if (currentStep == TutorialStep.BuyCamera_CloseShop && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.PickUpCamera, true));
        }
    }

    public void OnEquipmentBought(int itemsCount = 1)
    {
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
        {
            GokeLevelManager.Instance.OnEquipmentBought(itemsCount);
            return;
        }

        if (Level3Manager.Instance != null && Level3Manager.Instance.IsEquipmentIntroductionActive())
        {
            Level3Manager.Instance.OnEquipmentBought(itemsCount);
            return;
        }

        if (currentStep == TutorialStep.BuyLight_Checkout && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.SetDynamicGlow("shop", false);
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.BuyLight_CloseShop, true));
        }
        else if (currentStep == TutorialStep.BuyCamera_Checkout && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.SetDynamicGlow("shop", false);
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.BuyCamera_CloseShop, true));
        }
    }

    public void OnLightPickedUp(Player.Equipment.FilmLightItem light = null)
    {
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
        {
            GokeLevelManager.Instance.OnLightPickedUp(light);
            return;
        }

        if (Level3Manager.Instance != null && Level3Manager.Instance.IsEquipmentIntroductionActive())
        {
            Level3Manager.Instance.OnLightPickedUp(light);
            return;
        }

        if (currentStep == TutorialStep.PickUpLight && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.WalkToStageWithLight, true));
        }
    }

    public bool CanPickUpLight(Player.Equipment.FilmLightItem light)
    {
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
        {
            return GokeLevelManager.Instance.CanPickUpLight(light);
        }

        if (Level3Manager.Instance != null && Level3Manager.Instance.IsEquipmentIntroductionActive())
        {
            return Level3Manager.Instance.CanPickUpLight(light);
        }

        return true;
    }

    public void OnLightTurnedOn(Player.Equipment.FilmLightItem light = null)
    {
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
        {
            GokeLevelManager.Instance.OnLightTurnedOn(light);
            return;
        }

        if (Level3Manager.Instance != null && Level3Manager.Instance.IsEquipmentIntroductionActive())
        {
            Level3Manager.Instance.OnLightTurnedOn(light);
            return;
        }

        if (currentStep == TutorialStep.TurnOnLight && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.PracticeLight_Intensity, true));
        }
    }

    public void OnLightIntensityChanged(float intensity, Player.Equipment.FilmLightItem light = null)
    {
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
        {
            GokeLevelManager.Instance.OnLightIntensityChanged(light, intensity);
            return;
        }

        if (Level3Manager.Instance != null && Level3Manager.Instance.IsEquipmentIntroductionActive())
        {
            Level3Manager.Instance.OnLightIntensityChanged(light, intensity);
            return;
        }

        if (currentStep == TutorialStep.AdjustLight_Intensity && isTaskPhaseActive)
        {
            if (Mathf.RoundToInt(intensity) == 45)
            {
                TutorialUIManager.Instance.MarkTaskComplete(0);
                StartCoroutine(TransitionToNextStep(TutorialStep.PracticeLight_Tilt, true));
            }
        }
    }

    public void OnLightTilted(float tilt)
    {
        if (Level3Manager.Instance != null && Level3Manager.Instance.IsEquipmentIntroductionActive())
        {
            Level3Manager.Instance.OnLightTilted(tilt);
            return;
        }

        if (currentStep == TutorialStep.AdjustLight_Tilt && isTaskPhaseActive)
        {
            if (Mathf.RoundToInt(tilt) == -5)
            {
                TutorialUIManager.Instance.MarkTaskComplete(0);
                StartCoroutine(TransitionToNextStep(TutorialStep.DropLight, true));
            }
        }
    }

    public void OnLightFeatureChanged(Player.Equipment.FilmLightItem light)
    {
        if (Level3Manager.Instance != null && Level3Manager.Instance.IsEquipmentIntroductionActive())
        {
            Level3Manager.Instance.OnLightFeatureChanged(light);
        }
    }

    public void OnLightDropped(Player.Equipment.FilmLightItem light = null)
    {
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
        {
            GokeLevelManager.Instance.OnLightDropped(light);
            return;
        }

        if (Level3Manager.Instance != null && Level3Manager.Instance.IsEquipmentIntroductionActive())
        {
            Level3Manager.Instance.OnLightDropped(light);
            return;
        }

        if (currentStep == TutorialStep.DropLight && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            UnfreezePlayerMovement();
            StartCoroutine(TransitionToNextStep(TutorialStep.ShowProductionTitle, true));
        }
    }

    public void OnCameraPickedUp(string equipmentName = "")
    {
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
        {
            GokeLevelManager.Instance.OnCameraPickedUp(equipmentName);
            return;
        }

        if (currentStep == TutorialStep.PickUpCamera && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.PickUpSDCard, true));
        }
    }

    public void OnSDCardPickedUp()
    {
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
        {
            GokeLevelManager.Instance.OnSDCardPickedUp();
            return;
        }

        if (currentStep == TutorialStep.PickUpSDCard && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.InsertSDCard, true));
        }
    }

    public void OnUsedSDCardPickedUp()
    {
        if (currentStep == TutorialStep.PickUpUsedSDCard && isTaskPhaseActive)
        {
            SetUsedSDCardGlow(false);
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.InsertToComputer, true));
        }
    }

    public void OnCardInsertedToCamera(string equipmentName = "")
    {
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
        {
            GokeLevelManager.Instance.OnCardInsertedToCamera(equipmentName);
            return;
        }

        if (currentStep == TutorialStep.InsertSDCard && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.SetDynamicGlow("camera", false);
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.WalkToStageWithCamera, true));
        }
    }

    public void OnCameraViewEntered(string equipmentName = "")
    {
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
        {
            GokeLevelManager.Instance.OnCameraViewEntered(equipmentName);
            return;
        }

        if (currentStep == TutorialStep.EquipCameraView && isTaskPhaseActive && !cameraViewEntered)
        {
            cameraViewEntered = true;
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.PracticeCameraZoom, true));
        }
    }

    public void OnCameraViewExited(string equipmentName = "")
    {
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
        {
            GokeLevelManager.Instance.OnCameraViewExited(equipmentName);
        }
    }
    public void OnSubjectFramed() { if (currentStep == TutorialStep.FrameSubject && isTaskPhaseActive && !subjectFramed) { subjectFramed = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.RecordVideo, true)); } }
    public void OnRecordingFinished(GameObject usedSDCard = null)
    {
        if (usedSDCard != null)
        {
            tutorialUsedSDCard = usedSDCard.transform;
            tutorialUsedSDCardGlow = usedSDCard.GetComponent<TutorialGlowTarget>();
            if (tutorialUsedSDCardGlow == null) tutorialUsedSDCardGlow = usedSDCard.GetComponentInChildren<TutorialGlowTarget>();
        }

        if (currentStep == TutorialStep.RecordVideo && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.SetDynamicGlow("camera", false);
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(usedSDCard != null && usedSDCard.GetComponentInParent<Player.Interactor.EquipmentInteractor>() != null ? TutorialStep.InsertToComputer : TutorialStep.PickUpUsedSDCard, true));
        }
    }

    public void OnCardInsertedToComputer() { if (currentStep == TutorialStep.InsertToComputer && isTaskPhaseActive) { TutorialUIManager.Instance.SetDynamicGlow("computer", false); TutorialUIManager.Instance.MarkTaskComplete(0); TutorialUIManager.Instance.MarkTaskComplete(1); StartCoroutine(TransitionToNextStep(TutorialStep.OpenComputer, true)); } }
    public void OnComputerOpened() { if (currentStep == TutorialStep.OpenComputer && isTaskPhaseActive) { TutorialUIManager.Instance.SetDynamicGlow("computer", false); TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.ExplainComputerEditor, true)); } }

    public void OnRecordingsFolderOpened()
    {
        if (currentStep == TutorialStep.OpenRecordingsFolder && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.ClickVideoClip, true));
        }
    }

    public void OnVideoClipClicked()
    {
        if (currentStep == TutorialStep.ClickVideoClip && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.PlayVideoClip, true));
        }
    }

    public void OnVideoPlayed()
    {
        if (currentStep == TutorialStep.PlayVideoClip && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.ClickBack, true));
        }
    }

    public void OnComputerBackClicked()
    {
        if (currentStep == TutorialStep.ClickBack && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.ClickEditorApp, true));
        }
    }

    public void OnEditorAppClicked()
    {
        if (currentStep == TutorialStep.ClickEditorApp && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.ClickConfirmEditor, true));
        }
    }

    public void OnEditorConfirmed()
    {
        if (currentStep == TutorialStep.ClickConfirmEditor && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.Complete, true));
        }
    }

    private void UpdateBossDialogue()
    {
        CancelActiveWarning();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        LockPlayer();

        spacebarCooldown = Time.unscaledTime + 0.2f;

        if (spacePromptText != null) spacePromptText.gameObject.SetActive(false);

        var ui = TutorialUIManager.Instance;

        switch (currentStep)
        {
            case TutorialStep.Intro: ui.ShowBossDialogue("Hey, welcome to Crew-On-Set! I run the studio, but you can call me Boss. I'll be here to guide you through your first commercial.", ui.poseHappy, true, true); break;
            case TutorialStep.WaitForPrompt: ui.ShowBossDialogue("First day on set? Let me show you around.\n<color=red>[SPACE]</color> Show me the ropes   <color=red>[TAB]</color> Skip the tutorial", ui.posePoint, true, true); break;
            case TutorialStep.LearnMovement: ui.ShowBossDialogue("Take a look around. Use <color=red>[WASD]</color> to walk, <color=red>[SPACE]</color> to jump, and <color=red>[SHIFT]</color> to sprint. Give each a try.", ui.posePoint, true, false); break;
            case TutorialStep.GameExplanation: ui.ShowBossDialogue(explanationPages[currentExplanationPage], ui.poseBoss, true, true); break;
            case TutorialStep.OfferFirstContract: ui.ShowBossDialogue("Here's our first job: an Artisan Flower Vase commercial. Click <color=red>SELECT</color> to open the brief. Read what the client needs, then close the folder to begin.", ui.poseOpenHand, true, false); break;
            case TutorialStep.SetTrainingObjectAndMoney:
                if (isLevel1Retry)
                    ui.ShowBossDialogue("The client needs a few changes. Have a look at the feedback; I've topped your budget back up to <color=yellow>9,000 B-Coins</color> for another take.", ui.poseBoss, true, false);
                else
                    ui.ShowBossDialogue("All right, we're on the job. You've got <color=yellow>9,000 B-Coins</color> to work with, and the Floral Vase is ready. Let's build its set.", ui.poseSmile, true, true);
                break;

            case TutorialStep.ShowPreProductionTitle:
                if (preProductionTitleCard != null) StartCoroutine(FadeTitleCardSequence(preProductionTitleCard, TutorialStep.ExplainPreProduction));
                else StartCoroutine(TransitionToNextStep(TutorialStep.ExplainPreProduction, false));
                break;

            case TutorialStep.ExplainPreProduction:
                ui.ShowBossDialogue("Before we roll, we get the set ready. That's pre-production. Let's start with a backdrop and a place for our vase.", ui.poseBoss, true, true);
                break;

            case TutorialStep.BuildStageWall: ui.ShowBossDialogue("Let's start at the Director Tablet. Walk up to it and press <color=red>[E]</color> when the prompt appears.", ui.poseOpenHand, true, false); break;

            case TutorialStep.ExplainDirectorTablet: ui.ShowBossDialogue("The client asked for a pink backdrop. Let's give that vase a set of its own.", ui.posePointUp, true, true); break;

            case TutorialStep.Tablet_AddWall: ui.ShowBossDialogue("We'll need a backdrop behind the vase. Click <color=red>ADD WALL</color> to put one on the stage.", ui.poseOpenHand, true, false); break;

            case TutorialStep.Tablet_SelectWall: ui.ShowBossDialogue("Click the wall to select it. That tells the color sliders which object we're painting.", ui.posePoint, true, false); break;

            case TutorialStep.Tablet_PaintWall: ui.ShowBossDialogue("Let's give the backdrop that pink the client asked for. Set Red to 255, Green to 0, and Blue to 150.", ui.posePointUp, true, false); break;

            case TutorialStep.Tablet_SpawnCube: ui.ShowBossDialogue("The vase needs a little height. Click the <color=red>Cube</color> card to pick up a display stand with your cursor.", ui.poseOpenHand, true, false); break;
            case TutorialStep.Tablet_MoveCube: ui.ShowBossDialogue("Bring the cube over to the stage marker, then <color=red>[Left Click]</color> to set it down.", ui.posePoint, true, false); break;

            case TutorialStep.Tablet_PaintCube: ui.ShowBossDialogue("Let's match the stand to our backdrop. Select the cube and use the same pink: Red 255, Green 0, Blue 150.", ui.posePointUp, true, false); break;

            case TutorialStep.Tablet_SpawnProp: ui.ShowBossDialogue("Now for the star of the shot. Click the <color=red>Floral Vase</color> card to pick it up with your cursor.", ui.poseOpenHand, true, false); break;
            case TutorialStep.Tablet_MovePropToCube: ui.ShowBossDialogue("Move the vase onto the cube, then <color=red>[Left Click]</color> to place it. Leave the whole vase in view.", ui.poseBoss, true, false); break;

            case TutorialStep.TabletPracticeFinished: ui.ShowBossDialogue("That's our set! Need to reposition anything? Select it and press <color=red>[T]</color>. When you're done, close the tablet with <color=red>[E]</color> or <color=red>[ESC]</color>.", ui.poseChill, true, true); break;

            case TutorialStep.BuyLight_WalkToShop: ui.ShowBossDialogue("Let's give the flower some light. Head to the Equipment Shop and press <color=red>[E]</color>; we're buying one Stage Light.", ui.posePoint, true, false); break;
            case TutorialStep.BuyLight_AddToCart: ui.ShowBossDialogue("One Stage Light will do for this shot. Find it and click <color=red>ADD TO CART</color>.", ui.poseOpenHand, true, false); break;
            case TutorialStep.BuyLight_Checkout: ui.ShowBossDialogue("That's the one. Click <color=red>CONFIRM</color> to place the order.", ui.poseSmile, true, false); break;
            case TutorialStep.BuyLight_CloseShop: ui.ShowBossDialogue("Your light's here! Press <color=red>[SPACE]</color> to finish our chat, then <color=red>[E]</color> to leave the shop. Let's collect it.", ui.poseHappy, true, false); break;

            case TutorialStep.PickUpLight: ui.ShowBossDialogue("There's your light on the delivery table. Look at it and press <color=red>[E]</color> to pick it up.", ui.posePoint, true, false); break;

            case TutorialStep.WalkToStageWithLight: ui.ShowBossDialogue("Bring it over to the marker on the pink stage. We'll aim it from there.", ui.posePointUp, true, false); break;

            case TutorialStep.TurnOnLight: ui.ShowBossDialogue("Point it toward the flower and click <color=red>[Left Click]</color> once to switch it on.", ui.posePointUp, true, false); break;

            case TutorialStep.PracticeLight_Intensity: ui.ShowBossDialogue("Try the <color=red>[Scroll Wheel]</color>. See how the brightness changes? Watch the petals; we don't want to lose their detail.", ui.poseSmile, true, false); break;
            case TutorialStep.AdjustLight_Intensity: ui.ShowBossDialogue("Let's settle on 45% for this shot. Use the <color=red>[Scroll Wheel]</color> to dial it in.", ui.posePointUp, true, false); break;
            case TutorialStep.PracticeLight_Tilt: ui.ShowBossDialogue("Try tilting the light with the <color=red>[Up/Down Arrows]</color>. Follow the bright patch as it moves across the set.", ui.poseBoss, true, false); break;
            case TutorialStep.AdjustLight_Tilt: ui.ShowBossDialogue("Let's aim a little higher. Use the <color=red>[Up/Down Arrows]</color> to bring the tilt to -5°.", ui.poseBoss, true, false); break;

            case TutorialStep.DropLight: ui.ShowBossDialogue("There we go. Press <color=red>[G]</color> to set the light down and keep that aim.", ui.poseHappy, true, false); break;

            case TutorialStep.ShowProductionTitle:
                if (productionTitleCard != null) StartCoroutine(FadeTitleCardSequence(productionTitleCard, TutorialStep.ExplainProduction));
                else StartCoroutine(TransitionToNextStep(TutorialStep.ExplainProduction, false));
                break;

            case TutorialStep.ExplainProduction:
                ui.ShowBossDialogue("The set's built and the light's in place. Now we're into production: getting our shot on camera.", ui.poseBoss, true, true);
                break;

            case TutorialStep.BuyCamera_WalkToShop: ui.ShowBossDialogue("Time to get a camera on this set. Head back to the shop and press <color=red>[E]</color>; we'll need a Film Camera and an SD Card.", ui.poseOpenHand, true, false); break;
            case TutorialStep.BuyCamera_AddToCart: ui.ShowBossDialogue("Find the Film Camera and click <color=red>ADD TO CART</color>. That's our next tool.", ui.posePoint, true, false); break;
            case TutorialStep.BuySDCard_AddToCart: ui.ShowBossDialogue("Don't forget something to record onto. Click <color=red>ADD TO CART</color> under the SD Card.", ui.poseBoss, true, false); break;
            case TutorialStep.BuyCamera_Checkout: ui.ShowBossDialogue("Camera and card? We're set. Click <color=red>CONFIRM</color> to order them.", ui.poseSmile, true, false); break;
            case TutorialStep.BuyCamera_CloseShop: ui.ShowBossDialogue("Our gear's at the delivery table. Press <color=red>[SPACE]</color> to finish here, then <color=red>[E]</color> to close the shop.", ui.poseHappy, true, false); break;

            case TutorialStep.PickUpCamera: ui.ShowBossDialogue("Let's grab the camera first. Look at it on the delivery table and press <color=red>[E]</color>.", ui.posePoint, true, false); break;
            case TutorialStep.PickUpSDCard: ui.ShowBossDialogue("Grab the SD Card with <color=red>[E]</color> too. It'll fit in another hotbar slot.", ui.poseOpenHand, true, false); break;
            case TutorialStep.InsertSDCard: ui.ShowBossDialogue("Select the camera in your hotbar, then press <color=red>[C]</color> to pop the SD Card in.", ui.poseBoss, true, false); break;
            case TutorialStep.WalkToStageWithCamera: ui.ShowBossDialogue("Head over to Point C, the Director's mark. Let's see how our set looks through the camera.", ui.posePointUp, true, false); break;

            case TutorialStep.EquipCameraView: ui.ShowBossDialogue("With the camera selected, click <color=red>[Left Click]</color> to look through the viewfinder. This is what your audience will see.", ui.poseHappy, true, false); break;

            case TutorialStep.PracticeCameraZoom: ui.ShowBossDialogue("Try zooming with the <color=red>[Scroll Wheel]</color>. Get a closer look, but leave room for the whole vase.", ui.posePointUp, true, false); break;
            case TutorialStep.PracticeCameraPedestal: ui.ShowBossDialogue("Let's try a different height. Hold <color=red>[Q]</color> or <color=red>[E]</color> to raise or lower the camera, and try both directions.", ui.posePoint, true, false); break;
            case TutorialStep.FrameSubject: ui.ShowBossDialogue("For our first shot, put the flower right in the center. Give it enough room so nothing gets cut off.", ui.posePointUp, true, false); break;

            case TutorialStep.RecordVideo: ui.ShowBossDialogue("Ready? Press <color=red>[R]</color> to roll. Hold that centered shot for 10 seconds, then press <color=red>[R]</color> again to cut.", ui.poseBoss, true, false); break;

            case TutorialStep.PickUpUsedSDCard: ui.ShowBossDialogue("And cut! Your take is on the card the camera just ejected. Look at it and press <color=red>[E]</color> to collect it.", ui.poseHappy, true, false); break;
            case TutorialStep.InsertToComputer: ui.ShowBossDialogue("Your recorded SD Card is in your inventory. Select its hotbar slot, look at the computer tower, and press <color=red>[F]</color> to insert it.", ui.posePoint, true, false); break;
            case TutorialStep.OpenComputer: ui.ShowBossDialogue("The card's in. Look at the monitor and press <color=red>[E]</color>; let's see what we shot.", ui.poseBoss, true, false); break;

            case TutorialStep.ExplainComputerEditor: ui.ShowBossDialogue("Before we edit, let's watch the take. We're checking the framing, the light, and whether we recorded enough footage.", ui.poseOpenHand, true, true); break;

            case TutorialStep.OpenRecordingsFolder: ui.ShowBossDialogue("Open the <color=red>Recordings</color> folder. Your new take should be in there.", ui.posePoint, true, false); break;
            case TutorialStep.ClickVideoClip: ui.ShowBossDialogue("There's our take. Click the video clip so we can have a look.", ui.poseBoss, true, false); break;
            case TutorialStep.PlayVideoClip: ui.ShowBossDialogue("Hit <color=red>PLAY</color>. Watch the vase throughout the take: can you see it clearly, centered and evenly lit?", ui.poseSmile, true, false); break;
            case TutorialStep.ClickBack: ui.ShowBossDialogue("All right, let's get to the edit. Click <color=red>Close</color> or <color=red>Back</color> to return to the computer's main menu.", ui.posePointUp, true, false); break;
            case TutorialStep.ClickEditorApp: ui.ShowBossDialogue("Open the <color=red>Editor</color> app. This is where we'll put the commercial together.", ui.poseBoss, true, false); break;
            case TutorialStep.ClickConfirmEditor: ui.ShowBossDialogue("Happy with your footage? Click <color=red>CONFIRM</color> to head into editing. This commits the take; we can't return to the studio afterward.", ui.poseOpenHand, true, false); break;

            case TutorialStep.Complete: ui.ShowBossDialogue("That's a wrap on the shoot. Let's head into the edit!", ui.poseEndWave, false, false); break;

            case TutorialStep.PostEditComplete: ui.ShowBossDialogue("Your first commercial! You took it all the way from an empty stage. I've unlocked the <color=yellow>Production Almanac</color>; press <color=red>[P]</color> whenever you need a refresher.", ui.poseHappy, true, true); break;

            case TutorialStep.OfferLevel1: ui.ShowBossDialogue("One commercial down. I've got another brief on my desk; ready to hear about it?", ui.poseHappy, true, true); break;

        }
    }

    public void SpawnCheatSDCard()
    {
        if (sdCardPrefab == null)
        {
            GameFeedback.Show("CHEAT FAILED: SD card prefab is missing.", true);
            return;
        }

        if (playerTransform == null)
        {
            GameFeedback.Show("CHEAT FAILED: Cannot find the player.", true);
            return;
        }

        string dummyFileName = "Cheat_Footage_" + Random.Range(1000, 9999) + ".tape";
        string fullPath = System.IO.Path.Combine(Application.persistentDataPath, dummyFileName);

        try
        {
            using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(new System.IO.FileStream(fullPath, System.IO.FileMode.Create)))
            {
                int frameCount = Mathf.Max(1, Mathf.RoundToInt(10f * TapeSettings.framesPerSecond));
                writer.Write(frameCount);

                Texture2D tex = new Texture2D(16, 16, TextureFormat.RGB24, false);
                Color[] pixels = new Color[16 * 16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.red;
                tex.SetPixels(pixels);
                tex.Apply();

                byte[] bytes = tex.EncodeToJPG(50);
                for (int frame = 0; frame < frameCount; frame++)
                {
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                }
                Destroy(tex);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not write dummy cheat file: " + e.Message);
        }

        Vector3 spawnPos = playerTransform.position + playerTransform.forward * 1.5f + Vector3.up * 1.5f;
        GameObject fakeCard = Instantiate(sdCardPrefab, spawnPos, Quaternion.identity);
        tutorialUsedSDCard = fakeCard.transform;
        tutorialUsedSDCardGlow = fakeCard.GetComponent<TutorialGlowTarget>();
        if (tutorialUsedSDCardGlow == null) tutorialUsedSDCardGlow = fakeCard.GetComponentInChildren<TutorialGlowTarget>();

        Player.Equipment.SDCardItem cardScript = fakeCard.GetComponent<Player.Equipment.SDCardItem>();
        if (cardScript != null)
        {
            cardScript.isUsedCard = true;
            cardScript.recordedFileName = dummyFileName;
            cardScript.videoDuration = 10f;
            cardScript.videoScore = 100f;
            cardScript.cameraScore = 50f;
            cardScript.lightScore = 50f;
            cardScript.MarkAsUsed();
        }

        Rigidbody rb = fakeCard.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        MeshRenderer[] renderers = fakeCard.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer r in renderers)
        {
            r.material.color = Color.red;
        }

        GameFeedback.Show("CHEAT ACTIVATED\nSpawned a completed 10-second SD card");

        if (currentStep < TutorialStep.InsertToComputer)
        {
            currentStep = TutorialStep.PickUpUsedSDCard;
            UpdateBossDialogue();
            TutorialUIManager.Instance.SetDynamicGlow("camera", false);
            SetUsedSDCardGlow(true);
            PointLineAt("sd");
        }
    }

    public bool CanInteract(string objectType)
    {
        if (currentStep >= TutorialStep.OfferLevel1) return true;

        if (objectType == "DirectorTerminal") return currentStep >= TutorialStep.BuildStageWall && currentStep <= TutorialStep.FreePlayDirectorTablet;
        if (objectType == "ShopTerminal") return HasGuidedShopTask || currentStep >= TutorialStep.BuyCamera_WalkToShop;
        if (objectType == "ComputerStation") return currentStep >= TutorialStep.InsertToComputer && currentStep <= TutorialStep.Complete;
        if (objectType == "HelpDesk") return currentStep >= TutorialStep.Level1Accepted;

        return true;
    }

    private bool HasGuidedShopTask => isActiveAndEnabled && !DevTutorialBypass.Disabled &&
        ((currentStep >= TutorialStep.BuyLight_WalkToShop && currentStep <= TutorialStep.BuyLight_CloseShop) ||
         (currentStep >= TutorialStep.BuyCamera_WalkToShop && currentStep <= TutorialStep.BuyCamera_CloseShop));

    // Reopening an interrupted order must lead to the next missing item, not an empty checkout.
    public void RecoverTutorialCart(bool camera, bool light, bool sdCard)
    {
        if (!HasGuidedShopTask || isTransitioning) return;
        TutorialStep next = currentStep;
        if (currentStep == TutorialStep.BuyLight_Checkout && !light)
            next = TutorialStep.BuyLight_AddToCart;
        else if (currentStep == TutorialStep.BuySDCard_AddToCart || currentStep == TutorialStep.BuyCamera_Checkout)
            next = !camera ? TutorialStep.BuyCamera_AddToCart : !sdCard ? TutorialStep.BuySDCard_AddToCart : currentStep;
        if (next != currentStep) StartCoroutine(TransitionToNextStep(next, false));
    }

    public bool CanCheckoutTutorialCart(bool camera, bool light, bool sdCard)
    {
        if (!HasGuidedShopTask) return true;
        if (isTransitioning || !isTaskPhaseActive) return false;
        if ((currentStep == TutorialStep.BuyLight_Checkout && light) ||
            (currentStep == TutorialStep.BuyCamera_Checkout && camera && sdCard)) return true;
        ShowWarning("Finish adding the equipment in the task on the left before confirming the order.");
        RecoverTutorialCart(camera, light, sdCard);
        return false;
    }

    public bool CanBuyItem(int itemIndex)
    {
        if (!isActiveAndEnabled || DevTutorialBypass.Disabled) return true;
        if (HasGuidedShopTask && (isTransitioning || !isTaskPhaseActive)) return false;
        if (HasGuidedShopTask && (currentStep == TutorialStep.BuyLight_Checkout ||
            currentStep == TutorialStep.BuyLight_CloseShop || currentStep == TutorialStep.BuyCamera_CloseShop))
        {
            ShowWarning("Follow the task on the left to finish this order before shopping again.");
            return false;
        }
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
        {
            return GokeLevelManager.Instance.CanBuyItem(itemIndex);
        }

        if (Level3Manager.Instance != null && Level3Manager.Instance.IsEquipmentIntroductionActive())
        {
            return Level3Manager.Instance.CanBuyItem(itemIndex);
        }

        if (currentStep >= TutorialStep.OfferLevel1) return true;

        if (currentStep < TutorialStep.BuyLight_WalkToShop) { ShowWarning("Let's finish this step together. Your current task is on the left."); return false; }

        if (currentStep >= TutorialStep.BuyLight_WalkToShop && currentStep <= TutorialStep.BuyLight_CloseShop && itemIndex != 1) { ShowWarning("Let's start with just the Stage Light. We'll shop for the rest later."); return false; }

        if (currentStep >= TutorialStep.PickUpLight && currentStep <= TutorialStep.DropLight) { ShowWarning("Let's get that light set up and placed first. Then we'll get the camera."); return false; }

        if (currentStep == TutorialStep.BuyCamera_WalkToShop) { ShowWarning("Let's finish this step together. Your current task is on the left."); return false; }
        if (currentStep == TutorialStep.BuyCamera_AddToCart && itemIndex != 0) { ShowWarning("Let's add the Film Camera first."); return false; }
        if (currentStep == TutorialStep.BuySDCard_AddToCart && itemIndex != 2) { ShowWarning("We'll need an SD Card to record onto. Add one to the cart."); return false; }
        if (currentStep == TutorialStep.BuyCamera_Checkout) { ShowWarning("That's everything we need. Click CONFIRM to place the order."); return false; }

        return true;
    }

    public bool CanInsertSDCard(string equipmentName)
    {
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
        {
            return GokeLevelManager.Instance.CanInsertSDCard(equipmentName);
        }

        return true;
    }

    public bool CanRecord()
    {
        if (currentStep >= TutorialStep.OfferLevel1) return true;
        if (currentStep < TutorialStep.RecordVideo) { ShowWarning("Let's finish framing the shot before we roll."); return false; }
        return true;
    }

    public void SetTutorialRecordingLookLock(bool shouldLock)
    {
        isTutorialRecordingLookLocked = shouldLock && currentStep == TutorialStep.RecordVideo && isTaskPhaseActive;
    }

    public bool IsTutorialRecordingLookLocked()
    {
        return isTutorialRecordingLookLocked && currentStep == TutorialStep.RecordVideo && isTaskPhaseActive;
    }

    public bool CanCloseUI(string uiType)
    {
        if (!isActiveAndEnabled || DevTutorialBypass.Disabled) return true;
        if (isTransitioning && (uiType == "ShopTerminal" || uiType == "DirectorTerminal" || uiType == "ComputerStation")) return false;
        if (currentStep >= TutorialStep.OfferLevel1) return true;
        if (uiType == "ShopTerminal" && HasGuidedShopTask && !isTaskPhaseActive) return false;

        if (uiType == "DirectorTerminal")
        {
            if (currentStep == TutorialStep.Tablet_AddWall) { ShowWarning("Let's add our backdrop before we leave the tablet."); return false; }
            if (currentStep == TutorialStep.Tablet_SelectWall) { ShowWarning("Click the wall first. We still need to give it some color."); return false; }
            if (currentStep == TutorialStep.Tablet_PaintWall) { ShowWarning("We still need the pink backdrop. Let's finish its color first."); return false; }
            if (currentStep == TutorialStep.Tablet_SpawnCube) { ShowWarning("Let's add the Cube first; the vase needs a display stand."); return false; }
            if (currentStep == TutorialStep.Tablet_MoveCube) { ShowWarning("Bring the cube over to the center marker and click to place it."); return false; }
            if (currentStep == TutorialStep.Tablet_PaintCube) { ShowWarning("Let's give the cube the same pink as our backdrop before we leave."); return false; }
            if (currentStep == TutorialStep.Tablet_SpawnProp) { ShowWarning("Our set's missing its product. Choose the Floral Vase card first."); return false; }
            if (currentStep == TutorialStep.Tablet_MovePropToCube) { ShowWarning("Let's put the vase on top of the cube before we close the tablet."); return false; }
        }
        else if (uiType == "ShopTerminal")
        {
            if (currentStep == TutorialStep.BuyLight_WalkToShop || currentStep == TutorialStep.BuyLight_AddToCart || currentStep == TutorialStep.BuyLight_Checkout)
            {
                ShowWarning("We still need to order the Stage Light. Let's finish that purchase.");
                return false;
            }
            if (currentStep == TutorialStep.BuyCamera_WalkToShop || currentStep == TutorialStep.BuyCamera_AddToCart || currentStep == TutorialStep.BuySDCard_AddToCart || currentStep == TutorialStep.BuyCamera_Checkout)
            {
                ShowWarning("Let's finish the camera and SD Card order before we head out.");
                return false;
            }
        }
        else if (uiType == "ComputerStation")
        {
            if (currentStep == TutorialStep.OpenRecordingsFolder ||
                currentStep == TutorialStep.ClickVideoClip ||
                currentStep == TutorialStep.PlayVideoClip ||
                currentStep == TutorialStep.ClickBack ||
                currentStep == TutorialStep.ClickEditorApp ||
                currentStep == TutorialStep.ClickConfirmEditor)
            {
                ShowWarning("We're not quite done at the computer. Check the task on the left for our next step.");
                return false;
            }
        }

        return true;
    }

    public bool CanUseTabletFeature(string featureName)
    {
        if (currentStep >= TutorialStep.OfferLevel1) return true;

        if (currentStep == TutorialStep.FreePlayDirectorTablet) return true;

        if (featureName == "ClearStage")
        {
            ShowWarning("Let's keep this set for the shoot. We're still using it.");
            return false;
        }

        if (featureName == "AddWall" && currentStep != TutorialStep.Tablet_AddWall)
        {
            ShowWarning("We'll get to that. For now, let's finish the step on the left.");
            return false;
        }

        if (featureName == "ColorSliders" && currentStep != TutorialStep.Tablet_PaintWall && currentStep != TutorialStep.Tablet_PaintCube)
        {
            return false;
        }

        if (featureName == "SpawnCube")
        {
            if (currentStep == TutorialStep.Tablet_SpawnCube ||
                currentStep == TutorialStep.Tablet_MoveCube ||
                currentStep == TutorialStep.Tablet_PaintCube)
            {
                return true;
            }
            else
            {
                ShowWarning("Choose the Cube card first; that's our display stand.");
                return false;
            }
        }

        if (featureName == "SpawnFlower")
        {
            if (currentStep == TutorialStep.Tablet_SpawnProp ||
                currentStep == TutorialStep.Tablet_MovePropToCube)
            {
                return true;
            }
            else
            {
                ShowWarning("Choose the Floral Vase card. That's the product for this job.");
                return false;
            }
        }

        return true;
    }

    public bool CanUseComputerFeature(string featureName)
    {
        if (currentStep >= TutorialStep.OfferLevel1) return true;

        if (featureName == "RecordingsFolder" && currentStep != TutorialStep.OpenRecordingsFolder)
        {
            ShowWarning("Open Recordings first. That's where we'll find your take.");
            return false;
        }

        if (featureName == "VideoClip" && currentStep != TutorialStep.ClickVideoClip)
        {
            ShowWarning("Click your recorded clip so we can take a look.");
            return false;
        }

        if (featureName == "PlayVideo" && currentStep != TutorialStep.PlayVideoClip)
        {
            ShowWarning("Hit PLAY so we can watch the take.");
            return false;
        }

        if (featureName == "BackButton" && currentStep != TutorialStep.ClickBack)
        {
            ShowWarning("Let's finish watching this take before we move on.");
            return false;
        }

        if (featureName == "EditorApp" && currentStep != TutorialStep.ClickEditorApp)
        {
            ShowWarning("We're ready for the Editor app. Open it to start the edit.");
            return false;
        }

        if (featureName == "ConfirmEditor" && currentStep != TutorialStep.ClickConfirmEditor)
        {
            ShowWarning("Click CONFIRM when you're ready to take this footage into editing.");
            return false;
        }

        return true;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
