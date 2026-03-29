using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("UI: Boss Dialogue")]
    public GameObject bossHUDCanvas;
    public TextMeshProUGUI bossText;
    public GameObject okButton;
    public GameObject skipButton;

    [Header("Boss 2D Poses")]
    public Image bossPortraitDisplay;
    public Sprite poseBoss;
    public Sprite poseChill;
    public Sprite poseEndWave;
    public Sprite poseHappy;
    public Sprite poseOpenHand;
    public Sprite posePointUp;
    public Sprite posePoint;
    public Sprite poseSmile;

    [Header("UI: Task Checklist")]
    public GameObject taskPanel;
    public GameObject taskOpenView;
    public GameObject taskClosedView;
    public GameObject newTaskNotification;

    public TextMeshProUGUI[] taskListTexts;
    public Color pendingColor = Color.white;
    public Color completedColor = Color.green;

    [Header("Spawning Setup")]
    public GameObject practiceCubePrefab;
    public GameObject level1FlowerPrefab;
    public Transform stageSpawnPoint;

    [Header("Skip Tutorial Starter Gear")]
    public GameObject cameraPrefab;
    public GameObject lightPrefab;
    public GameObject sdCardPrefab;
    public Transform deliveryZone;

    [Header("Tutorial Guidance Systems")]
    public TutorialGlowTarget directorTerminalGlow;
    public TutorialGlowTarget shopTerminalGlow;
    public TutorialGlowTarget computerGlow;
    public TutorialArrowGuide navigationArrow;

    public enum TutorialStep
    {
        Intro, LearnMovement, SetTrainingObjectAndMoney,
        BuildStageWall, ExplainDirectorTablet, PracticeDirectorTablet, TabletPracticeFinished, FreePlayDirectorTablet,
        BuyLights, PickUpLight, TurnOnLight, AdjustLight, BuyCameraAndCard, InsertCardToCamera,

        // --- NEW CAMERA STEPS ---
        EquipCameraView, PracticeCameraZoom, PracticeCameraPedestal, FrameSubject, RecordVideo,

        InsertToComputer, ExplainComputerEditor, PracticeComputerEditor,
        Complete, OfferLevel1, Level1Accepted
    }

    public TutorialStep currentStep;

    private bool isTransitioning = false;
    private bool isTaskPhaseActive = false;
    private int tutorialItemsBought = 0;
    private bool skippedTutorial = false;
    private bool isTaskUIExpanded = false;
    private Coroutine warningCoroutine;

    // Task Tracking Variables
    private bool moved = false, jumped = false, sprinted = false;
    private bool tabletOpened = false, wallBuilt = false, wallColorChanged = false, propSpawned = false, propMoved = false;
    private bool lightTilted = false, lightIntensityChanged = false;

    // --- NEW CAMERA TRACKERS ---
    private bool cameraViewEntered = false, cameraZoomed = false, cameraPedestalMoved = false, subjectFramed = false;

    private bool computerAccessed = false, videoDragged = false, videoExported = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (bossHUDCanvas != null) bossHUDCanvas.SetActive(false);
        if (taskPanel != null) taskPanel.SetActive(false);
        if (okButton != null) okButton.SetActive(false);
        if (skipButton != null) skipButton.SetActive(false);
        if (newTaskNotification != null) newTaskNotification.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Player.PlayerController.PlayerController player = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (player != null) player.canLook = true;

        StartCoroutine(StartTutorialWithDelay());
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab) && taskPanel != null && taskPanel.activeSelf)
        {
            isTaskUIExpanded = !isTaskUIExpanded;
            if (taskOpenView != null) taskOpenView.SetActive(isTaskUIExpanded);
            if (taskClosedView != null) taskClosedView.SetActive(!isTaskUIExpanded);

            if (isTaskUIExpanded && newTaskNotification != null) newTaskNotification.SetActive(false);
        }

        if (currentStep == TutorialStep.LearnMovement && isTaskPhaseActive)
        {
            if (!moved && (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D)))
            {
                moved = true;
                if (taskListTexts.Length > 0) taskListTexts[0].color = completedColor;
            }
            if (!jumped && Input.GetKeyDown(KeyCode.Space))
            {
                jumped = true;
                if (taskListTexts.Length > 1) taskListTexts[1].color = completedColor;
            }
            if (!sprinted && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            {
                sprinted = true;
                if (taskListTexts.Length > 2) taskListTexts[2].color = completedColor;
            }

            if (moved && jumped && sprinted && !isTransitioning)
            {
                isTaskPhaseActive = false;
                StartCoroutine(TransitionToNextStep(TutorialStep.SetTrainingObjectAndMoney, true));
            }
        }

        // --- NEW: TRACK CAMERA ZOOM ---
        if (currentStep == TutorialStep.PracticeCameraZoom && isTaskPhaseActive && !cameraZoomed)
        {
            if (Input.mouseScrollDelta.y != 0)
            {
                cameraZoomed = true;
                if (taskListTexts.Length > 0) taskListTexts[0].color = completedColor;
                StartCoroutine(TransitionToNextStep(TutorialStep.PracticeCameraPedestal, true));
            }
        }

        // --- NEW: TRACK CAMERA PEDESTAL (Q/E) ---
        if (currentStep == TutorialStep.PracticeCameraPedestal && isTaskPhaseActive && !cameraPedestalMoved)
        {
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E))
            {
                cameraPedestalMoved = true;
                if (taskListTexts.Length > 0) taskListTexts[0].color = completedColor;
                StartCoroutine(TransitionToNextStep(TutorialStep.FrameSubject, true));
            }
        }
    }

    private IEnumerator StartTutorialWithDelay()
    {
        yield return new WaitForSeconds(3f);
        currentStep = TutorialStep.Intro;
        UpdateBossDialogue();
    }

    private IEnumerator ShowNewTaskNotification()
    {
        if (newTaskNotification != null)
        {
            newTaskNotification.SetActive(true);
            yield return new WaitForSeconds(4f);
            if (newTaskNotification != null) newTaskNotification.SetActive(false);
        }
    }

    public bool CanBuyItem(int itemIndex)
    {
        if (currentStep < TutorialStep.BuyLights)
        {
            if (warningCoroutine != null) StopCoroutine(warningCoroutine);
            warningCoroutine = StartCoroutine(ShowBossWarning("Don't get ahead of yourself! We aren't ready to buy equipment yet. Follow your tasks!"));
            return false;
        }

        if (currentStep == TutorialStep.BuyLights && itemIndex != 1)
        {
            if (warningCoroutine != null) StopCoroutine(warningCoroutine);
            warningCoroutine = StartCoroutine(ShowBossWarning("Hold on! We only need to buy the Stage Light right now. Don't waste your B coins!"));
            return false;
        }

        if (currentStep == TutorialStep.PickUpLight || currentStep == TutorialStep.TurnOnLight || currentStep == TutorialStep.AdjustLight)
        {
            if (warningCoroutine != null) StopCoroutine(warningCoroutine);
            warningCoroutine = StartCoroutine(ShowBossWarning("You already bought the Light! Go pick it up and learn how to use it before buying anything else."));
            return false;
        }

        if (currentStep == TutorialStep.BuyCameraAndCard && itemIndex == 1)
        {
            if (warningCoroutine != null) StopCoroutine(warningCoroutine);
            warningCoroutine = StartCoroutine(ShowBossWarning("You already sorted the lighting! Focus on grabbing the Film Camera and the SD Card."));
            return false;
        }

        if (currentStep > TutorialStep.BuyCameraAndCard && currentStep < TutorialStep.OfferLevel1)
        {
            if (warningCoroutine != null) StopCoroutine(warningCoroutine);
            warningCoroutine = StartCoroutine(ShowBossWarning("You already have all the gear you need! Focus on finishing the recording tasks."));
            return false;
        }

        return true;
    }

    private IEnumerator ShowBossWarning(string warningMessage)
    {
        if (bossHUDCanvas != null) bossHUDCanvas.SetActive(true);
        if (bossText != null) bossText.text = warningMessage;
        SetBossPose(poseBoss);

        if (okButton != null) okButton.SetActive(false);
        if (skipButton != null) skipButton.SetActive(false);

        yield return new WaitForSeconds(3.5f);

        if (isTaskPhaseActive && bossHUDCanvas != null)
        {
            bossHUDCanvas.SetActive(false);
        }
    }

    public void SkipTutorial()
    {
        if (isTransitioning) return;

        if (skipButton != null) skipButton.SetActive(false);
        skippedTutorial = true;

        if (CareerManager.Instance != null)
        {
            CareerManager.Instance.playerMoney += 60000;
            CareerManager.Instance.UpdateMoneyUI();
        }

        currentStep = TutorialStep.OfferLevel1;
        UpdateBossDialogue();
    }

    public void OnOkButtonPressed()
    {
        if (isTransitioning) return;

        if (currentStep == TutorialStep.OfferLevel1)
        {
            if (CareerManager.Instance != null) CareerManager.Instance.AcceptJob("Crystal Blooms", 30000);
            currentStep = TutorialStep.Level1Accepted;
            UpdateBossDialogue();
            return;
        }

        if (currentStep == TutorialStep.Level1Accepted)
        {
            if (bossHUDCanvas != null) bossHUDCanvas.SetActive(false);
            PointArrowAt("");
            CleanUpStudio();

            if (taskPanel != null) taskPanel.SetActive(true);

            isTaskUIExpanded = false;
            if (taskOpenView != null) taskOpenView.SetActive(false);
            if (taskClosedView != null) taskClosedView.SetActive(true);
            StartCoroutine(ShowNewTaskNotification());

            foreach (var t in taskListTexts) if (t != null) t.gameObject.SetActive(false);

            SetupTaskText(0, "- Stage a nice backdrop");
            SetupTaskText(1, "- Record a tabletop teaser of the vase");
            SetupTaskText(2, "- Submit the video on the computer");

            if (level1FlowerPrefab != null && stageSpawnPoint != null) Instantiate(level1FlowerPrefab, stageSpawnPoint.position, stageSpawnPoint.rotation);

            if (skippedTutorial && deliveryZone != null)
            {
                if (cameraPrefab != null) Instantiate(cameraPrefab, deliveryZone.position + new Vector3(0, 0.5f, 0), deliveryZone.rotation);
                if (lightPrefab != null) Instantiate(lightPrefab, deliveryZone.position + new Vector3(0.5f, 0.5f, 0), deliveryZone.rotation);
                if (sdCardPrefab != null) Instantiate(sdCardPrefab, deliveryZone.position + new Vector3(-0.5f, 0.5f, 0), deliveryZone.rotation);
            }

            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            Player.PlayerController.PlayerController playerAccept = FindObjectOfType<Player.PlayerController.PlayerController>();
            if (playerAccept != null) playerAccept.canLook = true;
            return;
        }

        if (currentStep == TutorialStep.Intro) { StartCoroutine(TransitionToNextStep(TutorialStep.LearnMovement, false)); return; }
        if (currentStep == TutorialStep.SetTrainingObjectAndMoney)
        {
            if (CareerManager.Instance != null) { CareerManager.Instance.playerMoney += 60000; CareerManager.Instance.UpdateMoneyUI(); }
            if (practiceCubePrefab != null && stageSpawnPoint != null) Instantiate(practiceCubePrefab, stageSpawnPoint.position, stageSpawnPoint.rotation);
            StartCoroutine(TransitionToNextStep(TutorialStep.BuildStageWall, false));
            return;
        }
        if (currentStep == TutorialStep.ExplainDirectorTablet) { StartCoroutine(TransitionToNextStep(TutorialStep.PracticeDirectorTablet, false)); return; }
        if (currentStep == TutorialStep.TabletPracticeFinished) { StartCoroutine(TransitionToNextStep(TutorialStep.FreePlayDirectorTablet, false)); return; }
        if (currentStep == TutorialStep.ExplainComputerEditor) { StartCoroutine(TransitionToNextStep(TutorialStep.PracticeComputerEditor, false)); return; }
        if (currentStep == TutorialStep.Complete) { StartCoroutine(TransitionToNextStep(TutorialStep.OfferLevel1, false)); return; }

        StartTaskPhase();
    }

    private void StartTaskPhase()
    {
        if (bossHUDCanvas != null) bossHUDCanvas.SetActive(false);
        if (taskPanel != null) taskPanel.SetActive(true);

        isTaskUIExpanded = false;
        if (taskOpenView != null) taskOpenView.SetActive(false);
        if (taskClosedView != null) taskClosedView.SetActive(true);
        StartCoroutine(ShowNewTaskNotification());

        foreach (var t in taskListTexts) if (t != null) t.gameObject.SetActive(false);

        Player.PlayerController.PlayerController p = FindObjectOfType<Player.PlayerController.PlayerController>();

        if (currentStep == TutorialStep.PracticeDirectorTablet || currentStep == TutorialStep.PracticeComputerEditor || currentStep == TutorialStep.FreePlayDirectorTablet)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (p != null) p.canLook = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (p != null) p.canLook = true;
        }

        isTaskPhaseActive = true;

        switch (currentStep)
        {
            case TutorialStep.LearnMovement:
                SetupTaskText(0, "- Use [W,A,S,D] to move");
                SetupTaskText(1, "- Press [Space] to jump");
                SetupTaskText(2, "- Hold [Shift] to sprint");
                moved = false; jumped = false; sprinted = false;
                break;
            case TutorialStep.BuildStageWall:
                SetupTaskText(0, "- Look at the Tablet and press [E]");
                tabletOpened = false;
                if (directorTerminalGlow != null) { directorTerminalGlow.StartGlowing(); PointArrowAt(directorTerminalGlow.gameObject.name); }
                break;
            case TutorialStep.PracticeDirectorTablet:
                SetupTaskText(0, "- Click the 'Wall' button");
                SetupTaskText(1, "- Use sliders to paint the wall");
                SetupTaskText(2, "- Click 'Cube' to spawn a prop");
                SetupTaskText(3, "- Press [T] to pick it up and move it");
                wallBuilt = wallColorChanged = propSpawned = propMoved = false;
                break;
            case TutorialStep.FreePlayDirectorTablet:
                if (taskPanel != null) taskPanel.SetActive(false);
                break;
            case TutorialStep.BuyLights:
                SetupTaskText(0, "- Walk to Shop and press [E] to buy a Light");
                if (shopTerminalGlow != null) { shopTerminalGlow.StartGlowing(); PointArrowAt(shopTerminalGlow.gameObject.name); }
                break;
            case TutorialStep.PickUpLight:
                SetupTaskText(0, "- Walk up to the dropped Stage Light and press [E] to pick it up");
                SetDynamicGlow("light", true); PointArrowAt("light");
                break;
            case TutorialStep.TurnOnLight:
                SetupTaskText(0, "- Aim at the stage and press [F] to turn it on");
                SetDynamicGlow("light", false); PointArrowAt("");
                break;
            case TutorialStep.AdjustLight:
                SetupTaskText(0, "- Use [Up/Down Arrows] to tilt the light");
                SetupTaskText(1, "- Use [Scroll Wheel] to adjust brightness");
                lightTilted = false; lightIntensityChanged = false;
                break;

            case TutorialStep.BuyCameraAndCard:
                tutorialItemsBought = 0;
                SetupTaskText(0, "- Buy a Film Camera and SD Card (0/2)");
                if (shopTerminalGlow != null) { shopTerminalGlow.StartGlowing(); PointArrowAt(shopTerminalGlow.gameObject.name); }
                break;
            case TutorialStep.InsertCardToCamera:
                SetupTaskText(0, "- Pick up both the Camera and SD Card");
                SetupTaskText(1, "- Hold the Camera and press [C] to insert card");
                SetDynamicGlow("camera", true); SetDynamicGlow("sd", true); PointArrowAt("camera");
                break;

            // --- NEW CAMERA TASKS ---
            case TutorialStep.EquipCameraView:
                SetupTaskText(0, "- Press [F] to look through the camera lens");
                cameraViewEntered = false;
                break;
            case TutorialStep.PracticeCameraZoom:
                SetupTaskText(0, "- Use [Scroll Wheel] to zoom the lens in and out");
                cameraZoomed = false;
                break;
            case TutorialStep.PracticeCameraPedestal:
                SetupTaskText(0, "- Hold [Q] or [E] to shift the camera height");
                cameraPedestalMoved = false;
                break;
            case TutorialStep.FrameSubject:
                SetupTaskText(0, "- Aim at the practice cube until the HUD says [SUBJECT DETECTED]");
                subjectFramed = false;
                break;
            case TutorialStep.RecordVideo:
                SetupTaskText(0, "- Press [R] to record for a few seconds, then press [R] to stop");
                SetDynamicGlow("camera", true); PointArrowAt("camera");
                break;

            case TutorialStep.InsertToComputer:
                SetupTaskText(0, "- Press [E] on the card to pick it up");
                SetupTaskText(1, "- Press [E] on the computer tower to insert it");
                SetDynamicGlow("sd", true); if (computerGlow != null) computerGlow.StartGlowing(); PointArrowAt("computer");
                break;
            case TutorialStep.PracticeComputerEditor:
                SetupTaskText(0, "- Press [E] on the Monitor to open Editor");
                computerAccessed = videoDragged = videoExported = false;
                if (computerGlow != null) computerGlow.StartGlowing(); PointArrowAt("computer");
                break;
        }
    }

    private void SetupTaskText(int index, string text)
    {
        if (index < taskListTexts.Length && taskListTexts[index] != null)
        {
            taskListTexts[index].text = text;
            taskListTexts[index].color = pendingColor;
            taskListTexts[index].gameObject.SetActive(true);
        }
    }

    private IEnumerator TransitionToNextStep(TutorialStep nextStep, bool didTaskJustComplete)
    {
        if (isTransitioning) yield break;

        isTransitioning = true;
        isTaskPhaseActive = false;
        PointArrowAt("");

        if (didTaskJustComplete) yield return new WaitForSeconds(1.5f);

        if (bossHUDCanvas != null) bossHUDCanvas.SetActive(false);
        if (taskPanel != null) taskPanel.SetActive(false);

        yield return new WaitForSeconds(1.0f);

        currentStep = nextStep;
        UpdateBossDialogue();
        isTransitioning = false;

        if (currentStep == TutorialStep.Complete)
        {
            yield return new WaitForSeconds(3f);
            StartCoroutine(TransitionToNextStep(TutorialStep.OfferLevel1, false));
        }
    }

    public void OnTabletOpened()
    {
        if (currentStep == TutorialStep.BuildStageWall && isTaskPhaseActive && !tabletOpened && !isTransitioning)
        {
            tabletOpened = true;
            if (taskListTexts.Length > 0) taskListTexts[0].color = completedColor;
            if (directorTerminalGlow != null) directorTerminalGlow.StopGlowing();
            PointArrowAt("");
            StartCoroutine(TransitionToNextStep(TutorialStep.ExplainDirectorTablet, true));
        }
    }

    public void OnStageWallBuilt() { if (currentStep == TutorialStep.PracticeDirectorTablet && isTaskPhaseActive && !wallBuilt) { wallBuilt = true; if (taskListTexts.Length > 0) taskListTexts[0].color = completedColor; CheckTabletTasksComplete(); } }
    public void OnWallColorChanged() { if (currentStep == TutorialStep.PracticeDirectorTablet && isTaskPhaseActive && !wallColorChanged) { wallColorChanged = true; if (taskListTexts.Length > 1) taskListTexts[1].color = completedColor; CheckTabletTasksComplete(); } }
    public void OnPropSpawnedFromUI() { if (currentStep == TutorialStep.PracticeDirectorTablet && isTaskPhaseActive && !propSpawned) { propSpawned = true; if (taskListTexts.Length > 2) taskListTexts[2].color = completedColor; CheckTabletTasksComplete(); } }
    public void OnPropMovedWithT() { if (currentStep == TutorialStep.PracticeDirectorTablet && isTaskPhaseActive && !propMoved) { propMoved = true; if (taskListTexts.Length > 3) taskListTexts[3].color = completedColor; CheckTabletTasksComplete(); } }

    private void CheckTabletTasksComplete()
    {
        if (currentStep == TutorialStep.PracticeDirectorTablet && wallBuilt && wallColorChanged && propSpawned && propMoved && !isTransitioning)
        {
            StartCoroutine(TransitionToNextStep(TutorialStep.TabletPracticeFinished, true));
        }
    }

    public void OnTabletClosed()
    {
        if (currentStep == TutorialStep.FreePlayDirectorTablet && isTaskPhaseActive && !isTransitioning)
        {
            StartCoroutine(TransitionToNextStep(TutorialStep.BuyLights, false));
        }
    }

    public void OnEquipmentBought()
    {
        if (currentStep == TutorialStep.BuyLights && isTaskPhaseActive && !isTransitioning)
        {
            if (shopTerminalGlow != null) shopTerminalGlow.StopGlowing();
            if (taskListTexts.Length > 0) taskListTexts[0].color = completedColor;

            StartCoroutine(TransitionToNextStep(TutorialStep.PickUpLight, true));
        }
        else if (currentStep == TutorialStep.BuyCameraAndCard && isTaskPhaseActive && !isTransitioning)
        {
            tutorialItemsBought++;
            if (taskListTexts.Length > 0) taskListTexts[0].text = $"- Buy a Film Camera and SD Card ({tutorialItemsBought}/2)";
            if (tutorialItemsBought >= 2)
            {
                if (shopTerminalGlow != null) shopTerminalGlow.StopGlowing();
                if (taskListTexts.Length > 0) taskListTexts[0].color = completedColor;
                StartCoroutine(TransitionToNextStep(TutorialStep.InsertCardToCamera, true));
            }
        }
    }

    public void OnLightPickedUp()
    {
        if (currentStep == TutorialStep.PickUpLight && isTaskPhaseActive && !isTransitioning)
        {
            if (taskListTexts.Length > 0) taskListTexts[0].color = completedColor;
            StartCoroutine(TransitionToNextStep(TutorialStep.TurnOnLight, true));
        }
    }

    public void OnLightTurnedOn()
    {
        if (currentStep == TutorialStep.TurnOnLight && isTaskPhaseActive && !isTransitioning)
        {
            if (taskListTexts.Length > 0) taskListTexts[0].color = completedColor;
            StartCoroutine(TransitionToNextStep(TutorialStep.AdjustLight, true));
        }
    }

    public void OnLightTilted()
    {
        if (currentStep == TutorialStep.AdjustLight && isTaskPhaseActive && !lightTilted)
        {
            lightTilted = true;
            if (taskListTexts.Length > 0) taskListTexts[0].color = completedColor;
            CheckLightTasksComplete();
        }
    }

    public void OnLightIntensityChanged()
    {
        if (currentStep == TutorialStep.AdjustLight && isTaskPhaseActive && !lightIntensityChanged)
        {
            lightIntensityChanged = true;
            if (taskListTexts.Length > 1) taskListTexts[1].color = completedColor;
            CheckLightTasksComplete();
        }
    }

    private void CheckLightTasksComplete()
    {
        if (currentStep == TutorialStep.AdjustLight && lightTilted && lightIntensityChanged && !isTransitioning)
        {
            StartCoroutine(TransitionToNextStep(TutorialStep.BuyCameraAndCard, true));
        }
    }

    public void OnCardInsertedToCamera()
    {
        if (currentStep == TutorialStep.InsertCardToCamera && isTaskPhaseActive && !isTransitioning)
        {
            SetDynamicGlow("sd", false);
            SetDynamicGlow("camera", false);
            if (taskListTexts.Length > 0) taskListTexts[0].color = completedColor;
            if (taskListTexts.Length > 1) taskListTexts[1].color = completedColor;

            // Now transitions to EquipCameraView instead of Record!
            StartCoroutine(TransitionToNextStep(TutorialStep.EquipCameraView, true));
        }
    }

    // --- NEW CAMERA PINGS ---
    public void OnCameraViewEntered()
    {
        if (currentStep == TutorialStep.EquipCameraView && isTaskPhaseActive && !cameraViewEntered && !isTransitioning)
        {
            cameraViewEntered = true;
            if (taskListTexts.Length > 0) taskListTexts[0].color = completedColor;
            StartCoroutine(TransitionToNextStep(TutorialStep.PracticeCameraZoom, true));
        }
    }

    public void OnSubjectFramed()
    {
        if (currentStep == TutorialStep.FrameSubject && isTaskPhaseActive && !subjectFramed && !isTransitioning)
        {
            subjectFramed = true;
            if (taskListTexts.Length > 0) taskListTexts[0].color = completedColor;
            StartCoroutine(TransitionToNextStep(TutorialStep.RecordVideo, true));
        }
    }

    public void OnRecordingFinished()
    {
        if (currentStep == TutorialStep.RecordVideo && isTaskPhaseActive && !isTransitioning)
        {
            SetDynamicGlow("camera", false);
            if (taskListTexts.Length > 0) taskListTexts[0].color = completedColor;
            StartCoroutine(TransitionToNextStep(TutorialStep.InsertToComputer, true));
        }
    }

    public void OnCardInsertedToComputer()
    {
        if (currentStep == TutorialStep.InsertToComputer && isTaskPhaseActive && !isTransitioning)
        {
            SetDynamicGlow("sd", false);
            if (taskListTexts.Length > 0) taskListTexts[0].color = completedColor;
            if (taskListTexts.Length > 1) taskListTexts[1].color = completedColor;
            StartCoroutine(TransitionToNextStep(TutorialStep.ExplainComputerEditor, true));
        }
    }

    public void OnComputerAccessed()
    {
        if (currentStep == TutorialStep.PracticeComputerEditor && isTaskPhaseActive && !computerAccessed && !isTransitioning)
        {
            computerAccessed = true;
            if (taskListTexts.Length > 0) taskListTexts[0].color = completedColor;
            if (computerGlow != null) computerGlow.StopGlowing();
            PointArrowAt("");
            SetupTaskText(1, "- Drag your video clip to the Timeline");
            SetupTaskText(2, "- Click the 'Export' button");
        }
    }

    public void OnVideoDraggedToTimeline() { if (currentStep == TutorialStep.PracticeComputerEditor && isTaskPhaseActive && !videoDragged) { videoDragged = true; if (taskListTexts.Length > 1) taskListTexts[1].color = completedColor; CheckEditorTasksComplete(); } }
    public void OnVideoExported() { if (currentStep == TutorialStep.PracticeComputerEditor && isTaskPhaseActive && !videoExported) { videoExported = true; if (taskListTexts.Length > 2) taskListTexts[2].color = completedColor; CheckEditorTasksComplete(); } }

    private void CheckEditorTasksComplete()
    {
        if (computerAccessed && videoDragged && videoExported && !isTransitioning) StartCoroutine(TransitionToNextStep(TutorialStep.Complete, true));
    }

    private void SetBossPose(Sprite newPose)
    {
        if (bossPortraitDisplay != null && newPose != null)
        {
            bossPortraitDisplay.sprite = newPose;
        }
    }

    private void UpdateBossDialogue()
    {
        if (bossText == null) return;

        if (bossHUDCanvas != null) bossHUDCanvas.SetActive(true);
        if (taskPanel != null) taskPanel.SetActive(false);
        if (okButton != null) okButton.SetActive(currentStep != TutorialStep.Complete);
        if (skipButton != null) skipButton.SetActive(currentStep == TutorialStep.Intro);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Player.PlayerController.PlayerController p = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (p != null) p.canLook = false;

        switch (currentStep)
        {
            case TutorialStep.Intro:
                bossText.text = "Welcome to the studio! I am going to teach you exactly how everything works from scratch. First, let's make sure you can walk.";
                SetBossPose(poseHappy);
                break;
            case TutorialStep.LearnMovement:
                bossText.text = "Use the [W, A, S, D] keys on your keyboard to walk around. You can jump with [Space] and hold [Shift] to run.";
                SetBossPose(posePoint);
                break;
            case TutorialStep.SetTrainingObjectAndMoney:
                bossText.text = "Perfect. I've placed a practice cube on the empty stage, and wired 60,000 B coins to your account to buy tools.";
                SetBossPose(poseSmile);
                break;
            case TutorialStep.BuildStageWall:
                bossText.text = "Follow the arrow to the Editor Tablet. I've added your objectives to your Task List. You can press [Tab] anytime to view them! Press [E] on the tablet to turn it on.";
                SetBossPose(poseOpenHand);
                if (directorTerminalGlow != null) { directorTerminalGlow.StartGlowing(); PointArrowAt(directorTerminalGlow.gameObject.name); }
                break;
            case TutorialStep.ExplainDirectorTablet:
                bossText.text = "This tablet controls the physical stage. You click the white UI buttons to spawn walls and props. You can click any object to paint it using the sliders, or press [T] to pick it up.";
                SetBossPose(posePointUp);
                break;
            case TutorialStep.PracticeDirectorTablet:
                bossText.text = "Give it a try. Click the button to add a wall, paint it a new color, click to spawn a prop, and press [T] to move the prop somewhere else.";
                SetBossPose(poseBoss);
                break;
            case TutorialStep.TabletPracticeFinished:
                bossText.text = "Great job! Take your time to play around and set up the stage however you like. When you are ready to move on to lighting, just close the Editor Tablet.";
                SetBossPose(poseChill);
                break;
            case TutorialStep.BuyLights:
                bossText.text = "Now we need lighting. Follow the arrow to the Shop Terminal, press [E], and buy a Stage Light.";
                SetBossPose(posePoint);
                break;
            case TutorialStep.PickUpLight:
                bossText.text = "Great! The Stage Light just spawned on the delivery table. Walk over there and press [E] to pick it up.";
                SetBossPose(posePoint);
                break;
            case TutorialStep.TurnOnLight:
                bossText.text = "Nice! That is a portable Stage Light. It is perfect for highlighting your subject. Hold it in your hands, face the stage, and press [F] to turn the bulb on!";
                SetBossPose(poseOpenHand);
                break;
            case TutorialStep.AdjustLight:
                bossText.text = "You can also control the exact angle and brightness! Try using your Up and Down arrow keys to tilt the stand, and scroll your mouse wheel to dim or brighten the LEDs.";
                SetBossPose(posePointUp);
                break;
            case TutorialStep.BuyCameraAndCard:
                bossText.text = "We can't film without a camera. Go back to the Shop Terminal and buy a Film Camera and an SD Card.";
                SetBossPose(poseChill);
                break;
            case TutorialStep.InsertCardToCamera:
                bossText.text = "Press [E] to pick up the Camera, and [E] to pick up the SD card. With the camera in your hand, press [C] to insert the memory card.";
                SetBossPose(poseBoss);
                break;

            // --- NEW CAMERA DIALOGUE ---
            case TutorialStep.EquipCameraView:
                bossText.text = "Time to learn the camera! Hold it in your hands and press [F] to look through the viewfinder.";
                SetBossPose(poseHappy);
                break;
            case TutorialStep.PracticeCameraZoom:
                bossText.text = "You can change your lens focal length dynamically. Try using your mouse's [Scroll Wheel] to zoom in and out.";
                SetBossPose(posePointUp);
                break;
            case TutorialStep.PracticeCameraPedestal:
                bossText.text = "You can also adjust the camera's height without moving your body. Hold [Q] to lower the pedestal, and [E] to raise it.";
                SetBossPose(posePoint);
                break;
            case TutorialStep.FrameSubject:
                bossText.text = "This camera has an automatic tracking focus, but you need to frame your subject correctly. Aim at the practice cube until your HUD detects it.";
                SetBossPose(poseOpenHand);
                break;
            case TutorialStep.RecordVideo:
                bossText.text = "Perfect! The subject is in focus. Press [R] to start recording. Wait a few seconds to get a good clip, then press [R] again to cut the tape.";
                SetBossPose(poseSmile);
                break;

            case TutorialStep.InsertToComputer:
                bossText.text = "Great take! Press [E] on the card to take it out. Walk over to the Computer tower and press [E] to push the card in.";
                SetBossPose(poseHappy);
                break;
            case TutorialStep.ExplainComputerEditor:
                bossText.text = "Now for post-production. The computer screen is your editing bay. Your raw video clips will appear at the top, and your final timeline is at the bottom.";
                SetBossPose(poseOpenHand);
                break;
            case TutorialStep.PracticeComputerEditor:
                bossText.text = "Press [E] on the monitor. Click and drag your video clip down into the timeline strip. When it's in place, click 'Export' to finalize the video!";
                SetBossPose(posePoint);
                break;
            case TutorialStep.Complete:
                bossText.text = "Video successfully rendered! You just completed the entire studio pipeline. Great job!";
                SetBossPose(poseEndWave);
                if (okButton != null) okButton.SetActive(false);
                break;
            case TutorialStep.OfferLevel1:
                bossText.text = "Flora & Form Home just offered us 60,000 B coins for a 20-second tabletop teaser of their new artisan vase. Do you want the job?";
                SetBossPose(posePointUp);
                break;
            case TutorialStep.Level1Accepted:
                bossText.text = "I've wired your 30,000 B coins upfront payment. The vase is on the stage. Use the Tablet, Setup your Lights, Record, Edit, and Export to finish the job!";
                SetBossPose(poseHappy);
                break;
        }
    }

    private void SetDynamicGlow(string keyword, bool state)
    {
        TutorialGlowTarget[] glows = FindObjectsOfType<TutorialGlowTarget>();
        foreach (var g in glows) if (g.gameObject.name.ToLower().Contains(keyword.ToLower())) { if (state) g.StartGlowing(); else g.StopGlowing(); }
    }

    private void PointArrowAt(string keyword)
    {
        if (navigationArrow == null) return;
        if (string.IsNullOrEmpty(keyword)) { navigationArrow.PointAt(null); return; }

        TutorialGlowTarget[] glows = FindObjectsOfType<TutorialGlowTarget>();
        foreach (var g in glows) if (g.gameObject.name.ToLower().Contains(keyword.ToLower())) { navigationArrow.PointAt(g.transform); return; }
    }

    private void CleanUpStudio()
    {
        StageSetupManager stageManager = FindObjectOfType<StageSetupManager>();
        if (stageManager != null) stageManager.ClearStage();

        Player.Interactor.EquipmentInteractor inventory = FindObjectOfType<Player.Interactor.EquipmentInteractor>();
        if (inventory != null) inventory.DropAllEquipment();

        foreach (GameObject obj in FindObjectsOfType<GameObject>()) if (obj.name.Contains("Cube") || obj.name.Contains("Flower")) Destroy(obj);

        Transform dropSpot = deliveryZone != null ? deliveryZone : stageSpawnPoint;
        foreach (Player.Equipment.Equipment gear in FindObjectsOfType<Player.Equipment.Equipment>())
        {
            if (gear.GetComponent<Player.Equipment.SDCardItem>() != null) continue;
            if (dropSpot != null)
            {
                gear.transform.position = dropSpot.position + new Vector3(Random.Range(-0.3f, 0.3f), 0.5f, Random.Range(-0.3f, 0.3f));
                Rigidbody rb = gear.GetComponent<Rigidbody>();
                if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            }
        }
    }
}