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

    private float spacebarCooldown = 0f;
    private bool wasJumpHeld = false;

    [Header("Game Explanation Dialogue")]
    private string[] explanationPages = new string[]
    {
        "Here is how things work. Your job is to produce <color=red>top-tier video commercials.</color>",
        "You will accept a <color=red>contract</color> from the client. This contract serves as your primary guide, containing all the specific details and requirements you must <color=red>follow</color> for the project.",
        "It is crucial to follow these instructions precisely. Your success and the <color=red>final payout</color> depend entirely on how accurately you execute the client's specific criteria."
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
            StartCampaignLevel(currentLevel);
            return;
        }

        if (currentLevel == 3)
        {
            currentStep = TutorialStep.Level1Accepted;
            StartLevel3();
            return;
        }

        if (currentLevel == 2)
        {
            currentStep = TutorialStep.Level1Accepted;
            StartGokeLevel();
            return;
        }
        else if (progress == 1) { StartCoroutine(StartPostEditTutorial()); return; }

        StartCoroutine(StartTutorialWithDelay());
    }

    private void Update()
    {// --- ADD THIS TO FIX THE POINT C CAMERA TRIGGER ---
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
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
            objectiveLine.SetPosition(0, playerTransform.position + (Vector3.up * lineHeightOffset));

            // Point 1: The Target Objective
            objectiveLine.SetPosition(1, lineTarget.position + (Vector3.up * lineHeightOffset));
        }
        // -----------------------------------------------
        UpdatePlacementChecks();

        if (keyboard != null && keyboard.f12Key.wasPressedThisFrame)
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        if (keyboard != null && keyboard.f9Key.wasPressedThisFrame)
        {
            SpawnCheatSDCard();
        }

        bool canAdvanceCampaignDialogue = CanAdvanceCampaignDialogue();

        if (spacePromptText != null)
        {
            bool canShowPrompt = !isTaskPhaseActive && !isTransitioning && (Time.time >= spacebarCooldown) && (currentStep != TutorialStep.WaitForPrompt) && canAdvanceCampaignDialogue;
            spacePromptText.gameObject.SetActive(canShowPrompt);
        }

        bool isJumpCurrentlyHeld = (pInput != null && pInput.Jump) || spaceHeld;

        bool jumpJustPressed = (pInput != null && pInput.Continue) || (isJumpCurrentlyHeld && !wasJumpHeld);
        wasJumpHeld = isJumpCurrentlyHeld;

        if (jumpJustPressed && !isTransitioning)
        {
            if (Time.time >= spacebarCooldown)
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
            if (mouse != null && mouse.scroll.ReadValue().y != 0) { cameraZoomed = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.PracticeCameraPedestal, true)); }
        }

        if (currentStep == TutorialStep.PracticeCameraPedestal && isTaskPhaseActive && !cameraPedestalMoved)
        {
            if (keyboard != null && (keyboard.qKey.isPressed || keyboard.eKey.isPressed)) { cameraPedestalMoved = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.FrameSubject, true)); }
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

                if (hDist <= 3.5f)
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

                if (hDist <= 4.0f && vDist <= 6.0f)
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

    public void OnPropPlaced(GameObject placedObject)
    {
        if (placedObject == null) return;

        string propName = placedObject.name.ToLower();
        if (propName.Contains("cube")) tutorialCube = placedObject;
        else if (propName.Contains("flower") || propName.Contains("floral")) tutorialFlower = placedObject;
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

    private IEnumerator StartTutorialWithDelay() { yield return new WaitForSeconds(1f); currentStep = TutorialStep.Intro; UpdateBossDialogue(); }
    private IEnumerator StartLevel1RetryWithDelay() { yield return new WaitForSeconds(1f); isLevel1Retry = true; currentStep = TutorialStep.SetTrainingObjectAndMoney; UpdateBossDialogue(); }
    private IEnumerator StartPostEditTutorial()
    {
        yield return new WaitForSeconds(1.5f);
        if (AlmanacManager.Instance != null) AlmanacManager.Instance.UnlockLevel1Knowledge();
        currentStep = TutorialStep.PostEditComplete;
        UpdateBossDialogue();
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
        yield return new WaitForSecondsRealtime(1.5f);
        TutorialUIManager.Instance.HideBossDialogue();
        RestoreTaskPanelAfterWarning();
        warningCoroutine = null;
    }

    private IEnumerator ShowTimedBossWarning(string warningMessage, float duration)
    {
        TutorialUIManager.Instance.ShowBossDialogue(warningMessage, TutorialUIManager.Instance.poseBoss, false, false);
        yield return new WaitForSecondsRealtime(duration);
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
            int budgetToAdd = 10000;

            if (isLevel1Retry)
            {
                int savedMoney = PlayerPrefs.GetInt("PlayerMoney", 0);
                budgetToAdd = Mathf.Max(0, 10000 - savedMoney);
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
                TutorialUIManager.Instance.SetupTasks(new string[] { "Click <color=red>'Accept'</color> on the contract panel" });
                if (firstContractPanel != null) firstContractPanel.SetActive(true);
                if (TutorialHighlighter.Instance != null) TutorialHighlighter.Instance.HighlightElement(acceptContractButtonRect);
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
                TutorialUIManager.Instance.SetupTasks(new string[] { "Set <color=red>Red</color> to ~255", "Set <color=green>Green</color> to 0", "Set <color=blue>Blue</color> to ~150" });
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
                TutorialUIManager.Instance.SetupTasks(new string[] { "Set <color=red>Red</color> to ~255", "Set <color=green>Green</color> to 0", "Set <color=blue>Blue</color> to ~150" });
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
                TutorialUIManager.Instance.SetupTasks(new string[] { "Press <color=red>[E]</color> or <color=red>[ESC]</color> to close the terminal" });
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
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Press <color=red>[E]</color> or <color=red>[ESC]</color> to close the terminal" });
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

            case TutorialStep.PracticeCameraZoom: TutorialUIManager.Instance.SetupTasks(new string[] { "- Use <color=red>[Scroll Wheel]</color> to zoom the lens in and out" }); cameraZoomed = false; break;
            case TutorialStep.PracticeCameraPedestal: TutorialUIManager.Instance.SetupTasks(new string[] { "- Hold <color=red>[Q]</color> or <color=red>[E]</color> to shift the camera height" }); cameraPedestalMoved = false; break;
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
        yield return new WaitForSeconds(duration);
        if (currentStep == TutorialStep.PracticeLight_Intensity || currentStep == TutorialStep.PracticeLight_Tilt)
        {
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(nextStep, true));
        }
    }

    private IEnumerator TransitionToNextStep(TutorialStep nextStep, bool didTaskJustComplete)
    {
        if (isTransitioning) yield break;
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

    public void OnTabletOpened() { if (currentStep == TutorialStep.BuildStageWall && isTaskPhaseActive && !tabletOpened) { tabletOpened = true; TutorialUIManager.Instance.MarkTaskComplete(0); TutorialUIManager.Instance.SetDynamicGlow("director", false); StartCoroutine(TransitionToNextStep(TutorialStep.ExplainDirectorTablet, true)); } }

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

    public void CheckWallColor(float rValue, float gValue, float bValue)
    {
        if (currentStep == TutorialStep.Tablet_PaintWall && isTaskPhaseActive && !wallColorChanged)
        {
            if (rValue >= 245f && rValue <= 255f && gValue <= 10f && bValue >= 140f && bValue <= 160f)
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
            if (rValue >= 245f && rValue <= 255f && gValue <= 10f && bValue >= 140f && bValue <= 160f)
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

        return true;
    }

    public void OnLightTurnedOn(Player.Equipment.FilmLightItem light = null)
    {
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
        {
            GokeLevelManager.Instance.OnLightTurnedOn(light);
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
        if (currentStep == TutorialStep.AdjustLight_Tilt && isTaskPhaseActive)
        {
            if (Mathf.RoundToInt(tilt) == -5)
            {
                TutorialUIManager.Instance.MarkTaskComplete(0);
                StartCoroutine(TransitionToNextStep(TutorialStep.DropLight, true));
            }
        }
    }

    public void OnLightDropped(Player.Equipment.FilmLightItem light = null)
    {
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
        {
            GokeLevelManager.Instance.OnLightDropped(light);
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
            StartCoroutine(TransitionToNextStep(TutorialStep.PickUpUsedSDCard, true));
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
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        LockPlayer();

        spacebarCooldown = Time.time + 0.2f;

        if (spacePromptText != null) spacePromptText.gameObject.SetActive(false);

        var ui = TutorialUIManager.Instance;

        switch (currentStep)
        {
            case TutorialStep.Intro: ui.ShowBossDialogue("Welcome to Crew On Set Studio. I'm your Boss, and I am going to teach you the absolute pure basics of video commercial production.", ui.poseHappy, true, true); break;
            case TutorialStep.WaitForPrompt: ui.ShowBossDialogue("Do you need a walkthrough of the basics?\n<color=red>[SPACE]</color> to learn, or <color=red>[TAB]</color> to skip", ui.posePoint, true, true); break;
            case TutorialStep.LearnMovement: ui.ShowBossDialogue("Use <color=red>[W, A, S, D]</color> to walk, <color=red>[Space]</color> to jump, and <color=red>[SHIFT]</color> to sprint. Getting comfortable moving around your set is crucial for finding the best camera angles later!", ui.posePoint, true, false); break;
            case TutorialStep.GameExplanation: ui.ShowBossDialogue(explanationPages[currentExplanationPage], ui.poseBoss, true, true); break;
            case TutorialStep.OfferFirstContract: ui.ShowBossDialogue("Here is the Artisan Flower Vase brief. Build a Pink set, center and fully frame the flower, use one controlled Stage Light, and deliver a precise 10-second edit with two title-safe graphics. Finish with a balanced grade that preserves detail instead of crushing shadows or oversaturating the Pink palette. Review the contract, then click ACCEPT.", ui.poseOpenHand, true, false); break;
            case TutorialStep.SetTrainingObjectAndMoney:
                if (isLevel1Retry)
                    ui.ShowBossDialogue("The first submission missed the client's requirements, but the contract is still active. I restored your working budget up to <color=yellow>10000 B-Coins</color>. Rebuild the set, record a stronger take, and use the grading feedback to fix every mistake.", ui.poseBoss, true, false);
                else
                    ui.ShowBossDialogue("Great. Here's your 10000 B-Coins. I've also unlocked the Floral Vase prop for your first commercial. Follow the production steps carefully and finish the contract first.", ui.poseSmile, true, true);
                break;

            case TutorialStep.ShowPreProductionTitle:
                if (preProductionTitleCard != null) StartCoroutine(FadeTitleCardSequence(preProductionTitleCard, TutorialStep.ExplainPreProduction));
                else StartCoroutine(TransitionToNextStep(TutorialStep.ExplainPreProduction, false));
                break;

            case TutorialStep.ExplainPreProduction:
                ui.ShowBossDialogue("Pre-Production is all about preparation. Before we even touch a camera, we must build the physical set and arrange our props.", ui.poseBoss, true, true);
                break;

            case TutorialStep.BuildStageWall: ui.ShowBossDialogue("As a Director, you use the Director's Tablet to instantly spawn and paint walls, saving hours of physical labor. Press <color=red>[E]</color> on the tablet to open it.", ui.poseOpenHand, true, false); break;

            case TutorialStep.ExplainDirectorTablet: ui.ShowBossDialogue("Our goal is an S-Rank video. The client wants a Floral arrangement in front of a Pink background.", ui.posePointUp, true, true); break;

            case TutorialStep.Tablet_AddWall: ui.ShowBossDialogue("First, let's build the physical set. Click the <color=red>'ADD WALL'</color> button on your tablet to spawn the stage backdrop.", ui.poseOpenHand, true, false); break;

            case TutorialStep.Tablet_SelectWall: ui.ShowBossDialogue("Before we can paint the wall, you must select it. Click directly on the stage backdrop to select it.", ui.posePoint, true, false); break;

            case TutorialStep.Tablet_PaintWall: ui.ShowBossDialogue("Good. Now we need to match the client's brand guidelines. Adjust the color sliders until the wall is a vibrant <color=red>Pink</color>. Max out Red, drop Green to 0, and bring Blue to around 150.", ui.posePointUp, true, false); break;

            case TutorialStep.Tablet_SpawnCube: ui.ShowBossDialogue("Next, we need a surface for the flower. Spawn a Cube to act as our table top.", ui.poseOpenHand, true, false); break;
            case TutorialStep.Tablet_MoveCube: ui.ShowBossDialogue("Pick up the cube and drag it to the placement marker on the stage.", ui.posePoint, true, false); break;

            case TutorialStep.Tablet_PaintCube: ui.ShowBossDialogue("Now Paint the cube to match the Pink wall. Set <color=red>Red</color> to ~255, <color=green>Green</color> to 0, and <color=blue>Blue</color> to ~150.", ui.posePointUp, true, false); break;

            case TutorialStep.Tablet_SpawnProp: ui.ShowBossDialogue("Now click the 'Prop' button to spawn the Floral arrangement.", ui.poseOpenHand, true, false); break;
            case TutorialStep.Tablet_MovePropToCube: ui.ShowBossDialogue("Proper staging is everything. Move the flower over the pink cube, then click to place it directly ON TOP.", ui.poseBoss, true, false); break;

            case TutorialStep.TabletPracticeFinished: ui.ShowBossDialogue("Perfect set design. Take your time arranging it. You can move props you've already placed by selecting them and pressing <color=red>[T]</color>. When you are happy with the background, close the tablet so we can move on to the one of the most important part of filming: Lighting.", ui.poseChill, true, true); break;

            case TutorialStep.BuyLight_WalkToShop: ui.ShowBossDialogue("Let's light the set. Walk to the Equipments Shop and press <color=red>[E]</color> to interact.", ui.posePoint, true, false); break;
            case TutorialStep.BuyLight_AddToCart: ui.ShowBossDialogue("Find the Stage Light in the shop menu and click the 'ADD TO CART’ button.", ui.poseOpenHand, true, false); break;
            case TutorialStep.BuyLight_Checkout: ui.ShowBossDialogue("Good. Now click the CONFIRM button to process the transaction and get your gear.", ui.poseSmile, true, false); break;
            case TutorialStep.BuyLight_CloseShop: ui.ShowBossDialogue("Purchase complete! The item is at the delivery zone. Press <color=red>[E]</color> or <color=red>[ESC]</color> to close the terminal.", ui.poseHappy, true, false); break;

            case TutorialStep.PickUpLight: ui.ShowBossDialogue("The shop delivered your light to the table. Walk over and press <color=red>[E]</color> to pick it up.", ui.posePoint, true, false); break;

            case TutorialStep.WalkToStageWithLight: ui.ShowBossDialogue("Now carry that light over to the Pink Stage you built.", ui.posePointUp, true, false); break;

            case TutorialStep.TurnOnLight: ui.ShowBossDialogue("Place it facing the flower, and click <color=red>[Left Mouse Button]</color> to turn it on.", ui.posePointUp, true, false); break;

            case TutorialStep.PracticeLight_Intensity: ui.ShowBossDialogue("Basic Lighting: First, use the <color=red>[Scroll Wheel]</color> to play around with the brightness. Give it a try!", ui.poseSmile, true, false); break;
            case TutorialStep.AdjustLight_Intensity: ui.ShowBossDialogue("Alright, enough playing. The client requested exactly 45% brightness. Use the <color=red>[Scroll Wheel]</color> to set your light intensity to 45%.", ui.posePointUp, true, false); break;
            case TutorialStep.PracticeLight_Tilt: ui.ShowBossDialogue("Now for the tilt. Use the <color=red>[Up/Down Arrows]</color> to tilt the light stand up and down. Try it out.", ui.poseBoss, true, false); break;
            case TutorialStep.AdjustLight_Tilt: ui.ShowBossDialogue("The client wants a slight upward angle. Use your <color=red>[Up/Down Arrows]</color> to set the tilt to exactly -5°.", ui.poseBoss, true, false); break;

            case TutorialStep.DropLight: ui.ShowBossDialogue("Perfect. We don't need to carry the light anymore. Press <color=red>[G]</color> to drop it on the floor.", ui.poseHappy, true, false); break;

            case TutorialStep.ShowProductionTitle:
                if (productionTitleCard != null) StartCoroutine(FadeTitleCardSequence(productionTitleCard, TutorialStep.ExplainProduction));
                else StartCoroutine(TransitionToNextStep(TutorialStep.ExplainProduction, false));
                break;

            case TutorialStep.ExplainProduction:
                ui.ShowBossDialogue("Now we enter the Production phase. The stage is set and lit. This is where we break out the camera, frame our shot, and actually record the footage.", ui.poseBoss, true, true);
                break;

            case TutorialStep.BuyCamera_WalkToShop: ui.ShowBossDialogue("Go back to the Equipment Shop. We need to buy a Camera to actually record this scene.", ui.poseOpenHand, true, false); break;
            case TutorialStep.BuyCamera_AddToCart: ui.ShowBossDialogue("First, let's grab a camera. Click 'Add to Cart' under the Film Camera.", ui.posePoint, true, false); break;
            case TutorialStep.BuySDCard_AddToCart: ui.ShowBossDialogue("A camera is useless without memory to record on. Click 'Add to Cart' under the SD Card.", ui.poseBoss, true, false); break;
            case TutorialStep.BuyCamera_Checkout: ui.ShowBossDialogue("Perfect. Now click CONFIRM to finalize your purchase.", ui.poseSmile, true, false); break;
            case TutorialStep.BuyCamera_CloseShop: ui.ShowBossDialogue("Gear delivered! Press <color=red>[E]</color> or <color=red>[ESC]</color> to close the shop.", ui.poseHappy, true, false); break;

            case TutorialStep.PickUpCamera: ui.ShowBossDialogue("The shop delivered your gear. Grab the Film Camera from the table first.", ui.posePoint, true, false); break;
            case TutorialStep.PickUpSDCard: ui.ShowBossDialogue("Good. Now grab the SD Card. A camera without memory is just an expensive brick.", ui.poseOpenHand, true, false); break;
            case TutorialStep.InsertSDCard: ui.ShowBossDialogue("While holding your camera, press <color=red>[C]</color> to insert the SD card so we can save our video files.", ui.poseBoss, true, false); break;
            case TutorialStep.WalkToStageWithCamera: ui.ShowBossDialogue("Perfect. Now walk over to Point C, the Director's mark, so we can frame our shot.", ui.posePointUp, true, false); break;

            case TutorialStep.EquipCameraView: ui.ShowBossDialogue("Hold the camera and click <color=red>[Left Mouse Button]</color> to look through the Director's Viewfinder. This frames the world exactly how the audience will see it.", ui.poseHappy, true, false); break;

            case TutorialStep.PracticeCameraZoom: ui.ShowBossDialogue("Use the <color=red>[Scroll Wheel]</color> to zoom your lens. Zooming in compresses the background and focuses the audience's attention entirely on the flower.", ui.posePointUp, true, false); break;
            case TutorialStep.PracticeCameraPedestal: ui.ShowBossDialogue("Hold <color=red>[Q]</color> or <color=red>[E]</color> to shift the camera up and down. Changing the camera height completely changes the psychology of the shot.", ui.posePoint, true, false); break;
            case TutorialStep.FrameSubject: ui.ShowBossDialogue("For this test, I want you to keep the flower perfectly dead-center in the frame.", ui.posePointUp, true, false); break;

            case TutorialStep.RecordVideo: ui.ShowBossDialogue("Press <color=red>[R]</color> to record. You MUST record for exactly 10 seconds, and KEEP the subject perfectly centered the entire time. Do NOT move the camera!", ui.poseBoss, true, false); break;

            case TutorialStep.PickUpUsedSDCard: ui.ShowBossDialogue("That's a wrap! The camera automatically ejected your tape. Press <color=red>[E]</color> to pick up the used SD card.", ui.poseHappy, true, false); break;
            case TutorialStep.InsertToComputer: ui.ShowBossDialogue("Walk over to the editing bay. Hold the SD card in your hand and press <color=red>[F]</color> on the computer tower to insert it.", ui.posePoint, true, false); break;
            case TutorialStep.OpenComputer: ui.ShowBossDialogue("Tape inserted successfully. Now press <color=red>[E]</color> on the monitor to log into the computer.", ui.poseBoss, true, false); break;

            case TutorialStep.ExplainComputerEditor: ui.ShowBossDialogue("Welcome to the Editing Bay. This computer is used to review the tapes we just ingested to make sure the lighting and framing were actually good.", ui.poseOpenHand, true, true); break;

            case TutorialStep.OpenRecordingsFolder: ui.ShowBossDialogue("Click the 'Recordings' folder to open the file browser and see the footage you just ingested.", ui.posePoint, true, false); break;
            case TutorialStep.ClickVideoClip: ui.ShowBossDialogue("Click the raw video clip you just recorded so we can review the take.", ui.poseBoss, true, false); break;
            case TutorialStep.PlayVideoClip: ui.ShowBossDialogue("Now click Play. Notice how the center framing and 10-second duration make the shot look professional.", ui.poseSmile, true, false); break;
            case TutorialStep.ClickBack: ui.ShowBossDialogue("Good review. We know the footage is safe. Now click the 'Close' or 'Back' button to return to the main menu.", ui.posePointUp, true, false); break;
            case TutorialStep.ClickEditorApp: ui.ShowBossDialogue("Now that we have verified the footage, click the 'Editor' application to proceed.", ui.poseBoss, true, false); break;
            case TutorialStep.ClickConfirmEditor: ui.ShowBossDialogue("Click 'Confirm' to send this footage to Post-Production. Be warned: Once you confirm, you leave the Studio and you CANNOT go back!", ui.poseOpenHand, true, false); break;

            case TutorialStep.Complete: ui.ShowBossDialogue("Raw footage submitted! Loading the Editor...", ui.poseEndWave, false, false); break;

            case TutorialStep.PostEditComplete: ui.ShowBossDialogue("Video successfully rendered! You finished your first commercial and unlocked the <color=yellow>Production Almanac</color>. Press <color=red>[P]</color> whenever you want to review equipment features, controls, and every production technique you have learned.", ui.poseHappy, true, true); break;

            case TutorialStep.OfferLevel1: ui.ShowBossDialogue("Your first commercial is complete, and I've got your next challenge ready. Want to hear what's next?", ui.poseHappy, true, true); break;

        }
    }

    private void SpawnCheatSDCard()
    {
        if (sdCardPrefab == null)
        {
            ShowWarning("Cheat Failed: SD Card Prefab is missing in TutorialManager!");
            return;
        }

        if (playerTransform == null)
        {
            ShowWarning("Cheat Failed: Cannot find player to spawn card in front of!");
            return;
        }

        string dummyFileName = "Cheat_Footage_" + Random.Range(1000, 9999) + ".tape";
        string fullPath = System.IO.Path.Combine(Application.persistentDataPath, dummyFileName);

        try
        {
            using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(new System.IO.FileStream(fullPath, System.IO.FileMode.Create)))
            {
                writer.Write((int)1);

                Texture2D tex = new Texture2D(16, 16, TextureFormat.RGB24, false);
                Color[] pixels = new Color[16 * 16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.red;
                tex.SetPixels(pixels);
                tex.Apply();

                byte[] bytes = tex.EncodeToJPG(50);
                writer.Write((int)bytes.Length);
                writer.Write(bytes);
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

        ShowWarning("CHEAT ACTIVATED: Spawned a completed 10s SD Card!");

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
        if (objectType == "ShopTerminal") return currentStep == TutorialStep.BuyLight_WalkToShop || currentStep >= TutorialStep.BuyCamera_WalkToShop;
        if (objectType == "ComputerStation") return currentStep >= TutorialStep.InsertToComputer && currentStep <= TutorialStep.Complete;
        if (objectType == "HelpDesk") return currentStep >= TutorialStep.Level1Accepted;

        return true;
    }

    public bool CanBuyItem(int itemIndex)
    {
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
        {
            return GokeLevelManager.Instance.CanBuyItem(itemIndex);
        }

        if (currentStep >= TutorialStep.OfferLevel1) return true;

        if (currentStep < TutorialStep.BuyLight_WalkToShop) { ShowWarning("Follow your tasks first!"); return false; }

        if (currentStep >= TutorialStep.BuyLight_WalkToShop && currentStep <= TutorialStep.BuyLight_CloseShop && itemIndex != 1) { ShowWarning("Only add the Stage Light to your cart right now."); return false; }

        if (currentStep >= TutorialStep.PickUpLight && currentStep <= TutorialStep.DropLight) { ShowWarning("Learn to use the light and drop it before buying more gear."); return false; }

        if (currentStep == TutorialStep.BuyCamera_WalkToShop) { ShowWarning("Follow your tasks first!"); return false; }
        if (currentStep == TutorialStep.BuyCamera_AddToCart && itemIndex != 0) { ShowWarning("Add the Camera to your cart first!"); return false; }
        if (currentStep == TutorialStep.BuySDCard_AddToCart && itemIndex != 2) { ShowWarning("Now add the SD Card to your cart!"); return false; }
        if (currentStep == TutorialStep.BuyCamera_Checkout) { ShowWarning("You have everything you need. Click Checkout!"); return false; }

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
        if (currentStep < TutorialStep.RecordVideo) { ShowWarning("Don't start recording yet! Finish setting up the shot first."); return false; }
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
        if (currentStep >= TutorialStep.OfferLevel1) return true;

        if (uiType == "DirectorTerminal")
        {
            if (currentStep == TutorialStep.Tablet_AddWall) { ShowWarning("Add the wall to the stage before closing!"); return false; }
            if (currentStep == TutorialStep.Tablet_SelectWall) { ShowWarning("Click the wall to select it before closing!"); return false; }
            if (currentStep == TutorialStep.Tablet_PaintWall) { ShowWarning("Paint the background pink before closing!"); return false; }
            if (currentStep == TutorialStep.Tablet_SpawnCube) { ShowWarning("Spawn the Cube before closing!"); return false; }
            if (currentStep == TutorialStep.Tablet_MoveCube) { ShowWarning("Place the Cube on the center marker before closing!"); return false; }
            if (currentStep == TutorialStep.Tablet_PaintCube) { ShowWarning("Paint the Cube pink before closing!"); return false; }
            if (currentStep == TutorialStep.Tablet_SpawnProp) { ShowWarning("Spawn the Floral arrangement before closing!"); return false; }
            if (currentStep == TutorialStep.Tablet_MovePropToCube) { ShowWarning("Place the flower ON TOP of the cube before closing!"); return false; }
        }
        else if (uiType == "ShopTerminal")
        {
            if (currentStep == TutorialStep.BuyLight_WalkToShop || currentStep == TutorialStep.BuyLight_AddToCart || currentStep == TutorialStep.BuyLight_Checkout)
            {
                ShowWarning("Don't leave yet! Finish buying the Stage Light first.");
                return false;
            }
            if (currentStep == TutorialStep.BuyCamera_WalkToShop || currentStep == TutorialStep.BuyCamera_AddToCart || currentStep == TutorialStep.BuySDCard_AddToCart || currentStep == TutorialStep.BuyCamera_Checkout)
            {
                ShowWarning("Don't leave yet! Finish buying your camera gear first.");
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
                ShowWarning("Follow the on-screen tasks! Do not close the computer yet.");
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
            ShowWarning("Don't clear the stage! We need this setup.");
            return false;
        }

        if (featureName == "AddWall" && currentStep != TutorialStep.Tablet_AddWall)
        {
            ShowWarning("Please follow the tasks! You don't need to do that right now.");
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
                ShowWarning("Please select the Cube prop like the task says!");
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
                ShowWarning("Please select the Flower prop like the task says!");
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
            ShowWarning("Please click the 'Recordings' folder as instructed.");
            return false;
        }

        if (featureName == "VideoClip" && currentStep != TutorialStep.ClickVideoClip)
        {
            ShowWarning("Please click the video clip to review it.");
            return false;
        }

        if (featureName == "PlayVideo" && currentStep != TutorialStep.PlayVideoClip)
        {
            ShowWarning("Please click the Play button to review the footage.");
            return false;
        }

        if (featureName == "BackButton" && currentStep != TutorialStep.ClickBack)
        {
            ShowWarning("Please finish reviewing your video first!");
            return false;
        }

        if (featureName == "EditorApp" && currentStep != TutorialStep.ClickEditorApp)
        {
            ShowWarning("Please click the 'Editor' app to proceed.");
            return false;
        }

        if (featureName == "ConfirmEditor" && currentStep != TutorialStep.ClickConfirmEditor)
        {
            ShowWarning("Please click 'Confirm' to leave the studio.");
            return false;
        }

        return true;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
