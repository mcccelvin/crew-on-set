using System.Collections;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("Spawning Setup")]
    public GameObject practiceCubePrefab;
    public GameObject level1FlowerPrefab;
    public Transform stageSpawnPoint;

    [Header("Skip Tutorial Starter Gear")]
    public GameObject cameraPrefab;
    public GameObject lightPrefab;
    public GameObject sdCardPrefab;
    public Transform deliveryZone;

    public enum TutorialStep
    {
        Intro, LearnMovement, SetTrainingObjectAndMoney,
        BuildStageWall, ExplainDirectorTablet, PracticeDirectorTablet, TabletPracticeFinished, FreePlayDirectorTablet,
        BuyLights, PickUpLight, TurnOnLight, AdjustLight, BuyCameraAndCard, InsertCardToCamera,
        EquipCameraView, PracticeCameraZoom, PracticeCameraPedestal, FrameSubject, RecordVideo,
        InsertToComputer, ExplainComputerEditor, PracticeComputerEditor,
        Complete, OfferLevel1, Level1Accepted
    }

    public TutorialStep currentStep;
    private bool isTransitioning = false;
    private bool isTaskPhaseActive = false;
    private int tutorialItemsBought = 0;
    private bool skippedTutorial = false;

    // Task Tracking (No UI logic here!)
    private bool moved = false, jumped = false, sprinted = false;
    private bool tabletOpened = false, wallBuilt = false, wallColorChanged = false, propSpawned = false, propMoved = false;
    private bool lightTilted = false, lightIntensityChanged = false;
    private bool cameraViewEntered = false, cameraZoomed = false, cameraPedestalMoved = false, subjectFramed = false;
    private bool computerAccessed = false, videoPlayed = false, videoSubmitted = false;

    private void Awake() { if (Instance == null) Instance = this; else Destroy(gameObject); }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Player.PlayerController.PlayerController player = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (player != null) player.canLook = true;

        StartCoroutine(StartTutorialWithDelay());
    }

    private void Update()
    {
        // Notice we removed the "KeyCode.Tab" logic because the UIManager handles that now!

        if (currentStep == TutorialStep.LearnMovement && isTaskPhaseActive)
        {
            if (!moved && (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D)))
            { moved = true; TutorialUIManager.Instance.MarkTaskComplete(0); }
            if (!jumped && Input.GetKeyDown(KeyCode.Space))
            { jumped = true; TutorialUIManager.Instance.MarkTaskComplete(1); }
            if (!sprinted && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            { sprinted = true; TutorialUIManager.Instance.MarkTaskComplete(2); }

            if (moved && jumped && sprinted && !isTransitioning)
            { isTaskPhaseActive = false; StartCoroutine(TransitionToNextStep(TutorialStep.SetTrainingObjectAndMoney, true)); }
        }

        if (currentStep == TutorialStep.PracticeCameraZoom && isTaskPhaseActive && !cameraZoomed)
        {
            if (Input.mouseScrollDelta.y != 0)
            { cameraZoomed = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.PracticeCameraPedestal, true)); }
        }

        if (currentStep == TutorialStep.PracticeCameraPedestal && isTaskPhaseActive && !cameraPedestalMoved)
        {
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E))
            { cameraPedestalMoved = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.FrameSubject, true)); }
        }
    }

    private IEnumerator StartTutorialWithDelay()
    {
        yield return new WaitForSeconds(3f);
        currentStep = TutorialStep.Intro;
        UpdateBossDialogue();
    }

    public bool CanBuyItem(int itemIndex)
    {
        if (currentStep < TutorialStep.BuyLights) { StartCoroutine(ShowBossWarning("Follow your tasks first!")); return false; }
        if (currentStep == TutorialStep.BuyLights && itemIndex != 1) { StartCoroutine(ShowBossWarning("Only buy the Stage Light right now.")); return false; }
        if (currentStep == TutorialStep.PickUpLight || currentStep == TutorialStep.TurnOnLight || currentStep == TutorialStep.AdjustLight) { StartCoroutine(ShowBossWarning("Learn to use the light before buying more.")); return false; }
        if (currentStep == TutorialStep.BuyCameraAndCard && itemIndex == 1) { StartCoroutine(ShowBossWarning("Focus on grabbing the Film Camera and the SD Card.")); return false; }
        if (currentStep > TutorialStep.BuyCameraAndCard && currentStep < TutorialStep.OfferLevel1) { StartCoroutine(ShowBossWarning("You have all the gear you need!")); return false; }
        return true;
    }

    private IEnumerator ShowBossWarning(string warningMessage)
    {
        TutorialUIManager.Instance.ShowBossDialogue(warningMessage, TutorialUIManager.Instance.poseBoss, false, false);
        yield return new WaitForSeconds(3.5f);
        if (isTaskPhaseActive) TutorialUIManager.Instance.HideBossDialogue();
    }

    public void SkipTutorial()
    {
        if (isTransitioning) return;
        skippedTutorial = true;
        if (CareerManager.Instance != null) { CareerManager.Instance.playerMoney += 60000; CareerManager.Instance.UpdateMoneyUI(); }
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
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.PointArrowAt("");
            CleanUpStudio();

            TutorialUIManager.Instance.SetupTasks(new string[] { "- Stage a nice backdrop", "- Record a tabletop teaser of the vase", "- Submit the video on the computer" });
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
        if (currentStep == TutorialStep.TabletPracticeFinished) { currentStep = TutorialStep.FreePlayDirectorTablet; StartTaskPhase(); return; }
        if (currentStep == TutorialStep.ExplainComputerEditor) { StartCoroutine(TransitionToNextStep(TutorialStep.PracticeComputerEditor, false)); return; }
        if (currentStep == TutorialStep.Complete) { StartCoroutine(TransitionToNextStep(TutorialStep.OfferLevel1, false)); return; }

        StartTaskPhase();
    }

    private void StartTaskPhase()
    {
        TutorialUIManager.Instance.HideBossDialogue();
        Player.PlayerController.PlayerController p = FindObjectOfType<Player.PlayerController.PlayerController>();

        if (currentStep == TutorialStep.PracticeDirectorTablet || currentStep == TutorialStep.PracticeComputerEditor || currentStep == TutorialStep.FreePlayDirectorTablet)
        { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; if (p != null) p.canLook = false; }
        else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; if (p != null) p.canLook = true; }

        isTaskPhaseActive = true;

        switch (currentStep)
        {
            case TutorialStep.LearnMovement:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Use [W,A,S,D] to move", "- Press [Space] to jump", "- Hold [Shift] to sprint" });
                moved = jumped = sprinted = false; break;
            case TutorialStep.BuildStageWall:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Look at the Tablet and press [E]" });
                tabletOpened = false; TutorialUIManager.Instance.SetDynamicGlow("director", true); TutorialUIManager.Instance.PointArrowAt("director"); break;
            case TutorialStep.PracticeDirectorTablet:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click the 'Wall' button", "- Use sliders to paint the wall", "- Click 'Cube' to spawn a prop", "- Press [T] to pick it up and move it" });
                wallBuilt = wallColorChanged = propSpawned = propMoved = false; break;
            case TutorialStep.FreePlayDirectorTablet:
                if (TutorialUIManager.Instance.taskPanel != null) TutorialUIManager.Instance.taskPanel.SetActive(false); break;
            case TutorialStep.BuyLights:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Walk to Shop and press [E] to buy a Light" });
                TutorialUIManager.Instance.SetDynamicGlow("shop", true); TutorialUIManager.Instance.PointArrowAt("shop"); break;
            case TutorialStep.PickUpLight:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Walk up to the dropped Stage Light and press [E] to pick it up" });
                TutorialUIManager.Instance.SetDynamicGlow("light", true); TutorialUIManager.Instance.PointArrowAt("light"); break;
            case TutorialStep.TurnOnLight:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Aim at the stage and press [F] to turn it on" });
                TutorialUIManager.Instance.SetDynamicGlow("light", false); TutorialUIManager.Instance.PointArrowAt(""); break;
            case TutorialStep.AdjustLight:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Use [Up/Down Arrows] to tilt the light", "- Use [Scroll Wheel] to adjust brightness" });
                lightTilted = lightIntensityChanged = false; break;
            case TutorialStep.BuyCameraAndCard:
                tutorialItemsBought = 0; TutorialUIManager.Instance.SetupTasks(new string[] { "- Buy a Film Camera and SD Card (0/2)" });
                TutorialUIManager.Instance.SetDynamicGlow("shop", true); TutorialUIManager.Instance.PointArrowAt("shop"); break;
            case TutorialStep.InsertCardToCamera:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Pick up both the Camera and SD Card", "- Hold the Camera and press [C] to insert card" });
                TutorialUIManager.Instance.SetDynamicGlow("camera", true); TutorialUIManager.Instance.SetDynamicGlow("sd", true); TutorialUIManager.Instance.PointArrowAt("camera"); break;
            case TutorialStep.EquipCameraView:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Press [F] to look through the camera lens" });
                cameraViewEntered = false; break;
            case TutorialStep.PracticeCameraZoom:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Use [Scroll Wheel] to zoom the lens in and out" });
                cameraZoomed = false; break;
            case TutorialStep.PracticeCameraPedestal:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Hold [Q] or [E] to shift the camera height" });
                cameraPedestalMoved = false; break;
            case TutorialStep.FrameSubject:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Aim at the practice cube until the HUD says [SUBJECT DETECTED]" });
                subjectFramed = false; break;
            case TutorialStep.RecordVideo:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Press [R] to record for a few seconds, then press [R] to stop" });
                TutorialUIManager.Instance.SetDynamicGlow("camera", true); TutorialUIManager.Instance.PointArrowAt("camera"); break;
            case TutorialStep.InsertToComputer:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Press [E] on the card to pick it up", "- Press [E] on the computer tower to insert it" });
                TutorialUIManager.Instance.SetDynamicGlow("sd", true); TutorialUIManager.Instance.SetDynamicGlow("computer", true); TutorialUIManager.Instance.PointArrowAt("computer"); break;
            case TutorialStep.PracticeComputerEditor:
                TutorialUIManager.Instance.SetupTasks(new string[] { "- Click your video clip to play it", "- Click 'Submit' to enter Post-Production" });
                computerAccessed = videoPlayed = videoSubmitted = false; TutorialUIManager.Instance.SetDynamicGlow("computer", true); TutorialUIManager.Instance.PointArrowAt("computer"); break;
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

        if (currentStep == TutorialStep.Complete)
        { yield return new WaitForSeconds(3f); StartCoroutine(TransitionToNextStep(TutorialStep.OfferLevel1, false)); }
    }

    public void OnTabletOpened() { if (currentStep == TutorialStep.BuildStageWall && isTaskPhaseActive && !tabletOpened) { tabletOpened = true; TutorialUIManager.Instance.MarkTaskComplete(0); TutorialUIManager.Instance.SetDynamicGlow("director", false); StartCoroutine(TransitionToNextStep(TutorialStep.ExplainDirectorTablet, true)); } }
    public void OnStageWallBuilt() { if (currentStep == TutorialStep.PracticeDirectorTablet && isTaskPhaseActive && !wallBuilt) { wallBuilt = true; TutorialUIManager.Instance.MarkTaskComplete(0); CheckTabletTasksComplete(); } }
    public void OnWallColorChanged() { if (currentStep == TutorialStep.PracticeDirectorTablet && isTaskPhaseActive && !wallColorChanged) { wallColorChanged = true; TutorialUIManager.Instance.MarkTaskComplete(1); CheckTabletTasksComplete(); } }
    public void OnPropSpawnedFromUI() { if (currentStep == TutorialStep.PracticeDirectorTablet && isTaskPhaseActive && !propSpawned) { propSpawned = true; TutorialUIManager.Instance.MarkTaskComplete(2); CheckTabletTasksComplete(); } }
    public void OnPropMovedWithT() { if (currentStep == TutorialStep.PracticeDirectorTablet && isTaskPhaseActive && !propMoved) { propMoved = true; TutorialUIManager.Instance.MarkTaskComplete(3); CheckTabletTasksComplete(); } }
    private void CheckTabletTasksComplete() { if (currentStep == TutorialStep.PracticeDirectorTablet && wallBuilt && wallColorChanged && propSpawned && propMoved && !isTransitioning) StartCoroutine(TransitionToNextStep(TutorialStep.TabletPracticeFinished, true)); }
    public void OnTabletClosed() { if (currentStep == TutorialStep.FreePlayDirectorTablet && isTaskPhaseActive) StartCoroutine(TransitionToNextStep(TutorialStep.BuyLights, false)); }

    public void OnEquipmentBought()
    {
        if (currentStep == TutorialStep.BuyLights && isTaskPhaseActive)
        { TutorialUIManager.Instance.SetDynamicGlow("shop", false); TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.PickUpLight, true)); }
        else if (currentStep == TutorialStep.BuyCameraAndCard && isTaskPhaseActive)
        {
            tutorialItemsBought++;
            TutorialUIManager.Instance.SetupTasks(new string[] { $"- Buy a Film Camera and SD Card ({tutorialItemsBought}/2)" });
            if (tutorialItemsBought >= 2) { TutorialUIManager.Instance.SetDynamicGlow("shop", false); TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.InsertCardToCamera, true)); }
        }
    }

    public void OnLightPickedUp() { if (currentStep == TutorialStep.PickUpLight && isTaskPhaseActive) { TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.TurnOnLight, true)); } }
    public void OnLightTurnedOn() { if (currentStep == TutorialStep.TurnOnLight && isTaskPhaseActive) { TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.AdjustLight, true)); } }
    public void OnLightTilted() { if (currentStep == TutorialStep.AdjustLight && isTaskPhaseActive && !lightTilted) { lightTilted = true; TutorialUIManager.Instance.MarkTaskComplete(0); CheckLightTasksComplete(); } }
    public void OnLightIntensityChanged() { if (currentStep == TutorialStep.AdjustLight && isTaskPhaseActive && !lightIntensityChanged) { lightIntensityChanged = true; TutorialUIManager.Instance.MarkTaskComplete(1); CheckLightTasksComplete(); } }
    private void CheckLightTasksComplete() { if (currentStep == TutorialStep.AdjustLight && lightTilted && lightIntensityChanged) StartCoroutine(TransitionToNextStep(TutorialStep.BuyCameraAndCard, true)); }

    public void OnCardInsertedToCamera() { if (currentStep == TutorialStep.InsertCardToCamera && isTaskPhaseActive) { TutorialUIManager.Instance.SetDynamicGlow("sd", false); TutorialUIManager.Instance.SetDynamicGlow("camera", false); TutorialUIManager.Instance.MarkTaskComplete(0); TutorialUIManager.Instance.MarkTaskComplete(1); StartCoroutine(TransitionToNextStep(TutorialStep.EquipCameraView, true)); } }
    public void OnCameraViewEntered() { if (currentStep == TutorialStep.EquipCameraView && isTaskPhaseActive && !cameraViewEntered) { cameraViewEntered = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.PracticeCameraZoom, true)); } }
    public void OnSubjectFramed() { if (currentStep == TutorialStep.FrameSubject && isTaskPhaseActive && !subjectFramed) { subjectFramed = true; TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.RecordVideo, true)); } }
    public void OnRecordingFinished() { if (currentStep == TutorialStep.RecordVideo && isTaskPhaseActive) { TutorialUIManager.Instance.SetDynamicGlow("camera", false); TutorialUIManager.Instance.MarkTaskComplete(0); StartCoroutine(TransitionToNextStep(TutorialStep.InsertToComputer, true)); } }
    public void OnCardInsertedToComputer() { if (currentStep == TutorialStep.InsertToComputer && isTaskPhaseActive) { TutorialUIManager.Instance.SetDynamicGlow("sd", false); TutorialUIManager.Instance.MarkTaskComplete(0); TutorialUIManager.Instance.MarkTaskComplete(1); StartCoroutine(TransitionToNextStep(TutorialStep.ExplainComputerEditor, true)); } }

    public void OnComputerAccessed() { if (currentStep == TutorialStep.PracticeComputerEditor && isTaskPhaseActive && !computerAccessed) { computerAccessed = true; TutorialUIManager.Instance.SetDynamicGlow("computer", false); TutorialUIManager.Instance.MarkTaskComplete(0); } }

    public void OnVideoPlayed() { if (currentStep == TutorialStep.PracticeComputerEditor && isTaskPhaseActive && !videoPlayed) { videoPlayed = true; TutorialUIManager.Instance.MarkTaskComplete(0); CheckEditorTasksComplete(); } }
    public void OnVideoSubmitted() { if (currentStep == TutorialStep.PracticeComputerEditor && isTaskPhaseActive && !videoSubmitted) { videoSubmitted = true; TutorialUIManager.Instance.MarkTaskComplete(1); CheckEditorTasksComplete(); } }

    private void CheckEditorTasksComplete() { if (computerAccessed && videoPlayed && videoSubmitted) StartCoroutine(TransitionToNextStep(TutorialStep.Complete, true)); }

    private void UpdateBossDialogue()
    {
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        Player.PlayerController.PlayerController p = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (p != null) p.canLook = false;

        var ui = TutorialUIManager.Instance;
        switch (currentStep)
        {
            case TutorialStep.Intro: ui.ShowBossDialogue("Welcome to the studio! I am going to teach you exactly how everything works from scratch. First, let's make sure you can walk.", ui.poseHappy, true, true); break;
            case TutorialStep.LearnMovement: ui.ShowBossDialogue("Use the [W, A, S, D] keys on your keyboard to walk around. You can jump with [Space] and hold [Shift] to run.", ui.posePoint, true, false); break;
            case TutorialStep.SetTrainingObjectAndMoney: ui.ShowBossDialogue("Perfect. I've placed a practice cube on the empty stage, and wired 60,000 B coins to your account to buy tools.", ui.poseSmile, true, false); break;
            case TutorialStep.BuildStageWall: ui.ShowBossDialogue("Follow the arrow to the Editor Tablet. I've added your objectives to your Task List. You can press [Tab] anytime to view them! Press [E] on the tablet to turn it on.", ui.poseOpenHand, true, false); break;
            case TutorialStep.ExplainDirectorTablet: ui.ShowBossDialogue("This tablet controls the physical stage. You click the white UI buttons to spawn walls and props. You can click any object to paint it using the sliders, or press [T] to pick it up.", ui.posePointUp, true, false); break;
            case TutorialStep.PracticeDirectorTablet: ui.ShowBossDialogue("Give it a try. Click the button to add a wall, paint it a new color, click to spawn a prop, and press [T] to move the prop somewhere else.", ui.poseBoss, true, false); break;
            case TutorialStep.TabletPracticeFinished: ui.ShowBossDialogue("Great job! Take your time to play around and set up the stage however you like. When you are ready to move on to lighting, just close the Editor Tablet.", ui.poseChill, true, false); break;
            case TutorialStep.BuyLights: ui.ShowBossDialogue("Now we need lighting. Follow the arrow to the Shop Terminal, press [E], and buy a Stage Light.", ui.posePoint, true, false); break;
            case TutorialStep.PickUpLight: ui.ShowBossDialogue("Great! The Stage Light just spawned on the delivery table. Walk over there and press [E] to pick it up.", ui.posePoint, true, false); break;
            case TutorialStep.TurnOnLight: ui.ShowBossDialogue("Nice! That is a portable Stage Light. It is perfect for highlighting your subject. Hold it in your hands, face the stage, and press [F] to turn the bulb on!", ui.poseOpenHand, true, false); break;
            case TutorialStep.AdjustLight: ui.ShowBossDialogue("You can also control the exact angle and brightness! Try using your Up and Down arrow keys to tilt the stand, and scroll your mouse wheel to dim or brighten the LEDs.", ui.posePointUp, true, false); break;
            case TutorialStep.BuyCameraAndCard: ui.ShowBossDialogue("We can't film without a camera. Go back to the Shop Terminal and buy a Film Camera and an SD Card.", ui.poseChill, true, false); break;
            case TutorialStep.InsertCardToCamera: ui.ShowBossDialogue("Press [E] to pick up the Camera, and [E] to pick up the SD card. With the camera in your hand, press [C] to insert the memory card.", ui.poseBoss, true, false); break;
            case TutorialStep.EquipCameraView: ui.ShowBossDialogue("Time to learn the camera! Hold it in your hands and press [F] to look through the viewfinder.", ui.poseHappy, true, false); break;
            case TutorialStep.PracticeCameraZoom: ui.ShowBossDialogue("You can change your lens focal length dynamically. Try using your mouse's [Scroll Wheel] to zoom in and out.", ui.posePointUp, true, false); break;
            case TutorialStep.PracticeCameraPedestal: ui.ShowBossDialogue("You can also adjust the camera's height without moving your body. Hold [Q] to lower the pedestal, and [E] to raise it.", ui.posePoint, true, false); break;
            case TutorialStep.FrameSubject: ui.ShowBossDialogue("This camera has an automatic tracking focus, but you need to frame your subject correctly. Aim at the practice cube until your HUD detects it.", ui.poseOpenHand, true, false); break;
            case TutorialStep.RecordVideo: ui.ShowBossDialogue("Perfect! The subject is in focus. Press [R] to start recording. Wait a few seconds to get a good clip, then press [R] again to cut the tape.", ui.poseSmile, true, false); break;
            case TutorialStep.InsertToComputer: ui.ShowBossDialogue("Great take! Press [E] on the card to take it out. Walk over to the Computer tower and press [E] to push the card in.", ui.poseHappy, true, false); break;
            case TutorialStep.ExplainComputerEditor: ui.ShowBossDialogue("Before we edit, we need to review the raw tapes. The computer screen shows all the SD cards you've inserted into the tower.", ui.poseOpenHand, true, false); break;
            case TutorialStep.PracticeComputerEditor: ui.ShowBossDialogue("Click on your clip to play the video. Watch the whole take, and if you are happy with it, click 'Submit' to go to post-production!", ui.posePoint, true, false); break;
            case TutorialStep.Complete: ui.ShowBossDialogue("Video successfully submitted! Loading Post-Production Editor...", ui.poseEndWave, false, false); break;
            case TutorialStep.OfferLevel1: ui.ShowBossDialogue("Flora & Form Home just offered us 60,000 B coins for a 20-second tabletop teaser of their new artisan vase. Do you want the job?", ui.posePointUp, true, false); break;
            case TutorialStep.Level1Accepted: ui.ShowBossDialogue("I've wired your 30,000 B coins upfront payment. The vase is on the stage. Use the Tablet, Setup your Lights, Record, Edit, and Export to finish the job!", ui.poseHappy, true, false); break;
        }
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