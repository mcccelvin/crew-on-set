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

    public enum TutorialStep
    {
        Intro,
        BuildStageWall,      // 1. Tablet
        SetupLight,          // 2. Lights
        GrabCameraAndCard,   // 3. Camera & SD Card
        RecordVideo,         // 4. Recording
        InsertToComputer,    // 5. Computer
        Complete
    }

    public TutorialStep currentStep;

    // Safety lock so players can't skip ahead while the timer is counting down!
    private bool isTransitioning = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (bossHUDCanvas != null) bossHUDCanvas.SetActive(false);
        if (okButton != null) okButton.SetActive(false);

        StartCoroutine(StartTutorialWithDelay());
    }

    private IEnumerator StartTutorialWithDelay()
    {
        yield return new WaitForSeconds(5f);

        if (bossHUDCanvas != null) bossHUDCanvas.SetActive(true);
        if (okButton != null) okButton.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Player.PlayerController.PlayerController player = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (player != null) player.canLook = false;

        currentStep = TutorialStep.Intro;
        UpdateBossDialogue();
    }

    public void OnOkButtonPressed()
    {
        if (okButton != null) okButton.SetActive(false);

        HelpDesk stageSpawner = FindObjectOfType<HelpDesk>();
        if (stageSpawner != null)
        {
            stageSpawner.StartGameSequence();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        Player.PlayerController.PlayerController player = FindObjectOfType<Player.PlayerController.PlayerController>();
        if (player != null) player.canLook = true;

        // Use our 5-second transition to go to the Tablet step!
        StartCoroutine(TransitionToNextStep(TutorialStep.BuildStageWall));
    }

    // --- THE MASTER TIMER FOR ALL STEPS ---
    private IEnumerator TransitionToNextStep(TutorialStep nextStep)
    {
        isTransitioning = true; // Lock the tutorial

        // Hide the UI completely while the player takes a breather
        if (bossHUDCanvas != null) bossHUDCanvas.SetActive(false);

        // Wait exactly 5 seconds
        yield return new WaitForSeconds(5f);

        // Move to the next step, pop the UI back up, and update the text!
        currentStep = nextStep;
        if (bossHUDCanvas != null) bossHUDCanvas.SetActive(true);
        UpdateBossDialogue();

        isTransitioning = false; // Unlock the tutorial

        // --- NEW: If the tutorial is complete, wait 5 more seconds and hide the UI! ---
        if (currentStep == TutorialStep.Complete)
        {
            yield return new WaitForSeconds(5f);
            if (bossHUDCanvas != null) bossHUDCanvas.SetActive(false);
        }
    }

    // --- STEP 1: TABLET ---
    public void OnStageWallBuilt()
    {
        if (currentStep == TutorialStep.BuildStageWall && !isTransitioning)
        {
            StartCoroutine(TransitionToNextStep(TutorialStep.SetupLight));
        }
    }

    // --- STEP 2: LIGHTS ---
    public void OnLightTurnedOn()
    {
        if (currentStep == TutorialStep.SetupLight && !isTransitioning)
        {
            StartCoroutine(TransitionToNextStep(TutorialStep.GrabCameraAndCard));
        }
    }

    // --- STEP 3: CAMERA & CARD ---
    public void OnCameraGrabbed()
    {
        if (currentStep == TutorialStep.GrabCameraAndCard && !isTransitioning)
        {
            StartCoroutine(TransitionToNextStep(TutorialStep.RecordVideo));
        }
    }

    // --- STEP 4: RECORDING ---
    public void OnRecordingFinished()
    {
        if (currentStep == TutorialStep.RecordVideo && !isTransitioning)
        {
            StartCoroutine(TransitionToNextStep(TutorialStep.InsertToComputer));
        }
    }

    // --- STEP 5: COMPUTER ---
    public void OnVideoPlayed()
    {
        if (currentStep == TutorialStep.InsertToComputer && !isTransitioning)
        {
            StartCoroutine(TransitionToNextStep(TutorialStep.Complete));
        }
    }

    // --- THE BOSS'S DIALOGUE FLOW ---
    private void UpdateBossDialogue()
    {
        if (bossText == null) return;

        switch (currentStep)
        {
            case TutorialStep.Intro:
                bossText.text = "BOSS: Hey there, welcome to the studio! We are so glad you're here. I've set up a little practice object on the stage for your first commercial.";
                break;
            case TutorialStep.BuildStageWall:
                bossText.text = "BOSS: First things first, head over to the Editor Tablet and add a wall to the stage to give us a nice backdrop.";
                break;
            case TutorialStep.SetupLight:
                bossText.text = "BOSS: Perfect. Now let's get some light on the subject! Pick up a Stage Light, turn it on, and aim it at the stage.";
                break;
            case TutorialStep.GrabCameraAndCard:
                bossText.text = "BOSS: Looking great! Now go grab the Film Camera and an SD card from the equipment table. Let's make some movie magic!";
                break;
            case TutorialStep.RecordVideo:
                bossText.text = "BOSS: Awesome! Press 'C' to load the memory card into the camera, and hit 'R' to record a quick shot. Eject it when you're happy with it.";
                break;
            case TutorialStep.InsertToComputer:
                bossText.text = "BOSS: Great take! Now, just bring that SD card over to the computer, insert it, and hit Play. I can't wait to see what you shot!";
                break;
            case TutorialStep.Complete:
                bossText.text = "BOSS: Fantastic work! You've got a really great eye for this. Welcome to the team! Tutorial Complete!";
                break;
        }
    }
}