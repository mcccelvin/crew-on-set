using System.Collections;
using UnityEngine;

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

    public enum TutorialStep
    {
        Intro, LearnMovement, SetTrainingObjectAndMoney,
        BuildStageWall, ExplainDirectorTablet,
        Tablet_PaintWall, Tablet_SpawnProp, Tablet_MoveProp,
        TabletPracticeFinished, FreePlayDirectorTablet,
        BuyLights, PickUpLight, TurnOnLight,
        AdjustLight_Intensity, AdjustLight_Tilt,
        BuyCameraAndCard, InsertCardToCamera,
        EquipCameraView, PracticeCameraZoom, PracticeCameraPedestal, FrameSubject, RecordVideo,
        InsertToComputer, ExplainComputerEditor,
        PracticeComputer_Play, PracticeComputer_Submit,
        Complete, PostEditComplete, OfferLevel1, Level1Accepted
    }

    public TutorialStep currentStep;
    private bool isTransitioning = false;
    private bool isTaskPhaseActive = false;
    private int tutorialItemsBought = 0;
    private bool skippedTutorial = false;
    private Coroutine warningCoroutine;

    private bool moved = false, jumped = false, sprinted = false;
    private bool tabletOpened = false, wallBuilt = false, wallColorChanged = false, propSpawned = false, propMoved = false;
    private bool lightTilted = false, lightIntensityChanged = false;
    private bool cameraViewEntered = false, cameraZoomed = false, cameraPedestalMoved = false, subjectFramed = false;
    private bool computerAccessed = false, videoPlayed = false, videoSubmitted = false;

    private void Awake() { if (Instance == null) Instance = this; else Destroy(gameObject); }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        Player.PlayerController.PlayerController player = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (player != null) player.canLook = true;

        int progress = PlayerPrefs.GetInt("TutorialProgress", 0);
        if (progress == 2) { FinishTutorialInstantly(); return; }
        else if (progress == 1) { StartCoroutine(StartPostEditTutorial()); return; }

        StartCoroutine(StartTutorialWithDelay());
    }

    private void Update()
    {
        if (currentStep == TutorialStep.LearnMovement && isTaskPhaseActive)
        {
            if (!moved && (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D))) { moved = true; TutorialUIManager.Instance.MarkTaskComplete(0); }
            if (!jumped && Input.GetKeyDown(KeyCode.Space)) { jumped = true; TutorialUIManager.Instance.MarkTaskComplete(1); }
            if (!sprinted && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) { sprinted = true; TutorialUIManager.Instance.MarkTaskComplete(2); }
            if (moved && jumped && sprinted && !isTransitioning) { isTaskPhaseActive = false; StartCoroutine(TransitionToNextStep(TutorialStep.SetTrainingObjectAndMoney, true)); }
        }

        if (currentStep == TutorialStep.PracticeCameraZoom && isTaskPhaseActive && !cameraZoomed)
        {
            if (Input.mouseScrollDelta.y != 0) { cameraZoomed = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.PracticeCameraPedestal, true)); }
        }

        if (currentStep == TutorialStep.PracticeCameraPedestal && isTaskPhaseActive && !cameraPedestalMoved)
        {
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E)) { cameraPedestalMoved = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.FrameSubject, true)); }
        }
    }

    private IEnumerator StartTutorialWithDelay() { yield return new WaitForSeconds(3f); currentStep = TutorialStep.Intro; UpdateBossDialogue(); }
    private IEnumerator StartPostEditTutorial() { yield return new WaitForSeconds(1.5f); currentStep = TutorialStep.PostEditComplete; UpdateBossDialogue(); }

    public void ShowWarning(string warningMessage)
    {
        if (warningCoroutine != null) StopCoroutine(warningCoroutine);
        warningCoroutine = StartCoroutine(ShowBossWarning(warningMessage));
    }

    private IEnumerator ShowBossWarning(string warningMessage)
    {
        TutorialUIManager.Instance.ShowBossDialogue(warningMessage, TutorialUIManager.Instance.poseBoss, false, false);
        yield return new WaitForSeconds(3.5f);
        if (isTaskPhaseActive) TutorialUIManager.Instance.HideBossDialogue();
    }

    public void SkipTutorial() { if (isTransitioning) return; skippedTutorial = true; currentStep = TutorialStep.OfferLevel1; UpdateBossDialogue(); }

    public void OnOkButtonPressed()
    {
        if (isTransitioning) return;

        if (currentStep == TutorialStep.PostEditComplete) { StartCoroutine(TransitionToNextStep(TutorialStep.OfferLevel1, false)); return; }

        if (currentStep == TutorialStep.OfferLevel1) { if (CareerManager.Instance != null) CareerManager.Instance.AcceptJob("Goke Cola", 60000); currentStep = TutorialStep.Level1Accepted; UpdateBossDialogue(); return; }

        if (currentStep == TutorialStep.Level1Accepted)
        {
            PlayerPrefs.SetInt("TutorialProgress", 2); PlayerPrefs.Save();
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.PointArrowAt("");

            CleanUpStudio();

            TutorialUIManager.Instance.SetupTasks(new string[] {
                "- Stage: RED backdrop & pull Cola away from wall",
                "- Camera: Use 'Rule of Thirds' composition",
                "- Light: Use 3-Point Lighting",
                "- Edit: 3 Graphics & high contrast!"
            });

            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            Player.PlayerController.PlayerController playerAccept = FindObjectOfType<Player.PlayerController.PlayerController>();
            if (playerAccept != null) playerAccept.canLook = true;
            return;
        }

        if (currentStep == TutorialStep.Intro) { StartCoroutine(TransitionToNextStep(TutorialStep.LearnMovement, false)); return; }

        if (currentStep == TutorialStep.SetTrainingObjectAndMoney)
        {
            if (CareerManager.Instance != null)
            {
                CareerManager.Instance.playerMoney += 5000;
                CareerManager.Instance.UpdateMoneyUI();
            }
            StartCoroutine(TransitionToNextStep(TutorialStep.BuildStageWall, false));
            return;
        }

        if (currentStep == TutorialStep.ExplainDirectorTablet) { StartCoroutine(TransitionToNextStep(TutorialStep.Tablet_PaintWall, false)); return; }
        if (currentStep == TutorialStep.TabletPracticeFinished) { currentStep = TutorialStep.FreePlayDirectorTablet; StartTaskPhase(); return; }
        if (currentStep == TutorialStep.ExplainComputerEditor) { StartCoroutine(TransitionToNextStep(TutorialStep.PracticeComputer_Play, false)); return; }
        if (currentStep == TutorialStep.Complete) { StartCoroutine(TransitionToNextStep(TutorialStep.OfferLevel1, false)); return; }

        StartTaskPhase();
    }

    private void FinishTutorialInstantly()
    {
        currentStep = TutorialStep.Level1Accepted;
        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Get to work on that Goke Cola contract! Use those MMA Syllabus techniques.", TutorialUIManager.Instance.poseBoss, true, false);

            TutorialUIManager.Instance.SetupTasks(new string[] {
                "- Stage: RED backdrop & pull Cola away from wall",
                "- Camera: Use 'Rule of Thirds' composition",
                "- Light: Use 3-Point Lighting",
                "- Edit: 3 Graphics & high contrast!"
            });
        }

        CleanUpStudio();

        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        Player.PlayerController.PlayerController playerAccept = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (playerAccept != null) playerAccept.canLook = false;
    }

    private void StartTaskPhase()
    {
        TutorialUIManager.Instance.HideBossDialogue();
        Player.PlayerController.PlayerController p = FindObjectOfType<Player.PlayerController.PlayerController>();

        if (currentStep == TutorialStep.Tablet_PaintWall || currentStep == TutorialStep.Tablet_SpawnProp || currentStep == TutorialStep.Tablet_MoveProp ||
            currentStep == TutorialStep.PracticeComputer_Play || currentStep == TutorialStep.PracticeComputer_Submit || currentStep == TutorialStep.FreePlayDirectorTablet)
        { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; if (p != null) p.canLook = false; }
        else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; if (p != null) p.canLook = true; }

        isTaskPhaseActive = true;

        switch (currentStep)
        {
            case TutorialStep.LearnMovement: TutorialUIManager.Instance.SetupTasks(new string[] { "- Use [W,A,S,D] to move", "- Press [Space] to jump", "- Hold [Shift] to sprint" }); moved = jumped = sprinted = false; break;
            case TutorialStep.BuildStageWall: TutorialUIManager.Instance.SetupTasks(new string[] { "- Look at the Tablet and press [E]" }); tabletOpened = false; TutorialUIManager.Instance.SetDynamicGlow("director", true); TutorialUIManager.Instance.PointArrowAt("director"); break;

            case TutorialStep.Tablet_PaintWall: TutorialUIManager.Instance.SetupTasks(new string[] { "- Use the Sliders to paint the wall PINK" }); wallColorChanged = false; break;
            case TutorialStep.Tablet_SpawnProp: TutorialUIManager.Instance.SetupTasks(new string[] { "- Click the 'Flower' Prop button to spawn it" }); propSpawned = false; break;
            case TutorialStep.Tablet_MoveProp: TutorialUIManager.Instance.SetupTasks(new string[] { "- Press [T] to pick up the prop and move it" }); propMoved = false; break;

            case TutorialStep.FreePlayDirectorTablet: if (TutorialUIManager.Instance.taskPanel != null) TutorialUIManager.Instance.taskPanel.SetActive(false); break;
            case TutorialStep.BuyLights: TutorialUIManager.Instance.SetupTasks(new string[] { "- Walk to Shop and press [E] to buy a Light" }); TutorialUIManager.Instance.SetDynamicGlow("shop", true); TutorialUIManager.Instance.PointArrowAt("shop"); break;
            case TutorialStep.PickUpLight: TutorialUIManager.Instance.SetupTasks(new string[] { "- Walk up to the dropped Stage Light and press [E] to pick it up" }); TutorialUIManager.Instance.SetDynamicGlow("light", true); TutorialUIManager.Instance.PointArrowAt("light"); break;
            case TutorialStep.TurnOnLight: TutorialUIManager.Instance.SetupTasks(new string[] { "- Aim at the stage and press [F] to turn it on" }); TutorialUIManager.Instance.SetDynamicGlow("light", false); TutorialUIManager.Instance.PointArrowAt(""); break;

            case TutorialStep.AdjustLight_Intensity: TutorialUIManager.Instance.SetupTasks(new string[] { "- Use the Mouse Scroll Wheel to adjust brightness" }); lightIntensityChanged = false; break;
            case TutorialStep.AdjustLight_Tilt: TutorialUIManager.Instance.SetupTasks(new string[] { "- Look Up/Down and left click to tilt the light" }); lightTilted = false; break;

            case TutorialStep.BuyCameraAndCard: tutorialItemsBought = 0; TutorialUIManager.Instance.SetupTasks(new string[] { "- Buy a Film Camera and SD Card (0/2)" }); TutorialUIManager.Instance.SetDynamicGlow("shop", true); TutorialUIManager.Instance.PointArrowAt("shop"); break;
            case TutorialStep.InsertCardToCamera: TutorialUIManager.Instance.SetupTasks(new string[] { "- Pick up both the Camera and SD Card", "- Hold the Camera and press [C] to insert card" }); TutorialUIManager.Instance.SetDynamicGlow("camera", true); TutorialUIManager.Instance.SetDynamicGlow("sd", true); TutorialUIManager.Instance.PointArrowAt("camera"); break;
            case TutorialStep.EquipCameraView: TutorialUIManager.Instance.SetupTasks(new string[] { "- Press [F] to look through the camera lens" }); cameraViewEntered = false; break;
            case TutorialStep.PracticeCameraZoom: TutorialUIManager.Instance.SetupTasks(new string[] { "- Use [Scroll Wheel] to zoom the lens in and out" }); cameraZoomed = false; break;
            case TutorialStep.PracticeCameraPedestal: TutorialUIManager.Instance.SetupTasks(new string[] { "- Hold [Q] or [E] to shift the camera height" }); cameraPedestalMoved = false; break;
            case TutorialStep.FrameSubject: TutorialUIManager.Instance.SetupTasks(new string[] { "- Aim at the prop until HUD says [SUBJECT DETECTED]" }); subjectFramed = false; break;
            case TutorialStep.RecordVideo: TutorialUIManager.Instance.SetupTasks(new string[] { "- Press [R] to record for a few seconds, then press [R] to stop" }); TutorialUIManager.Instance.SetDynamicGlow("camera", true); TutorialUIManager.Instance.PointArrowAt("camera"); break;
            case TutorialStep.InsertToComputer: TutorialUIManager.Instance.SetupTasks(new string[] { "- Press [E] on the card to pick it up", "- Hold the card and press [F] on the computer tower" }); TutorialUIManager.Instance.SetDynamicGlow("sd", true); TutorialUIManager.Instance.SetDynamicGlow("computer", true); TutorialUIManager.Instance.PointArrowAt("computer"); break;

            case TutorialStep.PracticeComputer_Play: TutorialUIManager.Instance.SetupTasks(new string[] { "- Click your video clip to play it" }); videoPlayed = false; TutorialUIManager.Instance.SetDynamicGlow("computer", true); TutorialUIManager.Instance.PointArrowAt("computer"); break;
            case TutorialStep.PracticeComputer_Submit: TutorialUIManager.Instance.SetupTasks(new string[] { "- Click 'Submit' to enter Post-Production" }); videoSubmitted = false; break;
        }
    }

    private IEnumerator TransitionToNextStep(TutorialStep nextStep, bool didTaskJustComplete)
    {
        if (isTransitioning) yield break;
        isTransitioning = true;
        isTaskPhaseActive = false;
        TutorialUIManager.Instance.PointArrowAt("");

        if (didTaskJustComplete) yield return new WaitForSeconds(1.5f);
        TutorialUIManager.Instance.HideBossDialogue();
        if (TutorialUIManager.Instance.taskPanel != null) TutorialUIManager.Instance.taskPanel.SetActive(false);

        yield return new WaitForSeconds(1.0f);
        currentStep = nextStep;
        UpdateBossDialogue();
        isTransitioning = false;
    }

    // --- TABLET TRIGGERS ---
    public void OnTabletOpened() { if (currentStep == TutorialStep.BuildStageWall && isTaskPhaseActive && !tabletOpened) { tabletOpened = true; TutorialUIManager.Instance.MarkTaskComplete(0); TutorialUIManager.Instance.SetDynamicGlow("director", false); StartCoroutine(TransitionToNextStep(TutorialStep.ExplainDirectorTablet, true)); } }
    public void OnWallColorChanged() { if (currentStep == TutorialStep.Tablet_PaintWall && isTaskPhaseActive && !wallColorChanged) { wallColorChanged = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.Tablet_SpawnProp, true)); } }
    public void OnPropSpawnedFromUI() { if (currentStep == TutorialStep.Tablet_SpawnProp && isTaskPhaseActive && !propSpawned) { propSpawned = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.Tablet_MoveProp, true)); } }
    public void OnPropMovedWithT() { if (currentStep == TutorialStep.Tablet_MoveProp && isTaskPhaseActive && !propMoved) { propMoved = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.TabletPracticeFinished, true)); } }
    public void OnTabletClosed() { if (currentStep == TutorialStep.FreePlayDirectorTablet && isTaskPhaseActive) StartCoroutine(TransitionToNextStep(TutorialStep.BuyLights, false)); }

    // --- LIGHT & SHOP TRIGGERS ---
    public void OnEquipmentBought(int itemsCount = 1)
    {
        if (currentStep == TutorialStep.BuyLights && isTaskPhaseActive)
        {
            TutorialUIManager.Instance.SetDynamicGlow("shop", false);
            TutorialUIManager.Instance.MarkTaskComplete(0);
            StartCoroutine(TransitionToNextStep(TutorialStep.PickUpLight, true));
        }
        else if (currentStep == TutorialStep.BuyCameraAndCard && isTaskPhaseActive)
        {
            tutorialItemsBought += itemsCount;
            int displayCount = Mathf.Min(tutorialItemsBought, 2);
            TutorialUIManager.Instance.SetupTasks(new string[] { $"- Buy a Film Camera and SD Card ({displayCount}/2)" });

            if (tutorialItemsBought >= 2)
            {
                TutorialUIManager.Instance.SetDynamicGlow("shop", false);
                TutorialUIManager.Instance.MarkTaskComplete(0);
                StartCoroutine(TransitionToNextStep(TutorialStep.InsertCardToCamera, true));
            }
        }
    }

    public void OnLightPickedUp() { if (currentStep == TutorialStep.PickUpLight && isTaskPhaseActive) { TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.TurnOnLight, true)); } }
    public void OnLightTurnedOn() { if (currentStep == TutorialStep.TurnOnLight && isTaskPhaseActive) { TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.AdjustLight_Intensity, true)); } }
    public void OnLightIntensityChanged() { if (currentStep == TutorialStep.AdjustLight_Intensity && isTaskPhaseActive && !lightIntensityChanged) { lightIntensityChanged = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.AdjustLight_Tilt, true)); } }
    public void OnLightTilted() { if (currentStep == TutorialStep.AdjustLight_Tilt && isTaskPhaseActive && !lightTilted) { lightTilted = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.BuyCameraAndCard, true)); } }

    // --- CAMERA TRIGGERS ---
    public void OnCardInsertedToCamera() { if (currentStep == TutorialStep.InsertCardToCamera && isTaskPhaseActive) { TutorialUIManager.Instance.SetDynamicGlow("sd", false); TutorialUIManager.Instance.SetDynamicGlow("camera", false); TutorialUIManager.Instance.MarkTaskComplete(0); TutorialUIManager.Instance.MarkTaskComplete(1); StartCoroutine(TransitionToNextStep(TutorialStep.EquipCameraView, true)); } }
    public void OnCameraViewEntered() { if (currentStep == TutorialStep.EquipCameraView && isTaskPhaseActive && !cameraViewEntered) { cameraViewEntered = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.PracticeCameraZoom, true)); } }
    public void OnSubjectFramed() { if (currentStep == TutorialStep.FrameSubject && isTaskPhaseActive && !subjectFramed) { subjectFramed = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.RecordVideo, true)); } }
    public void OnRecordingFinished() { if (currentStep == TutorialStep.RecordVideo && isTaskPhaseActive) { TutorialUIManager.Instance.SetDynamicGlow("camera", false); TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.InsertToComputer, true)); } }

    // --- COMPUTER TRIGGERS ---
    public void OnCardInsertedToComputer() { if (currentStep == TutorialStep.InsertToComputer && isTaskPhaseActive) { TutorialUIManager.Instance.SetDynamicGlow("sd", false); TutorialUIManager.Instance.MarkTaskComplete(0); TutorialUIManager.Instance.MarkTaskComplete(1); StartCoroutine(TransitionToNextStep(TutorialStep.ExplainComputerEditor, true)); } }
    public void OnComputerAccessed() { /* Intentionally left blank as a safe fallback */ }
    public void OnVideoPlayed() { if (currentStep == TutorialStep.PracticeComputer_Play && isTaskPhaseActive && !videoPlayed) { videoPlayed = true; TutorialUIManager.Instance.SetDynamicGlow("computer", false); TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.PracticeComputer_Submit, true)); } }
    public void OnVideoSubmitted() { if (currentStep == TutorialStep.PracticeComputer_Submit && isTaskPhaseActive && !videoSubmitted) { videoSubmitted = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.Complete, true)); } }

    private void UpdateBossDialogue()
    {
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        Player.PlayerController.PlayerController p = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (p != null) p.canLook = false;

        var ui = TutorialUIManager.Instance;
        switch (currentStep)
        {
            case TutorialStep.Intro: ui.ShowBossDialogue("Welcome to the Studio. I'm going to teach you the absolute pure basics of video production.", ui.poseHappy, true, true); break;
            case TutorialStep.LearnMovement: ui.ShowBossDialogue("Use [W, A, S, D] to walk, [Space] to jump, and [Shift] to sprint. Getting comfortable moving around your set is crucial for finding the best camera angles later!", ui.posePoint, true, false); break;
            case TutorialStep.SetTrainingObjectAndMoney: ui.ShowBossDialogue("Great. I've wired exactly 5000 B-Coins to your account. I've also unlocked the Floral Vase prop inside your Director Tablet's memory.", ui.poseSmile, true, false); break;
            case TutorialStep.BuildStageWall: ui.ShowBossDialogue("As a Director, you use the Editor Tablet to instantly spawn and paint walls, saving hours of physical labor. Press [E] on the tablet to open it.", ui.poseOpenHand, true, false); break;

            case TutorialStep.ExplainDirectorTablet: ui.ShowBossDialogue("Our goal is an S-Rank video. The client wants a Floral arrangement against a Pink background.", ui.posePointUp, true, false); break;
            case TutorialStep.Tablet_PaintWall: ui.ShowBossDialogue("Click the 'Wall' button to build a stage, then use the sliders to paint the background pink.", ui.poseSmile, true, false); break;
            case TutorialStep.Tablet_SpawnProp: ui.ShowBossDialogue("Now click the 'Prop' button to spawn the Floral arrangement.", ui.poseOpenHand, true, false); break;
            case TutorialStep.Tablet_MoveProp: ui.ShowBossDialogue("Proper staging is what gives a commercial its mood. Press [T] to pick up the prop and move it to the center.", ui.poseBoss, true, false); break;

            case TutorialStep.TabletPracticeFinished: ui.ShowBossDialogue("Perfect set design. Take your time arranging it. When you are happy with the background, close the tablet so we can move on to the most important part of filming: Lighting.", ui.poseChill, true, false); break;
            case TutorialStep.BuyLights: ui.ShowBossDialogue("Let's light the set. Go to the Shop Terminal and buy exactly ONE Light.", ui.posePoint, true, false); break;
            case TutorialStep.PickUpLight: ui.ShowBossDialogue("The shop delivered your light to the table. Walk over and press [E] to pick it up.", ui.posePoint, true, false); break;
            case TutorialStep.TurnOnLight: ui.ShowBossDialogue("Pick up the light, place it facing the flower, and click [LMB] to turn it on.", ui.posePointUp, true, false); break;

            case TutorialStep.AdjustLight_Intensity: ui.ShowBossDialogue("Basic Lighting: First, use the mouse scroll wheel to turn the brightness up. Make sure the light is actually hitting the front of the subject!", ui.poseSmile, true, false); break;
            case TutorialStep.AdjustLight_Tilt: ui.ShowBossDialogue("Now that it's bright, tilt the light so the harsh reflection goes away.", ui.poseBoss, true, false); break;

            case TutorialStep.BuyCameraAndCard: ui.ShowBossDialogue("Now go back to the Shop Terminal. Buy ONE Camera and ONE SD Card.", ui.poseOpenHand, true, false); break;
            case TutorialStep.InsertCardToCamera: ui.ShowBossDialogue("Pick up the Camera and the SD card. While holding the camera, press [C] to insert the memory card so we actually have somewhere to save our video files.", ui.poseBoss, true, false); break;
            case TutorialStep.EquipCameraView: ui.ShowBossDialogue("Hold the camera and press [F] to look through the Director's Viewfinder. This frames the world exactly how the audience will see it.", ui.poseHappy, true, false); break;
            case TutorialStep.PracticeCameraZoom: ui.ShowBossDialogue("Use the [Scroll Wheel] to zoom your lens. Zooming in compresses the background and focuses the audience's attention entirely on the flower.", ui.posePointUp, true, false); break;
            case TutorialStep.PracticeCameraPedestal: ui.ShowBossDialogue("Hold [Q] or [E] to shift the camera up and down. Changing the camera height completely changes the psychology of the shot.", ui.posePoint, true, false); break;
            case TutorialStep.FrameSubject: ui.ShowBossDialogue("Basic Camera: For this test, I want you to keep the flower perfectly dead-center in the frame.", ui.posePointUp, true, false); break;
            case TutorialStep.RecordVideo: ui.ShowBossDialogue("Press [R] to record. You MUST record for exactly 10 seconds, then press [R] to stop. Don't stop early!", ui.poseBoss, true, false); break;
            case TutorialStep.InsertToComputer: ui.ShowBossDialogue("That's a wrap! Press [E] on the ejected card to grab it. Walk to the editing bay, hold the card, and press [F] to insert it into the Computer tower.", ui.poseHappy, true, false); break;

            case TutorialStep.ExplainComputerEditor: ui.ShowBossDialogue("Welcome to the Editing Bay. Before we can cut a commercial, we must ingest and review the raw tapes to make sure the lighting and framing were actually good.", ui.poseOpenHand, true, false); break;
            case TutorialStep.PracticeComputer_Play: ui.ShowBossDialogue("Click your video to review the take.", ui.posePoint, true, false); break;
            case TutorialStep.PracticeComputer_Submit: ui.ShowBossDialogue("If it looks clean and steady, click 'Submit'. This sends the raw footage into Post-Production!", ui.poseSmile, true, false); break;

            case TutorialStep.Complete: ui.ShowBossDialogue("Raw footage submitted! Take the SD card to the computer. Loading the Editor...", ui.poseEndWave, false, false); break;

            case TutorialStep.PostEditComplete: ui.ShowBossDialogue("Video successfully rendered! You now know the pure basics: Center framing, simple lighting, and 10-second trims.", ui.poseHappy, true, false); break;
            case TutorialStep.OfferLevel1: ui.ShowBossDialogue("Now for Stage 1. Goke Cola wants a commercial, and you MUST use professional MMA Syllabus techniques. Ready?", ui.posePointUp, true, false); break;
            case TutorialStep.Level1Accepted: ui.ShowBossDialogue("I've wired your upfront payment. The Cola prop has been unlocked in your Director Tablet! They have strict rules: A RED background, NO reflective light, exactly 10s long, an S-Rank Color grade, and ONLY their Logo. Good luck!", ui.poseBoss, true, false); break;
        }
    }

    private void CleanUpStudio()
    {
        StageSetupManager stageManager = FindObjectOfType<StageSetupManager>();
        if (stageManager != null) stageManager.ClearStage();

        foreach (GameObject obj in FindObjectsOfType<GameObject>())
        {
            if (obj.name.Contains("Cube") || obj.name.Contains("Flower") || obj.name.Contains("Floral") || obj.name.Contains("Cola"))
            {
                Destroy(obj);
            }
        }
    }

    // ==========================================
    // 2. STRICT GATEKEEPERS (LOCKING PLAYER ACTIONS)
    // ==========================================

    public bool CanInteract(string objectType)
    {
        if (currentStep >= TutorialStep.OfferLevel1) return true;

        if (objectType == "DirectorTerminal")
        {
            return currentStep >= TutorialStep.BuildStageWall && currentStep <= TutorialStep.FreePlayDirectorTablet;
        }
        if (objectType == "ShopTerminal")
        {
            return currentStep == TutorialStep.BuyLights || currentStep == TutorialStep.BuyCameraAndCard;
        }
        if (objectType == "ComputerStation")
        {
            return currentStep >= TutorialStep.InsertToComputer && currentStep <= TutorialStep.Complete;
        }
        if (objectType == "HelpDesk")
        {
            return currentStep >= TutorialStep.Level1Accepted;
        }

        return true;
    }

    public bool CanBuyItem(int itemIndex)
    {
        if (currentStep >= TutorialStep.OfferLevel1) return true;

        if (currentStep < TutorialStep.BuyLights)
        {
            ShowWarning("Follow your tasks first!");
            return false;
        }

        if (currentStep == TutorialStep.BuyLights && itemIndex != 1)
        {
            ShowWarning("Only buy the Stage Light right now.");
            return false;
        }

        if (currentStep == TutorialStep.PickUpLight ||
            currentStep == TutorialStep.TurnOnLight ||
            currentStep == TutorialStep.AdjustLight_Intensity ||
            currentStep == TutorialStep.AdjustLight_Tilt)
        {
            ShowWarning("Learn to use the light before buying more.");
            return false;
        }

        if (currentStep == TutorialStep.BuyCameraAndCard && itemIndex == 1)
        {
            ShowWarning("Focus on grabbing the Film Camera and the SD Card.");
            return false;
        }

        if (currentStep > TutorialStep.BuyCameraAndCard && currentStep < TutorialStep.OfferLevel1)
        {
            ShowWarning("You have all the gear you need!");
            return false;
        }

        return true;
    }

    public bool CanRecord()
    {
        if (currentStep >= TutorialStep.OfferLevel1) return true;

        if (currentStep < TutorialStep.RecordVideo)
        {
            ShowWarning("Don't start recording yet! Finish setting up the shot first.");
            return false;
        }

        return true;
    }
}