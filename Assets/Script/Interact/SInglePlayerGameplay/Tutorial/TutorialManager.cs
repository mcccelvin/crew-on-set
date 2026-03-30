using System.Collections;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("Spawning Setup")]
    public GameObject tutorialFloralPrefab;
    public GameObject level1ColaPrefab;
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

    public bool CanInteract(string objectType)
    {
        if (currentStep >= TutorialStep.OfferLevel1) return true;
        if (objectType == "DirectorTerminal") return currentStep >= TutorialStep.BuildStageWall && currentStep <= TutorialStep.FreePlayDirectorTablet;
        if (objectType == "ShopTerminal") return currentStep == TutorialStep.BuyLights || currentStep == TutorialStep.BuyCameraAndCard;
        if (objectType == "ComputerStation") return currentStep >= TutorialStep.InsertToComputer && currentStep <= TutorialStep.Complete;
        if (objectType == "HelpDesk") return currentStep >= TutorialStep.Level1Accepted;
        return true;
    }

    public void ShowWarning(string warningMessage)
    {
        if (warningCoroutine != null) StopCoroutine(warningCoroutine);
        warningCoroutine = StartCoroutine(ShowBossWarning(warningMessage));
    }

    private IEnumerator StartTutorialWithDelay() { yield return new WaitForSeconds(3f); currentStep = TutorialStep.Intro; UpdateBossDialogue(); }
    private IEnumerator StartPostEditTutorial() { yield return new WaitForSeconds(1.5f); currentStep = TutorialStep.PostEditComplete; UpdateBossDialogue(); }

    public bool CanBuyItem(int itemIndex)
    {
        if (currentStep >= TutorialStep.OfferLevel1) return true;

        if (currentStep < TutorialStep.BuyLights) { ShowWarning("Follow your tasks first!"); return false; }
        if (currentStep == TutorialStep.BuyLights && itemIndex != 1) { ShowWarning("Only buy the Stage Light right now."); return false; }
        if (currentStep == TutorialStep.PickUpLight || currentStep == TutorialStep.TurnOnLight || currentStep == TutorialStep.AdjustLight) { ShowWarning("Learn to use the light before buying more."); return false; }
        if (currentStep == TutorialStep.BuyCameraAndCard && itemIndex == 1) { ShowWarning("Focus on grabbing the Film Camera and the SD Card."); return false; }
        if (currentStep > TutorialStep.BuyCameraAndCard && currentStep < TutorialStep.OfferLevel1) { ShowWarning("You have all the gear you need!"); return false; }
        return true;
    }

    private IEnumerator ShowBossWarning(string warningMessage)
    {
        TutorialUIManager.Instance.ShowBossDialogue(warningMessage, TutorialUIManager.Instance.poseBoss, false, false);
        yield return new WaitForSeconds(3.5f);
        if (isTaskPhaseActive) TutorialUIManager.Instance.HideBossDialogue();
    }

    private int GetExactTutorialCost()
    {
        ShopTerminal shop = FindObjectOfType<ShopTerminal>(true);
        if (shop != null && shop.availableItems.Count >= 3) return shop.availableItems[0].price + shop.availableItems[1].price + shop.availableItems[2].price;
        return 0;
    }

    public void SkipTutorial() { if (isTransitioning) return; skippedTutorial = true; currentStep = TutorialStep.OfferLevel1; UpdateBossDialogue(); }

    public void OnOkButtonPressed()
    {
        if (isTransitioning) return;

        if (currentStep == TutorialStep.PostEditComplete) { StartCoroutine(TransitionToNextStep(TutorialStep.OfferLevel1, false)); return; }

        if (currentStep == TutorialStep.OfferLevel1) { if (CareerManager.Instance != null) CareerManager.Instance.AcceptJob("Goke Cola", 30000); currentStep = TutorialStep.Level1Accepted; UpdateBossDialogue(); return; }

        if (currentStep == TutorialStep.Level1Accepted)
        {
            PlayerPrefs.SetInt("TutorialProgress", 2); PlayerPrefs.Save();
            TutorialUIManager.Instance.HideBossDialogue();
            TutorialUIManager.Instance.PointArrowAt("");

            CleanUpStudio();

            TutorialUIManager.Instance.SetupTasks(new string[] { "- Stage a RED backdrop & Soft Lighting", "- Record EXACTLY 10s of the Cola", "- Edit: S-Rank Color & ONLY 1 Logo" });

            // Just spawns the Cola bottle slightly above the desk
            if (level1ColaPrefab != null && stageSpawnPoint != null) Instantiate(level1ColaPrefab, stageSpawnPoint.position + new Vector3(0, 0.5f, 0), stageSpawnPoint.rotation);

            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            Player.PlayerController.PlayerController playerAccept = FindObjectOfType<Player.PlayerController.PlayerController>();
            if (playerAccept != null) playerAccept.canLook = true;
            return;
        }

        if (currentStep == TutorialStep.Intro) { StartCoroutine(TransitionToNextStep(TutorialStep.LearnMovement, false)); return; }
        if (currentStep == TutorialStep.SetTrainingObjectAndMoney)
        {
            if (CareerManager.Instance != null) { CareerManager.Instance.playerMoney += GetExactTutorialCost(); CareerManager.Instance.UpdateMoneyUI(); }
            if (tutorialFloralPrefab != null && stageSpawnPoint != null) Instantiate(tutorialFloralPrefab, stageSpawnPoint.position, stageSpawnPoint.rotation);
            StartCoroutine(TransitionToNextStep(TutorialStep.BuildStageWall, false));
            return;
        }

        if (currentStep == TutorialStep.ExplainDirectorTablet) { StartCoroutine(TransitionToNextStep(TutorialStep.PracticeDirectorTablet, false)); return; }
        if (currentStep == TutorialStep.TabletPracticeFinished) { currentStep = TutorialStep.FreePlayDirectorTablet; StartTaskPhase(); return; }
        if (currentStep == TutorialStep.ExplainComputerEditor) { StartCoroutine(TransitionToNextStep(TutorialStep.PracticeComputerEditor, false)); return; }
        if (currentStep == TutorialStep.Complete) { StartCoroutine(TransitionToNextStep(TutorialStep.OfferLevel1, false)); return; }

        StartTaskPhase();
    }

    private void FinishTutorialInstantly()
    {
        currentStep = TutorialStep.Level1Accepted;
        if (TutorialUIManager.Instance != null)
        {
            TutorialUIManager.Instance.ShowBossDialogue("Get to work on that Goke Cola contract! Remember: RED background, NO reflective light, exactly 10s long, an S-Rank Color grade, and ONLY their Logo.", TutorialUIManager.Instance.poseBoss, true, false);
            TutorialUIManager.Instance.SetupTasks(new string[] { "- Stage a RED backdrop & Soft Lighting", "- Record EXACTLY 10s of the Cola", "- Edit: S-Rank Color & ONLY 1 Logo" });
        }

        CleanUpStudio();

        // Just spawns the Cola bottle slightly above the desk
        if (level1ColaPrefab != null && stageSpawnPoint != null) Instantiate(level1ColaPrefab, stageSpawnPoint.position + new Vector3(0, 0.5f, 0), stageSpawnPoint.rotation);

        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        Player.PlayerController.PlayerController playerAccept = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (playerAccept != null) playerAccept.canLook = false;
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
            case TutorialStep.LearnMovement: TutorialUIManager.Instance.SetupTasks(new string[] { "- Use [W,A,S,D] to move", "- Press [Space] to jump", "- Hold [Shift] to sprint" }); moved = jumped = sprinted = false; break;
            case TutorialStep.BuildStageWall: TutorialUIManager.Instance.SetupTasks(new string[] { "- Look at the Tablet and press [E]" }); tabletOpened = false; TutorialUIManager.Instance.SetDynamicGlow("director", true); TutorialUIManager.Instance.PointArrowAt("director"); break;
            case TutorialStep.PracticeDirectorTablet: TutorialUIManager.Instance.SetupTasks(new string[] { "- Click the 'Wall' button", "- Paint the wall PINK", "- Click 'Flower' to spawn prop", "- Press [T] to pick it up and move it" }); wallBuilt = wallColorChanged = propSpawned = propMoved = false; break;
            case TutorialStep.FreePlayDirectorTablet: if (TutorialUIManager.Instance.taskPanel != null) TutorialUIManager.Instance.taskPanel.SetActive(false); break;
            case TutorialStep.BuyLights: TutorialUIManager.Instance.SetupTasks(new string[] { "- Walk to Shop and press [E] to buy a Light" }); TutorialUIManager.Instance.SetDynamicGlow("shop", true); TutorialUIManager.Instance.PointArrowAt("shop"); break;
            case TutorialStep.PickUpLight: TutorialUIManager.Instance.SetupTasks(new string[] { "- Walk up to the dropped Stage Light and press [E] to pick it up" }); TutorialUIManager.Instance.SetDynamicGlow("light", true); TutorialUIManager.Instance.PointArrowAt("light"); break;
            case TutorialStep.TurnOnLight: TutorialUIManager.Instance.SetupTasks(new string[] { "- Aim at the stage and press [F] to turn it on" }); TutorialUIManager.Instance.SetDynamicGlow("light", false); TutorialUIManager.Instance.PointArrowAt(""); break;
            case TutorialStep.AdjustLight: TutorialUIManager.Instance.SetupTasks(new string[] { "- Tilt the light so it's not reflective", "- Adjust brightness to look natural" }); lightTilted = lightIntensityChanged = false; break;
            case TutorialStep.BuyCameraAndCard: tutorialItemsBought = 0; TutorialUIManager.Instance.SetupTasks(new string[] { "- Buy a Film Camera and SD Card (0/2)" }); TutorialUIManager.Instance.SetDynamicGlow("shop", true); TutorialUIManager.Instance.PointArrowAt("shop"); break;
            case TutorialStep.InsertCardToCamera: TutorialUIManager.Instance.SetupTasks(new string[] { "- Pick up both the Camera and SD Card", "- Hold the Camera and press [C] to insert card" }); TutorialUIManager.Instance.SetDynamicGlow("camera", true); TutorialUIManager.Instance.SetDynamicGlow("sd", true); TutorialUIManager.Instance.PointArrowAt("camera"); break;
            case TutorialStep.EquipCameraView: TutorialUIManager.Instance.SetupTasks(new string[] { "- Press [F] to look through the camera lens" }); cameraViewEntered = false; break;
            case TutorialStep.PracticeCameraZoom: TutorialUIManager.Instance.SetupTasks(new string[] { "- Use [Scroll Wheel] to zoom the lens in and out" }); cameraZoomed = false; break;
            case TutorialStep.PracticeCameraPedestal: TutorialUIManager.Instance.SetupTasks(new string[] { "- Hold [Q] or [E] to shift the camera height" }); cameraPedestalMoved = false; break;
            case TutorialStep.FrameSubject: TutorialUIManager.Instance.SetupTasks(new string[] { "- Aim at the flower until HUD says [SUBJECT DETECTED]" }); subjectFramed = false; break;
            case TutorialStep.RecordVideo: TutorialUIManager.Instance.SetupTasks(new string[] { "- Press [R] to record for a few seconds, then press [R] to stop" }); TutorialUIManager.Instance.SetDynamicGlow("camera", true); TutorialUIManager.Instance.PointArrowAt("camera"); break;
            case TutorialStep.InsertToComputer: TutorialUIManager.Instance.SetupTasks(new string[] { "- Press [E] on the card to pick it up", "- Hold the card and press [F] on the computer tower" }); TutorialUIManager.Instance.SetDynamicGlow("sd", true); TutorialUIManager.Instance.SetDynamicGlow("computer", true); TutorialUIManager.Instance.PointArrowAt("computer"); break;
            case TutorialStep.PracticeComputerEditor: TutorialUIManager.Instance.SetupTasks(new string[] { "- Click your video clip to play it", "- Click 'Submit' to enter Post-Production" }); computerAccessed = videoPlayed = videoSubmitted = false; TutorialUIManager.Instance.SetDynamicGlow("computer", true); TutorialUIManager.Instance.PointArrowAt("computer"); break;
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

    public void OnTabletOpened() { if (currentStep == TutorialStep.BuildStageWall && isTaskPhaseActive && !tabletOpened) { tabletOpened = true; TutorialUIManager.Instance.MarkTaskComplete(0); TutorialUIManager.Instance.SetDynamicGlow("director", false); StartCoroutine(TransitionToNextStep(TutorialStep.ExplainDirectorTablet, true)); } }
    public void OnStageWallBuilt() { if (currentStep == TutorialStep.PracticeDirectorTablet && isTaskPhaseActive && !wallBuilt) { wallBuilt = true; TutorialUIManager.Instance.MarkTaskComplete(0); CheckTabletTasksComplete(); } }
    public void OnWallColorChanged() { if (currentStep == TutorialStep.PracticeDirectorTablet && isTaskPhaseActive && !wallColorChanged) { wallColorChanged = true; TutorialUIManager.Instance.MarkTaskComplete(1); CheckTabletTasksComplete(); } }
    public void OnPropSpawnedFromUI() { if (currentStep == TutorialStep.PracticeDirectorTablet && isTaskPhaseActive && !propSpawned) { propSpawned = true; TutorialUIManager.Instance.MarkTaskComplete(2); CheckTabletTasksComplete(); } }
    public void OnPropMovedWithT() { if (currentStep == TutorialStep.PracticeDirectorTablet && isTaskPhaseActive && !propMoved) { propMoved = true; TutorialUIManager.Instance.MarkTaskComplete(3); CheckTabletTasksComplete(); } }
    private void CheckTabletTasksComplete() { if (currentStep == TutorialStep.PracticeDirectorTablet && wallBuilt && wallColorChanged && propSpawned && propMoved && !isTransitioning) StartCoroutine(TransitionToNextStep(TutorialStep.TabletPracticeFinished, true)); }
    public void OnTabletClosed() { if (currentStep == TutorialStep.FreePlayDirectorTablet && isTaskPhaseActive) StartCoroutine(TransitionToNextStep(TutorialStep.BuyLights, false)); }

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
            case TutorialStep.Intro: ui.ShowBossDialogue("Welcome to Film School! I'm going to teach you how to shoot professional commercials. First, let's make sure you can move around the set.", ui.poseHappy, true, true); break;
            case TutorialStep.LearnMovement: ui.ShowBossDialogue("Use [W, A, S, D] to walk, [Space] to jump, and [Shift] to sprint. Getting comfortable moving around your set is crucial for finding the best camera angles later!", ui.posePoint, true, false); break;
            case TutorialStep.SetTrainingObjectAndMoney: int exactCost = GetExactTutorialCost(); ui.ShowBossDialogue($"Great. I've wired exactly {exactCost} B-Coins to your account to cover your starter gear. I placed a floral vase on the empty stage.", ui.poseSmile, true, false); break;
            case TutorialStep.BuildStageWall: ui.ShowBossDialogue("As a Director, you use the Editor Tablet to instantly spawn and paint walls, saving hours of physical labor. Press [E] on the tablet to open it.", ui.poseOpenHand, true, false); break;
            case TutorialStep.ExplainDirectorTablet: ui.ShowBossDialogue("Our goal is an S-Rank video. The client wants a Floral arrangement against a Pink background. Click to spawn a wall, use the sliders to paint it pink, and spawn the Flower prop.", ui.posePointUp, true, false); break;
            case TutorialStep.PracticeDirectorTablet: ui.ShowBossDialogue("Try it out! Paint the wall pink, spawn the flower, and press [T] to pick up and move the flower to the center. Proper staging is what gives a commercial its mood.", ui.poseBoss, true, false); break;
            case TutorialStep.TabletPracticeFinished: ui.ShowBossDialogue("Perfect set design. Take your time arranging it. When you are happy with the background, close the tablet so we can move on to the most important part of filming: Lighting.", ui.poseChill, true, false); break;
            case TutorialStep.BuyLights: ui.ShowBossDialogue("Without good lighting, our flower will look flat and cheap. Go to the Shop Terminal [E] and buy a Stage Light.", ui.posePoint, true, false); break;
            case TutorialStep.PickUpLight: ui.ShowBossDialogue("The shop delivered your light to the table. Walk over and press [E] to pick it up.", ui.posePoint, true, false); break;
            case TutorialStep.TurnOnLight: ui.ShowBossDialogue("Hold the light, aim it at your flower, and press [F] to turn it on. See how the shadows instantly give the petals a 3D shape?", ui.poseOpenHand, true, false); break;
            case TutorialStep.AdjustLight: ui.ShowBossDialogue("For an S-Rank, the light cannot be too reflective! Use [Up/Down Arrows] to tilt the angle, and the [Scroll Wheel] to soften the brightness. Make it look natural.", ui.posePointUp, true, false); break;
            case TutorialStep.BuyCameraAndCard: ui.ShowBossDialogue("The set is lit. Now we need to capture it. Go back to the shop and buy a Film Camera to record the light, and an SD Card to save the digital data.", ui.poseChill, true, false); break;
            case TutorialStep.InsertCardToCamera: ui.ShowBossDialogue("Pick up the Camera and the SD card. While holding the camera, press [C] to insert the memory card so we actually have somewhere to save our video files.", ui.poseBoss, true, false); break;
            case TutorialStep.EquipCameraView: ui.ShowBossDialogue("Hold the camera and press [F] to look through the Director's Viewfinder. This frames the world exactly how the audience will see it.", ui.poseHappy, true, false); break;
            case TutorialStep.PracticeCameraZoom: ui.ShowBossDialogue("Use the [Scroll Wheel] to zoom your lens. Zooming in compresses the background and focuses the audience's attention entirely on the flower.", ui.posePointUp, true, false); break;
            case TutorialStep.PracticeCameraPedestal: ui.ShowBossDialogue("Hold [Q] or [E] to shift the camera up and down. Changing the camera height completely changes the psychology of the shot.", ui.posePoint, true, false); break;
            case TutorialStep.FrameSubject: ui.ShowBossDialogue("Now, compose your shot for an S-Rank. A great director keeps the subject dead center. Aim at the flower until the HUD locks on.", ui.poseOpenHand, true, false); break;
            case TutorialStep.RecordVideo: ui.ShowBossDialogue("Perfect framing. Press [R] to record. Wait a few seconds to capture a steady, usable clip, then press [R] to cut. Always record longer than you think you need!", ui.poseSmile, true, false); break;
            case TutorialStep.InsertToComputer: ui.ShowBossDialogue("That's a wrap! Press [E] on the ejected card to grab it. Walk to the editing bay, hold the card, and press [F] to insert it into the Computer tower.", ui.poseHappy, true, false); break;
            case TutorialStep.ExplainComputerEditor: ui.ShowBossDialogue("Welcome to the Editing Bay. Before we can cut a commercial, we must ingest and review the raw tapes to make sure the lighting and framing were actually good.", ui.poseOpenHand, true, false); break;
            case TutorialStep.PracticeComputerEditor: ui.ShowBossDialogue("Click your video to review the take. If it looks clean and steady, click 'Submit'. This sends the raw footage into Post-Production!", ui.posePoint, true, false); break;
            case TutorialStep.Complete: ui.ShowBossDialogue("Raw footage submitted! Loading the Post-Production Editor...", ui.poseEndWave, false, false); break;

            case TutorialStep.PostEditComplete: ui.ShowBossDialogue("Video successfully rendered! You now know how to build a set, light it, film it, and edit it. You are officially a Director!", ui.poseEndWave, true, false); break;
            case TutorialStep.OfferLevel1: ui.ShowBossDialogue("Welcome back! Your tutorial commercial was a huge hit. Now you are on your own. Goke Cola wants a 10-second teaser for 60,000 B coins. Ready for Stage 1?", ui.posePointUp, true, false); break;
            case TutorialStep.Level1Accepted: ui.ShowBossDialogue("I've wired your upfront payment. The cola is on the stage. They have strict rules: A RED background, NO reflective light, exactly 10s long, an S-Rank Color grade, and ONLY their Logo. Good luck!", ui.poseHappy, true, false); break;
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
}