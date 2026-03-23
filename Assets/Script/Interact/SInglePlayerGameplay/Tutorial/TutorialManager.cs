using UnityEngine;
using TMPro;
using System.Collections;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("UI")]
    public GameObject bossHUDCanvas;
    public TextMeshProUGUI bossText;
    public GameObject okButton;
    public GameObject skipButton;

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
        Intro, LearnMovement, SetTrainingObjectAndMoney, BuildStageWall,
        BuyLights, SetupLight, BuyCameraAndCard, InsertCardToCamera,
        RecordVideo, InsertToComputer, PlayRecording, Complete,
        OfferLevel1, Level1Accepted
    }

    public TutorialStep currentStep;
    private bool isTransitioning = false;
    private int tutorialItemsBought = 0;
    private bool skippedTutorial = false;

    private bool pressedW = false;
    private bool pressedA = false;
    private bool pressedS = false;
    private bool pressedD = false;
    private bool pressedSpace = false;
    private bool pressedShift = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (bossHUDCanvas != null) bossHUDCanvas.SetActive(false);
        if (okButton != null) okButton.SetActive(false);
        if (skipButton != null) skipButton.SetActive(false);

        // --- THE FIX: Hide and lock the cursor the exact second the scene loads! ---
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Ensure the player can actually look around the room during the 5-second wait
        Player.PlayerController.PlayerController player = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (player != null) player.canLook = true;

        StartCoroutine(StartTutorialWithDelay());
    }

    private void Update()
    {
        if (currentStep == TutorialStep.LearnMovement && !isTransitioning)
        {
            if (Input.GetKeyDown(KeyCode.W)) pressedW = true;
            if (Input.GetKeyDown(KeyCode.A)) pressedA = true;
            if (Input.GetKeyDown(KeyCode.S)) pressedS = true;
            if (Input.GetKeyDown(KeyCode.D)) pressedD = true;
            if (Input.GetKeyDown(KeyCode.Space)) pressedSpace = true;
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)) pressedShift = true;

            if (pressedW && pressedA && pressedS && pressedD && pressedSpace && pressedShift)
            {
                OnMovementFinished();
            }
        }
    }

    private IEnumerator StartTutorialWithDelay()
    {
        yield return new WaitForSeconds(5f);

        if (bossHUDCanvas != null) bossHUDCanvas.SetActive(true);
        if (okButton != null) okButton.SetActive(true);
        if (skipButton != null) skipButton.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Player.PlayerController.PlayerController player = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (player != null) player.canLook = false;

        currentStep = TutorialStep.Intro;
        UpdateBossDialogue();
    }

    public void SkipTutorial()
    {
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

            CleanUpStudio();

            if (level1FlowerPrefab != null && stageSpawnPoint != null)
            {
                Instantiate(level1FlowerPrefab, stageSpawnPoint.position, stageSpawnPoint.rotation);
            }

            // ONLY spawn brand new gear if they skipped the tutorial!
            if (skippedTutorial)
            {
                Transform dropSpot = deliveryZone != null ? deliveryZone : stageSpawnPoint;
                if (dropSpot != null)
                {
                    if (cameraPrefab != null) Instantiate(cameraPrefab, dropSpot.position + new Vector3(0, 0.5f, 0), dropSpot.rotation);
                    if (lightPrefab != null) Instantiate(lightPrefab, dropSpot.position + new Vector3(0.5f, 0.5f, 0), dropSpot.rotation);
                    if (sdCardPrefab != null) Instantiate(sdCardPrefab, dropSpot.position + new Vector3(-0.5f, 0.5f, 0), dropSpot.rotation);
                }
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Player.PlayerController.PlayerController playerAccept = FindObjectOfType<Player.PlayerController.PlayerController>();
            if (playerAccept != null) playerAccept.canLook = true;
            return;
        }

        if (currentStep == TutorialStep.Intro)
        {
            if (okButton != null) okButton.SetActive(false);
            if (skipButton != null) skipButton.SetActive(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Player.PlayerController.PlayerController player = FindObjectOfType<Player.PlayerController.PlayerController>();
            if (player != null) player.canLook = true;

            StartCoroutine(TransitionToNextStep(TutorialStep.LearnMovement));
            return;
        }

        if (currentStep == TutorialStep.SetTrainingObjectAndMoney)
        {
            if (okButton != null) okButton.SetActive(false);

            if (CareerManager.Instance != null)
            {
                CareerManager.Instance.playerMoney += 60000;
                CareerManager.Instance.UpdateMoneyUI();
            }

            if (practiceCubePrefab != null && stageSpawnPoint != null)
            {
                Instantiate(practiceCubePrefab, stageSpawnPoint.position, stageSpawnPoint.rotation);
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Player.PlayerController.PlayerController player = FindObjectOfType<Player.PlayerController.PlayerController>();
            if (player != null) player.canLook = true;

            StartCoroutine(TransitionToNextStep(TutorialStep.BuildStageWall));
        }
    }

    // --- UPDATED: The new Teleportation Janitor! ---
    private void CleanUpStudio()
    {
        StageSetupManager stageManager = FindObjectOfType<StageSetupManager>();
        if (stageManager != null) stageManager.ClearStage();

        // 1. Force player to drop everything so we can teleport it safely
        Player.Interactor.EquipmentInteractor inventory = FindObjectOfType<Player.Interactor.EquipmentInteractor>();
        if (inventory != null) inventory.DropAllEquipment();

        // 2. Destroy the Practice Cube (and any old flowers)
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("Cube") || obj.name.Contains("Flower"))
            {
                Destroy(obj);
            }
        }

        // 3. Find all gear in the room and teleport the Camera/Light
        Player.Equipment.Equipment[] allGear = FindObjectsOfType<Player.Equipment.Equipment>();
        Transform dropSpot = deliveryZone != null ? deliveryZone : stageSpawnPoint;

        foreach (Player.Equipment.Equipment gear in allGear)
        {
            // IGNORE THE SD CARD! (Leaves it safe inside the computer)
            if (gear.GetComponent<Player.Equipment.SDCardItem>() != null) continue;

            // Teleport Camera and Light back to the delivery table
            if (dropSpot != null)
            {
                // Add a tiny random offset so they don't spawn perfectly inside each other
                Vector3 randomOffset = new Vector3(Random.Range(-0.3f, 0.3f), 0.5f, Random.Range(-0.3f, 0.3f));
                gear.transform.position = dropSpot.position + randomOffset;

                // Reset physics so they don't bounce off the table
                Rigidbody rb = gear.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }
    }

    private IEnumerator TransitionToNextStep(TutorialStep nextStep)
    {
        isTransitioning = true;

        if (bossHUDCanvas != null) bossHUDCanvas.SetActive(false);
        yield return new WaitForSeconds(5f);

        currentStep = nextStep;
        if (bossHUDCanvas != null) bossHUDCanvas.SetActive(true);
        UpdateBossDialogue();

        isTransitioning = false;

        if (currentStep == TutorialStep.Complete)
        {
            yield return new WaitForSeconds(5f);
            currentStep = TutorialStep.OfferLevel1;
            UpdateBossDialogue();
        }
    }

    public void OnMovementFinished()
    {
        if (currentStep == TutorialStep.LearnMovement && !isTransitioning)
            StartCoroutine(TransitionToNextStep(TutorialStep.SetTrainingObjectAndMoney));
    }

    public void OnEquipmentBought()
    {
        if (currentStep == TutorialStep.BuyLights && !isTransitioning)
            StartCoroutine(TransitionToNextStep(TutorialStep.SetupLight));
        else if (currentStep == TutorialStep.BuyCameraAndCard && !isTransitioning)
        {
            tutorialItemsBought++;
            if (tutorialItemsBought >= 2)
                StartCoroutine(TransitionToNextStep(TutorialStep.InsertCardToCamera));
        }
    }

    public void OnStageWallBuilt()
    {
        if (currentStep == TutorialStep.BuildStageWall && !isTransitioning)
            StartCoroutine(TransitionToNextStep(TutorialStep.BuyLights));
    }

    public void OnLightTurnedOn()
    {
        if (currentStep == TutorialStep.SetupLight && !isTransitioning)
        {
            tutorialItemsBought = 0;
            StartCoroutine(TransitionToNextStep(TutorialStep.BuyCameraAndCard));
        }
    }

    public void OnCardInsertedToCamera()
    {
        if (currentStep == TutorialStep.InsertCardToCamera && !isTransitioning)
            StartCoroutine(TransitionToNextStep(TutorialStep.RecordVideo));
    }

    public void OnRecordingFinished()
    {
        if (currentStep == TutorialStep.RecordVideo && !isTransitioning)
            StartCoroutine(TransitionToNextStep(TutorialStep.InsertToComputer));
    }

    public void OnCardInsertedToComputer()
    {
        if (currentStep == TutorialStep.InsertToComputer && !isTransitioning)
            StartCoroutine(TransitionToNextStep(TutorialStep.PlayRecording));
    }

    public void OnVideoPlayed()
    {
        if (currentStep == TutorialStep.PlayRecording && !isTransitioning)
            StartCoroutine(TransitionToNextStep(TutorialStep.Complete));
    }

    private void UpdateBossDialogue()
    {
        if (bossText == null) return;

        switch (currentStep)
        {
            case TutorialStep.Intro:
                bossText.text = "BOSS: Welcome to the studio! Let's get you trained up before we take on real clients.";
                if (okButton != null) okButton.SetActive(true);
                if (skipButton != null) skipButton.SetActive(true);
                break;
            case TutorialStep.LearnMovement:
                bossText.text = "BOSS: Use [W, A, S, D] to move, [Space] to jump, and hold [Shift] to sprint. Try them all out!";
                break;
            case TutorialStep.SetTrainingObjectAndMoney:
                bossText.text = "BOSS: Great. I've set up a practice cube on the stage, and I just wired 60,000 B coins to your account to buy some starter gear.";
                if (okButton != null) okButton.SetActive(true);

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Player.PlayerController.PlayerController p = FindObjectOfType<Player.PlayerController.PlayerController>();
                if (p != null) p.canLook = false;
                break;
            case TutorialStep.BuildStageWall:
                bossText.text = "BOSS: Head over to the Editor Tablet. Press [E] to use it, and add a wall to the stage to give us a nice backdrop.";
                break;
            case TutorialStep.BuyLights:
                bossText.text = "BOSS: Now, press [E] on the Shop Terminal and buy a Stage Light. It will drop into the delivery zone.";
                break;
            case TutorialStep.SetupLight:
                bossText.text = "BOSS: Press [E] to pick up that Stage Light, aim it at the stage, and press [F] to turn it on.";
                break;
            case TutorialStep.BuyCameraAndCard:
                bossText.text = "BOSS: Looking good! Go back to the Shop Terminal and press [E] to buy a Film Camera and an SD Card.";
                break;
            case TutorialStep.InsertCardToCamera:
                bossText.text = "BOSS: Press [E] to pick up your new camera and the SD card. Press [C] to load the memory card into the camera.";
                break;
            case TutorialStep.RecordVideo:
                bossText.text = "BOSS: Press [F] to look through the camera, then hit [R] to record a quick shot of the cube. Eject the card when you're happy with the take.";
                break;
            case TutorialStep.InsertToComputer:
                bossText.text = "BOSS: Pick up the SD card with [E], bring it over to the Computer tower, and press [E] to insert it.";
                break;
            case TutorialStep.PlayRecording:
                bossText.text = "BOSS: Finally, press [E] on the computer screen and hit Play to review the footage!";
                break;
            case TutorialStep.Complete:
                bossText.text = "BOSS: Fantastic work! You've got a really great eye for this. Tutorial Complete!";
                if (okButton != null) okButton.SetActive(false);
                break;
            case TutorialStep.OfferLevel1:
                bossText.text = "BOSS: Alright, training is over. Flora & Form Home just offered us 60,000 B coins for a 20-second tabletop teaser of their new artisan vase. You want the job?";
                if (okButton != null) okButton.SetActive(true);
                if (skipButton != null) skipButton.SetActive(false);

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Player.PlayerController.PlayerController playerOffer = FindObjectOfType<Player.PlayerController.PlayerController>();
                if (playerOffer != null) playerOffer.canLook = false;
                break;
            case TutorialStep.Level1Accepted:
                bossText.text = "BOSS: Excellent! I've wired your 30,000 B coins upfront payment. I have spawned the vase on the stage for you. Time to get to work!";
                break;
        }
    }
}